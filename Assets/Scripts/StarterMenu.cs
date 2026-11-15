using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FGJ2026
{
    /// <summary>Main menu: Start opens the puzzle overview, Credits toggles the credits panel, Quit exits.</summary>
    public sealed class StarterMenu : MonoBehaviour
    {
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button backButton;

        public void StartGame() => GameManager.Instance.OpenLevelSelect();

        public void ShowCredits()
        {
            menuPanel.SetActive(false);
            creditsPanel.SetActive(true);
            eventSystem.SetSelectedGameObject(backButton.gameObject);
        }

        public void CloseCredits()
        {
            creditsPanel.SetActive(false);
            menuPanel.SetActive(true);
            eventSystem.SetSelectedGameObject(creditsButton.gameObject);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
