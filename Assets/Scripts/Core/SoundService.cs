using System.Collections;
using System.Collections.Generic;
using FGJ2026.Simulation;
using UnityEngine;

namespace FGJ2026
{
    [DisallowMultipleComponent]
    public sealed class SoundService : MonoBehaviour, IService
    {
        private const string ResourceFolder = "AnimalSounds";
        private const float MusicVolume = 0.35f;
        private const float MusicFadeSeconds = 0.75f;

        private readonly Dictionary<AnimalColor, AnimalSoundProfile> profiles = new Dictionary<AnimalColor, AnimalSoundProfile>();
        private Soundtrack soundtrack;
        private AudioSource musicSourceA, musicSourceB;
        private MusicTrack? currentMusic;
        private Coroutine musicFade;

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

            soundtrack = Resources.Load<Soundtrack>(nameof(Soundtrack));
            IsInitialized = true;
        }

        public void DeInitialize()
        {
            StopMusicFade();
            StopMusicSource(musicSourceA);
            StopMusicSource(musicSourceB);
            currentMusic = null;
            soundtrack = null;
            profiles.Clear();
            IsInitialized = false;
        }

        internal AudioClip PickAnimalClip(AnimalColor color, AnimalSoundCue cue) =>
            IsInitialized && profiles.TryGetValue(color, out AnimalSoundProfile profile) ? profile.PickClip(cue) : null;

        public void PlayMusic(MusicTrack track)
        {
            if (!IsInitialized || currentMusic == track)
                return;

            AudioClip clip = soundtrack != null ? soundtrack.GetClip(track) : null;
            if (clip == null)
            {
                Debug.LogWarning($"No music clip is assigned for '{track}' in the Soundtrack asset.", this);
                return;
            }

            if (musicSourceA == null)
            {
                musicSourceA = CreateMusicSource();
                musicSourceB = CreateMusicSource();
            }

            StopMusicFade();
            AudioSource incoming;
            if (musicSourceA.clip == clip)
                incoming = musicSourceA;
            else if (musicSourceB.clip == clip)
                incoming = musicSourceB;
            else
                incoming = musicSourceA.volume <= musicSourceB.volume ? musicSourceA : musicSourceB;

            AudioSource outgoing = incoming == musicSourceA ? musicSourceB : musicSourceA;
            if (incoming.clip != clip || !incoming.isPlaying)
            {
                StopMusicSource(incoming);
                incoming.clip = clip;
                incoming.Play();
            }

            currentMusic = track;
            if (outgoing.isPlaying)
                musicFade = StartCoroutine(CrossfadeMusic(outgoing, incoming));
            else
                incoming.volume = MusicVolume;
        }

        private AudioSource CreateMusicSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private IEnumerator CrossfadeMusic(AudioSource outgoing, AudioSource incoming)
        {
            float outgoingVolume = outgoing.volume;
            float incomingVolume = incoming.volume;
            float elapsed = 0f;
            while (elapsed < MusicFadeSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / MusicFadeSeconds);
                outgoing.volume = Mathf.Lerp(outgoingVolume, 0f, progress);
                incoming.volume = Mathf.Lerp(incomingVolume, MusicVolume, progress);
            }

            StopMusicSource(outgoing);
            musicFade = null;
        }

        private void StopMusicFade()
        {
            if (musicFade == null)
                return;

            StopCoroutine(musicFade);
            musicFade = null;
        }

        private static void StopMusicSource(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }
    }
}
