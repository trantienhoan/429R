using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace Game.Locomotion
{
    /// <summary>
    /// Slows the player's walking (thumbstick and arm swing) for a while, e.g. while a spider's web sticks to them. It
    /// eases back to normal over the last half second. With several slows at once the strongest counts, until the
    /// latest one ends. Getting hurt slows too (Hurt, from the Hurt Slow on the XR Origin): the slowest of the two
    /// counts. Turning and teleporting aren't slowed. Added to the XR Origin by Apply or Hurt when first needed.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSlow : MonoBehaviour
    {
        private const float EaseBack = 0.5f;

        private readonly Dictionary<ContinuousMoveProvider, float> normalSpeeds = new();
        private float speedShare = 1f;
        private float until;

        // Getting hurt: the slowest share right after the hit, when it started and ends, and the part of that time it
        // stays at its slowest before easing back.
        private bool hurting;
        private float hurtPeak = 1f;
        private float hurtFrom;
        private float hurtUntil;
        private float hurtHold;

        /// <summary>True while the player is slowed.</summary>
        public bool IsSlowed => speedShare < 1f || hurting;

        /// <summary>The share of their walking speed the player has right now (1 = not slowed).</summary>
        public float CurrentShare => IsSlowed ? Mathf.Min(WebShare(), HurtShare()) : 1f;

        /// <summary>The provider's walking speed without the slow.</summary>
        public float NormalSpeed(ContinuousMoveProvider provider)
        {
            return IsSlowed && normalSpeeds.TryGetValue(provider, out float speed) ? speed : provider.moveSpeed;
        }

        /// <summary>
        /// Changes the speed the provider goes back to when the slow ends, e.g. the player got faster meanwhile (the
        /// Move Speed level-up stat). The slow carries on from the new speed.
        /// </summary>
        public void SetNormalSpeed(ContinuousMoveProvider provider, float speed)
        {
            if (IsSlowed) normalSpeeds[provider] = speed;
            else provider.moveSpeed = speed;
        }

        /// <summary>
        /// Slows the player to <paramref name="share"/> of their walking speed (0.4 = 40%) for
        /// <paramref name="seconds"/> seconds.
        /// </summary>
        public static void Apply(float share, float seconds)
        {
            var slow = Find(seconds);
            if (slow != null) slow.Slow(share, seconds);
        }

        /// <summary>
        /// Slows the player after a hit: to <paramref name="share"/> of their walking speed, which holds for the first
        /// <paramref name="hold"/> part (0 to 1) of <paramref name="seconds"/>, then eases smoothly back to normal.
        /// Hits close together drag a little more, never below <paramref name="slowest"/>.
        /// </summary>
        public static void Hurt(float share, float seconds, float hold, float slowest)
        {
            var slow = Find(seconds);
            if (slow != null) slow.HurtNow(share, seconds, hold, slowest);
        }

        /// <summary>Ends the slow from getting hurt (e.g. the player died: they get up at full speed).</summary>
        public static void EndHurt()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || !origin.TryGetComponent(out PlayerSlow slow) || !slow.hurting) return;
            // Update puts the speed back.
            slow.hurtUntil = 0f;
        }

        private static PlayerSlow Find(float seconds)
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || seconds <= 0f) return null;
            if (!origin.TryGetComponent(out PlayerSlow slow)) slow = origin.gameObject.AddComponent<PlayerSlow>();
            return slow;
        }

        // The normal speeds, taken when a slow starts while none is on.
        private void TakeNormalSpeeds()
        {
            if (IsSlowed) return;
            normalSpeeds.Clear();
            foreach (var provider in GetComponentsInChildren<ContinuousMoveProvider>(true)) normalSpeeds[provider] = provider.moveSpeed;
        }

        private void Slow(float share, float seconds)
        {
            TakeNormalSpeeds();
            speedShare = Mathf.Min(speedShare, Mathf.Clamp01(share));
            until = Mathf.Max(until, Time.time + seconds);
            enabled = true;
            SetSpeeds(CurrentShare);
        }

        private void HurtNow(float share, float seconds, float hold, float slowest)
        {
            TakeNormalSpeeds();
            float now = HurtShare();
            // Still slowed from the last hit: this one adds to it a little.
            float peak = now < 0.999f ? share * Mathf.Lerp(1f, now, 0.5f) : share;
            hurtPeak = Mathf.Clamp01(Mathf.Max(slowest, Mathf.Min(now, peak)));
            hurtFrom = Time.time;
            hurtUntil = Mathf.Max(hurtUntil, Time.time + seconds);
            hurtHold = Mathf.Clamp01(hold);
            hurting = true;
            enabled = true;
            SetSpeeds(CurrentShare);
        }

        // The web-style slow's share right now; eases back over its last half second.
        private float WebShare()
        {
            if (speedShare >= 1f) return 1f;
            float left = until - Time.time;
            if (left <= 0f) return 1f;
            return left < EaseBack ? Mathf.Lerp(1f, speedShare, left / EaseBack) : speedShare;
        }

        // The hurt slow's share right now: its slowest for the hold part, then a smooth ease back to 1.
        private float HurtShare()
        {
            if (!hurting || Time.time >= hurtUntil) return 1f;
            float u = Mathf.InverseLerp(hurtFrom, hurtUntil, Time.time);
            if (u <= hurtHold) return hurtPeak;
            float s = Mathf.Clamp01((u - hurtHold) / Mathf.Max(0.0001f, 1f - hurtHold));
            return Mathf.Lerp(hurtPeak, 1f, s * s * (3f - 2f * s));
        }

        private void Update()
        {
            bool wasSlowed = IsSlowed;
            if (speedShare < 1f && until - Time.time <= 0f) speedShare = 1f;
            if (hurting && Time.time >= hurtUntil) hurting = false;
            if (!IsSlowed)
            {
                // Just ended: back to the normal speeds.
                if (wasSlowed) SetSpeeds(1f);
                enabled = false;
                return;
            }
            SetSpeeds(Mathf.Min(WebShare(), HurtShare()));
        }

        private void OnDisable()
        {
            // Never leave the player slowed (e.g. the scene closing mid-slow).
            if (!IsSlowed) return;
            speedShare = 1f;
            hurting = false;
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
