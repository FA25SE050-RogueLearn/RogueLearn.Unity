/*
using UnityEngine;

namespace BossFight2D.Systems {
  public class LifelineSystem : MonoBehaviour {
    public BossFight2D.Player.PlayerFocus focus; public float freezeSeconds=3f;
    void Awake(){ if(focus==null) focus=UnityEngine.Object.FindFirstObjectByType<BossFight2D.Player.PlayerFocus>(); }
    public bool UseFiftyFifty(QuestionManager qm){ if(focus==null || !focus.Spend(1)) return false; /* UI should hide two wrong options  return true; }
    public bool UseFreeze(QuestionManager qm){ if(focus==null || !focus.Spend(1)) return false; StartCoroutine(FreezeRoutine(qm)); return true; }
    System.Collections.IEnumerator FreezeRoutine(QuestionManager qm){ float t=qm.RemainingTime; float end=Time.time+freezeSeconds; while(Time.time<end){ qm.RemainingTime=t; yield return null; } }
  }
}
*/