using UnityEngine;
using Cinemachine;

namespace BossFight2D.Effects
{
  // Switch from noise-based shake to Cinemachine Impulse for reliability with VCams
  public class CameraShake2D : MonoBehaviour
  {
    [Header("Impulse Settings (Medium)")]
    public float amplitude = 0.15f; // overall impulse strength

    [Header("Cinemachine References")]
    public CinemachineVirtualCamera vcam; // auto-found if not set
    public CinemachineImpulseSource impulseSource; // auto-added if missing
    CinemachineBrain _brain;

    void Awake()
    {
      // Cache CinemachineBrain and resolve the currently active VCam
      _brain = FindFirstObjectByType<CinemachineBrain>();
      if (vcam == null)
      {
        if (_brain != null && _brain.ActiveVirtualCamera != null)
        {
          var activeGo = _brain.ActiveVirtualCamera.VirtualCameraGameObject;
          vcam = activeGo.GetComponent<CinemachineVirtualCamera>();
        }
        if (vcam == null)
        {
          vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
        }
      }

      EnsureListenerOnActiveVCam();
      EnsureImpulseSource();
    }

    void Update()
    {
      if (Input.GetKeyDown(KeyCode.K))
      {
        OnPowerPlayHit();
      }
    }
    void OnEnable() { BossFight2D.Systems.EventBus.PowerPlayHitConfirmed += OnPowerPlayHit; EnsureListenerOnActiveVCam(); }
    void OnDisable() { BossFight2D.Systems.EventBus.PowerPlayHitConfirmed -= OnPowerPlayHit; }

    void OnPowerPlayHit()
    {
      // Refresh listener in case active VCam changed
      EnsureListenerOnActiveVCam();

      if (impulseSource == null)
      {
        Debug.LogWarning("CameraShake2D: No CinemachineImpulseSource found; adding one now.");
        EnsureImpulseSource();
      }
      // Use default upward velocity so GenerateImpulse(float) has effect even without a custom signal asset
      impulseSource.GenerateImpulse(amplitude);
    }

    void EnsureListenerOnActiveVCam()
    {
      GameObject camGO = null;
      if (_brain == null)
      {
        _brain = FindFirstObjectByType<CinemachineBrain>();
      }
      if (_brain != null && _brain.ActiveVirtualCamera != null)
      {
        camGO = _brain.ActiveVirtualCamera.VirtualCameraGameObject;
      }
      if (camGO == null && vcam != null)
      {
        camGO = vcam.gameObject;
      }
      if (camGO != null)
      {
        var listener = camGO.GetComponent<CinemachineImpulseListener>();
        if (listener == null)
        {
          listener = camGO.AddComponent<CinemachineImpulseListener>();
        }
        listener.m_Gain = 1f;
        listener.m_Use2DDistance = true;
      }
      else
      {
        Debug.LogWarning("CameraShake2D: Could not resolve an active CinemachineVirtualCamera. Ensure a VCam is active and the Main Camera has a CinemachineBrain.");
      }
    }

    void EnsureImpulseSource()
    {
      if (impulseSource == null)
      {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null)
        {
          impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }
      }
      impulseSource.m_DefaultVelocity = Vector3.up; // sensible default so float amplitude works
    }
  }
}
