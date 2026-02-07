#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;
using TMPro;

namespace LostSouls.Editor
{
    public class PrefabGenerator : EditorWindow
    {
        // Model paths in Assets/Models/
        private const string ModelsRoot = "Assets/Models";
        private const string FloorModelPath = ModelsRoot + "/floor tile/floor_tile.prefab";
        private const string WallModelPath = ModelsRoot + "/wall block/wall block.fbx";
        private const string DoorModelPath = ModelsRoot + "/door/door.prefab";
        private const string KeyModelPath = ModelsRoot + "/key/key.fbx";
        private const string GemModelPath = ModelsRoot + "/gems/gems.fbx";
        private const string PedestalModelPath = ModelsRoot + "/pedestal/pedestal.fbx";
        private const string PressurePlateModelPath = ModelsRoot + "/pressure plate/pressure plate.fbx";

        [MenuItem("Lost Souls/Generate Prefabs from Models")]
        public static void GeneratePrefabs()
        {
            EnsureDirectoryExists("Assets/Prefabs/Tiles");
            EnsureDirectoryExists("Assets/Prefabs/Characters");
            EnsureDirectoryExists("Assets/Prefabs/Objects");

            // Tile prefabs (floor, wall, door scaled 1.5x)
            CreateTilePrefab(FloorModelPath, "FloorTile", "Assets/Prefabs/Tiles/FloorTile.prefab", 2f);
            CreateTilePrefab(WallModelPath, "WallTile", "Assets/Prefabs/Tiles/WallTile.prefab", 2f);
            CreateTilePrefab(DoorModelPath, "DoorTile", "Assets/Prefabs/Tiles/DoorTile.prefab", 2f);
            CreatePressurePlatePrefab();

            // Object prefabs (keys and gems use variant prefabs created manually in Models folder)
            CreateObjectPrefab(PedestalModelPath, "Pedestal", "Assets/Prefabs/Objects/Pedestal.prefab");

            // Character prefab (keep placeholder until character model is available)
            CreateCharacterPrefab();

            AssetDatabase.Refresh();
            Debug.Log("All prefabs generated from models!");

            // Auto-wire to existing GridManager in scene
            WirePrefabReferences();
        }

        [MenuItem("Lost Souls/Wire Prefab References")]
        public static void WirePrefabReferences()
        {
            var gridManager = Object.FindObjectOfType<Grid.GridManager>();
            if (gridManager == null)
            {
                Debug.LogWarning("No GridManager found in scene — run 'Setup Game Scene' first.");
                return;
            }

            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tiles/FloorTile.prefab");
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tiles/WallTile.prefab");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tiles/DoorTile.prefab");
            var pressurePlatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tiles/PressurePlateTile.prefab");

            SerializedObject gridSO = new SerializedObject(gridManager);
            if (floorPrefab != null) gridSO.FindProperty("floorPrefab").objectReferenceValue = floorPrefab;
            if (wallPrefab != null) gridSO.FindProperty("wallPrefab").objectReferenceValue = wallPrefab;
            if (doorPrefab != null) gridSO.FindProperty("exitPrefab").objectReferenceValue = doorPrefab;
            if (pressurePlatePrefab != null) gridSO.FindProperty("pressurePlatePrefab").objectReferenceValue = pressurePlatePrefab;
            gridSO.ApplyModifiedProperties();

            Debug.Log($"Wired prefab references to GridManager: floor={floorPrefab != null}, wall={wallPrefab != null}, door={doorPrefab != null}, plate={pressurePlatePrefab != null}");

            // Mark scene dirty so it saves
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// Tries to load a model from the given FBX path. If missing, searches for
        /// an extracted .prefab in the same folder as a fallback.
        /// </summary>
        private static GameObject LoadModelWithFallback(string fbxPath)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model != null) return model;

            // FBX missing — look for an extracted prefab in the same folder
            string folder = Path.GetDirectoryName(fbxPath);
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    Debug.Log($"FBX not found at {fbxPath}, using extracted prefab: {path}");
                    return prefab;
                }
            }

            // Also try loading a mesh and see if there's any GameObject in the folder
            string[] allGuids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
            foreach (string guid in allGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    Debug.Log($"FBX not found at {fbxPath}, using asset: {path}");
                    return go;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a tile prefab from an FBX model.
        /// Structure: Parent (with Tile + BoxCollider) -> Model child (with MeshRenderer)
        /// </summary>
        private static void CreateTilePrefab(string modelPath, string prefabName, string savePath, float modelScale = 1f)
        {
            GameObject model = LoadModelWithFallback(modelPath);
            if (model == null)
            {
                Debug.LogWarning($"Model not found at {modelPath} — skipping {prefabName}. Primitive fallback will be used at runtime.");
                return;
            }

            // Create parent with Tile component
            GameObject parent = new GameObject(prefabName);
            parent.AddComponent<Grid.Tile>();

            // Add a box collider for raycasting
            BoxCollider collider = parent.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 2f);
            collider.center = Vector3.zero;

            // Instantiate model as child (nested prefab preserves material/texture refs)
            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            modelInstance.name = "Model";
            modelInstance.transform.SetParent(parent.transform);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one * modelScale;

            SavePrefab(parent, savePath);
            DestroyImmediate(parent);
        }

        /// <summary>
        /// Creates an object prefab from an FBX model.
        /// Just the model — GridObject components are added at runtime by LevelLoader.
        /// </summary>
        private static void CreateObjectPrefab(string modelPath, string prefabName, string savePath)
        {
            GameObject model = LoadModelWithFallback(modelPath);
            if (model == null)
            {
                Debug.LogWarning($"Model not found at {modelPath} — skipping {prefabName}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = prefabName;

            SavePrefab(instance, savePath);
            DestroyImmediate(instance);
        }

        /// <summary>
        /// Creates a pressure plate prefab: floor tile base + pressure plate model on top.
        /// </summary>
        private static void CreatePressurePlatePrefab()
        {
            GameObject floorModel = LoadModelWithFallback(FloorModelPath);
            GameObject plateModel = LoadModelWithFallback(PressurePlateModelPath);

            if (plateModel == null)
            {
                Debug.LogWarning("Pressure plate model not found — skipping PressurePlateTile");
                return;
            }

            // Create parent with Tile component
            GameObject parent = new GameObject("PressurePlateTile");
            parent.AddComponent<Grid.Tile>();

            BoxCollider collider = parent.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 2f);
            collider.center = Vector3.zero;

            // Floor base (same scale as normal floor tiles)
            if (floorModel != null)
            {
                GameObject floorInstance = (GameObject)PrefabUtility.InstantiatePrefab(floorModel);
                floorInstance.name = "FloorBase";
                floorInstance.transform.SetParent(parent.transform);
                floorInstance.transform.localPosition = Vector3.zero;
                floorInstance.transform.localRotation = Quaternion.identity;
                floorInstance.transform.localScale = Vector3.one * 2f;
            }

            // Pressure plate on top, slightly raised
            GameObject plateInstance = (GameObject)PrefabUtility.InstantiatePrefab(plateModel);
            plateInstance.name = "PressurePlate";
            plateInstance.transform.SetParent(parent.transform);
            plateInstance.transform.localPosition = Vector3.up * 0.15f;
            plateInstance.transform.localRotation = Quaternion.identity;
            plateInstance.transform.localScale = Vector3.one;

            SavePrefab(parent, "Assets/Prefabs/Tiles/PressurePlateTile.prefab");
            DestroyImmediate(parent);
        }

        private static void CreateCharacterPrefab()
        {
            GameObject character = new GameObject("Explorer");

            // Body (capsule)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(character.transform);
            body.transform.localPosition = Vector3.up * 0.5f;
            body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            SetMaterialColor(body, new Color(0.2f, 0.6f, 0.9f));

            // Face (sphere to indicate forward direction)
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            face.name = "Face";
            face.transform.SetParent(character.transform);
            face.transform.localPosition = new Vector3(0, 0.8f, 0.25f);
            face.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            SetMaterialColor(face, Color.white);

            // Add ExplorerController
            character.AddComponent<Character.ExplorerController>();

            SavePrefab(character, "Assets/Prefabs/Characters/Explorer.prefab");
            DestroyImmediate(character);
        }

        // ========== Setup Game Scene ==========

        [MenuItem("Lost Souls/Setup Game Scene")]
        public static void SetupGameScene()
        {
            // Create GameManager
            GameObject gameManagerObj = new GameObject("GameManager");
            gameManagerObj.AddComponent<Core.GameManager>();

            // Create GridManager
            GameObject gridManagerObj = new GameObject("GridManager");
            gridManagerObj.AddComponent<Grid.GridManager>();
            gridManagerObj.AddComponent<Grid.Pathfinding>();

            // Create LevelLoader
            GameObject levelLoaderObj = new GameObject("LevelLoader");
            levelLoaderObj.AddComponent<Core.LevelLoader>();

            // Create ObjectManager
            GameObject objectManagerObj = new GameObject("ObjectManager");
            objectManagerObj.AddComponent<Core.ObjectManager>();

            // Create DebugInput
            GameObject debugInputObj = new GameObject("DebugInput");
            debugInputObj.AddComponent<Core.DebugInput>();

            // Create TurnManager
            GameObject turnManagerObj = new GameObject("TurnManager");
            turnManagerObj.AddComponent<Core.TurnManager>();

            // Setup Camera
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObj = new GameObject("Main Camera");
                mainCamera = cameraObj.AddComponent<Camera>();
                cameraObj.tag = "MainCamera";
            }

            if (mainCamera.GetComponent<Animation.CameraController>() == null)
            {
                mainCamera.gameObject.AddComponent<Animation.CameraController>();
            }

            // Position camera for isometric view
            mainCamera.transform.position = new Vector3(5, 10, -5);
            mainCamera.transform.rotation = Quaternion.Euler(45, 45, 0);
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5;

            // Add directional light if needed
            if (FindObjectOfType<Light>() == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // Create UI
            SetupUI();

            // Wire all references
            WireReferences(gameManagerObj, gridManagerObj, levelLoaderObj, mainCamera, objectManagerObj);

            Debug.Log("Game scene setup complete! Press Play to test.");
        }

        [MenuItem("Lost Souls/Setup UI Only")]
        public static void SetupUI()
        {
            // Skip if a PromptInputUI already exists in the scene
            if (FindObjectOfType<UI.PromptInputUI>() != null)
            {
                Debug.Log("UI already exists in scene. Skipping UI setup.");
                return;
            }

            // --- Main Screen-Space Canvas ---
            GameObject canvasObj = new GameObject("GameUI Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // --- Prompt Input Panel (bottom of screen) ---
            GameObject promptPanel = CreateUIPanel(canvasObj.transform, "PromptPanel",
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0.5f, 0), new Vector2(0, 60),
                new Color(0.1f, 0.1f, 0.15f, 0.9f));

            // Input Field
            GameObject inputFieldObj = new GameObject("InputField");
            inputFieldObj.transform.SetParent(promptPanel.transform, false);
            RectTransform inputRT = inputFieldObj.AddComponent<RectTransform>();
            inputRT.anchorMin = new Vector2(0, 0);
            inputRT.anchorMax = new Vector2(0.82f, 1);
            inputRT.offsetMin = new Vector2(10, 5);
            inputRT.offsetMax = new Vector2(-5, -5);

            Image inputBg = inputFieldObj.AddComponent<Image>();
            inputBg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            TMP_InputField inputField = inputFieldObj.AddComponent<TMP_InputField>();

            // Input text area
            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputFieldObj.transform, false);
            RectTransform textAreaRT = textArea.AddComponent<RectTransform>();
            textAreaRT.anchorMin = Vector2.zero;
            textAreaRT.anchorMax = Vector2.one;
            textAreaRT.offsetMin = new Vector2(10, 0);
            textAreaRT.offsetMax = new Vector2(-10, 0);
            textArea.AddComponent<RectMask2D>();

            // Placeholder
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(textArea.transform, false);
            RectTransform phRT = placeholderObj.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = Vector2.zero;
            phRT.offsetMax = Vector2.zero;
            TextMeshProUGUI placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholder.text = "Type your command here...";
            placeholder.fontSize = 18;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            // Input text
            GameObject inputTextObj = new GameObject("Text");
            inputTextObj.transform.SetParent(textArea.transform, false);
            RectTransform itRT = inputTextObj.AddComponent<RectTransform>();
            itRT.anchorMin = Vector2.zero;
            itRT.anchorMax = Vector2.one;
            itRT.offsetMin = Vector2.zero;
            itRT.offsetMax = Vector2.zero;
            TextMeshProUGUI inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 18;
            inputText.color = Color.white;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;

            inputField.textViewport = textAreaRT;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.characterLimit = 150;

            // Send Button
            GameObject sendBtnObj = new GameObject("SendButton");
            sendBtnObj.transform.SetParent(promptPanel.transform, false);
            RectTransform sendRT = sendBtnObj.AddComponent<RectTransform>();
            sendRT.anchorMin = new Vector2(0.83f, 0);
            sendRT.anchorMax = new Vector2(0.93f, 1);
            sendRT.offsetMin = new Vector2(0, 5);
            sendRT.offsetMax = new Vector2(0, -5);

            Image sendBg = sendBtnObj.AddComponent<Image>();
            sendBg.color = new Color(0.2f, 0.5f, 0.8f, 1f);
            Button sendButton = sendBtnObj.AddComponent<Button>();
            sendButton.targetGraphic = sendBg;

            GameObject sendLabel = new GameObject("Label");
            sendLabel.transform.SetParent(sendBtnObj.transform, false);
            RectTransform slRT = sendLabel.AddComponent<RectTransform>();
            slRT.anchorMin = Vector2.zero;
            slRT.anchorMax = Vector2.one;
            slRT.offsetMin = Vector2.zero;
            slRT.offsetMax = Vector2.zero;
            TextMeshProUGUI sendText = sendLabel.AddComponent<TextMeshProUGUI>();
            sendText.text = "Send";
            sendText.fontSize = 18;
            sendText.color = Color.white;
            sendText.alignment = TextAlignmentOptions.Center;

            // Char counter
            GameObject charCountObj = new GameObject("CharCounter");
            charCountObj.transform.SetParent(promptPanel.transform, false);
            RectTransform ccRT = charCountObj.AddComponent<RectTransform>();
            ccRT.anchorMin = new Vector2(0.94f, 0);
            ccRT.anchorMax = new Vector2(1, 1);
            ccRT.offsetMin = new Vector2(0, 5);
            ccRT.offsetMax = new Vector2(-5, -5);
            TextMeshProUGUI charCountText = charCountObj.AddComponent<TextMeshProUGUI>();
            charCountText.text = "150";
            charCountText.fontSize = 16;
            charCountText.color = Color.white;
            charCountText.alignment = TextAlignmentOptions.Center;

            // Wire PromptInputUI
            UI.PromptInputUI promptUI = promptPanel.AddComponent<UI.PromptInputUI>();
            SerializedObject puiSO = new SerializedObject(promptUI);
            puiSO.FindProperty("inputField").objectReferenceValue = inputField;
            puiSO.FindProperty("sendButton").objectReferenceValue = sendButton;
            puiSO.FindProperty("charCountText").objectReferenceValue = charCountText;
            puiSO.ApplyModifiedProperties();

            // --- Energy Bar (top-right) ---
            GameObject energyObj = new GameObject("EnergyBar");
            energyObj.transform.SetParent(canvasObj.transform, false);
            RectTransform energyRT = energyObj.AddComponent<RectTransform>();
            energyRT.anchorMin = new Vector2(1f, 1f);
            energyRT.anchorMax = new Vector2(1f, 1f);
            energyRT.pivot = new Vector2(1f, 1f);
            energyRT.sizeDelta = new Vector2(160, 60);
            energyRT.anchoredPosition = new Vector2(-15, -15);

            // Background panel
            Image energyBg = energyObj.AddComponent<Image>();
            energyBg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

            // Label text ("ENERGY")
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(energyObj.transform, false);
            RectTransform labelRT = labelObj.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0.55f);
            labelRT.anchorMax = new Vector2(1, 1f);
            labelRT.offsetMin = new Vector2(10, 0);
            labelRT.offsetMax = new Vector2(-10, -4);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "ENERGY";
            labelText.fontSize = 14;
            labelText.color = new Color(0.7f, 0.7f, 0.8f);
            labelText.alignment = TextAlignmentOptions.Left;

            // Counter text ("5 / 5")
            GameObject counterObj = new GameObject("Counter");
            counterObj.transform.SetParent(energyObj.transform, false);
            RectTransform counterRT = counterObj.AddComponent<RectTransform>();
            counterRT.anchorMin = new Vector2(0, 0.55f);
            counterRT.anchorMax = new Vector2(1, 1f);
            counterRT.offsetMin = new Vector2(10, 0);
            counterRT.offsetMax = new Vector2(-10, -4);
            TextMeshProUGUI energyText = counterObj.AddComponent<TextMeshProUGUI>();
            energyText.text = "5 / 5";
            energyText.fontSize = 16;
            energyText.color = new Color(0f, 0.8f, 1f);
            energyText.alignment = TextAlignmentOptions.Right;

            // Fill bar background
            GameObject fillBgObj = new GameObject("FillBackground");
            fillBgObj.transform.SetParent(energyObj.transform, false);
            RectTransform fillBgRT = fillBgObj.AddComponent<RectTransform>();
            fillBgRT.anchorMin = new Vector2(0, 0);
            fillBgRT.anchorMax = new Vector2(1, 0.45f);
            fillBgRT.offsetMin = new Vector2(10, 6);
            fillBgRT.offsetMax = new Vector2(-10, 0);
            Image fillBgImg = fillBgObj.AddComponent<Image>();
            fillBgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            // Fill bar
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillBgObj.transform, false);
            RectTransform fillRT = fillObj.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            Image fillBar = fillObj.AddComponent<Image>();
            fillBar.color = new Color(0f, 0.8f, 1f);
            fillBar.type = Image.Type.Filled;
            fillBar.fillMethod = Image.FillMethod.Horizontal;
            fillBar.fillAmount = 1f;

            UI.EnergyBarUI energyUI = energyObj.AddComponent<UI.EnergyBarUI>();
            SerializedObject ebuiSO = new SerializedObject(energyUI);
            ebuiSO.FindProperty("energyText").objectReferenceValue = energyText;
            ebuiSO.FindProperty("labelText").objectReferenceValue = labelText;
            ebuiSO.FindProperty("fillBar").objectReferenceValue = fillBar;
            ebuiSO.FindProperty("fillBackground").objectReferenceValue = fillBgImg;
            ebuiSO.ApplyModifiedProperties();

            // --- Character Bio Panel (top-left) ---
            GameObject bioContainer = new GameObject("CharacterBio");
            bioContainer.transform.SetParent(canvasObj.transform, false);
            RectTransform bioContainerRT = bioContainer.AddComponent<RectTransform>();
            bioContainerRT.anchorMin = new Vector2(0, 0.75f);
            bioContainerRT.anchorMax = new Vector2(0.3f, 1f);
            bioContainerRT.offsetMin = new Vector2(10, 0);
            bioContainerRT.offsetMax = new Vector2(0, -5);

            // Bio toggle button (character name)
            GameObject bioToggle = new GameObject("ToggleButton");
            bioToggle.transform.SetParent(bioContainer.transform, false);
            RectTransform btRT = bioToggle.AddComponent<RectTransform>();
            btRT.anchorMin = new Vector2(0, 0.75f);
            btRT.anchorMax = new Vector2(1, 1);
            btRT.offsetMin = Vector2.zero;
            btRT.offsetMax = Vector2.zero;

            Image toggleBg = bioToggle.AddComponent<Image>();
            toggleBg.color = new Color(0.15f, 0.15f, 0.2f, 0.85f);
            Button bioButton = bioToggle.AddComponent<Button>();
            bioButton.targetGraphic = toggleBg;

            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(bioToggle.transform, false);
            RectTransform nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.anchorMin = Vector2.zero;
            nameRT.anchorMax = Vector2.one;
            nameRT.offsetMin = new Vector2(10, 0);
            nameRT.offsetMax = new Vector2(-10, 0);
            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = "Character Name";
            nameText.fontSize = 20;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;

            // Bio panel (expandable)
            GameObject bioPanel = new GameObject("BioPanel");
            bioPanel.transform.SetParent(bioContainer.transform, false);
            RectTransform bpRT = bioPanel.AddComponent<RectTransform>();
            bpRT.anchorMin = new Vector2(0, 0);
            bpRT.anchorMax = new Vector2(1, 0.75f);
            bpRT.offsetMin = Vector2.zero;
            bpRT.offsetMax = Vector2.zero;

            Image bioPanelBg = bioPanel.AddComponent<Image>();
            bioPanelBg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

            GameObject bioTextObj = new GameObject("BioText");
            bioTextObj.transform.SetParent(bioPanel.transform, false);
            RectTransform btxRT = bioTextObj.AddComponent<RectTransform>();
            btxRT.anchorMin = Vector2.zero;
            btxRT.anchorMax = Vector2.one;
            btxRT.offsetMin = new Vector2(10, 5);
            btxRT.offsetMax = new Vector2(-10, -5);
            TextMeshProUGUI bioText = bioTextObj.AddComponent<TextMeshProUGUI>();
            bioText.text = "Character bio goes here...";
            bioText.fontSize = 16;
            bioText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            bioText.alignment = TextAlignmentOptions.TopLeft;

            UI.CharacterBioUI bioUI = bioContainer.AddComponent<UI.CharacterBioUI>();
            SerializedObject bioSO = new SerializedObject(bioUI);
            bioSO.FindProperty("nameText").objectReferenceValue = nameText;
            bioSO.FindProperty("bioText").objectReferenceValue = bioText;
            bioSO.FindProperty("bioPanel").objectReferenceValue = bioPanel;
            bioSO.FindProperty("toggleButton").objectReferenceValue = bioButton;
            bioSO.ApplyModifiedProperties();

            // --- Dialogue Bubble (world-space canvas) ---
            GameObject dialogueObj = new GameObject("DialogueBubble");
            Canvas dialogueCanvas = dialogueObj.AddComponent<Canvas>();
            dialogueCanvas.renderMode = RenderMode.WorldSpace;
            dialogueCanvas.sortingOrder = 20;
            dialogueObj.AddComponent<CanvasScaler>();
            // No GraphicRaycaster — bubble shouldn't block clicks

            RectTransform dRT = dialogueObj.GetComponent<RectTransform>();
            dRT.sizeDelta = new Vector2(600f, 300f);
            dRT.localScale = Vector3.one * 0.008f; // World-space scaling: 600px * 0.008 = 4.8 world units wide

            // Bubble container with vertical layout for auto-sizing
            GameObject bubbleContainer = new GameObject("BubbleContainer");
            bubbleContainer.transform.SetParent(dialogueObj.transform, false);
            RectTransform bcRT = bubbleContainer.AddComponent<RectTransform>();
            bcRT.anchorMin = new Vector2(0.5f, 0f);
            bcRT.anchorMax = new Vector2(0.5f, 1f);
            bcRT.pivot = new Vector2(0.5f, 0f);
            bcRT.sizeDelta = new Vector2(600f, 0f);

            CanvasGroup cg = bubbleContainer.AddComponent<CanvasGroup>();

            // Vertical layout + content size fitter for auto-height
            VerticalLayoutGroup vlg = bubbleContainer.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter bcFitter = bubbleContainer.AddComponent<ContentSizeFitter>();
            bcFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bcFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Background panel (child of container, behind text)
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(bubbleContainer.transform, false);
            RectTransform bgRT = bgObj.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            Image bubbleBg = bgObj.AddComponent<Image>();
            bubbleBg.color = new Color(0.12f, 0.12f, 0.18f, 0.92f);
            bubbleBg.raycastTarget = false;
            // Rounded sprite will be created at runtime by DialogueBubbleUI

            // Add LayoutElement to background so it doesn't affect layout
            LayoutElement bgLE = bgObj.AddComponent<LayoutElement>();
            bgLE.ignoreLayout = true;

            // Text object (child of container)
            GameObject dialogueTextObj = new GameObject("DialogueText");
            dialogueTextObj.transform.SetParent(bubbleContainer.transform, false);
            RectTransform dtRT = dialogueTextObj.AddComponent<RectTransform>();
            TextMeshProUGUI dialogueText = dialogueTextObj.AddComponent<TextMeshProUGUI>();
            dialogueText.text = "";
            dialogueText.fontSize = 36;
            dialogueText.color = Color.white;
            dialogueText.alignment = TextAlignmentOptions.Center;
            dialogueText.enableWordWrapping = true;
            dialogueText.overflowMode = TextOverflowModes.Overflow;
            dialogueText.margin = new Vector4(28, 20, 28, 20); // left, top, right, bottom padding
            dialogueText.raycastTarget = false;

            // Tail (small triangle below the bubble)
            GameObject tailObj = new GameObject("Tail");
            tailObj.transform.SetParent(dialogueObj.transform, false);
            RectTransform tailRT = tailObj.AddComponent<RectTransform>();
            tailRT.anchorMin = new Vector2(0.5f, 0f);
            tailRT.anchorMax = new Vector2(0.5f, 0f);
            tailRT.pivot = new Vector2(0.5f, 1f);
            tailRT.anchoredPosition = new Vector2(0f, 5f);
            tailRT.sizeDelta = new Vector2(40f, 24f);
            Image tailImg = tailObj.AddComponent<Image>();
            tailImg.color = new Color(0.12f, 0.12f, 0.18f, 0.92f);
            tailImg.raycastTarget = false;
            // Triangle sprite will be created at runtime by DialogueBubbleUI

            UI.DialogueBubbleUI dialogueUI = dialogueObj.AddComponent<UI.DialogueBubbleUI>();
            SerializedObject duiSO = new SerializedObject(dialogueUI);
            duiSO.FindProperty("dialogueText").objectReferenceValue = dialogueText;
            duiSO.FindProperty("bubbleContainer").objectReferenceValue = bubbleContainer;
            duiSO.FindProperty("canvasGroup").objectReferenceValue = cg;
            duiSO.FindProperty("backgroundImage").objectReferenceValue = bubbleBg;
            duiSO.FindProperty("tailImage").objectReferenceValue = tailImg;
            duiSO.ApplyModifiedProperties();

            Debug.Log("UI setup complete: PromptInput, EnergyBar, CharacterBio, DialogueBubble");
        }

        private static GameObject CreateUIPanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Vector2 pivot, Vector2 sizeDelta, Color bgColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.offsetMin = new Vector2(offsetMin.x, 0);
            rt.offsetMax = new Vector2(offsetMax.x, sizeDelta.y);

            Image bg = panel.AddComponent<Image>();
            bg.color = bgColor;

            return panel;
        }

        private static void WireReferences(GameObject gameManagerObj, GameObject gridManagerObj, GameObject levelLoaderObj, Camera mainCamera, GameObject objectManagerObj)
        {
            var gridManager = gridManagerObj.GetComponent<Grid.GridManager>();
            var pathfinding = gridManagerObj.GetComponent<Grid.Pathfinding>();
            var levelLoader = levelLoaderObj.GetComponent<Core.LevelLoader>();
            var cameraController = mainCamera.GetComponent<Animation.CameraController>();

            // Wire GameManager
            SerializedObject gmSO = new SerializedObject(gameManagerObj.GetComponent<Core.GameManager>());
            gmSO.FindProperty("levelLoader").objectReferenceValue = levelLoader;
            gmSO.FindProperty("gridManager").objectReferenceValue = gridManager;
            gmSO.FindProperty("pathfinding").objectReferenceValue = pathfinding;
            gmSO.FindProperty("cameraController").objectReferenceValue = cameraController;
            if (objectManagerObj != null)
            {
                var prop = gmSO.FindProperty("objectManager");
                if (prop != null)
                    prop.objectReferenceValue = objectManagerObj.GetComponent<Core.ObjectManager>();
            }
            // Wire TurnManager
            var turnManagerComp = FindObjectOfType<Core.TurnManager>();
            if (turnManagerComp != null)
            {
                var prop2 = gmSO.FindProperty("turnManager");
                if (prop2 != null)
                    prop2.objectReferenceValue = turnManagerComp;
            }
            gmSO.ApplyModifiedProperties();

            // Wire LevelLoader
            SerializedObject llSO = new SerializedObject(levelLoader);
            llSO.FindProperty("gridManager").objectReferenceValue = gridManager;
            llSO.FindProperty("pathfinding").objectReferenceValue = pathfinding;
            llSO.FindProperty("cameraController").objectReferenceValue = cameraController;
            if (objectManagerObj != null)
            {
                var prop = llSO.FindProperty("objectManager");
                if (prop != null)
                    prop.objectReferenceValue = objectManagerObj.GetComponent<Core.ObjectManager>();
            }

            // Wire tile prefabs
            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tiles/FloorTile.prefab");
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tiles/WallTile.prefab");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tiles/DoorTile.prefab");
            var pressurePlatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tiles/PressurePlateTile.prefab");

            SerializedObject gridSO = new SerializedObject(gridManager);
            if (floorPrefab != null) gridSO.FindProperty("floorPrefab").objectReferenceValue = floorPrefab;
            if (wallPrefab != null) gridSO.FindProperty("wallPrefab").objectReferenceValue = wallPrefab;
            if (doorPrefab != null) gridSO.FindProperty("exitPrefab").objectReferenceValue = doorPrefab;
            if (pressurePlatePrefab != null) gridSO.FindProperty("pressurePlatePrefab").objectReferenceValue = pressurePlatePrefab;
            gridSO.ApplyModifiedProperties();

            // Wire object prefab arrays
            // Keys: Key1, Key2
            var keyPrefabs = new[] {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/key/Key1.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/key/Key2.prefab")
            };
            var keyArrayProp = llSO.FindProperty("keyPrefabs");
            if (keyArrayProp != null)
            {
                keyArrayProp.arraySize = keyPrefabs.Length;
                for (int i = 0; i < keyPrefabs.Length; i++)
                {
                    if (keyPrefabs[i] != null)
                        keyArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = keyPrefabs[i];
                }
            }

            // Gems: Gem1, Gem2, Gem3
            var gemPrefabs = new[] {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/gems/Gem1.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/gems/Gem2.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/gems/Gem3.prefab")
            };
            var gemArrayProp = llSO.FindProperty("gemPrefabs");
            if (gemArrayProp != null)
            {
                gemArrayProp.arraySize = gemPrefabs.Length;
                for (int i = 0; i < gemPrefabs.Length; i++)
                {
                    if (gemPrefabs[i] != null)
                        gemArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = gemPrefabs[i];
                }
            }

            // Pedestal
            var pedestalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Pedestal.prefab");
            if (pedestalPrefab != null) llSO.FindProperty("pedestalPrefab").objectReferenceValue = pedestalPrefab;
            llSO.ApplyModifiedProperties();

            // Wire character prefab
            var charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Explorer.prefab");
            if (charPrefab != null)
            {
                SerializedObject llSO2 = new SerializedObject(levelLoader);
                llSO2.FindProperty("characterPrefab").objectReferenceValue = charPrefab;
                llSO2.ApplyModifiedProperties();
            }

            Debug.Log("All references wired.");
            if (floorPrefab == null || wallPrefab == null || doorPrefab == null)
            {
                Debug.LogWarning("Some prefabs not found. Run 'Generate Prefabs from Models' first, then re-run 'Setup Game Scene'.");
            }
        }

        // ========== Helpers ==========

        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string currentPath = parts[0];

                for (int i = 1; i < parts.Length; i++)
                {
                    string nextPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = nextPath;
                }
            }
        }

        private static void SetMaterialColor(GameObject obj, Color color)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                {
                    mat = new Material(Shader.Find("Standard"));
                }
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }
                renderer.sharedMaterial = mat;
            }
        }

        private static void SavePrefab(GameObject obj, string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PrefabUtility.SaveAsPrefabAsset(obj, path);
            Debug.Log($"Created prefab: {path}");
        }
    }
}
#endif
