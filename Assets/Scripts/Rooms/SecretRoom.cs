using System.Collections;
using System.Linq;
using Game.Bonus;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Game.Rooms
{
    /// <summary>
    /// The bedroom's secret room (user, 2026-11-20): behind the big room's chained door, a room like the bedroom (same
    /// size and light, no second door, no window) that isn't there until the player breaks that door down.
    /// - The door breaks: a big smoke puff in the doorway, and the room pops up behind it like the kit rooms do (its walls
    ///   unfold, its roof flies in; its floor grows in). The north fall edge moves back to the new floor's edge.
    /// - It stays dark until the player walks in; then its lamp comes on.
    /// - In its middle stands a giant pumpkin, almost up to the ceiling. Broken (its own health, 2), it either bursts
    ///   into Candy Count random candies, or a Giant Spider climbs out (Spider Chance).
    /// - When the bedroom goes (the fight starts: Enemy1 - Wait turns EnemyManager 1 on and plays Bedroom_GO), the secret
    ///   room breaks away whole (user: "when the EnemyManager is on, break the secret room"): smoke, its walls and roof
    ///   go, then its floor; the fall edges come back over where it was, so anything left in it (candies, a Giant
    ///   Spider) falls. An unbroken giant pumpkin goes with it.
    /// Built by Tools > 429 Game > Rooms > Build Bedroom Kit Room (on Bedroom_Kit_Base).
    /// The living room has one too (user, 2026-11-20): behind its big double door (east), the same way, with a Giant
    /// Cockroach instead of the Giant Spider (Giant Spider here means whichever giant boss bug the room lets out); it
    /// breaks away when EnemyManager 2 starts (LivingRoom_GO). Built by Build Living Room Kit Room.
    /// </summary>
    [DisallowMultipleComponent]
    public class SecretRoom : MonoBehaviour
    {
        [Header("The room")]
        [SerializeField] private RoomKit kit;
        [Tooltip("The set the door is in: the secret room can only open while it stands.")]
        [SerializeField] private string hostSet = "Big";
        [Tooltip("The secret room's own set in the Room Kit.")]
        [SerializeField] private string secretSet = "Secret";
        [Tooltip("The door that opens it when it breaks (switched off or gone).")]
        [SerializeField] private GameObject door;
        [Tooltip("The doorway's NavMesh obstacle: switched off once the door is down, so enemies can walk through.")]
        [SerializeField] private NavMeshObstacle doorwayObstacle;
        [Tooltip("The secret room's floor (tiles and their colliders), hidden until it opens.")]
        [SerializeField] private Transform floor;
        [Tooltip("Its teleport area (the walkable floor for teleporting and the NavMesh), off until it opens.")]
        [SerializeField] private GameObject teleportArea;
        [Tooltip("Its room light, off until the player walks in.")]
        [SerializeField] private Light roomLight;
        [Tooltip("Inside the room (this object's space): where the player's head counts as walked in.")]
        [SerializeField] private Bounds inside = new(new Vector3(0f, 1.5f, 7.85f), new Vector3(7.3f, 3f, 7.3f));
        [Min(0.05f)]
        [SerializeField] private float comeSeconds = 2.67f;
        [Min(0.05f)]
        [SerializeField] private float goSeconds = 3.33f;
        [Min(0.05f)]
        [SerializeField] private float floorGrowSeconds = 0.8f;

        [Header("Fall edges (moved back when it opens)")]
        [Tooltip("The secret room lies along this object's +X (the living room's, east) instead of +Z (the bedroom's, north).")]
        [SerializeField] private bool alongX;
        [Tooltip("The fall edge past the host room's wall on the secret room's side: it moves back to the new floor's edge.")]
        [FormerlySerializedAs("fallEdgeNorth")]
        [SerializeField] private BoxCollider farEdge;
        [Tooltip("More fall edges on that side, moved back the same way.")]
        [SerializeField] private BoxCollider[] moreFarEdges = System.Array.Empty<BoxCollider>();
        [Tooltip("The fall edges along the two other sides: they reach along to the new floor's edge.")]
        [FormerlySerializedAs("fallEdgeEast")]
        [SerializeField] private BoxCollider sideEdgeA;
        [FormerlySerializedAs("fallEdgeWest")]
        [SerializeField] private BoxCollider sideEdgeB;
        [SerializeField] private BoxCollider[] moreSideEdges = System.Array.Empty<BoxCollider>();
        [Tooltip("The new floor's far edge, this object's space (z, or x when Along X).")]
        [FormerlySerializedAs("newNorthEdge")]
        [SerializeField] private float newFarEdge = 13.542f;

        [Header("Opening")]
        [Tooltip("The big smoke puff in the doorway when the door breaks.")]
        [SerializeField] private GameObject openPuff;
        [Min(0.01f)]
        [SerializeField] private float openPuffSize = 2.5f;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip lightOnSound;

        [Header("The giant pumpkin")]
        [SerializeField] private GameObject giantPumpkin;
        [Tooltip("Where it stands (this object's space).")]
        [SerializeField] private Vector3 pumpkinSpot = new(0f, 0f, 7.85f);
        [Tooltip("Chance (0 to 1) that the giant boss bug (Giant Spider / Giant Cockroach) climbs out; otherwise it bursts into candies.")]
        [Range(0f, 1f)]
        [SerializeField] private float spiderChance = 0.5f;
        [Tooltip("The giant boss bug that can climb out (Spider_Giant in the bedroom, Cockroach_Giant in the living room).")]
        [SerializeField] private GameObject giantSpider;
        [Min(0)]
        [SerializeField] private int candyCount = 33;
        [Tooltip("The candies it bursts into, picked at random.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Tooltip("Its pieces when it breaks (scaled to the giant).")]
        [SerializeField] private GameObject brokenPieces;
        [SerializeField] private GameObject breakPuff;
        [Min(0.01f)]
        [SerializeField] private float breakPuffSize = 3f;
        [SerializeField] private AudioClip candySound;
        [SerializeField] private AudioClip spiderSound;

        [Header("Hatching shake (user, 2026-11-20: \"like an egg about to hatch\")")]
        [Tooltip("Seconds between shakes (random in this range); after its first hit, half as long.")]
        [SerializeField] private Vector2 shakeEvery = new(2f, 4.5f);
        [Min(0.05f)]
        [SerializeField] private float shakeSeconds = 0.7f;
        [Tooltip("How far it rocks on its bottom (degrees), the first rock; the next ones die down.")]
        [SerializeField] private float shakeDegrees = 6f;
        [Tooltip("Rocks back and forth in one shake.")]
        [Min(1)]
        [SerializeField] private int shakeRocks = 3;

        /// <summary>Test hook: the next giant pumpkin gives the spider (true) or the candies (false); null rolls.</summary>
        public static bool? ForceSpider;

        /// <summary>Whether the secret room has opened.</summary>
        public bool IsOpen => opened;
        /// <summary>Whether its light is on (the player walked in).</summary>
        public bool IsLit => lit;
        /// <summary>The giant pumpkin while it stands.</summary>
        public GameObject Pumpkin => pumpkin;
        /// <summary>What the giant pumpkin gave: "", "spider" or "candies".</summary>
        public string Outcome { get; private set; } = "";
        /// <summary>The Giant Spider it let out, if any.</summary>
        public GameObject Spider { get; private set; }

        private bool hostWasUp, opened, lit, folded;
        private GameObject pumpkin;
        private HutongGames.PlayMaker.FsmFloat pumpkinHealth;
        private float lastPumpkinHealth = 1f;
        private Vector3 pumpkinAt;
        private Vector3[] floorScales;
        private Renderer[] floorRenderers;
        private Collider[] floorColliders;

        private void Awake()
        {
            if (kit == null) kit = GetComponent<RoomKit>();
            if (floor != null)
            {
                floorRenderers = floor.GetComponentsInChildren<Renderer>(true);
                floorColliders = floor.GetComponentsInChildren<Collider>(true);
                floorScales = new Vector3[floor.childCount];
                for (int i = 0; i < floor.childCount; i++) floorScales[i] = floor.GetChild(i).localScale;
                ShowFloor(false);
            }
            if (teleportArea != null) teleportArea.SetActive(false);
            if (roomLight != null) roomLight.gameObject.SetActive(false);
            // The fall edges as they were, to put back when the room breaks.
            edgeBoxes = new[] { farEdge, sideEdgeA, sideEdgeB }.Concat(moreFarEdges).Concat(moreSideEdges).ToArray();
            edgeCenters = new Vector3[edgeBoxes.Length];
            edgeSizes = new Vector3[edgeBoxes.Length];
            for (int i = 0; i < edgeBoxes.Length; i++)
                if (edgeBoxes[i] != null) { edgeCenters[i] = edgeBoxes[i].center; edgeSizes[i] = edgeBoxes[i].size; }
        }

        private BoxCollider[] edgeBoxes;
        private Vector3[] edgeCenters, edgeSizes;

        private void Update()
        {
            if (kit == null) return;
            bool hostUp = kit.IsUp(hostSet);
            if (!opened)
            {
                // Only a door broken while its room stands opens it.
                if (hostUp && (door == null || !door.activeInHierarchy)) Open();
                hostWasUp |= hostUp;
                return;
            }
            // The bedroom goes (its go started, or it's gone): the secret room goes with it.
            if (!folded && hostWasUp && !hostUp) Fold();
            hostWasUp |= hostUp;
            // The doorway stays open for enemies (the room kit puts its walls' NavMesh blocks back when they stand).
            if (doorwayObstacle != null && doorwayObstacle.enabled) doorwayObstacle.enabled = false;

            if (!lit && !folded && kit.IsUp(secretSet) && PlayerInside()) LightOn();
            WatchPumpkin();
        }

        private void Open()
        {
            opened = true;
            var doorway = door != null ? door.transform.position : transform.position;
            if (openPuff != null) EffectPool.Play(openPuff, new Vector3(doorway.x, transform.position.y + 0.9f, doorway.z), openPuffSize);
            if (openSound != null) AudioSource.PlayClipAtPoint(openSound, doorway);
            if (doorwayObstacle != null) doorwayObstacle.enabled = false;
            kit.Play(secretSet, true, comeSeconds);
            StartCoroutine(GrowFloor());
            if (teleportArea != null) teleportArea.SetActive(true);
            MoveFallEdges();
            StartCoroutine(PumpkinWhenUp());
        }

        private IEnumerator GrowFloor()
        {
            if (floor == null) yield break;
            ShowFloor(true);
            for (float t = 0f; t < floorGrowSeconds; t += Time.deltaTime)
            {
                float k = Mathf.Max(0.001f, EaseOutBack(t / floorGrowSeconds));
                for (int i = 0; i < floor.childCount; i++) floor.GetChild(i).localScale = floorScales[i] * k;
                yield return null;
            }
            for (int i = 0; i < floor.childCount; i++) floor.GetChild(i).localScale = floorScales[i];
        }

        private void ShowFloor(bool on)
        {
            if (floorRenderers != null) foreach (var r in floorRenderers) if (r != null) r.enabled = on;
            if (floorColliders != null) foreach (var c in floorColliders) if (c != null) c.enabled = on;
        }

        // The far edge(s) move back to the new floor's edge; the side ones reach along it. (The fall edges' boxes are
        // unturned to this object, as all the rooms' are.)
        private void MoveFallEdges()
        {
            // A far edge keeps its far end, or (a thin one, e.g. the living room's round-1 edge) its depth past the new edge.
            foreach (var far in new[] { farEdge }.Concat(moreFarEdges))
            {
                if (far == null) continue;
                float near = RoomAlong(far, Along(far.center) - Along(far.size) / 2f);
                float end = RoomAlong(far, Along(far.center) + Along(far.size) / 2f);
                SetAlong(far, newFarEdge, Mathf.Max(end, newFarEdge + Mathf.Abs(end - near)));
            }
            foreach (var side in new[] { sideEdgeA, sideEdgeB }.Concat(moreSideEdges))
                if (side != null) SetAlong(side, RoomAlong(side, Along(side.center) - Along(side.size) / 2f), newFarEdge);
            Physics.SyncTransforms();
        }

        private float Along(Vector3 v) => alongX ? v.x : v.z;

        private Vector3 WithAlong(Vector3 v, float a)
        {
            if (alongX) v.x = a; else v.z = a;
            return v;
        }

        // A point along the secret room's axis on a fall edge's box (its own space), in this object's space.
        private float RoomAlong(BoxCollider b, float a) =>
            Along(transform.InverseTransformPoint(b.transform.TransformPoint(WithAlong(b.center, a))));

        // Sets a fall edge's box to reach from one point to another along the axis (this object's space), and the NavMesh
        // hole it carves.
        private void SetAlong(BoxCollider b, float from, float to)
        {
            var t = b.transform;
            float a = Along(t.InverseTransformPoint(transform.TransformPoint(WithAlong(Vector3.zero, from))));
            float c = Along(t.InverseTransformPoint(transform.TransformPoint(WithAlong(Vector3.zero, to))));
            var center = WithAlong(b.center, (a + c) / 2f);
            var size = WithAlong(b.size, Mathf.Max(0.1f, Mathf.Abs(c - a)));
            b.center = center;
            b.size = size;
            if (b.TryGetComponent(out NavMeshObstacle hole))
            {
                hole.center = center;
                hole.size = size;
            }
        }

        private IEnumerator PumpkinWhenUp()
        {
            // The room is up before the pumpkin appears in it.
            yield return new WaitForSeconds(comeSeconds * 0.85f);
            if (giantPumpkin == null || folded) yield break;
            var at = transform.TransformPoint(pumpkinSpot);
            pumpkin = Instantiate(giantPumpkin, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            pumpkin.name = giantPumpkin.name;
            // Whole on the floor (user, 2026-11-20: "not half"): its pivot is its middle, so lift its bottom to the spot.
            if (pumpkin.TryGetComponent(out Renderer look))
            {
                pumpkin.transform.position += Vector3.up * (at.y - look.bounds.min.y);
                Physics.SyncTransforms();
            }
            pumpkinAt = at;
            foreach (var fsm in pumpkin.GetComponents<PlayMakerFSM>())
            {
                var h = fsm.FsmVariables.FindFsmFloat("health");
                if (fsm.FsmName == "Health" && h != null) { pumpkinHealth = h; lastPumpkinHealth = h.Value; startPumpkinHealth = h.Value; }
            }
            if (breakPuff != null) EffectPool.Play(breakPuff, at + Vector3.up * 0.3f, breakPuffSize * 0.6f);
            // 429R 2026-11-27: it may hold a piece of the Knight's armor (likelier than a room's furniture).
            Game.Combat.KnightArmorHider.Instance?.SecretPumpkinUp(this, pumpkin.transform);
            // Its round bottom's edge (it rocks on it, never into the floor), measured before it pops in from small.
            float rim = pumpkin.TryGetComponent(out Renderer body) ? 0.95f * Mathf.Min(body.bounds.extents.x, body.bounds.extents.z) : 1.2f;
            StartCoroutine(PopIn(pumpkin.transform));
            StartCoroutine(Hatching(pumpkin.transform, at, rim));
        }

        // Now and then it rocks on its bottom a few times, dying down, as if something inside wants out; more often once hit.
        private IEnumerator Hatching(Transform t, Vector3 bottom, float rim)
        {
            yield return new WaitForSeconds(1f);
            while (t != null && !folded)
            {
                bool hit = HitOnce;
                float wait = Random.Range(shakeEvery.x, shakeEvery.y) * (hit ? 0.5f : 1f);
                yield return new WaitForSeconds(wait);
                if (t == null || folded) yield break;
                var restPos = t.position;
                var restRot = t.rotation;
                // A positive angle tips its top toward "toward", on the edge on that side; a negative one the other way.
                var toward = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                var axis = Vector3.Cross(Vector3.up, toward);
                float degrees = shakeDegrees * (hit ? 1.4f : 1f);
                for (float time = 0f; time < shakeSeconds && t != null; time += Time.deltaTime)
                {
                    float k = time / shakeSeconds;
                    float angle = Mathf.Sin(k * shakeRocks * 2f * Mathf.PI) * degrees * (1f - k);
                    t.SetPositionAndRotation(restPos, restRot);
                    t.RotateAround(bottom + toward * (rim * Mathf.Sign(angle)), axis, angle);
                    yield return null;
                }
                if (t != null) t.SetPositionAndRotation(restPos, restRot);
            }
        }

        // Whether the giant pumpkin has been hit once (it starts at 2 health).
        private bool HitOnce => pumpkinHealth != null && pumpkinHealth.Value < startPumpkinHealth;
        private float startPumpkinHealth = 2f;

        private static IEnumerator PopIn(Transform t)
        {
            var full = t.localScale;
            for (float time = 0f; time < 0.45f && t != null; time += Time.deltaTime)
            {
                t.localScale = full * Mathf.Max(0.01f, EaseOutBack(time / 0.45f));
                yield return null;
            }
            if (t != null) t.localScale = full;
        }

        private bool PlayerInside()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            return inside.Contains(transform.InverseTransformPoint(cam.transform.position));
        }

        private void LightOn()
        {
            lit = true;
            if (roomLight == null) return;
            roomLight.gameObject.SetActive(true);
            if (lightOnSound != null) AudioSource.PlayClipAtPoint(lightOnSound, roomLight.transform.position);
        }

        // Broken by the player (its health ran out): the roll. Popped any other way (the fairy shield): nothing.
        private void WatchPumpkin()
        {
            if (pumpkinHealth == null || Outcome != "") return;
            bool gone = pumpkin == null || !pumpkin.activeInHierarchy;
            if (!gone)
            {
                lastPumpkinHealth = pumpkinHealth.Value;
                pumpkinAt = pumpkin.transform.position;
                if (lastPumpkinHealth > 0f) return;
            }
            if (lastPumpkinHealth > 0f)
            {
                pumpkinHealth = null;
                Game.Combat.KnightArmorHider.Instance?.SecretPumpkinGone(this);
                return;
            }
            Broke();
        }

        private void Broke()
        {
            float scale = giantPumpkin != null ? giantPumpkin.transform.localScale.x : 1f;
            if (breakPuff != null) EffectPool.Play(breakPuff, pumpkinAt + Vector3.up * 1.2f, breakPuffSize);
            if (brokenPieces != null)
            {
                var pieces = Instantiate(brokenPieces, pumpkinAt + Vector3.up * 0.2f, Quaternion.identity, Holder());
                pieces.transform.localScale *= scale;
            }
            bool spider = ForceSpider ?? Random.value < spiderChance;
            ForceSpider = null;
            if (spider && giantSpider != null)
            {
                Outcome = "spider";
                var at = pumpkinAt;
                if (NavMesh.SamplePosition(at, out var hit, 3f, NavMesh.AllAreas)) at = hit.position;
                Spider = Instantiate(giantSpider, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Holder());
                Spider.name = giantSpider.name + "(Clone)";
                if (Spider.TryGetComponent(out Game.Enemies.SpiderBrain brain)) brain.ComeAtOnce();
                if (spiderSound != null) AudioSource.PlayClipAtPoint(spiderSound, at);
            }
            else
            {
                Outcome = "candies";
                CandyFountain.Play(pumpkinAt + Vector3.up * 1.2f, candyCount, candies, 1.5f, new Vector2(2.5f, 5f), new Vector2(1f, 3f));
                if (candySound != null) AudioSource.PlayClipAtPoint(candySound, pumpkinAt);
            }
            // The armor piece it held (if any) pops out too.
            Game.Combat.KnightArmorHider.Instance?.SecretPumpkinBroke(this, pumpkinAt + Vector3.up * 1.2f);
            if (pumpkin != null) Destroy(pumpkin);
        }

        // The fight starts (user, 2026-11-20: "when the EnemyManager is on, break the secret room"): it breaks away whole,
        // floor and all, in smoke; whatever is still in it falls (the fall edges come back over its floor).
        private void Fold()
        {
            folded = true;
            kit.Play(secretSet, false, goSeconds);
            if (roomLight != null) roomLight.gameObject.SetActive(false);
            var middle = transform.TransformPoint(pumpkinSpot);
            if (breakPuff != null) EffectPool.Play(breakPuff, middle + Vector3.up * 1.2f, breakPuffSize * 1.4f);
            if (openPuff != null) EffectPool.Play(openPuff, middle + Vector3.up * 0.5f, openPuffSize);
            if (openSound != null) AudioSource.PlayClipAtPoint(openSound, middle);
            // An unbroken giant pumpkin goes with its room.
            if (pumpkin != null && Outcome == "")
            {
                pumpkinHealth = null;
                Destroy(pumpkin);
                // What it held hides again in the next room.
                Game.Combat.KnightArmorHider.Instance?.SecretPumpkinGone(this);
            }
            StartCoroutine(BreakFloor());
        }

        private IEnumerator BreakFloor()
        {
            yield return new WaitForSeconds(goSeconds * 0.5f);
            if (floor != null)
            {
                foreach (var c in floorColliders) if (c != null) c.enabled = false;
                for (float t = 0f; t < floorGrowSeconds; t += Time.deltaTime)
                {
                    float k = Mathf.Max(0.001f, 1f - t / floorGrowSeconds);
                    for (int i = 0; i < floor.childCount; i++) floor.GetChild(i).localScale = floorScales[i] * k;
                    yield return null;
                }
                ShowFloor(false);
                for (int i = 0; i < floor.childCount; i++) floor.GetChild(i).localScale = floorScales[i];
            }
            if (teleportArea != null) teleportArea.SetActive(false);
            RestoreFallEdges();
        }

        private void RestoreFallEdges()
        {
            if (edgeBoxes == null) return;
            for (int i = 0; i < edgeBoxes.Length; i++)
            {
                var b = edgeBoxes[i];
                if (b == null) continue;
                b.center = edgeCenters[i];
                b.size = edgeSizes[i];
                if (b.TryGetComponent(out NavMeshObstacle hole)) { hole.center = edgeCenters[i]; hole.size = edgeSizes[i]; }
            }
            Physics.SyncTransforms();
        }

        private static Transform Holder()
        {
            var v = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            return v != null && v.Value != null ? v.Value.transform : null;
        }

        private static float EaseOutBack(float t)
        {
            const float s = 1.4f;
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
