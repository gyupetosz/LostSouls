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
        public event Action<string, string> OnActionFeedback; // (dialogue, emotion)

        private ExplorerController character;
        private GridManager grid;
        private Pathfinding pathfinding;
        private ObjectManager objects;
        private CharacterProfileData currentProfile;

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

            currentProfile = profile;
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
                    yield return ExecutePickUp(action.targetObjectId);
                    break;

                case ActionType.PutDown:
                    ExecutePutDown(action.targetObjectId);
                    break;

                case ActionType.Use:
                    yield return ExecuteUse(action.useOnTarget);
                    break;

                case ActionType.Push:
                    yield return ExecutePush(resolvedDirection);
                    break;

                case ActionType.OpenClose:
                    yield return ExecuteOpenClose(action.useOnTarget);
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

        private IEnumerator ExecutePickUp(string targetId)
        {
            GridObject target = null;

            // Try specific target first (exact ID, then fuzzy with vocabulary)
            if (!string.IsNullOrEmpty(targetId))
            {
                target = objects.GetObject(targetId);
                if (target == null)
                    target = FuzzyFindObject(targetId);
            }

            // Fallback to nearest pickable
            if (target == null)
            {
                target = objects.GetPickableObjectNear(character.GridPosition);
            }

            if (target == null)
            {
                Debug.LogWarning($"ActionExecutor: No target found for pick_up '{targetId}'");
                yield break;
            }

            // If target is not adjacent, walk to it first
            int dist = Mathf.Abs(target.GridPosition.x - character.GridPosition.x) +
                       Mathf.Abs(target.GridPosition.y - character.GridPosition.y);
            if (dist > 1)
            {
                Debug.Log($"ActionExecutor: Target '{targetId}' is {dist} tiles away, moving there first");
                character.MoveTo(target.GridPosition);
                while (character.IsMoving) yield return null;
            }

            // Check if hands are full AFTER walking to the target
            if (character.IsHoldingObject)
            {
                OnActionFeedback?.Invoke("My hands are full! I can't pick that up.", "confused");
                yield break;
            }

            // Check if it's a gem on a pedestal
            if (target is GemObject gem)
            {
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

        private IEnumerator ExecuteUse(string useOnTarget)
        {
            if (!character.IsHoldingObject)
            {
                Debug.LogWarning("ActionExecutor: ExecuteUse — not holding anything");
                yield break;
            }

            GridObject heldObject = character.HeldGridObject;
            GridObject target = null;

            // Try exact ID lookup
            if (!string.IsNullOrEmpty(useOnTarget))
            {
                target = objects.GetObject(useOnTarget);
                // Don't use the held object on itself
                if (target == heldObject) target = null;
                Debug.Log($"ActionExecutor: Use target '{useOnTarget}' exact lookup: {(target != null ? "found" : "not found")}");
            }

            // Fuzzy lookup if exact failed
            if (target == null && !string.IsNullOrEmpty(useOnTarget))
            {
                target = FuzzyFindObject(useOnTarget);
                if (target == heldObject) target = null;
                Debug.Log($"ActionExecutor: Use target '{useOnTarget}' fuzzy lookup: {(target != null ? target.ObjectId : "not found")}");
            }

            // Fallback: nearest interactable (skip the held object)
            if (target == null)
            {
                target = objects.GetInteractableObjectNear(character.GridPosition);
                if (target == heldObject) target = null;
                Debug.Log($"ActionExecutor: Use fallback to nearest interactable: {(target != null ? target.ObjectId : "none")}");
            }

            // Last resort: find any door (common "use key on door" pattern)
            if (target == null)
            {
                var allObjects = objects.GetAllObjects();
                foreach (var o in allObjects)
                {
                    if (o is DoorObject) { target = o; break; }
                }
                Debug.Log($"ActionExecutor: Use last-resort door search: {(target != null ? target.ObjectId : "none")}");
            }

            if (target == null)
            {
                Debug.LogWarning("ActionExecutor: ExecuteUse — no target found at all");
                character.UseHeldObject();
                yield break;
            }

            // If target is not adjacent, walk near it first
            int dist = Mathf.Abs(target.GridPosition.x - character.GridPosition.x) +
                       Mathf.Abs(target.GridPosition.y - character.GridPosition.y);
            if (dist > 1)
            {
                Debug.Log($"ActionExecutor: Use target '{target.ObjectId}' is {dist} tiles away, moving there first");
                var neighbors = grid.GetWalkableNeighbors(target.GridPosition);
                if (neighbors.Count > 0)
                {
                    Vector2Int best = neighbors[0].GridPosition;
                    int bestDist = Mathf.Abs(best.x - character.GridPosition.x) +
                                   Mathf.Abs(best.y - character.GridPosition.y);
                    foreach (var n in neighbors)
                    {
                        Vector2Int nPos = n.GridPosition;
                        int d = Mathf.Abs(nPos.x - character.GridPosition.x) +
                                Mathf.Abs(nPos.y - character.GridPosition.y);
                        if (d < bestDist) { best = nPos; bestDist = d; }
                    }
                    character.MoveTo(best);
                    while (character.IsMoving) yield return null;
                }
            }

            Debug.Log($"ActionExecutor: Using held object on '{target.ObjectId}'");
            bool useSuccess = character.UseHeldObjectOn(target);
            if (!useSuccess && target is DoorObject)
            {
                OnActionFeedback?.Invoke("Hmm, this doesn't seem to work on this door.", "confused");
            }
        }

        private GridObject FuzzyFindObject(string nameOrId)
        {
            if (string.IsNullOrEmpty(nameOrId)) return null;
            string lower = nameOrId.ToLower();

            var allObjects = objects.GetAllObjects();
            if (allObjects == null) return null;

            foreach (var o in allObjects)
            {
                if (o == null) continue;
                string display = o.DisplayName?.ToLower() ?? "";
                string id = o.ObjectId?.ToLower() ?? "";

                if (display.Contains(lower) || lower.Contains(display) ||
                    id.Contains(lower) || lower.Contains(id))
                {
                    return o;
                }
            }

            // Reverse vocabulary lookup
            string realName = ReverseVocabularyLookup(nameOrId);
            if (realName != null)
            {
                string realLower = realName.ToLower();
                foreach (var o in allObjects)
                {
                    if (o == null) continue;
                    string display = o.DisplayName?.ToLower() ?? "";
                    if (display.Contains(realLower) || realLower.Contains(display))
                        return o;
                }
            }

            return null;
        }

        private string ReverseVocabularyLookup(string vocabularyName)
        {
            if (currentProfile?.perception_quirks == null) return null;

            string vocabLower = vocabularyName.ToLower();
            foreach (var quirk in currentProfile.perception_quirks)
            {
                if (quirk.type?.ToLower() == "own_vocabulary" &&
                    quirk.config?.vocabulary_map?.HasEntries() == true)
                {
                    foreach (var entry in quirk.config.vocabulary_map.entries)
                    {
                        if (entry.replacement?.ToLower() == vocabLower)
                            return entry.word;
                    }
                }
            }
            return null;
        }

        private IEnumerator ExecutePush(string direction)
        {
            Direction? dir = ActionValidator.ParseDirection(direction);
            if (!dir.HasValue) yield break;

            character.PushBox(dir.Value);
            while (character.IsMoving) yield return null;
        }

        private IEnumerator ExecuteOpenClose(string targetId)
        {
            DoorObject door = null;

            // Try specific target
            if (!string.IsNullOrEmpty(targetId))
            {
                GridObject obj = objects.GetObject(targetId);
                if (obj is DoorObject d) door = d;
            }

            // Fallback: nearby door
            if (door == null)
            {
                GridObject nearby = objects.GetInteractableObjectNear(character.GridPosition);
                if (nearby is DoorObject d) door = d;
            }

            if (door == null) yield break;

            // Move near door if not adjacent
            int dist = Mathf.Abs(door.GridPosition.x - character.GridPosition.x) +
                       Mathf.Abs(door.GridPosition.y - character.GridPosition.y);
            if (dist > 1)
            {
                var neighbors = grid.GetWalkableNeighbors(door.GridPosition);
                if (neighbors.Count > 0)
                {
                    Vector2Int best = neighbors[0].GridPosition;
                    int bestDist = Mathf.Abs(best.x - character.GridPosition.x) +
                                   Mathf.Abs(best.y - character.GridPosition.y);
                    foreach (var n in neighbors)
                    {
                        Vector2Int nPos = n.GridPosition;
                        int dd = Mathf.Abs(nPos.x - character.GridPosition.x) +
                                 Mathf.Abs(nPos.y - character.GridPosition.y);
                        if (dd < bestDist) { best = nPos; bestDist = dd; }
                    }
                    character.MoveTo(best);
                    while (character.IsMoving) yield return null;
                }
            }

            if (door.DoorState == DoorState.Closed)
                door.Open();
            else if (door.DoorState == DoorState.Open)
                door.Close();
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

            // Reverse vocabulary lookup
            string realName = ReverseVocabularyLookup(targetId);
            string realLower = realName?.ToLower();

            // Exit references (including vocabulary reverse lookup)
            bool isExitRef = targetLower.Contains("exit") || targetLower.Contains("doorway") ||
                targetLower.Contains("way out") || targetLower.Contains("leave") ||
                targetLower.Contains("escape") || targetLower.Contains("through");
            if (!isExitRef && realLower != null)
                isExitRef = realLower.Contains("exit");

            if (isExitRef)
            {
                Tile exitTile = grid.GetExitTile();
                if (exitTile != null) return exitTile.GridPosition;

                var doorTiles = grid.GetTilesOfType(TileType.Door);
                if (doorTiles != null)
                {
                    // Prefer open doors (they are walkable exits)
                    foreach (var tile in doorTiles)
                    {
                        if (tile.IsOpen) return tile.GridPosition;
                    }
                    // Fall back to any door tile
                    foreach (var tile in doorTiles)
                    {
                        return tile.GridPosition;
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

            // Reverse vocabulary match
            if (realLower != null)
            {
                foreach (var o in allObjects)
                {
                    if (o == null) continue;
                    string displayLower = o.DisplayName?.ToLower() ?? "";
                    if (displayLower.Contains(realLower) || realLower.Contains(displayLower))
                        return o.GridPosition;
                }
            }

            return null;
        }
    }
}
