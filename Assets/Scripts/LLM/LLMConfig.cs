using UnityEngine;

namespace LostSouls.LLM
{
    [System.Serializable]
    public class LLMConfig
    {
        public string openai_api_key;
        public string model = "gpt-4o-mini";
        public string api_url = "https://api.openai.com/v1/chat/completions";
        public int max_tokens = 300;
        public float temperature = 0.7f;

        private static LLMConfig _instance;

        public static LLMConfig Load()
        {
            if (_instance != null && _instance.IsValid()) return _instance;

            // Clear stale cache so we always re-read from disk
            _instance = null;

            TextAsset configAsset = Resources.Load<TextAsset>("api_config");
            if (configAsset == null)
            {
                Debug.LogWarning("api_config.json not found in Resources/. Copy api_config.example.json to api_config.json and add your API key.");
                return null;
            }

            _instance = JsonUtility.FromJson<LLMConfig>(configAsset.text);

            if (string.IsNullOrEmpty(_instance.openai_api_key) || _instance.openai_api_key == "YOUR_API_KEY_HERE")
            {
                Debug.LogWarning("OpenAI API key not configured. Edit Assets/Resources/api_config.json with your key.");
                _instance = null;
                return null;
            }

            Debug.Log($"LLM config loaded: model={_instance.model}");
            return _instance;
        }

        /// <summary>
        /// Clears cached config so next Load() re-reads from disk.
        /// Call this after editing api_config.json at runtime.
        /// </summary>
        public static void ClearCache()
        {
            _instance = null;
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(openai_api_key) &&
                   openai_api_key != "YOUR_API_KEY_HERE" &&
                   !string.IsNullOrEmpty(api_url);
        }
    }
}
