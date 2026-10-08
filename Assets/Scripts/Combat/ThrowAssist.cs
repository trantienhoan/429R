using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Gaze;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Combat
{
    /// <summary>
    /// Lets a thrown object turn the Throw Assist off for that throw, e.g. No Throw Assist, or the bowling ball when
    /// it's bowled.
    /// </summary>
    public interface IThrowAssistOptOut
    {
        bool OptOutOfThrowAssist { get; }
    }

    /// <summary>
    /// Helps the player's throws hit: a throw that already flies close to an enemy is bent the rest of the way, with the
    /// arc worked out so it lands on it. Targets are everything tagged Enemy or Monster, plus anything with a Throw
    /// Assist Target (the ghost balloons). Goes on the XR Origin. XRI asks it once, at the moment something leaves the
    /// hand, for the speed to throw it at (XRI's own throw hook), so it costs nothing while nothing is being thrown.
    /// Slow drops, and objects with No Throw Assist, fly exactly as thrown. Untick it (or disable it from an FSM) to
    /// turn it off.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThrowAssist : MonoBehaviour, IXRAimAssist
    {
        [Tooltip("How much of the miss it fixes: 0 = off, 0.5 = half of it, 1 = every helped throw lands on target.")]
        [Range(0f, 1f)]
        [SerializeField] private float strength = 0.8f;
        [Tooltip("It only helps a throw that already flies within this many degrees of a target. Bigger = more forgiving.")]
        [Range(0f, 45f)]
        [SerializeField] private float maxAngle = 15f;
        [Tooltip("Throws slower than this, in metres per second, are left alone, so drops and gentle tosses land where " +
                 "they're put.")]
        [Min(0f)]
        [SerializeField] private float minSpeed = 2.5f;
        [Tooltip("Targets further than this, in metres, are left out.")]
        [Min(1f)]
        [SerializeField] private float maxDistance = 15f;
        [Tooltip("A throw too weak to reach the target is sped up by at most this much (0.3 = 30% faster). Weaker " +
                 "throws are left alone.")]
        [Range(0f, 1f)]
        [SerializeField] private float maxExtraSpeed = 0.3f;
        [Tooltip("Objects with these tags are targets.")]
        [SerializeField] private string[] targetTags = { "Enemy", "Monster" };

        private const float MinDistance = 0.75f;

        private readonly List<XRBaseInteractor> interactors = new();
        private readonly List<Rigidbody> released = new();
        private int releasedFrame = -1;

        private void OnEnable()
        {
            GetComponentsInChildren(true, interactors);
            foreach (var interactor in interactors) interactor.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            foreach (var interactor in interactors)
            {
                if (interactor != null) interactor.selectExited.RemoveListener(OnReleased);
            }
            interactors.Clear();
        }

        // Remembers what just left a hand, so the throw below knows what it's throwing. XRI throws it later in the
        // same frame.
        private void OnReleased(SelectExitEventArgs args)
        {
            if (releasedFrame != Time.frameCount)
            {
                released.Clear();
                releasedFrame = Time.frameCount;
            }
            if (args.interactableObject is Component thing && thing.TryGetComponent(out Rigidbody body)) released.Add(body);
        }

        public Vector3 GetAssistedVelocity(in Vector3 source, in Vector3 velocity, float gravity)
        {
            return GetAssistedVelocity(source, velocity, gravity, maxAngle);
        }

        public Vector3 GetAssistedVelocity(in Vector3 source, in Vector3 velocity, float gravity, float maxAngle)
        {
            var thrown = TakeReleased(source);
            float speed = velocity.magnitude;
            if (!isActiveAndEnabled || strength <= 0f || speed < Mathf.Max(minSpeed, 0.1f) || OptsOut(thrown)) return velocity;

            // Of the targets the throw is close to, the one it's closest to wins.
            bool found = false;
            var best = velocity;
            float bestAngle = maxAngle;
            foreach (var tag in targetTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                GameObject[] tagged;
                try
                {
                    tagged = GameObject.FindGameObjectsWithTag(tag);
                }
                catch (UnityException)
                {
                    continue; // a tag that isn't in the project
                }
                foreach (var target in tagged)
                {
                    if (IsPartOf(target.transform, thrown)) continue;
                    Consider(source, velocity, speed, gravity, MiddleOf(target), VelocityOf(target), ref found, ref best, ref bestAngle);
                }
            }
            foreach (var target in ThrowAssistTarget.All)
            {
                if (IsPartOf(target.transform, thrown)) continue;
                Consider(source, velocity, speed, gravity, target.AimPoint, VelocityOf(target.gameObject), ref found, ref best, ref bestAngle);
            }
            if (!found) return velocity;

            var direction = Vector3.Slerp(velocity / speed, best.normalized, strength);
            return direction * Mathf.Lerp(speed, best.magnitude, strength);
        }

        private void Consider(Vector3 source, Vector3 velocity, float speed, float gravity, Vector3 point, Vector3 pointVelocity,
            ref bool found, ref Vector3 best, ref float bestAngle)
        {
            if (!Solve(source, point, pointVelocity, speed, gravity, out var ideal)) return;
            float angle = Vector3.Angle(velocity, ideal);
            if (angle > bestAngle) return;
            found = true;
            best = ideal;
            bestAngle = angle;
        }

        // The throw that lands on 'point' at about the same speed: the low arc, or if it's too slow for that, the
        // slowest throw that gets there (if that's not much faster). Leads a moving target by its flight time.
        private bool Solve(Vector3 source, Vector3 point, Vector3 pointVelocity, float speed, float gravity, out Vector3 ideal)
        {
            ideal = Vector3.zero;
            var toTarget = point - source;
            if (pointVelocity.sqrMagnitude > 0.01f) toTarget += pointVelocity * (toTarget.magnitude / speed);
            float distance = toTarget.magnitude;
            if (distance < MinDistance || distance > maxDistance) return false;
            if (gravity <= 0.01f)
            {
                ideal = toTarget / distance * speed;
                return true;
            }

            var flat = new Vector3(toTarget.x, 0f, toTarget.z);
            float x = flat.magnitude, y = toTarget.y;
            if (x < 0.1f) return false; // straight up or down
            float v2 = speed * speed;
            float root = v2 * v2 - gravity * (gravity * x * x + 2f * y * v2);
            if (root < 0f)
            {
                v2 = gravity * (y + Mathf.Sqrt(x * x + y * y));
                float most = speed * (1f + maxExtraSpeed);
                if (v2 > most * most) return false;
                root = 0f;
            }
            float angle = Mathf.Atan((v2 - Mathf.Sqrt(root)) / (gravity * x));
            float v = Mathf.Sqrt(v2);
            ideal = flat / x * (v * Mathf.Cos(angle)) + Vector3.up * (v * Mathf.Sin(angle));
            return true;
        }

        private Rigidbody TakeReleased(Vector3 source)
        {
            if (releasedFrame != Time.frameCount || released.Count == 0) return null;
            int closest = 0;
            for (int i = 1; i < released.Count; i++)
            {
                if (released[i] == null) continue;
                if (released[closest] == null ||
                    (released[i].position - source).sqrMagnitude < (released[closest].position - source).sqrMagnitude) closest = i;
            }
            var body = released[closest];
            released.RemoveAt(closest);
            return body;
        }

        private static bool OptsOut(Rigidbody thrown)
        {
            if (thrown == null) return false;
            foreach (var optOut in thrown.GetComponents<IThrowAssistOptOut>())
            {
                if (optOut.OptOutOfThrowAssist) return true;
            }
            return false;
        }

        private static bool IsPartOf(Transform target, Rigidbody thrown)
        {
            return thrown != null && target.IsChildOf(thrown.transform);
        }

        /// <summary>The middle of the object's collider, or where it is if it has none.</summary>
        public static Vector3 MiddleOf(GameObject target)
        {
            return target.TryGetComponent(out Collider collider) && collider.enabled ? collider.bounds.center : target.transform.position;
        }

        private static Vector3 VelocityOf(GameObject target)
        {
            var body = target.TryGetComponent(out Collider collider) ? collider.attachedRigidbody : target.GetComponentInParent<Rigidbody>();
            return body != null && !body.isKinematic ? body.linearVelocity : Vector3.zero;
        }
    }
}
