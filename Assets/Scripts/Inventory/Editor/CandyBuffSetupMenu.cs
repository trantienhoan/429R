using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.Inventory.Editor
{
    /// <summary>
    /// Tools > 429 Game > Player > Give Candies Their Buffs.
    /// Each Candy_1..8 gets an Eat Candy Buff in its eat state (the Damage FSM's "Health 1", where it heals and adds the
    /// Wrap): Candy 1, 2, 6, 7 Toughness, Candy 3, 4 Throw Power, Candy 5 Move Speed, Candy 8 Punch Damage, a tenth of a
    /// level-up point each (Player Level keeps them for the run, at most 10 a stat). Safe to run again: a candy that
    /// already has one gets this table's boost and points back.
    /// </summary>
    public static class CandyBuffSetupMenu
    {
        private const string CandyFolder = "Assets/Prefabs/Items/Breakable/";
        private const string EatState = "Health 1";
        private const float PointsPerCandy = 0.1f;

        // Candy_1..8, in order.
        private static readonly EatCandyBuff.Boost[] Boosts =
        {
            EatCandyBuff.Boost.Toughness, EatCandyBuff.Boost.Toughness, EatCandyBuff.Boost.ThrowPower,
            EatCandyBuff.Boost.ThrowPower, EatCandyBuff.Boost.MoveSpeed, EatCandyBuff.Boost.Toughness,
            EatCandyBuff.Boost.Toughness, EatCandyBuff.Boost.PunchDamage,
        };

        [MenuItem("Tools/429 Game/Player/Give Candies Their Buffs")]
        public static void Run()
        {
            int added = 0, updated = 0;
            var missing = new System.Collections.Generic.List<string>();
            for (int i = 1; i <= Boosts.Length; i++)
            {
                string path = CandyFolder + "Candy_" + i + ".prefab";
                switch (GiveBuff(path, Boosts[i - 1]))
                {
                    case 1: added++; break;
                    case 0: updated++; break;
                    default: missing.Add("Candy_" + i); break;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Candy] Buffs: {added} candies got Eat Candy Buff, {updated} already had it (set again)" +
                      (missing.Count > 0 ? $"; NOT changed: {string.Join(", ", missing)} (no '{EatState}' state, or an action in it couldn't be read)." : ".") +
                      " Reloading scripts so the Editor shows the saved candies.");
            // PlayMaker keeps the actions it has already read from a prefab, and saving the prefab doesn't renew them (a
            // candy read before could show its buff with 0 points, and an FSM edit could write that back). Only a script
            // reload makes it read them again from what was saved.
            EditorUtility.RequestScriptReload();
        }

        // 1 = added, 0 = already there (set again), -1 = not changed (no candy, no eat state, or an action in it unreadable).
        private static int GiveBuff(string path, EatCandyBuff.Boost boost)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return -1;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var fsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Damage");
                if (fsm == null) return -1;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                var state = fsm.Fsm.GetState(EatState);
                if (state == null) return -1;
                state.LoadActions();
                // Saving writes back only the actions that could be read: if one couldn't (e.g. renamed), leave the candy
                // alone rather than lose its heal or its Wrap.
                if (state.ActionData.ActionCount != state.Actions.Length ||
                    state.Actions.Any(a => a == null || a.GetType().Name == "MissingAction"))
                {
                    Debug.LogError($"[Candy] {path}: an action in '{EatState}' couldn't be read, so the candy was left " +
                                   "as it is (no buff added). Fix that action in PlayMaker, then run this again.");
                    return -1;
                }

                var buff = state.Actions.OfType<EatCandyBuff>().FirstOrDefault();
                int result = 0;
                if (buff == null)
                {
                    buff = new EatCandyBuff();
                    buff.Reset();
                    state.Actions = state.Actions.Concat(new FsmStateAction[] { buff }).ToArray();
                    result = 1;
                }
                buff.boost = boost;
                buff.points = PointsPerCandy;
                state.SaveActions();
                EditorUtility.SetDirty(fsm);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
