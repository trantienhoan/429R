using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Plays a Music Playlist, one song after another, while it's on: it stops when switched off or destroyed (a radio
    /// breaking, a fight's EnemyManager switched off). It plays on its own speaker (a child), so FSM Audio Play / Audio
    /// Stop actions on this object (hit sounds) can't change the music.
    /// - Punch To Skip (the radios): a punch or a tap with a fist, or a flying punch, skips to the next song.
    /// - Done Bool (the fights): once that FSM bool is true (the mini boss is down), no new song starts; the one
    ///   playing finishes.
    /// Tools > 429 Game > Audio > Set Up Music Playlists puts them on the radios and the EnemyManagers.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaylistPlayer : MonoBehaviour
    {
        [SerializeField] private MusicPlaylist playlist;
        [Tooltip("0: the same everywhere, like background music; 1: heard from here (3D).")]
        [Range(0f, 1f)]
        [SerializeField] private float spatialBlend;
        [Tooltip("A punch or a tap with a fist (or a flying punch) skips to the next song: the radios.")]
        [SerializeField] private bool punchToSkip;
        [Tooltip("At most one skip this often, in seconds.")]
        [Min(0f)]
        [SerializeField] private float skipCooldown = 1f;
        [Tooltip("Once this bool in the FSM named Done Fsm (on this object) is true, no new song starts (e.g. " +
                 "WhiteSpiderDown: the fight is won). Empty: songs go on for as long as this is on.")]
        [SerializeField] private string doneBool;
        [SerializeField] private string doneFsm = "EnemyManager";

        private AudioSource speaker;
        private HutongGames.PlayMaker.FsmBool done;
        private int current = -1;
        private float nextSkip;
        private bool playing;

        public MusicPlaylist Playlist
        {
            get => playlist;
            set => playlist = value;
        }

        /// <summary>Starts the playlist from its first song (a random one when shuffled).</summary>
        public void Play()
        {
            current = -1;
            playing = true;
            Next();
        }

        /// <summary>Ends the song playing and starts the next one, with the playlist's Skip Sound.</summary>
        public void Skip()
        {
            if (!playing) return;
            if (playlist != null && playlist.skipSound != null)
                AudioSource.PlayClipAtPoint(playlist.skipSound, transform.position, playlist.skipVolume);
            Next();
        }

        public void Stop()
        {
            playing = false;
            if (speaker != null) speaker.Stop();
        }

        private void OnEnable()
        {
            if (playlist != null) Play();
        }

        private void OnDisable() => Stop();

        private void Update()
        {
            if (!playing || speaker == null || speaker.isPlaying) return;
            // Paused mid-song (time kept) isn't the end of it.
            if (speaker.clip != null && speaker.time > 0f && speaker.time < speaker.clip.length - 0.05f) return;
            if (Done()) { playing = false; return; }
            Next();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!punchToSkip || Time.time < nextSkip) return;
            var hitter = collision.rigidbody;
            if (!Game.Combat.Fist.IsFistBall(hitter) && !Game.Combat.Fist.IsFlyingFist(hitter)) return;
            nextSkip = Time.time + skipCooldown;
            Skip();
        }

        private void Next()
        {
            var songs = playlist != null ? playlist.songs : null;
            if (songs == null || songs.Length == 0) { Stop(); return; }
            if (speaker == null) speaker = MakeSpeaker();
            int count = songs.Length;
            if (playlist.shuffle && count > 1)
            {
                int pick = Random.Range(0, count - 1);
                current = current >= 0 && pick >= current ? pick + 1 : pick;
            }
            else current = (current + 1) % count;
            // A missing song (taken out of the project) is skipped.
            for (int tries = 0; tries < count && songs[current] == null; tries++) current = (current + 1) % count;
            if (songs[current] == null) { Stop(); return; }
            speaker.clip = songs[current];
            speaker.volume = playlist.volume;
            speaker.spatialBlend = spatialBlend;
            speaker.Play();
        }

        private AudioSource MakeSpeaker()
        {
            var child = new GameObject("Playlist Speaker");
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.priority = 32;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 3f;
            source.maxDistance = 40f;
            return source;
        }

        // The fight is won: Done Bool in Done Fsm is true.
        private bool Done()
        {
            if (string.IsNullOrEmpty(doneBool)) return false;
            if (done == null)
            {
                foreach (var fsm in GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == doneFsm) done = fsm.FsmVariables.FindFsmBool(doneBool);
                }
            }
            return done != null && done.Value;
        }
    }
}
