using System;
using System.Collections.Generic;
using UnityEngine;
using LostSouls.Objects;

namespace LostSouls.Core
{
    public class ObjectManager : MonoBehaviour
    {
        public static ObjectManager Instance { get; private set; }

        private Dictionary<string, GridObject> objectsById = new Dictionary<string, GridObject>();
        private List<GridObject> allObjects = new List<GridObject>();

        public event Action OnObjectiveStateChanged;

        public int ObjectCount => allObjects.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void RegisterObject(GridObject obj)
        {
            if (obj == null || string.IsNullOrEmpty(obj.ObjectId)) return;

            if (objectsById.ContainsKey(obj.ObjectId))
            {
                Debug.LogWarning($"ObjectManager: Duplicate object ID '{obj.ObjectId}'");
                return;
            }

            objectsById[obj.ObjectId] = obj;
            allObjects.Add(obj);

            obj.OnStateChanged += HandleObjectStateChanged;
            Debug.Log($"ObjectManager: Registered '{obj.ObjectId}' ({obj.Type})");
        }

        public void UnregisterObject(GridObject obj)
        {
            if (obj == null) return;
            obj.OnStateChanged -= HandleObjectStateChanged;
            objectsById.Remove(obj.ObjectId);
            allObjects.Remove(obj);
        }

        public GridObject GetObject(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return objectsById.TryGetValue(id, out GridObject obj) ? obj : null;
        }

        public T GetObject<T>(string id) where T : GridObject
        {
            return GetObject(id) as T;
        }

        public List<T> GetObjectsOfType<T>() where T : GridObject
        {
            List<T> result = new List<T>();
            foreach (var obj in allObjects)
            {
                if (obj is T typed) result.Add(typed);
            }
            return result;
        }

        public List<GridObject> GetObjectsAtPosition(Vector2Int position)
        {
            List<GridObject> result = new List<GridObject>();
            foreach (var obj in allObjects)
            {
                if (obj.GridPosition == position) result.Add(obj);
            }
            return result;
        }

        public GridObject GetPickableObjectNear(Vector2Int position)
        {
            // Check same tile first
            foreach (var obj in GetObjectsAtPosition(position))
            {
                if (obj.CanPickUp()) return obj;
            }

            // Check adjacent tiles
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in directions)
            {
                foreach (var obj in GetObjectsAtPosition(position + dir))
                {
                    if (obj.CanPickUp()) return obj;
                }
            }
            return null;
        }

        public GridObject GetInteractableObjectNear(Vector2Int position)
        {
            // Check same tile first
            foreach (var obj in GetObjectsAtPosition(position))
            {
                if (obj.CanUseItemOn() || obj.CanOpenClose()) return obj;
            }

            // Check adjacent tiles
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in directions)
            {
                foreach (var obj in GetObjectsAtPosition(position + dir))
                {
                    if (obj.CanUseItemOn() || obj.CanOpenClose()) return obj;
                }
            }
            return null;
        }

        public BoxObject GetPushableBoxInDirection(Vector2Int characterPos, Vector2Int direction)
        {
            Vector2Int boxPos = characterPos + direction;
            foreach (var obj in GetObjectsAtPosition(boxPos))
            {
                if (obj is BoxObject box && box.CanPush()) return box;
            }
            return null;
        }

        /// <summary>
        /// Finds a pushable box on the same tile as the character
        /// </summary>
        public BoxObject GetPushableBoxOnTile(Vector2Int position)
        {
            foreach (var obj in GetObjectsAtPosition(position))
            {
                if (obj is BoxObject box && box.CanPush()) return box;
            }
            return null;
        }

        public bool AreAllDoorsOpen()
        {
            var doors = GetObjectsOfType<DoorObject>();
            if (doors.Count == 0) return true;

            foreach (var door in doors)
            {
                if (!door.IsOpen) return false;
            }
            return true;
        }

        public bool AreAllPedestalsActivated()
        {
            var pedestals = GetObjectsOfType<PedestalObject>();
            if (pedestals.Count == 0) return true;

            foreach (var pedestal in pedestals)
            {
                if (!pedestal.IsActivated) return false;
            }
            return true;
        }

        public void NotifyObjectiveStateChanged()
        {
            OnObjectiveStateChanged?.Invoke();
        }

        public void ClearAll()
        {
            foreach (var obj in allObjects)
            {
                if (obj != null)
                {
                    obj.OnStateChanged -= HandleObjectStateChanged;
                }
            }
            objectsById.Clear();
            allObjects.Clear();
        }

        private void HandleObjectStateChanged(GridObject obj)
        {
            // Could trigger UI updates in the future
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
