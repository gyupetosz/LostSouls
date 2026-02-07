using System;
using UnityEngine;
using LostSouls.Core;
using LostSouls.Grid;
using LostSouls.Character;

namespace LostSouls.Objects
{
    public class PedestalObject : GridObject
    {
        [Header("Pedestal Properties")]
        [SerializeField] private string acceptsGemId;
        [SerializeField] private bool activated;
        [SerializeField] private GemObject placedGem;

        public string AcceptsGemId => acceptsGemId;
        public bool IsActivated => activated;
        public GemObject PlacedGem => placedGem;
        public bool HasGem => placedGem != null;

        public event Action<PedestalObject> OnActivated;
        public event Action<PedestalObject> OnDeactivated;

        public override void Initialize(ObjectData data, GridManager gridMgr, ObjectManager objMgr)
        {
            base.Initialize(data, gridMgr, objMgr);
            if (data.properties != null)
            {
                acceptsGemId = data.properties.accepts_gem_id;
            }
        }

        public override bool CanUseItemOn() => true; // any gem can be placed
        public override bool BlocksMovement() => true; // can't push box onto pedestal

        public override bool OnItemUsed(GridObject item, ExplorerController character)
        {
            if (item is GemObject gem)
            {
                bool placed = gem.TryPlaceOnPedestal(this);
                if (placed)
                {
                    character.ClearHeldObject();
                }
                return placed;
            }
            return false;
        }

        public bool AcceptGem(GemObject gem)
        {
            if (placedGem != null)
            {
                Debug.Log($"Pedestal '{displayName}' already has a gem on it");
                return false;
            }

            placedGem = gem;

            // Check if this is the matching gem
            bool isMatch = !string.IsNullOrEmpty(acceptsGemId) && gem.ObjectId == acceptsGemId;
            if (isMatch)
            {
                activated = true;
                Debug.Log($"Pedestal '{displayName}' activated by matching gem '{gem.DisplayName}'!");
                NotifyStateChanged();
                OnActivated?.Invoke(this);
                objectManager?.NotifyObjectiveStateChanged();
            }
            else
            {
                Debug.Log($"Gem '{gem.DisplayName}' placed on pedestal '{displayName}' but doesn't match (expected: {acceptsGemId})");
            }

            return true;
        }

        public GemObject RemoveGem()
        {
            if (placedGem == null) return null;

            GemObject gem = placedGem;
            placedGem = null;

            bool wasActivated = activated;
            activated = false;

            gem.RemoveFromPedestal();

            if (wasActivated)
            {
                Debug.Log($"Pedestal '{displayName}' deactivated — gem removed");
                NotifyStateChanged();
                OnDeactivated?.Invoke(this);
                objectManager?.NotifyObjectiveStateChanged();
            }
            else
            {
                Debug.Log($"Gem removed from pedestal '{displayName}'");
            }

            return gem;
        }

        public override string GetDescription()
        {
            if (activated)
                return $"An activated pedestal (with {placedGem?.DisplayName})";
            if (placedGem != null)
                return $"A pedestal (with wrong gem: {placedGem.DisplayName})";
            return "An empty pedestal";
        }
    }
}
