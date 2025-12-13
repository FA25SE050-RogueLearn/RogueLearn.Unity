using UnityEngine;

namespace BossFight2D.Environments
{
    /// <summary>
    /// Auto-sorts sprites by Y position for top-down 2.5D view.
    /// Attach to any GameObject that needs depth sorting (characters, trees, rocks, etc.)
    /// Lower Y position = appears in front (closer to camera in top-down view)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class YSortingSprite : MonoBehaviour
    {
        [Header("Sorting Settings")]
        [SerializeField] private string sortingLayerName = "DynamicObjects";
        [SerializeField] private int sortingOrderOffset = 0;
        [SerializeField] private bool updateEveryFrame = true;

        [Header("Optimization")]
        [Tooltip("Only update when position changes (saves performance for static objects)")]
        [SerializeField] private bool onlyUpdateOnMove = false;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private SpriteRenderer spriteRenderer;
        private Vector3 lastPosition;
        private int lastSortingOrder;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            // Set sorting layer
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                spriteRenderer.sortingLayerName = sortingLayerName;
            }

            lastPosition = transform.position;
            UpdateSortingOrder();
        }

        void Start()
        {
            UpdateSortingOrder();
        }

        void LateUpdate()
        {
            if (!updateEveryFrame) return;

            // Optimization: only update if position changed
            if (onlyUpdateOnMove)
            {
                if (Vector3.Distance(transform.position, lastPosition) > 0.001f)
                {
                    UpdateSortingOrder();
                    lastPosition = transform.position;
                }
            }
            else
            {
                UpdateSortingOrder();
            }
        }

        /// <summary>
        /// Calculate and apply sorting order based on Y position
        /// Formula: Lower Y = Higher sorting order (appears in front)
        /// </summary>
        private void UpdateSortingOrder()
        {
            if (spriteRenderer == null) return;

            // Convert Y position to sorting order
            // Multiply by 100 for precision (allows 0.01 unit differences)
            // Negate so lower Y = higher order (in front)
            int newSortingOrder = -(int)(transform.position.y * 100f) + sortingOrderOffset;

            // Only update if changed (reduces overhead)
            if (newSortingOrder != lastSortingOrder)
            {
                spriteRenderer.sortingOrder = newSortingOrder;
                lastSortingOrder = newSortingOrder;

                if (showDebugInfo)
                {
                    Debug.Log($"[YSorting] {gameObject.name} - Y: {transform.position.y:F2}, Order: {newSortingOrder}");
                }
            }
        }

        /// <summary>
        /// Force immediate sorting update (useful after teleport or spawn)
        /// </summary>
        public void ForceUpdate()
        {
            UpdateSortingOrder();
        }

        /// <summary>
        /// Change the sorting layer at runtime
        /// </summary>
        public void SetSortingLayer(string layerName)
        {
            sortingLayerName = layerName;
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingLayerName = layerName;
            }
        }

        /// <summary>
        /// Adjust the sorting order offset
        /// </summary>
        public void SetSortingOffset(int offset)
        {
            sortingOrderOffset = offset;
            UpdateSortingOrder();
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!showDebugInfo) return;

            // Draw Y position indicator
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y, 0), 0.2f);

            // Draw sorting order text would require UnityEditor.Handles
            // So we just log it
        }

        [ContextMenu("Force Update Sorting")]
        private void TestForceUpdate()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            UpdateSortingOrder();
            Debug.Log($"Sorting Order Updated: {spriteRenderer.sortingOrder}");
        }

        [ContextMenu("Log Current Sorting")]
        private void LogCurrentSorting()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            Debug.Log($"[{gameObject.name}] Layer: {spriteRenderer.sortingLayerName}, Order: {spriteRenderer.sortingOrder}, Y: {transform.position.y}");
        }
#endif
    }
}
