using UnityEngine;
using BossFight2D.Systems;

namespace BossFight2D.Player
{
  public class PlayerDodgeDetector : MonoBehaviour
  {
    PlayerController controller;
    float windowEnd = -1f;
    bool windowActive;

    void Awake() { controller = GetComponent<PlayerController>(); }
    void OnEnable() { EventBus.PerfectDodgeWindowStarted += OnWindowStart; EventBus.PerfectDodgeWindowEnded += OnWindowEnd; }
    void OnDisable() { EventBus.PerfectDodgeWindowStarted -= OnWindowStart; EventBus.PerfectDodgeWindowEnded -= OnWindowEnd; }

    void OnWindowStart(float duration) { windowEnd = Time.time + duration; windowActive = true; }
    void OnWindowEnd() { windowActive = false; }

    void Update()
    {
      if (!windowActive) return;
      // Detect dash start within window for success
      // We consider dash active if controller.dashing is true (internal field), so expose a method
      if (controller != null && controller.IsDashing())
      {
        windowActive = false;
        EventBus.RaisePerfectDodgeSuccess();
      }
      if (windowActive && Time.time >= windowEnd) { windowActive = false; }
    }
  }
}