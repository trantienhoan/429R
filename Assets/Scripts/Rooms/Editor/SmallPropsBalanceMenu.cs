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
    /// Tools > 429 Game > Rooms > Toughen Small Props. Small breakables broke too easily: a 5 m/s knock cost them health,
    /// and that's a drop of only 1.3 m (off a shelf or a table, when the furniture under them is knocked over, or where
    /// they're spawned a little above it). Now:
    /// - paper boxes, books, candy, skulls and lollipops need a knock of 7 m/s (a 2.5 m drop); punches and throws still
    ///   break them as before;
    /// - Bedroom_Book_6 had 2 health where the other books have 7, and Paper_Box_0 had 2: now 7 and 3;
    /// - Pumpkin_4 jumped away from a hit at 13 m/s, faster than its own 12 m/s limit, so its landing often cost it a
    ///   second health point (and started the next wave early): it jumps at 8.7 m/s now;
    /// - Livingroom_Table_2 spawned its two random boxes in the same spot (they burst apart): each at its own spot now.
    /// Running it again changes nothing.
    /// </summary>
    public static class SmallPropsBalanceMenu
    {
        private const string Breakable = "Assets/Prefabs/Items/Breakable/";
        private const float KnockSpeed = 7f;

        [MenuItem("Tools/429 Game/Rooms/Toughen Small Props")]
        public static void Toughen()
        {
            var paths = new List<string>();
            for (int i = 0; i <= 5; i++) paths.Add(Breakable + "Paper_Box_" + i + ".prefab");
            for (int i = 1; i <= 13; i++) paths.Add(Breakable + "Bedroom_Book_" + i + ".prefab");
            for (int i = 1; i <= 8; i++) paths.Add(Breakable + "Candy_" + i + ".prefab");
            paths.Add(Breakable + "Skull_2.prefab");
            for (int i = 1; i <= 7; i++) paths.Add("Assets/Prefabs/PlayGround/Lolipop_" + i + ".prefab");
            var health = new Dictionary<string, float>
            {
                { Breakable + "Bedroom_Book_6.prefab", 7f },
                { Breakable + "Paper_Box_0.prefab", 3f },
            };

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var changed = new List<string>();
            var notes = new List<string>();
            foreach (var path in paths)
            {
                Edit(path, stage, notes, root =>
                {
                    bool any = RaiseKnockSpeed(root);
                    if (health.TryGetValue(path, out float hp)) any |= SetHealth(root, hp);
                    return any;
                }, changed);
            }
            Edit(Breakable + "Pumpkin_4.prefab", stage, notes, SlowPumpkinJump, changed);
            Edit("Assets/Prefabs/BedRoom/Livingroom_Table_2.prefab", stage, notes, root => OwnSpot(root, "Paper_Box_3_Pos"), changed);

            Debug.Log("[Small Props] " + (changed.Count > 0 ? "Changed: " + string.Join(", ", changed) : "already done") +
                      (notes.Count > 0 ? ". NOTES: " + string.Join("; ", notes) : "") + ".");
        }

        [MenuItem("Tools/429 Game/Rooms/Toughen Small Props", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static void Edit(string path, PrefabStage stage, List<string> notes, System.Func<GameObject, bool> change, List<string> changed)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add("no " + path);
                return;
            }
            if (stage != null && stage.assetPath == path)
            {
                notes.Add(path + " is open in Prefab Mode, skipped");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!change(root)) return;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // In the Damage FSM: knocks slower than Knock Speed don't cost health (the compare of the stored collision speed).
        private static bool RaiseKnockSpeed(GameObject root)
        {
            bool any = false;
            foreach (var component in root.GetComponents<PlayMakerFSM>().Where(f => f.FsmName == "Damage"))
            {
                bool changedHere = false;
                foreach (var state in component.Fsm.States)
                {
                    state.LoadActions();
                    foreach (var compare in state.Actions.OfType<FloatCompare>())
                    {
                        if (compare.float1 == null || compare.float1.Name != "collisionForce") continue;
                        if (compare.float2 == null || compare.float2.UseVariable || compare.float2.Value >= KnockSpeed) continue;
                        compare.float2.Value = KnockSpeed;
                        changedHere = true;
                    }
                }
                if (!changedHere) continue;
                foreach (var state in component.Fsm.States) state.SaveActions();
                EditorUtility.SetDirty(component);
                any = true;
            }
            return any;
        }

        // The Health FSM's 'health' (through the serialized FSM: a value set in code isn't saved).
        private static bool SetHealth(GameObject root, float value)
        {
            var component = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Health");
            if (component == null) return false;
            var so = new SerializedObject(component);
            var floats = so.FindProperty("fsm.variables.floatVariables");
            for (int i = 0; i < floats.arraySize; i++)
            {
                var variable = floats.GetArrayElementAtIndex(i);
                if (variable.FindPropertyRelative("name").stringValue != "health") continue;
                var v = variable.FindPropertyRelative("value");
                if (Mathf.Approximately(v.floatValue, value)) return false;
                v.floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }
            return false;
        }

        // Pumpkin_4's jump away from a hit: 9 sideways became 6.
        private static bool SlowPumpkinJump(GameObject root)
        {
            bool any = false;
            foreach (var component in root.GetComponents<PlayMakerFSM>().Where(f => f.FsmName == "Damage"))
            {
                bool changedHere = false;
                foreach (var state in component.Fsm.States)
                {
                    state.LoadActions();
                    foreach (var push in state.Actions.OfType<SetVelocity>())
                    {
                        foreach (var axis in new[] { push.x, push.z })
                        {
                            if (axis == null || axis.UseVariable || Mathf.Abs(axis.Value) <= 6f) continue;
                            axis.Value = Mathf.Sign(axis.Value) * 6f;
                            changedHere = true;
                        }
                    }
                }
                if (!changedHere) continue;
                foreach (var state in component.Fsm.States) state.SaveActions();
                EditorUtility.SetDirty(component);
                any = true;
            }
            return any;
        }

        // The child's spawn (Create Object) happens at the child itself, not at a sibling's spot.
        private static bool OwnSpot(GameObject root, string child)
        {
            var spot = root.transform.Find(child);
            var component = spot != null ? spot.GetComponents<PlayMakerFSM>().FirstOrDefault() : null;
            if (component == null) return false;
            bool any = false;
            foreach (var state in component.Fsm.States)
            {
                state.LoadActions();
                foreach (var create in state.Actions.OfType<CreateObject>())
                {
                    if (create.spawnPoint == null || create.spawnPoint.UseVariable || create.spawnPoint.Value == spot.gameObject) continue;
                    create.spawnPoint = new FsmGameObject { Value = spot.gameObject };
                    any = true;
                }
            }
            if (!any) return false;
            foreach (var state in component.Fsm.States) state.SaveActions();
            EditorUtility.SetDirty(component);
            return true;
        }
    }
}
