using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace BossFight2D.UI
{
    /// <summary>
    /// Adds visual enhancements to UI buttons including:
    /// - Hover scale effect
    /// - Press animation
    /// - Sound effects (optional)
    /// - Particle effects on click (optional)
    /// Attach to any Button for instant visual polish.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonEnhancer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Scale Animation")]
        [SerializeField] private bool enableHoverScale = true;
        [SerializeField] private Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1f);
        [SerializeField] private float scaleSpeed = 10f;

        [Header("Press Animation")]
        [SerializeField] private bool enablePressAnimation = true;
        [SerializeField] private Vector3 pressScale = new Vector3(0.95f, 0.95f, 1f);

        [Header("Glow Effect")]
        [SerializeField] private bool enableGlow = false;
        [SerializeField] private Image glowImage;
        [SerializeField] private float glowIntensity = 0.5f;

        [Header("Color Transition")]
        [SerializeField] private bool useThemeColors = true;
        [SerializeField] private bool overrideButtonColors = false;

        [Header("Sound Effects (Optional)")]
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private float soundVolume = 0.5f;

        [Header("Particle Effects (Optional)")]
        [SerializeField] private GameObject clickParticlePrefab;

        private Button button;
        private Vector3 originalScale;
        private Vector3 targetScale;
        private bool isHovering = false;
        private bool isPressed = false;
        private AudioSource audioSource;

        void Awake()
        {
            button = GetComponent<Button>();
            originalScale = transform.localScale;
            targetScale = originalScale;

            // Setup audio source if sound effects are provided
            if (hoverSound != null || clickSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.volume = soundVolume;
            }

            // Apply theme colors if enabled
            if (useThemeColors && overrideButtonColors && button != null)
            {
                var colors = button.colors;
                colors.normalColor = UIThemeManager.Instance.buttonNormal;
                colors.highlightedColor = UIThemeManager.Instance.buttonHover;
                colors.pressedColor = UIThemeManager.Instance.buttonPressed;
                colors.disabledColor = UIThemeManager.Instance.buttonDisabled;
                colors.fadeDuration = UIThemeManager.Instance.transitionFast;
                button.colors = colors;
            }
        }

        void Update()
        {
            // Smooth scale animation
            if (enableHoverScale || enablePressAnimation)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleSpeed);
            }

            // Glow effect
            if (enableGlow && glowImage != null)
            {
                float alpha = isHovering ? glowIntensity : 0f;
                var color = glowImage.color;
                color.a = Mathf.Lerp(color.a, alpha, Time.unscaledDeltaTime * scaleSpeed);
                glowImage.color = color;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;

            isHovering = true;

            if (enableHoverScale && !isPressed)
            {
                targetScale = Vector3.Scale(originalScale, hoverScale);
            }

            // Play hover sound
            if (audioSource != null && hoverSound != null)
            {
                audioSource.PlayOneShot(hoverSound);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;

            if (enableHoverScale && !isPressed)
            {
                targetScale = originalScale;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;

            isPressed = true;

            if (enablePressAnimation)
            {
                targetScale = Vector3.Scale(originalScale, pressScale);
            }

            // Play click sound
            if (audioSource != null && clickSound != null)
            {
                audioSource.PlayOneShot(clickSound);
            }

            // Spawn click particles
            if (clickParticlePrefab != null)
            {
                Instantiate(clickParticlePrefab, transform.position, Quaternion.identity);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;

            if (isHovering && enableHoverScale)
            {
                targetScale = Vector3.Scale(originalScale, hoverScale);
            }
            else
            {
                targetScale = originalScale;
            }
        }

        void OnDisable()
        {
            // Reset scale when disabled
            transform.localScale = originalScale;
            targetScale = originalScale;
            isHovering = false;
            isPressed = false;
        }

        /// <summary>
        /// Programmatically trigger a press animation
        /// </summary>
        public void TriggerPressEffect()
        {
            if (enablePressAnimation)
            {
                StartCoroutine(PressEffectCoroutine());
            }

            if (audioSource != null && clickSound != null)
            {
                audioSource.PlayOneShot(clickSound);
            }
        }

        private System.Collections.IEnumerator PressEffectCoroutine()
        {
            targetScale = Vector3.Scale(originalScale, pressScale);
            yield return new WaitForSecondsRealtime(0.1f);
            targetScale = originalScale;
        }
    }
}
