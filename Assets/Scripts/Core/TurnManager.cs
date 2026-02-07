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

            // Step 1: Input sanitization
            string characterName = currentProfile.name ?? "Explorer";
            int maxLength = gameManager.CurrentLevelData?.prompt_max_length ?? 150;

            SanitizeResult sanitizeResult = InputSanitizer.Sanitize(
                playerInput, maxLength, currentProfile, characterName);

            if (!sanitizeResult.passed)
            {
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
            string systemPrompt = PromptBuilder.Build(
                currentProfile, currentCharacter, gridManager, objectManager, hintEscalationLevel);

            // Step 4: Call LLM
            string llmResponse = null;
            string llmError = null;

            llmClient.SendMessage(systemPrompt, playerInput,
                response => llmResponse = response,
                error => llmError = error);

            // Wait for response
            while (llmResponse == null && llmError == null)
            {
                yield return null;
            }

            if (llmError != null)
            {
                Debug.LogWarning($"LLM error: {llmError}");
                // Refund the prompt on API failure
                // We can't actually refund easily, so just show error
                OnCharacterResponse?.Invoke(
                    "The spirit's voice fades... (API error, prompt not consumed)", "confused");
                isProcessingTurn = false;
                OnTurnCompleted?.Invoke();
                yield break;
            }

            // Step 5: Parse response
            CharacterAction action = ResponseParser.Parse(llmResponse);

            // Step 6: Apply personality post-processing
            action = ApplyPersonalityPostProcessing(action);

            // Step 7: Validate action
            action = ActionValidator.Validate(
                action, currentCharacter, gridManager, pathfinding, objectManager, currentProfile);

            // Step 8: Show dialogue
            OnCharacterResponse?.Invoke(action.dialogue, action.emotion);

            // Track action for hint escalation
            if (action.type == ActionType.None)
            {
                hintEscalationLevel++;
            }

            // Step 9: Execute action
            if (action.type != ActionType.None && action.type != ActionType.Wait &&
                action.type != ActionType.Look && action.type != ActionType.Examine)
            {
                bool actionDone = false;
                actionExecutor.OnActionCompleted += () => actionDone = true;

                actionExecutor.Execute(action, currentProfile);

                // Wait for action completion
                float timeout = 10f;
                float elapsed = 0f;
                while (!actionDone && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                actionExecutor.OnActionCompleted -= () => actionDone = true;
            }

            // Track last action type
            lastActionType = action.type;

            // Step 10: Check if out of prompts and objective not met
            if (gameManager.PromptsRemaining <= 0 &&
                gameManager.CurrentState == GameState.Playing)
            {
                // Give a moment for the action to trigger level complete
                yield return new WaitForSeconds(0.5f);
                if (gameManager.CurrentState == GameState.Playing)
                {
                    gameManager.FailLevel();
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
    }
}
