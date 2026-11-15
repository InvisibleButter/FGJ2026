using System.Collections.Generic;
using FGJ2026.Simulation;
using UnityEngine;

namespace FGJ2026
{
    /// <summary>
    /// Loads every level JSON in Resources/Levels, in the order given by Resources/LevelOrder.json
    /// (see <see cref="LevelLibrary"/>).
    /// </summary>
    public sealed class LevelService : IService
    {
        public const string ResourceFolder = "Levels";

        private readonly List<LevelDefinition> levels = new List<LevelDefinition>();
        private readonly Dictionary<string, string> fileNames = new Dictionary<string, string>();

        public IReadOnlyList<LevelDefinition> Levels => levels;
        public int Count => levels.Count;
        public bool IsInitialized { get; set; }

        public void Initialize()
        {
            Reload();
            IsInitialized = true;
        }

        public void DeInitialize() => IsInitialized = false;

        public void Reload()
        {
            levels.Clear();
            fileNames.Clear();

            foreach (TextAsset asset in Resources.LoadAll<TextAsset>(ResourceFolder))
            {
                if (!LevelDefinition.TryParse(asset.text, asset.name, out LevelDefinition level, out string error))
                {
                    Debug.LogError($"Level '{asset.name}' could not be loaded: {error}", asset);
                    continue;
                }
                if (fileNames.ContainsKey(level.Id))
                {
                    Debug.LogError($"Level '{asset.name}' uses the id '{level.Id}' of '{fileNames[level.Id]}' and is skipped.", asset);
                    continue;
                }
                levels.Add(level);
                fileNames[level.Id] = asset.name;
            }

            List<string> order = LevelLibrary.LoadOrder();
            levels.Sort((a, b) =>
            {
                int ia = order.IndexOf(a.Id), ib = order.IndexOf(b.Id);
                if (ia < 0) ia = int.MaxValue;
                if (ib < 0) ib = int.MaxValue;
                return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(fileNames[a.Id], fileNames[b.Id]);
            });

            if (levels.Count == 0)
                Debug.LogError($"No levels found in Resources/{ResourceFolder}.");
        }

        public LevelDefinition Get(int index) => index >= 0 && index < levels.Count ? levels[index] : null;

        public int IndexOf(string id) => levels.FindIndex(l => l.Id == id);

        /// <summary>The level's file name in Resources/Levels (without .json), or null.</summary>
        public string FileNameOf(string id) => id != null && fileNames.TryGetValue(id, out string name) ? name : null;

        /// <summary>The level's JSON text as stored in the project.</summary>
        public string LoadJson(string id)
        {
            string name = FileNameOf(id);
            return name == null ? null : Resources.Load<TextAsset>($"{ResourceFolder}/{name}")?.text;
        }
    }
}
