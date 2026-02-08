using System.Collections.Generic;
using UnityEngine;

namespace LostSouls.Character
{
    /// <summary>
    /// Manages character model swapping and animation playback.
    /// Added to the character GameObject at spawn time by LevelLoader.
    /// </summary>
    public class CharacterModelManager : MonoBehaviour
    {
        [Header("Database")]
        [SerializeField] private CharacterModelDatabase database;

        [Header("Runtime State")]
        [SerializeField] private string currentModelId;
        [SerializeField] private GameObject modelInstance;

        private Animator animator;
        private AnimatorOverrideController overrideController;
        private ExplorerController explorer;

        // Idle camera-facing
        private bool isIdle = true;
        private bool idleFacingCamera = false;
        private float idleTimer = 0f;
        private float idleDelay = 5f;
        private Vector3 modelBaseLocalPos;
        private float idleTurnSpeed = 5f;

        // Animator parameter hashes
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsHoldingHash = Animator.StringToHash("IsHolding");
        private static readonly int PickUpHash = Animator.StringToHash("PickUp");
        private static readonly int PushHash = Animator.StringToHash("Push");

        public void Initialize(CharacterModelDatabase db, ExplorerController explorerController)
        {
            database = db;
            explorer = explorerController;

            // Subscribe to explorer events
            if (explorer != null)
            {
                explorer.OnMovementStarted += HandleMovementStarted;
                explorer.OnMovementCompleted += HandleMovementCompleted;
                explorer.OnPickedUpObject += HandlePickedUp;
                explorer.OnPutDownObject += HandlePutDown;
                explorer.OnPushStarted += HandlePushStarted;
            }
        }

        /// <summary>
        /// Loads and instantiates the character model for the given model ID.
        /// Destroys any existing model first.
        /// </summary>
        public bool LoadModel(string modelId)
        {
            if (database == null)
            {
                Debug.LogWarning("CharacterModelManager: No database assigned");
                return false;
            }

            CharacterModelEntry entry = database.GetEntry(modelId);
            if (entry == null)
            {
                Debug.LogWarning($"CharacterModelManager: Model '{modelId}' not found in database");
                return false;
            }

            if (entry.modelPrefab == null)
            {
                Debug.LogWarning($"CharacterModelManager: Model '{modelId}' has no prefab");
                return false;
            }

            // Destroy existing model and placeholder visuals
            DestroyExistingVisuals();

            currentModelId = modelId;

            // Instantiate the model as a child
            modelInstance = Instantiate(entry.modelPrefab, transform);
            modelInstance.name = "CharacterModel";
            modelInstance.transform.localScale = Vector3.one * 2f;
            modelInstance.transform.localRotation = Quaternion.identity;

            // Center the model so rotation works around its visual center.
            // Compute the mesh bounds center in local space and offset so it sits at (0,0,0).
            Vector3 centerOffset = ComputeModelCenterOffset(modelInstance);
            modelBaseLocalPos = -centerOffset + Vector3.down * 0.5f;
            modelInstance.transform.localPosition = modelBaseLocalPos;

            // Apply texture to all renderers
            if (entry.texture != null)
            {
                ApplyTexture(modelInstance, entry.texture);
            }

            // Set up the Animator with override controller
            SetupAnimator(entry);

            Debug.Log($"CharacterModelManager: Loaded model '{modelId}'");
            return true;
        }

        private void DestroyExistingVisuals()
        {
            // Destroy previous model instance
            if (modelInstance != null)
            {
                Destroy(modelInstance);
                modelInstance = null;
            }

            // Destroy placeholder primitives (Body, Face, etc.)
            var toDestroy = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name == "Body" || child.name == "Face" ||
                    child.name == "CharacterModel" || child.name == "PlaceholderCharacter")
                {
                    toDestroy.Add(child.gameObject);
                }
            }
            foreach (var go in toDestroy)
            {
                Destroy(go);
            }
        }

        /// <summary>
        /// Computes the XZ center offset of the model's combined renderer bounds.
        /// Only offsets on X and Z so the model rotates around its visual center.
        /// Y is left alone (handled by the down offset separately).
        /// </summary>
        private Vector3 ComputeModelCenterOffset(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return Vector3.zero;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // Convert world-space bounds center to local space of the model
            Vector3 localCenter = model.transform.InverseTransformPoint(bounds.center);
            // Only correct XZ so rotation pivot is centered; keep Y as-is
            return new Vector3(localCenter.x, 0f, localCenter.z);
        }

        private void ApplyTexture(GameObject model, Texture2D texture)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.materials)
                {
                    mat.mainTexture = texture;
                }
            }
        }

        private void SetupAnimator(CharacterModelEntry entry)
        {
            // Get or add Animator component on the model instance
            animator = modelInstance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = modelInstance.AddComponent<Animator>();
            }

            // Load the base controller
            RuntimeAnimatorController baseController =
                Resources.Load<RuntimeAnimatorController>("CharacterBaseController");

            // Try loading from the Animation folder via AssetDatabase path (editor fallback)
            if (baseController == null)
            {
                // The controller is at Assets/Animation/ which isn't in Resources
                // We need to load it differently. For runtime, copy it to Resources during build.
                // For now, search the animator already on the FBX import
                Debug.LogWarning("CharacterModelManager: Base controller not found in Resources. " +
                    "Make sure to run 'Lost Souls/Build Character Database' and the controller is in Resources.");
            }

            if (baseController != null)
            {
                // Create an override controller to swap clips per character
                overrideController = new AnimatorOverrideController(baseController);

                // Map placeholder clip names to actual character clips
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrideController.GetOverrides(overrides);

                for (int i = 0; i < overrides.Count; i++)
                {
                    AnimationClip original = overrides[i].Key;
                    AnimationClip replacement = GetClipForState(entry, original.name);
                    if (replacement != null)
                    {
                        overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
                    }
                }

                overrideController.ApplyOverrides(overrides);
                animator.runtimeAnimatorController = overrideController;
            }

            // Initial state
            UpdateAnimatorState();
        }

        private AnimationClip GetClipForState(CharacterModelEntry entry, string stateName)
        {
            return stateName switch
            {
                "Idle" => entry.idle,
                "Walk" => entry.walk,
                "Pick" => entry.pick,
                "Push" => entry.push,
                "PushIdle" => entry.pushIdle,
                "HoldIdle" => entry.holdIdle,
                "HoldWalk" => entry.holdWalk,
                _ => null
            };
        }

        private void UpdateAnimatorState()
        {
            if (animator == null || explorer == null) return;

            animator.SetBool(IsMovingHash, explorer.IsMoving);
            animator.SetBool(IsHoldingHash, explorer.IsHoldingObject);
        }

        private void LateUpdate()
        {
            if (modelInstance == null) return;

            if (isIdle)
            {
                // Wait for idle delay before turning to camera
                idleTimer += Time.deltaTime;
                if (idleTimer >= idleDelay)
                {
                    idleFacingCamera = true;
                }

                if (idleFacingCamera)
                {
                    // Face the camera while idle (only rotate the model child, not the parent)
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 toCamera = cam.transform.position - transform.position;
                        toCamera.y = 0f;

                        if (toCamera.sqrMagnitude > 0.001f)
                        {
                            Quaternion worldTarget = Quaternion.LookRotation(toCamera.normalized);
                            Quaternion localTarget = Quaternion.Inverse(transform.rotation) * worldTarget;
                            modelInstance.transform.localRotation = Quaternion.Slerp(
                                modelInstance.transform.localRotation, localTarget, idleTurnSpeed * Time.deltaTime);
                        }
                    }
                }
            }
            else
            {
                // Snap model back to parent's forward (identity local rotation)
                modelInstance.transform.localRotation = Quaternion.Slerp(
                    modelInstance.transform.localRotation, Quaternion.identity, idleTurnSpeed * 2f * Time.deltaTime);
            }

            // Rotate held object to match the model's visual rotation
            if (explorer != null && explorer.IsHoldingObject && explorer.HeldGridObject != null)
            {
                Transform heldTransform = explorer.HeldGridObject.transform;
                if (heldTransform.parent == transform)
                {
                    // Recompute held position relative to the model's visual rotation
                    Quaternion modelLocalRot = modelInstance.transform.localRotation;
                    Vector3 holdOffset = Vector3.up * 0.8f + Vector3.forward * 0.375f;
                    heldTransform.localPosition = modelLocalRot * holdOffset;
                    heldTransform.localRotation = modelLocalRot * Quaternion.Euler(0f, 90f, 0f);
                }
            }
        }

        // --- Event Handlers ---

        private void HandleMovementStarted()
        {
            if (animator == null) return;
            isIdle = false;
            idleFacingCamera = false;
            idleTimer = 0f;
            animator.SetBool(IsMovingHash, true);
        }

        private void HandleMovementCompleted()
        {
            if (animator == null) return;
            isIdle = true;
            idleTimer = 0f;
            animator.SetBool(IsMovingHash, false);
        }

        private void HandlePickedUp()
        {
            if (animator == null) return;
            animator.SetBool(IsHoldingHash, true);
            animator.SetTrigger(PickUpHash);
        }

        private void HandlePutDown()
        {
            if (animator == null) return;
            animator.SetBool(IsHoldingHash, false);
        }

        private void HandlePushStarted()
        {
            if (animator == null) return;
            isIdle = false;
            idleFacingCamera = false;
            idleTimer = 0f;
            animator.SetTrigger(PushHash);
        }

        private void OnDestroy()
        {
            if (explorer != null)
            {
                explorer.OnMovementStarted -= HandleMovementStarted;
                explorer.OnMovementCompleted -= HandleMovementCompleted;
                explorer.OnPickedUpObject -= HandlePickedUp;
                explorer.OnPutDownObject -= HandlePutDown;
                explorer.OnPushStarted -= HandlePushStarted;
            }
        }
    }
}
