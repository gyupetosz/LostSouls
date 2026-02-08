using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LostSouls.LLM
{
    public enum ActionType
    {
        Move,
        MoveTo,
        Turn,
        Look,
        Examine,
        PickUp,
        PutDown,
        Use,
        Push,
        OpenClose,
        Wait,
        None
    }

    [Serializable]
    public class LLMActionItem
    {
        public string action;
        public ActionParams @params;
    }

    [Serializable]
    public class LLMResponse
    {
        public string dialogue;
        public string action;         // Single action (backward compat)
        public ActionParams @params;  // Single action params (backward compat)
        public List<LLMActionItem> actions; // Multi-action array
        public string emotion;
    }

    [Serializable]
    public class ActionParams
    {
        public string direction;
        public int steps = 1;
        public string target;
        public string use_on;
    }

    public class CharacterAction
    {
        public ActionType type;
        public string targetObjectId;
        public string direction;
        public int steps = 1;
        public string dialogue;
        public string emotion;
        public string useOnTarget;

        public static CharacterAction Fallback(string dialogue = null)
        {
            return new CharacterAction
            {
                type = ActionType.None,
                dialogue = dialogue ?? "I... I'm not sure what to do.",
                emotion = "confused"
            };
        }
    }

    public static class ResponseParser
    {
        /// <summary>
        /// Parses an LLM response into a list of CharacterActions.
        /// Standard/Clever characters may return multiple actions.
        /// </summary>
        public static List<CharacterAction> ParseMultiple(string rawResponse)
        {
            if (string.IsNullOrEmpty(rawResponse))
            {
                return new List<CharacterAction> { CharacterAction.Fallback() };
            }

            string cleaned = StripMarkdownFences(rawResponse.Trim());

            try
            {
                LLMResponse response = JsonUtility.FromJson<LLMResponse>(cleaned);
                if (response == null)
                {
                    return new List<CharacterAction> { CharacterAction.Fallback() };
                }

                string dialogue = response.dialogue ?? "";
                string emotion = response.emotion ?? "neutral";

                var results = new List<CharacterAction>();

                // Try multi-action array first
                if (response.actions != null && response.actions.Count > 0)
                {
                    for (int i = 0; i < response.actions.Count; i++)
                    {
                        var item = response.actions[i];
                        results.Add(new CharacterAction
                        {
                            type = ParseActionType(item.action),
                            targetObjectId = item.@params?.target,
                            direction = item.@params?.direction,
                            steps = item.@params?.steps > 0 ? item.@params.steps : 1,
                            dialogue = i == 0 ? dialogue : "", // Only first action gets dialogue
                            emotion = i == 0 ? emotion : "neutral",
                            useOnTarget = item.@params?.use_on
                        });
                    }
                }

                // Fallback to single action field
                if (results.Count == 0)
                {
                    results.Add(new CharacterAction
                    {
                        type = ParseActionType(response.action),
                        targetObjectId = response.@params?.target,
                        direction = response.@params?.direction,
                        steps = response.@params?.steps > 0 ? response.@params.steps : 1,
                        dialogue = dialogue,
                        emotion = emotion,
                        useOnTarget = response.@params?.use_on
                    });
                }

                return results;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to parse LLM response: {e.Message}\nRaw: {rawResponse}");
                // Use the raw LLM text as dialogue instead of a generic fallback
                string fallbackDialogue = rawResponse.Trim();
                // Clean up any partial JSON artifacts
                if (fallbackDialogue.Length > 300)
                    fallbackDialogue = fallbackDialogue.Substring(0, 300) + "...";
                return new List<CharacterAction>
                {
                    new CharacterAction
                    {
                        type = ActionType.None,
                        dialogue = fallbackDialogue,
                        emotion = "neutral"
                    }
                };
            }
        }

        /// <summary>
        /// Backward-compatible single-action parse (returns first action only).
        /// </summary>
        public static CharacterAction Parse(string rawResponse)
        {
            var actions = ParseMultiple(rawResponse);
            return actions.Count > 0 ? actions[0] : CharacterAction.Fallback();
        }

        private static string StripMarkdownFences(string text)
        {
            // Remove ```json ... ``` or ``` ... ```
            var match = Regex.Match(text, @"```(?:json)?\s*\n?(.*?)\n?\s*```", RegexOptions.Singleline);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // If response has text before/after JSON, extract the JSON object
            if (!text.TrimStart().StartsWith("{"))
            {
                int firstBrace = text.IndexOf('{');
                int lastBrace = text.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    return text.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                }
            }

            return text;
        }

        private static ActionType ParseActionType(string action)
        {
            if (string.IsNullOrEmpty(action)) return ActionType.None;

            return action.ToLower().Trim() switch
            {
                "move" => ActionType.Move,
                "move_to" => ActionType.MoveTo,
                "turn" => ActionType.Turn,
                "look" => ActionType.Look,
                "examine" => ActionType.Examine,
                "pick_up" => ActionType.PickUp,
                "put_down" => ActionType.PutDown,
                "use" => ActionType.Use,
                "push" => ActionType.Push,
                "open_close" => ActionType.OpenClose,
                "open" => ActionType.OpenClose,
                "close" => ActionType.OpenClose,
                "wait" => ActionType.Wait,
                "none" => ActionType.None,
                _ => ActionType.None
            };
        }
    }
}
