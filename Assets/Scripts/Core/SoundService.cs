using System.Collections.Generic;
using FGJ2026.Simulation;
using UnityEngine;

namespace FGJ2026
{
    [DisallowMultipleComponent]
    public sealed class SoundService : MonoBehaviour, IService
    {
        private const string ResourceFolder = "AnimalSounds";

        private readonly Dictionary<AnimalColor, AnimalSoundProfile> profiles = new Dictionary<AnimalColor, AnimalSoundProfile>();

        public bool IsInitialized { get; set; }

        public void Initialize()
        {
            profiles.Clear();
            foreach (AnimalSoundProfile profile in Resources.LoadAll<AnimalSoundProfile>(ResourceFolder))
            {
                if (profiles.TryGetValue(profile.Color, out AnimalSoundProfile existing))
                {
                    Debug.LogWarning($"Animal sound profile '{profile.name}' duplicates the color of '{existing.name}' and is skipped.", profile);
                    continue;
                }
                profiles.Add(profile.Color, profile);
            }

            IsInitialized = true;
        }

        public void DeInitialize()
        {
            profiles.Clear();
            IsInitialized = false;
        }

        internal AudioClip PickAnimalClip(AnimalColor color, AnimalSoundCue cue) =>
            IsInitialized && profiles.TryGetValue(color, out AnimalSoundProfile profile) ? profile.PickClip(cue) : null;
    }
}
