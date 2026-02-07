using System;
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
    public class LLMResponse
    {
        public string dialogue;
        public string action;
        public ActionParams @params;
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
        public static CharacterAction Parse(string rawResponse)
        {
            if (string.IsNullOrEmpty(rawResponse))
            {
                return CharacterAction.Fallback();
            }

            string cleaned = StripMarkdownFences(rawResponse.Trim());

            try
            {
                LLMResponse response = JsonUtility.FromJson<LLMResponse>(cleaned);
                if (response == null)
                {
                    return CharacterAction.Fallback();
                }

                return new CharacterAction
                {
                    type = ParseActionType(response.action),
                    targetObjectId = response.@params?.target,
                    direction = response.@params?.direction,
                    steps = response.@params?.steps > 0 ? response.@params.steps : 1,
                    dialogue = response.dialogue ?? "",
                    emotion = response.emotion ?? "neutral",
                    useOnTarget = response.@params?.use_on
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to parse LLM response: {e.Message}\nRaw: {rawResponse}");
                return CharacterAction.Fallback();
            }
        }

        private static string StripMarkdownFences(string text)
        {
            // Remove ```json ... ``` or ``` ... ```
            var match = Regex.Match(text, @"```(?:json)?\s*\n?(.*?)\n?\s*```", RegexOptions.Singleline);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
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
