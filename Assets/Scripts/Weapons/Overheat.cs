using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.Weapons
{
    /// <summary>
    /// Stops a gun being spammed. Fire it Shots times within Seconds and it overheats: it beeps, shakes, puffs smoke and
    /// buzzes the hand for Fuse seconds (time to throw it away!), then explodes like a grenade. Enemies in the blast are
    /// hurt, the player too if they still hold it or stand close, and the gun breaks into its pieces. The last shots
    /// before that buzz the hand as a warning. Counts trigger pulls (the grab's Activate). Goes on the gun's root, next
    /// to its XR Grab Interactable; Tools > 429 Game > Weapons > Make Boong Gun Overheat adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Overheat : MonoBehaviour
    {
        [Header("Overheat")]
        [Tooltip("This many shots...")]
        [Min(2)]
        [SerializeField] private int shots = 7;
        [Tooltip("...fired within this many seconds overheat it.")]
        [Min(0.1f)]
        [SerializeField] private float seconds = 3f;
        [Tooltip("Once this many shots have been fired within Seconds, each shot buzzes the hand: it's getting hot. " +
                 "0 = no warning.")]
        [Min(0)]
        [SerializeField] private int warnFrom = 5;
        [Tooltip("Seconds from overheating to exploding: time to throw it away.")]
        [Min(0f)]
        [SerializeField] private float fuse = 1.5f;

        [Header("About to blow")]
        [Tooltip("Optional: the part that shakes, e.g. its model. Empty: nothing shakes.")]
        [SerializeField] private Transform shakePart;
        [Tooltip("How far it shakes, in metres.")]
        [Min(0f)]
        [SerializeField] private float shake = 0.008f;
        [Tooltip("Played over and over until it explodes, e.g. a beep.")]
        [SerializeField] private AudioClip warningSound;
        [Tooltip("Optional: puffed out a few times a second until it explodes, e.g. smoke.")]
        [SerializeField] private GameObject smoke;
        [Tooltip("Size of each puff. The Hyper Casual FX smoke is about 13 m wide, so 0.04 makes it about half a metre.")]
        [Min(0f)]
        [SerializeField] private float smokeScale = 0.04f;

        [Header("Blast")]
        [Tooltip("How far the blast reaches, in metres.")]
        [Min(0.1f)]
        [SerializeField] private float radius = 2f;
        [Tooltip("Health everything in the blast loses (a flip-flop grenade takes 3).")]
        [Min(0f)]
        [SerializeField] private float damage = 5f;
        [Tooltip("How fast loose objects in the middle of the blast are pushed away, in metres per second. 0 = no push.")]
        [Min(0f)]
        [SerializeField] private float push = 5f;
        [Tooltip("Event sent to the player's Damage FSM if they're in the blast (holding it counts): Damage, " +
                 "DamageHeavy, ... Empty: it never hurts the player.")]
        [SerializeField] private string playerEvent = "Damage";
        [Tooltip("On: it breaks the way it normally breaks (its Health FSM drops its pieces). Off: it just vanishes.")]
        [SerializeField] private bool breakApart = true;

        [Header("Effects")]
        [Tooltip("Particle effects that appear where it explodes.")]
        [SerializeField] private GameObject[] effects = Array.Empty<GameObject>();
        [SerializeField] private AudioClip sound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;
        [Tooltip("How hard the hand holding it (or that threw it) buzzes when it explodes (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float rumbleStrength = 1f;
        [Min(0f)]
        [SerializeField] private float rumbleDuration = 0.4f;

        private const string PlayerVariable = "Player";
        private const string PlayerDamageFsm = "Damage";
        private const float PuffEvery = 0.3f;
        private const float BuzzEvery = 0.1f;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private AudioSource speaker;
        private XRBaseInputInteractor lastHand;
        private float[] shotTimes;
        private int nextShot;
        // When it explodes; below 0 while it isn't burning.
        private float blowAt = -1f;
        private float nextBeep, nextPuff, nextBuzz;
        private Vector3 shakeOffset;
        private bool exploded;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            speaker = GetComponent<AudioSource>();
            shotTimes = new float[shots];
            for (int i = 0; i < shotTimes.Length; i++) shotTimes[i] = float.NegativeInfinity;
            grab.activated.AddListener(OnShot);
            grab.selectEntered.AddListener(OnGrabbed);
        }

        private void OnDestroy()
        {
            if (grab == null) return;
            grab.activated.RemoveListener(OnShot);
            grab.selectEntered.RemoveListener(OnGrabbed);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRBaseInputInteractor hand) lastHand = hand;
        }

        private void OnShot(ActivateEventArgs args)
        {
            if (!isActiveAndEnabled || blowAt >= 0f || exploded) return;
            if (args.interactorObject is XRBaseInputInteractor hand) lastHand = hand;

            // The times of the last 'shots' shots: all of them within 'seconds' overheats it.
            shotTimes[nextShot] = Time.time;
            nextShot = (nextShot + 1) % shotTimes.Length;
            int recent = 0;
            foreach (float time in shotTimes)
            {
                if (Time.time - time <= seconds) recent++;
            }

            if (recent >= shotTimes.Length)
            {
                blowAt = Time.time + fuse;
                nextBeep = nextPuff = nextBuzz = Time.time;
            }
            else if (warnFrom > 0 && recent >= warnFrom)
            {
                Buzz(Mathf.Lerp(0.3f, 0.7f, (recent - warnFrom) / (float)Mathf.Max(1, shotTimes.Length - 1 - warnFrom)), 0.1f);
            }
        }

        private void Update()
        {
            if (blowAt < 0f) return;
            StopShaking();

            float now = Time.time;
            if (now >= blowAt)
            {
                Explode();
                return;
            }

            float heat = 1f - (blowAt - now) / Mathf.Max(fuse, 0.01f);
            if (now >= nextBeep && warningSound != null)
            {
                if (speaker != null) speaker.PlayOneShot(warningSound, volume);
                else AudioSource.PlayClipAtPoint(warningSound, transform.position, volume);
                nextBeep = now + Mathf.Max(warningSound.length, 0.2f);
            }
            if (now >= nextPuff && smoke != null)
            {
                var puff = Instantiate(smoke, body.worldCenterOfMass, Quaternion.identity);
                puff.transform.localScale = Vector3.one * smokeScale;
                Destroy(puff, 2f);
                nextPuff = now + PuffEvery;
            }
            if (now >= nextBuzz)
            {
                Buzz(Mathf.Lerp(0.3f, 1f, heat), BuzzEvery);
                nextBuzz = now + BuzzEvery;
            }
        }

        // Shaken after the Animator has posed it, and put back before it does again.
        private void LateUpdate()
        {
            if (blowAt < 0f || shakePart == null || shake <= 0f) return;
            var parent = shakePart.parent;
            var offset = Random.insideUnitSphere * shake;
            shakeOffset = parent != null ? parent.InverseTransformVector(offset) : offset;
            shakePart.localPosition += shakeOffset;
        }

        private void StopShaking()
        {
            if (shakePart != null) shakePart.localPosition -= shakeOffset;
            shakeOffset = Vector3.zero;
        }

        private void Explode()
        {
            blowAt = -1f;
            exploded = true;
            StopShaking();

            bool playerHurt = Blast.Explode(body.worldCenterOfMass, radius, damage, push, body, effects, sound, volume) || grab.isSelected;
            Buzz(rumbleStrength, rumbleDuration);
            if (playerHurt && !string.IsNullOrEmpty(playerEvent)) SendToPlayer(playerEvent);

            // Breaking the usual way lets its own Health FSM drop the pieces and remove it; otherwise it just vanishes.
            if (breakApart && Blast.HasHealth(gameObject, out var ownHealth, out _) && ownHealth != null) ownHealth.Value = 0f;
            else Destroy(gameObject);
        }

        // The hand holding it, or failing that the last one that did.
        private void Buzz(float strength, float duration)
        {
            if (strength <= 0f) return;
            var hand = grab.isSelected && grab.firstInteractorSelecting is XRBaseInputInteractor holder ? holder : lastHand;
            if (hand != null) hand.SendHapticImpulse(strength, duration);
        }

        private static void SendToPlayer(string eventName)
        {
            var player = FsmVariables.GlobalVariables.FindFsmGameObject(PlayerVariable);
            if (player == null || player.Value == null) return;
            foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == PlayerDamageFsm) fsm.SendEvent(eventName);
            }
        }
    }
}
