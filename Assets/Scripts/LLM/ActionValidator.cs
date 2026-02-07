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
                    return ValidateMoveTo(action, character, grid, pathfinding, objects);

                case ActionType.PickUp:
                    return ValidatePickUp(action, character, objects);

                case ActionType.PutDown:
                    return ValidatePutDown(action, character);

                case ActionType.Use:
                    return ValidateUse(action, character, objects);

                case ActionType.Push:
                    return ValidatePush(action, character, grid, objects);

                case ActionType.OpenClose:
                    return ValidateOpenClose(action, character, objects);

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
            GridManager grid, Pathfinding pathfinding, ObjectManager objects)
        {
            // Try to find target position
            Vector2Int? targetPos = ResolveTargetPosition(action.targetObjectId, objects, grid);

            if (!targetPos.HasValue)
            {
                action.dialogue += " I can't find what you're referring to.";
                action.type = ActionType.None;
                return action;
            }

            // Check if path exists
            var path = pathfinding.FindPath(character.GridPosition, targetPos.Value, ignoreOccupants: true);
            if (path == null || path.Count == 0)
            {
                action.dialogue += " I can't find a way to get there.";
                action.type = ActionType.None;
                return action;
            }

            return action;
        }

        private static CharacterAction ValidatePickUp(
            CharacterAction action, ExplorerController character, ObjectManager objects)
        {
            if (character.IsHoldingObject)
            {
                action.dialogue += " My hands are full already!";
                action.type = ActionType.None;
                return action;
            }

            GridObject target = objects.GetPickableObjectNear(character.GridPosition);
            if (target == null)
            {
                action.dialogue += " I don't see anything I can pick up nearby.";
                action.type = ActionType.None;
                return action;
            }

            // Set the resolved target ID for the executor
            action.targetObjectId = target.ObjectId;
            return action;
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
            CharacterAction action, ExplorerController character, ObjectManager objects)
        {
            if (!character.IsHoldingObject)
            {
                action.dialogue += " I'm not holding anything to use.";
                action.type = ActionType.None;
                return action;
            }

            GridObject target = objects.GetInteractableObjectNear(character.GridPosition);
            if (target == null)
            {
                action.dialogue += " I don't see anything nearby to use this on.";
                action.type = ActionType.None;
                return action;
            }

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
            CharacterAction action, ExplorerController character, ObjectManager objects)
        {
            GridObject target = objects.GetInteractableObjectNear(character.GridPosition);
            if (target == null || !(target is DoorObject))
            {
                action.dialogue += " I don't see a door nearby.";
                action.type = ActionType.None;
                return action;
            }

            DoorObject door = target as DoorObject;
            if (door.DoorState == DoorState.Locked)
            {
                action.dialogue += " It's locked. I need something to unlock it.";
                action.type = ActionType.None;
                return action;
            }

            return action;
        }

        private static Vector2Int? ResolveTargetPosition(
            string targetId, ObjectManager objects, GridManager grid)
        {
            if (string.IsNullOrEmpty(targetId)) return null;

            string targetLower = targetId.ToLower();

            // Check for exit references
            if (targetLower.Contains("exit") || targetLower.Contains("doorway") ||
                targetLower.Contains("way out"))
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
