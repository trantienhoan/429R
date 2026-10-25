using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Leveling;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Inventory
{
    /// <summary>
    /// The invisible Candy Helmet, a candy vacuum (a timed buff, Player Buffs: Candy Helmet; every pumpkin the bonus
    /// round's player breaks adds 5 s): while it's on, every candy around flies into the player's mouth, many at once, and
    /// is eaten the normal way, by the candy's own "Damage" FSM state "Health 1" (it heals, says om nom and adds 1 Candy to
    /// the save). It takes them the moment it sees them, even ones still flying out of a pumpkin, so punches and pumpkin
    /// pieces can't break them first (a candy breaks on any bump over 7 m/s). Ones held in a hand or busy breaking are
    /// left. Made by itself the first time the buff is given; nothing to put in a scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class CandyHelmet : MonoBehaviour
    {
        [Tooltip("How far from the player's head candies are pulled in, in metres.")]
        [SerializeField] private float reach = 30f;
        [Tooltip("Candies in the air at once.")]
        [SerializeField] private int maxFlying = 24;
        [Tooltip("Seconds between one candy setting off and the next: a quick stream into the mouth.")]
        [SerializeField] private float nextEvery = 0.04f;
        [Tooltip("A candy is taken once it has been lying (nearly) still this long, in seconds (0: at once, even flying).")]
        [SerializeField] private float settleSeconds = 0f;
        [Tooltip("... or once it's been about this long anyway (one that keeps rolling), in seconds.")]
        [SerializeField] private float takeAnywayAfter = 0f;

        // The candies: Candy_1 ... Candy_8 (not their broken pieces, Candy_Drop).
        private static readonly Regex CandyName = new(@"^Candy_\d+$");
        private const string DamageFsm = "Damage";
        private const string HealthFsm = "Health";
        private const string ListeningState = "Wait for Collision";
        private const string EatState = "Health 1";
        private const string DoneState = "Destroy Self";
        private const string MouthTag = "Mouth";
        // Where the mouth is from the eyes when there's no Mouth (as the Mouth sits under the Main Camera).
        private static readonly Vector3 MouthOffset = new(0f, -0.09f, 0.07f);
        private const float StillSpeed = 0.6f;
        private const float ScanEvery = 0.1f;
        private const float ArriveSeconds = 0.25f;

        private static CandyHelmet instance;

        private class Seen
        {
            public float firstSeen;
            public float stillSince = -1f;
        }

        private class Flight
        {
            public GameObject candy;
            public PlayMakerFSM damage;
            public Vector3 from;
            public float start;
            public float seconds;
            public float arc;
            public Vector3 spin;
            public bool eaten;
            public float eatenAt;
        }

        private readonly Dictionary<GameObject, Seen> seen = new();
        private readonly List<GameObject> lying = new();
        private readonly List<Flight> flights = new();
        private readonly List<GameObject> gone = new();
        private Transform mouth;
        private Transform head;
        private float nextScan;
        private float nextStart;

        /// <summary>Candies flying into the mouth right now.</summary>
        public static int Flying => instance != null ? instance.flights.Count : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            instance = null;
            // Player Buffs clears its events when play starts (SubsystemRegistration), before this.
            PlayerBuffs.Given += buff =>
            {
                if (buff == Buff.CandyHelmet) Ensure();
            };
        }

        /// <summary>Makes sure the helmet runs (in this scene); it stops by itself when the buff is over.</summary>
        public static void Ensure()
        {
            if (!Application.isPlaying) return;
            if (instance == null) instance = new GameObject("Candy Helmet").AddComponent<CandyHelmet>();
            instance.enabled = true;
        }

        /// <summary>
        /// Candies within 'radius' of 'near' that the helmet would still eat: lying about, in nobody's hand (the ones
        /// flying to the mouth are counted by Flying).
        /// </summary>
        public static int LyingAround(Vector3 near, float radius)
        {
            int count = 0;
            var stuff = SpawnedStuff();
            if (stuff == null) return 0;
            foreach (Transform child in stuff)
            {
                if (IsCandy(child.gameObject) && (child.position - near).sqrMagnitude <= radius * radius && Takeable(child.gameObject)) count++;
            }
            return count;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                if (Fly(flights[i], now)) flights.RemoveAt(i);
            }

            bool on = PlayerBuffs.CandyHelmet;
            if (!on)
            {
                // The buff is over: the candies in the air still get eaten, then it rests.
                if (flights.Count == 0)
                {
                    seen.Clear();
                    enabled = false;
                }
                return;
            }

            if (now >= nextScan)
            {
                nextScan = now + ScanEvery;
                Scan(now);
            }
            if (flights.Count < maxFlying && now >= nextStart && lying.Count > 0) TakeNearest(now);
        }

        // Notes the candies about and how long each has lain still.
        private void Scan(float now)
        {
            lying.Clear();
            var stuff = SpawnedStuff();
            var eyes = Head();
            if (stuff == null || eyes == null) return;

            gone.Clear();
            foreach (var pair in seen)
            {
                if (pair.Key == null) gone.Add(pair.Key);
            }
            foreach (var key in gone) seen.Remove(key);

            foreach (Transform child in stuff)
            {
                var candy = child.gameObject;
                if (!IsCandy(candy) || (child.position - eyes.position).sqrMagnitude > reach * reach) continue;
                if (!seen.TryGetValue(candy, out var note))
                {
                    note = new Seen { firstSeen = now };
                    seen[candy] = note;
                }
                var body = candy.GetComponent<Rigidbody>();
                bool still = body == null || body.linearVelocity.sqrMagnitude < StillSpeed * StillSpeed;
                if (!still) note.stillSince = -1f;
                else if (note.stillSince < 0f) note.stillSince = now;
                bool settled = note.stillSince >= 0f && now - note.stillSince >= settleSeconds;
                if ((settled || now - note.firstSeen >= takeAnywayAfter) && Free(candy)) lying.Add(candy);
            }
        }

        // The nearest one sets off.
        private void TakeNearest(float now)
        {
            var eyes = Head();
            if (eyes == null) return;
            GameObject best = null;
            float bestDistance = float.MaxValue;
            foreach (var candy in lying)
            {
                if (candy == null || !Free(candy)) continue;
                float d = (candy.transform.position - eyes.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    best = candy;
                    bestDistance = d;
                }
            }
            if (best == null) return;
            lying.Remove(best);
            Launch(best, now);
            nextStart = now + nextEvery;
        }

        private void Launch(GameObject candy, float now)
        {
            var damage = FsmOf(candy, DamageFsm);
            if (damage == null) return;
            // It can't break any more (its Health FSM also breaks it by itself 60 s after it appeared) and it isn't
            // a thing to grab or bump into while it flies.
            var health = FsmOf(candy, HealthFsm);
            if (health != null) health.enabled = false;
            if (candy.TryGetComponent<XRGrabInteractable>(out var grab)) grab.enabled = false;
            foreach (var part in candy.GetComponentsInChildren<Collider>())
            {
                if (!part.isTrigger) part.enabled = false;
            }
            if (candy.TryGetComponent<Rigidbody>(out var body))
            {
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
            }

            float distance = Vector3.Distance(candy.transform.position, MouthPoint());
            flights.Add(new Flight
            {
                candy = candy,
                damage = damage,
                from = candy.transform.position,
                start = now,
                seconds = Mathf.Clamp(0.35f + distance * 0.06f, 0.4f, 1.4f),
                arc = Mathf.Clamp(0.4f + distance * 0.08f, 0.4f, 2f),
                spin = Random.onUnitSphere * Random.Range(360f, 720f),
            });
        }

        // Moves it along; true when it's done with (eaten and shrunk away, or gone).
        private bool Fly(Flight flight, float now)
        {
            if (flight.candy == null) return true;
            var target = MouthPoint();
            if (flight.eaten)
            {
                // Its eat state shrinks it away (and destroys it); it stays at the mouth meanwhile.
                flight.candy.transform.position = target;
                return now - flight.eatenAt > 1f;
            }

            float t = Mathf.Clamp01((now - flight.start) / flight.seconds);
            // Slow off the ground, faster and faster into the mouth, over a little arc.
            float k = t * t * (3f - 2f * t) * 0.35f + t * t * 0.65f;
            var straight = Vector3.LerpUnclamped(flight.from, target, k);
            flight.candy.transform.position = straight + Vector3.up * (flight.arc * 4f * k * (1f - k));
            flight.candy.transform.Rotate(flight.spin * Time.deltaTime, Space.World);
            if (t < 1f && (target - flight.candy.transform.position).sqrMagnitude > 0.0225f) return false;

            Eat(flight);
            flight.eaten = true;
            flight.eatenAt = now;
            return false;
        }

        // The candy's own eat: heal, om nom, +1 Candy, shrink away.
        private static void Eat(Flight flight)
        {
            var fsm = flight.damage;
            if (fsm == null) return;
            string state = fsm.ActiveStateName;
            if (state == EatState || state == DoneState) return;
            if (!fsm.enabled) fsm.enabled = true;
            fsm.SetState(EatState);
        }

        // A candy the helmet can take: lying about, in nobody's hand, not already flying or busy being hit or eaten.
        private bool Free(GameObject candy)
        {
            if (candy == null) return false;
            foreach (var flight in flights)
            {
                if (flight.candy == candy) return false;
            }
            return Takeable(candy);
        }

        // Lying about: not in a hand, not flying (kinematic) and not busy being hit or eaten.
        private static bool Takeable(GameObject candy)
        {
            if (candy.TryGetComponent<XRGrabInteractable>(out var grab) && (grab.isSelected || !grab.enabled)) return false;
            if (candy.TryGetComponent<Rigidbody>(out var body) && body.isKinematic) return false;
            var damage = FsmOf(candy, DamageFsm);
            return damage != null && damage.enabled && damage.ActiveStateName == ListeningState;
        }

        private static bool IsCandy(GameObject candy)
        {
            return candy.activeInHierarchy && CandyName.IsMatch(Game.PlayGround.CookingIngredient.BaseName(candy.name));
        }

        private static PlayMakerFSM FsmOf(GameObject candy, string fsmName)
        {
            foreach (var fsm in candy.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == fsmName) return fsm;
            }
            return null;
        }

        private Vector3 MouthPoint()
        {
            if (mouth == null)
            {
                var found = GameObject.FindWithTag(MouthTag);
                if (found != null) mouth = found.transform;
            }
            if (mouth != null) return mouth.position;
            var eyes = Head();
            return eyes != null ? eyes.TransformPoint(MouthOffset) : transform.position;
        }

        private Transform Head()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }

        // Candies are spawned under the global CurrentlySpawnedStuffs.
        private static Transform SpawnedStuff()
        {
            var stuff = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            return stuff != null && stuff.Value != null ? stuff.Value.transform : null;
        }
    }
}
