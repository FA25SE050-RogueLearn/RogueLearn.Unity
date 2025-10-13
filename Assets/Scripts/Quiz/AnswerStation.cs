using UnityEngine;
using Unity.Netcode;

namespace BossFight2D.Quiz
{
    public class AnswerStation : NetworkBehaviour
    {
        [Tooltip("The answer option this station represents (e.g., 0 for A, 1 for B, etc.")]
        public int answerIndex;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsServer) return;

            if (other.CompareTag("Player"))
            {
                var playerNetworkObject = other.GetComponent<NetworkObject>();
                if (playerNetworkObject != null)
                {
                    QuizManager.Instance.SubmitAnswer(playerNetworkObject.OwnerClientId, answerIndex);
                }
            }
        }
    }
}