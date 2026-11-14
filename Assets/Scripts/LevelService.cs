using UnityEngine;
using UnityEngine.SceneManagement;

namespace FGJ2026
{
    public sealed class LevelService : MonoBehaviour, IService
    {
        [SerializeField] private TextAsset[] levels;

        public int CurrentLevelIndex { get; private set; } = -1;
        public PuzzleMap CurrentMap { get; private set; }

        public bool LoadNextLevel()
        {
            CurrentLevelIndex++;
            if (CurrentLevelIndex >= levels.Length)
            {
                ReturnToMenu();
                return false;
            }
            
            var mapData = levels[CurrentLevelIndex];
            if (!PuzzleMap.TryParse(mapData.text, out PuzzleMap map, out string error))
            {
                Debug.LogError($"Could not load level {CurrentLevelIndex} ({mapData.name}): {error}", mapData);
                return false;
            }

            CurrentMap = map;
            ServiceLocator.Instance.GetService<GridService>().LoadMap(CurrentMap);
            return true;
        }
        
        internal void ReturnToMenu()
        {
            CurrentLevelIndex = -1;
            CurrentMap = null;
            SceneManager.LoadSceneAsync("StarterMenu");
        }

        public void Initialize()
        {
            IsInitialized = true;
        }

        public void DeInitialize()
        {
            IsInitialized = false;
        }

        public bool IsInitialized { get; set; }
    }
}
