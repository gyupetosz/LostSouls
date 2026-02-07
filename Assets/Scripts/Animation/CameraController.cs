using UnityEngine;
using LostSouls.Character;
using LostSouls.Grid;

namespace LostSouls.Animation
{
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private ExplorerController explorer;

        [Header("Camera Settings")]
        [SerializeField] private float distance = 10f;
        [SerializeField] private float height = 10f;
        [SerializeField] private float angle = 45f; // Isometric angle
        [SerializeField] private bool useOrthographic = true;
        [SerializeField] private float orthographicSize = 5f;

        [Header("Follow Settings")]
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector3 offset = Vector3.zero;

        [Header("Bounds (Optional)")]
        [SerializeField] private bool useBounds;
        [SerializeField] private Vector2 minBounds;
        [SerializeField] private Vector2 maxBounds;

        private Camera cam;
        private Vector3 targetPosition;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = gameObject.AddComponent<Camera>();
            }
        }

        private void Start()
        {
            SetupIsometricCamera();

            if (target == null && explorer != null)
            {
                target = explorer.transform;
            }

            if (target != null)
            {
                // Snap to initial position
                UpdateCameraPosition(true);
            }
        }

        /// <summary>
        /// Sets the camera target to follow
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Sets the camera target to an explorer
        /// </summary>
        public void SetTarget(ExplorerController newExplorer)
        {
            explorer = newExplorer;
            target = newExplorer?.transform;
        }

        /// <summary>
        /// Configures the camera for isometric view
        /// </summary>
        public void SetupIsometricCamera()
        {
            if (cam == null) return;

            cam.orthographic = useOrthographic;
            if (useOrthographic)
            {
                cam.orthographicSize = orthographicSize;
            }

            // Calculate camera position based on isometric angle
            UpdateCameraAngle();
        }

        private void UpdateCameraAngle()
        {
            // Isometric angle: typically 30-45 degrees
            // Position camera looking down at the angle
            transform.rotation = Quaternion.Euler(angle, 45f, 0f); // 45 degree Y rotation for isometric look
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateCameraPosition(false);
        }

        private void UpdateCameraPosition(bool immediate)
        {
            if (target == null) return;

            // Calculate desired position
            Vector3 desiredPosition = CalculateDesiredPosition();

            // Apply bounds if enabled
            if (useBounds)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
                desiredPosition.z = Mathf.Clamp(desiredPosition.z, minBounds.y, maxBounds.y);
            }

            // Smooth follow or snap
            if (immediate)
            {
                transform.position = desiredPosition;
            }
            else
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    desiredPosition,
                    smoothSpeed * Time.deltaTime
                );
            }

            // Always look at target (with offset for better view)
            // Actually, for isometric we maintain fixed rotation
            // transform.LookAt(target.position + offset);
        }

        private Vector3 CalculateDesiredPosition()
        {
            // For isometric camera, position is offset from target
            // at the specified angle and distance

            Vector3 targetPos = target.position + offset;

            // Calculate offset based on angle
            float radAngle = angle * Mathf.Deg2Rad;
            float horizontalDistance = distance * Mathf.Cos(radAngle);
            float verticalDistance = distance * Mathf.Sin(radAngle);

            // Offset in the direction camera is facing (45 degrees from north)
            float yRotRad = 45f * Mathf.Deg2Rad;
            Vector3 horizontalOffset = new Vector3(
                -horizontalDistance * Mathf.Sin(yRotRad),
                0,
                -horizontalDistance * Mathf.Cos(yRotRad)
            );

            return targetPos + horizontalOffset + Vector3.up * verticalDistance;
        }

        /// <summary>
        /// Sets camera bounds based on grid size
        /// </summary>
        public void SetBoundsFromGrid(GridManager gridManager)
        {
            if (gridManager == null) return;

            useBounds = true;

            float padding = 2f;
            minBounds = new Vector2(-padding, -padding);
            maxBounds = new Vector2(
                gridManager.Width * gridManager.TileSize + padding,
                gridManager.Height * gridManager.TileSize + padding
            );
        }

        /// <summary>
        /// Centers the camera on the grid
        /// </summary>
        public void CenterOnGrid(GridManager gridManager)
        {
            if (gridManager == null) return;

            Vector3 gridCenter = new Vector3(
                (gridManager.Width - 1) * gridManager.TileSize * 0.5f,
                0,
                (gridManager.Height - 1) * gridManager.TileSize * 0.5f
            );

            // Temporarily set offset to center on grid
            offset = gridCenter - (target?.position ?? Vector3.zero);
        }

        /// <summary>
        /// Adjusts orthographic size to fit the grid
        /// </summary>
        public void FitToGrid(GridManager gridManager, float padding = 1f)
        {
            if (gridManager == null || cam == null) return;

            float gridWidth = gridManager.Width * gridManager.TileSize;
            float gridHeight = gridManager.Height * gridManager.TileSize;

            // Calculate size needed to fit grid
            float aspectRatio = (float)Screen.width / Screen.height;
            float sizeForWidth = (gridWidth + padding * 2) / (2f * aspectRatio);
            float sizeForHeight = (gridHeight + padding * 2) / 2f;

            orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);

            if (cam.orthographic)
            {
                cam.orthographicSize = orthographicSize;
            }
        }

        /// <summary>
        /// Snaps camera immediately to target position
        /// </summary>
        public void SnapToTarget()
        {
            UpdateCameraPosition(true);
        }

        /// <summary>
        /// Shakes the camera (for impact effects)
        /// </summary>
        public void Shake(float intensity = 0.3f, float duration = 0.2f)
        {
            StartCoroutine(ShakeCoroutine(intensity, duration));
        }

        private System.Collections.IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            Vector3 originalPos = transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;

                transform.position = originalPos + new Vector3(x, y, 0);

                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = originalPos;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw camera frustum in editor
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, target.position);
            }

            // Draw bounds
            if (useBounds)
            {
                Gizmos.color = Color.cyan;
                Vector3 bottomLeft = new Vector3(minBounds.x, 0, minBounds.y);
                Vector3 bottomRight = new Vector3(maxBounds.x, 0, minBounds.y);
                Vector3 topLeft = new Vector3(minBounds.x, 0, maxBounds.y);
                Vector3 topRight = new Vector3(maxBounds.x, 0, maxBounds.y);

                Gizmos.DrawLine(bottomLeft, bottomRight);
                Gizmos.DrawLine(bottomRight, topRight);
                Gizmos.DrawLine(topRight, topLeft);
                Gizmos.DrawLine(topLeft, bottomLeft);
            }
        }
    }
}
