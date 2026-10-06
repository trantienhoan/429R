using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// Turns a weapon into a grenade: thrown hard enough, it explodes on the first thing it hits. Everything in the
    /// blast that can be hit loses health, loose objects are pushed away, the hand that threw it buzzes, and the
    /// weapon is gone (with Break Apart, it shatters into its pieces, like a bottle). Dropped or tossed gently, it stays
    /// a normal weapon. Goes on the weapon's root, next to its
    /// XR Grab Interactable; Tools > 429 Game > Weapons > Make Flip-Flops Explode adds it.
    /// "Can be hit" means what the game's PlayMaker damage uses: a "Health" FSM (or "SpiderHealth", "BeeHealth", ...)
    /// with a 'health' float, next to a "Damage" FSM. Objects without a Damage FSM (story objects, minigame
    /// counters) and the player are left alone. Things with a Damage FSM but no health of their own (the whack-a-mole
    /// pumpkins, whose barrels count the hits) are sent the event "Blast"; a Damage FSM with a Blast transition takes
    /// it as a hard hit. Scripts that handle hits themselves (IHittable, e.g. the PlayGround's ghost tree and bush)
    /// are hit with the damage first, even when they also have a Health FSM (e.g. for the fairy's shield pop); the ghost
    /// tree passes it on to that FSM's health.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Grenade : MonoBehaviour
    {
        [Header("Throw")]
        [Tooltip("It only explodes if it leaves your hand at least this fast, in metres per second. Your hand's speed is " +
                 "multiplied by the grab's Throw Velocity Scale (6 on the flip-flops), so 8 takes a quick flick, not a drop.")]
        [Min(0f)]
        [SerializeField] private float minThrowSpeed = 8f;

        [Header("Blast")]
        [Tooltip("How far the blast reaches, in metres.")]
        [Min(0.1f)]
        [SerializeField] private float radius = 1.5f;
        [Tooltip("Health everything in the blast loses.")]
        [Min(0f)]
        [SerializeField] private float damage = 3f;
        [Tooltip("How fast loose objects in the middle of the blast are pushed away, in metres per second; the same for " +
                 "light and heavy things, less further out. 0 = no push.")]
        [Min(0f)]
        [SerializeField] private float push = 4f;
        [Tooltip("On: exploding breaks it the way it normally breaks (its Health FSM drops its pieces, like a bottle's " +
                 "glass). Off: it just vanishes.")]
        [SerializeField] private bool breakApart;

        [Header("Effects")]
        [Tooltip("Particle effects that appear where it explodes.")]
        [SerializeField] private GameObject[] effects = Array.Empty<GameObject>();
        [SerializeField] private AudioClip sound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;
        [Tooltip("How hard the controller that threw it buzzes (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float rumbleStrength = 1f;
        [Tooltip("How long the controller that threw it buzzes, in seconds.")]
        [Min(0f)]
        [SerializeField] private float rumbleDuration = 0.3f;

        // XR Toolkit gives a thrown object its speed at the end of the frame it's let go, so the throw is judged over
        // a short moment after that.
        private const float ThrowWindow = 0.2f;
        // Spawned effects are removed after this many seconds; they don't remove themselves.
        private const float EffectLifetime = 4f;
        // Sent to things in the blast that have a Damage FSM but no health (see the class summary).
        private const string BlastEvent = "Blast";

        private static readonly Collider[] hits = new Collider[256];
        private static readonly HashSet<GameObject> hurt = new();
        private static readonly HashSet<Rigidbody> pushed = new();
        private static readonly List<PlayMakerFSM> fsms = new();

        private XRGrabInteractable grab;
        private Rigidbody body;
        private XRBaseInputInteractor thrower;
        private bool inFlight;
        private float releasedAt;
        private float throwSpeed;
        private bool exploded;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnLetGo);
        }

        private void OnDestroy()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnLetGo);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            inFlight = false;
        }

        private void OnLetGo(SelectExitEventArgs args)
        {
            // Passed to the other hand, not thrown.
            if (grab.isSelected) return;

            thrower = args.interactorObject as XRBaseInputInteractor;
            inFlight = true;
            releasedAt = Time.time;
            throwSpeed = 0f;
        }

        private void FixedUpdate()
        {
            if (inFlight && body != null && Time.time - releasedAt <= ThrowWindow)
                throwSpeed = Mathf.Max(throwSpeed, body.linearVelocity.magnitude);
        }

        // The first thing it hits after leaving the hand decides: thrown hard, it explodes; otherwise it's a weapon again.
        private void OnCollisionEnter(Collision collision)
        {
            if (!inFlight || exploded || IsPlayer(collision.collider)) return;

            if (Time.time - releasedAt <= ThrowWindow) throwSpeed = Mathf.Max(throwSpeed, collision.relativeVelocity.magnitude);
            inFlight = false;
            if (throwSpeed >= minThrowSpeed) Explode(collision.contactCount > 0 ? collision.GetContact(0).point : body.position);
        }

        private void Explode(Vector3 point)
        {
            exploded = true;

            foreach (var effect in effects)
            {
                if (effect != null) Destroy(Instantiate(effect, point, Quaternion.identity), EffectLifetime);
            }
            if (sound != null) AudioSource.PlayClipAtPoint(sound, point, volume);
            if (thrower != null && rumbleStrength > 0f) thrower.SendHapticImpulse(rumbleStrength, rumbleDuration);

            hurt.Clear();
            pushed.Clear();
            int count = Physics.OverlapSphereNonAlloc(point, radius, hits, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.attachedRigidbody == body || IsPlayer(hit)) continue;

                Hurt(hit.transform, point);
                Push(hit.attachedRigidbody, point);
            }

            // Breaking the usual way lets its own Health FSM drop the pieces and remove it; otherwise it just vanishes.
            if (breakApart && HasHealth(gameObject, out var ownHealth, out _) && ownHealth != null) ownHealth.Value = 0f;
            else Destroy(gameObject);
        }

        // A script that handles hits itself (IHittable, e.g. the ghost tree or bush) is hit first. Otherwise it takes
        // 'damage' from the health its own Damage FSM would lower, so breaking, drops and GAMESTAGES flags happen as
        // usual. Colliders are often on children, so the nearest object upwards with a health FSM is the one hit. With
        // no health anywhere upwards, the nearest Damage FSM is sent "Blast".
        private void Hurt(Transform part, Vector3 point)
        {
            var hittable = part.GetComponentInParent<Game.Combat.IHittable>();
            if (hittable is Component target)
            {
                if (hurt.Add(target.gameObject)) hittable.Hit(damage, point);
                return;
            }

            PlayMakerFSM nearestDamageFsm = null;
            for (var t = part; t != null; t = t.parent)
            {
                if (!HasHealth(t.gameObject, out var health, out var damageFsm))
                {
                    if (nearestDamageFsm == null) nearestDamageFsm = damageFsm;
                    continue;
                }

                if (health != null && hurt.Add(t.gameObject)) health.Value -= damage;
                return;
            }

            if (nearestDamageFsm != null && hurt.Add(nearestDamageFsm.gameObject)) nearestDamageFsm.SendEvent(BlastEvent);
        }

        // Whether it has a health FSM, and if it can be hit (a Damage FSM next to it), the health to lower.
        // Also hands back its Damage FSM, if it has one.
        private static bool HasHealth(GameObject target, out HutongGames.PlayMaker.FsmFloat health, out PlayMakerFSM damageFsm)
        {
            health = null;
            damageFsm = null;
            target.GetComponents(fsms);
            PlayMakerFSM healthFsm = null;
            foreach (var fsm in fsms)
            {
                string name = fsm.FsmName;
                if (name == "Damage") damageFsm = fsm;
                else if (name.EndsWith("Health", StringComparison.OrdinalIgnoreCase)) healthFsm = fsm;
            }
            if (healthFsm == null) return false;

            // The player's and the fairy's health are never touched.
            if (damageFsm != null && healthFsm.FsmName != "PlayerHealth" && healthFsm.FsmName != "FairyHealth")
                health = healthFsm.FsmVariables.GetFsmFloat("health");
            return true;
        }

        private void Push(Rigidbody target, Vector3 point)
        {
            if (push <= 0f || target == null || target.isKinematic || !pushed.Add(target)) return;
            // Things in someone's hand stay there.
            if (target.TryGetComponent<XRGrabInteractable>(out var held) && held.isSelected) return;

            target.AddExplosionForce(push, point, radius, 0.5f, ForceMode.VelocityChange);
        }

        // The player's body, head and hands: everything under the XR Origin.
        private static bool IsPlayer(Collider collider)
        {
            return collider.GetComponentInParent<XROrigin>() != null;
        }
    }
}
