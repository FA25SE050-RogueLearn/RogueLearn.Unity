using UnityEngine;
using BossFight2D.Systems;
using BossFight2D;
using BossFight2D.Player;
using BossFight2D.Core;
using BossFight2D.Effects;
using BossFight2D.Quiz;

namespace BossFight2D.Boss
{
    public class BossCombat : MonoBehaviour
    {
        private ReadyStation _readyStation;

        [Header("Game State")]
        [Tooltip("If true, the boss will patrol while a question is active. If false, it will stand still.")]
        public Animator animator;
        public BossFight2D.Combat.Hitbox2D hitbox;
        public float windup = 0.2f;
        public float hitboxWindow = 0.2f;
        [SerializeField] private Transform playerTransform;

        [Header("Timing")]
        [Tooltip("If true, rely on Animation Events (AnimationEvent_HitboxStart/End) to start/stop the hitbox. If false, use Invoke() based on windup/hitboxWindow.")]
        public bool useAnimationEvents = true;

        // Debug visualization
        [Header("Debug")]
        public Color debugTelegraphColor = Color.yellow;
        public Color debugAttackColor = Color.red;

        // Telegraph visuals
        [Header("Telegraph")]
        [Tooltip("Show a visual indicator at the attack location during windup so players can dodge.")]
        public bool showTelegraph = true;
        [Tooltip("Prefab to instantiate for the telegraph (e.g., a semi-transparent red circle). Optional but recommended.")]
        public GameObject telegraphPrefab;
        [Tooltip("Optional tint to apply to the telegraph instance if it has a SpriteRenderer.")]
        public Color telegraphTint = new Color(1f, 0f, 0f, 0.4f);
        GameObject _activeTelegraph;
        [Tooltip("If true, lock the attack position/rotation to the telegraphed prediction so the hitbox spawns exactly where hinted, without re-aiming at the player.")]
        public bool lockAttackToTelegraph = true;
        [Tooltip("Fraction of windup to keep the telegraph visible. Lower values make it shorter; range 0.05..1.")]
        [Range(0.05f, 2f)] public float telegraphLifetimeFactor = 0.35f;
        Vector3 _predictedHitboxWorldPos;
        Quaternion _predictedHitboxWorldRot;
        bool _hasPredicted;

        // Orientation
        [Header("Orientation")]
        public bool facePlayer = true;
        SpriteRenderer sr;

        // Melee Aiming
        [Header("Melee Aiming")]
        [Tooltip("Rotate hitbox to face the player when the attack starts (melee aim).")]
        public bool rotateHitboxTowardPlayer = true;
        [Tooltip("Z rotation offset (degrees) to apply after aiming at the player.")]
        public float hitboxAngleOffset = 0f;
        Quaternion _hitboxDefaultLocalRotation;

        [Tooltip("Move hitbox forward toward the player when the attack starts (world-space offset).")]
        public bool moveHitboxTowardPlayer = true;
        [Tooltip("Distance (units) to push the hitbox forward in the aimed direction during the attack.")]
        public float hitboxForwardDistance = 1f;
        Vector3 _hitboxDefaultLocalPosition;

        // Patrol during question
        [Header("Patrol During Question")]
        [Tooltip("If true, boss will patrol (orbit) around the player while the question is active.")]
        public bool patrolDuringQuestion = true;
        [Tooltip("Only patrol when currently within attack range of the player.")]
        public bool onlyPatrolWhenWithinRange = true;
        [Tooltip("Orbit radius around the player while patrolling.")]
        public float patrolRadius = 1.5f;
        [Tooltip("Max allowed distance from player to remain in attack range.")]
        public float attackRange = 2.0f;
        [Tooltip("Linear movement speed while patrolling (units/sec).")]
        public float patrolSpeed = 1.5f;
        Rigidbody2D rb;
        bool _patrolling;
        float _orbitAngle;

        // Chase outside safe zone
        [Header("Chase Settings")]
        [Tooltip("Move speed while chasing the player outside the safe zone.")]
        public float chaseSpeed = 2.5f;
        [Tooltip("Minimum distance at which boss begins attacks when chasing.")]
        public float chaseAttackDistance = 1.75f;
        [Tooltip("Duration (seconds) to stop chasing after a correct answer.")]
        public float correctAnswerGracePeriod = 3.0f;
        float _chaseCooldownUntil;
        float _correctAnswerGraceUntil;

        // Obstacle avoidance
        [Header("Obstacle Avoidance")]
        [Tooltip("If true, boss will plan a simple detour around the Station so it doesn't get stuck when the direct path crosses it.")]
        public bool avoidStationObstacle = true;
        [Tooltip("Extra margin around station bounds when planning detours.")]
        public float stationAvoidMargin = 0.2f;
        [Tooltip("Distance threshold to consider a detour waypoint reached.")]
        public float detourArriveDistance = 0.1f;
        bool _detourActive;
        Vector2 _detourTarget;

        // Flow flags
        bool _questionActive;
        bool _isPunishAttack;
        bool _inPowerPlay;
        GameManager gm;

        int _queuedDamage = 1;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitbox == null) hitbox = GetComponentInChildren<BossFight2D.Combat.Hitbox2D>();
            if (playerTransform == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
            if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
            if (hitbox != null)
            {
                _hitboxDefaultLocalRotation = hitbox.transform.localRotation;
                _hitboxDefaultLocalPosition = hitbox.transform.localPosition;
            }
            rb = GetComponent<Rigidbody2D>();
            gm = FindFirstObjectByType<GameManager>();
            _readyStation = FindObjectOfType<ReadyStation>();
        }

        void OnEnable()
        {
            _questionActive = true;
            if (!patrolDuringQuestion) { _patrolling = false; return; }
            if (onlyPatrolWhenWithinRange && playerTransform != null)
            {
                _patrolling = Vector2.Distance(playerTransform.position, transform.position) <= attackRange;
            }
            else
            {
                _patrolling = true;
            }
        }
        void OnAnswerSubmitted(int choice, bool correct)
        {
            _patrolling = false;
            _questionActive = false;
            _isPunishAttack = !correct;

            // If correct answer, grant grace period where boss won't chase
            if (correct)
            {
                _correctAnswerGraceUntil = Time.time + correctAnswerGracePeriod;
            }
        }
        void OnQuestionTimeout() { _patrolling = false; _questionActive = false; _isPunishAttack = false; }

        void OnPowerPlayStarted(float duration)
        {
            _inPowerPlay = true;
            // Ensure boss keeps moving during Power Play; clear any grace pause
            _correctAnswerGraceUntil = 0f;
        }
        void OnPowerPlayEnded()
        {
            _inPowerPlay = false;
        }

        public void QueueAttack(int damage, GameObject target = null)
        {
            // Suppress normal attacks while the player is in the safe zone (ready inside station) unless we are in punish flow
            if (QuizManager.Instance != null && QuizManager.Instance.State.Value != QuizState.Idle && !_isPunishAttack) return;
            _queuedDamage = damage;
            if (animator != null) animator.SetTrigger("Attack");
            if (hitbox != null && !useAnimationEvents)
            {
                Invoke(nameof(BeginHitbox), windup);
                Invoke(nameof(EndHitbox), windup + hitboxWindow);
            }
            // Draw telegraph line toward player during windup
            if (playerTransform != null)
            {
                Debug.DrawLine(transform.position, playerTransform.position, debugTelegraphColor, windup);
            }
            // Spawn telegraph indicator at predicted hit location
            if (showTelegraph)
            {
                ShowTelegraph(windup);
            }
        }

        void BeginHitbox()
        {
            // Remove telegraph when the attack starts
            HideTelegraph();
            if (hitbox != null)
            {
                if (lockAttackToTelegraph && _hasPredicted)
                {
                    hitbox.transform.rotation = _predictedHitboxWorldRot;
                    hitbox.transform.position = _predictedHitboxWorldPos;
                }
                else
                {
                    if (rotateHitboxTowardPlayer && playerTransform != null)
                    {
                        Vector3 dir = (playerTransform.position - transform.position);
                        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + hitboxAngleOffset;
                        hitbox.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    }
                    if (moveHitboxTowardPlayer)
                    {
                        // Base world position preserving default local offset
                        Vector3 baseWorld = transform.TransformPoint(_hitboxDefaultLocalPosition);
                        // Push forward along the aimed (right) vector
                        Vector3 forward = hitbox.transform.right;
                        hitbox.transform.position = baseWorld + forward * Mathf.Clamp((transform.position - playerTransform.position).magnitude, 0, hitboxForwardDistance);
                    }
                }
                hitbox.Activate(hitboxWindow, _queuedDamage);
            }
            // Draw active attack line toward player for hitbox window duration
            if (playerTransform != null)
            {
                Debug.DrawLine(transform.position, playerTransform.position, debugAttackColor, hitboxWindow);
            }
        }
        void EndHitbox() { if (hitbox != null) { hitbox.Deactivate(); hitbox.transform.localRotation = _hitboxDefaultLocalRotation; hitbox.transform.localPosition = _hitboxDefaultLocalPosition; } _queuedDamage = 1; if (_isPunishAttack) { EventBus.RaiseWrongAnswerChallengeEnded(); _isPunishAttack = false; } ResetPrediction(); }

        public void AnimationEvent_HitboxStart() { BeginHitbox(); }
        public void AnimationEvent_HitboxEnd() { EndHitbox(); }

        // Create a telegraph instance at the predicted hit position/rotation and scale it to the collider size
        void ShowTelegraph(float duration)
        {
            // If we already have one, refresh position/rotation/scale and reschedule removal
            if (_activeTelegraph == null)
            {
                if (telegraphPrefab == null)
                {
                    // No prefab provided; skip runtime visuals (Debug.DrawLine will still show in editor). You can assign a prefab in the Inspector.
                    return;
                }
                _activeTelegraph = Instantiate(telegraphPrefab);
            }

            // Compute predicted transform for the hitbox (position and rotation)
            Quaternion predictedRotation = hitbox != null ? hitbox.transform.localRotation : Quaternion.identity;
            if (hitbox != null && rotateHitboxTowardPlayer && playerTransform != null)
            {
                Vector3 dir = (playerTransform.position - transform.position);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + hitboxAngleOffset;
                predictedRotation = Quaternion.Euler(0f, 0f, angle);
            }

            Vector3 predictedPosition;
            if (hitbox != null && moveHitboxTowardPlayer)
            {
                Vector3 baseWorld = transform.TransformPoint(_hitboxDefaultLocalPosition);
                // Forward along the predictedRotation's right vector
                Vector3 forward = predictedRotation * Vector3.right;
                float forwardDist = playerTransform != null ? Mathf.Clamp((transform.position - playerTransform.position).magnitude, 0, hitboxForwardDistance) : hitboxForwardDistance;
                predictedPosition = baseWorld + forward * forwardDist;
            }
            else
            {
                predictedPosition = transform.TransformPoint(_hitboxDefaultLocalPosition);
            }

            _activeTelegraph.transform.position = predictedPosition;
            _activeTelegraph.transform.rotation = predictedRotation;
            // Cache predicted transform for locking the attack
            _predictedHitboxWorldPos = predictedPosition;
            _predictedHitboxWorldRot = predictedRotation;
            _hasPredicted = true;
            var srTele = _activeTelegraph.GetComponent<SpriteRenderer>();
            // Try to scale telegraph to collider size if possible
            if (hitbox != null)
            {
                var col = hitbox.GetComponent<Collider2D>();

                if (col is CircleCollider2D cc)
                {
                    // Desired world diameter considers hitbox transform scale
                    float scaleFactor = Mathf.Max(Mathf.Abs(hitbox.transform.lossyScale.x), Mathf.Abs(hitbox.transform.lossyScale.y));
                    float desiredWorldDiameter = cc.radius * 30f * scaleFactor;
                    if (srTele != null)
                    {
                        float spriteWorldWidth = srTele.bounds.size.x;
                        float multiplier = (spriteWorldWidth > 0.0001f) ? desiredWorldDiameter / spriteWorldWidth : 1f;
                        _activeTelegraph.transform.localScale = new Vector3(multiplier, multiplier, 1f);
                    }
                    else
                    {
                        _activeTelegraph.transform.localScale = new Vector3(desiredWorldDiameter, desiredWorldDiameter, 1f);
                    }
                }
                else if (col is BoxCollider2D bc)
                {
                    Vector2 desiredWorldSize = new Vector2(bc.size.x * Mathf.Abs(hitbox.transform.lossyScale.x), bc.size.y * Mathf.Abs(hitbox.transform.lossyScale.y));
                    if (srTele != null)
                    {
                        Vector2 spriteWorldSize = srTele.bounds.size;
                        float sx = (spriteWorldSize.x > 0.0001f) ? desiredWorldSize.x / spriteWorldSize.x : 1f;
                        float sy = (spriteWorldSize.y > 0.0001f) ? desiredWorldSize.y / spriteWorldSize.y : 1f;
                        _activeTelegraph.transform.localScale = new Vector3(sx, sy, 1f);
                    }
                    else
                    {
                        _activeTelegraph.transform.localScale = new Vector3(desiredWorldSize.x, desiredWorldSize.y, 1f);
                    }
                }
                else
                {
                    // Default scale
                    _activeTelegraph.transform.localScale = Vector3.one;
                }
            }

            // Optional tint if prefab has a SpriteRenderer
            if (srTele != null)
            {
                srTele.color = telegraphTint;
            }

            // Add a non-scaling pulse animation to make the telegraph more noticeable
            var pulse = _activeTelegraph.GetComponent<TelegraphPulse>();
            if (pulse == null) pulse = _activeTelegraph.AddComponent<TelegraphPulse>();
            pulse.target = srTele;
            pulse.baseColor = telegraphTint;
            pulse.pulseAlphaOnly = true; // do not change color hue by default
            pulse.alphaMin = Mathf.Min(telegraphTint.a * 0.6f, telegraphTint.a);
            pulse.alphaMax = telegraphTint.a * 1.2f;
            pulse.speed = 2.0f;
            pulse.duration = duration;

            // Schedule automatic removal using a shortened lifetime
            float effectiveDuration = Mathf.Max(0.05f, duration * telegraphLifetimeFactor);
            CancelInvoke(nameof(HideTelegraph));
            Invoke(nameof(HideTelegraph), effectiveDuration);

            // Ensure pulse runs for the same shortened lifetime
            if (pulse != null)
            {
                pulse.duration = effectiveDuration;
            }
        }

        void HideTelegraph()
        {
            if (_activeTelegraph != null)
            {
                Destroy(_activeTelegraph);
                _activeTelegraph = null;
            }
        }

        void ResetPrediction()
        {
            _hasPredicted = false;
            _predictedHitboxWorldPos = default;
            _predictedHitboxWorldRot = Quaternion.identity;
        }
        void Update()
        {
            if (facePlayer && playerTransform != null && sr != null)
            {
                bool faceRight = playerTransform.position.x >= transform.position.x;
                // If default sprite faces right, flipX should be true when facing left
                sr.flipX = !faceRight;
            }
        }

        void FixedUpdate()
        {
            // Effective safe zone gating: only suppress chase if the safe zone is active AND the player is actually inside station bounds.
            bool inQuiz = QuizManager.Instance != null && QuizManager.Instance.State.Value != QuizState.Idle;
            bool effectiveSafeZone = inQuiz && PlayerInsideStationBounds();
            // If safe zone active (and we're not in punish flow), do not chase. Boss can still orbit if patrolling was enabled by question.
            if (effectiveSafeZone && !_isPunishAttack)
            {
                if (_patrolling && playerTransform != null && rb != null)
                {
                    if (onlyPatrolWhenWithinRange && Vector2.Distance(playerTransform.position, transform.position) > attackRange)
                    {
                        _patrolling = false; return;
                    }
                    float radius = Mathf.Min(patrolRadius, attackRange);
                    float omega = radius > 0.001f ? (patrolSpeed / radius) : 0f;
                    _orbitAngle += omega * Time.fixedDeltaTime;
                    Vector2 desiredPos = (Vector2)playerTransform.position + new Vector2(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle)) * radius;
                    Vector2 next = Vector2.MoveTowards(rb.position, desiredPos, patrolSpeed * Time.fixedDeltaTime);
                    // Prevent entering station
                    next = KeepOutsideStation(next);
                    rb.MovePosition(next);
                }
                return;
            }

            // Check if we're in grace period after correct answer
            if (Time.time < _correctAnswerGraceUntil && !_isPunishAttack && !_inPowerPlay)
            {
                return; // Don't chase during grace period (but keep moving during Power Play)
            }

            // Not in safe zone (or we are in punish flow): chase the player and attack when in range
            if (playerTransform != null && rb != null)
            {
                Vector2 target = playerTransform.position;
                var col = _readyStation != null ? _readyStation.GetComponent<Collider2D>() : null;
                Rect r = default;
                if (col != null) { var b = col.bounds; r = new Rect(b.min, b.size); r.xMin -= stationAvoidMargin; r.yMin -= stationAvoidMargin; r.xMax += stationAvoidMargin; r.yMax += stationAvoidMargin; }

                // If detouring, move toward detour waypoint; drop detour once line to player is clear
                if (avoidStationObstacle && _detourActive)
                {
                    Vector2 next = Vector2.MoveTowards(rb.position, _detourTarget, chaseSpeed * Time.fixedDeltaTime);
                    next = KeepOutsideStation(next);
                    rb.MovePosition(next);
                    // Drop detour when reached or when a direct ray no longer hits the station
                    bool clearBySegment = (col == null || !SegmentIntersectsRect(rb.position, target, r));
                    bool clearByRay = true;
                    if (col != null)
                    {
                        Vector2 dir = (target - rb.position);
                        float dist = dir.magnitude;
                        if (dist > 0.001f)
                        {
                            int mask = 1 << col.gameObject.layer; var hit = Physics2D.Raycast(rb.position, dir.normalized, dist, mask);
                            clearByRay = !(hit.collider == col);
                        }
                    }
                    if (Vector2.Distance(rb.position, _detourTarget) <= detourArriveDistance || (clearBySegment && clearByRay))
                    {
                        _detourActive = false;
                    }
                }
                else
                {
                    // If direct path crosses the station, compute a detour waypoint
                    bool pathBlocked = false; Vector2 detour = Vector2.zero;
                    if (avoidStationObstacle && col != null)
                    {
                        // Prefer physics raycast test for robust blocking detection
                        Vector2 dir = (target - rb.position);
                        float dist = dir.magnitude;
                        if (dist > 0.001f)
                        {
                            int mask = 1 << col.gameObject.layer; var hit = Physics2D.Raycast(rb.position, dir.normalized, dist, mask);
                            if (hit.collider == col) { pathBlocked = true; detour = ComputeRaycastDetourTarget(hit, target); }
                        }
                        // Fallback to geometric segment test
                        if (!pathBlocked && SegmentIntersectsRect(rb.position, target, r))
                        {
                            pathBlocked = true; detour = ComputeDetourTarget(rb.position, target, r);
                        }
                    }
                    if (pathBlocked)
                    {
                        _detourTarget = detour;
                        _detourActive = true;
                        Vector2 next = Vector2.MoveTowards(rb.position, _detourTarget, chaseSpeed * Time.fixedDeltaTime);
                        next = KeepOutsideStation(next);
                        rb.MovePosition(next);
                    }
                    else
                    {
                        // Direct chase
                        Vector2 next = Vector2.MoveTowards(rb.position, target, chaseSpeed * Time.fixedDeltaTime);
                        next = KeepOutsideStation(next);
                        rb.MovePosition(next);
                    }
                }

                float distance = Vector2.Distance(rb.position, playerTransform.position);
                if (distance <= chaseAttackDistance && Time.time >= _chaseCooldownUntil)
                {
                    _chaseCooldownUntil = Time.time + (windup + hitboxWindow + 0.4f);
                    // During punish flow, attack is coordinated elsewhere; allow normal chase attack only if not punish attack
                    if (!_isPunishAttack)
                        QueueAttack(1);
                }
            }
        }

        Vector2 KeepOutsideStation(Vector2 proposed)
        {
            var col = _readyStation != null ? _readyStation.GetComponent<Collider2D>() : null;
            if (col == null) return proposed;
            var b = col.bounds;
            // If proposed point inside bounds, project to nearest edge
            if (proposed.x > b.min.x && proposed.x < b.max.x && proposed.y > b.min.y && proposed.y < b.max.y)
            {
                float dxMin = Mathf.Abs(proposed.x - b.min.x);
                float dxMax = Mathf.Abs(b.max.x - proposed.x);
                float dyMin = Mathf.Abs(proposed.y - b.min.y);
                float dyMax = Mathf.Abs(b.max.y - proposed.y);
                float minDist = Mathf.Min(dxMin, dxMax, dyMin, dyMax);
                float margin = 0.15f;
                if (minDist == dxMin) return new Vector2(b.min.x - margin, proposed.y);
                if (minDist == dxMax) return new Vector2(b.max.x + margin, proposed.y);
                if (minDist == dyMin) return new Vector2(proposed.x, b.min.y - margin);
                return new Vector2(proposed.x, b.max.y + margin);
            }
            return proposed;
        }

        bool PlayerInsideStationBounds()
        {
            var col = _readyStation != null ? _readyStation.GetComponent<Collider2D>() : null; 
            if (col == null || playerTransform == null) return false;
            var b = col.bounds; var p = playerTransform.position;
            return (p.x > b.min.x && p.x < b.max.x && p.y > b.min.y && p.y < b.max.y);
        }

        // Geometry helpers for simple detour planning
        bool SegmentIntersectsRect(Vector2 p1, Vector2 p2, Rect r)
        {
            if (r.Contains(p1) || r.Contains(p2)) return true;
            Vector2 tl = new Vector2(r.xMin, r.yMax);
            Vector2 tr = new Vector2(r.xMax, r.yMax);
            Vector2 bl = new Vector2(r.xMin, r.yMin);
            Vector2 br = new Vector2(r.xMax, r.yMin);
            return SegmentsIntersect(p1, p2, bl, br) || SegmentsIntersect(p1, p2, bl, tl) || SegmentsIntersect(p1, p2, tr, br) || SegmentsIntersect(p1, p2, tl, tr);
        }
        bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float o1 = Orientation(p1, p2, q1);
            float o2 = Orientation(p1, p2, q2);
            float o3 = Orientation(q1, q2, p1);
            float o4 = Orientation(q1, q2, p2);
            if (o1 * o2 < 0 && o3 * o4 < 0) return true;
            return false;
        }
        float Orientation(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }
        Vector2 ComputeDetourTarget(Vector2 current, Vector2 target, Rect r)
        {
            // Choose the nearest of two candidate corners on the side we’re approaching
            // Evaluate top vs bottom (or left vs right) detours and pick shorter total path length
            Vector2 bl = new Vector2(r.xMin, r.yMin);
            Vector2 tl = new Vector2(r.xMin, r.yMax);
            Vector2 br = new Vector2(r.xMax, r.yMin);
            Vector2 tr = new Vector2(r.xMax, r.yMax);
            float margin = stationAvoidMargin;
            // Candidates are the four outward-offset corners
            Vector2 c_bl = new Vector2(bl.x - margin, bl.y - margin);
            Vector2 c_tl = new Vector2(tl.x - margin, tl.y + margin);
            Vector2 c_br = new Vector2(br.x + margin, br.y - margin);
            Vector2 c_tr = new Vector2(tr.x + margin, tr.y + margin);
            // Score candidates by total path length current->corner + corner->target
            float s_bl = Vector2.Distance(current, c_bl) + Vector2.Distance(c_bl, target);
            float s_tl = Vector2.Distance(current, c_tl) + Vector2.Distance(c_tl, target);
            float s_br = Vector2.Distance(current, c_br) + Vector2.Distance(c_br, target);
            float s_tr = Vector2.Distance(current, c_tr) + Vector2.Distance(c_tr, target);
            float best = s_bl; Vector2 bestPt = c_bl;
            if (s_tl < best) { best = s_tl; bestPt = c_tl; }
            if (s_br < best) { best = s_br; bestPt = c_br; }
            if (s_tr < best) { best = s_tr; bestPt = c_tr; }
            return bestPt;
        }

        Vector2 ComputeRaycastDetourTarget(RaycastHit2D hit, Vector2 target)
        {
            // Step outward from the hit point along the surface normal, then choose a tangent direction closer to the target
            Vector2 p = hit.point;
            Vector2 n = hit.normal; // outward normal from the station surface
            float outward = stationAvoidMargin + 0.05f; // small buffer
            Vector2 basePt = p + n * outward;
            // Two tangents around the obstacle (rotate normal by ±90°)
            Vector2 t1 = new Vector2(-n.y, n.x);
            Vector2 t2 = -t1;
            float step = Mathf.Max(stationAvoidMargin * 2f, 0.25f);
            Vector2 cand1 = basePt + t1 * step;
            Vector2 cand2 = basePt + t2 * step;
            // Pick the candidate with shorter total path (basePt->cand + cand->target)
            float s1 = Vector2.Distance(basePt, cand1) + Vector2.Distance(cand1, target);
            float s2 = Vector2.Distance(basePt, cand2) + Vector2.Distance(cand2, target);
            return s1 <= s2 ? cand1 : cand2;
        }
    }
}