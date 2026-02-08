using UnityEngine;
using LostSouls.Core;
using LostSouls.Grid;
using LostSouls.Character;
using LostSouls.Objects;

namespace LostSouls.LLM
{
    public static class ActionValidator
    {
        public static CharacterAction Validate(
            CharacterAction action,
            ExplorerController character,
            GridManager grid,
            Pathfinding pathfinding,
            ObjectManager objects,
            CharacterProfileData profile)
        {
            if (action == null || action.type == ActionType.None || action.type == ActionType.Wait)
                return action;

            // Check comprehension limits
            var comprehension = profile.GetComprehensionLevel();
            if (comprehension == ComprehensionLevel.Simple && action.type == ActionType.MoveTo)
            {
                action.dialogue += " I don't know how to get there on my own. Tell me which way to go — up, down, left, or right.";
                action.type = ActionType.None;
                action.emotion = "confused";
                return action;
            }

            switch (action.type)
            {
                case ActionType.Move:
                    return ValidateMove(action, character, grid);

                case ActionType.MoveTo:
                    return ValidateMoveTo(action, character, grid, pathfinding, objects, profile);

                case ActionType.PickUp:
                    return ValidatePickUp(action, character, objects, profile);

                case ActionType.PutDown:
                    return ValidatePutDown(action, character);

                case ActionType.Use:
                    return ValidateUse(action, character, objects, profile);

                case ActionType.Push:
                    return ValidatePush(action, character, grid, objects);

                case ActionType.OpenClose:
                    return ValidateOpenClose(action, character, objects, profile);

                case ActionType.Look:
                case ActionType.Examine:
                case ActionType.Turn:
                    // These are always valid (dialogue-only or simple state change)
                    return action;
            }

            return action;
        }

        private static CharacterAction ValidateMove(
            CharacterAction action, ExplorerController character, GridManager grid)
        {
            Direction? dir = ParseDirection(action.direction);
            if (!dir.HasValue)
            {
                action.dialogue += " I don't know which direction that is.";
                action.type = ActionType.None;
                return action;
            }

            int steps = action.steps > 0 ? action.steps : 1;
            Vector2Int dirVec = ExplorerController.DirectionToVector(dir.Value);
            Vector2Int checkPos = character.GridPosition;

            for (int i = 0; i < steps; i++)
            {
                checkPos += dirVec;
                if (!grid.IsWalkable(checkPos))
                {
                    if (i == 0)
                    {
                        action.dialogue += " I can't go that way, there's something blocking me.";
                        action.type = ActionType.None;
                    }
                    else
                    {
                        action.steps = i; // Partial movement
                    }
                    return action;
                }
            }

            return action;
        }

        private static CharacterAction ValidateMoveTo(
            CharacterAction action, ExplorerController character,
            GridManager grid, Pathfinding pathfinding, ObjectManager objects,
            CharacterProfileData profile = null)
        {
            // Try to find target position
            Vector2Int? targetPos = ResolveTargetPosition(action.targetObjectId, objects, grid, profile);

            if (!targetPos.HasValue)
            {
                action.dialogue += " I can't find what you're referring to.";
                action.type = ActionType.None;
                return action;
            }

            // Check if path exists to the target directly
            var path = pathfinding.FindPath(character.GridPosition, targetPos.Value, ignoreOccupants: true);
            if (path == null || path.Count == 0)
            {
                // Target tile might not be walkable (locked door, wall, pedestal, etc.)
                // Try finding a path to the nearest walkable neighbor instead
                var neighbors = grid.GetWalkableNeighbors(targetPos.Value);
                bool foundAlternate = false;
                foreach (var neighbor in neighbors)
                {
                    var altPath = pathfinding.FindPath(character.GridPosition, neighbor.GridPosition, ignoreOccupants: true);
                    if (altPath != null && altPath.Count > 0)
                    {
                        foundAlternate = true;
                        break;
                    }
                }

                if (!foundAlternate)
                {
                    action.dialogue += " I can't find a way to get there.";
                    action.type = ActionType.None;
                    return action;
                }
            }

            return action;
        }

        private static CharacterAction ValidatePickUp(
            CharacterAction action, ExplorerController character, ObjectManager objects,
            CharacterProfileData profile)
        {
            // If the LLM specified a target, try to resolve it first
            if (!string.IsNullOrEmpty(action.targetObjectId))
            {
                GridObject target = FindObjectByNameOrId(action.targetObjectId, objects, profile);
                if (target != null && target.CanPickUp())
                {
                    // Simple comprehension can't auto-walk to distant objects
                    if (profile.GetComprehensionLevel() == ComprehensionLevel.Simple)
                    {
                        int dist = Mathf.Abs(target.GridPosition.x - character.GridPosition.x) +
                                   Mathf.Abs(target.GridPosition.y - character.GridPosition.y);
                        if (dist > 1)
                        {
                            action.dialogue += " I can see it but it's too far away. Tell me which way to go!";
                            action.type = ActionType.None;
                            action.emotion = "confused";
                            return action;
                        }
                    }
                    action.targetObjectId = target.ObjectId;
                    return action;
                }
            }

            // No specific target or target not found — fall back to nearest pickable
            GridObject nearby = objects.GetPickableObjectNear(character.GridPosition);
            if (nearby != null)
            {
                // Simple comprehension can't auto-walk to distant objects
                if (profile.GetComprehensionLevel() == ComprehensionLevel.Simple)
                {
                    int dist = Mathf.Abs(nearby.GridPosition.x - character.GridPosition.x) +
                               Mathf.Abs(nearby.GridPosition.y - character.GridPosition.y);
                    if (dist > 1)
                    {
                        action.dialogue += " I can see it but it's too far away. Tell me which way to go!";
                        action.type = ActionType.None;
                        action.emotion = "confused";
                        return action;
                    }
                }
                action.targetObjectId = nearby.ObjectId;
                return action;
            }

            action.dialogue += " I don't see anything I can pick up nearby.";
            action.type = ActionType.None;
            return action;
        }

        /// <summary>
        /// Fuzzy lookup: tries exact ID, then display name match, then vocabulary reverse lookup.
        /// </summary>
        private static GridObject FindObjectByNameOrId(string nameOrId, ObjectManager objects,
            CharacterProfileData profile = null)
        {
            if (string.IsNullOrEmpty(nameOrId)) return null;

            // Exact ID
            GridObject obj = objects.GetObject(nameOrId);
            if (obj != null) return obj;

            // Fuzzy name match
            string lower = nameOrId.ToLower();
            var all = objects.GetAllObjects();
            if (all == null) return null;

            foreach (var o in all)
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

            // Reverse vocabulary lookup: LLM uses character's vocabulary names
            if (profile != null)
            {
                string realName = ReverseVocabularyLookup(nameOrId, profile);
                if (realName != null)
                {
                    string realLower = realName.ToLower();
                    foreach (var o in all)
                    {
                        if (o == null) continue;
                        string display = o.DisplayName?.ToLower() ?? "";
                        if (display.Contains(realLower) || realLower.Contains(display))
                        {
                            return o;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Reverse maps a vocabulary replacement back to the real object name.
        /// e.g. "sparkle" → "Ruby"
        /// </summary>
        private static string ReverseVocabularyLookup(string vocabularyName, CharacterProfileData profile)
        {
            if (profile?.perception_quirks == null) return null;

            string vocabLower = vocabularyName.ToLower();
            foreach (var quirk in profile.perception_quirks)
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

        private static CharacterAction ValidatePutDown(
            CharacterAction action, ExplorerController character)
        {
            if (!character.IsHoldingObject)
            {
                action.dialogue += " I'm not holding anything.";
                action.type = ActionType.None;
                return action;
            }

            return action;
        }

        private static CharacterAction ValidateUse(
            CharacterAction action, ExplorerController character, ObjectManager objects,
            CharacterProfileData profile)
        {
            if (!character.IsHoldingObject)
            {
                action.dialogue += " I'm not holding anything to use.";
                action.type = ActionType.None;
                return action;
            }

            GridObject heldObject = character.HeldGridObject;

            // Check nearby first (skip the object we're holding)
            GridObject nearbyTarget = objects.GetInteractableObjectNear(character.GridPosition);
            if (nearbyTarget != null && nearbyTarget != heldObject)
            {
                action.useOnTarget = nearbyTarget.ObjectId;
                return action;
            }

            // Not nearby — try finding the named target (executor will move there)
            if (!string.IsNullOrEmpty(action.useOnTarget))
            {
                GridObject target = FindObjectByNameOrId(action.useOnTarget, objects, profile);
                if (target != null && target != heldObject)
                {
                    // Simple comprehension can't auto-walk to distant targets
                    if (profile.GetComprehensionLevel() == ComprehensionLevel.Simple)
                    {
                        int dist = Mathf.Abs(target.GridPosition.x - character.GridPosition.x) +
                                   Mathf.Abs(target.GridPosition.y - character.GridPosition.y);
                        if (dist > 1)
                        {
                            action.dialogue += " I can see it but it's too far away. Tell me which way to go!";
                            action.type = ActionType.None;
                            action.emotion = "confused";
                            return action;
                        }
                    }
                    action.useOnTarget = target.ObjectId;
                    return action;
                }
            }

            // Also check if there's a door anywhere (common "use key on door" scenario)
            var allObjects = objects.GetAllObjects();
            if (allObjects != null)
            {
                foreach (var o in allObjects)
                {
                    if (o is DoorObject)
                    {
                        // Simple comprehension can't auto-walk to distant targets
                        if (profile.GetComprehensionLevel() == ComprehensionLevel.Simple)
                        {
                            int dist = Mathf.Abs(o.GridPosition.x - character.GridPosition.x) +
                                       Mathf.Abs(o.GridPosition.y - character.GridPosition.y);
                            if (dist > 1)
                            {
                                action.dialogue += " I can see it but it's too far away. Tell me which way to go!";
                                action.type = ActionType.None;
                                action.emotion = "confused";
                                return action;
                            }
                        }
                        action.useOnTarget = o.ObjectId;
                        return action;
                    }
                }
            }

            action.dialogue += " I don't see anything nearby to use this on.";
            action.type = ActionType.None;
            return action;
        }

        private static CharacterAction ValidatePush(
            CharacterAction action, ExplorerController character,
            GridManager grid, ObjectManager objects)
        {
            BoxObject box = objects.GetPushableBoxOnTile(character.GridPosition);
            if (box == null)
            {
                action.dialogue += " There's nothing here I can push.";
                action.type = ActionType.None;
                return action;
            }

            Direction? dir = ParseDirection(action.direction);
            if (!dir.HasValue)
            {
                action.dialogue += " Push it which way?";
                action.type = ActionType.None;
                return action;
            }

            Vector2Int pushDir = ExplorerController.DirectionToVector(dir.Value);
            if (!box.CanPushInDirection(pushDir))
            {
                action.dialogue += " I can't push it that way, something's blocking it.";
                action.type = ActionType.None;
                return action;
            }

            return action;
        }

        private static CharacterAction ValidateOpenClose(
            CharacterAction action, ExplorerController character, ObjectManager objects,
            CharacterProfileData profile)
        {
            // Find a door — nearby first, then anywhere
            DoorObject door = null;

            GridObject nearby = objects.GetInteractableObjectNear(character.GridPosition);
            if (nearby is DoorObject nearbyDoor)
            {
                door = nearbyDoor;
            }
            else
            {
                // Search all objects for a door (executor will move there)
                var allObjects = objects.GetAllObjects();
                if (allObjects != null)
                {
                    foreach (var o in allObjects)
                    {
                        if (o is DoorObject d) { door = d; break; }
                    }
                }
            }

            if (door == null)
            {
                action.dialogue += " I don't see a door.";
                action.type = ActionType.None;
                return action;
            }

            // Simple comprehension can't auto-walk to distant doors
            if (profile.GetComprehensionLevel() == ComprehensionLevel.Simple)
            {
                int dist = Mathf.Abs(door.GridPosition.x - character.GridPosition.x) +
                           Mathf.Abs(door.GridPosition.y - character.GridPosition.y);
                if (dist > 1)
                {
                    action.dialogue += " I can see a door but it's too far away. Tell me which way to go!";
                    action.type = ActionType.None;
                    action.emotion = "confused";
                    return action;
                }
            }

            // If locked but holding the right key, convert to Use action
            if (door.DoorState == DoorState.Locked)
            {
                if (character.IsHoldingObject && character.HeldGridObject is KeyObject)
                {
                    action.type = ActionType.Use;
                    action.useOnTarget = door.ObjectId;
                    return action;
                }

                action.dialogue += " It's locked. I need something to unlock it.";
                action.type = ActionType.None;
                return action;
            }

            action.useOnTarget = door.ObjectId;
            return action;
        }

        private static Vector2Int? ResolveTargetPosition(
            string targetId, ObjectManager objects, GridManager grid,
            CharacterProfileData profile = null)
        {
            if (string.IsNullOrEmpty(targetId)) return null;

            string targetLower = targetId.ToLower();

            // Check for exit references (including vocabulary reverse lookup)
            string realName = profile != null ? ReverseVocabularyLookup(targetId, profile) : null;
            string realLower = realName?.ToLower();

            bool isExitRef = targetLower.Contains("exit") || targetLower.Contains("doorway") ||
                targetLower.Contains("way out") || targetLower.Contains("leave") ||
                targetLower.Contains("escape") || targetLower.Contains("through");
            if (!isExitRef && realLower != null)
                isExitRef = realLower.Contains("exit");

            if (isExitRef)
            {
                Tile exitTile = grid.GetExitTile();
                if (exitTile != null) return exitTile.GridPosition;

                // Check for open door tiles (door = exit in key levels)
                var doorTiles = grid.GetTilesOfType(TileType.Door);
                if (doorTiles != null)
                {
                    foreach (var tile in doorTiles)
                    {
                        if (tile.IsOpen) return tile.GridPosition;
                    }
                    // Return first door tile even if closed (character will walk near it)
                    foreach (var tile in doorTiles)
                    {
                        return tile.GridPosition;
                    }
                }
            }

            // Try direct ID lookup
            GridObject obj = objects.GetObject(targetId);
            if (obj != null) return obj.GridPosition;

            // Try fuzzy matching on display names and IDs
            var allObjects = objects.GetAllObjects();
            if (allObjects != null)
            {
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

                // Reverse vocabulary lookup
                if (realLower != null)
                {
                    foreach (var o in allObjects)
                    {
                        if (o == null) continue;
                        string displayLower = o.DisplayName?.ToLower() ?? "";
                        if (displayLower.Contains(realLower) || realLower.Contains(displayLower))
                        {
                            return o.GridPosition;
                        }
                    }
                }
            }

            return null;
        }

        public static Direction? ParseDirection(string dirStr)
        {
            if (string.IsNullOrEmpty(dirStr)) return null;

            return dirStr.ToLower().Trim() switch
            {
                "north" or "up" => Direction.North,
                "south" or "down" => Direction.South,
                "east" or "right" => Direction.East,
                "west" or "left" => Direction.West,
                "forward" => Direction.North, // Will be converted by executor for relative mode
                "backward" or "back" => Direction.South,
                _ => null
            };
        }
    }
}
