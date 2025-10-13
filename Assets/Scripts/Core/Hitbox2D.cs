using UnityEngine;
using System.Collections.Generic;

namespace BossFight2D.Combat
{
  [RequireComponent(typeof(Collider2D))]
  public class Hitbox2D : MonoBehaviour
  {
    public int defaultDamage = 1;
    public GameObject owner;
    public bool disableColliderWhenInactive = true;
    public float autoDeactivateSeconds = -1f;

    // Debug visualization
    public bool debugDrawActive = true;
    public Color debugColor = Color.magenta;
    public int debugCircleSegments = 16;
    public bool debugLogHits = false;
    [Header("Target Filtering")]
    [Tooltip("Only apply damage when colliding with a Hurtbox2D marker.")]
    public bool requireHurtbox = true;
    [Tooltip("Valid target layers for this hitbox. Defaults to Everything.")]
    public LayerMask targetLayers = ~0;

    int _currentDamage;
    bool _active;
    float _deactivateAt;
    Collider2D _col;
    HashSet<Collider2D> _hitSet = new HashSet<Collider2D>();

    void Awake()
    {
      _col = GetComponent<Collider2D>();
      if (_col) _col.isTrigger = true;
      if (disableColliderWhenInactive && _col) _col.enabled = false;
      if (owner == null) owner = transform.root != null ? transform.root.gameObject : null;
    }

    void OnEnable() { _hitSet.Clear(); }

    void Update() { if (_active && autoDeactivateSeconds > 0f && Time.time >= _deactivateAt) { Deactivate(); } }

    public void Activate(float duration = -1f, int damageOverride = -1)
    {
      _active = true;
      _currentDamage = (damageOverride >= 0 ? damageOverride : defaultDamage);
      _hitSet.Clear();
      if (disableColliderWhenInactive && _col) _col.enabled = true;
      if (duration > 0f) { autoDeactivateSeconds = duration; _deactivateAt = Time.time + duration; }
      // Debug draw the hitbox shape while active
      if (debugDrawActive) DrawDebug(duration > 0f ? duration : 0.2f);
    }

    public void Deactivate()
    {
      _active = false;
      if (disableColliderWhenInactive && _col) _col.enabled = false;
      _hitSet.Clear();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
      if (!_active) return;
      if (owner != null && other.transform.IsChildOf(owner.transform)) return;
      if (_hitSet.Contains(other)) return;
      // Layer mask filter
      if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;
      // Hurtbox requirement
      IDamageable dmg = null;
      if (requireHurtbox)
      {
        var hb = other.GetComponent<Hurtbox2D>();
        if (hb == null) return;
        Debug.Log($"[Hitbox2D] {name} hit {other.name} with {_currentDamage} damage");
        dmg = hb.GetDamageable();
      }
      else
      {
        dmg = other.GetComponentInParent<IDamageable>();
        if (dmg == null) return;
        Debug.Log($"[Hitbox2D] {name} hit {other.name} with {_currentDamage} damage");
      }
      if (dmg != null)
      {
        // Apply Power Play bonus only when striking the boss, and consume window on successful hit
        int applied = _currentDamage;
        var boss = other.GetComponentInParent<BossHealth>();
        var ppm = BossFight2D.Core.PowerPlayManager.Instance;
        if (ppm != null && boss != null) { applied = ppm.ModifyDamageOnBossHit(applied); BossFight2D.Systems.EventBus.RaisePowerPlayHitConfirmed(); }
        dmg.TakeDamage(applied);
        if (debugLogHits) Debug.Log($"[Hitbox2D] {name} hit {other.name} for {applied}");
        _hitSet.Add(other);
      }
    }

    // Also handle the case where the target was already overlapping when the hitbox activated
    void OnTriggerStay2D(Collider2D other)
    {
      if (!_active) return;
      if (owner != null && other.transform.IsChildOf(owner.transform)) return;
      if (_hitSet.Contains(other)) return;
      // Layer mask filter
      if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;
      // Hurtbox requirement
      IDamageable dmg = null;
      if (requireHurtbox)
      {
        var hb = other.GetComponentInParent<BossFight2D.Combat.Hurtbox2D>();
        if (hb == null) return;
        dmg = hb.GetDamageable();
      }
      else
      {
        dmg = other.GetComponentInParent<IDamageable>();
      }
      if (dmg != null)
      {
        int applied = _currentDamage;
        var boss = other.GetComponent<BossHealth>();
        var ppm = BossFight2D.Core.PowerPlayManager.Instance;
        if (ppm != null && boss != null) { applied = ppm.ModifyDamageOnBossHit(applied); BossFight2D.Systems.EventBus.RaisePowerPlayHitConfirmed(); }
        dmg.TakeDamage(applied);
        if (debugLogHits) Debug.Log($"[Hitbox2D] (Stay) {name} hit {other.name} for {applied}");
        _hitSet.Add(other);
      }
    }

    // Draw the collider outline in world space for a given duration
    void DrawDebug(float duration)
    {
      if (_col == null) return;
      var bc = _col as BoxCollider2D;
      if (bc != null)
      {
        Vector2 size = bc.size;
        Vector2 offset = bc.offset;
        Vector3 c = transform.TransformPoint(offset);
        Vector3 right = transform.right * size.x * 0.5f;
        Vector3 up = transform.up * size.y * 0.5f;
        Vector3 p1 = c - right - up;
        Vector3 p2 = c + right - up;
        Vector3 p3 = c + right + up;
        Vector3 p4 = c - right + up;
        Debug.DrawLine(p1, p2, debugColor, duration);
        Debug.DrawLine(p2, p3, debugColor, duration);
        Debug.DrawLine(p3, p4, debugColor, duration);
        Debug.DrawLine(p4, p1, debugColor, duration);
        return;
      }
      var cc = _col as CircleCollider2D;
      if (cc != null)
      {
        Vector3 center = transform.TransformPoint(cc.offset);
        float radius = cc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        int segs = Mathf.Max(8, debugCircleSegments);
        Vector3 prev = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segs; i++)
        {
          float ang = i * Mathf.PI * 2f / segs;
          Vector3 next = center + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0);
          Debug.DrawLine(prev, next, debugColor, duration);
          prev = next;
        }
        return;
      }
      // Fallback: draw collider bounds
      var b = _col.bounds;
      Vector3 p1b = new Vector3(b.min.x, b.min.y, 0);
      Vector3 p2b = new Vector3(b.max.x, b.min.y, 0);
      Vector3 p3b = new Vector3(b.max.x, b.max.y, 0);
      Vector3 p4b = new Vector3(b.min.x, b.max.y, 0);
      Debug.DrawLine(p1b, p2b, debugColor, duration);
      Debug.DrawLine(p2b, p3b, debugColor, duration);
      Debug.DrawLine(p3b, p4b, debugColor, duration);
      Debug.DrawLine(p4b, p1b, debugColor, duration);
    }
  }
}