using System;
using System.Collections.Generic;
using System.IO;
using FGJ2026.Simulation;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FGJ2026
{
    /// <summary>
    /// Where levels live and in which order the game plays them.
    /// Levels: Assets/Resources/Levels/*.json. Order: Assets/Resources/LevelOrder.json (a list of level ids;
    /// levels missing from it come last, by file name).
    /// </summary>
    public static class LevelLibrary
    {
        public const string LevelsFolder = "Assets/Resources/Levels";
        public const string OrderAssetPath = "Assets/Resources/LevelOrder.json";
        private const string OrderResource = "LevelOrder";

        [Serializable]
        public sealed class OrderFile
        {
            public List<string> levels = new List<string>();
        }

        public static List<string> LoadOrder()
        {
            var asset = Resources.Load<TextAsset>(OrderResource);
            return asset == null ? new List<string>() : ParseOrder(asset.text);
        }

        /// <summary>Levels can only be written into the project from the Unity Editor.</summary>
        public static bool CanSave => Application.isEditor;

        /// <summary>
        /// Writes a level file and puts it at <paramref name="position"/> (0-based) in the level order.
        /// <paramref name="existingFileName"/> overwrites that file (without .json); null creates "&lt;id&gt;.json".
        /// </summary>
        public static bool Save(LevelFile data, string existingFileName, int position, out string savedPath, out string error)
        {
#if UNITY_EDITOR
            try
            {
                string fileName = string.IsNullOrEmpty(existingFileName) ? data.id : existingFileName;
                savedPath = $"{LevelsFolder}/{fileName}.json";
                Directory.CreateDirectory(LevelsFolder);
                File.WriteAllText(savedPath, JsonUtility.ToJson(data, true));

                List<string> order = File.Exists(OrderAssetPath) ? ParseOrder(File.ReadAllText(OrderAssetPath)) : new List<string>();
                order.Remove(data.id);
                order.Insert(Mathf.Clamp(position, 0, order.Count), data.id);
                File.WriteAllText(OrderAssetPath, JsonUtility.ToJson(new OrderFile { levels = order }, true));

                AssetDatabase.ImportAsset(savedPath);
                AssetDatabase.ImportAsset(OrderAssetPath);
                error = null;
                return true;
            }
            catch (IOException exception)
            {
                savedPath = null;
                error = $"Saving failed: {exception.Message}";
                return false;
            }
#else
            savedPath = null;
            error = "Levels can only be saved in the Unity Editor.";
            return false;
#endif
        }

        private static List<string> ParseOrder(string json)
        {
            try
            {
                return JsonUtility.FromJson<OrderFile>(json)?.levels ?? new List<string>();
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("LevelOrder.json could not be read; levels are sorted by file name.");
                return new List<string>();
            }
        }
    }
}
