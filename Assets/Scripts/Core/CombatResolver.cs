using UnityEngine;

namespace BossFight2D.Combat
{
  using BossFight2D.Boss;
  using BossFight2D.Systems;
  using BossFight2D.Player;
  using BossFight2D.Quiz;
  public static class CombatResolver
  {
    public static float baseEasy = 8f, baseMed = 12f, baseHard = 18f;
    public static float comboBonus = 0.15f;
    public static float maxMultiplier = 2.0f;
    public static float timeBonusMax = 0.5f; // up to +50% if answered instantly

    public static void ApplyAnswerDamage(QuestionData q, QuizManager qm)
    {
        var boss = Object.FindFirstObjectByType<BossHealth>();
        if (boss == null) return;

        float baseDmg = q.difficulty == "Easy" ? baseEasy : q.difficulty == "Hard" ? baseHard : baseMed;
        float timeRatio = Mathf.Clamp01(qm.RemainingTime.Value / Mathf.Max(0.01f, q.timeLimitSec));
        float timeMult = 1f + timeBonusMax * timeRatio;
        int dmg = Mathf.CeilToInt(baseDmg * timeMult);

        var playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        var playerCombat = playerHealth != null ? playerHealth.GetComponent<PlayerCombat>() : Object.FindFirstObjectByType<PlayerCombat>();

        boss.TakeDamage(dmg);

        if (playerCombat != null)
        {
            playerCombat.TriggerAttack();
        }
    }

    public static void ApplyPenalty(QuestionData q, QuizManager qm)
    {
        var boss = Object.FindFirstObjectByType<BossHealth>();
        var player = Object.FindFirstObjectByType<PlayerHealth>();
        if (boss != null)
        {
            // Boss doesn't take damage on penalty, maybe a different effect?
        }
        else
        {
            player?.Damage(1);
        }
    }
  }
}