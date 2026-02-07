using System.Collections;
using UnityEngine;
using LostSouls.Core;
using Assets.Scripts.Core;

namespace LostSouls.Grid
{
    public class Tile : MonoBehaviour
    {
        [Header("Tile Properties")]
        [SerializeField] private TileType tileType = TileType.Floor;
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private bool isBorderWall;

        [Header("Visual References")]
        [SerializeField] private MeshRenderer meshRenderer;

        [Header("Exit/Door State")]
        [SerializeField] private bool isOpen;
        [SerializeField] private float openAnimDuration = 3f;

        [Header("Visuals")]
        [SerializeField] private bool hasModelVisuals;

        [Header("Sub-Tile System")]
        [SerializeField] private float tileSize = 2f;

        // Sub-tile occupants: 0=front-left, 1=front-right, 2=back-left, 3=back-right
        private GameObject[] subTileOccupants = new GameObject[4];

        // Colors for different tile types
        private static readonly Color FloorColor = new Color(0.8f, 0.8f, 0.8f);
        private static readonly Color WallColor = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color ExitColor = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color DoorColor = new Color(0.6f, 0.4f, 0.2f);
        private static readonly Color PressurePlateColor = new Color(0.8f, 0.6f, 0.2f);
        private static readonly Color PedestalColor = new Color(0.5f, 0.5f, 0.8f);
        private static readonly Color HighlightColor = new Color(1f, 1f, 0.5f, 0.5f);

        // Properties
        public TileType TileType => tileType;
        public Vector2Int GridPosition => gridPosition;
        public bool IsBorderWall => isBorderWall;
        public bool IsOpen => isOpen;
        public float TileSize => tileSize;

        public bool IsWalkable
        {
            get
            {
                return tileType switch
                {
                    TileType.Floor => true,
                    TileType.Wall => false,
                    TileType.Exit => isOpen,
                    TileType.Door => isOpen,
                    TileType.PressurePlate => true,
                    TileType.Pedestal => true,
                    _ => true
                };
            }
        }

        public bool IsOccupied
        {
            get
            {
                // Tile is occupied only if ALL sub-tiles are full (no room for character)
                for (int i = 0; i < 4; i++)
                {
                    if (subTileOccupants[i] == null) return false;
                }
                return true;
            }
        }

        // Legacy compatibility
        public GameObject CurrentOccupant => subTileOccupants[0];

        private bool isHighlighted;
        private Coroutine animCoroutine;

        private void Awake()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }
        }

        public void Initialize(TileType type, Vector2Int position, bool borderWall = false, float size = 2f, bool modelVisuals = false)
        {
            tileType = type;
            gridPosition = position;
            isBorderWall = borderWall;
            tileSize = size;
            hasModelVisuals = modelVisuals;

            // Exit/Door starts closed
            if (type == TileType.Exit || type == TileType.Door)
            {
                isOpen = false;
            }
            else
            {
                isOpen = true;
            }

            UpdateVisuals();
        }

        /// <summary>
        /// Changes the tile type at runtime (e.g., floor → door when spawning a door object)
        /// </summary>
        public void ChangeTileType(TileType newType)
        {
            tileType = newType;

            if (newType == TileType.Door || newType == TileType.Exit)
            {
                isOpen = false;
            }

            UpdateVisuals();
            Debug.Log($"Tile at {gridPosition} changed type to {newType}");
        }

        // ========== Sub-Tile System ==========

        /// <summary>
        /// Gets the local offset for a sub-tile position.
        /// Sub-tiles: 0=front-left, 1=front-right, 2=back-left, 3=back-right
        /// </summary>
        public Vector3 GetSubTileOffset(int subTile)
        {
            float quarter = tileSize * 0.25f;
            return subTile switch
            {
                0 => new Vector3(-quarter, 0, -quarter),  // front-left (default character)
                1 => new Vector3(quarter, 0, -quarter),   // front-right
                2 => new Vector3(-quarter, 0, quarter),    // back-left
                3 => new Vector3(quarter, 0, quarter),     // back-right (default item)
                _ => Vector3.zero
            };
        }

        /// <summary>
        /// Gets the world position for a sub-tile
        /// </summary>
        public Vector3 GetSubTileWorldPosition(int subTile)
        {
            return transform.position + GetSubTileOffset(subTile);
        }

        /// <summary>
        /// Checks if a sub-tile is occupied
        /// </summary>
        public bool IsSubTileOccupied(int subTile)
        {
            return subTile >= 0 && subTile < 4 && subTileOccupants[subTile] != null;
        }

        /// <summary>
        /// Finds the best available sub-tile starting from preferred
        /// </summary>
        public int GetAvailableSubTile(int preferred)
        {
            if (!IsSubTileOccupied(preferred)) return preferred;

            // Try all sub-tiles in order of preference
            int[] searchOrder = preferred switch
            {
                0 => new[] { 0, 1, 2, 3 },
                1 => new[] { 1, 0, 3, 2 },
                2 => new[] { 2, 3, 0, 1 },
                3 => new[] { 3, 2, 1, 0 },
                _ => new[] { 0, 1, 2, 3 }
            };

            foreach (int st in searchOrder)
            {
                if (!IsSubTileOccupied(st)) return st;
            }

            return preferred; // All occupied, return preferred anyway
        }

        /// <summary>
        /// Places an occupant (character) on the tile at the best available sub-tile
        /// </summary>
        public int SetOccupant(GameObject occupant, int preferredSubTile = 0)
        {
            int subTile = GetAvailableSubTile(preferredSubTile);
            subTileOccupants[subTile] = occupant;
            return subTile;
        }

        /// <summary>
        /// Places an item on the tile at the best available sub-tile
        /// </summary>
        public int SetItem(GameObject item, int preferredSubTile = 3)
        {
            int subTile = GetAvailableSubTile(preferredSubTile);
            subTileOccupants[subTile] = item;
            return subTile;
        }

        /// <summary>
        /// Clears a specific sub-tile
        /// </summary>
        public void ClearSubTile(int subTile)
        {
            if (subTile >= 0 && subTile < 4)
            {
                subTileOccupants[subTile] = null;
            }
        }

        /// <summary>
        /// Clears a specific occupant from whichever sub-tile it's on
        /// </summary>
        public void ClearOccupant(GameObject obj)
        {
            for (int i = 0; i < 4; i++)
            {
                if (subTileOccupants[i] == obj)
                {
                    subTileOccupants[i] = null;
                    return;
                }
            }
        }

        /// <summary>
        /// Clears all sub-tile occupants
        /// </summary>
        public void ClearOccupant()
        {
            for (int i = 0; i < 4; i++)
            {
                subTileOccupants[i] = null;
            }
        }

        // ========== Exit/Door Animation ==========

        /// <summary>
        /// Opens the exit/door — wall descends into ground
        /// </summary>
        public void Open()
        {
            if (isOpen) return;
            if (tileType != TileType.Exit && tileType != TileType.Door) return;

            GlobalAudio.PlayRockRumble(transform.position);

            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(AnimateOpenClose(true));
        }

        /// <summary>
        /// Closes the exit/door — wall rises from ground
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;
            if (tileType != TileType.Exit && tileType != TileType.Door) return;

            GlobalAudio.PlayRockRumble(transform.position);

            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(AnimateOpenClose(false));
        }

        /// <summary>
        /// Instantly sets the tile to open state without animation
        /// </summary>
        public void OpenImmediate()
        {
            if (isOpen) return;
            if (tileType != TileType.Exit && tileType != TileType.Door) return;

            Transform meshTransform = meshRenderer != null ? meshRenderer.transform : transform;
            float wallH = meshTransform.localScale.y;
            meshTransform.localPosition -= Vector3.up * wallH;
            isOpen = true;
        }

        /// <summary>
        /// Instantly sets the tile to closed state without animation
        /// </summary>
        public void CloseImmediate()
        {
            if (!isOpen) return;
            if (tileType != TileType.Exit && tileType != TileType.Door) return;

            Transform meshTransform = meshRenderer != null ? meshRenderer.transform : transform;
            float wallH = meshTransform.localScale.y;
            meshTransform.localPosition += Vector3.up * wallH;
            isOpen = false;
        }

        private IEnumerator AnimateOpenClose(bool opening)
        {
            // Get the mesh child (the visual cube)
            Transform meshTransform = meshRenderer != null ? meshRenderer.transform : transform;
            Vector3 startPos = meshTransform.localPosition;

            // Calculate target: when opening, descend by wall height; when closing, return to original
            float wallHeight = meshTransform.localScale.y;
            Vector3 targetPos;

            if (opening)
            {
                targetPos = startPos - Vector3.up * wallHeight;
            }
            else
            {
                targetPos = startPos + Vector3.up * wallHeight;
            }

            float elapsed = 0f;
            while (elapsed < openAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / openAnimDuration);
                meshTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            meshTransform.localPosition = targetPos;
            isOpen = opening;
            animCoroutine = null;

            Debug.Log($"Exit at {gridPosition} {(opening ? "opened" : "closed")}");
        }

        // ========== Visuals ==========

        public void SetHighlight(bool highlighted)
        {
            isHighlighted = highlighted;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (hasModelVisuals || meshRenderer == null) return;

            Color baseColor = tileType switch
            {
                TileType.Floor => FloorColor,
                TileType.Wall => WallColor,
                TileType.Exit => ExitColor,
                TileType.Door => DoorColor,
                TileType.PressurePlate => PressurePlateColor,
                TileType.Pedestal => PedestalColor,
                _ => FloorColor
            };

            if (isHighlighted)
            {
                baseColor = Color.Lerp(baseColor, HighlightColor, 0.5f);
            }

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(props);
            props.SetColor("_Color", baseColor);
            props.SetColor("_BaseColor", baseColor);
            meshRenderer.SetPropertyBlock(props);
        }

        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        public Vector3 GetCharacterStandPosition()
        {
            return GetSubTileWorldPosition(GetAvailableSubTile(0)) + Vector3.up * 0.5f;
        }

        public static Vector3 GridToWorldPosition(Vector2Int gridPos, float tileSize = 2f)
        {
            return new Vector3(gridPos.x * tileSize, 0f, gridPos.y * tileSize);
        }

        public static Vector3 GridToWorldPosition(int x, int y, float tileSize = 2f)
        {
            return GridToWorldPosition(new Vector2Int(x, y), tileSize);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = tileType switch
            {
                TileType.Floor => Color.white,
                TileType.Wall => Color.black,
                TileType.Exit => Color.green,
                TileType.Door => new Color(0.6f, 0.4f, 0.2f),
                TileType.PressurePlate => Color.yellow,
                TileType.Pedestal => Color.blue,
                _ => Color.white
            };

            Gizmos.DrawWireCube(transform.position, Vector3.one * tileSize * 0.9f);

            // Draw sub-tile positions
            if (tileType == TileType.Floor || tileType == TileType.PressurePlate || tileType == TileType.Pedestal)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 subPos = transform.position + GetSubTileOffset(i);
                    Gizmos.DrawWireCube(subPos + Vector3.up * 0.1f, Vector3.one * tileSize * 0.4f);
                }
            }
        }
    }
}
