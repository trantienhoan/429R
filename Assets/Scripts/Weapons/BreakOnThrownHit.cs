using Game.Combat;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Weapons
{
    /// <summary>
    /// A throwable that bursts the moment it's thrown into an enemy (Pumpkin_On_Tree): it breaks the way it always
    /// does, through its Health FSM (its health goes to 0: pieces, candy, its EXP), instead of bouncing off. The enemy
    /// takes the hit as before; this adds no damage. Only a throw counts (let go a moment ago, not held), and only an
    /// enemy (anything with Enemy Hit Damage that isn't furniture, or tagged Enemy or Monster), not walls, floors or
    /// furniture. Splat: as it bursts, the other enemies close by are splashed (shoved a little and stunned for a moment,
    /// no damage), with a burst of pumpkin, a squelch and "SPLAT!". Goes on the throwable's root, next to its XR Grab
    /// Interactable.
    /// Tools > 429 Game > Items > Tree Pumpkins Break On Enemies adds it to Pumpkin_On_Tree.
    /// </summary>
    [DisallowMultipleComponent]
    public class BreakOnThrownHit : MonoBehaviour
    {
        [Tooltip("Hits slower than this (metres per second) don't break it: a pumpkin rolling into a bug stays whole.")]
        [Min(0f)]
        [SerializeField] private float minSpeed = 3f;

        [Header("Splat")]
        [Tooltip("Other enemies this close to where it burst (metres) are splashed: stunned and shoved, no damage. 0 = none.")]
        [Min(0f)]
        [SerializeField] private float splatRadius = 1.6f;
        [Tooltip("How long a splashed enemy is stunned, in seconds (a boss with its Stun Guard up shrugs it off).")]
        [Min(0f)]
        [SerializeField] private float splatStunSeconds = 0.8f;
        [Tooltip("How far a splashed enemy is shoved and how high it hops (shares of a big hit's knock-back).")]
        [SerializeField] private Vector2 splatPush = new(0.5f, 0.4f);
        [Tooltip("At most this many enemies are splashed.")]
        [Min(0)]
        [SerializeField] private int maxSplatted = 6;
        [Tooltip("Played where it bursts (Effect Pool), e.g. Hit_Blast and Smoke_Blast.")]
        [SerializeField] private GameObject[] splatEffects = System.Array.Empty<GameObject>();
        [Min(0.05f)]
        [SerializeField] private float splatEffectSize = 0.8f;
        [SerializeField] private AudioClip splatSound;
        [Range(0f, 1f)]
        [SerializeField] private float splatVolume = 0.9f;
        [SerializeField] private string splatWords = "SPLAT!";
        [SerializeField] private Color splatColor = new(1f, 0.55f, 0.1f);

        [Header("PlayMaker")]
        [Tooltip("The FSM whose float \"health\" breaks it at 0.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        private static readonly Collider[] nearby = new Collider[48];
        private static readonly System.Collections.Generic.HashSet<EnemyHitDamage> splatted = new();

        private XRGrabInteractable grab;
        private HutongGames.PlayMaker.FsmFloat health;
        private bool broken;

        private void Awake()
        {
            TryGetComponent(out grab);
            Blast.Warm(splatEffects);
        }

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                break;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (broken || health == null || health.Value <= 0f) return;
            // Thrown: let go a moment ago and still flying, not in a hand.
            if (grab == null || grab.isSelected || !HeldThings.InUse(grab)) return;
            if (collision.relativeVelocity.magnitude < minSpeed || !IsEnemy(collision)) return;
            broken = true;
            health.Value = 0f;
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            var other = collision.rigidbody != null ? collision.rigidbody.gameObject : collision.gameObject;
            Splat(at, other.GetComponentInParent<EnemyHitDamage>());
        }

        // The burst splashes the other enemies around: stunned and shoved for a moment (no damage), with its splat.
        private void Splat(Vector3 at, EnemyHitDamage hitOne)
        {
            foreach (var effect in splatEffects)
            {
                if (effect != null) EffectPool.Play(effect, at, splatEffectSize);
            }
            if (splatSound != null) AudioSource.PlayClipAtPoint(splatSound, at, splatVolume);
            if (!string.IsNullOrEmpty(splatWords)) Game.VFX.FloatingWords.Show(splatWords, splatColor, at + Vector3.up * 0.1f);
            if (splatRadius <= 0f || maxSplatted <= 0) return;

            splatted.Clear();
            if (hitOne != null) splatted.Add(hitOne);
            int count = Physics.OverlapSphereNonAlloc(at, splatRadius, nearby, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            int done = 0;
            for (int i = 0; i < count && done < maxSplatted; i++)
            {
                var enemy = nearby[i].GetComponentInParent<EnemyHitDamage>();
                if (enemy == null || enemy.IsFurniture || enemy.IsDead || !splatted.Add(enemy)) continue;
                enemy.KnockOut(0f, splatPush.x, splatPush.y, splatStunSeconds);
                done++;
            }
        }

        private static bool IsEnemy(Collision collision)
        {
            var other = collision.rigidbody != null ? collision.rigidbody.gameObject : collision.gameObject;
            var hits = other.GetComponentInParent<EnemyHitDamage>();
            if (hits != null) return !hits.IsFurniture && !hits.IsDead;
            return other.CompareTag("Enemy") || other.CompareTag("Monster");
        }
    }
}
