using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Whack Pumpkin 6 Drops A Chest. Whack Pumpkin 6 ends like Whack Pumpkin 7:
    /// - Bee_Boss, when it dies, sets BeeBoss on Whack Pumpkin 6's GAME-Pumpkin6 FSM (it used to set GAMESTAGES' whack_6
    ///   straight away). Bee_Boss_PumpkinButt, a variant of it, keeps setting whack_7 and its own BeeBoss;
    /// - GAME-Pumpkin6 (as the user laid it out: Wait Bool for BeeBoss, then 'Deactivate Self' drops a
    ///   Treasure_Chest_and_Seed_CULUA) gets the rest: BeeBoss starts off each time it dies, and it waits until the chest
    ///   is opened (it's gone), then 'Chest Opened' sets whack_6, so GAMESTAGES goes on to Whack 7.
    /// Needs 2 Game Scene open. Running it again changes nothing. Saves the two bee prefabs, not the scene.
    /// </summary>
    public static class WhackChestSetupMenu
    {
        private const string BossPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Bee_Boss.prefab";
        private const string ButtPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Bee_Boss_PumpkinButt.prefab";
        private const string ChestPath = "Assets/Prefabs/Items/Chest/Treasure_Chest_and_Seed_CULUA.prefab";
        private const string PumpkinFsm = "GAME-Pumpkin6";
        private const string BossFlag = "BeeBoss";
        private const string ChestVariable = "Chest";

        [MenuItem("Tools/429 Game/Enemies/Whack Pumpkin 6 Drops A Chest")]
        public static void SetUp()
        {
            var notes = Apply();
            Debug.Log("[Whack Chest] " + (notes.Count > 0 ? string.Join("; ", notes) + ". Save the scene." : "Already set up."));
        }

        [MenuItem("Tools/429 Game/Enemies/Whack Pumpkin 6 Drops A Chest", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        public static List<string> Apply()
        {
            var notes = new List<string>();
            var pumpkin = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == PumpkinFsm);
            var pumpkin7 = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAME-Pumpkin7");
            if (pumpkin == null || pumpkin7 == null)
            {
                Debug.LogError("[Whack Chest] No GAME-Pumpkin6 / GAME-Pumpkin7 FSM in the open scene: open 2 Game Scene first. Nothing changed.");
                return notes;
            }
            if (PrefabStageOpen(BossPath) || PrefabStageOpen(ButtPath))
            {
                Debug.LogError("[Whack Chest] Bee_Boss or Bee_Boss_PumpkinButt is open in Prefab Mode: close it first. Nothing changed.");
                return notes;
            }

            if (PointBossAtPumpkin()) notes.Add("Bee_Boss sets GAME-Pumpkin6's BeeBoss when it dies (PumpkinButt unchanged)");
            if (DropChest(pumpkin, pumpkin7)) notes.Add("Whack Pumpkin 6 drops a CULUA chest after Bee_Boss and sets whack_6 once it's opened");
            return notes;
        }

        private static bool PrefabStageOpen(string path)
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.assetPath == path;
        }

        // ---- Prefabs

        // Bee_Boss's first death flag goes to the pumpkin. PumpkinButt (a variant) inherits that flag's object and FSM, so
        // it first gets its own (GAMESTAGES, as before) written in, to keep setting whack_7 there.
        private static bool PointBossAtPumpkin()
        {
            if (Flag(BossPath, 0, "boolName") == BossFlag) return false;
            EditFlag(BossPath, so =>
            {
                Set(so, 0, "globalObject", PumpkinFsm);
                Set(so, 0, "fsm", PumpkinFsm);
                Set(so, 0, "boolName", BossFlag);
                so.FindProperty("setOnDeath").GetArrayElementAtIndex(0).FindPropertyRelative("value").boolValue = true;
            });
            EditFlag(ButtPath, so =>
            {
                Set(so, 0, "globalObject", "GAMESTAGES");
                Set(so, 0, "fsm", "GAMESTAGES");
            });
            return true;
        }

        private static string Flag(string path, int index, string field)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var so = FlagOwner(root);
                var list = so?.FindProperty("setOnDeath");
                return list != null && list.arraySize > index ? list.GetArrayElementAtIndex(index).FindPropertyRelative(field).stringValue : null;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EditFlag(string path, System.Action<SerializedObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var so = FlagOwner(root);
                if (so == null) return;
                edit(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // The component with the death flags (Spider Brain).
        private static SerializedObject FlagOwner(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                var so = new SerializedObject(component);
                if (so.FindProperty("setOnDeath") != null) return so;
            }
            return null;
        }

        private static void Set(SerializedObject so, int index, string field, string value) =>
            so.FindProperty("setOnDeath").GetArrayElementAtIndex(index).FindPropertyRelative(field).stringValue = value;

        // ---- Whack Pumpkin 6's FSM (in the scene)

        // The user's layout (2026-10-15): Deactivate (... Wait 3 s) > Wait Bool (Bool Test BeeBoss) > Deactivate Self
        // (Create Object the CULUA chest, Deactivate Self). This finishes it: BeeBoss starts off each time, and the pumpkin
        // stays on until the chest is opened (gone), then sets whack_6 and switches off ('Chest Opened').
        private static bool DropChest(PlayMakerFSM pumpkin, PlayMakerFSM pumpkin7)
        {
            var fsm = pumpkin.Fsm;
            if (!fsm.Initialized) fsm.Init(pumpkin);
            if (fsm.States.Any(s => s.Name == "Chest Opened")) return false;
            var dead = fsm.States.FirstOrDefault(s => s.Name == "Deactivate");
            var dropState = fsm.States.FirstOrDefault(s => s.Name == "Deactivate Self");
            var bossDown = fsm.Variables.BoolVariables.FirstOrDefault(v => v.Name == BossFlag);
            if (dead == null || dropState == null || bossDown == null)
            {
                Debug.LogWarning("[Whack Chest] GAME-Pumpkin6 needs its 'Deactivate' and 'Deactivate Self' states and the BeeBoss bool. Nothing changed there.");
                return false;
            }
            dead.LoadActions();
            dropState.LoadActions();
            var drop = dropState.Actions.OfType<CreateObject>().FirstOrDefault(c => c.gameObject.Value != null && c.gameObject.Value.name.StartsWith("Treasure_Chest"));
            // GAMESTAGES, as Whack Pumpkin 7 already points at it to set whack_7.
            var stages = pumpkin7.Fsm.States.SelectMany(s => { s.LoadActions(); return s.Actions; }).OfType<SetFsmVariable>()
                .FirstOrDefault(a => a.fsmName.Value == "GAMESTAGES");
            if (drop == null || stages == null)
            {
                Debug.LogWarning("[Whack Chest] No chest in 'Deactivate Self', or no GAMESTAGES flag in Whack Pumpkin 7 to copy. Nothing changed there.");
                return false;
            }

            // BeeBoss starts off every time the pumpkin dies (it stays set from the run before otherwise).
            if (!dead.Actions.OfType<SetBoolValue>().Any(a => a.boolVariable.Name == BossFlag))
            {
                dead.Actions = dead.Actions.Prepend(new SetBoolValue { boolVariable = Use(bossDown), boolValue = false, everyFrame = false }).ToArray();
                dead.SaveActions();
            }

            // The chest is remembered, and the pumpkin waits for it to be opened (it breaks itself away).
            var chest = Variable(fsm, ChestVariable, () => new FsmGameObject(ChestVariable), v => v.GameObjectVariables, (v, a) => v.GameObjectVariables = a);
            drop.storeObject = Use(chest);
            dropState.Actions = dropState.Actions.Where(a => a is not DeactivateSelf)
                .Append(new GameObjectIsNull { gameObject = Use(chest), isNull = FsmEvent.Finished, isNotNull = null, storeResult = null, everyFrame = true })
                .ToArray();
            dropState.Transitions = new[] { Go("Chest Opened") };

            var whack6 = new SetFsmBool
            {
                gameObject = new FsmOwnerDefault
                {
                    OwnerOption = OwnerDefaultOption.SpecifyGameObject,
                    GameObject = new FsmGameObject(stages.gameObject.GameObject),
                },
                fsmName = "GAMESTAGES",
                variableName = "whack_6",
                setValue = true,
                everyFrame = false,
            };
            var off = new DeactivateSelf();
            off.Reset();
            var opened = NewState(fsm, "Chest Opened", dropState.Position.x, dropState.Position.y + 90f, null, whack6, off);

            fsm.States = fsm.States.Append(opened).ToArray();
            dropState.SaveActions();
            opened.SaveActions();
            EditorUtility.SetDirty(pumpkin);
            EditorSceneManager.MarkSceneDirty(pumpkin.gameObject.scene);
            return true;
        }

        // The FSM's own variable of that name, made if missing.
        private static T Variable<T>(Fsm fsm, string name, System.Func<T> make, System.Func<FsmVariables, T[]> get,
            System.Action<FsmVariables, T[]> set) where T : NamedVariable
        {
            var found = get(fsm.Variables).FirstOrDefault(v => v.Name == name);
            if (found != null) return found;
            var made = make();
            set(fsm.Variables, get(fsm.Variables).Append(made).ToArray());
            return made;
        }

        // A reference to the variable, for an action's field.
        private static FsmBool Use(FsmBool variable) => new(variable) { UseVariable = true };
        private static FsmGameObject Use(FsmGameObject variable) => new(variable) { UseVariable = true };

        private static FsmTransition Go(string to) => new() { FsmEvent = FsmEvent.Finished, ToState = to };

        private static FsmState NewState(Fsm fsm, string name, float x, float y, string next, params FsmStateAction[] actions) =>
            new(fsm)
            {
                Name = name,
                Position = new Rect(x, y, 140f, 32f),
                Actions = actions,
                Transitions = next != null ? new[] { Go(next) } : new FsmTransition[0],
            };
    }
}
