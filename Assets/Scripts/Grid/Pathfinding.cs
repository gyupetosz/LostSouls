using System.Collections.Generic;
using UnityEngine;

namespace LostSouls.Grid
{
    public class Pathfinding : MonoBehaviour
    {
        private GridManager gridManager;

        private void Awake()
        {
            gridManager = GetComponent<GridManager>();
            if (gridManager == null)
            {
                gridManager = FindObjectOfType<GridManager>();
            }
        }

        public void Initialize(GridManager grid)
        {
            gridManager = grid;
        }

        /// <summary>
        /// Finds a path from start to end using A* algorithm
        /// </summary>
        /// <param name="start">Starting grid position</param>
        /// <param name="end">Target grid position</param>
        /// <param name="ignoreOccupants">If true, treats occupied tiles as walkable</param>
        /// <returns>List of positions from start to end, or empty list if no path found</returns>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, bool ignoreOccupants = false)
        {
            if (gridManager == null)
            {
                Debug.LogError("Pathfinding: GridManager not set!");
                return new List<Vector2Int>();
            }

            // Check if end is reachable
            Tile endTile = gridManager.GetTile(end);
            if (endTile == null || !endTile.IsWalkable)
            {
                return new List<Vector2Int>();
            }

            // A* implementation
            HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
            HashSet<Vector2Int> openSet = new HashSet<Vector2Int> { start };

            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            Dictionary<Vector2Int, float> gScore = new Dictionary<Vector2Int, float>();
            Dictionary<Vector2Int, float> fScore = new Dictionary<Vector2Int, float>();

            gScore[start] = 0;
            fScore[start] = HeuristicCost(start, end);

            while (openSet.Count > 0)
            {
                // Get node with lowest fScore
                Vector2Int current = GetLowestFScore(openSet, fScore);

                if (current == end)
                {
                    return ReconstructPath(cameFrom, current);
                }

                openSet.Remove(current);
                closedSet.Add(current);

                // Check all neighbors
                foreach (Vector2Int neighbor in GetNeighbors(current))
                {
                    if (closedSet.Contains(neighbor))
                        continue;

                    // Check if neighbor is walkable
                    Tile neighborTile = gridManager.GetTile(neighbor);
                    if (neighborTile == null || !neighborTile.IsWalkable)
                        continue;

                    // Check occupancy (unless ignoring)
                    if (!ignoreOccupants && neighborTile.IsOccupied && neighbor != end)
                        continue;

                    float tentativeGScore = gScore[current] + 1; // Cost of 1 per tile

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                    else if (tentativeGScore >= gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    {
                        continue;
                    }

                    // This path is the best so far
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + HeuristicCost(neighbor, end);
                }
            }

            // No path found
            return new List<Vector2Int>();
        }

        /// <summary>
        /// Gets the distance (in tiles) between two positions via pathfinding
        /// </summary>
        public int GetPathDistance(Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> path = FindPath(start, end);
            return path.Count > 0 ? path.Count - 1 : -1; // -1 if no path
        }

        /// <summary>
        /// Checks if a path exists between two positions
        /// </summary>
        public bool PathExists(Vector2Int start, Vector2Int end)
        {
            return FindPath(start, end).Count > 0;
        }

        /// <summary>
        /// Gets the next step in a path from start to end
        /// </summary>
        public Vector2Int? GetNextStep(Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> path = FindPath(start, end);
            if (path.Count > 1)
            {
                return path[1]; // Return next position (index 0 is start)
            }
            return null;
        }

        /// <summary>
        /// Manhattan distance heuristic
        /// </summary>
        private float HeuristicCost(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// Gets the node with the lowest fScore from the open set
        /// </summary>
        private Vector2Int GetLowestFScore(HashSet<Vector2Int> openSet, Dictionary<Vector2Int, float> fScore)
        {
            Vector2Int lowest = default;
            float lowestScore = float.MaxValue;

            foreach (Vector2Int pos in openSet)
            {
                float score = fScore.GetValueOrDefault(pos, float.MaxValue);
                if (score < lowestScore)
                {
                    lowestScore = score;
                    lowest = pos;
                }
            }

            return lowest;
        }

        /// <summary>
        /// Reconstructs the path from the cameFrom dictionary
        /// </summary>
        private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            List<Vector2Int> path = new List<Vector2Int> { current };

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Insert(0, current);
            }

            return path;
        }

        /// <summary>
        /// Gets the four cardinal neighbors of a position
        /// </summary>
        private List<Vector2Int> GetNeighbors(Vector2Int position)
        {
            return new List<Vector2Int>
            {
                position + Vector2Int.up,
                position + Vector2Int.down,
                position + Vector2Int.left,
                position + Vector2Int.right
            };
        }

        /// <summary>
        /// Visualizes a path in the editor (for debugging)
        /// </summary>
        public void DebugDrawPath(List<Vector2Int> path, Color color, float duration = 2f)
        {
            if (path == null || path.Count < 2) return;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 start = gridManager.GridToWorldPosition(path[i]) + Vector3.up * 0.5f;
                Vector3 end = gridManager.GridToWorldPosition(path[i + 1]) + Vector3.up * 0.5f;
                Debug.DrawLine(start, end, color, duration);
            }
        }
    }
}
