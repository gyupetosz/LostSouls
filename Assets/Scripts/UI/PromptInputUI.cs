using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LostSouls.Core;

namespace LostSouls.UI
{
    public class PromptInputUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private TextMeshProUGUI charCountText;

        [Header("Settings")]
        [SerializeField] private int maxCharacters = 150;

        private TurnManager turnManager;
        private bool inputEnabled = true;

        private void Start()
        {
            turnManager = FindObjectOfType<TurnManager>();

            if (inputField != null)
            {
                inputField.onValueChanged.AddListener(OnInputChanged);
                inputField.characterLimit = maxCharacters;
            }

            if (sendButton != null)
            {
                sendButton.onClick.AddListener(OnSendClicked);
            }

            if (turnManager != null)
            {
                turnManager.OnTurnStarted += DisableInput;
                turnManager.OnTurnCompleted += EnableInput;
            }

            UpdateCharCounter();
            UpdateSendButton();
        }

        public void SetMaxCharacters(int max)
        {
            maxCharacters = max;
            if (inputField != null)
            {
                inputField.characterLimit = max;
            }
            UpdateCharCounter();
        }

        private void OnInputChanged(string text)
        {
            UpdateCharCounter();
            UpdateSendButton();
        }

        private void OnSendClicked()
        {
            if (inputField == null || string.IsNullOrWhiteSpace(inputField.text)) return;
            if (turnManager == null || turnManager.IsProcessingTurn) return;

            string prompt = inputField.text.Trim();
            inputField.text = "";

            turnManager.SubmitPrompt(prompt);
        }

        private void Update()
        {
            // Allow Enter key to send
            if (inputEnabled && Input.GetKeyDown(KeyCode.Return) && inputField != null &&
                !string.IsNullOrWhiteSpace(inputField.text))
            {
                OnSendClicked();
            }
        }

        private void UpdateCharCounter()
        {
            if (charCountText == null) return;
            int remaining = maxCharacters - (inputField?.text?.Length ?? 0);
            charCountText.text = remaining.ToString();

            // Color warning
            if (remaining <= 10)
                charCountText.color = Color.red;
            else if (remaining <= 30)
                charCountText.color = Color.yellow;
            else
                charCountText.color = Color.white;
        }

        private void UpdateSendButton()
        {
            if (sendButton != null)
            {
                sendButton.interactable = inputEnabled &&
                    inputField != null &&
                    !string.IsNullOrWhiteSpace(inputField.text);
            }
        }

        private void DisableInput()
        {
            inputEnabled = false;
            if (inputField != null) inputField.interactable = false;
            UpdateSendButton();
        }

        private void EnableInput()
        {
            inputEnabled = true;
            if (inputField != null)
            {
                inputField.interactable = true;
                inputField.ActivateInputField();
            }
            UpdateSendButton();
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnTurnStarted -= DisableInput;
                turnManager.OnTurnCompleted -= EnableInput;
            }
        }
    }
}
