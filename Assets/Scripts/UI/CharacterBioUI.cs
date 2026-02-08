using System.Collections;
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

        private bool isExpanded = false;
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

            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(ToggleBio);
            }

            gameManager.OnLevelStarted += OnLevelStarted;
            subscribed = true;

            // Start collapsed
            if (bioPanel != null)
                bioPanel.SetActive(false);

            // If a level is already loaded, populate immediately
            if (gameManager.CurrentState == GameState.Playing)
            {
                PopulateBio();
            }
        }

        private void OnLevelStarted(int levelId)
        {
            PopulateBio();
        }

        private void PopulateBio()
        {
            var levelData = gameManager.CurrentLevelData;
            if (levelData?.characters == null || levelData.characters.Count == 0) return;

            var charData = levelData.characters[0];
            var profile = charData.profile;

            if (nameText != null)
                nameText.text = charData.name ?? "Explorer";

            if (bioText != null && profile != null)
                bioText.text = profile.bio ?? "";

            // Make sure the whole UI is visible (but keep bio panel collapsed)
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
            if (subscribed && gameManager != null)
            {
                gameManager.OnLevelStarted -= OnLevelStarted;
            }
        }
    }
}
