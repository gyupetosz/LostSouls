using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LostSouls.Core;

namespace LostSouls.UI
{
    public class EnergyBarUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private Image fillBar;
        [SerializeField] private Image fillBackground;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.1f, 0.4f, 0.85f);
        [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.2f, 0.2f);

        private GameManager gameManager;
        private bool subscribed;

        private IEnumerator Start()
        {
            // Wait until GameManager singleton is available
            while (GameManager.Instance == null)
            {
                yield return null;
            }

            gameManager = GameManager.Instance;
            gameManager.OnPromptUsed += UpdateEnergy;
            gameManager.OnLevelStarted += OnLevelStarted;
            subscribed = true;

            // If a level is already loaded, show current state
            if (gameManager.CurrentState == GameState.Playing)
            {
                UpdateDisplay(gameManager.PromptBudget, gameManager.PromptsRemaining);
            }
        }

        private void OnLevelStarted(int levelId)
        {
            UpdateDisplay(gameManager.PromptBudget, gameManager.PromptsRemaining);
        }

        private void UpdateEnergy(int remaining)
        {
            UpdateDisplay(gameManager.PromptBudget, remaining);
        }

        private void UpdateDisplay(int budget, int remaining)
        {
            // Update counter text
            if (energyText != null)
            {
                energyText.text = $"{remaining} / {budget}";
                energyText.fontSize = 36;
                energyText.color = GetStateColor(budget, remaining);
            }

            // Update label
            if (labelText != null)
            {
                labelText.text = "\u26A1 ENERGY";
                labelText.fontSize = 22;
            }

            // Update fill bar
            if (fillBar != null)
            {
                float fill = budget > 0 ? (float)remaining / budget : 0f;
                fillBar.fillAmount = fill;
                fillBar.color = GetStateColor(budget, remaining);
            }
        }

        private Color GetStateColor(int budget, int remaining)
        {
            if (remaining <= 1)
                return criticalColor;
            if (remaining <= budget * 0.3f)
                return warningColor;
            return normalColor;
        }

        private void OnDestroy()
        {
            if (subscribed && gameManager != null)
            {
                gameManager.OnPromptUsed -= UpdateEnergy;
                gameManager.OnLevelStarted -= OnLevelStarted;
            }
        }
    }
}
