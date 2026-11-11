using UnityEngine;

namespace Game.Shopping
{
    /// <summary>
    /// The Candy Box's treat (user, 2026-11-10: the shop's Candy Box, 26 wraps): as the broken box's drop appears it
    /// spills exactly one of each candy in Candies, evenly round a ring just above the broken pieces, tossed gently up
    /// and out so none lands hard enough to break (a candy is hurt by a knock over about 7 m/s). The falling pieces
    /// never knock a candy, and a start spot keeps clear of walls. They go into CurrentlySpawnedStuffs (cleared with the
    /// stage), like other spilled candies. Goes on the drop prefab (Candy_Box_Drop); set up by Tools > 429 Game >
    /// Balance 2026-11-10 > Shop Candies And Candy Box.
    /// </summary>
    [DisallowMultipleComponent]
    public class CandyBoxSpill : MonoBehaviour
    {
        [Tooltip("The candies it spills, one of each (the menu fills it from Candy Kinds).")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Tooltip("How far out from the middle they start, evenly round a ring, in metres.")]
        [Min(0f)]
        [SerializeField] private float ringRadius = 0.22f;
        [Tooltip("How far above the top of the broken pieces they start, in metres.")]
        [Min(0f)]
        [SerializeField] private float lift = 0.08f;
        [Tooltip("Up speed (x = least, y = most), in metres a second. Keep it gentle: a candy is hurt by a knock over 7.")]
        [SerializeField] private Vector2 upSpeed = new(1.2f, 1.6f);
        [Tooltip("Outward speed (x = least, y = most), in metres a second.")]
        [SerializeField] private Vector2 outSpeed = new(0.5f, 0.8f);
        [Tooltip("How fast they tumble, at most, in radians a second.")]
        [Min(0f)]
        [SerializeField] private float spin = 4f;

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // About a candy's size: a start spot keeps this far from walls and other solid things.
        private const float CandyRadius = 0.05f;
        private const float Margin = 0.01f;
        // A candy's solid ball (0.3 m round) is wider than the ring and the wall gap, so a candy can start inside a wall,
        // the box (solid for 0.1 s more) or the next candy. Physics pushes such a candy out at up to 10 m/s, which threw
        // them across the room and cracked them by a wall (play test 2026-11-10); this slows the push-out to an ease.
        private const float MaxPushOut = 1f;
        // How far from the drop's spot the box that broke is looked for.
        private const float BoxSearchRadius = 0.6f;
        // For this long a spilled candy can't be eaten, so a box broken in front of the face doesn't feed the player
        // (play test 2026-11-10: 1-2 of the 6 went straight into the Mouth). By then they have fallen away (re-test
        // 2026-11-11: the ones flung at the face were still within the Mouth's reach up to 0.7 s, so 0.5 s fed 2 of 30).
        private const float NoEatSeconds = 1f;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlaps = new Collider[32];

        private void Start()
        {
            Spill();
            enabled = false;
        }

        /// <summary>Spills one of each candy now (it happens by itself, once, as the drop appears).</summary>
        public void Spill()
        {
            if (candies == null || candies.Length == 0) return;

            var own = GetComponentsInChildren<Collider>();
            var middle = Middle(own, out float top);
            // Just above the pieces, unless something solid is over them (a table top).
            var start = middle + Vector3.up * Clear(middle, Vector3.up, top - middle.y + lift, own);
            var parent = SpawnedStuff();
            // The box that broke stays solid a moment more (its Health FSM waits 0.1 s) and, broken by a punch, is still
            // flying: it never knocks a candy either (play test 2026-11-10: a punched box cracked all six).
            int boxParts = BrokenBox();
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < candies.Length; i++)
            {
                var prefab = candies[i];
                if (prefab == null) continue;
                var outward = Quaternion.Euler(0f, turn + 360f * i / candies.Length, 0f) * Vector3.forward;
                var spot = start + outward * Clear(start, outward, ringRadius, own);
                var candy = Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * prefab.transform.rotation, parent);

                // The box's falling pieces never knock a candy.
                foreach (var part in candy.GetComponentsInChildren<Collider>())
                {
                    foreach (var piece in own)
                    {
                        if (piece != null) Physics.IgnoreCollision(part, piece);
                    }
                    for (int k = 0; k < boxParts; k++) Physics.IgnoreCollision(part, Overlaps[k]);
                }
                candy.AddComponent<SpilledCandyMouthDelay>().Hold(NoEatSeconds);
                if (candy.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
                {
                    body.maxDepenetrationVelocity = MaxPushOut;
                    body.linearVelocity = outward * Random.Range(outSpeed.x, outSpeed.y) + Vector3.up * Random.Range(upSpeed.x, upSpeed.y);
                    body.angularVelocity = Random.insideUnitSphere * spin;
                }
            }
        }

        // The solid colliders of whatever just broke here (its Enemy Hit Damage is dead): the box this drop came from. They
        // go to the front of Overlaps; returns how many.
        private int BrokenBox()
        {
            int found = Physics.OverlapSphereNonAlloc(transform.position, BoxSearchRadius, Overlaps, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            int kept = 0;
            for (int i = 0; i < found; i++)
            {
                var body = Overlaps[i].attachedRigidbody;
                if (body != null && body.TryGetComponent(out Game.Combat.EnemyHitDamage hit) && hit.IsDead) Overlaps[kept++] = Overlaps[i];
            }
            return kept;
        }

        // The middle of its solid pieces and the height of their top; without any, its own spot and 0.3 m up.
        private Vector3 Middle(Collider[] own, out float top)
        {
            bool any = false;
            var box = new Bounds();
            foreach (var part in own)
            {
                if (part == null || part.isTrigger || !part.enabled) continue;
                if (!any)
                {
                    box = part.bounds;
                    any = true;
                }
                else box.Encapsulate(part.bounds);
            }
            if (!any)
            {
                top = transform.position.y + 0.3f;
                return transform.position;
            }
            top = box.max.y;
            return box.center;
        }

        // How far it can go from a point in a direction (at most Reach) before a solid thing that isn't one of its own
        // pieces. Something it starts inside of doesn't count.
        private static float Clear(Vector3 from, Vector3 direction, float reach, Collider[] own)
        {
            if (reach <= 0f) return 0f;
            float clear = reach;
            int count = Physics.SphereCastNonAlloc(from, CandyRadius, direction, Hits, reach, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.collider == null || hit.distance <= 0f || System.Array.IndexOf(own, hit.collider) >= 0) continue;
                clear = Mathf.Min(clear, Mathf.Max(0f, hit.distance - Margin));
            }
            return clear;
        }

        private static Transform SpawnedStuff()
        {
            var holder = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }
}
