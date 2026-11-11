using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Lets the next hit on the player do only a share of its damage, e.g. half for a bite blocked by a bare fist (Spider
    /// Brain: Fist Block Share), or more than all of it, e.g. 1.3 for a chaos pumpkin boss's blow (Pumpkin Boss Brain:
    /// Chaos Damage). The PlayerHealth FSM's damage (Float Subtract Random with Player Toughness ticked) uses it up, on top
    /// of Toughness. Kept for a moment only: the damage event reaches the PlayerHealth FSM a frame or so later.
    /// </summary>
    public static class PlayerDamageShare
    {
        private const float KeepSeconds = 0.5f;
        // At most three times the damage (a chaos boss asks for 1.3).
        private const float MostShare = 3f;
        private static float share = 1f;
        private static float until = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            share = 1f;
            until = -1f;
        }

        /// <summary>The next hit on the player (within a moment) does this share of its damage (0.5 = half, 1.3 = 30% more).</summary>
        public static void Next(float value)
        {
            share = Mathf.Clamp(value, 0f, MostShare);
            until = Time.time + KeepSeconds;
        }

        /// <summary>The share for the hit landing now (1 = all of it); it's used up.</summary>
        public static float Take()
        {
            if (Time.time > until) return 1f;
            until = -1f;
            return share;
        }
    }
}
