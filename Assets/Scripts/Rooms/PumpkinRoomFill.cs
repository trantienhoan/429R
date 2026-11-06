using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// A pumpkin room (user, 2026-11-06): the Pumpkin Skull's room, the Stuffs room without its furniture, only the
    /// stage's laughing pumpkin and pumpkins all over the floor. Goes on a spot in the room's set (--Bedroom_Pumpkins--,
    /// --MiniBedroom_Pumpkins--). When the set comes on, a runner is made under CurrentlySpawnedStuffs (the set switches
    /// itself off after a few seconds) that:
    /// - raises the Pumpkin_On_Tree limit (Live Limit) for the room, so the room can be full;
    /// - rains Start Count pumpkins down from the ceiling over Rain Seconds, all over the floor (not on the player, the
    ///   stage pumpkin or each other);
    /// - while the room's Enemy Manager hasn't started, drops Refill Count more from the ceiling whenever fewer than
    ///   Refill Below are left;
    /// - once the room's fight is over (GAMESTAGES has left Fight State), puts the limit back and goes.
    /// Tools > 429 Game > Rooms > Set Up Magic Hats + Pumpkin Rooms makes the sets and fills this in.
    /// </summary>
    public class PumpkinRoomFill : MonoBehaviour
    {
        [Header("The pumpkins")]
        [Tooltip("What fills the room (Pumpkin_On_Tree).")]
        [SerializeField] private GameObject fill;
        [Min(0)]
        [SerializeField] private int startCount = 45;
        [Tooltip("Fewer than this left before the enemies come: Refill Count more drop from the ceiling.")]
        [Min(0)]
        [SerializeField] private int refillBelow = 12;
        [Min(1)]
        [SerializeField] private int refillCount = 12;
        [Min(0.1f)]
        [SerializeField] private float rainSeconds = 3f;
        [Min(0.1f)]
        [SerializeField] private float refillSeconds = 1.2f;
        [Tooltip("The Live Limit group raised for the room, and to how many.")]
        [SerializeField] private string limitGroup = "Pumpkin_On_Tree";
        [Min(1)]
        [SerializeField] private int roomLimit = 140;

        [Header("The room")]
        [Tooltip("The floor's area (world space): the Stuffs room's spots, a little wider.")]
        [SerializeField] private Bounds area = new(Vector3.zero, new Vector3(6f, 1f, 6f));
        [Tooltip("The floor's height (world space).")]
        [SerializeField] private float floorY;
        [Tooltip("Dropped this far under the ceiling (found by a ray up), at most this high over the floor, in metres.")]
        [SerializeField] private float underCeiling = 0.3f;
        [SerializeField] private float highest = 2.8f;
        [Tooltip("Kept this far from the player's head, the stage pumpkin and each other, in metres (flat).")]
        [SerializeField] private float clearOfPlayer = 1f;
        [SerializeField] private float clearOfStagePumpkin = 0.9f;
        [SerializeField] private float spacing = 0.32f;
        [Tooltip("The stage's pumpkin (its name without (Clone)), kept clear of.")]
        [SerializeField] private string stagePumpkin = "Pumpkin_2";

        [Header("When to stop")]
        [Tooltip("The room's Enemy Manager: once it's on, no more refills.")]
        [SerializeField] private GameObject enemyManager;
        [Tooltip("GAMESTAGES' state for the room's fight; once it has moved on from it, the room is done.")]
        [SerializeField] private string fightState = "Enemy1 - Wait";
        [SerializeField] private float startDelay = 0.8f;

        private bool runner;
        private bool lowered;
        private float ceilingY;
        // Whether the room has a ceiling over its middle: then every spot must have one over it (not outside the walls).
        private bool roofed;
        private Transform stage;
        private readonly List<GameObject> fillers = new();
        private readonly List<Vector3> planned = new();
        private PlayMakerFSM gameStages;

        private void OnEnable()
        {
            if (runner) return;
            // The set goes off after a few seconds: the work is done by a runner of its own.
            var parentVar = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            var go = new GameObject("Pumpkin Room (" + transform.parent.name + ")");
            if (parentVar != null && parentVar.Value != null) go.transform.SetParent(parentVar.Value.transform, false);
            go.SetActive(false);
            var copy = go.AddComponent<PumpkinRoomFill>();
            copy.runner = true;
            copy.fill = fill; copy.startCount = startCount; copy.refillBelow = refillBelow; copy.refillCount = refillCount;
            copy.rainSeconds = rainSeconds; copy.refillSeconds = refillSeconds; copy.limitGroup = limitGroup; copy.roomLimit = roomLimit;
            copy.area = area; copy.floorY = floorY; copy.underCeiling = underCeiling; copy.highest = highest;
            copy.clearOfPlayer = clearOfPlayer; copy.clearOfStagePumpkin = clearOfStagePumpkin; copy.spacing = spacing;
            copy.stagePumpkin = stagePumpkin; copy.enemyManager = enemyManager; copy.fightState = fightState; copy.startDelay = startDelay;
            go.SetActive(true);
        }

        private void Start()
        {
            if (runner) StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (runner && !lowered) LiveLimit.LowerLimit(limitGroup);
        }

        /// <summary>How many of its pumpkins are left (Play tests).</summary>
        public int Left => fillers.Count;

        /// <summary>How many it has dropped, and how many it couldn't find a free spot for (Play tests).</summary>
        public int Dropped { get; private set; }
        public int NoSpot { get; private set; }

        private IEnumerator Run()
        {
            LiveLimit.RaiseLimit(limitGroup, roomLimit);
            foreach (var f in FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.FsmName == "GAMESTAGES" && f.gameObject.scene.IsValid()) { gameStages = f; break; }
            }
            yield return new WaitForSeconds(startDelay);
            FindCeiling();
            yield return Rain(startCount, rainSeconds);

            // Refilled until the enemies come.
            bool fought = false;
            while (true)
            {
                string state = gameStages != null ? gameStages.ActiveStateName : "";
                if (state == fightState) fought = true;
                bool enemies = fought || (enemyManager != null && enemyManager.activeInHierarchy);
                if (enemies) break;
                fillers.RemoveAll(Broken);
                if (fillers.Count < refillBelow) yield return Rain(refillCount, refillSeconds);
                yield return new WaitForSeconds(0.5f);
            }
            // The fight is on; once GAMESTAGES has moved on from it, the room is done: the limit goes back.
            while (gameStages != null && (gameStages.ActiveStateName == fightState || !fought))
            {
                if (gameStages.ActiveStateName == fightState) fought = true;
                fillers.RemoveAll(Broken);
                yield return new WaitForSeconds(1f);
            }
            LiveLimit.LowerLimit(limitGroup);
            lowered = true;
            Destroy(gameObject);
        }

        private static bool Broken(GameObject pumpkin)
        {
            if (pumpkin == null || !pumpkin.activeInHierarchy) return true;
            foreach (var fsm in pumpkin.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "Health") continue;
                var health = fsm.FsmVariables.FindFsmFloat("health");
                return health != null && health.Value <= 0f;
            }
            return false;
        }

        private void FindCeiling()
        {
            // The real floor under the middle (the set's spots only give it roughly).
            var over = new Vector3(area.center.x, floorY + 1.5f, area.center.z);
            if (Physics.Raycast(over, Vector3.down, out var floor, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && floor.normal.y > 0.7f)
                floorY = floor.point.y;
            var from = new Vector3(area.center.x, floorY + 1f, area.center.z);
            roofed = Physics.Raycast(from, Vector3.up, out var hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            ceilingY = roofed ? hit.point.y - underCeiling : floorY + highest;
            ceilingY = Mathf.Min(ceilingY, floorY + highest);
        }

        // Count pumpkins down from under the ceiling, spread over Seconds.
        private IEnumerator Rain(int count, float seconds)
        {
            if (fill == null || count <= 0) yield break;
            if (stage == null) stage = FindStagePumpkin();
            planned.Clear();
            foreach (var f in fillers) if (f != null) planned.Add(f.transform.position);
            var parent = transform.parent;
            float gap = seconds / count;
            for (int i = 0; i < count; i++)
            {
                if (Pick(out var at))
                {
                    var made = Instantiate(fill, at, Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f)), parent);
                    if (made.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                        body.angularVelocity = Random.insideUnitSphere * 2f;
                    fillers.Add(made);
                    planned.Add(at);
                    Dropped++;
                }
                else NoSpot++;
                yield return new WaitForSeconds(gap);
            }
        }

        // A free spot on the floor, under the ceiling: not by the player, the stage pumpkin or another pumpkin.
        private bool Pick(out Vector3 at)
        {
            var head = Camera.main != null ? Camera.main.transform.position : new Vector3(9999f, 0f, 9999f);
            for (int tries = 0; tries < 24; tries++)
            {
                float x = Random.Range(area.min.x, area.max.x), z = Random.Range(area.min.z, area.max.z);
                var top = new Vector3(x, ceilingY, z);
                if (!Physics.Raycast(top, Vector3.down, out var hit, ceilingY - floorY + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.7f || Mathf.Abs(hit.point.y - floorY) > 0.35f) continue;
                // Inside the room: under its ceiling too (outside the walls there's none).
                if (roofed && !Physics.Raycast(hit.point + Vector3.up * 0.3f, Vector3.up, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (Flat(top - head) < clearOfPlayer) continue;
                if (stage != null && Flat(top - stage.position) < clearOfStagePumpkin) continue;
                bool crowded = false;
                foreach (var p in planned) if (Flat(top - p) < spacing) { crowded = true; break; }
                if (crowded) continue;
                at = top;
                return true;
            }
            at = default;
            return false;
        }

        private Transform FindStagePumpkin()
        {
            if (string.IsNullOrEmpty(stagePumpkin) || transform.parent == null) return null;
            foreach (Transform t in transform.parent)
            {
                if (t.name.Replace("(Clone)", "").Trim() == stagePumpkin) return t;
            }
            return null;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
