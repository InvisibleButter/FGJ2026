using System;
using FGJ2026.Simulation;
using UnityEngine;

namespace FGJ2026
{
    public enum AnimalSoundCue { Sing, Plopp, Sad, Happy, Eat, Quak }

    [CreateAssetMenu(menuName = "FGJ2026/Animal Sound Profile", fileName = "AnimalSoundProfile")]
    public sealed class AnimalSoundProfile : ScriptableObject
    {
        [SerializeField] private AnimalColor color;
        [SerializeField] private AudioClip[] sing = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip plopp;
        [SerializeField] private AudioClip[] sad = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip happy;
        [SerializeField] private AudioClip[] eat = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip quak;

        public AnimalColor Color => color;

        internal AudioClip PickClip(AnimalSoundCue cue) => cue switch
        {
            AnimalSoundCue.Sing => PickVariant(sing),
            AnimalSoundCue.Plopp => plopp,
            AnimalSoundCue.Sad => PickVariant(sad),
            AnimalSoundCue.Happy => happy,
            AnimalSoundCue.Eat => PickVariant(eat),
            AnimalSoundCue.Quak => quak,
            _ => null
        };

        private static AudioClip PickVariant(AudioClip[] clips)
        {
            AudioClip selected = null;
            int count = 0;
            foreach (AudioClip clip in clips)
            {
                if (clip != null && UnityEngine.Random.Range(0, ++count) == 0)
                    selected = clip;
            }
            return selected;
        }
    }
}
