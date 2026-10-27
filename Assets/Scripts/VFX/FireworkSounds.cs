using System.Collections.Generic;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// The sounds of a firework (Firework_Win): a whistle where each rocket goes up, a bang where it bursts. It watches
    /// the Rockets particle system (a rocket that's gone has burst), so the sounds always match the show. They play
    /// while the game is stopped too (a puzzle's win). Tools > 429 Game > VFX > Build Win Fireworks puts it on.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireworkSounds : MonoBehaviour
    {
        [Tooltip("The rockets: one sound as each appears, another as each bursts.")]
        [SerializeField] private ParticleSystem rockets;
        [SerializeField] private AudioClip launchSound;
        [SerializeField] private AudioClip[] bangSounds = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;
        [Tooltip("How far away they can still be heard, in metres.")]
        [Min(1f)]
        [SerializeField] private float hearing = 30f;

        private const int Voices = 6;
        private readonly List<AudioSource> voices = new();
        private int nextVoice;
        private ParticleSystem.Particle[] particles = new ParticleSystem.Particle[16];
        private readonly Dictionary<uint, Vector3> flying = new();
        private readonly HashSet<uint> seen = new();
        private readonly List<uint> gone = new();

        private void Awake()
        {
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Firework Voice");
                go.transform.SetParent(transform, false);
                var voice = go.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.minDistance = 1.5f;
                voice.maxDistance = hearing;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.dopplerLevel = 0f;
                // A win while the game is stopped (the picture puzzle) still has its bangs.
                voice.ignoreListenerPause = true;
                voices.Add(voice);
            }
        }

        private void LateUpdate()
        {
            if (rockets == null) return;
            if (particles.Length < rockets.main.maxParticles) particles = new ParticleSystem.Particle[rockets.main.maxParticles];
            int count = rockets.GetParticles(particles);
            seen.Clear();
            bool world = rockets.main.simulationSpace == ParticleSystemSimulationSpace.World;
            for (int i = 0; i < count; i++)
            {
                uint id = particles[i].randomSeed;
                var at = world ? particles[i].position : rockets.transform.TransformPoint(particles[i].position);
                seen.Add(id);
                if (!flying.ContainsKey(id)) Say(launchSound, at, 0.55f);
                flying[id] = at;
            }
            gone.Clear();
            foreach (var pair in flying)
                if (!seen.Contains(pair.Key)) gone.Add(pair.Key);
            foreach (var id in gone)
            {
                if (bangSounds.Length > 0) Say(bangSounds[Random.Range(0, bangSounds.Length)], flying[id], 1f);
                flying.Remove(id);
            }
        }

        private void Say(AudioClip clip, Vector3 at, float loudness)
        {
            if (clip == null || voices.Count == 0) return;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            voice.transform.position = at;
            voice.pitch = Random.Range(0.9f, 1.12f);
            voice.PlayOneShot(clip, loudness * volume);
        }
    }
}
