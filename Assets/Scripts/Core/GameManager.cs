using UnityEngine;
using UnityEngine.SceneManagement;

namespace FGJ2026
{
    /// <summary>
    /// Lives for the whole session. Owns the level list and the player's progress and switches scenes.
    /// It is created on demand, so any scene (e.g. MainGame) can be started directly in the Editor.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public const string MenuScene = "StarterMenu";
        public const string LevelSelectScene = "LevelSelect";
        public const string PuzzleScene = "MainGame";

        private static GameManager instance;
        private static bool quitting;

        public static GameManager Instance
        {
            get
            {
                if (instance == null && !quitting)
                    new GameObject(nameof(GameManager)).AddComponent<GameManager>();
                return instance;
            }
        }

        public LevelService Levels { get; private set; }
        public ProgressService Progress { get; private set; }

        /// <summary>Index into <see cref="LevelService.Levels"/> of the puzzle to play or show.</summary>
        public int SelectedLevelIndex { get; set; }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            Levels = new LevelService();
            Progress = new ProgressService();
            ServiceLocator.Instance.Add(Levels);
            ServiceLocator.Instance.Add(Progress);
            ServiceLocator.Instance.InitializeAll();
        }

        private void OnApplicationQuit() => quitting = true;

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public void OpenMenu() => SceneManager.LoadScene(MenuScene);

        public void OpenLevelSelect() => SceneManager.LoadScene(LevelSelectScene);

        public void OpenLevel(int index)
        {
            SelectedLevelIndex = index;
            SceneManager.LoadScene(PuzzleScene);
        }
    }
}
