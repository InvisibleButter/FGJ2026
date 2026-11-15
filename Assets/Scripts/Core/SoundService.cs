using System.Collections.Generic;
using FGJ2026.Simulation;
using UnityEngine;

namespace FGJ2026
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class SoundService : MonoBehaviour, IService
    {
        private const string ResourceFolder = "AnimalSounds";

        private readonly Dictionary<AnimalColor, AnimalSoundProfile> profiles = new Dictionary<AnimalColor, AnimalSoundProfile>();
        private AudioSource source;
        private AudioSource idleSource;

        public bool IsInitialized { get; set; }
        public bool IsIdleSoundPlaying => idleSource != null && idleSource.isPlaying;

        public void Initialize()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            if (idleSource == null)
                idleSource = gameObject.AddComponent<AudioSource>();
            idleSource.playOnAwake = false;
            idleSource.loop = false;
            idleSource.spatialBlend = source.spatialBlend;
            idleSource.outputAudioMixerGroup = source.outputAudioMixerGroup;
            idleSource.volume = source.volume;

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
            StopAnimalSounds();
            profiles.Clear();
            IsInitialized = false;
        }

        public void PlayAnimalSound(AnimalColor color, AnimalSoundCue cue)
        {
            if (!IsInitialized || !profiles.TryGetValue(color, out AnimalSoundProfile profile))
                return;

            AudioClip clip = profile.PickClip(cue);
            if (clip != null)
                source.PlayOneShot(clip);
        }

        public void PlayIdleSound(AnimalColor color)
        {
            if (!IsInitialized || !profiles.TryGetValue(color, out AnimalSoundProfile profile))
                return;

            AudioClip clip = profile.PickClip(AnimalSoundCue.Quak);
            if (clip == null)
                return;

            idleSource.clip = clip;
            idleSource.Play();
        }

        public void StopIdleSound()
        {
            if (idleSource != null)
                idleSource.Stop();
        }

        public void StopAnimalSounds()
        {
            if (source != null)
                source.Stop();
            StopIdleSound();
        }
    }
}
