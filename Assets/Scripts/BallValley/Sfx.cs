using System.Collections.Generic;
using UnityEngine;

namespace Balls
{
    /// <summary>
    /// Sound effects (from 429R, in Resources/BALLS/Sfx). Plain 2D sounds from a small pool of voices, quieter the
    /// farther they happen from your ball (the camera hangs high over the table, so real 3D falloff would make
    /// everything equally faint). Each sound can't restart within a few hundredths of a second, so a pile-up of
    /// crashes doesn't turn into noise. Online, the host relays every sound it plays to the other players. The player's
    /// effects slider (SoundSettings: the speaker button, M) scales them all.
    /// 429R: no network relay and no BALLS sound settings; the clips come from the stage's own list (429R's Audio/SFX
    /// files); each voice is its own object placed where its sound happens (3D in the headset), its loudness still
    /// falling off with the distance from the player's head here.
    /// </summary>
    public static class Sfx
    {
        public enum Sound : byte
        {
            Charge, Launch, BallHit, SpecialHit, RockHit, WoodHit, Pocket, EatBerry, EatSlime, GainBall, Respawn,
            SlimeEmerge, SlimeHit, SlimeLaugh, RockBreak, TreeBreak, FireworkLaunch, FireworkBang, Dizzy,
            Ready, PlayerPocket, Shrink,   // 429R gate 3: the special move is charged; the player put a ball in a hole; the valley shrinks
        }

        // Clip variants for each sound (one is picked at random), and a base volume.
        static readonly (string[] clips, float volume)[] Table =
        {
            (new[] { "choo_choo" }, 0.45f),                                         // Charge (the Bowling Pumpkin's roll warning)
            (new[] { "giant_ball_rolling_shot" }, 0.8f),                            // Launch: the Bowling Pumpkin's roll (first 1.8 s, faded out)
            (new[] { "Bell_1", "Bell_2" }, 0.75f),                                   // BallHit: a clear bell, louder the harder (429R "Clear, resonant bell")
            (new[] { "fist_bump_punch" }, 0.55f),                                   // SpecialHit (was 1)
            (new[] { "rock_hit_1", "rock_hit_2", "rock_hit_3", "rock_hit_4" }, 0.9f), // RockHit
            (new[] { "wood_get_hit_1", "wood_get_hit_2" }, 0.9f),                   // WoodHit
            (new[] { "lowDown" }, 0.8f),                                            // Pocket (429R 2026-11-23: was the player's own fall whistle)
            (new[] { "om_nom_nom" }, 0.3f),                                         // EatBerry: a small, quick nom (higher pitch, see Pitch)
            (new[] { "om_nom_nom" }, 0.7f),                                         // EatSlime
            (new[] { "pluck_001", "pluck_002" }, 0.3f),                             // GainBall (429R 2026-11-23: was 429R's "levelup")
            (new[] { "revive_sound_1" }, 0.7f),                                     // Respawn
            (new[] { "Balloon_Pop" }, 0.5f),                                        // SlimeEmerge
            (new[] { "pumpkin_face_splat" }, 0.5f),                                 // SlimeHit
            (new[] { "cute_laugh" }, 0.45f),                                        // SlimeLaugh
            (new[] { "rock_pumpkin_break", "rock_pumpkin_break_2" }, 1f),           // RockBreak (429R)
            (new[] { "wood_furniture_break_1", "wood_furniture_break_2" }, 0.9f),   // TreeBreak (429R)
            (new[] { "firework_launch" }, 0.5f),                                    // FireworkLaunch (429R win fireworks)
            (new[] { "firework_bang_1", "firework_bang_2" }, 0.8f),                 // FireworkBang
            (new[] { "Cartoon_dizzy_stun1" }, 0.55f),                               // Dizzy: knocked out of control (429R's stun stars)
            (new[] { "highUp" }, 0.5f),                                             // Ready (429R gate 3: the fists' charge is full)
            (new[] { "ka_ching" }, 0.8f),                                           // PlayerPocket (429R gate 3: the player's own pocket pays candy)
            (new[] { "earth_rumble" }, 0.7f),                                       // Shrink (429R gate 3: the valley closes in a step)
        };

        public static float masterVolume = 0.85f;
        const int Voices = 20;
        const float MinGap = 0.05f;          // seconds before the same sound can play again
        const float BellGap = 0.12f;         // bells ring for 1.5 s: space them out a little more so a pile-up doesn't drone
        const float NomGap = 0.18f;          // berries go down in quick runs: one little nom per mouthful, not a stutter

        static float Gap(Sound s) => s == Sound.BallHit ? BellGap : s == Sound.EatBerry ? NomGap : s == Sound.FireworkLaunch || s == Sound.FireworkBang ? 0.02f : MinGap;

        /// <summary>Base pitch: the small-food nom is the slime nom sped up, so it sounds little and quick.</summary>
        static float Pitch(Sound s) => s == Sound.EatBerry ? 1.45f : 1f;
        const float FullVolumeDistance = 15f, SilentDistance = 85f;

        // 429R: no network relay (BALLS' Relay) and no BALLS sound settings (its effects slider).
        const float EffectsGain = 1f;

        /// <summary>429R: every clip name in the table (Set Up Ball Valley finds these in 429R's Audio/SFX).</summary>
        public static IEnumerable<string> ClipNames
        {
            get
            {
                foreach (var row in Table)
                    foreach (var name in row.clips) yield return name;
            }
        }

        static AudioClip[][] clips;
        static AudioSource[] voices;
        static float[] lastPlayed;
        static int next;
        static Transform home;

        /// <summary>Every sound has finished loading (the loading screen waits for this).</summary>
        public static bool Ready
        {
            get
            {
                if (clips == null) return false;
                foreach (var set in clips)
                foreach (var c in set)
                    if (c && c.loadState == AudioDataLoadState.Loading) return false;
                return true;
            }
        }

        /// <summary>Loads the clips and makes the voices (called once by ValleyGame).</summary>
        /// <param name="find">429R: a clip by its table name (the stage's own list); Resources/BALLS/Sfx when it has none.</param>
        public static void Init(Transform parent, System.Func<string, AudioClip> find = null)
        {
            home = new GameObject("Sfx").transform;
            home.SetParent(parent, false);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) voices[i] = NewSource();
            clips = new AudioClip[Table.Length][];
            lastPlayed = new float[Table.Length];
            for (int s = 0; s < Table.Length; s++)
            {
                lastPlayed[s] = -99f;
                var names = Table[s].clips;
                clips[s] = new AudioClip[names.Length];
                for (int k = 0; k < names.Length; k++)
                {
                    var c = find != null ? find(names[k]) : null;   // 429R
                    if (!c) c = Resources.Load<AudioClip>("BALLS/Sfx/" + names[k]);
                    if (c) c.LoadAudioData();   // ready before the first crash, not loaded on demand
                    else Debug.LogWarning("Sound Resources/BALLS/Sfx/" + names[k] + " not found.");
                    clips[s][k] = c;
                }
            }
        }

        static AudioSource NewSource()
        {
            // 429R: its own object, put where the sound happens: heard from that way in the headset (BALLS: 2D, a camera
            // high over the table). Unity's own fading is off (a far linear rolloff): Falloff sets the loudness, as in BALLS.
            var go = new GameObject("Voice");
            go.transform.SetParent(home, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 500f;
            src.maxDistance = 501f;
            src.dopplerLevel = 0f;
            return src;
        }

        /// <summary>429R: the stage is gone (its voices with it): everything here is forgotten.</summary>
        public static void Shutdown()
        {
            voices = null;
            clips = null;
            lastPlayed = null;
            home = null;
            next = 0;
        }

        /// <summary>Play a sound that happened at <paramref name="pos"/> (and, on the host, tell the other players).</summary>
        public static void Play(Sound s, Vector3 pos, float volume = 1f)
        {
            PlayHere(s, pos, volume);   // 429R: no relay
        }

        /// <summary>Play only on this computer (what a client does with the host's sounds).</summary>
        public static void PlayHere(Sound s, Vector3 pos, float volume = 1f)
        {
            if (voices == null) return;
            int i = (int)s;
            if (i < 0 || i >= clips.Length || Time.time - lastPlayed[i] < Gap(s)) return;
            var options = clips[i];
            var clip = options[Random.Range(0, options.Length)];
            if (!clip) return;
            float v = volume * Table[i].volume * masterVolume * EffectsGain * Falloff(pos);
            if (v < 0.02f) return;
            lastPlayed[i] = Time.time;
            var src = FreeVoice();
            src.transform.position = pos;   // 429R
            src.clip = clip;
            src.volume = Mathf.Clamp01(v);
            src.pitch = Pitch(s) * Random.Range(0.93f, 1.07f);
            src.loop = false;
            src.Play();
        }

        /// <summary>A sound that keeps playing until <see cref="Stop"/> (the special move's charge-up). Null if silent.</summary>
        public static AudioSource Begin(Sound s, Vector3 pos)
        {
            if (voices == null) return null;
            var options = clips[(int)s];
            var clip = options[Random.Range(0, options.Length)];
            if (!clip) return null;
            var src = NewSource();
            src.transform.position = pos;   // 429R
            src.clip = clip;
            src.pitch = 1f;
            src.volume = Mathf.Clamp01(Table[(int)s].volume * masterVolume * EffectsGain * Falloff(pos));
            src.Play();
            return src;
        }

        /// <summary>Keep a <see cref="Begin"/>ed sound at the right loudness as its source moves.</summary>
        public static void Follow(AudioSource src, Sound s, Vector3 pos)
        {
            if (!src) return;
            src.transform.position = pos;   // 429R
            src.volume = Mathf.Clamp01(Table[(int)s].volume * masterVolume * EffectsGain * Falloff(pos));
        }

        public static void Stop(AudioSource src)
        {
            if (src) Object.Destroy(src.gameObject);   // 429R: each voice has its own object
        }

        static AudioSource FreeVoice()
        {
            for (int k = 0; k < voices.Length; k++)
            {
                var v = voices[(next + k) % voices.Length];
                if (!v.isPlaying) { next = (next + k + 1) % voices.Length; return v; }
            }
            var oldest = voices[next];   // all busy: cut the oldest
            next = (next + 1) % voices.Length;
            return oldest;
        }

        /// <summary>1 near the player's head (429R; BALLS: near your ball, or where its camera looks), fading to 0 far away.</summary>
        static float Falloff(Vector3 pos)
        {
            var cam = Camera.main;
            if (!cam) return 1f;
            Vector3 ear = cam.transform.position;
            ear.y = 0f;
            pos.y = 0f;
            float d = Vector3.Distance(ear, pos);
            return 1f - Mathf.Clamp01((d - FullVolumeDistance) / (SilentDistance - FullVolumeDistance));
        }
    }
}
