using System;
using UnityEngine;

namespace LostSouls.Character
{
    [CreateAssetMenu(fileName = "CharacterModelDatabase", menuName = "Lost Souls/Character Model Database")]
    public class CharacterModelDatabase : ScriptableObject
    {
        public CharacterModelEntry[] entries;

        public CharacterModelEntry GetEntry(string modelId)
        {
            if (string.IsNullOrEmpty(modelId) || entries == null) return null;

            foreach (var entry in entries)
            {
                if (string.Equals(entry.modelId, modelId, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }
    }

    [Serializable]
    public class CharacterModelEntry
    {
        public string modelId;
        public GameObject modelPrefab;
        public Texture2D texture;
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip pick;
        public AnimationClip push;
        public AnimationClip pushIdle;
        public AnimationClip holdIdle;
        public AnimationClip holdWalk;
    }
}
