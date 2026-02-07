using System;
using UnityEngine;
using LostSouls.Core;
using LostSouls.Grid;
using LostSouls.Character;

namespace LostSouls.Objects
{
    public class PressurePlateObject : GridObject
    {
        [Header("Pressure Plate Properties")]
        [SerializeField] private string linkedObjectId;
        [SerializeField] private bool activated;

        public string LinkedObjectId => linkedObjectId;
        public bool IsActivated => activated;

        public event Action<PressurePlateObject, bool> OnActivationChanged;

        public override void Initialize(ObjectData data, GridManager gridMgr, ObjectManager objMgr)
        {
            base.Initialize(data, gridMgr, objMgr);
            if (data.properties != null)
            {
                linkedObjectId = data.properties.linked_object_id;
            }
        }

        public override void OnCharacterEntered(ExplorerController character)
        {
            Activate();
        }

        public override void OnCharacterExited(ExplorerController character)
        {
            if (!HasBoxOnPlate())
            {
                Deactivate();
            }
        }

        public void OnBoxPlaced(BoxObject box)
        {
            Activate();
        }

        public void OnBoxRemoved(BoxObject box)
        {
            if (!HasCharacterOnPlate())
            {
                Deactivate();
            }
        }

        private void Activate()
        {
            if (activated) return;
            activated = true;

            Debug.Log($"Pressure plate '{displayName}' activated!");
            TriggerLinkedObject(true);
            NotifyStateChanged();
            OnActivationChanged?.Invoke(this, true);
            objectManager?.NotifyObjectiveStateChanged();
        }

        private void Deactivate()
        {
            if (!activated) return;
            activated = false;

            Debug.Log($"Pressure plate '{displayName}' deactivated!");
            TriggerLinkedObject(false);
            NotifyStateChanged();
            OnActivationChanged?.Invoke(this, false);
            objectManager?.NotifyObjectiveStateChanged();
        }

        private void TriggerLinkedObject(bool activate)
        {
            if (string.IsNullOrEmpty(linkedObjectId) || objectManager == null) return;

            GridObject linked = objectManager.GetObject(linkedObjectId);
            if (linked == null)
            {
                Debug.LogWarning($"Pressure plate '{displayName}': linked object '{linkedObjectId}' not found");
                return;
            }

            if (linked is DoorObject door)
            {
                if (activate) door.Open();
                else door.Close();
            }
        }

        private bool HasBoxOnPlate()
        {
            if (objectManager == null) return false;
            var objectsHere = objectManager.GetObjectsAtPosition(gridPosition);
            foreach (var obj in objectsHere)
            {
                if (obj is BoxObject) return true;
            }
            return false;
        }

        private bool HasCharacterOnPlate()
        {
            if (currentTile == null) return false;
            return currentTile.IsSubTileOccupied(0);
        }

        protected override Vector3 GetVerticalOffset() => Vector3.zero;

        public override string GetDescription()
        {
            return $"A pressure plate ({(activated ? "pressed" : "released")})";
        }
    }
}
