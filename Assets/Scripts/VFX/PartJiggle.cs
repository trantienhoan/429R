using System.Collections.Generic;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Makes only the part of a rigged body that's hit jiggle, like a pumpkin's jiggle (Jiggle) but on its own (user,
    /// 2026-10-29: "can the Pumpkin_Boss_Z only jiggle where it get punched? arm, leg, body, head, butt"): punched on
    /// the head, the head squashes along the punch and rocks away from it while the rest of the body carries on.
    /// Each Part is a bone with a hit collider on it, and the middle of its pumpkin in the bone's own space; the jiggle
    /// squashes and rocks that bone around that middle after the animation, and the bones holding other parts (an arm
    /// off the chest, the legs and waist off the hips) stay where the animation put them, while the rest hanging off it
    /// (the head's jaw and stem) jiggles with it. A part holding other parts swells and shrinks evenly instead of
    /// squashing flat: squashed one way, the parts hanging off it at a slant would be skewed and pulled along.
    /// Every hit by the player at Min Speed or more jiggles the part it landed on, harder the faster it came, whether
    /// or not it hurt (Enemy Hit Damage ignores hits for a moment after one that hurts), so every punch shows. It stops
    /// once its Enemy Hit Damage is dead.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class PartJiggle : MonoBehaviour
    {
        [System.Serializable]
        public struct Part
        {
            [Tooltip("The bone (its name, anywhere under this object) carrying the part and its hit collider.")]
            public string bone;
            [Tooltip("The middle of the part, in the bone's own space: it squashes and rocks around it.")]
            public Vector3 centre;
        }

        [SerializeField] private Part[] parts = System.Array.Empty<Part>();
        [Tooltip("Hits slower than this (metres per second) don't jiggle it.")]
        [Min(0f)]
        [SerializeField] private float minSpeed = 1.5f;
        [Tooltip("A hit this fast (metres per second) or faster gives a full jiggle; slower ones less.")]
        [Min(0.1f)]
        [SerializeField] private float fullSpeed = 6f;
        [Tooltip("How much a full hit squashes the part along the hit: 0.3 = 30% flatter (and wider, so it keeps its " +
                 "volume). It then springs back out longer, a little less each wobble.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float squash = 0.3f;
        [Tooltip("How far a full hit rocks the part away from the hit, in degrees.")]
        [Range(0f, 45f)]
        [SerializeField] private float tilt = 15f;
        [Tooltip("Wobbles per second.")]
        [Range(1f, 12f)]
        [SerializeField] private float wobblesPerSecond = 5f;
        [Tooltip("Seconds until it has (almost) settled.")]
        [Range(0.2f, 3f)]
        [SerializeField] private float settleTime = 0.7f;

        // As in Jiggle: the rocking a little slower than the squashing, quick hits add up to a limit.
        private const float TiltSpeed = 0.8f;
        private const float MaxSquash = 0.6f;
        private const float MaxStep = 1f / 240f;
        private const float MostHits = 1.25f;
        // How much of the squash a part holding other parts takes as an even shrink (0.3 squash = 15% smaller).
        private const float EvenShare = 0.5f;

        private class Spring
        {
            public Transform bone;
            public Vector3 centre;
            // Its children that hold other parts: kept where the animation put them.
            public Transform[] keep;
            // The bone's own axis it squashes along (the one closest to the hit's direction).
            public int axis;
            // Squash: + longer, - flatter. Tilt: a world-space axis whose length is the angle in degrees.
            public float amount, speed;
            public Vector3 tiltAmount, tiltSpeed;
            public bool moving;
        }

        // A bone it moved this frame: the pose before, and the pose it left. If nothing (no animation) has moved it by
        // the next frame, it goes back first, so jiggles don't pile up.
        private struct Touch
        {
            public Transform t;
            public Vector3 position, scale, wrotePosition, wroteScale;
            public Quaternion rotation, wroteRotation;
        }

        private readonly Dictionary<Transform, Spring> springs = new();
        private readonly List<Touch> touched = new();
        private readonly List<Vector3> keptAt = new();
        private readonly List<Quaternion> keptTurn = new();
        private Game.Combat.EnemyHitDamage hits;

        private void Awake()
        {
            TryGetComponent(out hits);
            var named = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) named.TryAdd(t.name, t);
            var bones = new HashSet<Transform>();
            foreach (var part in parts)
            {
                if (named.TryGetValue(part.bone ?? "", out var bone)) bones.Add(bone);
            }
            foreach (var part in parts)
            {
                if (!named.TryGetValue(part.bone ?? "", out var bone) || springs.ContainsKey(bone)) continue;
                var keep = new List<Transform>();
                foreach (Transform child in bone)
                {
                    foreach (var below in child.GetComponentsInChildren<Transform>(true))
                    {
                        if (!bones.Contains(below)) continue;
                        keep.Add(child);
                        break;
                    }
                }
                springs[bone] = new Spring { bone = bone, centre = part.centre, keep = keep.ToArray() };
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0 || Dead || !Game.Combat.HeldThings.ByPlayer(collision)) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed) return;
            var contact = collision.GetContact(0);
            if (contact.thisCollider == null) return;
            // The collider's bone, or the nearest part above it.
            Spring spring = null;
            for (var t = contact.thisCollider.transform; t != null && t != transform && spring == null; t = t.parent)
                springs.TryGetValue(t, out spring);
            if (spring != null) Hit(spring, contact.point, Mathf.Min(speed / fullSpeed, MostHits));
        }

        private bool Dead => hits != null && hits.IsDead;

        private void Hit(Spring spring, Vector3 at, float strength)
        {
            var bone = spring.bone;
            var middle = bone.TransformPoint(spring.centre);
            var into = middle - at;
            if (into.sqrMagnitude < 0.000001f) into = middle - (Camera.main != null ? Camera.main.transform.position : at - transform.forward);
            into.Normalize();

            // Flattened along whichever of its own axes is closest to the hit (kept while it's still wobbling).
            if (!spring.moving)
            {
                float x = Mathf.Abs(Vector3.Dot(bone.right, into)), y = Mathf.Abs(Vector3.Dot(bone.up, into)), z = Mathf.Abs(Vector3.Dot(bone.forward, into));
                spring.axis = x >= y && x >= z ? 0 : y >= z ? 1 : 2;
            }
            float omega = 2f * Mathf.PI * wobblesPerSecond;
            float most = squash * omega * MostHits;
            spring.speed = Mathf.Clamp(spring.speed - squash * strength * omega, -most, most);
            // Its top tips away from the hit.
            var axis = Vector3.Cross(Vector3.up, into);
            if (axis.sqrMagnitude > 0.0001f)
            {
                float push = tilt * omega * TiltSpeed;
                spring.tiltSpeed = Vector3.ClampMagnitude(spring.tiltSpeed + axis.normalized * (push * strength), push * MostHits);
            }
            spring.moving = true;
        }

        private void LateUpdate()
        {
            for (int i = 0; i < touched.Count; i++)
            {
                var was = touched[i];
                if (was.t == null) continue;
                if (was.t.localPosition == was.wrotePosition && was.t.localRotation == was.wroteRotation && was.t.localScale == was.wroteScale)
                {
                    was.t.localPosition = was.position;
                    was.t.localRotation = was.rotation;
                    was.t.localScale = was.scale;
                }
            }
            touched.Clear();

            bool dead = Dead;
            float omega = 2f * Mathf.PI * wobblesPerSecond;
            float tiltOmega = omega * TiltSpeed;
            float damping = 2f * Mathf.Min(1f, 3f / (omega * settleTime)) * omega;
            float tiltDamping = 2f * Mathf.Min(1f, 3f / (tiltOmega * settleTime)) * tiltOmega;
            foreach (var spring in springs.Values)
            {
                if (!spring.moving) continue;
                if (dead || spring.bone == null)
                {
                    Stop(spring);
                    continue;
                }
                // Small steps keep the spring steady on a slow frame.
                float left = Mathf.Min(Time.deltaTime, 0.1f);
                while (left > 0f)
                {
                    float dt = Mathf.Min(left, MaxStep);
                    left -= dt;
                    spring.speed += (-omega * omega * spring.amount - damping * spring.speed) * dt;
                    spring.amount = Mathf.Clamp(spring.amount + spring.speed * dt, -MaxSquash, MaxSquash);
                    spring.tiltSpeed += (-tiltOmega * tiltOmega * spring.tiltAmount - tiltDamping * spring.tiltSpeed) * dt;
                    spring.tiltAmount += spring.tiltSpeed * dt;
                }
                spring.tiltAmount = Vector3.ClampMagnitude(spring.tiltAmount, 60f);
                if (Mathf.Abs(spring.amount) < 0.001f && Mathf.Abs(spring.speed) < 0.01f &&
                    spring.tiltAmount.sqrMagnitude < 0.01f && spring.tiltSpeed.sqrMagnitude < 0.25f)
                {
                    Stop(spring);
                    continue;
                }
                Apply(spring);
            }

            // What it left, to tell next frame whether anything has moved it since.
            for (int i = 0; i < touched.Count; i++)
            {
                var was = touched[i];
                was.wrotePosition = was.t.localPosition;
                was.wroteRotation = was.t.localRotation;
                was.wroteScale = was.t.localScale;
                touched[i] = was;
            }
        }

        private static void Stop(Spring spring)
        {
            spring.moving = false;
            spring.amount = spring.speed = 0f;
            spring.tiltAmount = spring.tiltSpeed = Vector3.zero;
        }

        private void Apply(Spring spring)
        {
            var bone = spring.bone;
            var middle = bone.TransformPoint(spring.centre);
            keptAt.Clear();
            keptTurn.Clear();
            foreach (var child in spring.keep)
            {
                keptAt.Add(child.position);
                keptTurn.Add(child.rotation);
                Note(child);
            }
            Note(bone);

            // Longer is thinner and flatter is wider, so it keeps its volume; a part holding others shrinks and swells.
            float length = 1f + spring.amount;
            float even = 1f + spring.amount * EvenShare;
            var shape = spring.keep.Length > 0 ? Vector3.one * even : Vector3.one / Mathf.Sqrt(length);
            if (spring.keep.Length == 0) shape[spring.axis] = length;
            bone.localScale = Vector3.Scale(bone.localScale, shape);
            float angle = spring.tiltAmount.magnitude;
            if (angle > 0.0001f) bone.rotation = Quaternion.AngleAxis(angle, spring.tiltAmount / angle) * bone.rotation;
            // Around the part's middle, not the bone's root.
            bone.position += middle - bone.TransformPoint(spring.centre);

            // The other parts hanging off it stay put, its swell taken back off them.
            for (int i = 0; i < spring.keep.Length; i++)
            {
                var child = spring.keep[i];
                child.SetPositionAndRotation(keptAt[i], keptTurn[i]);
                child.localScale /= even;
            }
        }

        // Notes the pose a bone had before it's first moved this frame.
        private void Note(Transform t)
        {
            for (int i = 0; i < touched.Count; i++)
            {
                if (touched[i].t == t) return;
            }
            touched.Add(new Touch { t = t, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
        }
    }
}
