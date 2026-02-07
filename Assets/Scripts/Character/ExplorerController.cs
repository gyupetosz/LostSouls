using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LostSouls.Core;
using LostSouls.Grid;
using LostSouls.Objects;

namespace LostSouls.Character
{
    public class ExplorerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float turnSpeed = 360f;
        [SerializeField] private float arrivalThreshold = 0.01f;

        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Pathfinding pathfinding;

        [Header("State")]
        [SerializeField] private Vector2Int currentGridPosition;
        [SerializeField] private Direction facingDirection = Direction.North;
        [SerializeField] private bool isMoving;
        [SerializeField] private GridObject heldGridObject;
        [SerializeField] private int currentSubTile;  // Which sub-tile we're standing on

        [Header("Character Data")]
        [SerializeField] private string characterId;
        [SerializeField] private string characterName;

        // Events
        public event Action<Vector2Int> OnPositionChanged;
        public event Action<Direction> OnDirectionChanged;
        public event Action OnMovementStarted;
        public event Action OnMovementCompleted;
        public event Action<Vector2Int> OnReachedExit;
        public event Action OnPickedUpObject;
        public event Action OnPutDownObject;
        public event Action OnPushStarted;

        // Properties
        public Vector2Int GridPosition => currentGridPosition;
        public Direction Facing => facingDirection;
        public bool IsMoving => isMoving;
        public bool IsHoldingObject => heldGridObject != null;
        public GridObject HeldGridObject => heldGridObject;
        public GameObject HeldObject => heldGridObject?.gameObject;
        public string CharacterId => characterId;
        public string CharacterName => characterName;

        // Movement coroutine
        private Coroutine movementCoroutine;
        private List<Vector2Int> currentPath;
        private int currentPathIndex;

        private void Awake()
        {
            if (gridManager == null)
                gridManager = FindObjectOfType<GridManager>();
            if (pathfinding == null)
                pathfinding = FindObjectOfType<Pathfinding>();
        }

        /// <summary>
        /// Initializes the character at a specific grid position
        /// </summary>
        public void Initialize(string id, string name, Vector2Int startPosition, GridManager grid, Pathfinding path)
        {
            characterId = id;
            characterName = name;
            gridManager = grid;
            pathfinding = path;

            SetPosition(startPosition, immediate: true);
            UpdateRotation(true);
        }

        /// <summary>
        /// Sets the character's position on the grid
        /// </summary>
        public void SetPosition(Vector2Int gridPos, bool immediate = false)
        {
            // Clear old tile occupancy
            Tile oldTile = gridManager?.GetTile(currentGridPosition);
            if (oldTile != null)
            {
                oldTile.ClearOccupant(gameObject);
            }

            currentGridPosition = gridPos;

            // Set new tile occupancy with sub-tile
            Tile newTile = gridManager?.GetTile(gridPos);
            if (newTile != null)
            {
                currentSubTile = newTile.SetOccupant(gameObject, preferredSubTile: 0);
            }

            if (immediate && gridManager != null)
            {
                Vector3 worldPos = gridManager.GridToWorldPosition(gridPos);
                if (newTile != null)
                {
                    worldPos += newTile.GetSubTileOffset(currentSubTile);
                }
                transform.position = worldPos + Vector3.up * 0.5f;
            }

            // Notify pressure plates on enter/exit
            PressurePlateObject oldPlate = oldTile?.GetComponent<PressurePlateObject>();
            PressurePlateObject newPlate = newTile?.GetComponent<PressurePlateObject>();
            if (oldPlate != null && oldTile != newTile)
            {
                oldPlate.OnCharacterExited(this);
            }
            if (newPlate != null && oldTile != newTile)
            {
                newPlate.OnCharacterEntered(this);
            }

            OnPositionChanged?.Invoke(gridPos);

            // Check if reached exit (exit tile or open door)
            if (newTile != null &&
                (newTile.TileType == TileType.Exit ||
                 (newTile.TileType == TileType.Door && newTile.IsOpen)))
            {
                OnReachedExit?.Invoke(gridPos);
            }
        }

        /// <summary>
        /// Sets the facing direction
        /// </summary>
        public void SetFacing(Direction direction)
        {
            facingDirection = direction;
            OnDirectionChanged?.Invoke(direction);
        }

        /// <summary>
        /// Moves the character to a target position using pathfinding
        /// </summary>
        public bool MoveTo(Vector2Int targetPosition)
        {
            if (isMoving) return false;

            List<Vector2Int> path = pathfinding.FindPath(currentGridPosition, targetPosition, ignoreOccupants: false);

            if (path.Count == 0)
            {
                Debug.Log($"No path found from {currentGridPosition} to {targetPosition}");
                return false;
            }

            StartMovementAlongPath(path);
            return true;
        }

        /// <summary>
        /// Moves the character in a direction by a number of steps
        /// </summary>
        public bool MoveInDirection(Direction direction, int steps = 1)
        {
            if (isMoving) return false;

            Vector2Int directionVector = DirectionToVector(direction);
            Vector2Int targetPosition = currentGridPosition + (directionVector * steps);

            // Check if path is clear
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int currentCheck = currentGridPosition;

            for (int i = 0; i < steps; i++)
            {
                currentCheck += directionVector;

                if (!gridManager.IsWalkable(currentCheck))
                {
                    Debug.Log($"Cannot move to {currentCheck} - not walkable");
                    return false;
                }

                path.Add(currentCheck);
            }

            if (path.Count == 0) return false;

            // Add start position at the beginning
            path.Insert(0, currentGridPosition);

            // Face the direction of movement
            SetFacing(direction);

            StartMovementAlongPath(path);
            return true;
        }

        /// <summary>
        /// Turns the character to face a direction
        /// </summary>
        public void Turn(Direction direction)
        {
            SetFacing(direction);
            UpdateRotation(false);
        }

        /// <summary>
        /// Turns the character relative to current facing
        /// </summary>
        public void TurnRelative(string relativeDirection)
        {
            Direction newDirection = facingDirection;

            switch (relativeDirection.ToLower())
            {
                case "left":
                    newDirection = RotateDirection(facingDirection, -1);
                    break;
                case "right":
                    newDirection = RotateDirection(facingDirection, 1);
                    break;
                case "around":
                case "back":
                    newDirection = RotateDirection(facingDirection, 2);
                    break;
            }

            Turn(newDirection);
        }

        /// <summary>
        /// Picks up a GridObject
        /// </summary>
        public bool PickUp(GridObject obj)
        {
            if (IsHoldingObject)
            {
                Debug.Log("Already holding an object");
                return false;
            }
            if (obj == null || !obj.CanPickUp())
            {
                Debug.Log("Cannot pick up this object");
                return false;
            }

            heldGridObject = obj;
            obj.OnPickedUp(this);
            OnPickedUpObject?.Invoke();
            Debug.Log($"Picked up: {obj.DisplayName}");
            return true;
        }

        /// <summary>
        /// Puts down the held object at current position
        /// </summary>
        public GridObject PutDown()
        {
            if (!IsHoldingObject)
            {
                Debug.Log("Not holding any object");
                return null;
            }

            GridObject obj = heldGridObject;
            heldGridObject = null;
            obj.OnPutDown(currentGridPosition);
            OnPutDownObject?.Invoke();
            Debug.Log($"Put down: {obj.DisplayName} at {currentGridPosition}");
            return obj;
        }

        /// <summary>
        /// Clears the held object reference (used when held item is consumed, e.g., key unlocks door)
        /// </summary>
        public void ClearHeldObject()
        {
            heldGridObject = null;
            OnPutDownObject?.Invoke();
        }

        /// <summary>
        /// Uses the held object on a nearby interactable target
        /// </summary>
        public bool UseHeldObject()
        {
            if (!IsHoldingObject)
            {
                Debug.Log("Not holding any object to use");
                return false;
            }

            ObjectManager objManager = ObjectManager.Instance;
            if (objManager == null) return false;

            GridObject target = objManager.GetInteractableObjectNear(currentGridPosition);
            if (target == null)
            {
                Debug.Log("No interactable object nearby");
                return false;
            }

            return UseHeldObjectOn(target);
        }

        /// <summary>
        /// Uses the held object on a specific target
        /// </summary>
        public bool UseHeldObjectOn(GridObject target)
        {
            if (!IsHoldingObject || target == null) return false;

            bool success = target.OnItemUsed(heldGridObject, this);
            if (success)
            {
                // Check if held object was consumed (e.g., key destroyed after unlocking)
                if (heldGridObject == null || heldGridObject.gameObject == null)
                {
                    heldGridObject = null;
                }
            }
            return success;
        }

        /// <summary>
        /// Pushes a box in the given direction. Character must be on the same tile as the box.
        /// Character walks to the sub-tile position behind the box (opposite push dir), faces the
        /// push direction, then the box slides one tile away. Character stays on the same tile.
        /// </summary>
        public bool PushBox(Direction direction)
        {
            if (isMoving) return false;

            ObjectManager objManager = ObjectManager.Instance;
            if (objManager == null) return false;

            // Box must be on the same tile as the character
            BoxObject box = objManager.GetPushableBoxOnTile(currentGridPosition);

            if (box == null)
            {
                Debug.Log($"No pushable box on current tile");
                return false;
            }

            Vector2Int pushDir = DirectionToVector(direction);

            if (!box.CanPushInDirection(pushDir))
            {
                Debug.Log($"Cannot push box {direction} - destination blocked");
                return false;
            }

            Vector2Int newBoxPos = currentGridPosition + pushDir;

            // Start the push sequence: animate character to position, then push box
            OnPushStarted?.Invoke();
            StartCoroutine(PushBoxSequence(box, direction, pushDir, newBoxPos));
            return true;
        }

        private IEnumerator PushBoxSequence(BoxObject box, Direction direction, Vector2Int pushDir, Vector2Int newBoxPos)
        {
            isMoving = true;
            OnMovementStarted?.Invoke();

            // Box is at tile center. Character walks to the sub-tile behind the box
            // (opposite of push direction) so it looks like pushing from behind.
            Tile tile = gridManager.GetTile(currentGridPosition);
            float quarter = tile != null ? tile.TileSize * 0.25f : 0.5f;

            // Character goes to the sub-tile on the opposite side of the push direction
            // e.g. pushing North → character stands on south side of tile (behind box)
            Vector3 behindOffset = direction switch
            {
                Direction.North => new Vector3(0, 0, -quarter),  // south side
                Direction.South => new Vector3(0, 0, quarter),   // north side
                Direction.East  => new Vector3(-quarter, 0, 0),  // west side
                Direction.West  => new Vector3(quarter, 0, 0),   // east side
                _ => Vector3.zero
            };

            Vector3 tileWorldPos = gridManager.GridToWorldPosition(currentGridPosition);
            Vector3 charBehindPos = tileWorldPos + behindOffset + Vector3.up * 0.5f;

            // Face the push direction
            SetFacing(direction);
            Quaternion targetRot = GetRotationForDirection(direction);

            // Phase 1: Animate character walking behind the box
            float moveTime = 0.25f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;

            while (elapsed < moveTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / moveTime);
                transform.position = Vector3.Lerp(startPos, charBehindPos, t);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = charBehindPos;
            transform.rotation = targetRot;

            // Phase 2: Push — box slides to next tile center, character follows through
            box.OnPushed(pushDir, newBoxPos);

            // Character follows: move from behind-box midpoint to the forward-side midpoint
            // (opposite edge of tile), so it looks like pushing the box across the tile
            Vector3 charPushTarget = tileWorldPos - behindOffset + Vector3.up * 0.5f;
            elapsed = 0f;
            float pushFollowTime = 0.6f; // Match box push duration
            startPos = transform.position;

            while (elapsed < pushFollowTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / pushFollowTime);
                transform.position = Vector3.Lerp(startPos, charPushTarget, t);
                yield return null;
            }

            transform.position = charPushTarget;

            isMoving = false;
            OnMovementCompleted?.Invoke();

            Debug.Log($"Pushed box {direction} to {newBoxPos}");
        }

        /// <summary>
        /// Stops any current movement
        /// </summary>
        public void StopMovement()
        {
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
                movementCoroutine = null;
            }
            isMoving = false;
            currentPath = null;
        }

        private void StartMovementAlongPath(List<Vector2Int> path)
        {
            if (path.Count < 2) return;

            currentPath = path;
            currentPathIndex = 1; // Start from first movement target

            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
            }

            movementCoroutine = StartCoroutine(MoveAlongPathCoroutine());
        }

        private IEnumerator MoveAlongPathCoroutine()
        {
            isMoving = true;
            OnMovementStarted?.Invoke();

            while (currentPathIndex < currentPath.Count)
            {
                Vector2Int targetGridPos = currentPath[currentPathIndex];

                // Calculate target with sub-tile offset
                Tile targetTile = gridManager.GetTile(targetGridPos);
                int targetSubTile = targetTile != null ? targetTile.GetAvailableSubTile(0) : 0;
                Vector3 subTileOffset = targetTile != null ? targetTile.GetSubTileOffset(targetSubTile) : Vector3.zero;
                Vector3 targetWorldPos = gridManager.GridToWorldPosition(targetGridPos) + subTileOffset + Vector3.up * 0.5f;

                // Face direction of movement
                Vector2Int moveDir = targetGridPos - currentGridPosition;
                if (moveDir != Vector2Int.zero)
                {
                    Direction dir = VectorToDirection(moveDir);
                    SetFacing(dir);
                    UpdateRotation(false);
                }

                // Move to target position
                while (Vector3.Distance(transform.position, targetWorldPos) > arrivalThreshold)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position,
                        targetWorldPos,
                        moveSpeed * Time.deltaTime
                    );

                    Quaternion targetRotation = GetRotationForDirection(facingDirection);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        turnSpeed * Time.deltaTime
                    );

                    yield return null;
                }

                // Snap to exact position
                transform.position = targetWorldPos;
                SetPosition(targetGridPos, immediate: false);

                currentPathIndex++;
            }

            isMoving = false;
            currentPath = null;
            OnMovementCompleted?.Invoke();
        }

        private void UpdateRotation(bool immediate)
        {
            Quaternion targetRotation = GetRotationForDirection(facingDirection);

            if (immediate)
            {
                transform.rotation = targetRotation;
            }
            else
            {
                StartCoroutine(SmoothRotateCoroutine(targetRotation));
            }
        }

        private IEnumerator SmoothRotateCoroutine(Quaternion targetRotation)
        {
            while (Quaternion.Angle(transform.rotation, targetRotation) > 0.5f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime
                );
                yield return null;
            }
            transform.rotation = targetRotation;
        }

        private Quaternion GetRotationForDirection(Direction direction)
        {
            float angle = direction switch
            {
                Direction.North => 0f,
                Direction.East => 90f,
                Direction.South => 180f,
                Direction.West => 270f,
                _ => 0f
            };
            return Quaternion.Euler(0f, angle, 0f);
        }

        public static Vector2Int DirectionToVector(Direction direction)
        {
            return direction switch
            {
                Direction.North => Vector2Int.up,
                Direction.South => Vector2Int.down,
                Direction.East => Vector2Int.right,
                Direction.West => Vector2Int.left,
                _ => Vector2Int.zero
            };
        }

        public static Direction VectorToDirection(Vector2Int vector)
        {
            if (vector.y > 0) return Direction.North;
            if (vector.y < 0) return Direction.South;
            if (vector.x > 0) return Direction.East;
            if (vector.x < 0) return Direction.West;
            return Direction.North;
        }

        public static Direction RotateDirection(Direction direction, int steps)
        {
            // Steps: positive = clockwise, negative = counter-clockwise
            int dirIndex = (int)direction;
            dirIndex = (dirIndex + steps + 4) % 4;
            return (Direction)dirIndex;
        }

        /// <summary>
        /// Converts a relative direction (forward, left, etc.) to absolute direction based on facing
        /// </summary>
        public Direction RelativeToAbsolute(string relativeDir)
        {
            return relativeDir.ToLower() switch
            {
                "forward" => facingDirection,
                "backward" or "back" => RotateDirection(facingDirection, 2),
                "left" => RotateDirection(facingDirection, -1),
                "right" => RotateDirection(facingDirection, 1),
                _ => facingDirection
            };
        }

        private void OnDrawGizmos()
        {
            // Draw facing direction
            Gizmos.color = Color.blue;
            Vector3 forward = GetRotationForDirection(facingDirection) * Vector3.forward;
            Gizmos.DrawRay(transform.position + Vector3.up, forward);

            // Draw current grid position
            if (gridManager != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 tileCenter = gridManager.GridToWorldPosition(currentGridPosition);
                Gizmos.DrawWireCube(tileCenter, Vector3.one * 0.5f);
            }
        }
    }
}
