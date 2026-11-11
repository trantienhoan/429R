using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// The Fairy's lock, drawn as a purple tower of light instead of the old bone cage (user, 2026-11-11). The cage itself
    /// still does the locking: The_Cage keeps its trigger and its Start FSM, so the start pumpkin dropping on it (tag
    /// Monster) or the Fairy Shield still remove it exactly as before; it just isn't drawn any more. This shows it: while
    /// the cage is there, a seal glows on the floor, light bars circle the Fairy, a ring of light sits on top and sparks
    /// spiral up. Once the cage is gone, however it went, the tower lets go: it stops, the bars and the top ring fly up as
    /// they fade, a small purple burst plays with a seal-breaking sound, and it removes itself once that has faded
    /// (about a second and a half, the sound's length). It leaves the Fairy as it lets go (she destroys herself a moment
    /// later), so the release always plays out.
    /// No cage at the start, or a cage gone within a moment (the end-of-game Fairy's, which the Fairy Shield clears at
    /// once): no tower, no burst. Goes on Fairy_Lock_Tower, a child of the Fairy next to The_Cage; Tools > 429 Game >
    /// Fairy > Fairy Lock Tower builds it and puts it there.
    /// </summary>
    [DisallowMultipleComponent]
    public class FairyLockTower : MonoBehaviour
    {
        // The seal, bars and top ring live for good (100000 s) until the lock breaks; the sparks live a few seconds.
        private const float LivesForGood = 1000f;
        private const string DefaultCageName = "The_Cage";

        [Tooltip("The lock this shows: The_Cage. While it's there (and switched on) the tower plays; once it's gone the " +
                 "tower lets go. Empty = the Fairy's child called The_Cage.")]
        [SerializeField] private GameObject cage;
        [Tooltip("The tower's looping particles (everything under it). They don't play on their own: this starts them.")]
        [SerializeField] private Transform tower;
        [Tooltip("The burst played once when the lock breaks (everything under it). It doesn't play on its own.")]
        [SerializeField] private Transform release;
        [Tooltip("Parts of the tower that fly up as they fade when the lock breaks (the bars and the ring on top).")]
        [SerializeField] private ParticleSystem[] liftedParts;
        [Tooltip("What circles round the Fairy (the light bars). It turns round its own up axis.")]
        [SerializeField] private Transform turning;
        [Tooltip("Played when the lock breaks (a seal breaking, like the barrel seals'); not when it goes quietly. " +
                 "Empty = silent.")]
        [SerializeField] private AudioSource releaseSound;

        [Header("Look")]
        [Tooltip("How fast the bars circle round the Fairy (degrees a second).")]
        [SerializeField] private float turnSpeed = 25f;
        [Tooltip("How fast the bars and the top ring fly up as they fade when the lock breaks (m/s).")]
        [Min(0f)]
        [SerializeField] private float liftSpeed = 2.5f;

        [Header("Timing")]
        [Tooltip("The tower shows once the cage has been there this long (seconds). A cage gone sooner (the end-of-game " +
                 "Fairy's, which the Fairy Shield clears at once) goes quietly: no tower, no burst.")]
        [Min(0f)]
        [SerializeField] private float showAfter = 0.3f;
        [Tooltip("How long the tower takes to fade when the lock breaks (seconds).")]
        [Min(0.05f)]
        [SerializeField] private float fadeTime = 0.5f;

        private ParticleSystem[] towerSystems;
        private ParticleSystem[] releaseSystems;
        private ParticleSystem.Particle[] buffer;
        private Quaternion heading;
        private float bornAt;
        private bool shown;
        private bool released;

        private void Start()
        {
            bornAt = Time.time;
            heading = transform.rotation;
            if (cage == null && transform.parent != null)
            {
                var found = transform.parent.Find(DefaultCageName);
                if (found != null) cage = found.gameObject;
            }
            towerSystems = tower != null ? tower.GetComponentsInChildren<ParticleSystem>(true) : new ParticleSystem[0];
            releaseSystems = release != null ? release.GetComponentsInChildren<ParticleSystem>(true) : new ParticleSystem[0];
            // Already unlocked (no cage): nothing to show.
            if (!CageIsThere()) Destroy(gameObject);
        }

        private void Update()
        {
            if (shown && turning != null) turning.Rotate(0f, turnSpeed * Time.deltaTime, 0f, Space.Self);
            if (released) return;
            if (!CageIsThere())
            {
                if (shown) Release();
                else Destroy(gameObject);
                return;
            }
            if (!shown && Time.time - bornAt >= showAfter) Show();
        }

        private void LateUpdate()
        {
            // The Fairy keeps turning to face the player (her LookAt); the tower keeps its own heading.
            transform.rotation = heading;
        }

        private bool CageIsThere() => cage != null && cage.activeInHierarchy;

        private void Show()
        {
            shown = true;
            foreach (var system in towerSystems)
                if (system != null) system.Play(false);
        }

        // The lock is broken: the tower fades (the bars and top ring flying up), the burst plays, and this goes once
        // everything has faded.
        private void Release()
        {
            released = true;
            // The Fairy is gone a moment later (reaching her sends her "Dead": she destroys herself 3.5 s after, just
            // after the start pumpkin lands): the release plays on without her, beside her (under what she was under, so
            // a stage clean-up still clears it).
            var fairy = transform.parent;
            if (fairy != null) transform.SetParent(fairy.parent, true);
            foreach (var system in towerSystems)
                if (system != null) FadeOut(system, liftedParts != null && System.Array.IndexOf(liftedParts, system) >= 0);
            float longest = fadeTime;
            foreach (var system in releaseSystems)
            {
                if (system == null) continue;
                system.Play(false);
                var main = system.main;
                longest = Mathf.Max(longest, main.startDelay.constantMax + main.startLifetime.constantMax);
            }
            if (releaseSound != null && releaseSound.clip != null)
            {
                releaseSound.Play();
                longest = Mathf.Max(longest, releaseSound.clip.length);
            }
            Destroy(gameObject, longest + 0.1f);
        }

        // No new particles; the long-lived ones start a short life now, so their colour over lifetime fades them out
        // (and the seal's size over lifetime grows it); the sparks just end sooner.
        private void FadeOut(ParticleSystem system, bool lift)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            int most = system.main.maxParticles;
            if (buffer == null || buffer.Length < most) buffer = new ParticleSystem.Particle[most];
            int count = system.GetParticles(buffer);
            for (int i = 0; i < count; i++)
            {
                var particle = buffer[i];
                if (particle.startLifetime > LivesForGood)
                {
                    particle.startLifetime = fadeTime;
                    particle.remainingLifetime = fadeTime;
                }
                else
                {
                    particle.remainingLifetime = Mathf.Min(particle.remainingLifetime, fadeTime);
                }
                if (lift) particle.velocity += Vector3.up * liftSpeed;
                buffer[i] = particle;
            }
            system.SetParticles(buffer, count);
        }
    }
}
