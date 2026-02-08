using UnityEngine;

namespace Assets.Scripts.Core
{
    public static class GlobalAudio
    {
        // Audio clips
        private static AudioClip rockRumble;
        private static AudioClip boxPushing;
        private static AudioClip itemPickUp;
        private static AudioClip itemPutDown;
        private static AudioClip pressurePlate;
        private static AudioClip wrongItem;
        private static AudioClip pressurePlateGround;
        private static AudioClip footsteps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadAudio()
        {
            // Load all clips from Assets/Resources (or fallback to AssetDatabase in editor)
            rockRumble = LoadClip("RockRumble", "Assets/Sounds/RockRumble.wav");
            boxPushing = LoadClip("BoxPushing", "Assets/Sounds/BoxPushing.wav");
            itemPickUp = LoadClip("ItemPickUp", "Assets/Sounds/ItemPickUp.wav");
            itemPutDown = LoadClip("ItemPutDown", "Assets/Sounds/ItemPutDown.wav");
            pressurePlate = LoadClip("PressurePlate", "Assets/Sounds/PressurePlate.wav");
            wrongItem = LoadClip("WrongItem", "Assets/Sounds/WrongItem.wav");
            pressurePlateGround = LoadClip("PressurePlateGround", "Assets/Sounds/PressurePlateGround.wav");
            footsteps = LoadClip("Footsteps", "Assets/Sounds/Footsteps.wav");
        }

        private static AudioClip LoadClip(string resourceName, string editorPath)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourceName);

#if UNITY_EDITOR
            if (clip == null)
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(editorPath);
            }
#endif
            return clip;
        }

        // Generic helper
        private static void PlayClip(AudioClip clip, Vector3 position)
        {
            if (clip == null) return;

            GameObject temp = new GameObject("TempAudio_" + clip.name);
            temp.transform.position = position;

            var source = temp.AddComponent<AudioSource>();
            source.spatialBlend = 1f;                 // 3D sound
            source.pitch = Random.Range(0.95f, 1.05f);
            source.volume = Random.Range(0.85f, 1f);

            source.PlayOneShot(clip);

            Object.Destroy(temp, clip.length);
        }

        // Now expose one function per sound
        public static void PlayRockRumble(Vector3 position) => PlayClip(rockRumble, position);
        public static void PlayBoxPushing(Vector3 position) => PlayClip(boxPushing, position);
        public static void PlayItemPickUp(Vector3 position) => PlayClip(itemPickUp, position);
        public static void PlayItemPutDown(Vector3 position) => PlayClip(itemPutDown, position);
        public static void PlayPressurePlate(Vector3 position) => PlayClip(pressurePlate, position);
        public static void PlayWrongItem(Vector3 position) => PlayClip(wrongItem, position);
        public static void PlayPressurePlateGround(Vector3 position) => PlayClip(pressurePlateGround, position);
        public static void PlayFootsteps(Vector3 position) => PlayClip(footsteps, position);
    }
}