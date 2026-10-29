using System.Collections.Generic;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// The dizzy stars over a stunned mini boss's or the pumpkin boss's head (user, 2026-10-27), and over a runner pumpkin
    /// out of breath (2026-10-29): a faint ring with stars
    /// circling on it and sparkles trailing them (the Stun_Stars prefab, Tools > 429 Game > Enemies > Build Stun Stars FX).
    /// Show puts one over a target for a while, or keeps the one already there up longer; it follows the head (not
    /// parented, so the bones' scale doesn't touch it), stays level, and is gone when the time is up or the target is.
    /// </summary>
    public class StunStars : MonoBehaviour
    {
        private static readonly Dictionary<Transform, StunStars> Showing = new();

        private Transform target;
        private Transform follow;
        private float lift;
        private float until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Showing.Clear();

        /// <summary>
        /// Shows the stars (a copy of Prefab) over Target for Seconds: following Follow (its head, or itself), Lift metres
        /// above it, the ring Radius metres across from the middle. Already showing: it stays up longer, at this Lift and
        /// Radius. Sound off: no
        /// dizzy sound (e.g. a tired runner pumpkin, out of breath for many seconds).
        /// </summary>
        public static void Show(GameObject prefab, Transform target, Transform follow, float lift, float radius, float seconds, bool sound = true)
        {
            if (prefab == null || target == null || seconds <= 0f) return;
            if (Showing.TryGetValue(target, out var stars) && stars != null)
            {
                stars.until = Mathf.Max(stars.until, Time.time + seconds);
                // As high and as wide as asked now (e.g. a pumpkin still growing in).
                stars.lift = lift;
                stars.transform.localScale = Vector3.one * Mathf.Max(0.01f, radius);
                return;
            }

            var copy = Instantiate(prefab);
            copy.name = prefab.name;
            if (!sound)
            {
                foreach (var speaker in copy.GetComponentsInChildren<AudioSource>())
                {
                    speaker.Stop();
                    speaker.enabled = false;
                }
            }
            if (!copy.TryGetComponent(out stars)) stars = copy.AddComponent<StunStars>();
            stars.target = target;
            stars.follow = follow != null ? follow : target;
            stars.lift = lift;
            stars.until = Time.time + seconds;
            copy.transform.localScale = Vector3.one * Mathf.Max(0.01f, radius);
            stars.Place();
            Showing[target] = stars;
        }

        /// <summary>Takes the stars off Target at once (e.g. it died).</summary>
        public static void Hide(Transform target)
        {
            if (target != null && Showing.TryGetValue(target, out var stars) && stars != null) stars.until = 0f;
        }

        private void LateUpdate()
        {
            if (target == null || follow == null || !target.gameObject.activeInHierarchy || Time.time >= until)
            {
                Finish();
                return;
            }
            Place();
        }

        // Over the head, level whatever the head does.
        private void Place()
        {
            transform.SetPositionAndRotation(follow.position + Vector3.up * lift, Quaternion.identity);
        }

        private void Finish()
        {
            // By reference: a target that's been destroyed is still its key.
            if ((object)target != null && Showing.TryGetValue(target, out var stars) && stars == this) Showing.Remove(target);
            Destroy(gameObject);
        }
    }
}
