using UnityEngine;

namespace BossFight2D.Environments
{
    /// <summary>
    /// Plays sprite animation from sprite sheet.
    /// Perfect for Tiny Swords water animations (12 frames).
    /// Attach to GameObject with SpriteRenderer.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class AnimatedSprite : MonoBehaviour
    {
        [Header("Animation Sprites")]
        [Tooltip("Array of sprites to animate through (drag multi-sprite asset)")]
        [SerializeField] private Sprite[] animationSprites;

        [Header("Animation Settings")]
        [Tooltip("Frames per second (8-12 recommended for water)")]
        [SerializeField] private float frameRate = 10f;

        [Tooltip("Loop the animation")]
        [SerializeField] private bool loop = true;

        [Tooltip("Play on awake")]
        [SerializeField] private bool playOnAwake = true;

        [Tooltip("Randomize start frame (good for multiple water tiles)")]
        [SerializeField] private bool randomizeStartFrame = false;

        private SpriteRenderer spriteRenderer;
        private int currentFrame = 0;
        private float frameTimer = 0f;
        private bool isPlaying = false;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (randomizeStartFrame && animationSprites != null && animationSprites.Length > 0)
            {
                currentFrame = Random.Range(0, animationSprites.Length);
            }
        }

        void Start()
        {
            if (playOnAwake)
            {
                Play();
            }
        }

        void Update()
        {
            if (!isPlaying || animationSprites == null || animationSprites.Length == 0)
                return;

            frameTimer += Time.deltaTime;

            float frameDuration = 1f / frameRate;

            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                currentFrame++;

                if (currentFrame >= animationSprites.Length)
                {
                    if (loop)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        currentFrame = animationSprites.Length - 1;
                        isPlaying = false;
                    }
                }

                UpdateSprite();
            }
        }

        private void UpdateSprite()
        {
            if (spriteRenderer != null && animationSprites != null && currentFrame < animationSprites.Length)
            {
                spriteRenderer.sprite = animationSprites[currentFrame];
            }
        }

        /// <summary>
        /// Start playing animation
        /// </summary>
        public void Play()
        {
            isPlaying = true;
            if (animationSprites != null && animationSprites.Length > 0)
            {
                UpdateSprite();
            }
        }

        /// <summary>
        /// Stop playing animation
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
        }

        /// <summary>
        /// Pause animation (can resume from same frame)
        /// </summary>
        public void Pause()
        {
            isPlaying = false;
        }

        /// <summary>
        /// Reset to first frame
        /// </summary>
        public void Reset()
        {
            currentFrame = 0;
            frameTimer = 0f;
            UpdateSprite();
        }

        /// <summary>
        /// Set animation frame rate
        /// </summary>
        public void SetFrameRate(float fps)
        {
            frameRate = Mathf.Max(1f, fps);
        }

        /// <summary>
        /// Load sprites from a multi-sprite texture
        /// </summary>
        public void LoadSprites(Sprite[] sprites)
        {
            animationSprites = sprites;
            currentFrame = 0;
            if (isPlaying)
            {
                UpdateSprite();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Preview first frame in editor
            if (!Application.isPlaying && animationSprites != null && animationSprites.Length > 0)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null && currentFrame < animationSprites.Length)
                {
                    sr.sprite = animationSprites[currentFrame];
                }
            }
        }

        [ContextMenu("Preview Animation")]
        private void PreviewAnimation()
        {
            if (animationSprites != null && animationSprites.Length > 0)
            {
                currentFrame = (currentFrame + 1) % animationSprites.Length;
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = animationSprites[currentFrame];
                }
            }
        }
#endif
    }
}
