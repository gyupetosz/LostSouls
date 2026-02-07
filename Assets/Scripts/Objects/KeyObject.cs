using UnityEngine;
using LostSouls.Core;
using LostSouls.Grid;
using LostSouls.Character;

namespace LostSouls.Objects
{
    public class KeyObject : GridObject
    {
        [Header("Key Properties")]
        [SerializeField] private string unlocksDoorId;
        [SerializeField] private bool isPickedUp;

        public string UnlocksDoorId => unlocksDoorId;
        public bool IsPickedUp => isPickedUp;

        public override void Initialize(ObjectData data, GridManager gridMgr, ObjectManager objMgr)
        {
            base.Initialize(data, gridMgr, objMgr);
            if (data.properties != null)
            {
                unlocksDoorId = data.properties.unlocks_door_id;
            }
        }

        public override bool CanPickUp() => !isPickedUp;

        public override void OnPickedUp(ExplorerController character)
        {
            isPickedUp = true;

            if (currentTile != null)
            {
                currentTile.ClearOccupant(gameObject);
            }

            transform.SetParent(character.transform);
            transform.localPosition = Vector3.up * 1.5f;

            Debug.Log($"Key '{displayName}' picked up (unlocks: {unlocksDoorId})");
            NotifyStateChanged();
        }

        public override void OnPutDown(Vector2Int position)
        {
            isPickedUp = false;
            transform.SetParent(null);
            SetGridPosition(position);

            Debug.Log($"Key '{displayName}' placed at {position}");
            NotifyStateChanged();
        }

        public bool TryUnlockDoor(DoorObject door)
        {
            if (door == null) return false;
            if (door.ObjectId != unlocksDoorId) return false;

            bool unlocked = door.Unlock(this);
            if (unlocked)
            {
                Debug.Log($"Key '{displayName}' unlocked door '{door.DisplayName}'");
                objectManager?.UnregisterObject(this);
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        public override string GetDescription()
        {
            string desc = $"A {color} key";
            if (!string.IsNullOrEmpty(shape)) desc += $" ({shape})";
            return desc;
        }
    }
}
