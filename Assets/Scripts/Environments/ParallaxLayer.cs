using UnityEngine;

namespace BossFight2D.Environments
{
    /// <summary>
    /// Creates parallax scrolling effect for 2D backgrounds.
    /// Layers move slower/faster based on distance to create depth.
    /// Attach to background sprites (castle walls, mountains, clouds).
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Parallax Settings")]
        [Tooltip("How much this layer moves relative to camera. 0 = no movement, 1 = moves with camera")]
        [Range(0f, 1f)]
        [SerializeField] private float parallaxEffect = 0.5f;

        [Tooltip("Enable vertical parallax (for clouds, birds)")]
        [SerializeField] private bool enableVerticalParallax = false;

        [Range(0f, 1f)]
        [SerializeField] private float verticalParallaxEffect = 0.3f;

        [Header("Auto-Repeat (Infinite Scrolling)")]
        [Tooltip("Automatically repeat sprite when it goes off-screen")]
        [SerializeField] private bool autoRepeat = false;
        [SerializeField] private float spriteWidth = 10f;

        private Transform cameraTransform;
        private Vector3 previousCameraPosition;
        private Vector3 startPosition;

        void Start()
        {
            cameraTransform = Camera.main.transform;
            previousCameraPosition = cameraTransform.position;
            startPosition = transform.position;
        }

        void LateUpdate()
        {
            if (cameraTransform == null) return;

            // Calculate camera movement
            Vector3 deltaMovement = cameraTransform.position - previousCameraPosition;

            // Apply parallax effect (inverse of parallaxEffect - closer layers move more)
            float parallaxX = deltaMovement.x * (1f - parallaxEffect);
            float parallaxY = enableVerticalParallax ? deltaMovement.y * (1f - verticalParallaxEffect) : 0f;

            // Move layer
            transform.position += new Vector3(parallaxX, parallaxY, 0f);

            // Auto-repeat logic (for infinite scrolling backgrounds)
            if (autoRepeat)
            {
                float distanceFromStart = transform.position.x - startPosition.x;

                if (Mathf.Abs(distanceFromStart) >= spriteWidth)
                {
                    float offsetX = Mathf.Sign(distanceFromStart) * spriteWidth;
                    transform.position = new Vector3(
                        transform.position.x - offsetX,
                        transform.position.y,
                        transform.position.z
                    );
                    startPosition = new Vector3(
                        startPosition.x - offsetX,
                        startPosition.y,
                        startPosition.z
                    );
                }
            }

            previousCameraPosition = cameraTransform.position;
        }

        /// <summary>
        /// Set parallax effect at runtime
        /// </summary>
        public void SetParallaxEffect(float effect)
        {
            parallaxEffect = Mathf.Clamp01(effect);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (autoRepeat)
            {
                // Draw repeat boundary
                Gizmos.color = Color.cyan;
                Vector3 pos = Application.isPlaying ? startPosition : transform.position;
                Gizmos.DrawWireCube(pos, new Vector3(spriteWidth, 10f, 0f));
            }
        }
#endif
    }
}
