using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Ends a looping 3D smoke as a puff (user, 2026-11-19: the magic hat tables' smoke was "scaling down at root, not
    /// puff out"). The looping smoke on the hat tables and treasure chests (Door_smoke_black, a Smoke_Puff_Loop) is
    /// ended by an FSM of its own: Wait, then Tween Scale itself down to 0.1 over 2 s, then Deactivate Self. That suited
    /// the old flat cards, but the 3D puffs scale with their object and live in its space, so every puff was squeezed
    /// into a thin column at its root. Now, the moment it starts shrinking, that FSM is stopped, the size is put back,
    /// no new puffs are made, and the last ones drift, grow and fade on their own; once they're gone it switches itself
    /// off (the FSM is switched back on, so it runs again next time). A smoke that never shrinks isn't touched.
    /// With Come Back ticked it only takes a break instead (the music players' notes, Music_Notes_Loop: its FSM hides it
    /// for 2 s every 300 s and grows it back, which squeezed the notes into its root too): once the last one has faded,
    /// its FSM starts again from the top and so does the effect.
    /// With Fade When Removed ticked, a smoke switched off or removed with what it's on doesn't vanish either (the
    /// rooftop gates' smoke, removed with its gate when the rooftop ends): the puffs in the air go on in a loose copy
    /// that makes no new ones and is gone once they've faded.
    /// Goes on Prefabs/Effects/Smoke/Smoke_Puff_Loop and Prefabs/Effects/Music_Notes_Loop (all their copies get it).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public class PuffOutWhenShrunk : MonoBehaviour
    {
        // Shrunk below this share of its size: its FSM has started the scale-down.
        private const float ShrinkShare = 0.98f;

        [Tooltip("Comes back once the last one has faded: its FSM starts again from the top, and so does the effect (an " +
                 "FSM that only hides it a moment, e.g. the music players' notes). Off: it switches itself off (the hat " +
                 "tables' and treasure chests' smoke).")]
        [SerializeField] private bool comeBack;
        [Tooltip("Fades out instead of vanishing when it's switched off or removed with what it's on (the rooftop gates' " +
                 "smoke: the rooftop's End removes the gates with it): the puffs in the air go on in a loose copy that " +
                 "makes no new ones, and that's gone once they've faded. Off: they vanish with it.")]
        [SerializeField] private bool fadeWhenRemoved;

        private static bool quitting;

        private ParticleSystem system;
        // Fade When Removed: its puffs as of the last frame (switched off, a particle system has already dropped its
        // puffs by the time this hears of it).
        private ParticleSystem.Particle[] lastPuffs;
        private int lastPuffCount;
        private Vector3 fullScale;
        private PlayMakerFSM stoppedFsm;
        private bool puffingOut;

        private void Awake()
        {
            system = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            fullScale = transform.localScale;
            puffingOut = false;
        }

        private void LateUpdate()
        {
            if (fadeWhenRemoved) KeepLastPuffs();
            if (!puffingOut)
            {
                var now = transform.localScale;
                // (Its FSM may grow it first, e.g. the notes' Init: its full size is the biggest it has been.)
                if (Mathf.Abs(now.x) > Mathf.Abs(fullScale.x)) fullScale = now;
                if (Mathf.Abs(now.x) >= Mathf.Abs(fullScale.x) * ShrinkShare) return;
                puffingOut = true;
                // The FSM shrinking it (its own) stops here; it's switched back on when the smoke goes off.
                foreach (var fsm in GetComponents<PlayMakerFSM>())
                {
                    if (!fsm.enabled) continue;
                    fsm.enabled = false;
                    stoppedFsm = fsm;
                }
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            transform.localScale = fullScale;
            if (system.IsAlive(true)) return;
            if (comeBack) Again();
            else gameObject.SetActive(false);
        }

        // The break is over: its FSM from the top (it grows it back to full, then waits for the next break), and the effect.
        private void Again()
        {
            puffingOut = false;
            if (stoppedFsm != null)
            {
                stoppedFsm.enabled = true;
                stoppedFsm.Fsm.SetState(stoppedFsm.Fsm.StartState);
                stoppedFsm = null;
            }
            system.Play(true);
        }

        private void OnDisable()
        {
            if (fadeWhenRemoved) LeaveLastPuffs();
            if (stoppedFsm != null) stoppedFsm.enabled = true;
            stoppedFsm = null;
            if (puffingOut) transform.localScale = fullScale;
            puffingOut = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => quitting = true;

        // Its puffs as they are now (a few dozen at most), for Leave Last Puffs.
        private void KeepLastPuffs()
        {
            int count = system.particleCount;
            if (count > 0 && (lastPuffs == null || lastPuffs.Length < count))
                lastPuffs = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];
            lastPuffCount = count > 0 ? system.GetParticles(lastPuffs) : 0;
        }

        // Switched off or removed with what it's on: the puffs in the air go on in a loose copy of it, where it stood and as
        // big, that makes no new ones and is gone once they've faded. Not while the game or the scene is closing.
        private void LeaveLastPuffs()
        {
            int count = lastPuffCount;
            lastPuffCount = 0;
            if (quitting || !Application.isPlaying || count <= 0 || !gameObject.scene.isLoaded) return;
            float last = 0f;
            for (int i = 0; i < count; i++) last = Mathf.Max(last, lastPuffs[i].remainingLifetime);

            var copy = Instantiate(gameObject, transform.position, transform.rotation);
            copy.name = name + " (fading)";
            copy.transform.localScale = transform.lossyScale;
            if (copy.TryGetComponent(out PuffOutWhenShrunk own)) own.fadeWhenRemoved = false;
            foreach (var fsm in copy.GetComponents<PlayMakerFSM>()) Destroy(fsm);
            // (Copied switched off when it was switching itself off.)
            if (!copy.activeSelf) copy.SetActive(true);
            var left = copy.GetComponent<ParticleSystem>();
            left.Clear(true);
            var emission = left.emission;
            emission.enabled = false;
            left.Play(true);
            left.SetParticles(lastPuffs, count);
            Destroy(copy, last + 0.2f);
        }
    }
}
