using System;
using System.Collections.Generic;
using FGJ2026.Simulation;
using UnityEngine;

namespace FGJ2026
{
    /// <summary>Loads every level JSON in Resources/Levels, sorted by file name (01_..., 02_...).</summary>
    public sealed class LevelService : IService
    {
        public const string ResourceFolder = "Levels";

        private readonly List<LevelDefinition> levels = new List<LevelDefinition>();

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
            TextAsset[] assets = Resources.LoadAll<TextAsset>(ResourceFolder);
            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));

            foreach (TextAsset asset in assets)
            {
                if (LevelDefinition.TryParse(asset.text, asset.name, out LevelDefinition level, out string error))
                    levels.Add(level);
                else
                    Debug.LogError($"Level '{asset.name}' could not be loaded: {error}", asset);
            }

            if (levels.Count == 0)
                Debug.LogError($"No levels found in Resources/{ResourceFolder}.");
        }

        public LevelDefinition Get(int index) => index >= 0 && index < levels.Count ? levels[index] : null;
    }
}
