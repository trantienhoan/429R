using System.Collections;
using Game.Gates;
using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// Gate 1 inside the shared gate flow (user 2026-11-23: "cook and defend"; Gate Run does the card, the chests, the
    /// rounds' time limits, the HUD line and the hand-off to the finale). Goes on the PlayGround's root (Play_Ground_Z),
    /// next to its Gate Run:
    /// - Gate Run's start chest is the cauldron chest (Treasure_Chest_and_Seed_Bracer_Cauldron), dropped in the middle of
    ///   the field: its seed (Bracer) pops the cauldron out there, as it always did (smoke, a thump, the player pushed
    ///   back). Seed never eaten: Gate Run goes on after its wait and the cauldron pops out by itself;
    /// - rounds 1-3: a new recipe each (read off the magic hat, which hides somewhere new from round 2), a cook the monsters
    ///   attack (coming in at the open monster gates), then the round's mini bosses; beat them and Gate Run drops the
    ///   round's gear chest (Chest, Head, Shield) in front of the player, never on the pot;
    /// - after round 3's chest: the field is swept, the pot bursts and the gate's giant pumpkin (the stew pumpkin) drops.
    /// The HUD line says what to do now: FIND THE HAT, FILL THE POT n/m, LIGHT THE FIRE, DEFEND THE POT, BEAT THE SLIME n/m.
    /// Tools > 429 Game > PlayGround > Use Gate Run (3 Rounds) sets it up.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayGroundGame : MonoBehaviour, IGateGame
    {
        [Header("The cauldron")]
        [Tooltip("Fire_Woods_n_Cauldron (with its Cauldron Defence): popped out by the start chest's seed, or by this when " +
                 "the seed is never eaten.")]
        public GameObject cauldronPrefab;
        [Tooltip("Where the start chest drops and the cauldron pops out, in the PlayGround's own space, when its field isn't " +
                 "measured (else the field's middle).")]
        public Vector3 cauldronSpot = new(0f, 0f, 1.4f);
        [Tooltip("The PlayGround's own spawner of the start chest: switched off, as Gate Run drops the start chest now.")]
        public string oldChestSpawner = "Cauldron_Pos";
        [Tooltip("A round chest drops this far in front of the player (metres), and at least this far from the pot.")]
        [Min(0.5f)] public float chestAhead = 1.4f;
        [Min(0f)] public float chestFromPot = 1.8f;

        [Header("Rounds (1, 2, 3)")]
        public CauldronDefence.Round[] rounds =
        {
            new() { label = "1", candies = 2, pumpkins = 1, cookSeconds = 45f, maxAlive = 4, spawnWait = new Vector2(2.5f, 4f), openGates = 1 },
            new() { label = "2", candies = 3, pumpkins = 1, cookSeconds = 60f, maxAlive = 5, spawnWait = new Vector2(2f, 3.5f), openGates = 2 },
            new() { label = "3", candies = 4, pumpkins = 1, cookSeconds = 75f, maxAlive = 6, spawnWait = new Vector2(1.8f, 3f), openGates = 3 },
        };

        [Header("The HUD line (Gate Run shows it after ROUND n/3)")]
        public string findHat = "FIND THE HAT";
        public string fillPot = "FILL THE POT";
        public string lightFire = "LIGHT THE FIRE";
        public string defend = "DEFEND THE POT";
        public string beatBosses = "BEAT THE SLIME";
        public string potBroken = "THE POT IS BROKEN";

        [Header("Look")]
        [Tooltip("The house fog (6-22 m, nearly black) is lifted while the PlayGround is out, so its field and edge can be " +
                 "seen: Fog = start, end in metres (the camera sees 40 m). Put back when the PlayGround goes (the exit, a " +
                 "Game Over). Off: the house fog.")]
        public bool liftFog = true;
        public Vector2 fog = new(12f, 36f);

        /// <summary>The cauldron's defence once it has popped out (null before).</summary>
        public CauldronDefence Defence => defence;

        private GateRun run;
        private CauldronDefence defence;
        private string shownGoal;
        private int shownNeed = -1, shownDone = -1;
        private float nextGoal, nextCheck;
        private bool fogSet;
        private float fogStartWas, fogEndWas;

        private void OnEnable()
        {
            if (!liftFog || fogSet) return;
            fogStartWas = RenderSettings.fogStartDistance;
            fogEndWas = RenderSettings.fogEndDistance;
            RenderSettings.fogStartDistance = Mathf.Max(fogStartWas, fog.x);
            RenderSettings.fogEndDistance = Mathf.Max(fogEndWas, fog.y);
            fogSet = true;
        }

        private void OnDisable()
        {
            // The house fog back (the PlayGround goes at the exit, or with a Game Over).
            if (!fogSet) return;
            RenderSettings.fogStartDistance = fogStartWas;
            RenderSettings.fogEndDistance = fogEndWas;
            fogSet = false;
        }

        private void Awake()
        {
            // Gate Run drops the start chest now: the PlayGround's own stays away. (Awake runs on a switched-off component
            // too: then the old single dish keeps its own chest.)
            if (!enabled || string.IsNullOrEmpty(oldChestSpawner)) return;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != oldChestSpawner) continue;
                foreach (var fsm in t.GetComponents<PlayMakerFSM>()) fsm.enabled = false;
            }
        }

        private void Start()
        {
            // The start chest in the field's middle (the cauldron pops out of its seed there); the round chests in front of
            // the player, off the pot.
            if (TryGetComponent(out run)) run.chestSpot = ChestSpot;
        }

        // ---------------------------------------------------------------- Gate Run's calls

        public void GateStart(GateRun gate)
        {
            run = gate;
            StartCoroutine(BringCauldron());
        }

        public void RoundBegin(GateRun gate, int round)
        {
            run = gate;
            StartCoroutine(Begin(round));
        }

        public void RoundTimeUp(GateRun gate, int round)
        {
            if (defence != null) defence.EndRoundNow();
        }

        public void FinaleBegin(GateRun gate)
        {
            // A broken pot (the doom) can't burst: Gate Run's own fallback brings the giant pumpkin.
            if (defence != null && !defence.Doomed) defence.FinaleFor(gate);
        }

        // ---------------------------------------------------------------- the cauldron

        // The start chest's seed pops the cauldron out (a moment after its gear bool is set). Not there after a few
        // seconds (the seed wasn't eaten, Gate Run went on): it pops out by itself in the field's middle.
        private IEnumerator BringCauldron()
        {
            for (float until = Time.time + 4f; defence == null && Time.time < until;)
            {
                defence = FindAnyObjectByType<CauldronDefence>();
                yield return null;
            }
            if (defence == null) PopCauldron(Middle());
        }

        private IEnumerator Begin(int round)
        {
            // The cauldron first (Bring Cauldron is on it).
            for (float until = Time.time + 5f; defence == null && Time.time < until;) yield return null;
            if (defence == null) PopCauldron(Middle());
            // Its Awake and Start first (it sets the cauldron up for one dish).
            yield return null;
            if (defence == null || run == null) yield break;
            var plan = rounds != null && rounds.Length > 0 ? rounds[Mathf.Clamp(round - 1, 0, rounds.Length - 1)] : null;
            if (plan == null)
            {
                run.RoundWon();
                yield break;
            }
            // A new recipe to read: the hat hides somewhere new (round 1: where the PlayGround put it).
            if (round > 1 && TryGetComponent(out PlayGroundField field)) field.HideHatNow();
            int r = round;
            defence.BeginRound(plan, () =>
            {
                if (run != null && run.Now == GateRun.Step.Round && run.Round == r) run.RoundWon();
            });
            shownGoal = null;
        }

        private void PopCauldron(Vector3 at)
        {
            if (cauldronPrefab == null)
            {
                Debug.LogError("[PlayGround Game] No Cauldron Prefab: nothing to cook (Tools > 429 Game > PlayGround > Use Gate " +
                               "Run (3 Rounds) sets it).", this);
                return;
            }
            var holder = GateExit.Global("CurrentlySpawnedStuffs");
            var cam = Camera.main;
            var face = cam != null ? cam.transform.position - at : -transform.forward;
            face.y = 0f;
            // A little above the floor: it lands with its thump (Cauldron's Stick To Ground), and pops out (Cauldron Defence).
            var made = Instantiate(cauldronPrefab, at + Vector3.up * 0.5f,
                face.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(face) : Quaternion.identity,
                holder != null ? holder.transform : transform);
            defence = made.GetComponent<CauldronDefence>();
            Debug.Log("[PlayGround Game] The start chest's seed wasn't eaten: the cauldron popped out by itself.", this);
        }

        // One cauldron only: a start seed eaten late (after the cauldron came by itself) makes a second one, which goes.
        private void KeepOneCauldron()
        {
            if (defence == null) return;
            foreach (var other in FindObjectsByType<CauldronDefence>(FindObjectsSortMode.None))
            {
                if (other == null || other == defence) continue;
                Debug.Log("[PlayGround Game] A second cauldron (the start seed eaten late) went.", this);
                Destroy(other.gameObject);
            }
        }

        // ---------------------------------------------------------------- where things go

        // The middle of the PlayGround's field (on its floor), or Cauldron Spot before the field is measured.
        private Vector3 Middle()
        {
            Vector3 at;
            if (TryGetComponent(out PlayGroundField field) && field.Field.width > 2f && field.Field.height > 2f)
                at = new Vector3(field.Field.center.x, transform.position.y, field.Field.center.y);
            else at = transform.TransformPoint(cauldronSpot);
            return Floor(at);
        }

        private Vector3 ChestSpot()
        {
            if (run == null || run.Now == GateRun.Step.StartChest || run.Now == GateRun.Step.Arriving) return Middle();
            var cam = Camera.main;
            if (cam == null) return Middle();
            var fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            var at = cam.transform.position + fwd.normalized * chestAhead;
            // Never on (or in) the pot: pushed out to Chest From Pot.
            if (defence != null)
            {
                var away = at - defence.transform.position;
                away.y = 0f;
                if (away.magnitude < chestFromPot)
                    at = defence.transform.position + (away.sqrMagnitude > 0.0001f ? away.normalized : -fwd.normalized) * chestFromPot;
            }
            return Floor(at);
        }

        private static Vector3 Floor(Vector3 at)
        {
            if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point;
            return at;
        }

        // ---------------------------------------------------------------- the HUD line's goal

        private void Update()
        {
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 0.5f;
                KeepOneCauldron();
            }
            if (run == null || defence == null || Time.time < nextGoal) return;
            nextGoal = Time.time + 0.2f;
            if (run.Now != GateRun.Step.Round) return;
            var pot = defence.TheCauldron;
            string goal;
            int need = 0, done = 0;
            if (defence.Doomed) goal = potBroken;
            else switch (defence.Step)
            {
                case CauldronDefence.RoundStep.Gathering:
                    if (pot.WaitingForFire) goal = lightFire;
                    else if (!pot.RecipeKnown) goal = findHat;
                    else
                    {
                        goal = fillPot;
                        need = pot.RecipeTotal;
                        done = pot.InPot;
                    }
                    break;
                case CauldronDefence.RoundStep.Cooking:
                    goal = defend;
                    break;
                case CauldronDefence.RoundStep.Bosses:
                    goal = beatBosses;
                    need = defence.RoundBossesTotal;
                    done = defence.RoundBossesBeaten;
                    break;
                default:
                    goal = "";
                    break;
            }
            if (goal != shownGoal || need != shownNeed)
            {
                run.Goal(goal, need);
                shownGoal = goal;
                shownNeed = need;
                shownDone = -1;
            }
            if (done != shownDone)
            {
                run.Progress(done);
                shownDone = done;
            }
        }
    }
}
