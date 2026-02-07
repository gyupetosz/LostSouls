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
            if (_instance != null) return _instance;

            TextAsset configAsset = Resources.Load<TextAsset>("api_config");
            if (configAsset == null)
            {
                Debug.LogError("api_config.json not found in Resources/. Copy api_config.example.json to api_config.json and add your API key.");
                return null;
            }

            _instance = JsonUtility.FromJson<LLMConfig>(configAsset.text);

            if (string.IsNullOrEmpty(_instance.openai_api_key) || _instance.openai_api_key == "YOUR_API_KEY_HERE")
            {
                Debug.LogError("OpenAI API key not configured. Edit Assets/Resources/api_config.json with your key.");
                return null;
            }

            return _instance;
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(openai_api_key) &&
                   openai_api_key != "YOUR_API_KEY_HERE" &&
                   !string.IsNullOrEmpty(api_url);
        }
    }
}
