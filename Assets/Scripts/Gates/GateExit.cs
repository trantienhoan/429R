using System.Collections;
using Game.Locomotion;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Game.Gates
{
    /// <summary>
    /// How every gate stage ends (user, 2026-11-18: "break the pumpkin boss here to drop the seed to get out of here and
    /// move to defend tree stage like the main game"; gates 1 and 2 learn from gate 3). Started by the stage when its
    /// giant pumpkin (Pumpkin Ball Boss) breaks, on the stage's own root:
    /// - the stage stands alone (out of Currently Spawned Stuffs, so GAMESTAGES' Clear Spawned Stuffs doesn't take it from
    ///   under the player), the gate's chest seeds still lying about are eaten for the player and any gear bool still
    ///   false is set (the gate paths wait for all four and The_Seed);
    /// - The_Seed shows where the boss broke; eating it (or Seed Seconds passing) sets The_Seed, and GAMESTAGES goes on:
    ///   the stage's things are cleared and the rooftop comes up (Rooftop_ON PG, then Whack 6 and 7: the tree defence,
    ///   then the Lid and the boss, as in the main game);
    /// - in the black until the new rooftop is there, the player is put Tree Stand Off metres from its Pumpkin Tree
    ///   (towards the south gate), facing it; the view comes back and the stage goes.
    /// A test scene without GAMESTAGES: The_Seed is shown, nothing more.
    /// </summary>
    [DisallowMultipleComponent]
    public class GateExit : MonoBehaviour
    {
        /// <summary>The 4 chest gear bools the gate paths wait for (with The_Seed).</summary>
        public static readonly string[] GearBools = { "Bracer_Gear", "Chest_Gear", "Head_Gear", "Shield_Gear" };

        private const string SeedBool = "The_Seed";
        private const string SeedWaitState = "Rotate and Wait";
        private const string SeedEatenEvent = "next";
        private const string TreeTag = "PumpkinTree";

        [Tooltip("The seed not eaten after this many seconds: eaten for the player.")]
        [Min(1f)]
        public float seedSeconds = 20f;
        [Tooltip("Waiting in the black longer than this for the rooftop: the view comes back (and a warning).")]
        [Min(1f)]
        public float roofWaitSeconds = 20f;
        [Tooltip("Metres from the rooftop's Pumpkin Tree the player is put, facing it.")]
        [Min(1f)]
        public float treeStandOff = 3.5f;

        private GameObject seedPrefab;
        private Vector3 seedAt;
        private string log = "[Gate]";
        private XROrigin origin;
        private Transform head;
        private HutongGames.PlayMaker.FsmFloat playerHealth;

        /// <summary>Whether a gate exit is under way (the seed is out, or the player is being taken to the rooftop).</summary>
        public static bool Running { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Running = false;

        /// <summary>
        /// The end of the gate stage <paramref name="stage"/> (its root): The_Seed (<paramref name="seed"/>) shows at
        /// <paramref name="seedSpot"/> (a floor point; it floats 1 m over it), then the rooftop. <paramref name="logTag"/>
        /// prefixes the console lines, e.g. "[BallValley]".
        /// </summary>
        public static GateExit Begin(GameObject stage, GameObject seed, Vector3 seedSpot, string logTag)
        {
            if (stage == null) return null;
            var exit = stage.GetComponent<GateExit>();
            if (exit != null) return exit;
            exit = stage.AddComponent<GateExit>();
            exit.seedPrefab = seed;
            exit.seedAt = seedSpot;
            if (!string.IsNullOrEmpty(logTag)) exit.log = logTag;
            return exit;
        }

        private void Start()
        {
            Running = true;
            origin = FindAnyObjectByType<XROrigin>();
            var cam = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            head = cam != null ? cam.transform : null;
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            Hold(false);
            Running = false;
        }

        private IEnumerator Run()
        {
            // Before anything can set The_Seed: GAMESTAGES' Clear Spawned Stuffs must not take the stage.
            transform.SetParent(null, true);
            GearGuard();
            var oldRoof = Global("Rooftop_Base");
            var seed = seedPrefab != null ? Instantiate(seedPrefab, seedAt + Vector3.up, Quaternion.identity, transform) : null;
            if (GameStages.Fsm() == null)
            {
                Debug.LogWarning($"{log} No GAMESTAGES: the seed is out, the stage stays (no rooftop).", this);
                Running = false;
                yield break;
            }

            float autoAt = Time.time + seedSeconds;
            bool given = false;
            while (!GameStages.GetBool(SeedBool))
            {
                if (GameOver()) yield break;
                if (seed == null && !given)
                {
                    // No seed (none set, or gone): the bool is set for them.
                    if (seedPrefab == null) Debug.LogWarning($"{log} No seed prefab: GAMESTAGES The_Seed set directly.", this);
                    GameStages.SetBool(SeedBool, true, this);
                    given = true;
                }
                else if (seed != null && !given && Time.time >= autoAt && !Paused() && !PlayerDown())
                {
                    // Not eaten in time: eaten for them (its Activate Stuffs: the item, the heal, The_Seed).
                    foreach (var seedFsm in seed.GetComponentsInChildren<PlayMakerFSM>())
                    {
                        if (seedFsm.ActiveStateName != SeedWaitState) continue;
                        seedFsm.SendEvent(SeedEatenEvent);
                        given = true;
                        break;
                    }
                }
                yield return null;
            }

            // The seed's sound and shrink, then the black (not over a notice: it would hide it).
            yield return new WaitForSeconds(1f);
            while (Paused()) yield return null;
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(0.8f);
            yield return new WaitForSeconds(0.9f);
            // Held where they stand in the black: GAMESTAGES may sink the stage's floor away (the PlayGround goes down
            // 11 m) before the rooftop comes, and the fall would count as one (Fall Out Of World: damage, a put-back).
            while (PlayerDown()) yield return null;
            Hold(true);

            // In the black until the new rooftop is there.
            GameObject roof;
            float waited = 0f;
            bool warned = false;
            while (true)
            {
                if (GameOver()) yield break;
                bool down = PlayerDown();
                roof = Global("Rooftop_Base");
                if (!down && roof != null && roof != oldRoof) break;
                if (!down && Paused())
                {
                    if (fade != null) fade.FadeIn(0.3f);
                    while (Paused()) yield return null;
                    if (fade != null && !warned) fade.FadeOut(0.5f);
                }
                else if (!down && !warned && (waited += Time.deltaTime) > roofWaitSeconds)
                {
                    Debug.LogWarning($"{log} The rooftop hasn't come {roofWaitSeconds} s after the seed (GAMESTAGES is in '" +
                                     GameStages.ActiveState() + "'): waiting on the stage.", this);
                    if (fade != null) fade.FadeIn(0.8f);
                    warned = true;
                }
                yield return null;
            }
            // A moment for the rooftop to grow and place its tree.
            yield return new WaitForSeconds(0.3f);
            var launch = origin != null ? origin.GetComponent<Game.Combat.PlayerLaunch>() : null;
            for (float until = Time.time + 6f; Time.time < until;)
            {
                if (GameOver()) yield break;
                if (!PlayerDown() && Paused())
                {
                    if (fade != null) fade.FadeIn(0.3f);
                    while (Paused()) yield return null;
                    continue;
                }
                if (PlayerDown() || (launch != null && launch.Flying))
                {
                    yield return null;
                    continue;
                }
                if (fade == null || fade.Alpha >= 0.99f) break;
                fade.FadeOut(0.5f);
                yield return new WaitForSeconds(0.6f);
            }
            roof = Global("Rooftop_Base");
            var (spot, yaw) = TreeSpot(roof);
            Hold(false);
            MovePlayer(spot, yaw);
            Running = false;
            if (fade != null) fade.FadeIn(0.8f);
            Destroy(gameObject, 0.2f);
        }

        /// <summary>
        /// The gate's chest seeds still waiting (under Currently Spawned Stuffs or this stage) are eaten for the player
        /// (item, heal, gear bool), then any gear bool still false is set: the gate paths wait for all 4 and The_Seed.
        /// </summary>
        public void GearGuard()
        {
            var spawned = Global("CurrentlySpawnedStuffs");
            foreach (var root in new[] { spawned != null ? spawned.transform : null, transform })
            {
                if (root == null) continue;
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>())
                {
                    if (fsm == null || fsm.ActiveStateName != SeedWaitState || !IsChestSeed(fsm.transform, root)) continue;
                    Debug.Log($"{log} {fsm.gameObject.name} not eaten: eaten for the player.", this);
                    fsm.SendEvent(SeedEatenEvent);
                }
            }
            if (GameStages.Fsm() == null) return;
            foreach (var gear in GearBools)
            {
                if (GameStages.GetBool(gear)) continue;
                GameStages.SetBool(gear, true, this);
            }
        }

        // The FSM's object, or one of its parents below Root, is a "Seed_" (a chest seed).
        private static bool IsChestSeed(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (t.name.StartsWith("Seed_")) return true;
            return false;
        }

        // Died meanwhile: Game Over has the player; the stage (no longer under Currently Spawned Stuffs) just goes.
        private bool GameOver()
        {
            if (GameStages.ActiveState() != "Game Over") return false;
            Hold(false);
            Running = false;
            Destroy(gameObject);
            return true;
        }

        private bool holding;
        private bool bodyWasOn;
        private bool moverWasOn;

        // The rig's body and its mover off (no walking, no falling) while it waits in the black; back on as they were.
        private void Hold(bool on)
        {
            if (on == holding || origin == null) return;
            holding = on;
            var body = origin.GetComponent<CharacterController>();
            var mover = origin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.XRBodyTransformer>();
            if (on)
            {
                var launch = origin.GetComponent<Game.Combat.PlayerLaunch>();
                if (launch != null) launch.Stop();
                bodyWasOn = body != null && body.enabled;
                moverWasOn = mover != null && mover.enabled;
                if (bodyWasOn) body.enabled = false;
                if (moverWasOn) mover.enabled = false;
                return;
            }
            if (bodyWasOn && body != null) body.enabled = true;
            if (moverWasOn && mover != null) mover.enabled = true;
            Physics.SyncTransforms();
        }

        // Tree Stand Off metres from the rooftop's Pumpkin Tree (or its middle), towards its south gate, on its floor, facing it.
        private (Vector3 spot, float yaw) TreeSpot(GameObject roof)
        {
            var roofAt = roof != null ? roof.transform.position : Vector3.zero;
            var middle = roofAt;
            foreach (var tree in GameObject.FindGameObjectsWithTag(TreeTag))
            {
                if (tree == null || (roof != null && !tree.transform.IsChildOf(roof.transform))) continue;
                middle = tree.transform.position;
                break;
            }
            var back = roof != null ? -roof.transform.forward : Vector3.back;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f) back = Vector3.back;
            var spot = middle + back.normalized * treeStandOff;
            spot.y = roofAt.y + 0.03f;
            if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) &&
                Mathf.Abs(hit.point.y - roofAt.y) < 1.5f)
                spot.y = hit.point.y;
            var look = middle - spot;
            look.y = 0f;
            float yaw = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
            return (spot, yaw);
        }

        // The head over Spot, the feet on its floor, facing Yaw.
        private void MovePlayer(Vector3 spot, float yaw)
        {
            if (origin == null) return;
            var body = origin.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            var launch = origin.GetComponent<Game.Combat.PlayerLaunch>();
            if (launch != null) launch.Stop();
            origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(HeadYaw(), yaw));
            var feet = HeadFloor();
            var to = origin.transform.position + new Vector3(spot.x - feet.x, 0f, spot.z - feet.z);
            to.y = spot.y;
            origin.transform.position = to;
            Physics.SyncTransforms();
            if (body != null) body.enabled = true;
        }

        private Vector3 HeadFloor()
        {
            float y = origin != null ? origin.transform.position.y : 0f;
            return head != null ? new Vector3(head.position.x, y, head.position.z) : new Vector3(0f, y, 0f);
        }

        private float HeadYaw()
        {
            if (head == null) return 0f;
            var f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            return f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f).eulerAngles.y : 0f;
        }

        /// <summary>The player is down: health out, the lucky coins under way, or getting back up from a revive.</summary>
        public bool PlayerDown()
        {
            if (playerHealth == null && origin != null)
            {
                foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == "PlayerHealth") playerHealth = fsm.FsmVariables.FindFsmFloat("PlayerHealth");
                }
            }
            return Game.Saving.Checkpoints.IsDying ||
                   (Game.Combat.PlayerRevive.Instance != null && Game.Combat.PlayerRevive.Instance.IsReviving) ||
                   (playerHealth != null && playerHealth.Value <= 0f);
        }

        /// <summary>The game is stopped: the pause menu, a level-up or other notice, the lucky coins, lost focus.</summary>
        public static bool Paused() => Time.timeScale <= 0f || Game.Menus.PauseMenu.IsPaused || Game.Menus.PauseMenu.IsHeld;

        /// <summary>A PlayMaker global GameObject's object (null if it's empty or gone).</summary>
        public static GameObject Global(string name)
        {
            var global = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject(name);
            return global != null && global.Value != null ? global.Value : null;
        }
    }
}
