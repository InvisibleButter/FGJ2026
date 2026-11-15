using FGJ2026.Simulation;
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
        public const string LevelEditorScene = "LevelEditor";

        private static GameManager instance;
        private static bool quitting;

        /// <summary>The project enters Play mode without a domain reload, so static state must be reset by hand.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            quitting = false;
            ServiceLocator.Reset();
        }

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
        public SoundService Sound { get; private set; }

        /// <summary>Index into <see cref="LevelService.Levels"/> of the puzzle to play or show.</summary>
        public int SelectedLevelIndex { get; set; }

        /// <summary>Set while a level from the level editor is being test-played.</summary>
        public TestRun CurrentTest { get; private set; }

        /// <summary>The level editor's state, kept across test runs.</summary>
        public EditorSession EditorState { get; } = new EditorSession();

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
            if (!TryGetComponent(out SoundService sound))
                sound = gameObject.AddComponent<SoundService>();
            Sound = sound;

            ServiceLocator.Instance.Add(Levels);
            ServiceLocator.Instance.Add(Progress);
            ServiceLocator.Instance.Add(Sound);
            ServiceLocator.Instance.InitializeAll();
            SceneManager.sceneLoaded += UpdateSceneAudio;
            UpdateSceneAudio(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnApplicationQuit() => quitting = true;

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= UpdateSceneAudio;
            if (instance == this)
                instance = null;
        }

        private void UpdateSceneAudio(Scene scene, LoadSceneMode mode)
        {
            Camera camera = Camera.main;
            if (camera != null && FindFirstObjectByType<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            if (scene.name != PuzzleScene)
                Sound.PlayMusic(MusicTrack.Menu);
        }

        public void OpenMenu()
        {
            CurrentTest = null;
            SceneManager.LoadScene(MenuScene);
        }

        public void OpenLevelSelect()
        {
            CurrentTest = null;
            SceneManager.LoadScene(LevelSelectScene);
        }

        public void OpenLevel(int index)
        {
            CurrentTest = null;
            SelectedLevelIndex = index;
            SceneManager.LoadScene(PuzzleScene);
        }

        public void OpenLevelEditor()
        {
            CurrentTest = null;
            SceneManager.LoadScene(LevelEditorScene);
        }

        /// <summary>Plays a level from the level editor; leaving the puzzle returns to the editor.</summary>
        public void StartTestRun(LevelDefinition level, bool infiniteDays)
        {
            CurrentTest = new TestRun(level, infiniteDays);
            SceneManager.LoadScene(PuzzleScene);
        }
    }
}
