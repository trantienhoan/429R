using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Game.Locomotion
{
    /// <summary>
    /// Slows the player's walking (thumbstick and arm swing) for a while, e.g. while a spider's web sticks to them. It
    /// eases back to normal over the last half second. With several slows at once the strongest counts, until the
    /// latest one ends. Getting hurt slows too (Hurt, from the Hurt Slow on the XR Origin): the slowest of the two
    /// counts. Turning and teleporting aren't slowed, but a slow that says so (being webbed, Player Web) stops
    /// teleporting until it ends: the teleport ray still shows, it just can't land anywhere. Added to the XR Origin by
    /// Apply or Hurt when first needed.
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

        // No teleporting until then (being webbed): the teleport layers taken off each teleport ray, to give back.
        private float noTeleportUntil;
        private readonly Dictionary<XRRayInteractor, int> takenTeleport = new();
        // XRI's Teleport interaction layer, if no teleport area is out to tell which one it is.
        private const int TeleportLayerBit = 1 << 31;

        /// <summary>True while the player is slowed.</summary>
        public bool IsSlowed => speedShare < 1f || hurting;

        /// <summary>True while the player can't teleport (being webbed, Player Web).</summary>
        public bool TeleportBlocked => takenTeleport.Count > 0;

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
        /// <paramref name="seconds"/> seconds. <paramref name="noTeleport"/>: they can't teleport either until then.
        /// </summary>
        public static void Apply(float share, float seconds, bool noTeleport = false)
        {
            var slow = Find(seconds);
            if (slow == null) return;
            slow.Slow(share, seconds);
            if (noTeleport) slow.BlockTeleport(seconds);
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

        /// <summary>
        /// Ends every slow from Apply (a web, a cockroach's leap, a bug pumpkin) and lets the player teleport again, e.g.
        /// they went down while webbed: they get up free.
        /// </summary>
        public static void EndWebSlow()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || !origin.TryGetComponent(out PlayerSlow slow)) return;
            if (slow.speedShare >= 1f && !slow.TeleportBlocked) return;
            // Update puts the speed back and gives teleporting back.
            slow.until = 0f;
            slow.noTeleportUntil = 0f;
            slow.enabled = true;
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

        // Takes the teleport layers off the player's teleport rays (they no longer find anywhere to land), until then.
        private void BlockTeleport(float seconds)
        {
            noTeleportUntil = Mathf.Max(noTeleportUntil, Time.time + seconds);
            enabled = true;
            if (TeleportBlocked) return;
            int teleportLayers = 0;
            foreach (var area in FindObjectsByType<BaseTeleportationInteractable>(FindObjectsSortMode.None)) teleportLayers |= area.interactionLayers.value;
            if (teleportLayers == 0) teleportLayers = TeleportLayerBit;
            foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true))
            {
                int taken = ray.interactionLayers.value & teleportLayers;
                if (taken == 0) continue;
                takenTeleport[ray] = taken;
                ray.interactionLayers = ray.interactionLayers.value & ~taken;
            }
        }

        private void GiveTeleportBack()
        {
            foreach (var pair in takenTeleport)
            {
                if (pair.Key != null) pair.Key.interactionLayers = pair.Key.interactionLayers.value | pair.Value;
            }
            takenTeleport.Clear();
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
            if (TeleportBlocked && Time.time >= noTeleportUntil) GiveTeleportBack();
            if (!IsSlowed)
            {
                // Just ended: back to the normal speeds.
                if (wasSlowed) SetSpeeds(1f);
                if (!TeleportBlocked) enabled = false;
                return;
            }
            SetSpeeds(Mathf.Min(WebShare(), HurtShare()));
        }

        private void OnDisable()
        {
            // Never leave the player slowed or unable to teleport (e.g. the scene closing mid-slow).
            GiveTeleportBack();
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
