using UnityEngine;
using BossFight2D.Systems;
using System.Collections;

namespace BossFight2D.Player
{
  public class PlayerQuestionGuard : MonoBehaviour
  {
    PlayerController controller; PlayerCombat combat; PlayerHealth health;

    [Header("Freeze Timing")]
    [Tooltip("Extra time to keep input frozen after a correct answer to let the attack resolve smoothly.")]
    public float correctExtraFreeze = 0.4f;

    void Awake() { controller = GetComponent<PlayerController>(); combat = GetComponent<PlayerCombat>(); health = GetComponent<PlayerHealth>(); }
    void OnEnable() { EventBus.QuestionStarted += OnQuestionStarted; EventBus.AnswerSubmitted += OnAnswerOrTimeout; EventBus.QuestionTimeout += OnTimeout; EventBus.AnswerModeExited += OnAnswerModeExited; EventBus.GamePaused += OnGamePaused; EventBus.GameResumed += OnGameResumed; }
    void OnDisable() { EventBus.QuestionStarted -= OnQuestionStarted; EventBus.AnswerSubmitted -= OnAnswerOrTimeout; EventBus.QuestionTimeout -= OnTimeout; EventBus.AnswerModeExited -= OnAnswerModeExited; EventBus.GamePaused -= OnGamePaused; EventBus.GameResumed -= OnGameResumed; }

    void OnQuestionStarted(QuestionData q) { }
    void OnAnswerOrTimeout(int _, bool correct) { if (correct) { StartCoroutine(UnfreezeAfterCorrect()); } else { SetGuard(false); } }
    void OnTimeout() { SetGuard(false); }
    void OnAnswerModeExited() { SetGuard(false); }
    void OnGamePaused() { SetGuard(true); }
    void OnGameResumed() { SetGuard(false); }

    IEnumerator UnfreezeAfterCorrect() { yield return new WaitForSeconds(correctExtraFreeze); SetGuard(false); }

    void SetGuard(bool active)
    {
      if (controller != null) controller.inputEnabled = !active;
      if (combat != null)
      {
        combat.SetCombatEnabled(!active);
      }
      if (health != null) health.invincible = active;
    }
  }
}