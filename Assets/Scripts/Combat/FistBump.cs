using System;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Notices the player bumping their two fists together: both hands are fists (Fist), the fists touch, and each
    /// hand was moving towards the other (so a punch flying past the other fist doesn't count). Each bump clanks like two
    /// knight gauntlets and flashes a little star where the fists meet (Feedback), so the player notices it counted.
    /// Two bumps in a row (Double Bump Seconds) is the double bump that opens the Level Up menu.
    /// Goes on the XR Origin; it finds the two Fists on the controllers by itself.
    /// </summary>
    [DisallowMultipleComponent]
    public class FistBump : MonoBehaviour
    {
        [Tooltip("The fists count as touching when they're this close, in metres (between the two fist balls; 0 = just " +
                 "touching). Bigger = easier.")]
        [SerializeField] private float touchGap = 0.03f;
        [Tooltip("How fast each hand must be moving towards the other at the bump, in metres per second. Keeps fists that " +
                 "drift close, or a punch past the other fist, from counting.")]
        [Min(0f)]
        [SerializeField] private float minHandSpeed = 0.2f;
        [Tooltip("The second bump must come within this many seconds of the first for a double bump.")]
        [Min(0.1f)]
        [SerializeField] private float doubleBumpSeconds = 1.2f;
        [Tooltip("How strong both controllers buzz at a bump (0 to 1). 0 = none.")]
        [Range(0f, 1f)]
        [SerializeField] private float rumble = 0.6f;

        [Header("Feedback")]
        [Tooltip("A little flash where the fists meet at each bump (e.g. Flash_star_ellow_purple). The second bump of a " +
                 "double bump makes it twice as big. Empty = none.")]
        [SerializeField] private GameObject bumpEffect;
        [Tooltip("Size of the flash: this many times its prefab's own size. 0 = no flash.")]
        [Min(0f)]
        [SerializeField] private float bumpEffectSize = 0.15f;
        [Tooltip("The clank of two knight gauntlets at each bump (e.g. metal_object_hit_1). Empty = silent.")]
        [SerializeField] private AudioClip bumpSound;
        [Range(0f, 1f)]
        [SerializeField] private float bumpVolume = 0.6f;

        // The fists must come this much farther apart again, in metres, before the next bump counts.
        private const float PartGap = 0.05f;

        private Fist[] fists;
        private bool touching;
        private float lastBump = -100f;

        /// <summary>Raised at every fist bump.</summary>
        public static event Action Bumped;

        /// <summary>Raised at the second of two fist bumps in a row.</summary>
        public static event Action DoubleBumped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Bumped = null;
            DoubleBumped = null;
        }

        private void Start()
        {
            fists = GetComponentsInChildren<Fist>(true);
            if (fists.Length < 2)
            {
                Debug.LogWarning($"[Fist Bump] '{name}' needs two Fists under it (Left Controller and Right Controller), found {fists.Length}.", this);
                enabled = false;
                return;
            }
            // Ready at load, so the first bump doesn't stutter (two: a double bump plays it twice).
            if (bumpEffectSize > 0f) EffectPool.Warm(bumpEffect, 2);
        }

        private void Update()
        {
            var a = fists[0];
            var b = fists[1];
            if (a == null || b == null || !a.isActiveAndEnabled || !b.isActiveAndEnabled || !a.IsFist || !b.IsFist)
            {
                touching = false;
                return;
            }

            var between = b.Knuckles - a.Knuckles;
            float gap = between.magnitude - a.BallSize - b.BallSize;
            if (touching)
            {
                if (gap > touchGap + PartGap) touching = false;
                return;
            }
            if (gap > touchGap) return;

            touching = true;
            // Each hand coming at the other. The hands' speeds are in the play area's space, like the controllers.
            var towards = between.sqrMagnitude > 0.000001f ? between.normalized : Vector3.zero;
            var space = a.transform.parent;
            if (space != null) towards = space.InverseTransformDirection(towards);
            if (Vector3.Dot(a.HandVelocity, towards) < minHandSpeed || Vector3.Dot(b.HandVelocity, -towards) < minHandSpeed) return;

            if (rumble > 0f)
            {
                a.Buzz(rumble, 0.08f);
                b.Buzz(rumble, 0.08f);
            }
            Bumped?.Invoke();
            bool second = Time.unscaledTime - lastBump <= doubleBumpSeconds;
            // A clank and a flash where the fists meet (no words): the player sees and hears that the bump counted.
            Clank((a.Knuckles + b.Knuckles) * 0.5f, second ? 2f : 1f);
            if (second)
            {
                lastBump = -100f;
                DoubleBumped?.Invoke();
            }
            else
            {
                lastBump = Time.unscaledTime;
            }
        }

        private void Clank(Vector3 at, float size)
        {
            if (bumpEffect != null && bumpEffectSize > 0f) EffectPool.Play(bumpEffect, at, bumpEffectSize * size);
            if (bumpSound != null && bumpVolume > 0f) AudioSource.PlayClipAtPoint(bumpSound, at, bumpVolume);
        }
    }
}
