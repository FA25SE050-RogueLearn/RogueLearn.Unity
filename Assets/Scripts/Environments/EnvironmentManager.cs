using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Netcode;

namespace BossFight2D.Environments
{
    /// <summary>
    /// Manages medieval environment atmosphere including:
    /// - Dynamic lighting (day/night, torch flicker)
    /// - Weather effects (particles)
    /// - Ambient sounds
    ///
    /// MULTIPLAYER READY:
    /// - Server authoritative lighting changes
    /// - All clients see synchronized atmosphere
    /// - Boss atmosphere changes propagate to all players
    ///
    /// Place on empty GameObject in scene.
    /// </summary>
    public class EnvironmentManager : NetworkBehaviour
    {
        [Header("Lighting")]
        [SerializeField] private Light2D globalLight;
        [SerializeField] private Color daytimeColor = new Color(1f, 0.96f, 0.88f); // Warm sunlight
        [SerializeField] private Color nightColor = new Color(0.5f, 0.6f, 0.8f, 0.7f); // Cool moonlight
        [SerializeField] private float dayIntensity = 1f;
        [SerializeField] private float nightIntensity = 0.5f;

        [Header("Torch Lights")]
        [SerializeField] private Light2D[] torchLights;
        [SerializeField] private bool enableTorchFlicker = true;
        [SerializeField] private float flickerSpeed = 10f;
        [SerializeField] private float flickerAmount = 0.1f;

        [Header("Weather Effects")]
        [SerializeField] private ParticleSystem dustParticles;
        [SerializeField] private ParticleSystem windParticles;
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private bool enableWeather = true;

        [Header("Time of Day")]
        [SerializeField] private bool enableDayNightCycle = false;
        [SerializeField] private float cycleDuration = 120f; // seconds for full day/night

        [Header("Boss Battle Atmosphere")]
        [SerializeField] private bool dimLightsOnBossSpawn = true;
        [SerializeField] private float bossLightIntensity = 0.6f;
        [SerializeField] private Color bossLightColor = new Color(0.8f, 0.4f, 0.4f); // Ominous red tint

        [Header("Network Settings")]
        [SerializeField] private bool showNetworkDebugLogs = false;

        // Network synchronized variables
        private NetworkVariable<TimeOfDay> currentTime = new NetworkVariable<TimeOfDay>(
            TimeOfDay.Day,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<bool> bossActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<bool> weatherActive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private float cycleTimer = 0f;
        private Color originalLightColor;
        private float originalLightIntensity;

        public enum TimeOfDay
        {
            Day,
            Dusk,
            Night,
            Dawn
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Subscribe to network variable changes (all clients)
            currentTime.OnValueChanged += OnTimeOfDayChanged;
            bossActive.OnValueChanged += OnBossAtmosphereChanged;
            weatherActive.OnValueChanged += OnWeatherChanged;

            // Initialize with current network state
            ApplyTimeOfDay(currentTime.Value);
            ApplyBossAtmosphere(bossActive.Value);
            ApplyWeatherState(weatherActive.Value);

            if (showNetworkDebugLogs)
            {
                Debug.Log($"[EnvironmentManager] NetworkSpawn - IsServer: {IsServer}, IsClient: {IsClient}");
            }
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from network variable changes
            currentTime.OnValueChanged -= OnTimeOfDayChanged;
            bossActive.OnValueChanged -= OnBossAtmosphereChanged;
            weatherActive.OnValueChanged -= OnWeatherChanged;

            base.OnNetworkDespawn();
        }

        void Start()
        {
            // Find global light if not assigned
            if (globalLight == null)
            {
                globalLight = FindFirstObjectByType<Light2D>();
            }

            if (globalLight != null)
            {
                originalLightColor = globalLight.color;
                originalLightIntensity = globalLight.intensity;
            }

            // Subscribe to boss events (server only handles logic)
            if (dimLightsOnBossSpawn)
            {
                BossFight2D.Systems.EventBus.BossSpawned += OnBossSpawned;
                BossFight2D.Systems.EventBus.GameWon += OnBossDefeated;
            }

            // For non-networked games (single-player or local)
            if (!IsSpawned)
            {
                ApplyTimeOfDay(TimeOfDay.Day);
            }
        }

        void OnDestroy()
        {
            if (dimLightsOnBossSpawn)
            {
                BossFight2D.Systems.EventBus.BossSpawned -= OnBossSpawned;
                BossFight2D.Systems.EventBus.GameWon -= OnBossDefeated;
            }
        }

        void Update()
        {
            // Day/Night cycle (server only updates state)
            if (enableDayNightCycle && !bossActive.Value)
            {
                if (IsServer || !IsSpawned)
                {
                    cycleTimer += Time.deltaTime;
                    float cycleProgress = (cycleTimer % cycleDuration) / cycleDuration;
                    UpdateDayNightCycle(cycleProgress);
                }
            }

            // Torch flicker (local effect, no sync needed)
            if (enableTorchFlicker)
            {
                UpdateTorchFlicker();
            }
        }

        private void UpdateDayNightCycle(float progress)
        {
            if (globalLight == null) return;

            // Determine which time of day based on progress
            TimeOfDay newTime;
            if (progress < 0.25f)
                newTime = TimeOfDay.Day;
            else if (progress < 0.5f)
                newTime = TimeOfDay.Dusk;
            else if (progress < 0.75f)
                newTime = TimeOfDay.Night;
            else
                newTime = TimeOfDay.Dawn;

            // Update network variable if server
            if (IsServer && currentTime.Value != newTime)
            {
                currentTime.Value = newTime;
            }
            else if (!IsSpawned)
            {
                // Non-networked game
                ApplyTimeOfDay(newTime);
            }
        }

        private void UpdateTorchFlicker()
        {
            if (torchLights == null || torchLights.Length == 0) return;

            foreach (var torch in torchLights)
            {
                if (torch == null) continue;

                // Perlin noise for smooth flicker
                float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, torch.GetInstanceID());
                float flicker = 1f + (noise - 0.5f) * flickerAmount;

                torch.intensity = torch.intensity * flicker;
            }
        }

        #region Network Callbacks

        private void OnTimeOfDayChanged(TimeOfDay oldValue, TimeOfDay newValue)
        {
            ApplyTimeOfDay(newValue);

            if (showNetworkDebugLogs)
            {
                Debug.Log($"[EnvironmentManager] Time changed: {oldValue} -> {newValue}");
            }
        }

        private void OnBossAtmosphereChanged(bool oldValue, bool newValue)
        {
            ApplyBossAtmosphere(newValue);

            if (showNetworkDebugLogs)
            {
                Debug.Log($"[EnvironmentManager] Boss atmosphere: {newValue}");
            }
        }

        private void OnWeatherChanged(bool oldValue, bool newValue)
        {
            ApplyWeatherState(newValue);

            if (showNetworkDebugLogs)
            {
                Debug.Log($"[EnvironmentManager] Weather active: {newValue}");
            }
        }

        #endregion

        #region Apply State (Client-Side Visual Updates)

        private void ApplyTimeOfDay(TimeOfDay time)
        {
            if (globalLight == null) return;

            switch (time)
            {
                case TimeOfDay.Day:
                    globalLight.color = daytimeColor;
                    globalLight.intensity = dayIntensity;
                    break;
                case TimeOfDay.Dusk:
                    globalLight.color = Color.Lerp(daytimeColor, nightColor, 0.5f);
                    globalLight.intensity = Mathf.Lerp(dayIntensity, nightIntensity, 0.5f);
                    break;
                case TimeOfDay.Night:
                    globalLight.color = nightColor;
                    globalLight.intensity = nightIntensity;
                    break;
                case TimeOfDay.Dawn:
                    globalLight.color = Color.Lerp(nightColor, daytimeColor, 0.5f);
                    globalLight.intensity = Mathf.Lerp(nightIntensity, dayIntensity, 0.5f);
                    break;
            }
        }

        private void ApplyBossAtmosphere(bool active)
        {
            if (globalLight == null) return;

            if (active)
            {
                // Dim and tint lights for dramatic effect
                StartCoroutine(TransitionLight(globalLight.color, bossLightColor, 1f));
                StartCoroutine(TransitionIntensity(globalLight.intensity, bossLightIntensity, 1f));
            }
            else
            {
                // Restore original lighting
                StartCoroutine(TransitionLight(globalLight.color, originalLightColor, 1f));
                StartCoroutine(TransitionIntensity(globalLight.intensity, originalLightIntensity, 1f));
            }
        }

        private void ApplyWeatherState(bool active)
        {
            if (dustParticles != null)
            {
                if (active && !dustParticles.isPlaying)
                    dustParticles.Play();
                else if (!active && dustParticles.isPlaying)
                    dustParticles.Stop();
            }

            if (windParticles != null)
            {
                if (active && !windParticles.isPlaying)
                    windParticles.Play();
                else if (!active && windParticles.isPlaying)
                    windParticles.Stop();
            }

            if (rainParticles != null)
            {
                if (active && !rainParticles.isPlaying)
                    rainParticles.Play();
                else if (!active && rainParticles.isPlaying)
                    rainParticles.Stop();
            }
        }

        #endregion

        #region Public Methods (Server RPC)

        /// <summary>
        /// Manually set time of day (server only or non-networked)
        /// </summary>
        public void SetTimeOfDay(TimeOfDay time)
        {
            if (IsSpawned)
            {
                // Networked game - use ServerRpc
                if (IsServer)
                {
                    currentTime.Value = time;
                }
                else
                {
                    SetTimeOfDayServerRpc(time);
                }
            }
            else
            {
                // Non-networked game - apply directly
                ApplyTimeOfDay(time);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetTimeOfDayServerRpc(TimeOfDay time)
        {
            currentTime.Value = time;
        }

        /// <summary>
        /// Enable/disable weather particles (server only or non-networked)
        /// </summary>
        public void SetWeatherActive(bool active)
        {
            if (IsSpawned)
            {
                // Networked game - use ServerRpc
                if (IsServer)
                {
                    weatherActive.Value = active;
                }
                else
                {
                    SetWeatherActiveServerRpc(active);
                }
            }
            else
            {
                // Non-networked game - apply directly
                enableWeather = active;
                ApplyWeatherState(active);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetWeatherActiveServerRpc(bool active)
        {
            weatherActive.Value = active;
        }

        /// <summary>
        /// Change to boss battle atmosphere (server only or non-networked)
        /// </summary>
        public void SetBossAtmosphere(bool active)
        {
            if (IsSpawned)
            {
                // Networked game - use ServerRpc
                if (IsServer)
                {
                    bossActive.Value = active;
                }
                else
                {
                    SetBossAtmosphereServerRpc(active);
                }
            }
            else
            {
                // Non-networked game - apply directly
                ApplyBossAtmosphere(active);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetBossAtmosphereServerRpc(bool active)
        {
            bossActive.Value = active;
        }

        #endregion

        #region Event Handlers

        private void OnBossSpawned(string bossName, Sprite icon)
        {
            SetBossAtmosphere(true);
            Debug.Log($"[EnvironmentManager] Boss spawned: {bossName} - Atmosphere changed");
        }

        private void OnBossDefeated()
        {
            SetBossAtmosphere(false);
            Debug.Log("[EnvironmentManager] Boss defeated - Atmosphere restored");
        }

        #endregion

        #region Coroutines

        private System.Collections.IEnumerator TransitionLight(Color from, Color to, float duration)
        {
            if (globalLight == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                globalLight.color = Color.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            globalLight.color = to;
        }

        private System.Collections.IEnumerator TransitionIntensity(float from, float to, float duration)
        {
            if (globalLight == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                globalLight.intensity = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            globalLight.intensity = to;
        }

        #endregion

#if UNITY_EDITOR
        [ContextMenu("Test Boss Atmosphere")]
        private void TestBossAtmosphere()
        {
            SetBossAtmosphere(true);
        }

        [ContextMenu("Test Normal Atmosphere")]
        private void TestNormalAtmosphere()
        {
            SetBossAtmosphere(false);
        }

        [ContextMenu("Test Day")]
        private void TestDay()
        {
            SetTimeOfDay(TimeOfDay.Day);
        }

        [ContextMenu("Test Night")]
        private void TestNight()
        {
            SetTimeOfDay(TimeOfDay.Night);
        }
#endif
    }
}
