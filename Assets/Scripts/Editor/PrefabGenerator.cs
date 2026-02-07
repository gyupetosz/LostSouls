#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace LostSouls.Editor
{
    public class PrefabGenerator : EditorWindow
    {
        // Model paths in Assets/Models/
        private const string ModelsRoot = "Assets/Models";
        private const string FloorModelPath = ModelsRoot + "/floor tile/floor tile.fbx";
        private const string WallModelPath = ModelsRoot + "/wall block/wall block.fbx";
        private const string DoorModelPath = ModelsRoot + "/door/door.fbx";
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
        }

        /// <summary>
        /// Creates a tile prefab from an FBX model.
        /// Structure: Parent (with Tile + BoxCollider) -> Model child (with MeshRenderer)
        /// </summary>
        private static void CreateTilePrefab(string modelPath, string prefabName, string savePath, float modelScale = 1f)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning($"Model not found at {modelPath} — skipping {prefabName}");
                return;
            }

            // Create parent with Tile component
            GameObject parent = new GameObject(prefabName);
            parent.AddComponent<Grid.Tile>();

            // Add a box collider for raycasting
            BoxCollider collider = parent.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 2f);
            collider.center = Vector3.zero;

            // Instantiate model as child
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
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
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
            GameObject floorModel = AssetDatabase.LoadAssetAtPath<GameObject>(FloorModelPath);
            GameObject plateModel = AssetDatabase.LoadAssetAtPath<GameObject>(PressurePlateModelPath);

            if (floorModel == null || plateModel == null)
            {
                Debug.LogWarning("Floor or pressure plate model not found — skipping PressurePlateTile");
                return;
            }

            // Create parent with Tile component
            GameObject parent = new GameObject("PressurePlateTile");
            parent.AddComponent<Grid.Tile>();

            BoxCollider collider = parent.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 1f, 2f);
            collider.center = Vector3.zero;

            // Floor base (same scale as normal floor tiles)
            GameObject floorInstance = (GameObject)PrefabUtility.InstantiatePrefab(floorModel);
            floorInstance.name = "FloorBase";
            floorInstance.transform.SetParent(parent.transform);
            floorInstance.transform.localPosition = Vector3.zero;
            floorInstance.transform.localRotation = Quaternion.identity;
            floorInstance.transform.localScale = Vector3.one * 2f;

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

            // Wire all references
            WireReferences(gameManagerObj, gridManagerObj, levelLoaderObj, mainCamera, objectManagerObj);

            Debug.Log("Game scene setup complete! Press Play to test.");
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
