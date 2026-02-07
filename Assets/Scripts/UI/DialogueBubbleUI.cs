using System.Collections;
using UnityEngine;
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

        [Header("Settings")]
        [SerializeField] private float typewriterSpeed = 0.03f;
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        [Header("Follow Target")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 offset = new Vector3(0, 2.5f, 0);

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

            HideBubble();
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
                yield return new WaitForSeconds(typewriterSpeed);
            }

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
            if (followTarget != null && mainCamera != null && bubbleContainer != null &&
                bubbleContainer.activeSelf)
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
    }
}
