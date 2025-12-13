using UnityEngine;
using BossFight2D.Quiz;
using Unity.Netcode;

public class DevAnswerHotkeys : MonoBehaviour
{
    void Update()
    {
        if (QuizManager.Instance == null || QuizManager.Instance.State.Value != QuizState.Question) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SubmitAnswer(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SubmitAnswer(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SubmitAnswer(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SubmitAnswer(3);
    }

    private void SubmitAnswer(int index)
    {
        if (NetworkManager.Singleton.IsClient)
        {
            QuizManager.Instance.SubmitAnswer(NetworkManager.Singleton.LocalClientId, index);
        }
    }
}