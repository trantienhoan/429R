using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Fight music for an enemy manager (on -EnemyManager 4). When the manager turns on, the pumpkin music player that's
    /// playing is destroyed (the next stage makes its own) and a fight song fades in: the next of Songs each time, so
    /// a second fight sounds new. When the manager turns off (its fight is won, or the game is over), the song fades out.
    /// Tools > 429 Game > Enemies > Set Up EnemyManager 4 Bee Swarm And Music puts it on.
    /// </summary>
    [DisallowMultipleComponent]
    public class FightMusic : MonoBehaviour
    {
        [Tooltip("Played in turn, one each time the manager turns on.")]
        [SerializeField] private AudioClip[] songs = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;
        [Tooltip("Seconds to fade in and out.")]
        [Min(0f)]
        [SerializeField] private float fadeSeconds = 1.5f;
        [Tooltip("Music players are the objects with an Audio Source whose name has this in it (broken pieces, \"drop\", " +
                 "aren't). They're destroyed when the manager turns on. Empty: none are.")]
        [SerializeField] private string musicPlayerName = "music_player";

        private Player player;
        private int nextSong;

        private void OnEnable()
        {
            DestroyMusicPlayers();
            if (songs == null || songs.Length == 0) return;
            var song = songs[nextSong++ % songs.Length];
            if (song == null) return;
            player = new GameObject("Fight Music").AddComponent<Player>();
            player.Play(song, volume, fadeSeconds);
        }

        private void OnDisable()
        {
            // The song lives outside the manager, so it can fade out after the manager is off.
            if (player != null) player.FadeOut(fadeSeconds);
            player = null;
        }

        private void DestroyMusicPlayers()
        {
            if (string.IsNullOrEmpty(musicPlayerName)) return;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // The music player is the object (or a parent of it) named like one.
                for (var t = source.transform; t != null; t = t.parent)
                {
                    if (!Named(t.name, musicPlayerName)) continue;
                    if (!Named(t.name, "drop")) Destroy(t.gameObject);
                    break;
                }
            }
        }

        private static bool Named(string name, string part) => name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The song's own speaker: fades between songs, and away once it's told to.</summary>
        private class Player : MonoBehaviour
        {
            private AudioSource source;
            private AudioClip nextClip;
            private float volume;
            private float fadeSpeed = 1f;
            private bool leaving;

            private void Awake()
            {
                source = gameObject.AddComponent<AudioSource>();
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 0;
                source.volume = 0f;
            }

            /// <summary>Fades the song in (out of the one playing first, if any).</summary>
            public void Play(AudioClip clip, float toVolume, float seconds)
            {
                volume = toVolume;
                fadeSpeed = seconds > 0f ? Mathf.Max(toVolume, 0.01f) / seconds : float.MaxValue;
                if (source.isPlaying && source.clip != clip) nextClip = clip;
                else if (!source.isPlaying)
                {
                    source.clip = clip;
                    source.Play();
                }
            }

            public void FadeOut(float seconds)
            {
                leaving = true;
                nextClip = null;
                fadeSpeed = seconds > 0f ? Mathf.Max(source.volume, 0.01f) / seconds : float.MaxValue;
            }

            // Unscaled time: a fade still finishes while the game is held (a notice) underneath it.
            private void Update()
            {
                float target = leaving || nextClip != null ? 0f : volume;
                source.volume = Mathf.MoveTowards(source.volume, target, fadeSpeed * Time.unscaledDeltaTime);
                if (source.volume > 0f) return;
                if (leaving)
                {
                    Destroy(gameObject);
                }
                else if (nextClip != null)
                {
                    source.clip = nextClip;
                    nextClip = null;
                    source.Play();
                }
            }
        }
    }
}
