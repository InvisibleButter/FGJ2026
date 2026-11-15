using System;
using System.IO;
using UnityEngine;

namespace FGJ2026
{
    /// <summary>Reads and writes text files in Application.persistentDataPath.</summary>
    public static class FileManager
    {
        public static bool WriteToFile(string fileName, string contents)
        {
            string fullPath = Path.Combine(Application.persistentDataPath, fileName);
            try
            {
                File.WriteAllText(fullPath, contents);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to write {fullPath}: {exception}");
                return false;
            }
        }

        /// <summary>Returns false (without an error) when the file doesn't exist yet.</summary>
        public static bool LoadFromFile(string fileName, out string result)
        {
            string fullPath = Path.Combine(Application.persistentDataPath, fileName);
            result = string.Empty;
            if (!File.Exists(fullPath))
                return false;

            try
            {
                result = File.ReadAllText(fullPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to read {fullPath}: {exception}");
                return false;
            }
        }
    }
}
