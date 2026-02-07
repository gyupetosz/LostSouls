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
        [SerializeField] private Image fillBar;
        [SerializeField] private GameObject pipContainer;
        [SerializeField] private GameObject pipPrefab;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.cyan;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;

        private GameManager gameManager;
        private Image[] pips;

        private void Start()
        {
            gameManager = GameManager.Instance;

            if (gameManager != null)
            {
                gameManager.OnPromptUsed += UpdateEnergy;
                gameManager.OnLevelStarted += OnLevelStarted;
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
            // Update text
            if (energyText != null)
            {
                energyText.text = $"{remaining}/{budget}";

                // Color based on remaining
                if (remaining <= 1)
                    energyText.color = criticalColor;
                else if (remaining <= budget * 0.3f)
                    energyText.color = warningColor;
                else
                    energyText.color = normalColor;
            }

            // Update fill bar
            if (fillBar != null)
            {
                float fill = budget > 0 ? (float)remaining / budget : 0f;
                fillBar.fillAmount = fill;

                if (remaining <= 1)
                    fillBar.color = criticalColor;
                else if (remaining <= budget * 0.3f)
                    fillBar.color = warningColor;
                else
                    fillBar.color = normalColor;
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnPromptUsed -= UpdateEnergy;
                gameManager.OnLevelStarted -= OnLevelStarted;
            }
        }
    }
}
