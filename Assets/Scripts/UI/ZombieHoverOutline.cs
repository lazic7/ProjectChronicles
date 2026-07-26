using IsometricPathfinding.Zombies;
using UnityEngine;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ZombieHoverOutline : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] [Min(3)] private int segmentCount = 32;

        [SerializeField] private Vector2 size = new Vector2(0.8f, 1.6f);

        [SerializeField] private Vector2 offset = new Vector2(0f, 0.45f);

        [Header("Appearance")]
        [SerializeField] private Color outlineColor = Color.white;

        [SerializeField] [Min(0.001f)] private float lineWidth = 0.035f;
        
        [SerializeField] private Material outlineMaterial;

        private LineRenderer lineRenderer;

        private ZombieAgent zombieAgent;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            zombieAgent = GetComponentInParent<ZombieAgent>();

            ConfigureLineRenderer();
            RebuildOutline();
            Hide();
        }

        public void Show()
        {
            if (zombieAgent != null && zombieAgent.State == ZombieState.Dead)
            {
                Hide();
                return;
            }

            lineRenderer.enabled = true;
        }

        public void Hide()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }

        private void ConfigureLineRenderer()
        {
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.positionCount = segmentCount;
            
            if (outlineMaterial != null)
            {
                lineRenderer.sharedMaterial = outlineMaterial;
            }

            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;

            lineRenderer.startColor = outlineColor;
            lineRenderer.endColor = outlineColor;

            /*
             * For simple prototype lines, no lighting/shadows needed.
             */
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
        }

        private void RebuildOutline()
        {
            float width = size.x;
            float height = Mathf.Max(size.y, size.x);

            float radius = width * 0.5f;
            float halfHeight = height * 0.5f;

            float topCenterY = offset.y + halfHeight - radius;
            float bottomCenterY = offset.y - halfHeight + radius;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = ((float)i / segmentCount) * Mathf.PI * 2f;

                float x = Mathf.Cos(angle) * radius + offset.x;

                float y;

                if (angle <= Mathf.PI)
                {
                    // Top semicircle
                    y = Mathf.Sin(angle) * radius + topCenterY;
                }
                else
                {
                    // Bottom semicircle
                    y = Mathf.Sin(angle) * radius + bottomCenterY;
                }

                lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        private void OnValidate()
        {
            segmentCount = Mathf.Max(3, segmentCount);
            lineWidth = Mathf.Max(0.001f, lineWidth);

            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer == null)
            {
                return;
            }

            ConfigureLineRenderer();
            RebuildOutline();
        }
    }
}