using UnityEngine;

// Attach to any Host-only UI root to hide it in WebGL builds.
public class PlatformUIHide : MonoBehaviour
{
    [SerializeField] private bool hideInWebGL = true;

    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (hideInWebGL)
        {
            gameObject.SetActive(false);
        }
#endif
    }
}