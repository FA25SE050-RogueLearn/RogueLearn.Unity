using UnityEngine;
using BossFight2D.Systems;
using BossFight2D.Core;
using Unity.Netcode.Components;

namespace BossFight2D.Boss
{
  public enum BossPhase { Phase1, Transition, Phase2, Dead }
  public class BossStateMachine : MonoBehaviour, BossFight2D.Combat.IDamageable
  {
    public string bossName = "The Professor";
    public BossPhase phase = BossPhase.Phase1;
    public BossCombat combat;
    private BossHealth bossHealth;

    [Header("Wrong Answer Telegraph")]
    public float wrongTelegraph = 0.9f;
    public float perfectWindow = 0.35f;

    public Animator animator;
    bool perfectSuccess;
    bool deathTriggered;

    void Awake()
    {
      if (combat == null) combat = GetComponent<BossCombat>();
      bossHealth = GetComponent<BossHealth>();
    }

    void Start()
    {
      // Notify HUD that boss has spawned
      EventBus.RaiseBossSpawned(bossName, null);
    }

    void OnEnable() { EventBus.PerfectDodgeSuccess += OnPerfectDodgeSuccess; EventBus.GamePaused += OnGamePaused; EventBus.GameResumed += OnGameResumed; EventBus.GameWon += OnGameWon; }
    void OnDisable() { EventBus.PerfectDodgeSuccess -= OnPerfectDodgeSuccess; EventBus.GamePaused -= OnGamePaused; EventBus.GameResumed -= OnGameResumed; EventBus.GameWon -= OnGameWon; }

    void OnPerfectDodgeSuccess() { perfectSuccess = true; }

    public void ApplyDamage(int dmg)
    {
      if (phase == BossPhase.Dead) return;

      bossHealth.TakeDamage(dmg);

      if (bossHealth.currentHealth.Value <= 0)
      {
        phase = BossPhase.Dead;
      }
      else if (bossHealth.currentHealth.Value <= 500 && phase == BossPhase.Phase1)
      {
        phase = BossPhase.Transition;
        Invoke(nameof(EnterPhase2), 1.0f);
      }
    }

    void EnterPhase2()
    {
      phase = BossPhase.Phase2;
      // Notify HUD of phase change
      EventBus.RaiseBossPhaseChanged(2, 2);
    }

    public void OnWrongAnswer()
    {
      // Start telegraph, then open a perfect dodge window
      perfectSuccess = false;
      if (combat != null)
      {
        Invoke(nameof(BeginPerfectWindow), wrongTelegraph);
        Invoke(nameof(EndPerfectWindowAndResolve), wrongTelegraph + perfectWindow);
        EventBus.RaisePerfectDodgeWindowStarted(perfectWindow);
      }
      else
      {
        // No combat component, end challenge immediately
        EventBus.RaisePerfectDodgeWindowStarted(perfectWindow);
        EventBus.RaisePerfectDodgeWindowEnded();
        EventBus.RaiseWrongAnswerChallengeEnded();
      }
    }

    void BeginPerfectWindow() { /* window open */ }
    void EndPerfectWindowAndResolve()
    {
      EventBus.RaisePerfectDodgeWindowEnded();
      if (perfectSuccess)
      {
        // Player succeeded: no attack, end challenge now
        EventBus.RaiseWrongAnswerChallengeEnded();
        return;
      }
      if (combat != null)
      {
        // Attack will run; end signal will be raised by BossCombat when hitbox ends
        combat.QueueAttack(1);
      }
      else
      {
        // Safety net if combat missing
        EventBus.RaiseWrongAnswerChallengeEnded();
      }
    }

    void OnGamePaused()
    {
      // Freeze combat and cancel any pending invocations during pause
      if (combat != null) combat.enabled = false;
      CancelInvoke();
      // Ensure boss combat/Hitbox are disabled after victory to prevent stray interactions
      if (combat != null)
      {
        combat.enabled = false;
        if (combat.hitbox != null) combat.hitbox.Deactivate();
      }
    }
    void OnGameResumed()
    {
      if (combat != null) combat.enabled = true;
      // Do not re-schedule previous invokes; resume normal flow
    }

    public void TakeDamage(int amount) { ApplyDamage(amount); }

    void OnGameWon()
    {
      if (deathTriggered) return;
      deathTriggered = true;

      var na = GetComponent<NetworkAnimator>();

      var nm = Unity.Netcode.NetworkManager.Singleton;
      if (nm != null)
      {
        if (nm.IsServer)
        {
          if (na != null) na.SetTrigger("Death");
          else if (animator != null) animator.SetTrigger("Death");
        }
        else
        {
          if (na == null && animator != null) animator.SetTrigger("Death");
        }
      }
      else
      {
        if (animator != null) animator.SetTrigger("Death");
      }

      // Ensure boss combat/Hitbox are disabled after victory to prevent stray interactions
      if (combat != null)
      {
        combat.enabled = false;
        if (combat.hitbox != null) combat.hitbox.Deactivate();
      }
      // End any pending invocations when game is won
      CancelInvoke();
    }

  }
}
