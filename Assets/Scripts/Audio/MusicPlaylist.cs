using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// A list of songs played one after another: a room radio's, or a fight's battle music (user, 2026-10-28: "a smarter
    /// way to choose and change the song"). Drag songs in or out, set how loud, in order or shuffled. Played by a
    /// Playlist Player (the radios, the EnemyManagers). Make one with Assets > Create > 429 Game > Music Playlist; the
    /// game's are in Assets/Data/Music.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Music Playlist", fileName = "New Music Playlist")]
    public class MusicPlaylist : ScriptableObject
    {
        [Tooltip("The songs, one after another. In order: from the top, then round again.")]
        public AudioClip[] songs = System.Array.Empty<AudioClip>();
        [Tooltip("How loud the songs play: the real loudness, 0 to 1.")]
        [Range(0f, 1f)]
        public float volume = 0.5f;
        [Tooltip("On: a random song each time, never the same one twice in a row. Off: in order from the top.")]
        public bool shuffle;
        [Tooltip("Played when a song is skipped (a radio punched or tapped with a fist). Empty: none.")]
        public AudioClip skipSound;
        [Range(0f, 1f)]
        public float skipVolume = 0.8f;
    }
}
