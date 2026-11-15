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

        public bool TryPlayAnimalSound(AudioSource source, AnimalColor color, AnimalSoundCue cue)
        {
            if (!IsInitialized || source == null || source.isPlaying || !profiles.TryGetValue(color, out AnimalSoundProfile profile))
                return false;

            AudioClip clip = profile.PickClip(cue);
            if (clip == null)
                return false;

            source.clip = clip;
            source.Play();
            return true;
        }
    }
}
