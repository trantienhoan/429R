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
    /// Rebuilds -EnemyManager 4 (the wave on Path 1, the way in through a bought gate, so a player who's been through
    /// the game before) as the hardest wave (Tools > 429 Game > Enemies). Same flow as the other managers (Set Up Spider
    /// Waves): Init (Start Wave) -> Wait for Slot -> Loop Spawns -> Spawn Enemies -> Wait Cleared -> wait -> COMPLETE ->
    /// Call Boss -> Deactivate Self, with waveSize / maxAlive / bossWhenLeft / spawnWaitMin / spawnWaitMax in the FSM's
    /// Inspector. Each spawn is picked at random from a mix (Spider_Normal, Cockroach_Normal, Ghost_Balloon_3; Select
    /// Random Game Object, weights in the action). Then the two boss ghost balloons, each carrying a Big Bee: COMPLETE
    /// brings Ghost_Balloon_3 1 and 4.2 s later Call Boss brings Ghost_Balloon_3 2; Call Boss waits for BigBeeDown and
    /// BigBee1Down, which the balloons set (Ghost Balloon > Inside Down) when their bee is done for, then the reward
    /// chest (The Seed). The balloons are set up here too: hard to pop (Hits To Pop), one bee each, extra damage
    /// when the enemy limit keeps the bee in. Running it again keeps numbers already there.
    /// </summary>
    public static class FinalWaveSetupMenu
    {
        private const string Manager = "-EnemyManager 4";
        private const string Smarter = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string Ghosts = "Assets/Prefabs/Enemies/PLAYGROUND/";

        [MenuItem("Tools/429 Game/Enemies/Set Up Final Wave (EnemyManager 4)")]
        public static void SetUp()
        {
            var scene = SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "---GameManager---");
            var managerObject = root != null ? root.transform.Find(Manager) : null;
            var fsm = managerObject != null ? managerObject.GetComponents<PlayMakerFSM>().FirstOrDefault(x => x.FsmName == "EnemyManager") : null;
            if (fsm == null)
            {
                Debug.LogWarning($"[Final Wave] {Manager} with an EnemyManager FSM not found under ---GameManager---.");
                return;
            }
            var spider = Load(Smarter + "Spider_Normal");
            var roach = Load(Smarter + "Cockroach_Normal");
            var ghost = Load(Ghosts + "Ghost_Balloon_3");
            var bigGhost = Load(Ghosts + "Ghost_Balloon_3 1");
            var fastGhost = Load(Ghosts + "Ghost_Balloon_3 2");
            if (new[] { spider, roach, ghost, bigGhost, fastGhost }.Any(p => p == null)) return;
            SetUpBossBalloon(bigGhost, "BigBeeDown");
            SetUpBossBalloon(fastGhost, "BigBee1Down");

            Undo.RecordObject(fsm, "Set Up Final Wave");
            var f = fsm.Fsm;
            if (!f.Initialized) f.Init(fsm);
            foreach (var s in f.States) s.LoadActions();
            var vars = fsm.FsmVariables;

            var waveSize = Int(vars, "waveSize", 18, "How many enemies this wave spawns in total (before the bosses).");
            var maxAlive = Int(vars, "maxAlive", 6, "Most enemies of this wave alive at the same time.");
            var bossWhenLeft = Int(vars, "bossWhenLeft", 2, "The bosses come when this many or fewer of the wave are still alive (0 = all dead).");
            var waitMin = Float(vars, "spawnWaitMin", 1f, "Shortest pause between two spawns, in seconds.");
            var waitMax = Float(vars, "spawnWaitMax", 2.5f, "Longest pause between two spawns, in seconds.");
            var count = vars.FindFsmInt("numberOfEnemies");
            var spawnPoint = vars.FindFsmVector3("spawn_v3");
            var added = GameObjectVar(vars, "EnemyAdded");
            var enemyType = GameObjectVar(vars, "enemyType");
            var bee0 = Bool(vars, "BigBeeDown");
            var bee1 = Bool(vars, "BigBee1Down");
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
            var loopSpawns = State(f, "Loop Spawns");
            var waitForSlot = StateOrNew(f, "Wait for Slot", Offset(loopSpawns.Position, 0f, -60f));
            var room = New<WaitForWaveRoom>();
            room.maxAlive = maxAlive;
            room.useEnemyLimit = true;
            waitForSlot.Actions = new FsmStateAction[] { room };
            waitForSlot.Transitions = new[] { Go("FINISHED", "Loop Spawns") };

            // Loop Spawns: the next spawn point, round and round (it used to go to COMPLETE after the last one)
            loopSpawns.Transitions = new[] { Go("LoopEvent", "Spawn Enemies"), Go("next", "Loop Spawns") };

            // Spawn Enemies: a random one of the mix, counted in the wave
            var spawn = State(f, "Spawn Enemies");
            var actions = spawn.Actions.Where(a => !(a is AddEnemyToWave) && !(a is IntCompare) && !(a is SelectRandomGameObject)).ToList();
            foreach (var r in actions.OfType<RandomFloat>())
            {
                r.min = -0.3f;
                r.max = 0.3f;
            }
            var sample = actions.FirstOrDefault(a => a.GetType().Name == "NavMeshSamplePosition");
            if (sample != null) sample.GetType().GetField("maxDistance")?.SetValue(sample, new FsmFloat { Value = 10f });
            var create = actions.OfType<CreateObject>().First();
            var pick = New<SelectRandomGameObject>();
            pick.gameObjects = new[] { new FsmGameObject { Value = spider }, new FsmGameObject { Value = roach }, new FsmGameObject { Value = ghost } };
            pick.weights = new[] { new FsmFloat { Value = 3f }, new FsmFloat { Value = 3f }, new FsmFloat { Value = 2f } };
            pick.storeGameObject = enemyType;
            actions.Insert(actions.IndexOf(create), pick);
            create.gameObject = enemyType;
            create.storeObject = added;
            var parent = create.parent;
            actions.Insert(actions.IndexOf(create) + 1, AddToWave(added));
            var countUp = actions.OfType<IntAdd>().First();
            countUp.intVariable = count;
            countUp.add = 1;
            var done = New<IntCompare>();
            done.integer1 = count;
            done.integer2 = waveSize;
            done.equal = waveDone;
            done.greaterThan = waveDone;
            var pause = actions.OfType<RandomWait>().First();
            pause.min = waitMin;
            pause.max = waitMax;
            pause.realTime = true;
            actions.Remove(pause);
            actions.Add(done);      // leaves at once when the wave is all out...
            actions.Add(pause);     // ...otherwise waits a moment before the next one
            spawn.Actions = actions.ToArray();
            spawn.Transitions = new[] { Go("FINISHED", "Wait for Slot"), new FsmTransition { FsmEvent = waveDone, ToState = "Wait Cleared" } };

            // Wait Cleared -> wait -> COMPLETE
            var complete = State(f, "COMPLETE");
            var breather = StateOrNew(f, "wait", Offset(complete.Position, -200f, 0f));
            var rest = New<RandomWait>();
            rest.min = 2f;
            rest.max = 4f;
            rest.realTime = true;
            breather.Actions = new FsmStateAction[] { rest };
            breather.Transitions = new[] { Go("FINISHED", "COMPLETE") };
            var cleared = StateOrNew(f, "Wait Cleared", Offset(breather.Position, 0f, -60f));
            var nearlyCleared = New<WaitForWaveCleared>();
            nearlyCleared.leftAlive = bossWhenLeft;
            nearlyCleared.giveUpAfter = 90f;
            cleared.Actions = new FsmStateAction[] { nearlyCleared };
            cleared.Transitions = new[] { Go("FINISHED", "wait") };

            // COMPLETE: the first boss balloon (Big Bee), then 4.2 s
            var pauseBoss = New<Wait>();
            pauseBoss.time = 4.2f;
            pauseBoss.finishEvent = FsmEvent.GetFsmEvent("FINISHED");
            pauseBoss.realTime = true;
            complete.Actions = new FsmStateAction[] { Spawn(bigGhost, parent, spawnPoint, new FsmGameObject()), pauseBoss };
            complete.Transitions = new[] { Go("FINISHED", "Call Boss") };

            // Call Boss: the second boss balloon, then until both bees are done for (the balloons set the bools)
            var callBoss = State(f, "Call Boss");
            var beesDown = New<BoolAllTrue>();
            beesDown.boolVariables = new[] { bee0, bee1 };
            beesDown.sendEvent = FsmEvent.GetFsmEvent("FINISHED");
            beesDown.everyFrame = true;
            callBoss.Actions = new FsmStateAction[] { Spawn(fastGhost, parent, spawnPoint, new FsmGameObject()), beesDown };
            callBoss.Transitions = new[] { Go("FINISHED", "Deactivate Self") };

            // The old Loop state (two of each in a row, with no room check) goes.
            f.States = f.States.Where(s => s.Name != "Loop").ToArray();
            foreach (var s in f.States) s.Transitions = s.Transitions.Where(t => t.ToState != "Loop").ToArray();

            foreach (var s in f.States) s.SaveActions();
            EditorUtility.SetDirty(fsm);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Final Wave] {Manager}: {waveSize.Value} of Spider_Normal / Cockroach_Normal / Ghost_Balloon_3 (3:3:2), " +
                      $"at most {maxAlive.Value} alive, {waitMin.Value}-{waitMax.Value} s apart; when {bossWhenLeft.Value} or fewer are " +
                      "left: Ghost_Balloon_3 1, then Ghost_Balloon_3 2 (a Big Bee each); the chest with The Seed once both bees " +
                      "are down (BigBeeDown, BigBee1Down). Save the scene.", fsm);
        }

        // A boss balloon: hard to pop, one Big Bee inside, extra damage when the enemy limit keeps the bee in, and its
        // manager's bool set when the bee is done for. Values already tuned by hand (more hits) are kept.
        private static void SetUpBossBalloon(GameObject prefab, string boolName)
        {
            var balloon = prefab.GetComponent<Game.Enemies.GhostBalloon>();
            if (balloon == null) return;
            var so = new SerializedObject(balloon);
            if (so.FindProperty("hitsToPop").intValue < 2) so.FindProperty("hitsToPop").intValue = 10;
            if (so.FindProperty("popSpeed").floatValue <= 0f) so.FindProperty("popSpeed").floatValue = 4f;
            so.FindProperty("minSpiders").intValue = 1;
            so.FindProperty("maxSpiders").intValue = 1;
            so.FindProperty("limitFullEvent").stringValue = "DamageHeavy";
            var flag = so.FindProperty("insideDown");
            flag.FindPropertyRelative("globalObject").stringValue = "EnemyManager4";
            flag.FindPropertyRelative("fsm").stringValue = "EnemyManager";
            flag.FindPropertyRelative("boolName").stringValue = boolName;
            flag.FindPropertyRelative("value").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssetIfDirty(prefab);
        }

        private static FsmBool Bool(FsmVariables vars, string name)
        {
            var v = vars.BoolVariables.FirstOrDefault(b => b.Name == name);
            if (v != null) return v;
            v = new FsmBool(name);
            vars.BoolVariables = vars.BoolVariables.Append(v).ToArray();
            return v;
        }

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
            if (go == null) Debug.LogWarning($"[Final Wave] {path}.prefab not found; nothing changed.");
            return go;
        }

        private static CreateObject Spawn(GameObject prefab, FsmGameObject parent, FsmVector3 at, FsmGameObject store)
        {
            var create = New<CreateObject>();
            create.gameObject = new FsmGameObject { Value = prefab };
            create.parent = parent;
            create.spawnPoint = new FsmGameObject();
            create.position = at;
            create.rotation = new FsmVector3 { UseVariable = true };
            create.storeObject = store;
            return create;
        }

        private static AddEnemyToWave AddToWave(FsmGameObject enemy)
        {
            var add = New<AddEnemyToWave>();
            add.enemy = enemy;
            return add;
        }

        private static FsmState StateOrNew(Fsm f, string name, Rect position)
        {
            var state = f.States.FirstOrDefault(s => s.Name == name);
            if (state != null) return state;
            state = new FsmState(f) { Name = name, Position = position };
            f.States = f.States.Append(state).ToArray();
            return state;
        }

        private static Rect Offset(Rect r, float x, float y) => new(r.x + x, r.y + y, r.width, r.height);

        private static FsmGameObject GameObjectVar(FsmVariables vars, string name)
        {
            var v = vars.GameObjectVariables.FirstOrDefault(g => g.Name == name);
            if (v != null) return v;
            v = new FsmGameObject(name);
            vars.GameObjectVariables = vars.GameObjectVariables.Append(v).ToArray();
            return v;
        }

        private static FsmInt Int(FsmVariables vars, string name, int value, string tooltip)
        {
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

        private static FsmFloat Float(FsmVariables vars, string name, float value, string tooltip)
        {
            var v = vars.FloatVariables.FirstOrDefault(i => i.Name == name);
            if (v == null)
            {
                v = new FsmFloat(name) { Value = value };
                vars.FloatVariables = vars.FloatVariables.Append(v).ToArray();
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
