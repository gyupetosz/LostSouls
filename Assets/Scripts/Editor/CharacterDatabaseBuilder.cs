#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;
using LostSouls.Character;

namespace LostSouls.Editor
{
    public class CharacterDatabaseBuilder : EditorWindow
    {
        private const string CharactersRoot = "Assets/Characters";
        private const string DatabasePath = "Assets/Resources/CharacterModelDatabase.asset";
        private const string ControllerPath = "Assets/Resources/CharacterBaseController.controller";

        [MenuItem("Lost Souls/Build Character Database")]
        public static void BuildDatabase()
        {
            // Ensure output directories exist
            EnsureDirectory("Assets/Resources");

            // Build or update the base animator controller
            AnimatorController baseController = BuildBaseAnimatorController();

            // Scan character folders
            var entries = new List<CharacterModelEntry>();

            if (!Directory.Exists(CharactersRoot))
            {
                Debug.LogError($"Characters folder not found at {CharactersRoot}");
                return;
            }

            string[] charFolders = Directory.GetDirectories(CharactersRoot);
            foreach (string folder in charFolders)
            {
                string folderName = Path.GetFileName(folder);
                string assetFolder = $"{CharactersRoot}/{folderName}";

                CharacterModelEntry entry = BuildEntry(folderName, assetFolder);
                if (entry != null)
                {
                    entries.Add(entry);
                    Debug.Log($"  Loaded character: {entry.modelId}");
                }
                else
                {
                    Debug.LogWarning($"  Skipped folder: {folderName} (no model FBX found)");
                }
            }

            // Create or update the database ScriptableObject
            CharacterModelDatabase database = AssetDatabase.LoadAssetAtPath<CharacterModelDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<CharacterModelDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.entries = entries.ToArray();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Character Database built: {entries.Count} characters at {DatabasePath}");
            Debug.Log($"Base Animator Controller at {ControllerPath}");
        }

        private static CharacterModelEntry BuildEntry(string folderId, string assetFolder)
        {
            // Find the main model FBX (Character_N.fbx)
            GameObject modelPrefab = null;
            string[] fbxFiles = Directory.GetFiles(assetFolder, "Character_*.fbx");
            if (fbxFiles.Length == 0)
            {
                // Try any FBX that isn't an animation
                string[] allFbx = Directory.GetFiles(assetFolder, "*.fbx");
                foreach (string f in allFbx)
                {
                    string name = Path.GetFileNameWithoutExtension(f).ToLower();
                    if (name != "idle" && name != "walk" && name != "pick" &&
                        name != "push" && name != "push_idle" &&
                        name != "hold_idle" && name != "hold_walk")
                    {
                        modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace("\\", "/"));
                        break;
                    }
                }
            }
            else
            {
                modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxFiles[0].Replace("\\", "/"));
            }

            if (modelPrefab == null) return null;

            var entry = new CharacterModelEntry
            {
                modelId = folderId,
                modelPrefab = modelPrefab,
                texture = FindTexture(assetFolder),
                idle = FindAnimationClip(assetFolder, "Idle"),
                walk = FindAnimationClip(assetFolder, "Walk"),
                pick = FindAnimationClip(assetFolder, "Pick"),
                push = FindAnimationClip(assetFolder, "Push"),
                pushIdle = FindAnimationClip(assetFolder, "Push_Idle"),
                holdIdle = FindAnimationClip(assetFolder, "Hold_Idle"),
                holdWalk = FindAnimationClip(assetFolder, "Hold_Walk"),
            };

            return entry;
        }

        // Clips that should loop
        private static readonly HashSet<string> LoopingClips = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase)
        { "Idle", "Walk", "Hold_Idle", "Hold_Walk", "Push_Idle" };

        private static AnimationClip FindAnimationClip(string folder, string clipName)
        {
            string fbxPath = $"{folder}/{clipName}.fbx";
            if (!File.Exists(fbxPath)) return null;

            string assetPath = fbxPath.Replace("\\", "/");

            // Set loop time via ModelImporter before loading the clip
            bool shouldLoop = LoopingClips.Contains(clipName);
            SetClipLoopTime(assetPath, shouldLoop);

            // Load all assets from the FBX — the clip is a sub-asset
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            // Fallback: try loading the FBX directly as a clip
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static void SetClipLoopTime(string assetPath, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                // Use default clip animations as base if none are set
                clips = importer.defaultClipAnimations;
            }
            if (clips == null || clips.Length == 0) return;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].loopTime != loop)
                {
                    clips[i].loopTime = loop;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static Texture2D FindTexture(string folder)
        {
            // Search for jpg/png texture files
            string[] extensions = { "*.jpg", "*.jpeg", "*.png", "*.tga" };
            foreach (string ext in extensions)
            {
                string[] files = Directory.GetFiles(folder, ext);
                if (files.Length > 0)
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(files[0].Replace("\\", "/"));
                }
            }
            return null;
        }

        /// <summary>
        /// Creates a base AnimatorController with states and transitions.
        /// Characters override the clips via AnimatorOverrideController at runtime.
        /// </summary>
        private static AnimatorController BuildBaseAnimatorController()
        {
            // Check if it already exists
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
            {
                Debug.Log("Base AnimatorController already exists, recreating...");
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Add parameters
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsHolding", AnimatorControllerParameterType.Bool);
            controller.AddParameter("PickUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Push", AnimatorControllerParameterType.Trigger);

            // Get the base layer state machine
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // Create placeholder clips for each state (will be overridden at runtime)
            AnimationClip placeholderIdle = CreatePlaceholderClip("Idle");
            AnimationClip placeholderWalk = CreatePlaceholderClip("Walk");
            AnimationClip placeholderPick = CreatePlaceholderClip("Pick");
            AnimationClip placeholderPush = CreatePlaceholderClip("Push");
            AnimationClip placeholderPushIdle = CreatePlaceholderClip("PushIdle");
            AnimationClip placeholderHoldIdle = CreatePlaceholderClip("HoldIdle");
            AnimationClip placeholderHoldWalk = CreatePlaceholderClip("HoldWalk");

            // Add clips as sub-assets of the controller
            AssetDatabase.AddObjectToAsset(placeholderIdle, ControllerPath);
            AssetDatabase.AddObjectToAsset(placeholderWalk, ControllerPath);
            AssetDatabase.AddObjectToAsset(placeholderPick, ControllerPath);
            AssetDatabase.AddObjectToAsset(placeholderPush, ControllerPath);
            AssetDatabase.AddObjectToAsset(placeholderPushIdle, ControllerPath);
            AssetDatabase.AddObjectToAsset(placeholderHoldIdle, ControllerPath);
            AssetDatabase.AddObjectToAsset(placeholderHoldWalk, ControllerPath);

            // Create states
            AnimatorState idleState = sm.AddState("Idle", new Vector3(0, 0, 0));
            idleState.motion = placeholderIdle;
            sm.defaultState = idleState;

            AnimatorState walkState = sm.AddState("Walk", new Vector3(300, 0, 0));
            walkState.motion = placeholderWalk;

            AnimatorState holdIdleState = sm.AddState("HoldIdle", new Vector3(0, 200, 0));
            holdIdleState.motion = placeholderHoldIdle;

            AnimatorState holdWalkState = sm.AddState("HoldWalk", new Vector3(300, 200, 0));
            holdWalkState.motion = placeholderHoldWalk;

            AnimatorState pickState = sm.AddState("Pick", new Vector3(150, 100, 0));
            pickState.motion = placeholderPick;

            AnimatorState pushState = sm.AddState("Push", new Vector3(-150, 100, 0));
            pushState.motion = placeholderPush;

            AnimatorState pushIdleState = sm.AddState("PushIdle", new Vector3(-150, 0, 0));
            pushIdleState.motion = placeholderPushIdle;

            // --- Transitions ---

            // Idle → Walk (IsMoving = true, IsHolding = false)
            var t1 = idleState.AddTransition(walkState);
            t1.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            t1.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHolding");
            t1.hasExitTime = false;
            t1.duration = 0.1f;

            // Walk → Idle (IsMoving = false)
            var t2 = walkState.AddTransition(idleState);
            t2.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            t2.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHolding");
            t2.hasExitTime = false;
            t2.duration = 0.1f;

            // Idle → HoldIdle (IsHolding = true, IsMoving = false)
            var t3 = idleState.AddTransition(holdIdleState);
            t3.AddCondition(AnimatorConditionMode.If, 0, "IsHolding");
            t3.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            t3.hasExitTime = false;
            t3.duration = 0.1f;

            // HoldIdle → Idle (IsHolding = false)
            var t4 = holdIdleState.AddTransition(idleState);
            t4.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHolding");
            t4.hasExitTime = false;
            t4.duration = 0.1f;

            // HoldIdle → HoldWalk (IsMoving = true)
            var t5 = holdIdleState.AddTransition(holdWalkState);
            t5.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            t5.hasExitTime = false;
            t5.duration = 0.1f;

            // HoldWalk → HoldIdle (IsMoving = false)
            var t6 = holdWalkState.AddTransition(holdIdleState);
            t6.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            t6.hasExitTime = false;
            t6.duration = 0.1f;

            // Walk → HoldWalk (IsHolding = true, IsMoving = true)
            var t7 = walkState.AddTransition(holdWalkState);
            t7.AddCondition(AnimatorConditionMode.If, 0, "IsHolding");
            t7.hasExitTime = false;
            t7.duration = 0.1f;

            // HoldWalk → Walk (IsHolding = false, IsMoving = true)
            var t8 = holdWalkState.AddTransition(walkState);
            t8.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHolding");
            t8.hasExitTime = false;
            t8.duration = 0.1f;

            // Any State → Pick (PickUp trigger)
            var tPick = sm.AddAnyStateTransition(pickState);
            tPick.AddCondition(AnimatorConditionMode.If, 0, "PickUp");
            tPick.hasExitTime = false;
            tPick.duration = 0.1f;

            // Pick → HoldIdle (exit time)
            var tPickExit = pickState.AddTransition(holdIdleState);
            tPickExit.hasExitTime = true;
            tPickExit.exitTime = 0.9f;
            tPickExit.duration = 0.1f;

            // Any State → Push (Push trigger)
            var tPush = sm.AddAnyStateTransition(pushState);
            tPush.AddCondition(AnimatorConditionMode.If, 0, "Push");
            tPush.hasExitTime = false;
            tPush.duration = 0.1f;

            // Push → PushIdle (exit time)
            var tPushExit = pushState.AddTransition(pushIdleState);
            tPushExit.hasExitTime = true;
            tPushExit.exitTime = 0.9f;
            tPushExit.duration = 0.1f;

            // PushIdle → Idle (after brief hold)
            var tPushIdleExit = pushIdleState.AddTransition(idleState);
            tPushIdleExit.hasExitTime = true;
            tPushIdleExit.exitTime = 0.8f;
            tPushIdleExit.duration = 0.2f;

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip CreatePlaceholderClip(string name)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = name;
            // Add a dummy keyframe so the clip has non-zero length
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, 1f, 0));

            // Set looping on clips that should loop
            if (LoopingClips.Contains(name) || name == "Idle" || name == "Walk" ||
                name == "HoldIdle" || name == "HoldWalk" || name == "PushIdle")
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }

            return clip;
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif
