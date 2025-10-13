// using UnityEngine;
// using System;
// using System.Collections;
// using System.Collections.Generic;

// namespace BossFight2D.Systems
// {


//     public class QuestionManager : MonoBehaviour
//     {
//         [Header("Load from Resources/QuestionPacks/questions_pack1.json (as TextAsset)")]
//         public TextAsset questionsJson;
//         public QuestionPackData Pack;
//         public int CurrentIndex = -1;
//         public float RemainingTime;
//         public bool QuestionActive;

//         [Header("References (optional)")]
//         public BossFight2D.Boss.BossStateMachine bossOverride;
//         public BossFight2D.Player.PlayerHealth playerOverride;
//         [Tooltip("If assigned, will be SetActive(true) on question start and false on answer/timeout")] public GameObject questionPanel;

//         [Header("Answer Bonuses")]
//         [Tooltip("Temporary bonus applied to the next correct answer (e.g., from perfect dodge). Value is a multiplier delta: 0.1 = +10%.")]
//         public float nextAnswerBonus = 0f;
//         public float perfectDodgeBonus = 0.1f;

//         // Manual progression state (press E to continue)
//         [Header("Manual Progression")]
//         [Tooltip("When true, question progression waits for the player to press E after combat window resolves.")]
//         public bool manualAdvance = false;
//         bool awaitingAdvance = false;

//         void OnEnable() { EventBus.PerfectDodgeSuccess += OnPerfectDodge; EventBus.GameStarted += OnGameStarted; EventBus.PowerPlayStarted += OnPowerPlayStarted; EventBus.PowerPlayEnded += OnPowerPlayEnded; }
//         void OnDisable() { EventBus.PerfectDodgeSuccess -= OnPerfectDodge; EventBus.GameStarted -= OnGameStarted; EventBus.PowerPlayStarted -= OnPowerPlayStarted; EventBus.PowerPlayEnded -= OnPowerPlayEnded; }

//         void OnPerfectDodge() { nextAnswerBonus = Mathf.Max(nextAnswerBonus, perfectDodgeBonus); }

//         void Start()
//         {
//             if (Pack == null)
//             {
//                 if (questionsJson == null) { questionsJson = Resources.Load<TextAsset>("QuestionPacks/questions_pack1"); }
//                 if (questionsJson != null) { var w = JsonUtility.FromJson<Wrapper>(questionsJson.text); Pack = w != null ? w.pack : null; }
//             }
//             // If the game is still in Init, wait for GameStarted
//             var gm = BossFight2D.Core.GameObjectFactory.FindOrCreate<BossFight2D.Core.GameManager>();
//             if (gm != null && gm.State == BossFight2D.Core.GameState.Init)
//             {
//                 if (questionPanel) questionPanel.SetActive(false);
//                 return;
//             }
//             NextQuestion();
//         }

//         void OnGameStarted()
//         {
//             // Reset and reload questions to ensure a fresh state on every new game (including replays)
//             if (questionsJson == null) { questionsJson = Resources.Load<TextAsset>("QuestionPacks/questions_pack1"); }
//             if (questionsJson != null) { var w = JsonUtility.FromJson<Wrapper>(questionsJson.text); Pack = w != null ? w.pack : null; }
//             CurrentIndex = -1;
//             QuestionActive = false;

//             // Start the first question when the game starts
//             if (!QuestionActive && (CurrentIndex < 0))
//             {
//                 NextQuestion();
//             }
//             // Ensure panel becomes visible with first question
//             if (questionPanel) questionPanel.SetActive(true);
//         }
//         public void NextQuestion()
//         {
//             // Only allow questions if player is inside station and ready
//             if (!ReadyStation.SafeZoneActive)
//             {
//                 // If not in safe zone, wait and try again
//                 StartCoroutine(WaitForSafeZoneAndStartQuestion());
//                 return;
//             }

//             NextQuestionDirect();
//         }

//         void NextQuestionDirect()
//         {
//             CurrentIndex++;
//             if (Pack == null || Pack.questions == null || CurrentIndex >= Pack.questions.Count)
//             {
//                 BossFight2D.Core.GameObjectFactory.FindOrCreate<BossFight2D.Core.GameManager>()?.WinGame();
//                 return;
//             }
//             var q = Pack.questions[CurrentIndex];
//             RemainingTime = q.timeLimitSec;
//             QuestionActive = true;
//             awaitingAdvance = false;
//             EventBus.RaiseAdvancePromptHidden();
//             EventBus.RaiseQuestionStarted(q);
//             // Ensure panel becomes visible with first question
//             if (questionPanel) questionPanel.SetActive(true);
//         }
//         void Update()
//         {
//             // Keep panel visibility controlled by question start/end only.
//             // Answering outside the station is already prevented in SubmitAnswer(),
//             // so we don't auto-hide the panel based on safe zone here to avoid flicker.

//             // Global cancel binding (ESC): allow exiting a question early only by spending an attack charge
//             if (Input.GetKeyDown(KeyCode.Escape))
//             {
//                 if (QuestionActive)
//                 {
//                     var pc = UnityEngine.Object.FindFirstObjectByType<BossFight2D.Player.PlayerCombat>();
//                     if (pc != null && pc.TryConsumeChargeForQuestionEscape())
//                     {
//                         CancelQuestionPhase();
//                     }
//                     else
//                     {
//                         // Not enough charges: ignore ESC during active question
//                         // Optional: provide feedback via UI/audio
//                     }
//                 }
//             }

//             if (QuestionActive)
//             {
//                 RemainingTime -= Time.deltaTime; if (RemainingTime <= 0f) { QuestionActive = false; EventBus.RaiseQuestionTimeout(); if (questionPanel) questionPanel.SetActive(false); ResetCombo(); if (manualAdvance) { StartCoroutine(PrepareAdvanceAfterTimeout()); } else { StartCoroutine(NextQuestionAfter(0.75f)); } }
//             }
//             else if (awaitingAdvance && manualAdvance)
//             {
//                 if (Input.GetKeyDown(KeyCode.E)) { awaitingAdvance = false; EventBus.RaiseAdvancePromptHidden(); NextQuestion(); }
//             }
//         }

//         // Public method to cancel the question phase manually
//         public void CancelQuestionPhase()
//         {
//             // If a question is active, stop it and hide the panel
//             if (QuestionActive)
//             {
//                 QuestionActive = false;
//                 if (questionPanel) questionPanel.SetActive(false);
//             }
//             // Clear any advance waiting prompt/state
//             if (awaitingAdvance)
//             {
//                 awaitingAdvance = false;
//                 EventBus.RaiseAdvancePromptHidden();
//             }
//             // Notify systems that answer mode has been exited (for station/UI coordination)
//             EventBus.RaiseAnswerModeExited();

//             // Resume the question flow: this will either start immediately if the player
//             // is in the safe zone, or wait until the player returns and toggles READY.
//             // This fixes the issue where ESC-cancel would hide the panel and never
//             // re-open the next question after returning to the station.
//             NextQuestion();
//         }
//         public void SubmitAnswer(int choice)
//         {
//             // Only allow answer submission if player is in safe zone
//             if (!ReadyStation.SafeZoneActive) return;

//             if (!QuestionActive) return;
//             var q = Pack.questions[CurrentIndex];
//             bool correct = choice == q.correctIndex;
//             QuestionActive = false;
//             EventBus.RaiseAnswerSubmitted(choice, correct);

//             // Hide panel immediately on any answer resolution
//             if (questionPanel) questionPanel.SetActive(false);

//             if (correct)
//             {
//                 // Start Power Play window before applying damage so the first hit benefits
//                 var ppm = BossFight2D.Core.GameObjectFactory.FindOrCreate<BossFight2D.Core.PowerPlayManager>();
//                 bool powerPlayStarted = false;
//                 if (ppm != null)
//                 {
//                     ppm.RequestPowerPlayServerRpc();
//                     powerPlayStarted = ppm.IsPowerPlayActive.Value; // may be false due to cooldown or missing manager
//                 }
//                 BossFight2D.Combat.CombatResolver.ApplyAnswerDamage(q, this);
//                 AddCombo();
//                 // If Power Play did not start (e.g., cooldown), auto-advance so flow doesn't stall
//                 if (!powerPlayStarted)
//                 {
//                     StartCoroutine(NextQuestionAfter(0.75f));
//                 }
//                 // Otherwise, wait for PowerPlayEnded to return player to station and continue next question
//             }
//             else { BossFight2D.Combat.CombatResolver.ApplyPenalty(q, this); ResetCombo(); if (manualAdvance) { StartCoroutine(PrepareAdvanceAfterWrong()); } else { StartCoroutine(NextQuestionAfterWrong()); } }
//         }
//         int combo = 0; public void AddCombo() { combo = Mathf.Min(combo + 1, 10); }
//         public void ResetCombo() { combo = 0; }
//         public int Combo => combo;

//         public float ConsumeNextAnswerBonus() { var v = nextAnswerBonus; nextAnswerBonus = 0f; return v; }

//         IEnumerator NextQuestionAfter(float delay)
//         {
//             yield return new WaitForSeconds(delay);
//             var boss = (bossOverride != null ? bossOverride : UnityEngine.Object.FindFirstObjectByType<BossFight2D.Boss.BossStateMachine>());
//             if (boss != null && boss.phase == BossFight2D.Boss.BossPhase.Dead) yield break;

//             // For auto-advance after correct answer, bypass safe zone check
//             NextQuestionDirect();
//         }

//         // Wait for boss wrong-answer challenge to end (attack finished or canceled) before starting the next question
//         IEnumerator NextQuestionAfterWrong()
//         {
//             bool ended = false;
//             System.Action handler = () => { ended = true; };
//             EventBus.WrongAnswerChallengeEnded += handler;
//             // Safety timeout in case event was raised immediately or boss has no combat; keep short to avoid hanging
//             float timeout = 2.0f; float end = Time.time + timeout;
//             while (!ended && Time.time < end) { yield return null; }
//             EventBus.WrongAnswerChallengeEnded -= handler;
//             // Small grace period for flow polish
//             yield return new WaitForSeconds(0.25f);
//             var boss = (bossOverride != null ? bossOverride : UnityEngine.Object.FindFirstObjectByType<BossFight2D.Boss.BossStateMachine>());
//             if (boss != null && boss.phase == BossFight2D.Boss.BossPhase.Dead) yield break;
//             // After wrong answer and punish window, resume with safe-zone gating
//             NextQuestion();
//         }

//         // Manual-advance variants
//         IEnumerator PrepareAdvanceAfterPowerPlay(BossFight2D.Core.PowerPlayManager ppm)
//         {
//             // Wait until power play window ends (consumed or timed out)
//             float safety = 6f; float until = Time.unscaledTime + safety;
//             while (ppm != null && ppm.IsPowerPlayActive.Value && Time.unscaledTime < until) { yield return null; }
//             yield return new WaitForSeconds(0.1f);
//             var boss = (bossOverride != null ? bossOverride : UnityEngine.Object.FindFirstObjectByType<BossFight2D.Boss.BossStateMachine>());
//             if (boss != null && boss.phase == BossFight2D.Boss.BossPhase.Dead) yield break;
//             // Deprecated: now we auto-continue on PowerPlayEnded
//             awaitingAdvance = false; EventBus.RaiseAdvancePromptHidden();
//         }

//         IEnumerator PrepareAdvanceAfterWrong()
//         {
//             bool ended = false; System.Action handler = () => { ended = true; };
//             EventBus.WrongAnswerChallengeEnded += handler; float timeout = 3.0f; float end = Time.time + timeout;
//             while (!ended && Time.time < end) { yield return null; }
//             EventBus.WrongAnswerChallengeEnded -= handler;
//             yield return new WaitForSeconds(0.1f);
//             var boss = (bossOverride != null ? bossOverride : UnityEngine.Object.FindFirstObjectByType<BossFight2D.Boss.BossStateMachine>());
//             if (boss != null && boss.phase == BossFight2D.Boss.BossPhase.Dead) yield break;
//             awaitingAdvance = true; EventBus.RaiseAdvancePromptShown();
//         }

//         IEnumerator PrepareAdvanceAfterTimeout()
//         {
//             // On timeout, there is no punish window; allow immediate advance
//             yield return new WaitForSeconds(0.1f);
//             awaitingAdvance = true; EventBus.RaiseAdvancePromptShown();
//         }

//         IEnumerator WaitForSafeZoneAndStartQuestion()
//         {
//             // Wait until player enters safe zone (station + ready)
//             while (!ReadyStation.SafeZoneActive)
//             {
//                 yield return new WaitForSeconds(0.1f);
//             }

//             // Now that player is in safe zone, start the question
//             CurrentIndex++;
//             if (Pack == null || Pack.questions == null || CurrentIndex >= Pack.questions.Count)
//             {
//                 BossFight2D.Core.GameObjectFactory.FindOrCreate<BossFight2D.Core.GameManager>()?.WinGame();
//                 yield break;
//             }
//             var q = Pack.questions[CurrentIndex];
//             RemainingTime = q.timeLimitSec;
//             QuestionActive = true;
//             awaitingAdvance = false;
//             EventBus.RaiseAdvancePromptHidden();
//             EventBus.RaiseQuestionStarted(q);
//             if (questionPanel) questionPanel.SetActive(true);
//         }

//         void OnPowerPlayStarted(float duration)
//         {
//             // Ensure question panel is hidden during power play (even if triggered externally)
//             if (questionPanel) questionPanel.SetActive(false);
//         }

//         void OnPowerPlayEnded()
//         {
//             // When power play ends (by hit or timeout), return the player to station and continue
//             var boss = (bossOverride != null ? bossOverride : UnityEngine.Object.FindFirstObjectByType<BossFight2D.Boss.BossStateMachine>());
//             if (boss != null && boss.phase == BossFight2D.Boss.BossPhase.Dead) return;

//             var station = UnityEngine.Object.FindFirstObjectByType<ReadyStation>();
//             if (station != null)
//             {
//                 station.ForceReturnPlayerAndReady();
//                 // Proceed to next question gated by safe zone (now active)
//                 NextQuestion();
//             }
//             else
//             {
//                 // Fallback: if station isn't present/active, continue questions without safe-zone gating
//                 Debug.LogWarning("QuestionManager: ReadyStation not found on PowerPlayEnded; continuing without safe-zone gating.");
//                 NextQuestionDirect();
//             }
//         }
//     }
// }
