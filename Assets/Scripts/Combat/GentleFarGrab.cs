using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Combat
{
    /// <summary>
    /// Makes grabbing from a distance gentle. On its own, XRI yanks a far-grabbed object into the hand within a few
    /// hundredths of a second, so from a few metres away it flies at 50-100 m/s: whatever it brushes on the way gets
    /// smashed, and its own Damage FSM takes the bump as a huge hit (a big puff of smoke and lost health). With this,
    /// the grab starts where the object is and it flies to the hand at Pull Speed, slow enough that a bump on the way
    /// doesn't count as a big hit (weapons' Damage FSMs need 20-30 m/s); Throw Power makes it faster, up to Max Pull
    /// Speed. Near grabs are untouched. Goes on the XR Origin; it listens to the hands' interactors, so it costs nothing
    /// while nothing is being pulled.
    /// Ghost on the way (user, 2026-11-20: "disable the grabbed object's collider and only re-activate it when it gets
    /// to Player hand"): it passes through everything solid while it flies to the hand. Not by switching its colliders
    /// off (that counts as leaving the trigger it's in, and things that die on leaving a FIREKILL area would): it's told
    /// to ignore each solid collider near its way (Physics.IgnoreCollision; triggers are left alone), and all of that is
    /// undone once it's in the hand.
    /// Let go on the way ("make it still fly at Player and re-activate its collider in air before it touches the
    /// ground"): it flies on to the player's chest on an arc, still a ghost, and turns solid again once it's near them,
    /// just above the floor, or after a short while.
    /// </summary>
    [DisallowMultipleComponent]
    public class GentleFarGrab : MonoBehaviour
    {
        [Tooltip("A grab from further than this, in metres from the hand, counts as far.")]
        [Min(0.05f)]
        [SerializeField] private float farDistance = 0.4f;
        [Tooltip("How fast a far-grabbed object flies to the hand, in metres per second. The player's Throw Power (a " +
                 "level-up stat) makes it faster, up to Max Pull Speed.")]
        [Min(0.5f)]
        [SerializeField] private float pullSpeed = 6f;
        [Tooltip("The fastest Throw Power makes a pull, in metres per second. Keep it under 15: the Broom's Damage FSM " +
                 "takes a 15 m/s bump as a hit.")]
        [Min(0.5f)]
        [SerializeField] private float maxPullSpeed = 14f;
        [Tooltip("The longest a pull takes, in seconds: things further away fly faster than Pull Speed to make it.")]
        [Min(0.1f)]
        [SerializeField] private float maxPullSeconds = 0.6f;

        // Close enough to the hand to count as arrived, in metres.
        private const float Arrived = 0.02f;

        [Header("Ghost on the way")]
        [Tooltip("It passes through everything solid while it flies to the hand (and after a let-go on the way).")]
        [SerializeField] private bool ghostOnTheWay = true;
        [Tooltip("Let go on the way: it turns solid again this close to the player's head (m)...")]
        [Min(0.1f)]
        [SerializeField] private float solidNearPlayer = 0.9f;
        [Tooltip("...or this high over the floor under it (m, from its bottom; more when it falls fast)...")]
        [Min(0.02f)]
        [SerializeField] private float solidOverFloor = 0.15f;
        [Tooltip("...or after this long (s).")]
        [Min(0.1f)]
        [SerializeField] private float solidAfter = 1.5f;
        [Tooltip("Reaching the player it keeps this share of its speed, so it drops in front of them instead of flying past.")]
        [Range(0f, 1f)]
        [SerializeField] private float arriveSlow = 0.15f;

        private class Pull
        {
            public XRGrabInteractable grab;
            public NearFarInteractor hand;
            public float speed;
            public float giveUpAt;
        }

        // Something passing through everything solid: its own solid colliders and what they're told to ignore.
        private class Ghost
        {
            public XRGrabInteractable grab;
            public Rigidbody body;
            public readonly List<Collider> own = new();
            public readonly HashSet<Collider> ignored = new();
            public int pulls;          // hands pulling it in
            public bool flying;        // let go on the way: flying to the player
            public bool launched;
            public int waitSteps;     // physics steps after the let-go before it's launched (XRI applies its throw first)
            public float speed;
            public float solidAt;
            // The pull is over (the grab point reached the hand), but a heavy weapon (velocity tracking, 500-750 kg)
            // is still on its way: it stays a ghost until it is really in the hand.
            public bool catchingUp;
            public NearFarInteractor hand;
        }

        [Tooltip("A pulled thing stays a ghost until its grab point is this close to the hand (m)...")]
        [Min(0.02f)]
        [SerializeField] private float inHandDistance = 0.12f;
        [Tooltip("...or for at most this long after the pull (s).")]
        [Min(0.1f)]
        [SerializeField] private float catchUpSeconds = 1.2f;

        private readonly List<XRBaseInteractor> interactors = new();
        private readonly List<Pull> pulls = new();
        private readonly List<Ghost> ghosts = new();
        private static readonly Collider[] Near = new Collider[64];

        private void OnEnable()
        {
            GetComponentsInChildren(true, interactors);
            foreach (var interactor in interactors)
            {
                interactor.selectEntered.AddListener(OnGrabbed);
                interactor.selectExited.AddListener(OnLetGo);
            }
        }

        private void OnDisable()
        {
            foreach (var interactor in interactors)
            {
                if (interactor == null) continue;
                interactor.selectEntered.RemoveListener(OnGrabbed);
                interactor.selectExited.RemoveListener(OnLetGo);
            }
            interactors.Clear();
            for (int i = pulls.Count - 1; i >= 0; i--) End(i, true);
            for (int i = ghosts.Count - 1; i >= 0; i--) Solid(i);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (args.interactorObject is not NearFarInteractor hand || hand.interactionAttachController == null) return;
            if (args.interactableObject is not XRGrabInteractable grab) return;
            float distance = HandDistance(hand.transform, grab);
            if (distance < farDistance)
            {
                // A near grab far from its grip (the broom taken by its head): XRI swings the grip into the hand in a
                // blink, and the rest of it sweeps through everything round the player. It goes through them as a ghost
                // too, until its grip is in the hand (user 2026-11-26: "when I grab the broom, on its way to my hand,
                // everything get damaged").
                if (ghostOnTheWay) GhostUntilInHand(grab, hand);
                return;
            }

            // XRI has just put the grab point in the hand; put it back where the object is, so the object starts from
            // where it lies, and Update brings it in.
            hand.interactionAttachController.MoveTo(grab.GetAttachTransform(hand).position);
            float speed = Mathf.Max(PullSpeed, distance / maxPullSeconds);
            pulls.Add(new Pull
            {
                grab = grab,
                hand = hand,
                speed = speed,
                giveUpAt = Time.time + maxPullSeconds + 0.5f,
            });
            if (!ghostOnTheWay) return;
            var ghost = GhostOf(grab);
            if (ghost == null)
            {
                ghost = new Ghost { grab = grab, body = grab.GetComponent<Rigidbody>() };
                // Every solid collider of its own (a weapon's head as well as its handle).
                foreach (var c in grab.GetComponentsInChildren<Collider>(true))
                    if (c != null && !c.isTrigger && (c.attachedRigidbody == null || c.attachedRigidbody == ghost.body)) ghost.own.Add(c);
                ghosts.Add(ghost);
            }
            ghost.pulls++;
            ghost.flying = false;
            ghost.launched = false;
            ghost.catchingUp = false;
            ghost.hand = hand;
            ghost.speed = speed;
        }

        private void GhostUntilInHand(XRGrabInteractable grab, NearFarInteractor hand)
        {
            var ghost = GhostOf(grab);
            bool made = ghost == null;
            if (made)
            {
                ghost = new Ghost { grab = grab, body = grab.GetComponent<Rigidbody>() };
                foreach (var c in grab.GetComponentsInChildren<Collider>(true))
                    if (c != null && !c.isTrigger && (c.attachedRigidbody == null || c.attachedRigidbody == ghost.body)) ghost.own.Add(c);
            }
            var was = (ghost.hand, ghost.catchingUp, ghost.flying);
            ghost.hand = hand;
            // Already in the hand (a grip that goes where it's grabbed, or the grip itself grabbed): nothing to do.
            if (ghost.body == null || InHand(ghost))
            {
                if (!made) (ghost.hand, ghost.catchingUp, ghost.flying) = was;
                return;
            }
            ghost.flying = false;
            ghost.launched = false;
            ghost.catchingUp = true;
            ghost.solidAt = Time.time + catchUpSeconds;
            if (made) ghosts.Add(ghost);
        }

        /// <summary>
        /// Whether this body is on its way to a hand right now (pulled, catching up, or let go on the way): a ghost that
        /// hurts nothing. Enemy Hit Damage ignores its bumps (and its own).
        /// </summary>
        public static bool OnTheWay(Rigidbody body)
        {
            if (body == null || instance == null) return false;
            foreach (var g in instance.ghosts) if (g.body == body) return true;
            return false;
        }

        private static GentleFarGrab instance;

        private void Awake() => instance = this;

        // Its grab point has reached the hand's (it's really in the hand, not just the pull's target).
        private bool InHand(Ghost g)
        {
            if (g.hand == null) return true;
            var mine = g.grab.GetAttachTransform(g.hand);
            var theirs = g.hand.GetAttachTransform(g.grab);
            if (mine == null || theirs == null) return true;
            var handPoint = theirs.parent != null ? theirs.parent.position : theirs.position;
            return (mine.position - handPoint).sqrMagnitude <= inHandDistance * inHandDistance;
        }

        private void OnLetGo(SelectExitEventArgs args)
        {
            // Only the pull of the hand that let go: with two hands on a weapon, the other hand's pull goes on.
            for (int i = pulls.Count - 1; i >= 0; i--)
            {
                if (pulls[i].grab != (Object)args.interactableObject || pulls[i].hand != (Object)args.interactorObject) continue;
                End(i, false);
            }
            // Let go on the way (pulled, or still catching up with the hand): it flies on to the player, still a ghost
            // (FixedUpdate launches it once XRI has let it go; if another hand still holds it, it turns solid there).
            var ghost = args.interactableObject is XRGrabInteractable grab ? GhostOf(grab) : null;
            if (ghost != null && ghost.pulls <= 0 && !ghost.flying)
            {
                ghost.flying = true;
                ghost.catchingUp = false;
                ghost.launched = false;
                ghost.waitSteps = 2;
                ghost.solidAt = Time.time + solidAfter;
            }
        }

        private Ghost GhostOf(XRGrabInteractable grab)
        {
            foreach (var g in ghosts) if (g.grab == grab) return g;
            return null;
        }

        private void FixedUpdate()
        {
            if (ghosts.Count == 0) return;
            var head = Camera.main != null ? Camera.main.transform : null;
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                if (g.grab == null || g.body == null) { Solid(i); continue; }
                if (g.flying)
                {
                    // Grabbed again (near), or the hand got it: solid.
                    if (g.grab.isSelected || head == null) { Solid(i); continue; }
                    if (!g.launched && --g.waitSteps <= 0) { Launch(g, head); g.launched = true; }
                    if (NearPlayer(g, head))
                    {
                        // Reached the player: it slows right down and drops in front of them, to catch or pick up.
                        if (!g.body.isKinematic) g.body.linearVelocity *= arriveSlow;
                        Solid(i);
                        continue;
                    }
                    if (Time.time >= g.solidAt || NearFloor(g)) { Solid(i); continue; }
                }
                else if (g.pulls <= 0)
                {
                    // Pull over: solid once it's really in the hand (or it takes too long, or nothing holds it).
                    if (!g.catchingUp || !g.grab.isSelected || Time.time >= g.solidAt || InHand(g)) { Solid(i); continue; }
                }
                IgnoreNear(g);
            }
        }

        // Off to the player's chest on an arc (gravity on), at about the pull's speed.
        private void Launch(Ghost g, Transform head)
        {
            if (g.body.isKinematic) return;
            // Just in front of the chest.
            var flatAhead = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            var target = head.position + Vector3.down * 0.45f + (flatAhead.sqrMagnitude > 0.01f ? flatAhead.normalized * 0.35f : Vector3.zero);
            var from = g.body.worldCenterOfMass;
            var gap = target - from;
            float t = Mathf.Clamp(gap.magnitude / Mathf.Max(1f, g.speed), 0.25f, 1.2f);
            var gravity = g.body.useGravity ? Physics.gravity : Vector3.zero;
            g.body.linearVelocity = gap / t - 0.5f * gravity * t;
            g.body.angularVelocity *= 0.3f;
            g.solidAt = Mathf.Min(g.solidAt, Time.time + t + 0.4f);
        }

        private bool NearPlayer(Ghost g, Transform head)
        {
            var at = g.body.worldCenterOfMass;
            var gap = at - head.position;
            gap.y = Mathf.Max(0f, Mathf.Abs(gap.y) - 0.6f);   // anywhere along the body counts
            return gap.magnitude <= solidNearPlayer;
        }

        // Its bottom just over something solid that isn't itself (the floor, a table): solid before it lands.
        private bool NearFloor(Ghost g)
        {
            if (g.own.Count == 0) return true;
            var bounds = g.own[0].bounds;
            foreach (var c in g.own) if (c != null) bounds.Encapsulate(c.bounds);
            float fall = Mathf.Max(0f, -g.body.linearVelocity.y) * Time.fixedDeltaTime * 3f;
            float reach = solidOverFloor + fall + bounds.extents.y;
            var hits = Physics.RaycastAll(bounds.center, Vector3.down, reach, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.rigidbody == g.body) continue;
                if (hit.collider.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) continue;
                return true;
            }
            return false;
        }

        // Every solid collider round it (and a little ahead) is ignored by its own solid ones. Triggers aren't touched.
        private void IgnoreNear(Ghost g)
        {
            if (g.own.Count == 0) return;
            var bounds = g.own[0].bounds;
            foreach (var c in g.own) if (c != null) bounds.Encapsulate(c.bounds);
            var ahead = g.body.linearVelocity * Time.fixedDeltaTime * 2f;
            bounds.Encapsulate(bounds.center + ahead);
            bounds.Expand(0.3f);
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, Near, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < count; k++)
            {
                var other = Near[k];
                if (other == null || other.isTrigger || other.attachedRigidbody == g.body || g.ignored.Contains(other)) continue;
                foreach (var mine in g.own) if (mine != null) Physics.IgnoreCollision(mine, other, true);
                g.ignored.Add(other);
            }
        }

        // Solid again: everything it ignored counts again.
        private void Solid(int index)
        {
            var g = ghosts[index];
            ghosts.RemoveAt(index);
            foreach (var other in g.ignored)
            {
                if (other == null) continue;
                foreach (var mine in g.own) if (mine != null) Physics.IgnoreCollision(mine, other, false);
            }
            g.ignored.Clear();
        }

        private void Update()
        {
            for (int i = pulls.Count - 1; i >= 0; i--)
            {
                var pull = pulls[i];
                if (pull.grab == null || pull.hand == null || !pull.grab.isSelected || Time.time >= pull.giveUpAt)
                {
                    End(i, true);
                    continue;
                }

                // The grab point slides from the object to the hand (where it sits with no offset).
                var point = pull.hand.GetAttachTransform(pull.grab);
                var handPoint = point.parent != null ? point.parent.position : pull.hand.transform.position;
                var next = Vector3.MoveTowards(point.position, handPoint, pull.speed * Time.deltaTime);
                if ((next - handPoint).sqrMagnitude <= Arrived * Arrived) End(i, true);
                else pull.hand.interactionAttachController.MoveTo(next);
            }
        }

        // Pull Speed with the player's Throw Power, never past Max Pull Speed (nor below Pull Speed).
        private float PullSpeed => Mathf.Max(pullSpeed, Mathf.Min(pullSpeed * Game.Leveling.PlayerLevel.PullScale, maxPullSpeed));

        private void End(int index, bool backToHand)
        {
            var pull = pulls[index];
            pulls.RemoveAt(index);
            // In the hand (or the pull gave up): no longer pulled by this hand, so solid again unless another still pulls.
            var ghost = GhostOf(pull.grab);
            if (ghost != null)
            {
                ghost.pulls--;
                if (backToHand && ghost.pulls <= 0)
                {
                    ghost.catchingUp = true;
                    ghost.solidAt = Time.time + catchUpSeconds;
                }
            }
            if (backToHand && pull.hand != null && pull.hand.interactionAttachController != null && pull.grab != null && pull.grab.isSelected)
                pull.hand.interactionAttachController.ResetOffset();
        }

        /// <summary>How far the nearest part of 'thing' is from the hand, in metres.</summary>
        public static float HandDistance(Transform hand, IXRInteractable thing)
        {
            var point = hand.position;
            float nearest = float.MaxValue;
            if (thing is XRBaseInteractable interactable)
            {
                foreach (var collider in interactable.colliders)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    // ClosestPoint only works on convex shapes; a concave mesh counts by its bounds.
                    var closest = collider is MeshCollider { convex: false } ? collider.bounds.ClosestPoint(point) : collider.ClosestPoint(point);
                    nearest = Mathf.Min(nearest, (closest - point).sqrMagnitude);
                }
            }
            return nearest < float.MaxValue ? Mathf.Sqrt(nearest) : Vector3.Distance(point, thing.transform.position);
        }
    }
}
