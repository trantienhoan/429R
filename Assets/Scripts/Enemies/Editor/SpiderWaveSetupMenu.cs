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
    /// Rewires the wave managers (-EnemyManager 0 to 3 under ---GameManager---) to spawn the smarter spiders and
    /// cockroaches in controlled waves (Tools > 429 Game > Enemies). Each manager's EnemyManager FSM gets three numbers
    /// in its Inspector: waveSize (enemies in the wave), maxAlive (alive at once) and bossWhenLeft (the boss comes when
    /// this many or fewer are left). Flow: Init (Start Wave) -> Wait for Slot (Wait For Wave Room) -> Loop Spawns (next
    /// spawn point, round and round) -> Spawn Enemies (Create Object, Add Enemy To Wave, count; Wave Done once
    /// waveSize are out) -> Wait Cleared (Wait For Wave Cleared) -> wait -> COMPLETE -> its boss state (COMPLETE itself,
    /// or Call Boss in managers 2 and 3) -> its end. The boss state's Create Objects get the bosses in order (extra
    /// enemy ones are removed) and it waits for the bosses' bools (each boss sets its own in Spider Brain > Set On
    /// Death). The boss comes right after the wave: anything else COMPLETE waited for (manager 3's old Pumpkin_61 /
    /// Pumpkin_62 gate) is removed. spawnWait (seconds between spawns) is set where the table gives one.
    /// Running it again keeps numbers already there; only new ones get the defaults below.
    /// </summary>
    public static class SpiderWaveSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Enemies_Smarter/";

        private static readonly (string manager, string regular, string bossState, string[] bosses, string[] bossBools, int size, int alive, int left, Vector2 spawnWait)[] Managers =
        {
            ("-EnemyManager 0", "Spider_Small", "COMPLETE", new[] { "Spider_Boss" }, new[] { "WhiteSpiderDown" }, 6, 3, 1, Vector2.zero),
            ("-EnemyManager 1", "Spider_Normal", "COMPLETE", new[] { "Spider_Boss_PumpkinButt" }, new[] { "BigSpiderDown" }, 12, 5, 2, Vector2.zero),
            ("-EnemyManager 2", "Cockroach_Small", "Call Boss", new[] { "Cockroach_Boss" }, new[] { "BigRoachDown" }, 10, 4, 2, Vector2.zero),
            ("-EnemyManager 3", "Cockroach_Normal", "Call Boss", new[] { "Cockroach_Boss_PumpkinButt" }, new[] { "BigRoach1Down" }, 12, 5, 2, new Vector2(1f, 3f)),
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
                Rewire(fsm, Load(m.regular), m.bossState, m.bosses.Select(Load).ToArray(), m.bossBools, m.size, m.alive, m.left, m.spawnWait);
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

        private static void Rewire(PlayMakerFSM fsm, GameObject regular, string bossState, GameObject[] bosses, string[] bossBools, int size, int alive, int left, Vector2 spawnWait)
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
            // The bosses' "I'm down" bools (Spider Brain > Set On Death sets them in this FSM).
            var downs = bossBools.Select(b => Bool(vars, b)).ToArray();

            // Init: a new wave, counter back to 0, the bosses' bools off
            var init = State(f, "Init");
            var initActions = init.Actions.Where(a => !(a is StartWave) && !(a is SetIntValue)).ToList();
            int waitAt = Mathf.Max(0, initActions.FindIndex(a => a is Wait));
            var setCount = New<SetIntValue>();
            setCount.intVariable = count;
            setCount.intValue = 0;
            var resets = new List<FsmStateAction> { New<StartWave>(), setCount };
            foreach (var down in downs.Where(d => !initActions.Any(a => Resets(a, d.Name))))
            {
                var reset = New<SetBoolValue>();
                reset.boolVariable = down;
                reset.boolValue = false;
                resets.Add(reset);
            }
            initActions.InsertRange(waitAt, resets);
            init.Actions = initActions.ToArray();
            init.Transitions = new[] { Go("FINISHED", "Wait for Slot") };

            // Wait for Slot: room in this wave and in the Enemy Limit
            var room = New<WaitForWaveRoom>();
            room.maxAlive = maxAlive;
            room.useEnemyLimit = true;
            var waitForSlot = f.States.FirstOrDefault(s => s.Name == "Wait for Slot");
            if (waitForSlot == null)
            {
                var near = State(f, "Loop Spawns").Position;
                waitForSlot = new FsmState(f) { Name = "Wait for Slot", Position = new Rect(near.x, near.y - 60f, near.width, near.height) };
                f.States = f.States.Append(waitForSlot).ToArray();
            }
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
            if (spawnWait != Vector2.zero)
            {
                pause.min = spawnWait.x;
                pause.max = spawnWait.y;
            }
            actions.Remove(pause);
            actions.Add(done);      // leaves at once when the wave is all out...
            actions.Add(pause);     // ...otherwise waits a moment before the next one
            spawn.Actions = actions.ToArray();
            spawn.Transitions = new[] { Go("FINISHED", "Wait for Slot"), new FsmTransition { FsmEvent = waveDone, ToState = "Wait Cleared" } };

            // wait: a short pause before COMPLETE (manager 3 had none: its COMPLETE came straight after the last spawn)
            if (f.States.All(s => s.Name != "wait"))
            {
                var complete = State(f, "COMPLETE").Position;
                var breather = New<RandomWait>();
                breather.min = 2f;
                breather.max = 4f;
                breather.realTime = true;
                var waitState = new FsmState(f) { Name = "wait", Position = new Rect(complete.x - 200f, complete.y, complete.width, complete.height) };
                waitState.Actions = new FsmStateAction[] { breather };
                waitState.Transitions = new[] { Go("FINISHED", "COMPLETE") };
                f.States = f.States.Append(waitState).ToArray();
            }

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

            // The boss state: its Create Objects get the bosses, in order; extra enemies go. It waits for their bools.
            var boss = State(f, bossState);
            var bossActions = boss.Actions.ToList();
            var bossCreates = bossActions.OfType<CreateObject>().ToList();
            for (int i = 0; i < bossCreates.Count; i++)
            {
                if (i < bosses.Length)
                {
                    if (bosses[i] != null) bossCreates[i].gameObject = new FsmGameObject { Value = bosses[i] };
                }
                else if (IsEnemy(bossCreates[i].gameObject.Value))
                {
                    bossActions.Remove(bossCreates[i]);
                }
            }
            foreach (var test in bossActions.OfType<BoolTest>()) test.boolVariable = downs[0];
            foreach (var test in bossActions.OfType<BoolAllTrue>()) test.boolVariables = downs.ToArray();
            boss.Actions = bossActions.ToArray();

            // COMPLETE (when it isn't the boss state itself) waits for nothing: the boss comes right after the wave.
            var completeState = State(f, "COMPLETE");
            if (completeState != boss) completeState.Actions = completeState.Actions.Where(a => !(a is BoolTest) && !(a is BoolAllTrue) && !(a is BoolAnyTrue)).ToArray();

            // The old Loop state (spawn again without waiting for room) isn't used any more, and nothing else goes back
            // to spawning (manager 3's COMPLETE sent more bees every 3 s while it waited): only the wave spawns.
            f.States = f.States.Where(s => s.Name != "Loop").ToArray();
            foreach (var s in f.States.Where(s => s.Name != "Wait for Slot" && s.Name != "Loop Spawns"))
            {
                var kept = s.Transitions.Where(t => t.ToState != "Loop" && t.ToState != "Loop Spawns").ToArray();
                if (kept.Length == s.Transitions.Length) continue;
                s.Transitions = kept;
                s.Actions = s.Actions.Where(a => !(a is Wait w) || w.finishEvent == null || w.finishEvent.Name == "FINISHED" ||
                                                 kept.Any(t => t.EventName == w.finishEvent.Name)).ToArray();
            }

            foreach (var s in f.States) s.SaveActions();
        }

        private static bool IsEnemy(GameObject prefab) =>
            prefab == null || AssetDatabase.GetAssetPath(prefab).StartsWith("Assets/Prefabs/Enemies/");

        // Already switched off in Init, by Set Fsm Variable / Set Fsm Bool on this FSM or Set Bool Value.
        private static bool Resets(FsmStateAction action, string boolName) => action switch
        {
            SetFsmVariable set => set.variableName != null && set.variableName.Value == boolName,
            SetFsmBool set => set.variableName != null && set.variableName.Value == boolName,
            SetBoolValue set => set.boolVariable != null && set.boolVariable.Name == boolName,
            _ => false,
        };

        private static FsmBool Bool(FsmVariables vars, string name)
        {
            var v = vars.BoolVariables.FirstOrDefault(b => b.Name == name);
            if (v != null) return v;
            v = new FsmBool(name);
            vars.BoolVariables = vars.BoolVariables.Append(v).ToArray();
            return v;
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
