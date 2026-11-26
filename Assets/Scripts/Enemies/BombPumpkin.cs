using Game.Weapons;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The bad chase pumpkin (Bad_Pumpkin_3, 5, 7; user 2026-11-26: "50 50 chance to spawn pumpkin 3 5 7 or
    /// Bad_pumpkin_3 5 7. Those bad pumpkin 3 5 7 instead of dropping candies, they drop a bunch of spiders and Pumpkin
    /// only have 1 health, mean when Player get near them pumpkin, they'd explode and drop a lot of spiders").
    /// It looks and moves like the good one, but its touch FSM is off: for Arm Delay after it appears nothing sets it
    /// off (one that lands on the player waits), then the player's head within Trigger Range lights the fuse: it shakes for Fuse Seconds with a rattle and a beep, then goes off: a bang (no harm to the player: the
    /// spiders are the punishment), its health to 0 (its Bug Horde lets the spiders out) and its Health FSM straight to
    /// Drop State (Drop_Crack: its candy-free pieces, the GAMESTAGES bool Pumpkin_3 / Pumpkin_5, or Pumpkin_7's skulls),
    /// so the game goes on exactly as after the good one. Lost health any other way (a Fall Edge, Lost Out Of Room)
    /// goes off at once the same way. Tools > 429 Game > Enemies > Set Up Bad Pumpkins makes the prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public class BombPumpkin : MonoBehaviour
    {
        [Header("Going off")]
        [Tooltip("It can't go off for this many seconds after it appears (it may land right next to the player).")]
        [Min(0f)]
        [SerializeField] private float armDelay = 1.5f;
        [Tooltip("It lights its fuse when the player's head is this close, in metres, measured flat along the floor.")]
        [Min(0.1f)]
        [SerializeField] private float triggerRange = 1.3f;
        [Tooltip("...and the head is at most this many metres higher or lower than the pumpkin.")]
        [Min(0.1f)]
        [SerializeField] private float triggerHeight = 2.2f;
        [Tooltip("How long it shakes before it goes off (0 = at once). Backing off doesn't stop it.")]
        [Min(0f)]
        [SerializeField] private float fuseSeconds = 0.6f;

        [Header("Shaking")]
        [Min(1f)]
        [SerializeField] private float kicksPerSecond = 11f;
        [Tooltip("Each kick hops it up this fast, in m/s.")]
        [Min(0f)]
        [SerializeField] private float hopSpeed = 0.9f;
        [Tooltip("Each kick tilts it this fast, in radians a second.")]
        [Min(0f)]
        [SerializeField] private float tiltSpeed = 2f;
        [Tooltip("Plays on the pumpkin while it shakes (the box bombs' rattle).")]
        [SerializeField] private AudioClip rattle;
        [Range(0f, 1f)]
        [SerializeField] private float rattleVolume = 0.8f;
        [Tooltip("Plays when the fuse is lit (the box bombs' beep).")]
        [SerializeField] private AudioClip beep;
        [Range(0f, 1f)]
        [SerializeField] private float beepVolume = 0.8f;

        [Header("Bang")]
        [Tooltip("Its effects, played from the Effect Pool (the box bombs' Smoke_Flash_Small_Puff, Flash_round_yellow).")]
        [SerializeField] private GameObject[] effects = System.Array.Empty<GameObject>();
        [Min(0.01f)]
        [SerializeField] private float effectSize = 1f;
        [SerializeField] private AudioClip bangSound;
        [Range(0f, 1f)]
        [SerializeField] private float bangVolume = 0.9f;
        [Tooltip("Loose things within this many metres are pushed away (not the player, not held things).")]
        [Min(0f)]
        [SerializeField] private float pushRadius = 1.5f;
        [Tooltip("How fast they're pushed, in m/s (heavy things much less, by Push Weight).")]
        [Min(0f)]
        [SerializeField] private float pushSpeed = 2f;

        [Header("PlayMaker")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("The Health FSM state that breaks it and does its job for the game (skips the laugh before it).")]
        [SerializeField] private string dropState = "Drop_Crack";
        [Tooltip("The FSM that takes health on the player's touch: switched off (coming near lights the fuse instead).")]
        [SerializeField] private string touchFsm = "Damage";

        private PlayMakerFSM fsm;
        private FsmFloat health;
        private Rigidbody body;
        private AudioSource speaker;
        private float bornAt;
        private float fuseAt = -1f;
        private float nextKick;
        private bool done;
        private static readonly Collider[] Overlap = new Collider[48];

        /// <summary>True once it has gone off.</summary>
        public bool WentOff => done;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            foreach (var f in GetComponents<PlayMakerFSM>())
            {
                if (f.FsmName != healthFsm) continue;
                fsm = f;
                health = f.FsmVariables.FindFsmFloat(healthVariable);
                break;
            }
            // Its touch (the Damage FSM: a touch took its 1 health at once, with no warning) is switched off for good:
            // coming near lights the fuse instead, so it always shakes and beeps before it goes off.
            foreach (var f in GetComponents<PlayMakerFSM>())
            {
                if (f.FsmName == touchFsm) f.enabled = false;
            }
            bornAt = Time.time;
            foreach (var effect in effects)
            {
                if (effect != null) EffectPool.Warm(effect, 1);
            }
        }

        private void Update()
        {
            if (done || health == null) return;
            // Its health ran out some other way (a fall): it goes off now.
            if (health.Value <= 0f)
            {
                GoOff();
                return;
            }
            if (fuseAt >= 0f)
            {
                Shake();
                if (Time.time >= fuseAt + fuseSeconds) GoOff();
                return;
            }
            if (Time.time < bornAt + armDelay || Game.Menus.PauseMenu.IsPaused || Game.Saving.Checkpoints.IsDying) return;
            var head = SlimeKit.Head;
            if (head == null) return;
            var to = head.position - transform.position;
            if (Mathf.Abs(to.y) > triggerHeight) return;
            to.y = 0f;
            if (to.sqrMagnitude > triggerRange * triggerRange) return;
            LightFuse();
        }

        // Bumped by the player (body or hands, tagged Player) after the Arm Delay: the fuse lights too, wherever the
        // head is (a pumpkin lying low or high still answers a player who runs into it).
        private void OnCollisionEnter(Collision collision) => Bumped(collision.collider);
        private void OnTriggerEnter(Collider other) => Bumped(other);

        private void Bumped(Collider other)
        {
            if (done || fuseAt >= 0f || health == null || Time.time < bornAt + armDelay || other == null) return;
            if (!other.CompareTag("Player") && !(other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))) return;
            LightFuse();
        }

        private void LightFuse()
        {
            fuseAt = Time.time;
            if (fuseSeconds <= 0f)
            {
                GoOff();
                return;
            }
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.spatialBlend = 1f;
            speaker.minDistance = 1.5f;
            speaker.maxDistance = 20f;
            speaker.dopplerLevel = 0f;
            if (TryGetComponent(out AudioSource like) && like != speaker) speaker.outputAudioMixerGroup = like.outputAudioMixerGroup;
            if (beep != null) speaker.PlayOneShot(beep, beepVolume);
            if (rattle != null)
            {
                speaker.clip = rattle;
                speaker.volume = rattleVolume;
                speaker.loop = true;
                speaker.Play();
            }
        }

        private void Shake()
        {
            if (body == null || body.isKinematic || Time.time < nextKick) return;
            nextKick = Time.time + 1f / kicksPerSecond;
            if (body.linearVelocity.y < hopSpeed * 0.5f) body.AddForce(Vector3.up * hopSpeed, ForceMode.VelocityChange);
            var axis = Random.insideUnitCircle;
            if (axis.sqrMagnitude < 0.0001f) axis = Vector2.right;
            axis.Normalize();
            body.angularVelocity = new Vector3(axis.x, 0f, axis.y) * tiltSpeed;
        }

        private void GoOff()
        {
            if (done) return;
            done = true;
            if (speaker != null) speaker.Stop();
            var centre = body != null ? body.worldCenterOfMass : transform.position;
            // A bang that doesn't hurt anyone (no damage, no player event): the spiders are what hurts.
            Blast.Explode(centre, Mathf.Max(0.1f, pushRadius), 0f, 0f, body, effects, bangSound, bangVolume, null, false,
                effectSize, -1f, false);
            Push(centre);
            // Health 0: its Bug Horde lets the spiders out. Then the Health FSM breaks it and does its job for the game.
            health.Value = 0f;
            if (fsm != null && !string.IsNullOrEmpty(dropState) && fsm.Fsm.ActiveStateName != dropState &&
                fsm.Fsm.GetState(dropState) != null)
            {
                fsm.Fsm.SetState(dropState);
            }
        }

        private void Push(Vector3 centre)
        {
            if (pushSpeed <= 0f || pushRadius <= 0f) return;
            int count = Physics.OverlapSphereNonAlloc(centre, pushRadius, Overlap, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            var pushed = new System.Collections.Generic.HashSet<Rigidbody>();
            for (int i = 0; i < count; i++)
            {
                var other = Overlap[i].attachedRigidbody;
                if (other == null || other == body || other.isKinematic || Blast.IsPlayer(Overlap[i]) || !pushed.Add(other)) continue;
                if (other.TryGetComponent(out UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable held) && held.isSelected) continue;
                float share = Game.Combat.PushWeight.Share(other);
                if (share <= 0f) continue;
                other.AddExplosionForce(pushSpeed * share, centre, pushRadius, 0.4f, ForceMode.VelocityChange);
            }
        }
    }
}
