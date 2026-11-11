using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// A puff of smoke where something appears: the moment it's made, its Effect plays once at its feet, from the
    /// shared pool (EffectPool), so it's cheap on the headset. Goes on Spider_Tree, the spider-lings that come out of
    /// the rooftop portals in Defend the Tree, with the shared 3D Smoke_Flash_Small_Puff (user, 2026-11-10: the portal
    /// "still uses the old smoke particles"; never Lana's Smoke_Blast). Works the same on any other spawned bug.
    /// Tools > 429 Game > Balance 2026-11-10 > PlayGround And Portal FX puts it there.
    /// </summary>
    [DisallowMultipleComponent]
    public class PuffOnAppear : MonoBehaviour
    {
        [Tooltip("The one-shot smoke it puffs (Smoke_Flash_Small_Puff). Empty = no puff.")]
        [SerializeField] private GameObject effect;
        [Tooltip("Its size, times the effect's own (Smoke_Flash_Small_Puff's root is 3, so 0.5 makes it 1.5).")]
        [Min(0.01f)]
        [SerializeField] private float size = 0.5f;
        [Tooltip("How far above this thing's position (a bug's feet) it puffs, in metres.")]
        [SerializeField] private float height = 0.1f;

        private void Awake()
        {
            // Two copies ready, since a portal lets several out within a few seconds.
            if (effect != null) EffectPool.Warm(effect, 2);
        }

        private void Start()
        {
            if (effect != null) EffectPool.Play(effect, transform.position + Vector3.up * height, size);
        }
    }
}
