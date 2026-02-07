using System;
using System.Collections;
using UnityEngine;
using LostSouls.Core;
using LostSouls.Grid;
using LostSouls.Character;
using LostSouls.Objects;

namespace LostSouls.LLM
{
    public class ActionExecutor : MonoBehaviour
    {
        public event Action OnActionCompleted;

        private ExplorerController character;
        private GridManager grid;
        private Pathfinding pathfinding;
        private ObjectManager objects;

        public void Initialize(ExplorerController character, GridManager grid,
            Pathfinding pathfinding, ObjectManager objects)
        {
            this.character = character;
            this.grid = grid;
            this.pathfinding = pathfinding;
            this.objects = objects;
        }

        public void Execute(CharacterAction action, CharacterProfileData profile)
        {
            if (action == null || action.type == ActionType.None ||
                action.type == ActionType.Wait || action.type == ActionType.Look ||
                action.type == ActionType.Examine)
            {
                // No physical action needed
                OnActionCompleted?.Invoke();
                return;
            }

            StartCoroutine(ExecuteCoroutine(action, profile));
        }

        private IEnumerator ExecuteCoroutine(CharacterAction action, CharacterProfileData profile)
        {
            // Apply direction mode conversion
            string resolvedDirection = ResolveDirection(action.direction, profile);

            switch (action.type)
            {
                case ActionType.Move:
                    yield return ExecuteMove(resolvedDirection, action.steps);
                    break;

                case ActionType.MoveTo:
                    yield return ExecuteMoveTo(action.targetObjectId);
                    break;

                case ActionType.Turn:
                    ExecuteTurn(resolvedDirection);
                    break;

                case ActionType.PickUp:
                    ExecutePickUp(action.targetObjectId);
                    break;

                case ActionType.PutDown:
                    ExecutePutDown(action.targetObjectId);
                    break;

                case ActionType.Use:
                    ExecuteUse(action.useOnTarget);
                    break;

                case ActionType.Push:
                    yield return ExecutePush(resolvedDirection);
                    break;

                case ActionType.OpenClose:
                    ExecuteOpenClose();
                    break;
            }

            // Wait for any movement to complete
            while (character.IsMoving)
            {
                yield return null;
            }

            OnActionCompleted?.Invoke();
        }

        private IEnumerator ExecuteMove(string direction, int steps)
        {
            Direction? dir = ActionValidator.ParseDirection(direction);
            if (!dir.HasValue) yield break;

            character.MoveInDirection(dir.Value, steps > 0 ? steps : 1);

            while (character.IsMoving) yield return null;
        }

        private IEnumerator ExecuteMoveTo(string targetId)
        {
            Vector2Int? targetPos = ResolveTargetPosition(targetId);
            if (!targetPos.HasValue)
            {
                Debug.LogWarning($"ActionExecutor: Could not resolve target '{targetId}'");
                yield break;
            }

            // For objects that block movement (pedestals, boxes), move to adjacent tile
            Tile targetTile = grid.GetTile(targetPos.Value);
            if (targetTile != null && !targetTile.IsWalkable)
            {
                // Find nearest walkable neighbor
                var neighbors = grid.GetWalkableNeighbors(targetPos.Value);
                if (neighbors.Count > 0)
                {
                    // Pick the closest neighbor to current position
                    Vector2Int best = neighbors[0].GridPosition;
                    int bestDist = Mathf.Abs(best.x - character.GridPosition.x) +
                                   Mathf.Abs(best.y - character.GridPosition.y);
                    foreach (var n in neighbors)
                    {
                        Vector2Int nPos = n.GridPosition;
                        int dist = Mathf.Abs(nPos.x - character.GridPosition.x) +
                                   Mathf.Abs(nPos.y - character.GridPosition.y);
                        if (dist < bestDist) { best = nPos; bestDist = dist; }
                    }
                    targetPos = best;
                }
            }

            character.MoveTo(targetPos.Value);
            while (character.IsMoving) yield return null;
        }

        private void ExecuteTurn(string direction)
        {
            if (string.IsNullOrEmpty(direction)) return;

            string dirLower = direction.ToLower();
            if (dirLower == "left" || dirLower == "right" || dirLower == "around" || dirLower == "back")
            {
                character.TurnRelative(dirLower);
            }
            else
            {
                Direction? dir = ActionValidator.ParseDirection(direction);
                if (dir.HasValue) character.Turn(dir.Value);
            }
        }

        private void ExecutePickUp(string targetId)
        {
            GridObject target = null;

            // Try specific target first
            if (!string.IsNullOrEmpty(targetId))
            {
                target = objects.GetObject(targetId);
            }

            // Fallback to nearest pickable
            if (target == null)
            {
                target = objects.GetPickableObjectNear(character.GridPosition);
            }

            if (target != null)
            {
                // Check if it's a gem on a pedestal
                if (target is GemObject gem)
                {
                    // Check if any nearby pedestal has this gem
                    var pedestals = objects.GetObjectsOfType<PedestalObject>();
                    foreach (var pedestal in pedestals)
                    {
                        if (pedestal.HasGem && pedestal.PlacedGem == gem)
                        {
                            pedestal.RemoveGem();
                            break;
                        }
                    }
                }

                character.PickUp(target);
            }
        }

        private void ExecutePutDown(string targetId)
        {
            if (!character.IsHoldingObject) return;

            // Check if there's a target pedestal nearby to place on
            if (character.HeldGridObject is GemObject gem)
            {
                var pedestals = objects.GetObjectsOfType<PedestalObject>();
                foreach (var pedestal in pedestals)
                {
                    Vector2Int pedPos = pedestal.GridPosition;
                    int dist = Mathf.Abs(pedPos.x - character.GridPosition.x) +
                               Mathf.Abs(pedPos.y - character.GridPosition.y);
                    if (dist <= 1)
                    {
                        if (gem.TryPlaceOnPedestal(pedestal))
                        {
                            character.ClearHeldObject();
                            return;
                        }
                    }
                }
            }

            character.PutDown();
        }

        private void ExecuteUse(string useOnTarget)
        {
            if (!character.IsHoldingObject) return;

            if (!string.IsNullOrEmpty(useOnTarget))
            {
                GridObject target = objects.GetObject(useOnTarget);
                if (target != null)
                {
                    character.UseHeldObjectOn(target);
                    return;
                }
            }

            // Use on nearest interactable
            character.UseHeldObject();
        }

        private IEnumerator ExecutePush(string direction)
        {
            Direction? dir = ActionValidator.ParseDirection(direction);
            if (!dir.HasValue) yield break;

            character.PushBox(dir.Value);
            while (character.IsMoving) yield return null;
        }

        private void ExecuteOpenClose()
        {
            // Find nearby door
            GridObject target = objects.GetInteractableObjectNear(character.GridPosition);
            if (target is DoorObject door)
            {
                if (door.DoorState == DoorState.Closed)
                    door.Open();
                else if (door.DoorState == DoorState.Open)
                    door.Close();
            }
        }

        private string ResolveDirection(string direction, CharacterProfileData profile)
        {
            if (string.IsNullOrEmpty(direction)) return direction;

            var mode = profile.GetDirectionMode();

            switch (mode)
            {
                case DirectionMode.Relative:
                    // Convert relative to absolute based on character facing
                    Direction abs = character.RelativeToAbsolute(direction.ToLower());
                    return abs.ToString().ToLower();

                case DirectionMode.InvertedLeftRight:
                    string dirLower = direction.ToLower();
                    if (dirLower == "left" || dirLower == "west") return "east";
                    if (dirLower == "right" || dirLower == "east") return "west";
                    return direction;

                case DirectionMode.InvertedNorthSouth:
                    string dirLow = direction.ToLower();
                    if (dirLow == "north" || dirLow == "up") return "south";
                    if (dirLow == "south" || dirLow == "down") return "north";
                    return direction;

                case DirectionMode.Absolute:
                default:
                    return direction;
            }
        }

        private Vector2Int? ResolveTargetPosition(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return null;

            string targetLower = targetId.ToLower();

            // Exit references
            if (targetLower.Contains("exit") || targetLower.Contains("doorway") ||
                targetLower.Contains("way out"))
            {
                Tile exitTile = grid.GetExitTile();
                if (exitTile != null) return exitTile.GridPosition;

                var doorTiles = grid.GetTilesOfType(TileType.Door);
                if (doorTiles != null)
                {
                    foreach (var tile in doorTiles)
                    {
                        if (tile.IsOpen) return tile.GridPosition;
                    }
                }
            }

            // Direct ID lookup
            GridObject obj = objects.GetObject(targetId);
            if (obj != null) return obj.GridPosition;

            // Fuzzy match
            var allObjects = objects.GetAllObjects();
            foreach (var o in allObjects)
            {
                if (o == null) continue;
                string displayLower = o.DisplayName?.ToLower() ?? "";
                string idLower = o.ObjectId?.ToLower() ?? "";

                if (displayLower.Contains(targetLower) || idLower.Contains(targetLower) ||
                    targetLower.Contains(displayLower) || targetLower.Contains(idLower))
                {
                    return o.GridPosition;
                }
            }

            return null;
        }
    }
}
