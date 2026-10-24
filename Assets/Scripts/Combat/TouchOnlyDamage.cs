using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// For the chase pumpkins (Pumpkin_3, 5, 7): only the player running into them hurts them (their PlayMaker Damage
    /// FSM, on a Player-tagged touch). Everything else is blocked, with a block effect so the player sees it bounce off:
    /// punches, swung weapons, things thrown at them, and blasts (which would otherwise lower their health). Goes on the
    /// pumpkin's root, next to its Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    public class TouchOnlyDamage : MonoBehaviour
    {
        [Tooltip("Played where a hit is blocked (Effect Pool), e.g. Flash_round_yellow, at this size.")]
        [SerializeField] private GameObject blockEffect;
        [Min(0f)]
        [SerializeField] private float blockEffectSize = 0.5f;
        [Tooltip("Played where a hit is blocked, one at random, e.g. bone_bonk_1 and 2 (the bugs' blocked sound).")]
        [SerializeField] private AudioClip[] blockSounds = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float blockVolume = 0.8f;
        [Tooltip("Hits slower than this, in metres per second, are just touches: no block effect.")]
        [Min(0f)]
        [SerializeField] private float minHitSpeed = 1.5f;
        [Tooltip("At most one block effect per this many seconds.")]
        [Min(0f)]
        [SerializeField] private float blockGap = 0.2f;

        private float nextBlock;

        /// <summary>
        /// Whether a blast (or anything else that lowers health directly) on this part is blocked: then the block effect
        /// plays at the point and the caller leaves its health alone.
        /// </summary>
        public static bool Blocks(Component part, Vector3 point)
        {
            var guard = part != null ? part.GetComponentInParent<TouchOnlyDamage>() : null;
            if (guard == null) return false;
            guard.Block(point);
            return true;
        }

        // The player's hits and the things they use: a fist, a weapon, a thrown thing (anything with a Weapon Power, or
        // tagged Weapon). The player's body is no Rigidbody: its touch goes to the Damage FSM as before.
        private void OnCollisionEnter(Collision collision)
        {
            var other = collision.rigidbody;
            if (other == null || collision.relativeVelocity.magnitude < minHitSpeed) return;
            if (!Fist.IsFistBall(other) && !other.TryGetComponent<WeaponPower>(out _) && !other.CompareTag("Weapon")) return;
            Block(collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position);
        }

        private void Block(Vector3 point)
        {
            if (Time.time < nextBlock) return;
            nextBlock = Time.time + blockGap;
            if (blockEffect != null) EffectPool.Play(blockEffect, point, blockEffectSize);
            if (blockSounds.Length == 0) return;
            var clip = blockSounds[Random.Range(0, blockSounds.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, point, blockVolume);
        }
    }
}
