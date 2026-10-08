using System;
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
    /// it as a hard hit. Scripts that handle hits themselves (IHittable, e.g. the PlayGround's ghost tree, bush and balloons)
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
            if (!inFlight || exploded || Blast.IsPlayer(collision.collider)) return;

            if (Time.time - releasedAt <= ThrowWindow) throwSpeed = Mathf.Max(throwSpeed, collision.relativeVelocity.magnitude);
            inFlight = false;
            if (throwSpeed >= minThrowSpeed) Explode(collision.contactCount > 0 ? collision.GetContact(0).point : body.position);
        }

        private void Explode(Vector3 point)
        {
            exploded = true;

            if (thrower != null && rumbleStrength > 0f) thrower.SendHapticImpulse(rumbleStrength, rumbleDuration);
            Blast.Explode(point, radius, damage, push, body, effects, sound, volume);

            // Breaking the usual way lets its own Health FSM drop the pieces and remove it; otherwise it just vanishes.
            if (breakApart && Blast.HasHealth(gameObject, out var ownHealth, out _) && ownHealth != null) ownHealth.Value = 0f;
            else Destroy(gameObject);
        }
    }
}
