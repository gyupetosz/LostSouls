using System.Collections.Generic;
using System.Text.RegularExpressions;
using LostSouls.Core;

namespace LostSouls.LLM
{
    public class SanitizeResult
    {
        public bool passed;
        public string rejectionDialogue;
        public bool costsPrompt;

        public static SanitizeResult Pass() => new SanitizeResult { passed = true };

        public static SanitizeResult Reject(string dialogue, bool costsPrompt)
        {
            return new SanitizeResult
            {
                passed = false,
                rejectionDialogue = dialogue,
                costsPrompt = costsPrompt
            };
        }
    }

    public static class InputSanitizer
    {
        private static readonly string[] InjectionPatterns = new[]
        {
            "ignore previous", "ignore above", "ignore all", "you are now",
            "system:", "system prompt", "forget everything", "act as",
            "repeat your instructions", "what are your rules",
            "reveal your prompt", "new instructions", "override",
            "pretend you are", "roleplay as", "disregard",
            "ignore your instructions", "bypass"
        };

        private static readonly string[] ProfanityPatterns = new[]
        {
            // Basic profanity filter — can be expanded
            "fuck", "shit", "damn", "bitch", "ass hole"
        };

        public static SanitizeResult Sanitize(
            string input,
            int maxLength,
            CharacterProfileData profile,
            string characterName)
        {
            // Step 1: Length check (does NOT cost a prompt)
            if (string.IsNullOrWhiteSpace(input))
            {
                return SanitizeResult.Reject("Say something to guide the explorer.", false);
            }

            if (input.Length > maxLength)
            {
                return SanitizeResult.Reject(
                    "Your spirit energy is too dispersed. Try a shorter message.", false);
            }

            string inputLower = input.ToLower();

            // Step 2: Prompt injection detection (costs a prompt)
            foreach (var pattern in InjectionPatterns)
            {
                if (inputLower.Contains(pattern))
                {
                    return SanitizeResult.Reject(
                        "I don't understand what you mean. Can you just help me get out of here?",
                        true);
                }
            }

            // Step 3: Profanity filter (costs a prompt)
            foreach (var word in ProfanityPatterns)
            {
                if (Regex.IsMatch(inputLower, $@"\b{Regex.Escape(word)}\b"))
                {
                    return SanitizeResult.Reject(
                        $"{characterName} frowns. \"That's not very nice. I'd rather you spoke kindly.\"",
                        true);
                }
            }

            // Step 4: Personality pre-filter (costs a prompt — these are gameplay mechanics)
            if (profile?.personality_quirks != null)
            {
                foreach (var quirk in profile.personality_quirks)
                {
                    if (quirk.type?.ToLower() == "polite")
                    {
                        if (!HasPoliteKeyword(inputLower, quirk.config?.required_keywords))
                        {
                            string refusal = quirk.config?.refusal_response ??
                                "*looks away, seemingly offended by the lack of manners*";
                            return SanitizeResult.Reject(refusal, false);
                        }
                    }
                }
            }

            return SanitizeResult.Pass();
        }

        private static bool HasPoliteKeyword(string inputLower, List<string> keywords)
        {
            if (keywords == null || keywords.Count == 0)
            {
                // Default polite keywords
                keywords = new List<string>
                {
                    "please", "could you", "would you", "kindly", "if you don't mind"
                };
            }

            foreach (var keyword in keywords)
            {
                if (inputLower.Contains(keyword.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
