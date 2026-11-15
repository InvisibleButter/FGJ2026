using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FGJ2026
{
    /// <summary>Remembers which stars the player has earned per level. Stars are kept forever once earned.</summary>
    public sealed class ProgressService : IService
    {
        private const string FileName = "progress.json";

        [Serializable]
        public sealed class LevelProgress
        {
            public string id;
            public bool[] stars = Array.Empty<bool>();
        }

        [Serializable]
        public sealed class ProgressFile
        {
            public List<LevelProgress> levels = new List<LevelProgress>();
        }

        private ProgressFile data = new ProgressFile();

        public bool IsInitialized { get; set; }

        public void Initialize()
        {
            Load();
            IsInitialized = true;
        }

        public void DeInitialize() => IsInitialized = false;

        public void Load()
        {
            data = new ProgressFile();
            if (!FileManager.LoadFromFile(FileName, out string json) || string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                data = JsonUtility.FromJson<ProgressFile>(json) ?? new ProgressFile();
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"Progress file could not be read and was reset: {exception.Message}");
                data = new ProgressFile();
            }
        }

        public void Save() => FileManager.WriteToFile(FileName, JsonUtility.ToJson(data, true));

        public bool HasStar(string levelId, int index)
        {
            LevelProgress entry = Find(levelId);
            return entry != null && index >= 0 && index < entry.stars.Length && entry.stars[index];
        }

        public int CountStars(string levelId) => Find(levelId)?.stars.Count(s => s) ?? 0;

        /// <summary>Adds the stars earned in a run. Returns which of them are new.</summary>
        public bool[] Record(string levelId, bool[] earned)
        {
            LevelProgress entry = Find(levelId);
            if (entry == null)
            {
                entry = new LevelProgress { id = levelId };
                data.levels.Add(entry);
            }
            if (entry.stars.Length < earned.Length)
                Array.Resize(ref entry.stars, earned.Length);

            var isNew = new bool[earned.Length];
            for (int i = 0; i < earned.Length; i++)
            {
                if (earned[i] && !entry.stars[i])
                {
                    entry.stars[i] = true;
                    isNew[i] = true;
                }
            }

            Save();
            return isNew;
        }

        public void ResetAll()
        {
            data = new ProgressFile();
            Save();
        }

        private LevelProgress Find(string levelId) => data.levels.FirstOrDefault(l => l.id == levelId);
    }
}
