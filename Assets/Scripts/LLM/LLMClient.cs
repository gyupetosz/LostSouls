using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LostSouls.LLM
{
    public class LLMClient : MonoBehaviour
    {
        private LLMConfig config;
        private bool isProcessing;

        public bool IsProcessing => isProcessing;

        private void Awake()
        {
            config = LLMConfig.Load();
        }

        public void SendMessage(string systemPrompt, string userMessage,
            Action<string> onResponse, Action<string> onError)
        {
            // Retry loading config if it failed on Awake (e.g. key was added after startup)
            if (config == null || !config.IsValid())
            {
                LLMConfig.ClearCache();
                config = LLMConfig.Load();
            }

            if (config == null || !config.IsValid())
            {
                onError?.Invoke("API key not configured. Edit Assets/Resources/api_config.json.");
                return;
            }

            if (isProcessing)
            {
                onError?.Invoke("Already processing a request.");
                return;
            }

            StartCoroutine(SendRequestCoroutine(systemPrompt, userMessage, onResponse, onError));
        }

        private IEnumerator SendRequestCoroutine(string systemPrompt, string userMessage,
            Action<string> onResponse, Action<string> onError)
        {
            isProcessing = true;

            string requestBody = BuildRequestBody(systemPrompt, userMessage);

            using var request = new UnityWebRequest(config.api_url, "POST");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {config.openai_api_key}");
            request.timeout = 15;

            yield return request.SendWebRequest();

            isProcessing = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"API error: {request.error} (HTTP {request.responseCode})";
                Debug.LogWarning(error);
                onError?.Invoke(error);
                yield break;
            }

            try
            {
                string responseJson = request.downloadHandler.text;
                string content = ExtractContent(responseJson);

                if (string.IsNullOrEmpty(content))
                {
                    onError?.Invoke("Empty response from API.");
                    yield break;
                }

                onResponse?.Invoke(content);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Response parse error: {e.Message}");
            }
        }

        private string BuildRequestBody(string systemPrompt, string userMessage)
        {
            // Build JSON manually to avoid JsonUtility issues with nested arrays
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":\"{EscapeJson(config.model)}\",");
            sb.Append($"\"max_tokens\":{config.max_tokens},");
            sb.Append($"\"temperature\":{config.temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append("\"messages\":[");
            sb.Append($"{{\"role\":\"system\",\"content\":\"{EscapeJson(systemPrompt)}\"}},");
            sb.Append($"{{\"role\":\"user\",\"content\":\"{EscapeJson(userMessage)}\"}}");
            sb.Append("]}");
            return sb.ToString();
        }

        private static string ExtractContent(string responseJson)
        {
            // Parse the OpenAI response to extract the assistant's message content
            // Response format: { "choices": [{ "message": { "content": "..." } }] }
            try
            {
                var wrapper = JsonUtility.FromJson<OpenAIResponse>(responseJson);
                if (wrapper?.choices != null && wrapper.choices.Length > 0)
                {
                    return wrapper.choices[0].message?.content;
                }
            }
            catch
            {
                // Fallback: regex extract
                var match = System.Text.RegularExpressions.Regex.Match(
                    responseJson, "\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                if (match.Success)
                {
                    return System.Text.RegularExpressions.Regex.Unescape(match.Groups[1].Value);
                }
            }
            return null;
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        // OpenAI response deserialization classes
        [Serializable]
        private class OpenAIResponse
        {
            public Choice[] choices;
        }

        [Serializable]
        private class Choice
        {
            public Message message;
        }

        [Serializable]
        private class Message
        {
            public string role;
            public string content;
        }
    }
}
