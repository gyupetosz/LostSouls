using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LostSouls.Core;

namespace LostSouls.UI
{
    public class CharacterBioUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI bioText;
        [SerializeField] private GameObject bioPanel;
        [SerializeField] private Button toggleButton;

        private bool isExpanded;
        private GameManager gameManager;

        private void Start()
        {
            gameManager = GameManager.Instance;

            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(ToggleBio);
            }

            if (gameManager != null)
            {
                gameManager.OnLevelStarted += OnLevelStarted;
            }

            // Start collapsed
            if (bioPanel != null)
                bioPanel.SetActive(false);
        }

        private void OnLevelStarted(int levelId)
        {
            var levelData = gameManager.CurrentLevelData;
            if (levelData?.characters == null || levelData.characters.Count == 0) return;

            var charData = levelData.characters[0];
            var profile = charData.profile;

            if (nameText != null)
                nameText.text = charData.name ?? "Explorer";

            if (bioText != null && profile != null)
                bioText.text = profile.bio ?? "";

            // Check if bio should be visible
            if (levelData.hints != null && levelData.hints.bio_visible)
            {
                gameObject.SetActive(true);
            }
        }

        private void ToggleBio()
        {
            isExpanded = !isExpanded;
            if (bioPanel != null)
                bioPanel.SetActive(isExpanded);
        }

        public void SetCharacterInfo(string name, string bio)
        {
            if (nameText != null) nameText.text = name;
            if (bioText != null) bioText.text = bio;
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnLevelStarted -= OnLevelStarted;
            }
        }
    }
}
