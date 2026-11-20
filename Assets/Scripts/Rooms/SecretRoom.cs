using System.Collections;
using Game.Bonus;
using UnityEngine;
using UnityEngine.AI;

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
        [SerializeField] private BoxCollider fallEdgeNorth;
        [SerializeField] private BoxCollider fallEdgeEast;
        [SerializeField] private BoxCollider fallEdgeWest;
        [Tooltip("The new floor's north edge, this object's space (z).")]
        [SerializeField] private float newNorthEdge = 13.542f;

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
        [Tooltip("Chance (0 to 1) that a Giant Spider climbs out; otherwise it bursts into candies.")]
        [Range(0f, 1f)]
        [SerializeField] private float spiderChance = 0.5f;
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
            edgeBoxes = new[] { fallEdgeNorth, fallEdgeEast, fallEdgeWest };
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

        // The north edge moves back to the new floor's edge; the east and west ones reach along it.
        private void MoveFallEdges()
        {
            if (fallEdgeNorth != null)
                SetZ(fallEdgeNorth, newNorthEdge, RoomZ(fallEdgeNorth, fallEdgeNorth.center.z + fallEdgeNorth.size.z / 2f));
            foreach (var side in new[] { fallEdgeEast, fallEdgeWest })
                if (side != null) SetZ(side, RoomZ(side, side.center.z - side.size.z / 2f), newNorthEdge);
            Physics.SyncTransforms();
        }

        // A z on a fall edge's box (its own space) in this object's space.
        private float RoomZ(BoxCollider b, float z) =>
            transform.InverseTransformPoint(b.transform.TransformPoint(new Vector3(b.center.x, b.center.y, z))).z;

        // Sets a fall edge's box to reach from one z to another (this object's space), and the NavMesh hole it carves.
        private void SetZ(BoxCollider b, float from, float to)
        {
            var t = b.transform;
            float a = t.InverseTransformPoint(transform.TransformPoint(new Vector3(0f, 0f, from))).z;
            float c = t.InverseTransformPoint(transform.TransformPoint(new Vector3(0f, 0f, to))).z;
            var center = b.center;
            var size = b.size;
            center.z = (a + c) / 2f;
            size.z = Mathf.Max(0.1f, Mathf.Abs(c - a));
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
            pumpkinAt = at;
            foreach (var fsm in pumpkin.GetComponents<PlayMakerFSM>())
            {
                var h = fsm.FsmVariables.FindFsmFloat("health");
                if (fsm.FsmName == "Health" && h != null) { pumpkinHealth = h; lastPumpkinHealth = h.Value; }
            }
            if (breakPuff != null) EffectPool.Play(breakPuff, at + Vector3.up * 0.3f, breakPuffSize * 0.6f);
            StartCoroutine(PopIn(pumpkin.transform));
        }

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
            if (lastPumpkinHealth > 0f) { pumpkinHealth = null; return; }
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
