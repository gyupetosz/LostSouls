using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LostSouls.Character;
using LostSouls.Grid;
using LostSouls.LLM;
using LostSouls.Objects;

namespace LostSouls.Core
{
    public class TurnManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Pathfinding pathfinding;
        [SerializeField] private ObjectManager objectManager;

        [Header("State")]
        [SerializeField] private bool isProcessingTurn;
        [SerializeField] private int hintEscalationLevel;

        // LLM components
        private LLMClient llmClient;
        private ActionExecutor actionExecutor;

        // Personality state tracking
        private ActionType lastActionType = ActionType.None;
        private int trustCounter;
        private Dictionary<ActionType, int> stubbornAttempts = new Dictionary<ActionType, int>();

        // Current character data
        private ExplorerController currentCharacter;
        private CharacterProfileData currentProfile;

        // Events
        public event Action<string, string> OnCharacterResponse; // (dialogue, emotion)
        public event Action OnTurnStarted;
        public event Action OnTurnCompleted;
        public event Action<string> OnInputRejected; // rejection dialogue

        public bool IsProcessingTurn => isProcessingTurn;

        private void Awake()
        {
            if (gameManager == null)
                gameManager = FindObjectOfType<GameManager>();
            if (gridManager == null)
                gridManager = FindObjectOfType<GridManager>();
            if (pathfinding == null)
                pathfinding = FindObjectOfType<Pathfinding>();
            if (objectManager == null)
                objectManager = FindObjectOfType<ObjectManager>();

            // Create LLM components
            llmClient = gameObject.AddComponent<LLMClient>();
            actionExecutor = gameObject.AddComponent<ActionExecutor>();
            actionExecutor.OnActionFeedback += (dialogue, emotion) =>
            {
                OnCharacterResponse?.Invoke(dialogue, emotion);
            };
        }

        public void InitializeForLevel(ExplorerController character, CharacterProfileData profile)
        {
            currentCharacter = character;
            currentProfile = profile;

            // Reset personality state
            hintEscalationLevel = 0;
            lastActionType = ActionType.None;
            trustCounter = 0;
            stubbornAttempts.Clear();

            // Initialize executor
            actionExecutor.Initialize(character, gridManager, pathfinding, objectManager);
        }

        public void SubmitPrompt(string playerInput)
        {
            if (isProcessingTurn)
            {
                Debug.Log("TurnManager: Already processing a turn");
                return;
            }

            if (gameManager.CurrentState != GameState.Playing)
            {
                Debug.Log("TurnManager: Not in playing state");
                return;
            }

            if (currentCharacter == null || currentProfile == null)
            {
                Debug.LogError("TurnManager: No character or profile set");
                return;
            }

            StartCoroutine(ProcessTurnCoroutine(playerInput));
        }

        private IEnumerator ProcessTurnCoroutine(string playerInput)
        {
            isProcessingTurn = true;
            OnTurnStarted?.Invoke();

            Debug.Log($"[TurnManager] Processing prompt: \"{playerInput}\"");

            // Step 1: Input sanitization
            string characterName = currentProfile.name ?? "Explorer";
            int maxLength = gameManager.CurrentLevelData?.prompt_max_length ?? 150;

            SanitizeResult sanitizeResult;
            try
            {
                sanitizeResult = InputSanitizer.Sanitize(
                    playerInput, maxLength, currentProfile, characterName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TurnManager] Sanitizer crashed: {e}");
                isProcessingTurn = false;
                OnTurnCompleted?.Invoke();
                yield break;
            }

            if (!sanitizeResult.passed)
            {
                Debug.Log($"[TurnManager] Input rejected: {sanitizeResult.rejectionDialogue}");
                if (sanitizeResult.costsPrompt)
                {
                    gameManager.UsePrompt();
                    hintEscalationLevel++;
                }
                OnInputRejected?.Invoke(sanitizeResult.rejectionDialogue);
                OnCharacterResponse?.Invoke(sanitizeResult.rejectionDialogue, "annoyed");
                isProcessingTurn = false;
                OnTurnCompleted?.Invoke();
                yield break;
            }

            // Step 2: Cost a prompt
            if (!gameManager.UsePrompt())
            {
                isProcessingTurn = false;
                OnTurnCompleted?.Invoke();
                yield break;
            }

            // Step 3: Build system prompt
            string systemPrompt;
            try
            {
                systemPrompt = PromptBuilder.Build(
                    currentProfile, currentCharacter, gridManager, objectManager, hintEscalationLevel);
                Debug.Log($"[TurnManager] System prompt built ({systemPrompt.Length} chars)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TurnManager] PromptBuilder crashed: {e}");
                gameManager.RefundPrompt();
                isProcessingTurn = false;
                OnTurnCompleted?.Invoke();
                yield break;
            }

            // Step 4: Call LLM (with retry for transient errors like 429)
            string llmResponse = null;
            string llmError = null;
            int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                llmResponse = null;
                llmError = null;

                Debug.Log($"[TurnManager] Sending to LLM (attempt {attempt + 1}/{maxRetries})...");

                llmClient.SendMessage(systemPrompt, playerInput,
                    response => llmResponse = response,
                    error => llmError = error);

                // Wait for response
                while (llmResponse == null && llmError == null)
                {
                    yield return null;
                }

                // Success — break out
                if (llmResponse != null)
                {
                    Debug.Log($"[TurnManager] LLM response received ({llmResponse.Length} chars)");
                    break;
                }

                // Retry on 429 rate limit
                if (llmError != null && llmError.Contains("429"))
                {
                    float retryDelay = (attempt + 1) * 2f; // 2s, 4s, 6s
                    Debug.Log($"[TurnManager] Rate limited (429). Retrying in {retryDelay}s... (attempt {attempt + 1}/{maxRetries})");
                    yield return new WaitForSeconds(retryDelay);
                    continue;
                }

                // Non-retryable error — break
                break;
            }

            if (llmError != null)
            {
                Debug.LogWarning($"[TurnManager] LLM error: {llmError}");
                gameManager.RefundPrompt();
                OnCharacterResponse?.Invoke(
                    "*the connection wavers* ...Try again in a moment.", "confused");
                isProcessingTurn = false;
                OnTurnCompleted?.Invoke();
                yield break;
            }

            // Step 5: Parse response (may return multiple actions)
            List<CharacterAction> actions;
            try
            {
                actions = ResponseParser.ParseMultiple(llmResponse);

                // Enforce comprehension limits
                int maxActions = GetMaxActions(currentProfile);
                if (actions.Count > maxActions)
                {
                    Debug.Log($"[TurnManager] Truncating {actions.Count} actions to comprehension limit of {maxActions}");
                    // Keep dialogue from first action, add overflow message
                    if (actions.Count > 0 && maxActions > 0)
                    {
                        actions[maxActions - 1].dialogue += " That's too much at once!";
                    }
                    actions = actions.GetRange(0, maxActions);
                }

                Debug.Log($"[TurnManager] Parsed {actions.Count} action(s):");
                for (int i = 0; i < actions.Count; i++)
                {
                    Debug.Log($"[TurnManager]   [{i}] {actions[i].type}, target: {actions[i].targetObjectId}, dir: {actions[i].direction}");
                }
                if (actions.Count > 0)
                    Debug.Log($"[TurnManager] Dialogue: {actions[0].dialogue}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TurnManager] ResponseParser crashed: {e}\nRaw: {llmResponse}");
                OnCharacterResponse?.Invoke("I... I'm confused.", "confused");
                isProcessingTurn = false;
                OnTurnCompleted?.Invoke();
                yield break;
            }

            // Step 5b: Vocabulary enforcement — if user used real names the character
            // doesn't know, override any physical actions to None and hint at the correct name
            var vocabMatch = FindRealVocabNameInInput(playerInput, currentProfile);
            if (vocabMatch.matched)
            {
                bool anyBlocked = false;
                for (int i = 0; i < actions.Count; i++)
                {
                    var a = actions[i];
                    if (a.type != ActionType.None && a.type != ActionType.Wait &&
                        a.type != ActionType.Look && a.type != ActionType.Examine)
                    {
                        Debug.Log($"[TurnManager] Vocabulary enforcement: blocking {a.type} because user used a real name");
                        a.type = ActionType.None;
                        anyBlocked = true;
                    }
                }
                // Override dialogue with a hint about the character's vocabulary name
                if (anyBlocked && actions.Count > 0)
                {
                    string charName = currentProfile?.name ?? "I";
                    actions[0].dialogue = $"*looks confused* I don't know what a \"{vocabMatch.realWord}\" is... " +
                        $"Do you mean the {vocabMatch.characterWord}?";
                    actions[0].emotion = "confused";
                }
            }

            // Step 6-9: Process each action sequentially
            bool anyActionExecuted = false;

            for (int i = 0; i < actions.Count; i++)
            {
                CharacterAction action = actions[i];

                // Track whether the LLM intentionally chose "none" (conversation)
                bool wasIntentionalNone = action.type == ActionType.None;

                // Step 6: Apply personality post-processing (first action only)
                if (i == 0)
                {
                    try
                    {
                        action = ApplyPersonalityPostProcessing(action);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[TurnManager] Personality post-processing crashed: {e}");
                    }
                }

                // Step 7: Validate action (skip validation for intentional conversation)
                if (!wasIntentionalNone)
                {
                    try
                    {
                        action = ActionValidator.Validate(
                            action, currentCharacter, gridManager, pathfinding, objectManager, currentProfile);
                        Debug.Log($"[TurnManager] Validated action [{i}]: {action.type}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[TurnManager] ActionValidator crashed on action [{i}]: {e}");
                        action = CharacterAction.Fallback(action.dialogue);
                    }
                }

                // Step 8: Show dialogue (first action only, or if no action)
                if (i == 0 || action.type == ActionType.None)
                {
                    if (!string.IsNullOrEmpty(action.dialogue))
                        OnCharacterResponse?.Invoke(action.dialogue, action.emotion);
                }

                // Handle None actions
                if (action.type == ActionType.None)
                {
                    // Always escalate hints on None — player needs more help
                    hintEscalationLevel++;
                    // If first action is None (conversation or failure), stop the chain
                    if (i == 0) break;
                    // If a later action fails, skip it but continue
                    continue;
                }

                // Step 9: Execute action
                if (action.type != ActionType.Wait &&
                    action.type != ActionType.Look && action.type != ActionType.Examine)
                {
                    Debug.Log($"[TurnManager] Executing action [{i}]: {action.type}");

                    bool actionDone = false;
                    System.Action onDone = () => actionDone = true;
                    actionExecutor.OnActionCompleted += onDone;

                    try
                    {
                        actionExecutor.Execute(action, currentProfile);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[TurnManager] ActionExecutor crashed on action [{i}]: {e}");
                        actionDone = true;
                    }

                    // Wait for action completion
                    float timeout = 15f;
                    float elapsed = 0f;
                    while (!actionDone && elapsed < timeout)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }

                    if (!actionDone)
                    {
                        Debug.LogWarning($"[TurnManager] Action [{i}] timed out after 15s");
                    }

                    actionExecutor.OnActionCompleted -= onDone;
                    anyActionExecuted = true;

                    // Check if level completed during this action
                    if (gameManager.CurrentState != GameState.Playing)
                    {
                        Debug.Log($"[TurnManager] Game state changed to {gameManager.CurrentState} during action [{i}], stopping chain");
                        break;
                    }

                    // Small delay between chained actions for visual clarity
                    if (i < actions.Count - 1)
                    {
                        yield return new WaitForSeconds(0.3f);
                    }
                }

                // Track last action type
                lastActionType = action.type;
            }

            // Step 10: Check if out of prompts and objective not met — auto-restart
            if (gameManager.PromptsRemaining <= 0 &&
                gameManager.CurrentState == GameState.Playing)
            {
                // Give a moment for the action to trigger level complete
                yield return new WaitForSeconds(0.5f);
                if (gameManager.CurrentState == GameState.Playing)
                {
                    OnCharacterResponse?.Invoke("*Your spirit energy fades... the connection resets*", "sad");
                    yield return new WaitForSeconds(1.5f);
                    gameManager.RestartLevel();
                }
            }

            isProcessingTurn = false;
            OnTurnCompleted?.Invoke();
        }

        private CharacterAction ApplyPersonalityPostProcessing(CharacterAction action)
        {
            if (currentProfile?.personality_quirks == null) return action;

            foreach (var quirk in currentProfile.personality_quirks)
            {
                switch (quirk.type?.ToLower())
                {
                    case "stubborn":
                        action = ApplyStubbornQuirk(action, quirk);
                        break;

                    case "distrustful":
                        action = ApplyDistrustfulQuirk(action, quirk);
                        break;

                    case "impatient":
                        action = ApplyImpatientQuirk(action, quirk);
                        break;
                }
            }

            return action;
        }

        private CharacterAction ApplyStubbornQuirk(CharacterAction action, PersonalityQuirkData quirk)
        {
            if (action.type == ActionType.None || action.type == ActionType.Look) return action;

            int refusalCount = quirk.config?.refusal_count ?? 1;

            if (!stubbornAttempts.ContainsKey(action.type))
            {
                stubbornAttempts[action.type] = 0;
            }

            stubbornAttempts[action.type]++;

            if (stubbornAttempts[action.type] <= refusalCount)
            {
                // Refuse this action type
                action.dialogue = $"Hmm, I don't feel like doing that right now.";
                action.type = ActionType.None;
                action.emotion = "annoyed";
            }

            return action;
        }

        private CharacterAction ApplyDistrustfulQuirk(CharacterAction action, PersonalityQuirkData quirk)
        {
            int threshold = quirk.config?.trust_threshold ?? 2;

            if (trustCounter >= threshold) return action; // Trust earned

            trustCounter++;

            if (quirk.config?.invert_before_trust == true &&
                (action.type == ActionType.Move || action.type == ActionType.MoveTo))
            {
                // Invert the direction
                if (!string.IsNullOrEmpty(action.direction))
                {
                    action.direction = InvertDirection(action.direction);
                    action.dialogue = "I'll go... this way instead.";
                    action.emotion = "annoyed";
                }
            }

            if (trustCounter >= threshold)
            {
                string trustResponse = quirk.config?.trust_response ??
                    "Alright... I suppose I can trust you now.";
                action.dialogue += $" {trustResponse}";
            }

            return action;
        }

        private CharacterAction ApplyImpatientQuirk(CharacterAction action, PersonalityQuirkData quirk)
        {
            if (action.type == lastActionType && action.type != ActionType.None)
            {
                action.dialogue = quirk.config?.refusal_response ??
                    "Ugh, not THAT again. Tell me something different.";
                action.type = ActionType.None;
                action.emotion = "annoyed";
            }

            return action;
        }

        private int GetMaxActions(CharacterProfileData profile)
        {
            var level = profile.GetComprehensionLevel();
            return level switch
            {
                ComprehensionLevel.Simple => 1,
                ComprehensionLevel.Standard => 2,
                ComprehensionLevel.Clever => 5,
                _ => 1
            };
        }

        private string InvertDirection(string direction)
        {
            return direction?.ToLower() switch
            {
                "north" or "up" => "south",
                "south" or "down" => "north",
                "east"  => "west",
                "west"  => "east",
                "forward" => "backward",
                "backward" or "back" => "forward",
                "left" => "right",
                "right" => "left",
                _ => direction
            };
        }

        /// <summary>
        /// Checks if the player's input contains any real-world name that the character's
        /// own_vocabulary quirk maps FROM (i.e. names the character doesn't recognize).
        /// Returns (matched, realWord, characterWord) so we can build a hint.
        /// </summary>
        private (bool matched, string realWord, string characterWord) FindRealVocabNameInInput(
            string userInput, CharacterProfileData profile)
        {
            if (profile?.perception_quirks == null) return (false, null, null);

            string inputLower = userInput.ToLower();
            foreach (var quirk in profile.perception_quirks)
            {
                if (quirk.type?.ToLower() != "own_vocabulary") continue;
                if (quirk.config?.vocabulary_map?.HasEntries() != true) continue;

                foreach (var entry in quirk.config.vocabulary_map.entries)
                {
                    if (string.IsNullOrEmpty(entry.word)) continue;

                    // Check full phrase first
                    if (inputLower.Contains(entry.word.ToLower()))
                    {
                        Debug.Log($"[TurnManager] User input contains real vocab name '{entry.word}' (character calls it '{entry.replacement}')");
                        return (true, entry.word, entry.replacement);
                    }

                    // Check individual words from real name, excluding words shared with replacement
                    string[] realWords = entry.word.ToLower().Split(' ');
                    string replacementLower = entry.replacement?.ToLower() ?? "";
                    string[] replacementWords = replacementLower.Split(' ');

                    foreach (string realWord in realWords)
                    {
                        if (realWord.Length < 3) continue; // Skip tiny words like "a", "of"
                        // Skip words that also appear in the replacement name
                        bool sharedWithReplacement = false;
                        foreach (string repWord in replacementWords)
                        {
                            if (realWord == repWord) { sharedWithReplacement = true; break; }
                        }
                        if (sharedWithReplacement) continue;

                        if (inputLower.Contains(realWord))
                        {
                            Debug.Log($"[TurnManager] User input contains real vocab word '{realWord}' from '{entry.word}' (character calls it '{entry.replacement}')");
                            return (true, entry.word, entry.replacement);
                        }
                    }
                }
            }
            return (false, null, null);
        }
    }
}
