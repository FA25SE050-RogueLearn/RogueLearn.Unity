using UnityEngine;
using BossFight2D.Systems;

public class DebugUI : MonoBehaviour
{
    int boss = 300, bossMax = 300;
    int hearts = 5, heartsMax = 5;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) { boss -= 50; EventBus.RaiseBossHealthChanged(boss, bossMax); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { hearts -= 1; EventBus.RaisePlayerHealthChanged(hearts, heartsMax); }
        //if (Input.GetKeyDown(KeyCode.T)) EventBus.RaiseTopicChanged("CS101: Data Structures"); // whatever your topic text event is
    }
}
