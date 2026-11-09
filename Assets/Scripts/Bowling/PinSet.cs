using System;
using System.Collections;
using System.Collections.Generic;
using Game.Locomotion;
using UnityEngine;

namespace Game.Bowling
{
    /// <summary>
    /// One poop of slime pins on a lane (Gate 2, "Strike the Slimes"), on an empty "Pin Set" object made by Spawn.
    /// It drops its pins one by one onto the rack slots, then watches them:
    /// - A pin is down when it tilts past Down Angle, slides Down Slide off its slot, goes over a Fall Edge or falls
    ///   through the floor. A down pin pops Pop Delay later (so it can still topple its neighbours first).
    /// - The first live ball that reaches the head pin opens the set's one window. It closes after Window seconds (sooner
    ///   when every pin is down, a little later while pins still move). Strike = the set was full (all landed, none
    ///   knocked before) and every pin is down. OnResolved fires once with the answer.
    /// - While the window is open, Strike Help gives knocked pins an extra shove (0 = pure physics).
    /// - When its pins are full grown the standing ones hatch into flyers, one by one; down ones pop.
    /// - No pins left: Done, OnCleared fires once (after OnResolved, which fires as "no strike" if it never resolved).
    /// Melt pops every pin with no flyers and no OnResolved (its slime was hurt).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PinSet : MonoBehaviour
    {
        [Header("Ball arrival")]
        [Tooltip("The head pin's distance along the lane from the lane's origin, in metres.")]
        [SerializeField] private float headAlong = BowlingLayout.HeadPin;
        [Tooltip("A ball has arrived this far before the head pin, in metres.")]
        [Min(0f)]
        [SerializeField] private float arriveAhead = 1f;
        [Tooltip("Half the lane's width, in metres. A ball counts within this (+0.1) of the lane's middle line.")]
        [Min(0.1f)]
        [SerializeField] private float laneHalfWidth = 1f;
        [Tooltip("A ball must still roll down the lane at least this fast, in metres per second, to count. A ball that stopped " +
                 "or bounced back at the deck before the rack landed is not a bowl at the full rack.")]
        [Min(0f)]
        [SerializeField] private float arriveSpeed = 1f;

        [Header("Window")]
        [Tooltip("Seconds from a ball's arrival until the set is judged.")]
        [Min(0.1f)]
        [SerializeField] private float window = 2.5f;
        [Tooltip("Most extra seconds the window waits while pins still move.")]
        [Min(0f)]
        [SerializeField] private float windowExtra = 1f;
        [Tooltip("A pin faster than this, in metres per second, still moves.")]
        [Min(0f)]
        [SerializeField] private float settleSpeed = 0.15f;

        [Header("Down")]
        [Tooltip("A pin leaning more than this, in degrees, is down.")]
        [Range(5f, 90f)]
        [SerializeField] private float downAngle = 40f;
        [Tooltip("A pin whose foot moved this far off its slot (flat), in metres, is down.")]
        [Min(0.01f)]
        [SerializeField] private float downSlide = 0.3f;
        [Tooltip("A pin below this height, in metres, has fallen off.")]
        [SerializeField] private float fallY = -0.5f;
        [Tooltip("Seconds a down pin waits before it pops.")]
        [Min(0f)]
        [SerializeField] private float popDelay = 0.8f;

        [Header("Strike help")]
        [Tooltip("How much extra shove knocked pins get inside the window (1 = as designed, 0 = pure physics).")]
        [Min(0f)]
        [SerializeField] private float strikeHelp = 1f;
        [Tooltip("Ball hits at least this fast (metres per second) get help.")]
        [Min(0f)]
        [SerializeField] private float ballHelpSpeed = 2f;
        [Tooltip("The pin gets this share of the ball's speed, away from the ball.")]
        [Min(0f)]
        [SerializeField] private float ballShare = 0.5f;
        [Tooltip("That shove is turned by up to this many degrees either way.")]
        [Range(0f, 90f)]
        [SerializeField] private float ballTurn = 25f;
        [Tooltip("Plus this much up, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float ballLift = 0.5f;
        [Tooltip("And this topple spin, in radians per second.")]
        [Min(0f)]
        [SerializeField] private float ballSpin = 6f;
        [Tooltip("Pin hits at least this fast (metres per second) get help.")]
        [Min(0f)]
        [SerializeField] private float pinHelpSpeed = 0.8f;
        [Tooltip("The hit pin gets this speed away from the hitter, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float pinPush = 1.2f;
        [Tooltip("And this topple spin, in radians per second.")]
        [Min(0f)]
        [SerializeField] private float pinSpin = 4f;

        [Header("Timing")]
        [Tooltip("Seconds between pops on a strike, and on a melt.")]
        [Min(0f)]
        [SerializeField] private float popStagger = 0.05f;
        [Tooltip("Seconds between hatches.")]
        [Min(0f)]
        [SerializeField] private float hatchStagger = 0.12f;

        private enum PinState { Waiting, Flying, Standing, Down, Popped, Hatched }

        private sealed class Slot
        {
            public SlimePin pin;
            public Vector3 at;
            public PinState state;
            public float downAt;
            public bool ballHelped;
            public bool pinHelped;
        }

        private Slot[] slots = Array.Empty<Slot>();
        private BowlingBallSupply balls;
        private Vector3 laneOrigin;
        private Vector3 laneDir = Vector3.forward;
        private Vector3 laneRight = Vector3.right;
        private readonly HashSet<Rigidbody> counted = new();
        private Coroutine spawning;
        private bool spawnFinished;
        private bool knocked;
        private bool windowUsed, windowOpen, wasFull;
        private float windowAt;
        private Vector3? lastBall;
        private bool hatching, melting, strike, cleared;

        /// <summary>How much extra shove knocked pins get inside the window (1 = as designed, 0 = pure physics). For tuning.</summary>
        public float StrikeHelp
        {
            get => strikeHelp;
            set => strikeHelp = Mathf.Max(0f, value);
        }

        /// <summary>Who pooped it (the Giant Flying Slime); the stage sets it.</summary>
        public object Owner { get; set; }
        /// <summary>The lane it is on; the stage sets it.</summary>
        public int Lane { get; set; }
        /// <summary>Its pins' colour.</summary>
        public Color Colour { get; private set; } = Color.white;
        /// <summary>How its pins grow and hatch.</summary>
        internal PinGrowth Growth { get; private set; }
        /// <summary>Every pin has landed on the deck.</summary>
        public bool Landed { get; private set; }
        /// <summary>All landed and standing, none knocked yet: a strike is still possible.</summary>
        public bool Full => Landed && !knocked;
        /// <summary>OnResolved has fired.</summary>
        public bool Resolved { get; private set; }
        /// <summary>No pins left (the lane's spot is free).</summary>
        public bool Done { get; private set; }
        /// <summary>Pins landed and still standing.</summary>
        public int Standing
        {
            get
            {
                int n = 0;
                foreach (var s in slots) if (s.state == PinState.Standing && s.pin != null) n++;
                return n;
            }
        }

        /// <summary>Once, when a ball's window closes (or the set is gone unjudged): the set, and whether it was a strike.</summary>
        public event Action<PinSet, bool> OnResolved;
        /// <summary>Once, when no pins are left.</summary>
        public event Action<PinSet> OnCleared;

        /// <summary>
        /// Makes a "Pin Set" under Parent and drops one Prefab pin from From onto each of Slots (floor points), Stagger
        /// seconds apart, each flying Flight Seconds. The lane (origin + direction, flat) is where balls are watched;
        /// Balls is the supply whose live balls count.
        /// </summary>
        public static PinSet Spawn(SlimePin prefab, Transform parent, Vector3 from, Vector3[] slots, Vector3 laneOrigin,
                                   Vector3 laneDirection, Color colour, PinGrowth growth, BowlingBallSupply balls,
                                   float flightSeconds = 0.7f, float stagger = 0.1f)
        {
            var go = new GameObject("Pin Set");
            go.transform.SetParent(parent, false);
            var points = slots ?? Array.Empty<Vector3>();
            var middle = Vector3.zero;
            foreach (var p in points) middle += p;
            go.transform.position = points.Length > 0 ? middle / points.Length : laneOrigin;

            var set = go.AddComponent<PinSet>();
            set.Colour = colour;
            set.Growth = growth ?? new PinGrowth();
            set.balls = balls;
            set.laneOrigin = laneOrigin;
            var dir = Vector3.ProjectOnPlane(laneDirection, Vector3.up);
            set.laneDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            set.laneRight = Vector3.Cross(Vector3.up, set.laneDir);
            set.slots = new Slot[points.Length];
            for (int i = 0; i < points.Length; i++) set.slots[i] = new Slot { at = points[i], state = PinState.Waiting };
            if (prefab == null)
            {
                Debug.LogError("[Bowling] Pin Set has no pin prefab.", set);
                foreach (var s in set.slots) s.state = PinState.Popped;
                set.spawnFinished = true;
                return set;
            }
            set.spawning = set.StartCoroutine(set.DropAll(prefab, from, Mathf.Max(0.01f, flightSeconds), Mathf.Max(0f, stagger)));
            return set;
        }

        private IEnumerator DropAll(SlimePin prefab, Vector3 from, float flight, float stagger)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var pin = Instantiate(prefab, from, Quaternion.identity, transform);
                pin.name = prefab.name + " " + i;
                pin.Bind(this, i);
                pin.Drop(from, slot.at, UnityEngine.Random.Range(0f, 360f), flight, Colour, Growth.seconds);
                slot.pin = pin;
                slot.state = PinState.Flying;
                if (i < slots.Length - 1 && stagger > 0f) yield return new WaitForSeconds(stagger);
            }
            spawnFinished = true;
            spawning = null;
        }

        /// <summary>Pops every pin (no flyers, no OnResolved); OnCleared fires when they're gone. For a hurt slime.</summary>
        public void Melt()
        {
            if (Done || melting) return;
            melting = true;
            StopSpawning();
            windowOpen = false;
            StopAllCoroutines();
            hatching = false;
            StartCoroutine(PopAll(null));
        }

        /// <summary>Tests: opens the set's window (as a ball would) unless Inside Window is false, then shoves every standing pin over.</summary>
        public void DebugKnockAll(bool insideWindow = true)
        {
            if (Done || melting) return;
            if (insideWindow && Landed && !windowUsed && !Resolved) OpenWindow(laneOrigin + laneDir * (headAlong - arriveAhead));
            foreach (var s in slots)
            {
                if (s.state != PinState.Standing || s.pin == null || s.pin.Body == null) continue;
                var body = s.pin.Body;
                body.AddForce(laneDir * 2.5f + Vector3.up * 0.5f, ForceMode.VelocityChange);
                body.AddTorque(Vector3.Cross(Vector3.up, laneDir) * 6f, ForceMode.VelocityChange);
            }
        }

        /// <summary>Tests: shoves every pin over with no window (so the set is no longer full).</summary>
        [ContextMenu("Debug Knock All (no window)")]
        public void DebugKnockAllNoWindow() => DebugKnockAll(false);

        [ContextMenu("Debug Knock All")]
        private void DebugKnockAllMenu() => DebugKnockAll();

        /// <summary>Resolves it as a strike now (every pin pops). For the stage's Debug Strike.</summary>
        [ContextMenu("Force Strike")]
        public void ForceStrike()
        {
            if (Done || melting || Resolved) return;
            StopSpawning();
            windowOpen = false;
            windowUsed = true;
            Resolve(true);
        }

        /// <summary>Hatches its standing pins now (down ones pop).</summary>
        [ContextMenu("Hatch Now")]
        public void HatchNow()
        {
            if (Done || melting || hatching) return;
            StartCoroutine(HatchAll());
        }

        /// <summary>A pin of this set touched something (Slime Pin calls this): the strike help, inside the window.</summary>
        internal void PinContact(SlimePin pin, Collision collision)
        {
            if (!windowOpen || strikeHelp <= 0f || pin == null || pin.Body == null) return;
            if (pin.Slot < 0 || pin.Slot >= slots.Length) return;
            var slot = slots[pin.Slot];
            var other = collision.rigidbody;
            if (other == null) return;
            float hit = collision.relativeVelocity.magnitude;
            var body = pin.Body;

            if (IsBall(other))
            {
                if (slot.ballHelped || hit < ballHelpSpeed) return;
                slot.ballHelped = true;
                var away = Flat(body.worldCenterOfMass - other.worldCenterOfMass);
                away = Quaternion.AngleAxis(UnityEngine.Random.Range(-ballTurn, ballTurn), Vector3.up) * away;
                float speed = other.linearVelocity.magnitude;
                body.AddForce((away * (ballShare * speed) + Vector3.up * ballLift) * strikeHelp, ForceMode.VelocityChange);
                body.AddTorque(Vector3.Cross(Vector3.up, away) * (ballSpin * strikeHelp), ForceMode.VelocityChange);
                return;
            }

            if (!other.TryGetComponent(out SlimePin hitter) || hitter.Set != this) return;
            if (slot.pinHelped || hit < pinHelpSpeed) return;
            // Only the slower of the two is the one hit.
            if (body.linearVelocity.sqrMagnitude >= other.linearVelocity.sqrMagnitude) return;
            slot.pinHelped = true;
            var push = Flat(body.worldCenterOfMass - other.worldCenterOfMass);
            body.AddForce(push * (pinPush * strikeHelp), ForceMode.VelocityChange);
            body.AddTorque(Vector3.Cross(Vector3.up, push) * (pinSpin * strikeHelp), ForceMode.VelocityChange);
        }

        // ------------------------------------------------------------------ watching

        private void FixedUpdate()
        {
            if (Done || melting) return;
            WatchPins();
            if (!windowUsed && Landed && !Resolved) WatchBalls();
            if (windowOpen) WatchWindow();
        }

        private void WatchPins()
        {
            int landed = 0;
            foreach (var s in slots)
            {
                if (s.state == PinState.Flying)
                {
                    if (s.pin == null) { Gone(s); continue; }
                    if (s.pin.Landed) s.state = PinState.Standing;
                }
                if (s.state == PinState.Standing)
                {
                    if (s.pin == null) { Gone(s); continue; }
                    if (IsKnocked(s)) SetDown(s);
                }
                else if (s.state == PinState.Down && s.pin == null) s.state = PinState.Popped;
                if (s.state != PinState.Waiting && s.state != PinState.Flying) landed++;
            }
            if (!Landed && spawnFinished && landed == slots.Length) Landed = true;
        }

        // Destroyed by something else: popped (it counts as down).
        private void Gone(Slot s)
        {
            s.state = PinState.Popped;
            knocked = true;
        }

        private bool IsKnocked(Slot s)
        {
            var t = s.pin.transform;
            if (Vector3.Angle(t.up, Vector3.up) > downAngle) return true;
            var slide = t.position - s.at;
            slide.y = 0f;
            if (slide.sqrMagnitude > downSlide * downSlide) return true;
            var middle = s.pin.Body != null ? s.pin.Body.worldCenterOfMass : t.position;
            return t.position.y < fallY || FallEdge.Covers(middle);
        }

        private void SetDown(Slot s)
        {
            s.state = PinState.Down;
            s.downAt = Time.time;
            s.pin.MarkDown();
            knocked = true;
        }

        private void WatchBalls()
        {
            if (balls == null) return;
            var live = balls.Balls;
            for (int i = 0; i < live.Count; i++)
            {
                var ball = live[i];
                if (ball == null || counted.Contains(ball)) continue;
                var d = ball.position - laneOrigin;
                float along = Vector3.Dot(d, laneDir);
                float side = Vector3.Dot(d, laneRight);
                if (along < headAlong - arriveAhead || Mathf.Abs(side) > laneHalfWidth + 0.1f) continue;
                if (Vector3.Dot(ball.linearVelocity, laneDir) < arriveSpeed) continue;
                counted.Add(ball);
                OpenWindow(ball.position);
                return;
            }
        }

        private void OpenWindow(Vector3 ball)
        {
            windowUsed = true;
            windowOpen = true;
            windowAt = Time.time;
            wasFull = Full;
            lastBall = ball;
        }

        private void WatchWindow()
        {
            float open = Time.time - windowAt;
            bool allDown = AllDown();
            if (!allDown && open < window) return;
            if (!allDown && open < window + windowExtra && AnyMoving()) return;
            windowOpen = false;
            Resolve(wasFull && allDown);
        }

        private bool AllDown()
        {
            if (slots.Length == 0) return false;
            foreach (var s in slots)
            {
                if (s.state != PinState.Down && s.state != PinState.Popped) return false;
            }
            return true;
        }

        private bool AnyMoving()
        {
            foreach (var s in slots)
            {
                if (s.pin == null || s.pin.Done || s.pin.Body == null) continue;
                if (s.pin.Body.linearVelocity.sqrMagnitude > settleSpeed * settleSpeed) return true;
            }
            return false;
        }

        private bool IsBall(Rigidbody body)
        {
            if (counted.Contains(body)) return true;
            if (balls == null) return false;
            var live = balls.Balls;
            for (int i = 0; i < live.Count; i++) if (live[i] == body) return true;
            return false;
        }

        // ------------------------------------------------------------------ results

        private void Resolve(bool isStrike)
        {
            if (Resolved) return;
            Resolved = true;
            strike = isStrike;
            OnResolved?.Invoke(this, isStrike);
            if (isStrike)
            {
                StopAllCoroutines();
                hatching = false;
                StartCoroutine(PopAll(lastBall));
            }
            else StartCoroutine(PopDown());
        }

        private void Update()
        {
            if (Done) return;
            float now = Time.time;
            if (!melting && !(Resolved && strike))
            {
                foreach (var s in slots)
                {
                    if (s.state != PinState.Down || s.pin == null || now - s.downAt < popDelay) continue;
                    s.pin.Pop(lastBall);
                    s.state = PinState.Popped;
                }
                if (!hatching && !windowOpen && FullGrown()) StartCoroutine(HatchAll());
            }
            if (spawnFinished && AllGone()) Clear();
        }

        // A standing pin has finished growing.
        private bool FullGrown()
        {
            foreach (var s in slots)
            {
                if (s.state == PinState.Standing && s.pin != null && s.pin.Grown >= 1f) return true;
            }
            return false;
        }

        private bool AllGone()
        {
            foreach (var s in slots)
            {
                if (s.state == PinState.Popped || s.state == PinState.Hatched) continue;
                if (s.pin == null && s.state != PinState.Waiting) continue;
                return false;
            }
            return true;
        }

        private void Clear()
        {
            if (cleared) return;
            cleared = true;
            Done = true;
            windowOpen = false;
            StopAllCoroutines();
            if (!Resolved && !melting) Resolve(false);
            OnCleared?.Invoke(this);
            Destroy(gameObject, 2f);
        }

        private IEnumerator PopAll(Vector3? from)
        {
            foreach (var s in slots)
            {
                if (s.state == PinState.Waiting) { s.state = PinState.Popped; continue; }
                if (s.state == PinState.Popped || s.state == PinState.Hatched) continue;
                if (s.pin != null && !s.pin.Done) s.pin.Pop(from);
                s.state = PinState.Popped;
                if (popStagger > 0f) yield return new WaitForSeconds(popStagger);
            }
            spawnFinished = true;
        }

        private IEnumerator PopDown()
        {
            foreach (var s in slots)
            {
                if (s.state != PinState.Down) continue;
                if (s.pin != null && !s.pin.Done) s.pin.Pop(lastBall);
                s.state = PinState.Popped;
                if (popStagger > 0f) yield return new WaitForSeconds(popStagger);
            }
        }

        private IEnumerator HatchAll()
        {
            hatching = true;
            foreach (var s in slots)
            {
                if (s.pin == null || s.pin.Done) continue;
                if (s.state == PinState.Standing)
                {
                    s.pin.Hatch(Growth, Colour);
                    s.state = PinState.Hatched;
                    if (hatchStagger > 0f) yield return new WaitForSeconds(hatchStagger);
                }
                else if (s.state == PinState.Down)
                {
                    s.pin.Pop(null);
                    s.state = PinState.Popped;
                }
            }
            hatching = false;
        }

        private void StopSpawning()
        {
            if (spawning != null) StopCoroutine(spawning);
            spawning = null;
            spawnFinished = true;
            foreach (var s in slots)
            {
                if (s.state == PinState.Waiting) s.state = PinState.Popped;
            }
        }

        private Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : laneDir;
        }
    }
}
