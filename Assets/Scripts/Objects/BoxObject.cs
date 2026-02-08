using Assets.Scripts.Core;
using LostSouls.Character;
using LostSouls.Core;
using LostSouls.Grid;
using System.Collections;
using UnityEngine;

namespace LostSouls.Objects
{
    public class BoxObject : GridObject
    {
        [Header("Box Properties")]
        [SerializeField] private float weight = 1f;
        [SerializeField] private float pushAnimDuration = 1.5f;
        [SerializeField] private bool isBeingPushed;

        public float Weight => weight;
        public bool IsBeingPushed => isBeingPushed;

        public override void Initialize(ObjectData data, GridManager gridMgr, ObjectManager objMgr)
        {
            base.Initialize(data, gridMgr, objMgr);
            if (data.properties != null && data.properties.weight > 0)
            {
                weight = data.properties.weight;
            }
        }

        public override bool CanPush() => !isBeingPushed;
        public override bool BlocksMovement() => true;

        public bool CanPushInDirection(Vector2Int pushDirection)
        {
            Vector2Int targetPos = gridPosition + pushDirection;

            Tile targetTile = gridManager.GetTile(targetPos);
            if (targetTile == null || !targetTile.IsWalkable) return false;

            // Check for blocking objects at target
            if (objectManager != null)
            {
                var objectsAtTarget = objectManager.GetObjectsAtPosition(targetPos);
                foreach (var obj in objectsAtTarget)
                {
                    if (obj.BlocksMovement()) return false;
                }
            }

            return true;
        }

        public override void OnPushed(Vector2Int pushDirection, Vector2Int newPosition)
        {
            Vector2Int oldPosition = gridPosition;

            // Check for pressure plate at old position
            PressurePlateObject oldPlate = GetPressurePlateAt(oldPosition);

            // Clear from old tile
            if (currentTile != null)
            {
                currentTile.ClearOccupant(gameObject);
            }

            gridPosition = newPosition;
            currentTile = gridManager.GetTile(newPosition);

            if (currentTile != null)
            {
                currentTile.SetItem(gameObject, preferredSubTile: 3);
            }

            // Animate the push
            StartCoroutine(AnimatePush(newPosition));

            GlobalAudio.PlayBoxPushing(transform.position);

            // Notify old pressure plate
            if (oldPlate != null)
            {
                oldPlate.OnBoxRemoved(this);
            }

            // Check for pressure plate at new position
            PressurePlateObject newPlate = GetPressurePlateAt(newPosition);
            if (newPlate != null)
            {
                newPlate.OnBoxPlaced(this);
            }

            Debug.Log($"Box '{displayName}' pushed from {oldPosition} to {newPosition}");
            NotifyStateChanged();
        }

        private IEnumerator AnimatePush(Vector2Int targetGridPos)
        {
            isBeingPushed = true;

            Vector3 startPos = transform.position;
            // Box always goes to tile center
            Vector3 endPos = gridManager.GridToWorldPosition(targetGridPos) + GetVerticalOffset();

            float elapsed = 0f;
            while (elapsed < pushAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / pushAnimDuration);
                transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            transform.position = endPos;
            isBeingPushed = false;
        }

        private PressurePlateObject GetPressurePlateAt(Vector2Int position)
        {
            if (objectManager == null) return null;
            var objectsHere = objectManager.GetObjectsAtPosition(position);
            foreach (var obj in objectsHere)
            {
                if (obj is PressurePlateObject plate) return plate;
            }
            return null;
        }

        public override string GetDescription()
        {
            return $"A {size} box (weight: {weight})";
        }
    }
}
