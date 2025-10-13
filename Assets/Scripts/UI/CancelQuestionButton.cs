/*
using UnityEngine;
using UnityEngine.UI;
using BossFight2D.Systems;

// Attach to a UI Button; assign QuestionManager reference in Inspector
public class CancelQuestionButton : MonoBehaviour
{
    public QuestionManager questionManager;
    Button _btn;
    void Awake(){ _btn = GetComponent<Button>(); if(_btn!=null) _btn.onClick.AddListener(OnClick); }
    void OnDestroy(){ if(_btn!=null) _btn.onClick.RemoveListener(OnClick); }
    void OnClick(){ if(questionManager!=null) questionManager.CancelQuestionPhase(); }
    void Update()
    {
        // Auto-hide button when no active question or panel not visible
        if (questionManager == null) return;
        bool show = questionManager.QuestionActive;
        if (gameObject.activeSelf != show) gameObject.SetActive(show);
    }
}
*/