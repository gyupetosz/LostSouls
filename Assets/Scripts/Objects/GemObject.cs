using UnityEngine;
using LostSouls.Core;
using LostSouls.Grid;
using LostSouls.Character;

namespace LostSouls.Objects
{
    public class GemObject : GridObject
    {
        [Header("Gem Properties")]
        [SerializeField] private string targetPedestalId;
        [SerializeField] private bool isPickedUp;
        [SerializeField] private bool isOnPedestal;

        public string TargetPedestalId => targetPedestalId;
        public bool IsPickedUp => isPickedUp;
        public bool IsOnPedestal => isOnPedestal;

        public override void Initialize(ObjectData data, GridManager gridMgr, ObjectManager objMgr)
        {
            base.Initialize(data, gridMgr, objMgr);
            if (data.properties != null)
            {
                targetPedestalId = data.properties.target_pedestal_id;
            }
        }

        public override bool CanPickUp() => !isPickedUp;

        public override void OnPickedUp(ExplorerController character)
        {
            isPickedUp = true;
            isOnPedestal = false;

            if (currentTile != null)
            {
                currentTile.ClearOccupant(gameObject);
            }

            transform.SetParent(character.transform);
            transform.localPosition = Vector3.up * 1.5f;

            Debug.Log($"Gem '{displayName}' picked up");
            NotifyStateChanged();
        }

        public override void OnPutDown(Vector2Int position)
        {
            isPickedUp = false;
            transform.SetParent(null);
            SetGridPosition(position);

            Debug.Log($"Gem '{displayName}' placed at {position}");
            NotifyStateChanged();
        }

        public bool TryPlaceOnPedestal(PedestalObject pedestal)
        {
            if (pedestal == null) return false;

            bool placed = pedestal.AcceptGem(this);
            if (placed)
            {
                isOnPedestal = true;
                isPickedUp = false;
                transform.SetParent(null);

                // Position on pedestal
                Vector3 pedestalPos = pedestal.transform.position;
                transform.position = pedestalPos + Vector3.up * 0.6f;

                Debug.Log($"Gem '{displayName}' placed on pedestal '{pedestal.DisplayName}'");
                NotifyStateChanged();
                return true;
            }
            return false;
        }

        public void RemoveFromPedestal()
        {
            isOnPedestal = false;
            NotifyStateChanged();
        }

        public override string GetDescription()
        {
            return $"A {color} {size} gem";
        }
    }
}
