using System;
using System.Collections.Generic;
using UnityEngine;
using LostSouls.Grid;
using LostSouls.Character;
using LostSouls.Animation;
using LostSouls.Objects;

namespace LostSouls.Core
{
    public class LevelLoader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Pathfinding pathfinding;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private ObjectManager objectManager;

        [Header("Prefabs")]
        [SerializeField] private GameObject characterPrefab;
        [SerializeField] private GameObject[] keyPrefabs;
        [SerializeField] private GameObject[] gemPrefabs;
        [SerializeField] private GameObject pedestalPrefab;

        [Header("Object Settings")]
        [SerializeField] private float keyYOffset = 0.5f;
        [SerializeField] private float gemYOffset = 0.6f;
        [SerializeField] private float keyScale = 2f;
        [SerializeField] private float gemScale = 3f;
        [SerializeField] private float pedestalScale = 2f;

        [Header("Current Level")]
        [SerializeField] private LevelData currentLevelData;
        [SerializeField] private ExplorerController currentCharacter;

        // Track spawned objects for cleanup
        private List<GameObject> spawnedObjects = new List<GameObject>();

        // Auto-cycle counters for variant assignment
        private int gemSpawnCount;
        private int keySpawnCount;

        // Events
        public event Action<LevelData> OnLevelLoaded;
        public event Action<ExplorerController> OnCharacterSpawned;

        public LevelData CurrentLevel => currentLevelData;
        public ExplorerController CurrentCharacter => currentCharacter;

        private void Awake()
        {
            if (gridManager == null)
                gridManager = FindObjectOfType<GridManager>();
            if (pathfinding == null)
                pathfinding = FindObjectOfType<Pathfinding>();
            if (cameraController == null)
                cameraController = FindObjectOfType<CameraController>();
            if (objectManager == null)
                objectManager = FindObjectOfType<ObjectManager>();
        }

        /// <summary>
        /// Loads a level from a JSON file in Resources/Data/Levels/
        /// </summary>
        public bool LoadLevel(int levelId)
        {
            string path = $"Data/Levels/level_{levelId:D2}";
            return LoadLevelFromResources(path);
        }

        /// <summary>
        /// Loads a level from a TextAsset
        /// </summary>
        public bool LoadLevel(TextAsset levelAsset)
        {
            if (levelAsset == null)
            {
                Debug.LogError("LevelLoader: Level asset is null");
                return false;
            }

            return LoadLevelFromJson(levelAsset.text);
        }

        /// <summary>
        /// Loads a level from a Resources path
        /// </summary>
        public bool LoadLevelFromResources(string resourcePath)
        {
            Debug.Log($"LevelLoader: Attempting to load from Resources/{resourcePath}");
            TextAsset levelAsset = Resources.Load<TextAsset>(resourcePath);

            if (levelAsset == null)
            {
                Debug.LogError($"LevelLoader: Could not load level from Resources/{resourcePath} (file not found or not a TextAsset)");
                return false;
            }

            Debug.Log($"LevelLoader: Loaded TextAsset, length={levelAsset.text.Length}");
            return LoadLevelFromJson(levelAsset.text);
        }

        /// <summary>
        /// Loads a level from a JSON string
        /// </summary>
        public bool LoadLevelFromJson(string json)
        {
            try
            {
                currentLevelData = LevelData.FromJson(json);

                if (currentLevelData == null)
                {
                    Debug.LogError("LevelLoader: Failed to parse level JSON");
                    return false;
                }

                BuildLevel();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"LevelLoader: Error loading level: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a hardcoded fallback level when JSON loading fails
        /// </summary>
        public bool CreateFallbackLevel()
        {
            Debug.Log("LevelLoader: Creating fallback test level...");

            string fallbackJson = @"{
                ""level_id"": 1,
                ""title"": ""Fallback Level"",
                ""description"": ""Auto-generated test level"",
                ""grid"": {
                    ""width"": 4,
                    ""height"": 4,
                    ""tiles"": [
                        { ""x"": 0, ""y"": 0, ""type"": ""floor"" },
                        { ""x"": 1, ""y"": 0, ""type"": ""floor"" },
                        { ""x"": 2, ""y"": 0, ""type"": ""floor"" },
                        { ""x"": 3, ""y"": 0, ""type"": ""wall"" },
                        { ""x"": 0, ""y"": 1, ""type"": ""floor"" },
                        { ""x"": 1, ""y"": 1, ""type"": ""floor"" },
                        { ""x"": 2, ""y"": 1, ""type"": ""floor"" },
                        { ""x"": 3, ""y"": 1, ""type"": ""door"" },
                        { ""x"": 0, ""y"": 2, ""type"": ""floor"" },
                        { ""x"": 1, ""y"": 2, ""type"": ""floor"" },
                        { ""x"": 2, ""y"": 2, ""type"": ""floor"" },
                        { ""x"": 3, ""y"": 2, ""type"": ""wall"" },
                        { ""x"": 0, ""y"": 3, ""type"": ""wall"" },
                        { ""x"": 1, ""y"": 3, ""type"": ""wall"" },
                        { ""x"": 2, ""y"": 3, ""type"": ""wall"" },
                        { ""x"": 3, ""y"": 3, ""type"": ""wall"" }
                    ]
                },
                ""objects"": [
                    {
                        ""id"": ""door_1"",
                        ""type"": ""door"",
                        ""display_name"": ""Door"",
                        ""position"": { ""x"": 3, ""y"": 1 },
                        ""properties"": { ""state"": ""closed"" }
                    }
                ],
                ""characters"": [
                    {
                        ""id"": ""explorer_1"",
                        ""name"": ""Sage"",
                        ""position"": { ""x"": 0, ""y"": 0 },
                        ""profile_id"": ""sage_profile""
                    }
                ],
                ""objectives"": [
                    { ""type"": ""reach_exit"", ""target_character"": ""explorer_1"" }
                ],
                ""prompt_budget"": 5,
                ""prompt_max_length"": 150,
                ""par_score"": 1,
                ""hints"": { ""bio_visible"": true, ""observation_cost"": 1 }
            }";

            return LoadLevelFromJson(fallbackJson);
        }

        /// <summary>
        /// Builds the level from the loaded data
        /// </summary>
        private void BuildLevel()
        {
            Debug.Log($"Building level: {currentLevelData.title}");

            // Re-check references in case they weren't set during Awake
            if (gridManager == null)
            {
                gridManager = FindObjectOfType<GridManager>();
                Debug.Log($"LevelLoader.BuildLevel: Re-found GridManager: {gridManager != null}");
            }
            if (pathfinding == null)
            {
                pathfinding = FindObjectOfType<Pathfinding>();
            }
            if (cameraController == null)
            {
                cameraController = FindObjectOfType<CameraController>();
            }

            // Save level data before clearing (ClearLevel nulls it)
            LevelData levelData = currentLevelData;

            // Clear any existing level
            ClearLevel();

            // Restore level data
            currentLevelData = levelData;

            // Validate exit placement (must be on north or east edge)
            ValidateExitPlacement(currentLevelData.grid);

            // Build the grid
            if (gridManager != null && currentLevelData.grid != null)
            {
                Debug.Log($"Building grid: {currentLevelData.grid.width}x{currentLevelData.grid.height} with {currentLevelData.grid.tiles?.Count ?? 0} tiles");
                gridManager.BuildGrid(currentLevelData.grid);
            }
            else
            {
                Debug.LogError($"LevelLoader: GridManager={gridManager != null}, grid data={currentLevelData.grid != null}");
                return;
            }

            // Initialize pathfinding
            if (pathfinding != null)
            {
                pathfinding.Initialize(gridManager);
            }

            // Spawn characters
            SpawnCharacters();

            // Spawn objects (keys, gems, boxes)
            SpawnObjects();

            // Setup camera
            SetupCamera();

            // Notify listeners
            OnLevelLoaded?.Invoke(currentLevelData);

            Debug.Log($"Level '{currentLevelData.title}' loaded successfully");
        }

        /// <summary>
        /// Spawns all characters defined in the level
        /// </summary>
        private void SpawnCharacters()
        {
            if (currentLevelData.characters == null || currentLevelData.characters.Count == 0)
            {
                Debug.LogWarning("LevelLoader: No characters defined in level");
                return;
            }

            foreach (CharacterData charData in currentLevelData.characters)
            {
                SpawnCharacter(charData);
            }
        }

        /// <summary>
        /// Spawns a single character
        /// </summary>
        private ExplorerController SpawnCharacter(CharacterData charData)
        {
            Vector2Int gridPos = charData.position.ToVector2Int();
            Vector3 worldPos = gridManager.GridToWorldPosition(gridPos) + Vector3.up * 0.5f;

            // Instantiate character
            GameObject charObj;
            if (characterPrefab != null)
            {
                charObj = Instantiate(characterPrefab, worldPos, Quaternion.identity);
            }
            else
            {
                // Create placeholder character (capsule)
                charObj = CreatePlaceholderCharacter();
                charObj.transform.position = worldPos;
            }

            charObj.name = $"Character_{charData.name}";

            // Setup ExplorerController
            ExplorerController explorer = charObj.GetComponent<ExplorerController>();
            if (explorer == null)
            {
                explorer = charObj.AddComponent<ExplorerController>();
            }

            explorer.Initialize(charData.id, charData.name, gridPos, gridManager, pathfinding);

            // Store reference to first character as main character
            if (currentCharacter == null)
            {
                currentCharacter = explorer;
            }

            OnCharacterSpawned?.Invoke(explorer);

            Debug.Log($"Spawned character: {charData.name} at {gridPos}");
            return explorer;
        }

        /// <summary>
        /// Creates a placeholder character using primitive shapes
        /// </summary>
        private GameObject CreatePlaceholderCharacter()
        {
            GameObject character = new GameObject("PlaceholderCharacter");

            // Body (capsule)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(character.transform);
            body.transform.localPosition = Vector3.up * 0.5f;
            body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            // Set color
            MeshRenderer renderer = body.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                }
                mat.color = new Color(0.2f, 0.6f, 0.9f); // Blue character
                renderer.material = mat;
            }

            // Face indicator (small sphere to show forward direction)
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            face.transform.SetParent(character.transform);
            face.transform.localPosition = new Vector3(0, 0.8f, 0.25f);
            face.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            MeshRenderer faceRenderer = face.GetComponent<MeshRenderer>();
            if (faceRenderer != null)
            {
                Material faceMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (faceMat.shader == null)
                {
                    faceMat = new Material(Shader.Find("Standard"));
                }
                faceMat.color = Color.white;
                faceRenderer.material = faceMat;
            }

            return character;
        }

        /// <summary>
        /// Spawns all objects defined in the level
        /// </summary>
        private void SpawnObjects()
        {
            if (currentLevelData.objects == null || currentLevelData.objects.Count == 0)
                return;

            if (objectManager == null)
                objectManager = FindObjectOfType<ObjectManager>();

            gemSpawnCount = 0;
            keySpawnCount = 0;

            foreach (ObjectData objData in currentLevelData.objects)
            {
                SpawnObject(objData);
            }
        }

        /// <summary>
        /// Spawns a single object with proper GridObject component
        /// </summary>
        private void SpawnObject(ObjectData objData)
        {
            Vector2Int gridPos = objData.position.ToVector2Int();
            Grid.Tile tile = gridManager.GetTile(gridPos);

            if (tile == null)
            {
                Debug.LogWarning($"Cannot spawn object '{objData.display_name}' at {gridPos} - no tile");
                return;
            }

            ObjectType type = objData.GetObjectType();

            switch (type)
            {
                case ObjectType.Key:
                case ObjectType.Gem:
                case ObjectType.Box:
                case ObjectType.Pedestal:
                    SpawnFreestandingObject(objData, tile, gridPos);
                    break;

                case ObjectType.Door:
                case ObjectType.PressurePlate:
                    SpawnTileMountedObject(objData, tile, gridPos, type);
                    break;
            }
        }

        /// <summary>
        /// Spawns a freestanding object (key, gem, box, pedestal) on a tile
        /// </summary>
        private void SpawnFreestandingObject(ObjectData objData, Grid.Tile tile, Vector2Int gridPos)
        {
            int variant = objData.properties?.variant ?? 0;
            GameObject prefab = GetObjectPrefab(objData.GetObjectType(), variant);
            GameObject obj = prefab != null ? Instantiate(prefab) : CreatePlaceholderObject(objData);
            obj.name = $"Object_{objData.id}_{objData.type}";

            // Scale models by type
            ObjectType objType = objData.GetObjectType();
            float scale = objType switch
            {
                ObjectType.Key => keyScale,
                ObjectType.Gem => gemScale,
                ObjectType.Pedestal => pedestalScale,
                _ => 1f
            };
            obj.transform.localScale *= scale;

            // Add the correct GridObject component
            GridObject gridObj = null;
            switch (objData.GetObjectType())
            {
                case ObjectType.Key:
                    gridObj = obj.AddComponent<KeyObject>();
                    break;
                case ObjectType.Gem:
                    gridObj = obj.AddComponent<GemObject>();
                    break;
                case ObjectType.Box:
                    gridObj = obj.AddComponent<BoxObject>();
                    break;
                case ObjectType.Pedestal:
                    gridObj = obj.AddComponent<PedestalObject>();
                    break;
            }

            if (gridObj != null)
            {
                gridObj.Initialize(objData, gridManager, objectManager);

                // Per-type Y offset
                float yOffset = objType switch
                {
                    ObjectType.Key => keyYOffset,
                    ObjectType.Gem => gemYOffset,
                    _ => 0.3f
                };

                // Boxes go to tile center; other items go to sub-tile 3
                bool isBox = objType == ObjectType.Box;
                if (isBox)
                {
                    tile.SetItem(obj, preferredSubTile: 3);
                    Vector3 worldPos = gridManager.GridToWorldPosition(gridPos);
                    obj.transform.position = worldPos + Vector3.up * yOffset;
                }
                else
                {
                    int subTile = tile.SetItem(obj, preferredSubTile: 3);
                    Vector3 worldPos = gridManager.GridToWorldPosition(gridPos) + tile.GetSubTileOffset(subTile);
                    obj.transform.position = worldPos + Vector3.up * yOffset;
                }

                // Add spinning to keys and gems
                if (objType == ObjectType.Key || objType == ObjectType.Gem)
                {
                    obj.AddComponent<SpinObject>();
                }

                objectManager?.RegisterObject(gridObj);
                spawnedObjects.Add(obj);
            }

            Debug.Log($"Spawned {objData.type}: '{objData.display_name}' at {gridPos}");
        }

        /// <summary>
        /// Spawns a tile-mounted object (door, pressure plate) on an existing tile
        /// </summary>
        private void SpawnTileMountedObject(ObjectData objData, Grid.Tile tile, Vector2Int gridPos, ObjectType type)
        {
            // Upgrade tile type if needed
            TileType requiredType = type switch
            {
                ObjectType.Door => TileType.Door,
                ObjectType.PressurePlate => TileType.PressurePlate,
                _ => TileType.Floor
            };

            if (tile.TileType != requiredType)
            {
                tile.ChangeTileType(requiredType);
            }

            // Add GridObject component to the tile's GameObject
            GridObject gridObj = null;
            switch (type)
            {
                case ObjectType.Door:
                    gridObj = tile.gameObject.AddComponent<DoorObject>();
                    break;
                case ObjectType.PressurePlate:
                    gridObj = tile.gameObject.AddComponent<PressurePlateObject>();
                    break;
            }

            if (gridObj != null)
            {
                gridObj.Initialize(objData, gridManager, objectManager);
                objectManager?.RegisterObject(gridObj);
            }

            Debug.Log($"Spawned tile-mounted {objData.type}: '{objData.display_name}' at {gridPos}");
        }

        private GameObject GetObjectPrefab(ObjectType type, int variant)
        {
            switch (type)
            {
                case ObjectType.Key:
                    if (keyPrefabs == null || keyPrefabs.Length == 0) return null;
                    int keyIndex = variant > 0 ? (variant - 1) : keySpawnCount;
                    keySpawnCount++;
                    return keyPrefabs[keyIndex % keyPrefabs.Length];

                case ObjectType.Gem:
                    if (gemPrefabs == null || gemPrefabs.Length == 0) return null;
                    int gemIndex = variant > 0 ? (variant - 1) : gemSpawnCount;
                    gemSpawnCount++;
                    return gemPrefabs[gemIndex % gemPrefabs.Length];

                case ObjectType.Pedestal:
                    return pedestalPrefab;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Creates a placeholder visual for a freestanding object (fallback when no prefab)
        /// </summary>
        private GameObject CreatePlaceholderObject(ObjectData objData)
        {
            ObjectType type = objData.GetObjectType();
            GameObject obj;

            switch (type)
            {
                case ObjectType.Key:
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    obj.transform.localScale = new Vector3(0.2f, 0.3f, 0.2f);
                    SetObjectColor(obj, new Color(1f, 0.85f, 0.1f)); // Yellow
                    break;

                case ObjectType.Gem:
                    obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    obj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                    Color gemColor = objData.properties?.color?.ToLower() switch
                    {
                        "red" => Color.red,
                        "blue" => Color.blue,
                        "green" => Color.green,
                        "purple" => new Color(0.6f, 0.2f, 0.8f),
                        _ => Color.cyan
                    };
                    SetObjectColor(obj, gemColor);
                    break;

                case ObjectType.Box:
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    SetObjectColor(obj, new Color(0.6f, 0.4f, 0.2f)); // Brown
                    break;

                case ObjectType.Pedestal:
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);
                    SetObjectColor(obj, new Color(0.45f, 0.45f, 0.75f)); // Blue-purple
                    // Add a small top platform indicator
                    GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    top.name = "PedestalTop";
                    top.transform.SetParent(obj.transform);
                    top.transform.localPosition = Vector3.up * 0.6f;
                    top.transform.localScale = new Vector3(0.8f, 0.1f, 0.8f);
                    SetObjectColor(top, new Color(0.55f, 0.55f, 0.85f));
                    break;

                default:
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                    SetObjectColor(obj, Color.white);
                    break;
            }

            return obj;
        }

        private void SetObjectColor(GameObject obj, Color color)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                }
                mat.color = color;
                renderer.material = mat;
            }
        }

        /// <summary>
        /// Sets up the camera for the loaded level
        /// </summary>
        private void SetupCamera()
        {
            if (cameraController == null) return;

            // Set camera to follow the main character
            if (currentCharacter != null)
            {
                cameraController.SetTarget(currentCharacter);
            }

            // Fit camera to grid size
            cameraController.FitToGrid(gridManager, padding: 2f);
            cameraController.SnapToTarget();
        }

        /// <summary>
        /// Validates level data before building
        /// </summary>
        private void ValidateExitPlacement(GridData gridData)
        {
            // No validation needed — all walls and gates are explicit in the JSON
        }

        /// <summary>
        /// Clears the current level
        /// </summary>
        public void ClearLevel()
        {
            // Clear object manager
            if (objectManager == null)
                objectManager = FindObjectOfType<ObjectManager>();
            if (objectManager != null)
            {
                objectManager.ClearAll();
            }

            // Destroy spawned freestanding objects
            foreach (var obj in spawnedObjects)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedObjects.Clear();

            // Clear grid (also destroys tile-mounted object components)
            if (gridManager != null)
            {
                gridManager.ClearGrid();
            }

            // Destroy current character
            if (currentCharacter != null)
            {
                Destroy(currentCharacter.gameObject);
                currentCharacter = null;
            }

            // Find and destroy any other characters
            ExplorerController[] explorers = FindObjectsOfType<ExplorerController>();
            foreach (var explorer in explorers)
            {
                Destroy(explorer.gameObject);
            }

            currentLevelData = null;
        }

        /// <summary>
        /// Reloads the current level
        /// </summary>
        public void ReloadLevel()
        {
            if (currentLevelData != null)
            {
                int levelId = currentLevelData.level_id;
                LoadLevel(levelId);
            }
        }
    }
}
