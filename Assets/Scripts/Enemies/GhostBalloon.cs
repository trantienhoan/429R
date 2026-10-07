using Game.Combat;
using Game.PlayGround;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A ghost balloon. It floats about, bobbing, drifting and swaying with its string swinging below, and gets knocked
    /// about by whatever hits it. With Roam on it wanders from spot to spot around its Ghost Roam Area (e.g. the
    /// PlayGround), now and then hanging out around the cauldron for a while; otherwise it stays where it's placed.
    /// One good hit by the player (something they hold or throw, at Pop Speed or faster) pops it at any time, and it
    /// drops 1-2 lollipops as a prize. A punch or slap of the hand never pops it but pushes it away, also while it
    /// chases. A hit too soft to pop it, or a punch, makes it angry (as does coming close, with Wake On Near): it
    /// giggles and shakes, then chases the player's face and pops in it: a puff of smoke, a pop,
    /// a bite of damage, and 3-5 spiders dropped on the floor. If it can't reach the player in time it just pops. It
    /// flies through walls, being a ghost. Trees and bushes let new ones out (Spawn), which float up to their spot. Goes
    /// on the balloon's root; Tools > 429 Game > Enemies > Set Up Ghost Balloons sets up Ghost_Balloon_1-3.
    /// </summary>
    [DisallowMultipleComponent]
    public class GhostBalloon : MonoBehaviour
    {
        public enum WakeOn
        {
            Hit,
            Near,
            HitOrNear,
            // Right away, as soon as it appears (e.g. made by an FSM's Create Object).
            Start,
        }

        private enum Mood
        {
            Floating,
            Waking,
            Chasing,
            Popped,
        }

        [Header("Floating")]
        [Tooltip("How far it bobs up and down, in metres.")]
        [Min(0f)]
        [SerializeField] private float bobHeight = 0.12f;
        [Tooltip("Seconds for one bob up and down.")]
        [Min(0.1f)]
        [SerializeField] private float bobSeconds = 3f;
        [Tooltip("How far it wanders around its spot, in metres.")]
        [Min(0f)]
        [SerializeField] private float drift = 0.3f;
        [Tooltip("How far it sways, in degrees.")]
        [Min(0f)]
        [SerializeField] private float sway = 6f;
        [Tooltip("How hard a hit knocks it away, in metres per second. It drifts back to its spot afterwards.")]
        [Min(0f)]
        [SerializeField] private float knock = 1.5f;

        [Header("Waking")]
        [Tooltip("A hit too soft to pop it always makes it angry. Hit: only that. Near / Hit Or Near: the player coming " +
                 "close does too. Start: it's angry as soon as it appears and goes straight for the player.")]
        [SerializeField] private WakeOn wakeOn = WakeOn.Hit;
        [Tooltip("With Near: how close the player must come, in metres along the floor (it may float high above them).")]
        [Min(0f)]
        [SerializeField] private float wakeDistance = 3f;
        [Tooltip("Played when it wakes, e.g. a creepy laugh.")]
        [SerializeField] private AudioClip wakeSound;
        [Tooltip("Seconds it shakes after waking before it gives chase.")]
        [Min(0f)]
        [SerializeField] private float wakeSeconds = 0.8f;

        [Header("Chasing")]
        [Tooltip("How fast it flies at the player, in metres per second.")]
        [Min(0.1f)]
        [SerializeField] private float chaseSpeed = 2.5f;
        [Tooltip("How quickly it gets up to speed and turns, in metres per second per second.")]
        [Min(0.1f)]
        [SerializeField] private float chaseAcceleration = 4f;
        [Tooltip("It pops when its middle is this close to the player's face, in metres.")]
        [Min(0.05f)]
        [SerializeField] private float popDistance = 0.45f;
        [Tooltip("It gives up and pops where it is after chasing this many seconds. 0 = it never gives up: it keeps " +
                 "chasing until it pops in the player's face.")]
        [Min(0f)]
        [SerializeField] private float giveUpSeconds = 12f;
        [Tooltip("A hit with something held or thrown, or a bullet, at least this fast (metres per second) pops it at once, whatever " +
                 "it's doing, and it drops its lollipops; a softer hit makes it angry and it chases the player. Punches " +
                 "push it instead (see Punched). 0 = hits never pop it.")]
        [Min(0f)]
        [FormerlySerializedAs("swatSpeed")]
        [SerializeField] private float popSpeed = 3f;
        [Tooltip("Turns it while it chases, in degrees, if its face isn't on its blue Z arrow.")]
        [SerializeField] private float faceAngle;

        [Header("Pop")]
        [Tooltip("Effect where it pops, e.g. a puff of smoke.")]
        [SerializeField] private GameObject popEffect;
        [Tooltip("Size of the pop effect. The Hyper Casual FX smoke is about 13 m wide, so 0.12 makes it about 1.5 m.")]
        [Min(0.01f)]
        [SerializeField] private float popEffectScale = 0.12f;
        [SerializeField] private AudioClip popSound;
        [Range(0f, 1f)]
        [SerializeField] private float popVolume = 1f;
        [Tooltip("Event sent to the player's Damage FSM when it pops in their face, like a spider's bite: Damage, " +
                 "DamageHeavy, ... Empty: no damage.")]
        [SerializeField] private string playerEvent = "Damage";
        [Tooltip("What it drops on the floor when it pops in the player's face: spiders, or for a boss balloon mini " +
                 "bosses (TheSpider, TheCockroach, TheBee ...); each picks one at random.")]
        [SerializeField] private GameObject[] spiders = System.Array.Empty<GameObject>();
        [Min(0)]
        [SerializeField] private int minSpiders = 3;
        [Min(0)]
        [SerializeField] private int maxSpiders = 5;
        [Tooltip("How far apart they land, in metres.")]
        [Min(0f)]
        [SerializeField] private float spiderSpread = 0.8f;
        [Tooltip("Tick for a boss balloon: what it drops always comes out, even when the game's enemy limit is full " +
                 "(otherwise it drops only as many as the limit has room for).")]
        [SerializeField] private bool ignoreEnemyLimit;
        [Tooltip("Optional: a GAMESTAGES bool that turns true when it pops in the player's face, for the stage's FSMs. " +
                 "Add it to the GAMESTAGES FSM's Variables.")]
        [SerializeField] private string inFaceBool;

        [Header("Punched")]
        [Tooltip("How hard a punch or slap pushes it away: its push speed = the punch's speed x this. Punches never " +
                 "pop it (weapons do); they make it angry and knock it back, also while it chases.")]
        [Min(0f)]
        [SerializeField] private float punchPush = 1f;
        [Tooltip("Fastest a punch can push it, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float maxPunchPush = 6f;

        [Header("Popped by the player")]
        [Tooltip("Prizes it drops when the player pops it before it reaches them (Lolipop_1 ... 7); each picks one at random.")]
        [SerializeField] private GameObject[] lollipops = System.Array.Empty<GameObject>();
        [Min(0)]
        [SerializeField] private int minLollipops = 1;
        [Min(0)]
        [SerializeField] private int maxLollipops = 2;

        [Header("Travel")]
        [Tooltip("Fastest it floats to its spot, e.g. rising out of a tree, in metres per second.")]
        [Min(0.1f)]
        [SerializeField] private float travelSpeed = 1.5f;

        [Header("Roaming")]
        [Tooltip("Wanders from spot to spot instead of staying where it's put: around its Ghost Roam Area (e.g. the " +
                 "PlayGround's), or, with none around, within Roam Distance of where it started.")]
        [SerializeField] private bool roam = true;
        [Tooltip("How fast it roams, in metres per second.")]
        [Min(0.05f)]
        [SerializeField] private float roamSpeed = 0.6f;
        [Tooltip("Seconds it stops at each spot, picked at random between these.")]
        [Min(0f)]
        [SerializeField] private float minStop = 1f;
        [Min(0f)]
        [SerializeField] private float maxStop = 5f;
        [Tooltip("With no Ghost Roam Area around: how far from where it started it roams, in metres.")]
        [Min(0f)]
        [SerializeField] private float roamDistance = 4f;
        [Tooltip("Chance (0 to 1) that it heads over to the cauldron next, to hang out around it.")]
        [Range(0f, 1f)]
        [SerializeField] private float cauldronChance = 0.3f;
        [Tooltip("Seconds it hangs out around the cauldron, picked at random between these.")]
        [Min(0f)]
        [SerializeField] private float minHangOut = 8f;
        [Min(0f)]
        [SerializeField] private float maxHangOut = 16f;

        private enum PopReason
        {
            InFace,
            Swatted,
            GaveUp,
        }

        private enum RoamStep
        {
            Stopped,
            Going,
            HangingOut,
        }

        private const string PlayerVariable = "Player";
        private const string PlayerDamageFsm = "Damage";
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // Spring back to its spot after a knock: slow and floaty.
        private const float KnockSpring = 3f;
        private const float KnockDamping = 1.6f;
        // Roaming: how far away a cauldron can be to go and hang out at, how fast it turns, and how fast it circles the
        // cauldron, in degrees per second.
        private const float CauldronReach = 20f;
        private const float RoamTurnSpeed = 60f;
        private const float HangOutCircling = 12f;
        // Slower than this (metres per second) is a touch, not a hit.
        private const float MinHitSpeed = 0.3f;
        // A hand this close to the balloon's skin, in metres, slaps it.
        private const float SlapReach = 0.08f;

        private Rigidbody body;
        private Vector3 middle;
        private float radius;
        private Transform[] hands = System.Array.Empty<Transform>();
        private Vector3[] lastHand = System.Array.Empty<Vector3>();
        private bool[] handTouching = System.Array.Empty<bool>();
        private Vector3 lastMiddle;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Vector3 knockOffset;
        private Vector3 knockVelocity;
        private Vector3 velocity;
        private Quaternion wakeRotation;
        private float phase;
        private float seed;
        private float moodSince;
        private Mood mood;
        private Transform head;
        private bool homeSet;
        private bool roaming;
        private RoamStep roamStep;
        private Vector3 roamPoint;
        private Vector3 roamTarget;
        private Vector3 roamOrigin;
        private Vector3 roamVelocity;
        private Quaternion roamTurn;
        private float roamUntil;
        private GhostRoamArea roamArea;
        private Cauldron hangOut;
        private float hangAngle;
        private float hangRadius;
        private float hangHeight;

        /// <summary>
        /// Makes a ghost balloon with its balloon part at 'from' (e.g. in a treetop), which then floats up to 'home' and
        /// stays there like any other.
        /// </summary>
        public static GhostBalloon Spawn(GameObject prefab, Vector3 from, Vector3 home, Transform parent)
        {
            var made = Instantiate(prefab, from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
            if (!made.TryGetComponent<GhostBalloon>(out var balloon)) return null;

            made.transform.position = from - (balloon.Middle() - made.transform.position);
            balloon.SetHome(home);
            return balloon;
        }

        /// <summary>Where its balloon part floats from now on (roaming, it starts from there); it travels there at Travel Speed.</summary>
        public void SetHome(Vector3 balloonHome)
        {
            var rootHome = balloonHome - transform.rotation * Scaled(middle);
            var parent = transform.parent;
            homePosition = parent != null ? parent.InverseTransformPoint(rootHome) : rootHome;
            homeRotation = transform.localRotation;
            homeSet = true;
            roaming = false;
        }

        /// <summary>Its balloon part (not the string), in the root's own space: the middle of the top of its mesh, and its radius.</summary>
        public static bool FindBalloon(GameObject root, out Vector3 center, out float radius)
        {
            center = Vector3.zero;
            radius = 0.25f;
            var filter = root.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            var bounds = filter.sharedMesh.bounds;
            // Its widest across is the balloon; the string hangs below it.
            float meshRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            var top = new Vector3(bounds.center.x, bounds.max.y - meshRadius, bounds.center.z);
            center = root.transform.InverseTransformPoint(filter.transform.TransformPoint(top));
            var edge = root.transform.InverseTransformPoint(filter.transform.TransformPoint(top + Vector3.right * meshRadius));
            radius = Vector3.Distance(center, edge);
            return true;
        }

        private void Awake()
        {
            if (!TryGetComponent(out body)) body = gameObject.AddComponent<Rigidbody>();
            // Moved by this script; things that hit it still bounce off and tell it so.
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            FindBalloon(gameObject, out middle, out radius);
            if (GetComponentInChildren<Collider>() == null)
            {
                var sphere = gameObject.AddComponent<SphereCollider>();
                sphere.center = middle;
                sphere.radius = radius;
            }

            phase = Random.value * Mathf.PI * 2f;
            seed = Random.value * 100f;
        }

        private void Start()
        {
            hands = new[] { XRRig.FindController(left: true), XRRig.FindController(left: false) };
            lastHand = new Vector3[hands.Length];
            handTouching = new bool[hands.Length];
            for (int i = 0; i < hands.Length; i++) lastHand[i] = hands[i] != null ? hands[i].position : Vector3.zero;
            lastMiddle = Middle();
            if (wakeOn == WakeOn.Start) Wake();

            // Its spot, kept relative to whatever it's placed in, so it goes along when that moves.
            if (homeSet) return;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
        }

        private void Update()
        {
            CheckSlaps();
            if (mood != Mood.Floating || wakeOn == WakeOn.Hit || FindHead() == null) return;

            var away = head.position - Middle();
            away.y = 0f;
            if (away.sqrMagnitude <= wakeDistance * wakeDistance) Wake();
        }

        private void FixedUpdate()
        {
            switch (mood)
            {
                case Mood.Floating:
                    Float();
                    break;
                case Mood.Waking:
                    Shake();
                    break;
                case Mood.Chasing:
                    Chase();
                    break;
            }
        }

        // Hit by something the player holds or threw, a bullet from their gun, or the player.
        private void OnCollisionEnter(Collision collision)
        {
            if (mood == Mood.Popped) return;

            var hitBy = collision.rigidbody;
            bool byPlayer = collision.collider.GetComponentInParent<XROrigin>() != null;
            bool byItem = hitBy != null && hitBy.GetComponentInParent<XRGrabInteractable>() != null;
            // Bullets aren't grabbable but have a Weapon Power; fists have one too but push it instead (CheckSlaps).
            bool byBullet = hitBy != null && hitBy.TryGetComponent<WeaponPower>(out _) && !Fist.IsFistBall(hitBy);
            if (!byPlayer && !byItem && !byBullet) return;

            var hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            TakeHit(collision.relativeVelocity.magnitude, hitPoint);
        }

        // One good hit pops it, whatever it's doing; a softer one knocks it about and makes it angry.
        private void TakeHit(float speed, Vector3 hitPoint)
        {
            if (mood == Mood.Popped || speed < MinHitSpeed) return;
            if (popSpeed > 0f && speed >= popSpeed)
            {
                Pop(PopReason.Swatted);
                return;
            }
            if (mood == Mood.Chasing) return;

            // Knocked away from where it was hit.
            var away = Middle() - hitPoint;
            if (away.sqrMagnitude > 0.0001f) knockVelocity += away.normalized * (knock * Mathf.Clamp(speed / 4f, 0.5f, 2f));
            Wake();
        }

        // A punch or slap: it's pushed away the way the fist was going (and away from it), and gets angry. Chasing,
        // it's knocked back and has to come at the player again.
        private void TakePunch(float speed, Vector3 handPoint, Vector3 punchDirection)
        {
            if (mood == Mood.Popped || speed < MinHitSpeed) return;

            var away = Middle() - handPoint;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : punchDirection;
            var direction = punchDirection.sqrMagnitude > 0.0001f ? (punchDirection.normalized * 0.7f + away * 0.3f).normalized : away;
            var push = direction * Mathf.Min(speed * punchPush, maxPunchPush);

            if (mood == Mood.Floating)
            {
                Wake();
                // Knocked off its spot; it shakes there angrily, then gives chase from wherever it ends up.
                velocity = push;
            }
            else
            {
                velocity += push;
            }
        }

        // A hand punching or slapping it pushes it away (hands don't bump into things the way held items do): as fast
        // as the hand was moving towards it when it touched.
        private void CheckSlaps()
        {
            var at = Middle();
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var moved = at - lastMiddle;
            lastMiddle = at;
            if (mood == Mood.Popped) return;

            float reach = radius * Mathf.Abs(transform.lossyScale.x) + SlapReach;
            for (int i = 0; i < hands.Length; i++)
            {
                if (hands[i] == null) continue;
                var now = hands[i].position;
                var punch = now - lastHand[i] - moved;
                float speed = punch.magnitude / dt;
                lastHand[i] = now;

                bool touching = (now - at).sqrMagnitude <= reach * reach;
                bool slapped = touching && !handTouching[i];
                handTouching[i] = touching;
                if (!slapped) continue;

                TakePunch(speed, now, punch);
            }
        }

        /// <summary>Wakes it up: it giggles, shakes, then chases the player.</summary>
        public void Wake()
        {
            if (mood != Mood.Floating) return;

            mood = Mood.Waking;
            moodSince = Time.time;
            wakeRotation = transform.rotation;
            velocity = Vector3.zero;
            if (wakeSound != null) AudioSource.PlayClipAtPoint(wakeSound, Middle());
        }

        /// <summary>
        /// Pops it with smoke and a sound. In the player's face it bites (Player Event) and drops spiders; popped by
        /// the player first it drops lollipops; giving up it drops nothing.
        /// </summary>
        private void Pop(PopReason reason)
        {
            if (mood == Mood.Popped) return;
            mood = Mood.Popped;

            var at = Middle();
            if (popEffect != null)
            {
                var effect = Instantiate(popEffect, at, Quaternion.identity);
                effect.transform.localScale = Vector3.one * popEffectScale;
                Destroy(effect, 4f);
            }
            if (popSound != null) AudioSource.PlayClipAtPoint(popSound, at, popVolume);

            if (reason == PopReason.InFace)
            {
                if (!string.IsNullOrEmpty(playerEvent)) SendToPlayer(playerEvent);
                DropSpiders(head != null ? head.position : at);
                if (!string.IsNullOrEmpty(inFaceBool)) GameStages.SetBool(inFaceBool, true, this);
            }
            else if (reason == PopReason.Swatted)
            {
                DropLollipops(at);
            }
            Destroy(gameObject);
        }

        // Prizes for popping it in time: they fall from where it burst.
        private void DropLollipops(Vector3 at)
        {
            if (lollipops == null || lollipops.Length == 0 || maxLollipops <= 0) return;

            var parent = SpawnedStuff();
            int count = Random.Range(Mathf.Min(minLollipops, maxLollipops), maxLollipops + 1);
            for (int i = 0; i < count; i++)
            {
                var prefab = lollipops[Random.Range(0, lollipops.Length)];
                if (prefab == null) continue;

                var made = Instantiate(prefab, at + Random.insideUnitSphere * 0.2f, Random.rotation, parent);
                if (made.TryGetComponent<Rigidbody>(out var prize) && !prize.isKinematic)
                    prize.linearVelocity = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f), Random.Range(-1f, 1f));
            }
        }

        private void Float()
        {
            float dt = Time.fixedDeltaTime;
            float t = Time.time;
            knockVelocity += (-knockOffset * KnockSpring - knockVelocity * KnockDamping) * dt;
            knockOffset += knockVelocity * dt;

            // Its spot: where it was put, or wherever it has roamed to.
            Vector3 spot;
            Quaternion facing;
            if (roam)
            {
                Roam(dt);
                spot = roamPoint;
                facing = roamTurn;
            }
            else
            {
                facing = HomeTurn();
                spot = HomeRoot() + facing * Scaled(middle);
            }

            var bob = Vector3.up * (Mathf.Sin(t / bobSeconds * Mathf.PI * 2f + phase) * bobHeight);
            var wander = new Vector3(Mathf.PerlinNoise(seed, t * 0.1f) - 0.5f, 0f, Mathf.PerlinNoise(seed + 7f, t * 0.1f) - 0.5f) * (2f * drift);
            var swaying = Quaternion.Euler(Mathf.Sin(t * 0.7f + phase) * sway, 0f, Mathf.Cos(t * 0.53f + phase) * sway);
            var turn = Lean(knockVelocity + roamVelocity, 12f) * facing * swaying;

            // Swings around its balloon, so the string swings below it. Far from its spot (just let out of a tree), it
            // floats there at Travel Speed instead of jumping.
            var balloon = spot + bob + wander + knockOffset;
            balloon = Vector3.MoveTowards(Middle(), balloon, Mathf.Max(travelSpeed, knockVelocity.magnitude * 2f) * dt);
            Place(balloon, turn);
        }

        // Floats from spot to spot around its area, stopping at each for a bit; now and then it goes over to the
        // cauldron and circles it slowly for a while, watching it, before it roams on.
        private void Roam(float dt)
        {
            if (!roaming)
            {
                roaming = true;
                roamTurn = HomeTurn();
                roamPoint = HomeRoot() + roamTurn * Scaled(middle);
                roamOrigin = roamPoint;
                roamArea = GhostRoamArea.Around(roamOrigin);
                Stop(Random.Range(minStop, Mathf.Max(minStop, maxStop)));
            }

            var was = roamPoint;
            switch (roamStep)
            {
                case RoamStep.Stopped:
                    if (Time.time >= roamUntil) PickSpot();
                    break;
                case RoamStep.Going:
                    if (hangOut != null) roamTarget = HangOutSpot();
                    roamPoint = Vector3.MoveTowards(roamPoint, roamTarget, roamSpeed * dt);
                    FaceTowards(roamTarget - roamPoint, dt);
                    if ((roamTarget - roamPoint).sqrMagnitude > 0.0025f) break;
                    if (hangOut == null)
                    {
                        Stop(Random.Range(minStop, Mathf.Max(minStop, maxStop)));
                        break;
                    }
                    roamStep = RoamStep.HangingOut;
                    roamUntil = Time.time + Random.Range(minHangOut, Mathf.Max(minHangOut, maxHangOut));
                    break;
                case RoamStep.HangingOut:
                    if (hangOut == null || Time.time >= roamUntil)
                    {
                        hangOut = null;
                        PickSpot();
                        break;
                    }
                    hangAngle += HangOutCircling * dt;
                    roamPoint = Vector3.MoveTowards(roamPoint, HangOutSpot(), roamSpeed * dt);
                    FaceTowards(hangOut.Mouth - roamPoint, dt);
                    break;
            }
            roamVelocity = (roamPoint - was) / Mathf.Max(dt, 0.0001f);
        }

        private void Stop(float seconds)
        {
            roamStep = RoamStep.Stopped;
            roamUntil = Time.time + seconds;
        }

        // Its next stop: the cauldron, sometimes; otherwise anywhere in its area.
        private void PickSpot()
        {
            hangOut = Random.value < cauldronChance ? Cauldron.Nearest(roamPoint, CauldronReach) : null;
            if (hangOut != null)
            {
                hangAngle = Random.Range(0f, 360f);
                hangRadius = Random.Range(1.3f, 2.3f);
                hangHeight = Random.Range(0.8f, 1.8f);
                roamTarget = HangOutSpot();
            }
            else
            {
                if (roamArea == null) roamArea = GhostRoamArea.Around(roamOrigin);
                if (roamArea != null)
                {
                    roamTarget = roamArea.RandomPoint();
                }
                else
                {
                    var around = Random.insideUnitCircle * roamDistance;
                    roamTarget = roamOrigin + new Vector3(around.x, Random.Range(-0.5f, 0.5f), around.y);
                }
            }
            roamStep = RoamStep.Going;
        }

        // A spot on a ring around and above the cauldron's pot.
        private Vector3 HangOutSpot()
        {
            float angle = hangAngle * Mathf.Deg2Rad;
            return hangOut.Mouth + new Vector3(Mathf.Cos(angle) * hangRadius, hangHeight, Mathf.Sin(angle) * hangRadius);
        }

        // Turns slowly to face along the floor towards 'direction'.
        private void FaceTowards(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            var wanted = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, faceAngle, 0f);
            roamTurn = Quaternion.RotateTowards(roamTurn, wanted, RoamTurnSpeed * dt);
        }

        // Where its root goes when it's at its spot, kept relative to whatever it's placed in.
        private Vector3 HomeRoot()
        {
            var parent = transform.parent;
            return parent != null ? parent.TransformPoint(homePosition) : homePosition;
        }

        private Quaternion HomeTurn()
        {
            var parent = transform.parent;
            return parent != null ? parent.rotation * homeRotation : homeRotation;
        }

        private void Shake()
        {
            // Still sliding from a punch, slowing down.
            float dt = Time.fixedDeltaTime;
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, chaseAcceleration * dt);
            var jitter = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(-8f, 8f), Random.Range(-8f, 8f));
            Place(Middle() + velocity * dt, jitter * wakeRotation);
            if (Time.time - moodSince < wakeSeconds) return;

            mood = Mood.Chasing;
            moodSince = Time.time;
        }

        private void Chase()
        {
            if (FindHead() == null) return;

            var balloon = Middle();
            var toFace = head.position - balloon;
            float distance = toFace.magnitude;
            if (distance <= popDistance)
            {
                Pop(PopReason.InFace);
                return;
            }
            if (giveUpSeconds > 0f && Time.time - moodSince > giveUpSeconds)
            {
                Pop(PopReason.GaveUp);
                return;
            }

            // Straight at the face, weaving a little like it's giggling.
            float t = Time.time;
            var weave = new Vector3(Mathf.Sin(t * 3.1f + phase), Mathf.Sin(t * 2.3f + seed) * 0.5f, Mathf.Cos(t * 2.7f + phase)) * 0.4f;
            var wanted = toFace / distance * chaseSpeed + weave;
            velocity = Vector3.MoveTowards(velocity, wanted, chaseAcceleration * Time.fixedDeltaTime);

            var flat = toFace;
            flat.y = 0f;
            var facing = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) * Quaternion.Euler(0f, faceAngle, 0f) : transform.rotation;
            Place(balloon + velocity * Time.fixedDeltaTime, Lean(velocity, 8f) * facing);
        }

        // Puts the balloon part at 'balloon', turned 'turn', moving the root to match.
        private void Place(Vector3 balloon, Quaternion turn)
        {
            body.MoveRotation(turn);
            body.MovePosition(balloon - turn * Scaled(middle));
        }

        // Tips its top towards where it's moving, so the string trails behind.
        private static Quaternion Lean(Vector3 moving, float degreesPerSpeed)
        {
            moving.y = 0f;
            float speed = moving.magnitude;
            if (speed < 0.01f) return Quaternion.identity;
            return Quaternion.AngleAxis(Mathf.Min(speed * degreesPerSpeed, 25f), Vector3.Cross(Vector3.up, moving / speed));
        }

        private Vector3 Middle()
        {
            return transform.TransformPoint(middle);
        }

        private Vector3 Scaled(Vector3 local)
        {
            return Vector3.Scale(local, transform.lossyScale);
        }

        private Transform FindHead()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }

        // Where spawned things go, so the stage clean-up removes them.
        private static Transform SpawnedStuff()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return holder != null && holder.Value != null ? holder.Value.transform : null;
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

        private void DropSpiders(Vector3 above)
        {
            if (spiders == null || spiders.Length == 0 || maxSpiders <= 0) return;

            var floor = FloorBelow(above);
            var parent = SpawnedStuff();
            int count = Random.Range(Mathf.Min(minSpiders, maxSpiders), maxSpiders + 1);
            for (int i = 0; i < count; i++)
            {
                // Within the game's enemy limit, so a popped balloon can't swamp the Quest.
                if (!ignoreEnemyLimit && !EnemyLimit.TryTakeSlot()) break;

                var prefab = spiders[Random.Range(0, spiders.Length)];
                if (prefab == null) continue;

                var spread = Random.insideUnitCircle * spiderSpread;
                var spot = floor + new Vector3(spread.x, 0f, spread.y);
                if (NavMesh.SamplePosition(spot, out var onMesh, 2f, NavMesh.AllAreas)) spot = onMesh.position;
                Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
            }
        }

        // The first thing under 'point' that isn't the player or this balloon.
        private Vector3 FloorBelow(Vector3 point)
        {
            var hits = Physics.RaycastAll(point, Vector3.down, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            var floor = point;
            foreach (var hit in hits)
            {
                if (hit.distance >= nearest || hit.collider.attachedRigidbody == body) continue;
                if (hit.collider.GetComponentInParent<XROrigin>() != null) continue;
                nearest = hit.distance;
                floor = hit.point;
            }
            return floor;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!FindBalloon(gameObject, out var center, out _)) return;
            if (wakeOn == WakeOn.Hit) return;

            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.6f);
            var around = transform.TransformPoint(center);
            const int segments = 32;
            var previous = around + new Vector3(wakeDistance, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var next = around + new Vector3(Mathf.Cos(angle) * wakeDistance, 0f, Mathf.Sin(angle) * wakeDistance);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
