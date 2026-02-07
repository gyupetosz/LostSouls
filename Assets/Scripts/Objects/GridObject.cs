using Assets.Scripts.Core;
using LostSouls.Character;
using LostSouls.Core;
using LostSouls.Grid;
using System;
using UnityEngine;

namespace LostSouls.Objects
{
    public abstract class GridObject : MonoBehaviour
    {
        [Header("Object Identity")]
        [SerializeField] protected string objectId;
        [SerializeField] protected string displayName;
        [SerializeField] protected ObjectType objectType;
        [SerializeField] protected Vector2Int gridPosition;

        [Header("Object Properties")]
        [SerializeField] protected string color;
        [SerializeField] protected string size;
        [SerializeField] protected string shape;

        protected GridManager gridManager;
        protected ObjectManager objectManager;
        protected Tile currentTile;

        public string ObjectId => objectId;
        public string DisplayName => displayName;
        public ObjectType Type => objectType;
        public Vector2Int GridPosition => gridPosition;
        public Tile CurrentTile => currentTile;

        public event Action<GridObject> OnStateChanged;

        public virtual void Initialize(ObjectData data, GridManager gridMgr, ObjectManager objMgr)
        {
            objectId = data.id;
            displayName = data.display_name;
            objectType = data.GetObjectType();
            gridPosition = data.position.ToVector2Int();
            gridManager = gridMgr;
            objectManager = objMgr;

            if (data.properties != null)
            {
                color = data.properties.color;
                size = data.properties.size;
                shape = data.properties.shape;
            }

            currentTile = gridManager.GetTile(gridPosition);
        }

        // Interaction query methods — override in subclasses
        public virtual bool CanPickUp() => false;
        public virtual bool CanPush() => false;
        public virtual bool CanUseItemOn() => false;
        public virtual bool CanOpenClose() => false;
        public virtual bool BlocksMovement() => false;

        // Interaction callbacks — override in subclasses
        public virtual void OnPickedUp(ExplorerController character)
        {
            // Play pick-up sound at the character's position
            GlobalAudio.PlayItemPickUp(character.transform.position);
        }

        public virtual void OnPutDown(Vector2Int position)
        {
            // Convert grid position to world position if needed
            Vector3 worldPos = new Vector3(position.x, 0, position.y);
            GlobalAudio.PlayItemPutDown(worldPos);
        }
        public virtual bool OnItemUsed(GridObject item, ExplorerController character) => false;
        public virtual void OnPushed(Vector2Int pushDirection, Vector2Int newPosition) { }
        public virtual void OnCharacterEntered(ExplorerController character) { }
        public virtual void OnCharacterExited(ExplorerController character) { }

        public virtual void SetGridPosition(Vector2Int newPosition)
        {
            if (currentTile != null)
            {
                currentTile.ClearOccupant(gameObject);
            }

            gridPosition = newPosition;
            currentTile = gridManager.GetTile(newPosition);

            if (currentTile != null)
            {
                int subTile = currentTile.SetItem(gameObject, preferredSubTile: 3);
                Vector3 worldPos = gridManager.GridToWorldPosition(newPosition)
                    + currentTile.GetSubTileOffset(subTile);
                transform.position = worldPos + GetVerticalOffset();
            }
        }

        protected virtual Vector3 GetVerticalOffset() => Vector3.up * 0.3f;

        protected void NotifyStateChanged()
        {
            OnStateChanged?.Invoke(this);
        }

        public virtual string GetDescription()
        {
            return $"{displayName} ({objectType})";
        }
    }
}
