using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace Game.Locomotion
{
    /// <summary>
    /// Slows the player's walking (thumbstick and arm swing) for a while, e.g. while a spider's web sticks to them. It
    /// eases back to normal over the last half second. With several slows at once the strongest counts, until the
    /// latest one ends. Turning and teleporting aren't slowed. Added to the XR Origin by Apply when first needed.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSlow : MonoBehaviour
    {
        private const float EaseBack = 0.5f;

        private readonly Dictionary<ContinuousMoveProvider, float> normalSpeeds = new();
        private float speedShare = 1f;
        private float until;

        /// <summary>True while the player is slowed.</summary>
        public bool IsSlowed => speedShare < 1f;

        /// <summary>
        /// Slows the player to <paramref name="share"/> of their walking speed (0.4 = 40%) for
        /// <paramref name="seconds"/> seconds.
        /// </summary>
        public static void Apply(float share, float seconds)
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || seconds <= 0f) return;
            if (!origin.TryGetComponent(out PlayerSlow slow)) slow = origin.gameObject.AddComponent<PlayerSlow>();
            slow.Slow(share, seconds);
        }

        private void Slow(float share, float seconds)
        {
            if (!IsSlowed)
            {
                normalSpeeds.Clear();
                foreach (var provider in GetComponentsInChildren<ContinuousMoveProvider>(true)) normalSpeeds[provider] = provider.moveSpeed;
            }
            speedShare = Mathf.Min(speedShare, Mathf.Clamp01(share));
            until = Mathf.Max(until, Time.time + seconds);
            enabled = true;
            SetSpeeds(speedShare);
        }

        private void Update()
        {
            if (!IsSlowed)
            {
                enabled = false;
                return;
            }

            float left = until - Time.time;
            if (left <= 0f)
            {
                speedShare = 1f;
                SetSpeeds(1f);
                enabled = false;
                return;
            }
            SetSpeeds(left < EaseBack ? Mathf.Lerp(1f, speedShare, left / EaseBack) : speedShare);
        }

        private void OnDisable()
        {
            // Never leave the player slowed (e.g. the scene closing mid-slow).
            if (!IsSlowed) return;
            speedShare = 1f;
            SetSpeeds(1f);
        }

        private void SetSpeeds(float share)
        {
            foreach (var pair in normalSpeeds)
            {
                if (pair.Key != null) pair.Key.moveSpeed = pair.Value * share;
            }
        }
    }
}
