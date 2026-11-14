using UnityEngine;

namespace FGJ2026
{
    public sealed class LevelService : IService
    {
        private readonly TextAsset[] levels;
        private GridService _gridService;

        internal LevelService(TextAsset[] levels, int startingLevelIndex)
        {
            this.levels = levels;
           
            _gridService = ServiceLocator.Instance.GetService<GridService>();
            StartingLevelIndex = startingLevelIndex;
        }

        public int StartingLevelIndex { get; }
        public int CurrentLevelIndex { get; private set; } = -1;
        public int LevelCount => levels.Length;
       
        internal bool TryLoadLevel(int index)
        {
            if (index < 0 || index >= LevelCount || levels[index] == null)
            {
                Debug.LogError($"The GameManager has no map assigned at level index {index}.");
                return false;
            }

            TextAsset asset = levels[index];
            if (!PuzzleMap.TryParse(asset.text, out PuzzleMap map, out string error))
            {
                Debug.LogError($"Could not load level {index + 1} ({asset.name}): {error}", asset);
                return false;
            }

            _gridService.LoadMap(map);
            CurrentLevelIndex = index;
            return true;
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
