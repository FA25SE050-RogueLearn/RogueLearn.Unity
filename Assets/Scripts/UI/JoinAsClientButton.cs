using UnityEngine;
using UnityEngine.UI;
using BossFight2D.Systems;
using UnityEngine.Networking;
using Unity.Netcode;

public class JoinAsClientButton : MonoBehaviour
{
    public Button joinAsClientButton;

    private void Awake()
    {
        joinAsClientButton.onClick.AddListener(JoinAsClient);
    }

    public void JoinAsClient()
    {
        FindObjectOfType<NetworkManager>().StartClient();

    }
}