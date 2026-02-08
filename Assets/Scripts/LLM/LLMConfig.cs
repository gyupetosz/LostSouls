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

            // Try Resources/api_config.json first
            TextAsset configAsset = Resources.Load<TextAsset>("api_config");
            if (configAsset != null)
            {
                _instance = JsonUtility.FromJson<LLMConfig>(configAsset.text);
                if (_instance != null && _instance.IsValid())
                {
                    Debug.Log($"LLM config loaded from Resources: model={_instance.model}");
                    return _instance;
                }
            }

            // Fallback: read from project root api_config.template.json
            string templatePath = System.IO.Path.Combine(Application.dataPath, "..", "api_config.template.json");
            if (System.IO.File.Exists(templatePath))
            {
                string json = System.IO.File.ReadAllText(templatePath);
                _instance = JsonUtility.FromJson<LLMConfig>(json);
                if (_instance != null && _instance.IsValid())
                {
                    Debug.Log($"LLM config loaded from api_config.template.json: model={_instance.model}");
                    return _instance;
                }
            }

            if (_instance == null || !_instance.IsValid())
            {
                Debug.LogWarning("API key not configured. Put your key in Assets/Resources/api_config.json or api_config.template.json in the project root.");
                _instance = null;
                return null;
            }

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
