using UnityEngine;
using LostSouls.Core;
using LostSouls.Grid;
using LostSouls.Character;

namespace LostSouls.Objects
{
    public class DoorObject : GridObject
    {
        [Header("Door Properties")]
        [SerializeField] private DoorState doorState = DoorState.Closed;
        [SerializeField] private string requiredKeyId;

        public DoorState DoorState => doorState;
        public string RequiredKeyId => requiredKeyId;
        public bool IsLocked => doorState == DoorState.Locked;
        public bool IsOpen => doorState == DoorState.Open;

        public override void Initialize(ObjectData data, GridManager gridMgr, ObjectManager objMgr)
        {
            base.Initialize(data, gridMgr, objMgr);

            if (data.properties != null)
            {
                requiredKeyId = data.properties.required_key_id;

                doorState = data.properties.state?.ToLower() switch
                {
                    "open" => DoorState.Open,
                    "closed" => DoorState.Closed,
                    "locked" => DoorState.Locked,
                    _ => DoorState.Closed
                };
            }

            ApplyStateToTile();
        }

        public override bool CanOpenClose() => true;
        public override bool CanUseItemOn() => IsLocked;
        public override bool BlocksMovement() => !IsOpen;

        public bool Unlock(KeyObject key)
        {
            if (doorState != DoorState.Locked) return false;
            if (key.UnlocksDoorId != objectId) return false;

            doorState = DoorState.Closed;
            Debug.Log($"Door '{displayName}' unlocked");

            Open();
            NotifyStateChanged();
            return true;
        }

        public bool Open()
        {
            if (doorState == DoorState.Locked)
            {
                Debug.Log($"Door '{displayName}' is locked!");
                return false;
            }
            if (doorState == DoorState.Open) return true;

            doorState = DoorState.Open;
            ApplyStateToTile();
            Debug.Log($"Door '{displayName}' opened");
            NotifyStateChanged();
            objectManager?.NotifyObjectiveStateChanged();
            return true;
        }

        public bool Close()
        {
            if (doorState != DoorState.Open) return false;

            doorState = DoorState.Closed;
            ApplyStateToTile();
            Debug.Log($"Door '{displayName}' closed");
            NotifyStateChanged();
            objectManager?.NotifyObjectiveStateChanged();
            return true;
        }

        public override bool OnItemUsed(GridObject item, ExplorerController character)
        {
            if (item is KeyObject key)
            {
                bool unlocked = Unlock(key);
                if (unlocked)
                {
                    // Destroy the key and clear from character
                    character.ClearHeldObject();
                    objectManager?.UnregisterObject(key);
                    Destroy(key.gameObject);
                }
                return unlocked;
            }
            return false;
        }

        private void ApplyStateToTile()
        {
            if (currentTile == null) return;

            if (doorState == DoorState.Open)
            {
                currentTile.Open();
            }
            else
            {
                currentTile.Close();
            }
        }

        protected override Vector3 GetVerticalOffset() => Vector3.zero;

        public override string GetDescription()
        {
            return $"A door ({doorState})";
        }
    }
}
