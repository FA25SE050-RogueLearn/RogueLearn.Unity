using UnityEngine;
using UnityEngine.UI;
using BossFight2D.Core;

// Simple manual trigger to start a Power Play window for testing or special ability usage
// Attach to a UI Button and configure duration in Inspector
public class PowerPlayButton : MonoBehaviour
{
    Button _btn;
    void Awake(){ _btn = GetComponent<Button>(); if(_btn!=null) _btn.onClick.AddListener(OnClick); }
    void OnDestroy(){ if(_btn!=null) _btn.onClick.RemoveListener(OnClick); }
    void OnClick(){ 
        if (PowerPlayManager.Instance != null)
        {
            PowerPlayManager.Instance.RequestPowerPlayServerRpc();
        }
    }
}