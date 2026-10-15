using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Combat
{
    /// <summary>
    /// Which things the player is using: held in a hand, or let go (thrown) a moment ago. Hit Rumble On Grab (on the XR
    /// Origin) tells it when a hand lets go. Furniture uses it to tell the player's hits (a fist, a held or thrown weapon,
    /// a shot) from things that only fell or slid into it (a hammer lying on the floor, a book falling off a shelf).
    /// </summary>
    public static class HeldThings
    {
        // Let go less than this many seconds ago, a thing still counts as the player's (it's flying from a throw).
        private const float ThrownSeconds = 2.5f;

        private static readonly Dictionary<XRGrabInteractable, float> releasedAt = new();
        private static readonly List<XRGrabInteractable> stale = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => releasedAt.Clear();

        /// <summary>A hand just let go of this.</summary>
        public static void Released(XRGrabInteractable thing)
        {
            if (thing == null) return;
            if (releasedAt.Count > 200) Forget();
            releasedAt[thing] = Time.time;
        }

        /// <summary>Held, or let go (thrown) less than a moment ago.</summary>
        public static bool InUse(XRGrabInteractable thing)
        {
            if (thing == null || !thing.enabled) return false;
            return thing.isSelected || (releasedAt.TryGetValue(thing, out float at) && Time.time - at < ThrownSeconds);
        }

        /// <summary>
        /// Whether the player made this hit: a fist (or a flying fist), a thing they hold or just threw, or a weapon that
        /// can't be held (a shot). A thing lying around that just fell or slid into it doesn't count.
        /// </summary>
        public static bool ByPlayer(Collision collision)
        {
            var source = collision.rigidbody;
            if (source == null) return false;
            if (Fist.IsFistBall(source)) return true;
            if (source.TryGetComponent<XRGrabInteractable>(out var grab)) return InUse(grab);
            return WeaponPower.Has(collision);
        }

        // Drops things that are gone or were let go long ago.
        private static void Forget()
        {
            stale.Clear();
            foreach (var pair in releasedAt)
            {
                if (pair.Key == null || Time.time - pair.Value > ThrownSeconds) stale.Add(pair.Key);
            }
            foreach (var thing in stale) releasedAt.Remove(thing);
        }
    }
}
