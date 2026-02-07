using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LostSouls.Core;

namespace LostSouls.UI
{
    public class DialogueBubbleUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private GameObject bubbleContainer;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image tailImage;

        [Header("Settings")]
        [SerializeField] private float typewriterSpeed = 0.03f;
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float maxWidth = 350f;

        [Header("Follow Target")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 offset = new Vector3(0, 3.5f, 0);

        private TurnManager turnManager;
        private Coroutine displayCoroutine;
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;
            turnManager = FindObjectOfType<TurnManager>();

            if (turnManager != null)
            {
                turnManager.OnCharacterResponse += ShowDialogue;
                turnManager.OnInputRejected += ShowRejection;
            }

            // Auto-find character if no follow target set
            if (followTarget == null)
            {
                var character = FindObjectOfType<LostSouls.Character.ExplorerController>();
                if (character != null)
                    followTarget = character.transform;
            }

            // Generate rounded background sprite if no sprite assigned
            SetupBubbleVisuals();

            HideBubble();
        }

        private void SetupBubbleVisuals()
        {
            if (backgroundImage != null && backgroundImage.sprite == null)
            {
                backgroundImage.sprite = CreateRoundedRectSprite(64, 64, 16);
                backgroundImage.type = Image.Type.Sliced;
                backgroundImage.pixelsPerUnitMultiplier = 1f;
            }

            if (tailImage != null && tailImage.sprite == null)
            {
                tailImage.sprite = CreateTriangleSprite(32, 20);
            }
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
        }

        public void ShowDialogue(string dialogue, string emotion)
        {
            if (string.IsNullOrEmpty(dialogue)) return;

            if (displayCoroutine != null)
                StopCoroutine(displayCoroutine);

            displayCoroutine = StartCoroutine(DisplayDialogueCoroutine(dialogue));
        }

        public void ShowRejection(string dialogue)
        {
            ShowDialogue(dialogue, "annoyed");
        }

        private IEnumerator DisplayDialogueCoroutine(string fullText)
        {
            ShowBubble();

            // Typewriter effect
            dialogueText.text = "";
            for (int i = 0; i < fullText.Length; i++)
            {
                dialogueText.text += fullText[i];

                // Force layout rebuild periodically so bubble resizes during typing
                if (i % 5 == 0)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleContainer.GetComponent<RectTransform>());

                yield return new WaitForSeconds(typewriterSpeed);
            }

            // Final layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleContainer.GetComponent<RectTransform>());

            // Hold for display duration
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = 1f - (elapsed / fadeOutDuration);
                    yield return null;
                }
            }

            HideBubble();
        }

        private void ShowBubble()
        {
            if (bubbleContainer != null)
                bubbleContainer.SetActive(true);
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        private void HideBubble()
        {
            if (bubbleContainer != null)
                bubbleContainer.SetActive(false);
            if (dialogueText != null)
                dialogueText.text = "";
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                // Try to find character again (might have been spawned after Start)
                var character = FindObjectOfType<LostSouls.Character.ExplorerController>();
                if (character != null)
                    followTarget = character.transform;
                return;
            }

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null && bubbleContainer != null && bubbleContainer.activeSelf)
            {
                transform.position = followTarget.position + offset;
                // Billboard: face camera
                transform.rotation = mainCamera.transform.rotation;
            }
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnCharacterResponse -= ShowDialogue;
                turnManager.OnInputRejected -= ShowRejection;
            }
        }

        /// <summary>
        /// Creates a rounded rectangle sprite at runtime for the bubble background.
        /// Uses 9-slice borders so it scales properly.
        /// </summary>
        private static Sprite CreateRoundedRectSprite(int width, int height, int radius)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color fill = Color.white;
            Color clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Check if pixel is inside rounded rectangle
                    bool inside = true;

                    // Check corners
                    if (x < radius && y < radius)
                        inside = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) <= radius;
                    else if (x >= width - radius && y < radius)
                        inside = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, radius)) <= radius;
                    else if (x < radius && y >= height - radius)
                        inside = Vector2.Distance(new Vector2(x, y), new Vector2(radius, height - radius - 1)) <= radius;
                    else if (x >= width - radius && y >= height - radius)
                        inside = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, height - radius - 1)) <= radius;

                    tex.SetPixel(x, y, inside ? fill : clear);
                }
            }

            tex.Apply();

            // 9-slice borders at the radius so corners stay round when stretched
            Vector4 border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>
        /// Creates a small downward-pointing triangle sprite for the bubble tail.
        /// </summary>
        private static Sprite CreateTriangleSprite(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color fill = Color.white;
            Color clear = new Color(0, 0, 0, 0);

            float halfW = width / 2f;

            for (int y = 0; y < height; y++)
            {
                // Triangle narrows as y goes down (y=0 is bottom tip, y=height-1 is top base)
                float t = (float)y / (height - 1);
                float halfSpan = halfW * t;
                float left = halfW - halfSpan;
                float right = halfW + halfSpan;

                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, (x >= left && x <= right) ? fill : clear);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 1f), 100f);
        }
    }
}
