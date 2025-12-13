using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using BossFight2D.Player;

public class TestDamageButton : MonoBehaviour
{
    public PlayerController localPlayerController;
    public Button button;
    void Awake()
    {

        if (button != null)
        {
            button.onClick.AddListener(TestDamage);
        }

    }
    public void TestDamage()
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localPlayerController = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>();
        }
        if (localPlayerController != null)
        {
            //localPlayerController.TakeDamageServerRpc(10);
        }
    }
}
