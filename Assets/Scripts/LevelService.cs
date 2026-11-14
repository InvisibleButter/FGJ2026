using UnityEngine;

namespace FGJ2026
{
    public sealed class LevelService
    {
        private readonly TextAsset[] levels;
        private readonly GridService grid;

        internal LevelService(TextAsset[] levels, int startingLevelIndex, GridService grid)
        {
            this.levels = levels;
            this.grid = grid;
            StartingLevelIndex = startingLevelIndex;
        }

        public int StartingLevelIndex { get; }
        public int CurrentLevelIndex { get; private set; } = -1;
        public int LevelCount => levels.Length;
        public PuzzleMap CurrentMap => grid.Map;

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

            grid.LoadMap(map);
            CurrentLevelIndex = index;
            return true;
        }
    }
}
