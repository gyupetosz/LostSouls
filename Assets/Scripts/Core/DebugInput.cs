using UnityEngine;
using UnityEngine.EventSystems;
using LostSouls.Character;
using LostSouls.Grid;
using LostSouls.Objects;

namespace LostSouls.Core
{
    /// <summary>
    /// Debug input handler for testing game functionality.
    /// </summary>
    public class DebugInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ExplorerController character;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Pathfinding pathfinding;

        [Header("Settings")]
        [SerializeField] private bool debugEnabled = false;
        [SerializeField] private bool showDebugInfo = true;

        private void Update()
        {
            // F12 toggles debug mode on/off
            if (Input.GetKeyDown(KeyCode.F12))
            {
                debugEnabled = !debugEnabled;
                Debug.Log($"Debug input {(debugEnabled ? "ENABLED" : "DISABLED")}");
            }

            if (!debugEnabled) return;

            if (character == null)
            {
                character = FindObjectOfType<ExplorerController>();
                if (character == null) return;
            }

            if (gridManager == null)
                gridManager = FindObjectOfType<GridManager>();

            if (pathfinding == null)
                pathfinding = FindObjectOfType<Pathfinding>();

            // Don't process input while moving
            if (character.IsMoving) return;

            // Don't process debug input while typing in a UI field
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Arrow keys / WASD for movement or push (with Shift)
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                if (shift) TryPushBox(Direction.North);
                else TryMove(Direction.North);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                if (shift) TryPushBox(Direction.South);
                else TryMove(Direction.South);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                if (shift) TryPushBox(Direction.East);
                else TryMove(Direction.East);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                if (shift) TryPushBox(Direction.West);
                else TryMove(Direction.West);
            }

            // Space to pathfind to exit
            if (Input.GetKeyDown(KeyCode.Space))
            {
                MoveToExit();
            }

            // R to restart level
            if (Input.GetKeyDown(KeyCode.R))
            {
                GameManager.Instance?.RestartLevel();
            }

            // Rotate with Q and E
            if (Input.GetKeyDown(KeyCode.Q))
            {
                character.TurnRelative("left");
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                character.TurnRelative("right");
            }

            // F to pick up nearby object (or pick gem off pedestal)
            if (Input.GetKeyDown(KeyCode.F))
            {
                TryPickUp();
            }

            // G to put down / place gem on pedestal
            if (Input.GetKeyDown(KeyCode.G))
            {
                TryPutDown();
            }

            // T to use held item on nearby target
            if (Input.GetKeyDown(KeyCode.T))
            {
                TryUseItem();
            }

            // O to toggle exit open/closed
            if (Input.GetKeyDown(KeyCode.O))
            {
                ToggleExit();
            }

            // Number keys to load specific levels
            if (Input.GetKeyDown(KeyCode.Alpha1)) LoadDebugLevel(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) LoadDebugLevel(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) LoadDebugLevel(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) LoadDebugLevel(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) LoadDebugLevel(5);
        }

        private void TryMove(Direction direction)
        {
            bool success = character.MoveInDirection(direction, 1);
            if (showDebugInfo)
            {
                if (success)
                    Debug.Log($"Moving {direction}");
                else
                    Debug.Log($"Cannot move {direction} - blocked");
            }
        }

        private void TryPickUp()
        {
            ObjectManager objManager = ObjectManager.Instance;
            if (objManager == null) return;

            // First check if there's a pedestal with a gem nearby to pick the gem off
            GridObject nearby = objManager.GetInteractableObjectNear(character.GridPosition);
            if (nearby is PedestalObject pedestal && pedestal.HasGem)
            {
                if (character.IsHoldingObject)
                {
                    Debug.Log("Already holding something — can't pick up gem from pedestal");
                    return;
                }

                GemObject gem = pedestal.RemoveGem();
                if (gem != null)
                {
                    character.PickUp(gem);
                    return;
                }
            }

            // Otherwise try picking up a regular object
            GridObject obj = objManager.GetPickableObjectNear(character.GridPosition);
            if (obj != null)
            {
                character.PickUp(obj);
            }
            else
            {
                Debug.Log("Nothing nearby to pick up");
            }
        }

        private void TryPutDown()
        {
            if (!character.IsHoldingObject)
            {
                Debug.Log("Not holding anything to put down");
                return;
            }

            GridObject held = character.HeldGridObject;

            // If holding a gem, check for nearby pedestal
            if (held is GemObject gem)
            {
                ObjectManager objManager = ObjectManager.Instance;
                if (objManager != null)
                {
                    GridObject target = objManager.GetInteractableObjectNear(character.GridPosition);
                    if (target is PedestalObject pedestal)
                    {
                        if (gem.TryPlaceOnPedestal(pedestal))
                        {
                            character.ClearHeldObject();
                            return;
                        }
                    }
                }
            }

            // Otherwise just drop it
            character.PutDown();
        }

        private void TryUseItem()
        {
            bool success = character.UseHeldObject();
            if (!success)
            {
                Debug.Log("Could not use item (no valid target or not holding anything)");
            }
        }

        private void TryPushBox(Direction direction)
        {
            bool success = character.PushBox(direction);
            if (showDebugInfo)
            {
                if (success)
                    Debug.Log($"Pushing box {direction}");
                else
                    Debug.Log($"Cannot push box {direction} — no box on tile or destination blocked");
            }
        }

        private void ToggleExit()
        {
            if (gridManager == null) return;

            Tile exitTile = gridManager.GetExitTile();
            if (exitTile == null)
            {
                Debug.Log("No exit tile found!");
                return;
            }

            if (exitTile.IsOpen)
            {
                gridManager.CloseExits();
                Debug.Log("Exit closed");
            }
            else
            {
                gridManager.OpenExits();
                Debug.Log("Exit opened");
            }
        }

        private void LoadDebugLevel(int levelId)
        {
            if (GameManager.Instance != null)
            {
                Debug.Log($"Loading level {levelId} via debug key");
                GameManager.Instance.LoadLevel(levelId);
            }
        }

        private void MoveToExit()
        {
            if (gridManager == null) return;

            Tile exitTile = gridManager.GetExitTile();
            if (exitTile == null)
            {
                Debug.Log("No exit tile found!");
                return;
            }

            bool success = character.MoveTo(exitTile.GridPosition);
            if (showDebugInfo)
            {
                if (success)
                    Debug.Log($"Pathfinding to exit at {exitTile.GridPosition}");
                else
                    Debug.Log("No path to exit!");
            }
        }

        private void OnGUI()
        {
            if (!debugEnabled || !showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 300));
            GUILayout.Label("=== Debug Controls ===");
            GUILayout.Label("Arrow Keys / WASD: Move");
            GUILayout.Label("Shift + Dir: Push Box (same tile)");
            GUILayout.Label("Q / E: Rotate Left / Right");
            GUILayout.Label("F: Pick Up | G: Put Down");
            GUILayout.Label("T: Use Item on Target");
            GUILayout.Label("Space: Pathfind to Exit");
            GUILayout.Label("R: Restart | O: Toggle Exit");
            GUILayout.Label("1-6: Load Level");
            GUILayout.Space(10);

            if (character != null)
            {
                GUILayout.Label($"Position: {character.GridPosition}");
                GUILayout.Label($"Facing: {character.Facing}");
                GUILayout.Label($"Moving: {character.IsMoving}");
                string holding = character.IsHoldingObject ? character.HeldGridObject.DisplayName : "nothing";
                GUILayout.Label($"Holding: {holding}");
            }

            if (GameManager.Instance != null)
            {
                GUILayout.Label($"State: {GameManager.Instance.CurrentState}");
                GUILayout.Label($"Level: {GameManager.Instance.CurrentLevelId}");
            }

            GUILayout.EndArea();
        }
    }
}
