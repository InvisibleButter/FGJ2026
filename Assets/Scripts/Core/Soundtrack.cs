using UnityEngine;

namespace FGJ2026
{
    public enum MusicTrack { Menu, Planning, Simulation }

    public sealed class Soundtrack : ScriptableObject
    {
        [SerializeField] private AudioClip menu;
        [SerializeField] private AudioClip planning;
        [SerializeField] private AudioClip simulation;

        internal AudioClip GetClip(MusicTrack track) => track switch
        {
            MusicTrack.Menu => menu,
            MusicTrack.Planning => planning,
            MusicTrack.Simulation => simulation,
            _ => null
        };
    }
}
