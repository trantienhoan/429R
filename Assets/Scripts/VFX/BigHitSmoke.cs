using Game.Combat;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Puffs a piece of furniture's own smoke (its child "Smoke_Blast") when the player lands a big hit on it: a hammer
    /// blow, a heavy throw or a hard punch, the kind that would knock an enemy back. Small hits, bumps and things falling
    /// on it don't. Then it waits Cooldown seconds before it can puff again, and only one piece in the whole game puffs
    /// at a time (a quarter of a second apart), so a falling tower of blocks can't start a dozen smokes at once.
    /// - With Enemy Hit Damage on the same object (the breakable furniture) it uses that hit's damage.
    /// - Without it (the Jenga blocks) it measures the hit the same way itself (Settings, Toughness), counting only the
    ///   player's hits (a fist, a thing they hold or just threw, a shot).
    /// The smoke switches itself off again (its own FSM), so it is simply switched off and on to play it again.
    /// </summary>
    [DisallowMultipleComponent]
    public class BigHitSmoke : MonoBehaviour
    {
        // Two puffs anywhere in the game are at least this far apart, in seconds.
        private const float GapBetweenAny = 0.25f;

        [Tooltip("The smoke to puff: a child that switches itself off again. Empty: the child named Smoke_Blast.")]
        [SerializeField] private GameObject smoke;
        [Tooltip("Only a big hit doing at least this much damage puffs (a big hit does 3-5, a hammer's more). " +
                 "0 = every big hit.")]
        [Min(0f)]
        [SerializeField] private float minDamage = 4f;
        [Tooltip("After a puff, no new one for this many seconds.")]
        [Min(0f)]
        [SerializeField] private float cooldown = 1.5f;

        [Header("Without Enemy Hit Damage (e.g. the Jenga blocks)")]
        [Tooltip("How hits are measured (the same asset as the enemies'). Empty: the default numbers.")]
        [SerializeField] private HitSettings settings;
        [Tooltip("Divides the hit power, like an enemy's Toughness: higher = needs harder hits to puff.")]
        [Min(0.1f)]
        [SerializeField] private float toughness = 1f;

        private static float nextAnyPuff;
        private EnemyHitDamage hits;
        private float nextPuff;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => nextAnyPuff = 0f;

        private void Awake()
        {
            TryGetComponent(out hits);
            if (smoke == null) smoke = FindChild(transform, "Smoke_Blast");
        }

        private void OnEnable()
        {
            if (hits != null) EnemyHitDamage.AnyHit += OnAnyHit;
        }

        private void OnDisable()
        {
            EnemyHitDamage.AnyHit -= OnAnyHit;
        }

        private void OnAnyHit(EnemyHitDamage hurt, HitResult hit)
        {
            if (hurt == hits && IsBig(hit)) Puff();
        }

        // Only for objects without Enemy Hit Damage.
        private void OnCollisionEnter(Collision collision)
        {
            if (hits != null || smoke == null || Time.time < nextPuff || Time.time < nextAnyPuff) return;
            if (!HeldThings.ByPlayer(collision)) return;

            float power = collision.relativeVelocity.magnitude * WeaponPower.Of(collision) / Mathf.Max(0.1f, toughness);
            var hit = (settings != null ? settings : HitSettings.Defaults).Evaluate(power, WeaponPower.DamageOf(collision));
            if (IsBig(hit)) Puff();
        }

        private bool IsBig(HitResult hit) => hit.IsBig && hit.Damage >= minDamage;

        /// <summary>Puffs the smoke now (unless it puffed less than Cooldown ago).</summary>
        public void Puff()
        {
            if (smoke == null || Time.time < nextPuff || Time.time < nextAnyPuff) return;
            nextPuff = Time.time + cooldown;
            nextAnyPuff = Time.time + GapBetweenAny;

            // Switched on again: its particles and its FSM (which switches it off after a second) start over.
            smoke.SetActive(false);
            smoke.SetActive(true);
            foreach (var particles in smoke.GetComponentsInChildren<ParticleSystem>())
            {
                if (particles.transform.parent != null && particles.transform.parent.GetComponentInParent<ParticleSystem>() != null) continue;
                particles.Clear(true);
                particles.Play(true);
            }
        }

        private static GameObject FindChild(Transform parent, string childName)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t != parent && t.name == childName) return t.gameObject;
            }
            return null;
        }
    }
}
