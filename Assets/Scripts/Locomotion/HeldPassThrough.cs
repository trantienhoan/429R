using System.Collections.Generic;
using Game.Leveling;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Locomotion
{
    /// <summary>
    /// Keeps a held thing in the hand when it meets something heavier than itself (user, 2026-10-29: a swung weapon
    /// "would be blocked by something heavier and not stay right on my hand ... if the weapon on hand try to move
    /// something heavier than it, it should make the weapon easier to break, but still I want it to stay right in my
    /// hand"). XR Interaction moves a held thing by physics, so a wall, the floor, a heavy table or an enemy's body
    /// pushed it back out of the hand. Now, where a held thing touches something heavier than itself (more mass, a
    /// kinematic body such as an enemy, or the level itself), the touch can't move the held thing (Physics contact
    /// modification): it goes on following the hand. The heavy thing only gets the push the held thing's own weight could
    /// give it (Push Strength), so heavy things move slowly and the level not at all, and the touch still counts as a
    /// hit (damage, the weapon's own wear). Lighter things are knocked about as before.
    /// Driving a weapon into something heavier strains it: Strain Wear for each metre it's pushed in (across the touch,
    /// so sliding along the floor or a wall costs nothing), taken off its Health FSM's health a bite at a time (Weapon
    /// Wear then counts Weapon Efficiency and creaks a worn one). Goes on the XR Origin, next to Held Swing Limit.
    /// </summary>
    // After the XR Interaction Manager (-105) and Held Swing Limit (50) in FixedUpdate: the held things are known for this
    // physics step before it runs.
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public class HeldPassThrough : MonoBehaviour
    {
        [Tooltip("How hard a held thing pushes something heavier: the speed it could give something its own weight in " +
                 "one second of pushing, in metres per second (something ten times as heavy speeds up a tenth as fast).")]
        [Min(0f)]
        [SerializeField] private float pushStrength = 4f;
        [Tooltip("Health a weapon loses for each metre it's driven into something heavier than itself.")]
        [Min(0f)]
        [SerializeField] private float strainWear = 0.5f;

        // Strain is taken off the weapon's health in bites this big, so a worn weapon creaks now and then, not every step.
        private const float WearBite = 0.25f;
        // How far round a held thing others are looked for (beyond its own size), in metres: a fast swing's step.
        private const float Reach = 1f;
        private const string HealthFsm = "Health";
        private const string HealthVariable = "health";

        // Read by the physics threads while a physics step runs; only written here in FixedUpdate, before it does.
        // Rigidbody instance id -> mass: the held things, and the bodies near them (kinematic ones count as endless).
        private static readonly Dictionary<int, float> HeldMass = new();
        private static readonly Dictionary<int, float> OtherMass = new();
        // The most impulse a held thing may give something heavier in one physics step, per kilogram of its own mass.
        private static float pushPerKilo;
        private static int listeners;

        private class Held
        {
            public Rigidbody body;
            public Collider[] colliders;
            public bool[] wasModifiable;
            public FsmFloat health;
            public bool healthLooked;
            public StrainSensor sensor;
            public float strain;
        }

        private readonly List<XRBaseInteractor> interactors = new();
        private readonly Dictionary<Rigidbody, Held> held = new();
        private readonly HashSet<Rigidbody> seen = new();
        private readonly List<Rigidbody> letGo = new();
        private static readonly Collider[] Near = new Collider[128];

        private void OnEnable()
        {
            var origin = GetComponent<XROrigin>();
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            interactors.Clear();
            interactors.AddRange((origin != null ? origin.transform : transform).GetComponentsInChildren<XRBaseInteractor>(true));
            if (listeners++ == 0)
            {
                Physics.ContactModifyEvent += Modify;
                Physics.ContactModifyEventCCD += Modify;
            }
        }

        private void OnDisable()
        {
            foreach (var h in held.Values) Release(h);
            held.Clear();
            HeldMass.Clear();
            OtherMass.Clear();
            if (--listeners == 0)
            {
                Physics.ContactModifyEvent -= Modify;
                Physics.ContactModifyEventCCD -= Modify;
            }
        }

        private void FixedUpdate()
        {
            pushPerKilo = pushStrength * Time.fixedDeltaTime;
            seen.Clear();
            foreach (var interactor in interactors)
            {
                if (interactor == null || !interactor.hasSelection) continue;
                foreach (var selected in interactor.interactablesSelected)
                {
                    if (selected == null || !selected.transform.TryGetComponent<Rigidbody>(out var body) || body.isKinematic) continue;
                    if (seen.Add(body) && !held.ContainsKey(body)) held[body] = Hold(body);
                }
            }
            letGo.Clear();
            foreach (var body in held.Keys)
            {
                if (body == null || !seen.Contains(body)) letGo.Add(body);
            }
            foreach (var body in letGo)
            {
                Release(held[body]);
                held.Remove(body);
            }

            // What the physics step about to run needs: the held things' masses and the masses round them.
            HeldMass.Clear();
            OtherMass.Clear();
            foreach (var h in held.Values)
            {
                HeldMass[h.body.GetInstanceID()] = h.body.mass;
                var bounds = BoundsOf(h);
                int count = Physics.OverlapSphereNonAlloc(bounds.center, bounds.extents.magnitude + Reach, Near, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var other = Near[i].attachedRigidbody;
                    if (other == null || other == h.body) continue;
                    OtherMass[other.GetInstanceID()] = other.isKinematic ? float.PositiveInfinity : other.mass;
                }
                Strain(h);
            }
        }

        private Held Hold(Rigidbody body)
        {
            var h = new Held { body = body, colliders = body.GetComponentsInChildren<Collider>() };
            h.wasModifiable = new bool[h.colliders.Length];
            for (int i = 0; i < h.colliders.Length; i++)
            {
                var c = h.colliders[i];
                if (c == null || c.attachedRigidbody != body) continue;
                h.wasModifiable[i] = c.hasModifiableContacts;
                c.hasModifiableContacts = true;
            }
            if (!body.TryGetComponent(out h.sensor)) h.sensor = body.gameObject.AddComponent<StrainSensor>();
            h.sensor.Begin(this);
            return h;
        }

        private static void Release(Held h)
        {
            if (h.sensor != null) h.sensor.End();
            for (int i = 0; i < h.colliders.Length; i++)
            {
                var c = h.colliders[i];
                if (c != null && c.attachedRigidbody == h.body) c.hasModifiableContacts = h.wasModifiable[i];
            }
        }

        private static Bounds BoundsOf(Held h)
        {
            var bounds = new Bounds(h.body.worldCenterOfMass, Vector3.zero);
            foreach (var c in h.colliders)
            {
                if (c != null && c.enabled && !c.isTrigger) bounds.Encapsulate(c.bounds);
            }
            return bounds;
        }

        // Whether this body is heavier than a held thing of this mass: the level itself (no body), a kinematic body, or
        // more mass. Bodies it didn't look for count as lighter (left as they were).
        private static bool Heavier(int body, float mass) =>
            body == 0 || (OtherMass.TryGetValue(body, out float other) && other > mass);

        // On the physics threads, while the step runs: a held thing touching something heavier can't be moved by it, and
        // pushes it at most as hard as its own weight could.
        private static void Modify(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            if (HeldMass.Count == 0) return;
            for (int p = 0; p < pairs.Length; p++)
            {
                var pair = pairs[p];
                bool first = HeldMass.TryGetValue(pair.bodyInstanceID, out float mass);
                if (!first && !HeldMass.TryGetValue(pair.otherBodyInstanceID, out mass)) continue;
                int other = first ? pair.otherBodyInstanceID : pair.bodyInstanceID;
                // Two held things (one in each hand) touch as ever.
                if (HeldMass.ContainsKey(other) || !Heavier(other, mass)) continue;

                var masses = pair.massProperties;
                if (first) { masses.inverseMassScale = 0f; masses.inverseInertiaScale = 0f; }
                else { masses.otherInverseMassScale = 0f; masses.otherInverseInertiaScale = 0f; }
                pair.massProperties = masses;
                float most = mass * pushPerKilo / Mathf.Max(1, pair.contactCount);
                for (int i = 0; i < pair.contactCount; i++) pair.SetMaxImpulse(i, most);
                pairs[p] = pair;
            }
        }

        // Called by a held thing's Strain Sensor for each touch (after the physics step): how fast it's driven into
        // something heavier, across the touch.
        internal void Touched(Rigidbody body, Collision collision)
        {
            if (!held.TryGetValue(body, out var h)) return;
            var other = collision.rigidbody;
            bool heavier = other == null || other.isKinematic || other.mass > body.mass;
            if (!heavier || collision.contactCount == 0) return;
            float into = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
            h.sensor.step = Mathf.Max(h.sensor.step, into);
        }

        // The last step's strain becomes wear: Strain Wear a metre, a bite at a time.
        private void Strain(Held h)
        {
            if (h.sensor == null) return;
            float into = h.sensor.step;
            h.sensor.step = 0f;
            if (into <= 0f || strainWear <= 0f) return;
            // Only weapons wear (Weapon Wear looks after them once they're picked up).
            if (!h.body.TryGetComponent<WeaponWear>(out _)) return;
            if (!h.healthLooked)
            {
                h.healthLooked = true;
                foreach (var fsm in h.body.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == HealthFsm) h.health = fsm.FsmVariables.FindFsmFloat(HealthVariable);
                }
            }
            if (h.health == null) return;
            h.strain += into * Time.fixedDeltaTime * strainWear;
            if (h.strain < WearBite) return;
            float bite = Mathf.Floor(h.strain / WearBite) * WearBite;
            h.strain -= bite;
            if (h.health.Value > 0f) h.health.Value -= bite;
        }
    }
}
