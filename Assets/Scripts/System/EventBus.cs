using System;
using UnityEngine;

namespace BossFight2D.Systems
{
  public static class EventBus
  {
    public static event Action GameStarted;
    public static event Action GameWon;
    public static event Action GameLost;
    public static event Action GamePaused; public static event Action GameResumed;
    public static event Action<QuestionData> QuestionStarted; public static event Action<int, bool> AnswerSubmitted; public static event Action QuestionTimeout; public static event Action AnswerResolved;
    public static event Action<float> PerfectDodgeWindowStarted;
    public static event Action PerfectDodgeWindowEnded;
    public static event Action PerfectDodgeSuccess;
    public static event Action WrongAnswerChallengeEnded;
    // Power Play events
    public static event Action<float> PowerPlayStarted;
    public static event Action PowerPlayEnded;
    public static event Action PowerPlayHitConfirmed;
    // Answer Mode lifecycle (for station/UI coordination)
    public static event Action AnswerModeExited;
    // Advance prompt events
    public static event Action AdvancePromptShown;
    public static event Action AdvancePromptHidden;

    // HUD events
    public static event Action<float, float> PlayerHealthChanged; // currentHealth, maxHealth
    public static event Action<int, int> ChargesChanged; // charges, max
    public static event Action<bool> QuestionAnswered; // correct
    public static event Action<float, float> BossHealthChanged; // currentHealth, maxHealth
    public static event Action<int, int> BossPhaseChanged; // phase, totalPhases
    public static event Action<string, Sprite> BossSpawned; // bossName, icon
    public static event Action LifelineUsed;
    public static event Action<string, string, Sprite, float> PowerUpActivated; // powerUpId, displayName, icon, duration
    public static event Action<string> PowerUpExpired; // powerUpId
    public static void RaiseGameStarted() => GameStarted?.Invoke();
    public static void RaiseGameWon() => GameWon?.Invoke();
    public static void RaiseGameLost() => GameLost?.Invoke();
    public static void RaiseGamePaused() => GamePaused?.Invoke();
    public static void RaiseGameResumed() => GameResumed?.Invoke();
    public static void RaiseQuestionStarted(QuestionData q) => QuestionStarted?.Invoke(q);
    public static void RaiseAnswerSubmitted(int selected, bool correct) => AnswerSubmitted?.Invoke(selected, correct);
    public static void RaiseQuestionTimeout() => QuestionTimeout?.Invoke();
    public static void RaiseAnswerResolved() => AnswerResolved?.Invoke();
    // Raisers for new events
    public static void RaisePerfectDodgeWindowStarted(float duration) => PerfectDodgeWindowStarted?.Invoke(duration);
    public static void RaisePerfectDodgeWindowEnded() => PerfectDodgeWindowEnded?.Invoke();
    public static void RaisePerfectDodgeSuccess() => PerfectDodgeSuccess?.Invoke();
    public static void RaiseWrongAnswerChallengeEnded() => WrongAnswerChallengeEnded?.Invoke();
    public static void RaisePowerPlayStarted(float duration) => PowerPlayStarted?.Invoke(duration);
    public static void RaisePowerPlayEnded() => PowerPlayEnded?.Invoke();
    public static void RaisePowerPlayHitConfirmed() => PowerPlayHitConfirmed?.Invoke();
    public static void RaiseAdvancePromptShown() => AdvancePromptShown?.Invoke();
    public static void RaiseAdvancePromptHidden() => AdvancePromptHidden?.Invoke();
    public static void RaiseAnswerModeExited() => AnswerModeExited?.Invoke();

    // HUD event raisers
    public static void RaisePlayerHealthChanged(float currentHealth, float maxHealth) => PlayerHealthChanged?.Invoke(currentHealth, maxHealth);
    public static void RaiseChargesChanged(int charges, int max) => ChargesChanged?.Invoke(charges, max);
    public static void RaiseQuestionAnswered(bool correct) => QuestionAnswered?.Invoke(correct);
    public static void RaiseBossHealthChanged(float currentHealth, float maxHealth) => BossHealthChanged?.Invoke(currentHealth, maxHealth);
    public static void RaiseBossPhaseChanged(int phase, int totalPhases) => BossPhaseChanged?.Invoke(phase, totalPhases);
    public static void RaiseBossSpawned(string bossName, Sprite icon = null) => BossSpawned?.Invoke(bossName, icon);
    public static void TriggerLifeline() => LifelineUsed?.Invoke();
    public static void RaisePowerUpActivated(string powerUpId, string displayName, Sprite icon, float duration) => PowerUpActivated?.Invoke(powerUpId, displayName, icon, duration);
    public static void TriggerPowerUpExpired(string powerUpId) => PowerUpExpired?.Invoke(powerUpId);
  }
}