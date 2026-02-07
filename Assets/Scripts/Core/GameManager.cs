using System;
using UnityEngine;
using LostSouls.Grid;
using LostSouls.Character;
using LostSouls.Animation;

namespace LostSouls.Core
{
    public enum GameState
    {
        Loading,
        Playing,
        Paused,
        LevelComplete,
        LevelFailed,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private LevelLoader levelLoader;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Pathfinding pathfinding;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private ObjectManager objectManager;
        [SerializeField] private TurnManager turnManager;

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.Loading;
        [SerializeField] private int currentLevelId = 1;

        [Header("Level Progress")]
        [SerializeField] private int promptsUsed;
        [SerializeField] private int promptBudget;
        [SerializeField] private int parScore;

        // Events
        public event Action<GameState> OnGameStateChanged;
        public event Action<int> OnLevelStarted;
        public event Action<int, int> OnLevelComplete; // (promptsUsed, parScore)
        public event Action<int> OnLevelFailed; // promptsUsed
        public event Action<int> OnPromptUsed; // remaining prompts

        // Properties
        public GameState CurrentState => currentState;
        public int CurrentLevelId => currentLevelId;
        public int PromptsRemaining => promptBudget - promptsUsed;
        public int PromptsUsed => promptsUsed;
        public int PromptBudget => promptBudget;
        public int ParScore => parScore;
        public LevelData CurrentLevelData => levelLoader?.CurrentLevel;
        public ExplorerController CurrentCharacter => levelLoader?.CurrentCharacter;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeReferences();
        }

        private void Start()
        {
            // Re-check references in case Awake ordering left some null
            if (levelLoader == null || gridManager == null)
            {
                Debug.Log("GameManager: Re-checking references in Start()...");
                InitializeReferences();
            }

            Debug.Log($"GameManager.Start: levelLoader={levelLoader != null}, gridManager={gridManager != null}, pathfinding={pathfinding != null}, camera={cameraController != null}");

            // Auto-load level from Inspector field (default: 1)
            Debug.Log($"GameManager: Loading level {currentLevelId}");
            LoadLevel(currentLevelId);
        }

        private void InitializeReferences()
        {
            if (levelLoader == null)
                levelLoader = FindObjectOfType<LevelLoader>();
            if (gridManager == null)
                gridManager = FindObjectOfType<GridManager>();
            if (pathfinding == null)
                pathfinding = FindObjectOfType<Pathfinding>();
            if (cameraController == null)
                cameraController = FindObjectOfType<CameraController>();
            if (objectManager == null)
                objectManager = FindObjectOfType<ObjectManager>();
            if (turnManager == null)
                turnManager = FindObjectOfType<TurnManager>();

            // Subscribe to object manager events
            if (objectManager != null)
            {
                objectManager.OnObjectiveStateChanged += HandleObjectiveStateChanged;
            }

            // Subscribe to level loader events
            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded += HandleLevelLoaded;
                levelLoader.OnCharacterSpawned += HandleCharacterSpawned;
            }
        }

        /// <summary>
        /// Loads a level by ID
        /// </summary>
        public void LoadLevel(int levelId)
        {
            SetState(GameState.Loading);
            currentLevelId = levelId;

            // Last-resort reference check
            if (levelLoader == null)
            {
                levelLoader = FindObjectOfType<LevelLoader>();
                if (levelLoader != null)
                {
                    levelLoader.OnLevelLoaded += HandleLevelLoaded;
                    levelLoader.OnCharacterSpawned += HandleCharacterSpawned;
                    Debug.Log("GameManager: Found LevelLoader via fallback FindObjectOfType");
                }
            }

            if (levelLoader != null)
            {
                bool success = levelLoader.LoadLevel(levelId);
                if (!success)
                {
                    Debug.LogWarning($"Failed to load level {levelId} from Data/Levels/. Trying Resources root...");
                    success = levelLoader.LoadLevelFromResources($"level_{levelId:D2}");
                }
                if (!success)
                {
                    Debug.LogError($"All level loading methods failed for level {levelId}. Creating fallback test level...");
                    success = levelLoader.CreateFallbackLevel();
                }
                if (!success)
                {
                    Debug.LogError("Even fallback level creation failed. Check console for errors.");
                    SetState(GameState.LevelFailed);
                }
            }
            else
            {
                Debug.LogError("LevelLoader is not assigned and could not be found in the scene!");
                SetState(GameState.LevelFailed);
            }
        }

        private void HandleLevelLoaded(LevelData levelData)
        {
            promptsUsed = 0;
            promptBudget = levelData.prompt_budget;
            parScore = levelData.par_score;

            Debug.Log($"Level loaded: {levelData.title} | Budget: {promptBudget} | Par: {parScore}");

            // For levels gated by doors (no place_all_gems), open exit immediately
            // so it's just a gap in the border wall — the puzzle door is the only barrier.
            // For gem levels (level 5+), exit stays closed until all gems are placed.
            if (!HasGemGatingObjective(levelData))
            {
                gridManager?.OpenExitsImmediate();
            }

            SetState(GameState.Playing);
            OnLevelStarted?.Invoke(currentLevelId);
        }

        private bool HasGemGatingObjective(LevelData levelData)
        {
            if (levelData.objectives == null) return false;
            foreach (var objective in levelData.objectives)
            {
                if (objective.type == "place_all_gems")
                    return true;
            }
            return false;
        }

        private void HandleCharacterSpawned(ExplorerController character)
        {
            // Subscribe to character events
            character.OnReachedExit += HandleCharacterReachedExit;
        }

        private void HandleCharacterReachedExit(Vector2Int position)
        {
            if (CurrentLevelData?.objectives == null) return;

            bool hasReachExit = false;
            bool allObjectivesMet = true;

            foreach (var objective in CurrentLevelData.objectives)
            {
                switch (objective.type)
                {
                    case "reach_exit":
                        hasReachExit = true;
                        break;
                    case "place_all_gems":
                        if (objectManager == null || !objectManager.AreAllPedestalsActivated())
                            allObjectivesMet = false;
                        break;
                }
            }

            if (hasReachExit && allObjectivesMet)
            {
                CompleteLevel();
            }
        }

        private void HandleObjectiveStateChanged()
        {
            if (currentState != GameState.Playing) return;
            if (CurrentLevelData?.objectives == null) return;

            bool hasGemObjective = false;

            foreach (var objective in CurrentLevelData.objectives)
            {
                if (objective.type == "place_all_gems")
                {
                    hasGemObjective = true;
                    if (objectManager != null && objectManager.AreAllPedestalsActivated())
                    {
                        gridManager?.OpenExits();
                        Debug.Log("All pedestals activated! Exit opened.");
                    }
                    else
                    {
                        gridManager?.CloseExits();
                        Debug.Log("Pedestal deactivated. Exit closed.");
                    }
                }
            }

            // For levels without gem objectives, exits are already open from level load
            // (see HandleLevelLoaded). No need to toggle exits based on doors.
        }

        /// <summary>
        /// Called when a prompt is submitted
        /// </summary>
        public bool UsePrompt()
        {
            if (currentState != GameState.Playing)
            {
                Debug.Log("Cannot use prompt - not in playing state");
                return false;
            }

            if (promptsUsed >= promptBudget)
            {
                Debug.Log("No prompts remaining!");
                FailLevel();
                return false;
            }

            promptsUsed++;
            OnPromptUsed?.Invoke(PromptsRemaining);

            Debug.Log($"Prompt used. Remaining: {PromptsRemaining}");

            // Check if out of prompts
            if (PromptsRemaining <= 0)
            {
                // Don't fail immediately - let the action complete first
                // The level will fail if the objective isn't met
            }

            return true;
        }

        /// <summary>
        /// Marks the current level as complete
        /// </summary>
        public void CompleteLevel()
        {
            if (currentState != GameState.Playing) return;

            SetState(GameState.LevelComplete);

            int stars = CalculateStars();
            Debug.Log($"Level Complete! Prompts: {promptsUsed}/{promptBudget}, Par: {parScore}, Stars: {stars}");

            OnLevelComplete?.Invoke(promptsUsed, parScore);

            // TODO: Show level complete screen here
            // For now, auto-load next level after a short delay
            StartCoroutine(LoadNextLevelDelayed(1.5f));
        }

        private System.Collections.IEnumerator LoadNextLevelDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            NextLevel();
        }

        /// <summary>
        /// Marks the current level as failed
        /// </summary>
        public void FailLevel()
        {
            if (currentState != GameState.Playing) return;

            SetState(GameState.LevelFailed);

            Debug.Log($"Level Failed! Prompts used: {promptsUsed}");

            OnLevelFailed?.Invoke(promptsUsed);
        }

        /// <summary>
        /// Calculates star rating for level completion
        /// </summary>
        public int CalculateStars()
        {
            if (promptsUsed <= parScore)
                return 3; // Par or better
            else if (promptsUsed <= promptBudget * 0.7f)
                return 2; // Under 70% of budget
            else
                return 1; // Completed
        }

        /// <summary>
        /// Restarts the current level
        /// </summary>
        public void RestartLevel()
        {
            LoadLevel(currentLevelId);
        }

        /// <summary>
        /// Loads the next level
        /// </summary>
        public void NextLevel()
        {
            LoadLevel(currentLevelId + 1);
        }

        /// <summary>
        /// Pauses the game
        /// </summary>
        public void Pause()
        {
            if (currentState == GameState.Playing)
            {
                SetState(GameState.Paused);
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// Resumes the game
        /// </summary>
        public void Resume()
        {
            if (currentState == GameState.Paused)
            {
                SetState(GameState.Playing);
                Time.timeScale = 1f;
            }
        }

        private void SetState(GameState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                OnGameStateChanged?.Invoke(newState);
            }
        }

        /// <summary>
        /// Debug method to test movement
        /// </summary>
        [ContextMenu("Test Move To Exit")]
        public void TestMoveToExit()
        {
            if (CurrentCharacter == null)
            {
                Debug.LogError("No character to move");
                return;
            }

            Tile exitTile = gridManager?.GetExitTile();
            if (exitTile == null)
            {
                Debug.LogError("No exit tile found");
                return;
            }

            Debug.Log($"Testing movement to exit at {exitTile.GridPosition}");
            CurrentCharacter.MoveTo(exitTile.GridPosition);
        }

        /// <summary>
        /// Debug method to test directional movement
        /// </summary>
        [ContextMenu("Test Move North")]
        public void TestMoveNorth()
        {
            CurrentCharacter?.MoveInDirection(Direction.North, 1);
        }

        [ContextMenu("Test Move East")]
        public void TestMoveEast()
        {
            CurrentCharacter?.MoveInDirection(Direction.East, 1);
        }

        [ContextMenu("Test Move South")]
        public void TestMoveSouth()
        {
            CurrentCharacter?.MoveInDirection(Direction.South, 1);
        }

        [ContextMenu("Test Move West")]
        public void TestMoveWest()
        {
            CurrentCharacter?.MoveInDirection(Direction.West, 1);
        }

        private void OnDestroy()
        {
            if (levelLoader != null)
            {
                levelLoader.OnLevelLoaded -= HandleLevelLoaded;
                levelLoader.OnCharacterSpawned -= HandleCharacterSpawned;
            }

            if (objectManager != null)
            {
                objectManager.OnObjectiveStateChanged -= HandleObjectiveStateChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
