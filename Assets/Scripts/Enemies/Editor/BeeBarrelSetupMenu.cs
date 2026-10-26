using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// The rooftop whack stages end with bees (Tools > 429 Game > Enemies > Set Up Bee Barrels). When the whack pumpkin's
    /// health reaches 0, its GAME-Pumpkin FSM's Deactivate state breaks every barrel (Send Event Dead, one after another);
    /// now a bee comes out of each one as it breaks (Spawn From Barrels): Bee_Small and one Bee_Boss in Whack 6, Bee_Normal
    /// and one Bee_Boss_PumpkinButt in Whack 7. The stage no longer goes on right away: whack_6 / whack_7 are set by the
    /// boss bee when it dies (Spider Brain > Set On Death), so the Set Fsm Variable that set them here is switched off.
    /// The bee each whack hit lets out in Whack 7 is the new Bee_Small too.
    /// Running it again only updates what's already there.
    /// </summary>
    public static class BeeBarrelSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string OldBeeSmallPath = "Assets/Prefabs/Enemies/Bee_Small.prefab";

        private static readonly (string holder, string fsm, string regular, string boss, string stageBool)[] Stages =
        {
            ("-Wood_Barrels_Whack_Pumpkin_6-", "GAME-Pumpkin6", "Bee_Small", "Bee_Boss", "whack_6"),
            ("-Wood_Barrels_Whack_Pumpkin_7-", "GAME-Pumpkin7", "Bee_Normal", "Bee_Boss_PumpkinButt", "whack_7"),
        };

        private const string SmokePath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        private const string BeeBuzzPath = "Assets/Audio/SFX/bee_fly.wav";

        /// <summary>
        /// Whack 7's boss bee (Bee_Boss_PumpkinButt) no longer comes out with the swarm: its Spawn From Barrels gets Boss
        /// Last, so the boss comes once 2 or fewer Bee_Normal are alive (at least 3 s, at most 15 s after the last one came
        /// out), after a 1.5 s warning at its barrel: a Smoke_Blast puff, the bee buzz (bee_fly) and ShakeMed. Whack 6 is
        /// unchanged. Safe to run again.
        /// </summary>
        [MenuItem("Tools/429 Game/Enemies/Whack 7 Boss Waits For Swarm")]
        public static void BossWaitsForSwarm()
        {
            var scene = SceneManager.GetActiveScene();
            var holder = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "-Wood_Barrels_Whack_Pumpkin_7-");
            var fsm = holder != null ? holder.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "GAME-Pumpkin7") : null;
            if (fsm == null)
            {
                Debug.LogWarning("[Bee Barrels] -Wood_Barrels_Whack_Pumpkin_7- / GAME-Pumpkin7 not found in the open scene.");
                return;
            }
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            var state = fsm.Fsm.GetState("Deactivate");
            state?.LoadActions();
            var spawn = state?.Actions.OfType<SpawnFromBarrels>().FirstOrDefault();
            // Saving writes back only the actions that could be read: if one couldn't, leave the state alone.
            if (spawn == null || state.ActionData.ActionCount != state.Actions.Length)
            {
                Debug.LogWarning("[Bee Barrels] GAME-Pumpkin7's Deactivate has no Spawn From Barrels (run Set Up Bee Barrels), or an " +
                                 "action in it couldn't be read; nothing changed.", fsm);
                return;
            }
            Undo.RecordObject(fsm, "Whack 7 Boss Waits For Swarm");
            spawn.bossLast = true;
            spawn.bossWhenLeft = 2;
            spawn.bossMinWait = 3f;
            spawn.bossMaxWait = 15f;
            spawn.bossWarning = 1.5f;
            spawn.warningEffect = new FsmGameObject { Value = AssetDatabase.LoadAssetAtPath<GameObject>(SmokePath) };
            spawn.warningSound = new FsmObject { ObjectType = typeof(AudioClip), Value = AssetDatabase.LoadAssetAtPath<AudioClip>(BeeBuzzPath) };
            spawn.warningShake = "ShakeMed";
            state.SaveActions();
            EditorUtility.SetDirty(fsm);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Bee Barrels] GAME-Pumpkin7: the boss bee waits until 2 or fewer bees are left (3-15 s after the last), " +
                      $"then a 1.5 s warning (smoke {(spawn.warningEffect.Value != null ? "ok" : "MISSING")}, buzz " +
                      $"{(spawn.warningSound.Value != null ? "ok" : "MISSING")}, ShakeMed) and it comes out of its barrel. Save the scene.", fsm);
        }

        [MenuItem("Tools/429 Game/Enemies/Set Up Bee Barrels")]
        public static void SetUp()
        {
            var scene = SceneManager.GetActiveScene();
            var holders = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            foreach (var stage in Stages)
            {
                var holder = holders.FirstOrDefault(t => t.name == stage.holder);
                var fsm = holder != null ? holder.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == stage.fsm) : null;
                var regular = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + stage.regular + ".prefab");
                var boss = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + stage.boss + ".prefab");
                if (fsm == null || regular == null || boss == null)
                {
                    Debug.LogWarning($"[Bee Barrels] {stage.holder} / {stage.fsm}, {stage.regular} or {stage.boss} not found; skipped.");
                    continue;
                }
                Undo.RecordObject(fsm, "Set Up Bee Barrels");
                Debug.Log($"[Bee Barrels] {stage.fsm}: " + Rewire(fsm, regular, boss, stage.stageBool), fsm);
                EditorUtility.SetDirty(fsm);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static string Rewire(PlayMakerFSM fsm, GameObject regular, GameObject boss, string stageBool)
        {
            var f = fsm.Fsm;
            if (!f.Initialized) f.Init(fsm);
            foreach (var s in f.States) s.LoadActions();
            var end = f.States.First(s => s.Name == "Deactivate");
            var actions = end.Actions.ToList();

            // The barrels it breaks, in order, and when.
            var breaks = actions.OfType<SendEvent>()
                .Where(a => a.Enabled && a.sendEvent != null && a.sendEvent.Name == "Dead" && a.eventTarget?.gameObject?.GameObject != null &&
                            a.eventTarget.gameObject.GameObject.Name.StartsWith("Barrel_"))
                .ToList();
            var delays = breaks.Select(a => a.delay != null ? a.delay.Value : 0f).ToList();

            var spawn = actions.OfType<SpawnFromBarrels>().FirstOrDefault();
            if (spawn == null)
            {
                spawn = new SpawnFromBarrels();
                spawn.Reset();
                actions.Insert(breaks.Count > 0 ? actions.IndexOf(breaks.Last()) + 1 : actions.Count, spawn);
            }
            spawn.barrels = breaks.Select(a => a.eventTarget.gameObject.GameObject).ToArray();
            spawn.regular = new FsmGameObject { Value = regular };
            spawn.boss = new FsmGameObject { Value = boss };
            // Under the same parent as the stage's other spawns (CurrentlySpawnedStuffs).
            var parent = f.States.SelectMany(s => s.Actions).OfType<CreateObject>().Select(c => c.parent).FirstOrDefault(p => p != null && p.UseVariable);
            if (parent != null) spawn.parent = parent;
            spawn.firstDelay = delays.Count > 0 ? delays.Min() : 0.1f;
            spawn.delayBetween = delays.Count > 1 ? (delays.Max() - delays.Min()) / (delays.Count - 1) : 0.1f;

            // The boss bee sets the stage bool now.
            int switchedOff = 0;
            foreach (var set in actions.OfType<SetFsmVariable>().Where(a => a.Enabled && a.variableName != null && a.variableName.Value == stageBool))
            {
                set.Enabled = false;
                switchedOff++;
            }
            end.Actions = actions.ToArray();

            // The bee each whack hit lets out (GAME-Pumpkin7's Get_hit): the new Bee_Small instead of the old one.
            var newSmall = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Bee_Small.prefab");
            int swapped = 0;
            foreach (var create in f.States.SelectMany(s => s.Actions).OfType<CreateObject>())
            {
                if (newSmall == null || create.gameObject == null || create.gameObject.Value == null ||
                    AssetDatabase.GetAssetPath(create.gameObject.Value) != OldBeeSmallPath) continue;
                create.gameObject = new FsmGameObject { Value = newSmall };
                swapped++;
            }
            foreach (var s in f.States) s.SaveActions();
            return $"{breaks.Count} barrels -> {regular.name} out of each but one, {boss.name} out of the last " +
                   $"(from {spawn.firstDelay.Value:0.##} s, every {spawn.delayBetween.Value:0.##} s)" +
                   (switchedOff > 0 ? $"; its Set Fsm Variable {stageBool} switched off ({boss.name} sets it when it dies)" : "") +
                   (swapped > 0 ? $"; {swapped} Create Object(s) of the old Bee_Small now make the new one" : "");
        }
    }
}
