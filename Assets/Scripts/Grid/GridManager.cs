using System.Collections.Generic;
using UnityEngine;
using LostSouls.Core;

namespace LostSouls.Grid
{
    public class GridManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private float tileSize = 2f;
        [SerializeField] private float tileHeight = 0.2f;
        [SerializeField] private float wallHeight = 2f;
        [SerializeField] private float wallYOffset = 1.1f;

        [Header("Prefabs")]
        [SerializeField] private GameObject floorPrefab;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject exitPrefab;
        [SerializeField] private GameObject pressurePlatePrefab;

        [Header("Runtime Data")]
        [SerializeField] private int gridWidth;
        [SerializeField] private int gridHeight;

        private Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();
        private Transform tilesParent;

        public int Width => gridWidth;
        public int Height => gridHeight;
        public float TileSize => tileSize;

        private void Awake()
        {
            tilesParent = new GameObject("Tiles").transform;
            tilesParent.SetParent(transform);
        }

        public void BuildGrid(GridData gridData)
        {
            ClearGrid();

            gridWidth = gridData.width;
            gridHeight = gridData.height;

            foreach (TileData tileData in gridData.tiles)
            {
                // Tiles on the outer edge of the grid are full-height (border walls)
                bool isOnEdge = tileData.x == 0 || tileData.y == 0 ||
                                tileData.x == gridData.width - 1 || tileData.y == gridData.height - 1;
                CreateTile(tileData, isBorderWall: isOnEdge);
            }

            Debug.Log($"Grid built: {gridWidth}x{gridHeight} with {tiles.Count} tiles");
        }

        public void ClearGrid()
        {
            foreach (var tile in tiles.Values)
            {
                if (tile != null)
                {
                    Destroy(tile.gameObject);
                }
            }
            tiles.Clear();
        }

        /// <summary>
        /// Creates a single tile at the specified position
        /// </summary>
        private void CreateTile(TileData tileData, bool isBorderWall)
        {
            Vector2Int gridPos = new Vector2Int(tileData.x, tileData.y);
            TileType tileType = tileData.GetTileType();

            GameObject prefab = GetPrefabForTileType(tileType);

            GameObject tileObject;
            if (prefab != null)
            {
                tileObject = Instantiate(prefab, tilesParent);
            }
            else
            {
                tileObject = CreatePrimitiveTile(tileType, isBorderWall);
                tileObject.transform.SetParent(tilesParent);
            }

            Vector3 worldPos = GridToWorldPosition(gridPos);
            tileObject.transform.position = worldPos;
            tileObject.name = $"Tile_{gridPos.x}_{gridPos.y}_{tileType}";

            Tile tile = tileObject.GetComponent<Tile>();
            if (tile == null)
            {
                tile = tileObject.AddComponent<Tile>();
            }
            tile.Initialize(tileType, gridPos, borderWall: isBorderWall, size: tileSize, modelVisuals: prefab != null);

            // Apply wall height offset based on border vs interior
            if (tileType == TileType.Wall || tileType == TileType.Exit || tileType == TileType.Door)
            {
                ApplyWallOffset(tileObject, isBorderWall);
            }

            tiles[gridPos] = tile;
        }

        /// <summary>
        /// Applies vertical offset to wall tiles.
        /// Border walls: full height visible. Interior walls: pushed down, half visible.
        /// </summary>
        private void ApplyWallOffset(GameObject tileObject, bool isBorderWall)
        {
            if (isBorderWall)
            {
                tileObject.transform.position += Vector3.up * wallYOffset;
            }
            else
            {
                // Interior walls: push down so they don't block the view
                tileObject.transform.position += Vector3.down * 0.1f;
            }
        }

        /// <summary>
        /// Creates a primitive cube tile when no prefab is available
        /// </summary>
        private GameObject CreatePrimitiveTile(TileType tileType, bool isBorderWall)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);

            bool isWallHeight = tileType == TileType.Wall || tileType == TileType.Exit || tileType == TileType.Door;
            float height = isWallHeight ? wallHeight : tileHeight;
            tile.transform.localScale = new Vector3(tileSize * 0.95f, height, tileSize * 0.95f);

            // Floor tiles: push slightly down so top surface is at Y=0
            if (!isWallHeight)
            {
                tile.transform.position += Vector3.down * (tileHeight / 2f);
            }
            // Wall/exit offset is handled by ApplyWallOffset after positioning

            MeshRenderer renderer = tile.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Color color = tileType switch
                {
                    TileType.Floor => new Color(0.8f, 0.8f, 0.8f),
                    TileType.Wall => new Color(0.3f, 0.3f, 0.35f),
                    TileType.Exit => new Color(0.2f, 0.9f, 0.3f),
                    TileType.Door => new Color(0.6f, 0.4f, 0.2f),
                    TileType.PressurePlate => new Color(0.9f, 0.7f, 0.2f),
                    TileType.Pedestal => new Color(0.5f, 0.5f, 0.9f),
                    _ => Color.white
                };

                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                }
                mat.color = color;
                renderer.material = mat;
            }

            return tile;
        }

        private GameObject GetPrefabForTileType(TileType tileType)
        {
            return tileType switch
            {
                TileType.Floor => floorPrefab,
                TileType.Wall => wallPrefab,
                TileType.Door => exitPrefab,  // Door and Exit are visually the same
                TileType.Exit => exitPrefab,
                TileType.PressurePlate => pressurePlatePrefab,
                _ => null
            };
        }

        public Tile GetTile(Vector2Int position)
        {
            return tiles.TryGetValue(position, out Tile tile) ? tile : null;
        }

        public Tile GetTile(int x, int y)
        {
            return GetTile(new Vector2Int(x, y));
        }

        public bool IsInBounds(Vector2Int position)
        {
            return position.x >= 0 && position.x < gridWidth &&
                   position.y >= 0 && position.y < gridHeight;
        }

        public bool IsWalkable(Vector2Int position)
        {
            Tile tile = GetTile(position);
            return tile != null && tile.IsWalkable;
        }

        public List<Tile> GetWalkableNeighbors(Vector2Int position)
        {
            List<Tile> neighbors = new List<Tile>();

            Vector2Int[] directions = new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighborPos = position + dir;
                if (IsWalkable(neighborPos))
                {
                    neighbors.Add(GetTile(neighborPos));
                }
            }

            return neighbors;
        }

        public List<Tile> GetAllNeighbors(Vector2Int position)
        {
            List<Tile> neighbors = new List<Tile>();

            Vector2Int[] directions = new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighborPos = position + dir;
                Tile tile = GetTile(neighborPos);
                if (tile != null)
                {
                    neighbors.Add(tile);
                }
            }

            return neighbors;
        }

        public Vector3 GridToWorldPosition(Vector2Int gridPos)
        {
            return new Vector3(gridPos.x * tileSize, 0f, gridPos.y * tileSize);
        }

        public Vector2Int WorldToGridPosition(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.RoundToInt(worldPos.x / tileSize),
                Mathf.RoundToInt(worldPos.z / tileSize)
            );
        }

        public Tile GetExitTile()
        {
            foreach (var tile in tiles.Values)
            {
                if (tile.TileType == TileType.Exit)
                {
                    return tile;
                }
            }
            return null;
        }

        /// <summary>
        /// Opens all exit tiles (wall descends)
        /// </summary>
        public void OpenExits()
        {
            foreach (var tile in tiles.Values)
            {
                if (tile.TileType == TileType.Exit)
                {
                    tile.Open();
                }
            }
        }

        /// <summary>
        /// Opens all exit tiles instantly without animation
        /// </summary>
        public void OpenExitsImmediate()
        {
            foreach (var tile in tiles.Values)
            {
                if (tile.TileType == TileType.Exit)
                {
                    tile.OpenImmediate();
                }
            }
        }

        /// <summary>
        /// Closes all exit tiles instantly without animation
        /// </summary>
        public void CloseExitsImmediate()
        {
            foreach (var tile in tiles.Values)
            {
                if (tile.TileType == TileType.Exit)
                {
                    tile.CloseImmediate();
                }
            }
        }

        /// <summary>
        /// Closes all exit tiles (wall rises)
        /// </summary>
        public void CloseExits()
        {
            foreach (var tile in tiles.Values)
            {
                if (tile.TileType == TileType.Exit)
                {
                    tile.Close();
                }
            }
        }

        public List<Tile> GetTilesOfType(TileType type)
        {
            List<Tile> result = new List<Tile>();
            foreach (var tile in tiles.Values)
            {
                if (tile.TileType == type)
                {
                    result.Add(tile);
                }
            }
            return result;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 bottomLeft = Vector3.zero;
            Vector3 bottomRight = new Vector3(gridWidth * tileSize, 0, 0);
            Vector3 topLeft = new Vector3(0, 0, gridHeight * tileSize);
            Vector3 topRight = new Vector3(gridWidth * tileSize, 0, gridHeight * tileSize);

            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);
        }
    }
}
