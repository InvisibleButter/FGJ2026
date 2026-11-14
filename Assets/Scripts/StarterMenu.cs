using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace FGJ2026
{
    public sealed class StarterMenu : MonoBehaviour
    {
        private const string MainGameScene = "MainGame";

        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private UnityEngine.UI.Button creditsButton;
        [SerializeField] private UnityEngine.UI.Button backButton;

        private AsyncOperation gameLoad;

        public void StartGame()
        {
            StartGameAtLevel(0);
        }

        public void StartGameAtLevel(int levelIndex)
        {
            if (gameLoad != null)
                return;
            
            GameManager.Instance.SelectStartingLevel(levelIndex);
        }

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
