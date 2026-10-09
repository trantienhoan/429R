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
    /// Rewires the wave managers (-EnemyManager 0, 1 and 2 under ---GameManager---) to spawn the smarter spiders and
    /// cockroaches in controlled waves (Tools > 429 Game > Enemies). Each manager's EnemyManager FSM gets three numbers
    /// in its Inspector: waveSize (enemies in the wave), maxAlive (alive at once) and bossWhenLeft (the boss comes when
    /// this many or fewer are left). Flow: Init (Start Wave) -> Wait for Slot (Wait For Wave Room) -> Loop Spawns (next
    /// spawn point, round and round) -> Spawn Enemies (Create Object, Add Enemy To Wave, count; Wave Done once
    /// waveSize are out) -> Wait Cleared (Wait For Wave Cleared) -> wait -> its boss state (COMPLETE, or Call Boss in
    /// manager 2, whose Create Objects get the bosses in order) -> its end.
    /// Running it again keeps numbers already there; only new ones get the defaults below.
    /// </summary>
    public static class SpiderWaveSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Enemies_Smarter/";

        private static readonly (string manager, string regular, string bossState, string[] bosses, int size, int alive, int left)[] Managers =
        {
            ("-EnemyManager 0", "Spider_Small", "COMPLETE", new[] { "Spider_Boss" }, 6, 3, 1),
            ("-EnemyManager 1", "Spider_Normal", "COMPLETE", new[] { "Spider_Boss_PumpkinButt" }, 12, 5, 2),
            ("-EnemyManager 2", "Cockroach_Normal", "Call Boss", new[] { "Cockroach_Boss", "Cockroach_Boss_PumpkinButt" }, 10, 4, 2),
        };

        [MenuItem("Tools/429 Game/Enemies/Set Up Spider Waves")]
        public static void SetUpSpiderWaves()
        {
            var scene = SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "---GameManager---");
            if (root == null)
            {
                Debug.LogWarning("[Spider Waves] No ---GameManager--- in the open scene.");
                return;
            }

            foreach (var m in Managers)
            {
                var managerObject = root.transform.Find(m.manager);
                var fsm = managerObject != null ? managerObject.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "EnemyManager") : null;
                if (fsm == null)
                {
                    Debug.LogWarning($"[Spider Waves] {m.manager} with an EnemyManager FSM not found.");
                    continue;
                }
                Undo.RecordObject(fsm, "Set Up Spider Waves");
                Rewire(fsm, Load(m.regular), m.bossState, m.bosses.Select(Load).ToArray(), m.size, m.alive, m.left);
                EditorUtility.SetDirty(fsm);
                var v = fsm.FsmVariables;
                Debug.Log($"[Spider Waves] {m.manager}: {IntValue(v, "waveSize")} x {m.regular}, at most " +
                          $"{IntValue(v, "maxAlive")} alive, then {string.Join(" + ", m.bosses)} when " +
                          $"{IntValue(v, "bossWhenLeft")} or fewer are left.", fsm);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GameObject Load(string prefab)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + prefab + ".prefab");
            if (go == null) Debug.LogWarning($"[Spider Waves] {Folder}{prefab}.prefab not found.");
            return go;
        }

        private static void Rewire(PlayMakerFSM fsm, GameObject regular, string bossState, GameObject[] bosses, int size, int alive, int left)
        {
            var f = fsm.Fsm;
            // FSMs on inactive objects aren't set up in the editor until something selects them.
            if (!f.Initialized) f.Init(fsm);
            var vars = fsm.FsmVariables;
            foreach (var s in f.States) s.LoadActions();

            var waveSize = Int(vars, "waveSize", size, "How many regular enemies this wave spawns in total (before the boss).");
            var maxAlive = Int(vars, "maxAlive", alive, "Most enemies of this wave alive at the same time.");
            var bossWhenLeft = Int(vars, "bossWhenLeft", left, "The boss comes when this many or fewer of the wave are still alive (0 = all dead).");
            var count = vars.FindFsmInt("numberOfEnemies");
            var spawnPoint = vars.FindFsmVector3("spawn_v3");
            var added = vars.FindFsmGameObject("EnemyAdded");
            if (added == null)
            {
                added = new FsmGameObject("EnemyAdded");
                vars.GameObjectVariables = vars.GameObjectVariables.Append(added).ToArray();
            }
            // By name: an FSM loaded from the scene holds its own copy of the event. One of each, no duplicates.
            var waveDone = FsmEvent.GetFsmEvent("Wave Done");
            f.Events = f.Events.Where(e => e != null && e.Name != waveDone.Name).Append(waveDone).ToArray();

            // Init: a new wave, counter back to 0
            var init = State(f, "Init");
            var initActions = init.Actions.Where(a => !(a is StartWave) && !(a is SetIntValue)).ToList();
            int waitAt = Mathf.Max(0, initActions.FindIndex(a => a is Wait));
            var setCount = New<SetIntValue>();
            setCount.intVariable = count;
            setCount.intValue = 0;
            initActions.InsertRange(waitAt, new FsmStateAction[] { New<StartWave>(), setCount });
            init.Actions = initActions.ToArray();
            init.Transitions = new[] { Go("FINISHED", "Wait for Slot") };

            // Wait for Slot: room in this wave and in the Enemy Limit
            var room = New<WaitForWaveRoom>();
            room.maxAlive = maxAlive;
            room.useEnemyLimit = true;
            var waitForSlot = State(f, "Wait for Slot");
            waitForSlot.Actions = new FsmStateAction[] { room };
            waitForSlot.Transitions = new[] { Go("FINISHED", "Loop Spawns") };

            // Loop Spawns: the next spawn point, starting over after the last one
            State(f, "Loop Spawns").Transitions = new[] { Go("LoopEvent", "Spawn Enemies"), Go("next", "Loop Spawns") };

            // Spawn Enemies
            var spawn = State(f, "Spawn Enemies");
            var actions = spawn.Actions.Where(a => !(a is AddEnemyToWave) && !(a is IntCompare)).ToList();
            var offset = actions.OfType<Vector3AddXYZ>().First();
            offset.vector3Variable = spawnPoint;
            offset.addX = vars.FindFsmFloat("randomX");
            offset.addY = 0f;
            offset.addZ = vars.FindFsmFloat("randomZ");
            var sample = actions.FirstOrDefault(a => a.GetType().Name == "NavMeshSamplePosition");
            if (sample != null)
            {
                var t = sample.GetType();
                t.GetField("sourcePosition")?.SetValue(sample, spawnPoint);
                t.GetField("position")?.SetValue(sample, spawnPoint);
                t.GetField("allowedMask")?.SetValue(sample, new FsmInt { Value = -1 });
                t.GetField("maxDistance")?.SetValue(sample, new FsmFloat { Value = 10f });
            }
            var create = actions.OfType<CreateObject>().First();
            create.gameObject = new FsmGameObject { Value = regular };
            create.storeObject = added;
            var addToWave = New<AddEnemyToWave>();
            addToWave.enemy = added;
            actions.Insert(actions.IndexOf(create) + 1, addToWave);
            var countUp = actions.OfType<IntAdd>().First();
            countUp.intVariable = count;
            countUp.add = 1;
            var done = New<IntCompare>();
            done.integer1 = count;
            done.integer2 = waveSize;
            done.equal = waveDone;
            done.greaterThan = waveDone;
            done.everyFrame = false;
            var pause = actions.OfType<RandomWait>().First();
            actions.Remove(pause);
            actions.Add(done);      // leaves at once when the wave is all out...
            actions.Add(pause);     // ...otherwise waits a moment before the next one
            spawn.Actions = actions.ToArray();
            spawn.Transitions = new[] { Go("FINISHED", "Wait for Slot"), new FsmTransition { FsmEvent = waveDone, ToState = "Wait Cleared" } };

            // Wait Cleared: the boss comes when the wave is (nearly) beaten
            var cleared = f.States.FirstOrDefault(s => s.Name == "Wait Cleared");
            if (cleared == null)
            {
                var near = State(f, "wait").Position;
                cleared = new FsmState(f) { Name = "Wait Cleared", Position = new Rect(near.x, near.y - 60f, near.width, near.height) };
                f.States = f.States.Append(cleared).ToArray();
            }
            var wait = New<WaitForWaveCleared>();
            wait.leftAlive = bossWhenLeft;
            wait.giveUpAfter = 90f;
            cleared.Actions = new FsmStateAction[] { wait };
            cleared.Transitions = new[] { Go("FINISHED", "wait") };

            // The boss state: its Create Objects get the bosses, in order
            var bossCreates = State(f, bossState).Actions.OfType<CreateObject>().ToList();
            for (int i = 0; i < bossCreates.Count && i < bosses.Length; i++)
            {
                if (bosses[i] != null) bossCreates[i].gameObject = new FsmGameObject { Value = bosses[i] };
            }

            // The old Loop state (spawn again without waiting for room) isn't used any more.
            f.States = f.States.Where(s => s.Name != "Loop").ToArray();

            foreach (var s in f.States) s.SaveActions();
        }

        private static string IntValue(FsmVariables vars, string name) =>
            vars.IntVariables.FirstOrDefault(i => i.Name == name)?.Value.ToString() ?? "?";

        private static FsmInt Int(FsmVariables vars, string name, int value, string tooltip)
        {
            // Looked up in the list itself: FindFsmInt can miss a variable added a moment ago.
            var v = vars.IntVariables.FirstOrDefault(i => i.Name == name);
            if (v == null)
            {
                v = new FsmInt(name) { Value = value };      // a number already there (maybe tuned) is kept
                vars.IntVariables = vars.IntVariables.Append(v).ToArray();
            }
            v.Tooltip = tooltip;
            v.ShowInInspector = true;
            return v;
        }

        private static T New<T>() where T : FsmStateAction, new()
        {
            var action = new T();
            action.Reset();
            return action;
        }

        private static FsmState State(Fsm f, string name) => f.States.First(s => s.Name == name);

        private static FsmTransition Go(string eventName, string toState) =>
            new FsmTransition { FsmEvent = FsmEvent.GetFsmEvent(eventName), ToState = toState };
    }
}
