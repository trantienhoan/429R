using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.Inventory.Editor
{
    /// <summary>
    /// Tools > 429 Game > Player > Give Candies Their Buffs.
    /// Each candy in the game (Candy Kinds: user 2026-11-10, one per level-up stat) gets an Eat Candy Buff in its eat
    /// state (the Damage FSM's "Health 1", where it heals and adds the Wrap): Candy_1 Max Health, Candy_2 Weapon
    /// Efficiency, Candy_4 Move Speed, Candy_5 Toughness, Candy_7 Throw Power, Candy_8 Fist Size, half a level-up point
    /// each before the player's own points in that stat make it bigger (Player Level keeps them for the run: the full
    /// share up to +5 a stat, then a tenth a candy, at most 10). Retired candies (Candy_3, Candy_6) are left alone.
    /// Safe to run again: a candy that already has one gets its stat and points back.
    /// </summary>
    public static class CandyBuffSetupMenu
    {
        private const string EatState = "Health 1";
        private const float PointsPerCandy = 0.5f;

        [MenuItem("Tools/429 Game/Player/Give Candies Their Buffs")]
        public static void Run()
        {
            int added = 0, updated = 0;
            var missing = new System.Collections.Generic.List<string>();
            foreach (int n in CandyKinds.Numbers)
            {
                string path = CandyKinds.PathOf(n);
                switch (GiveBuff(path, EatCandyBuff.BoostOf(CandyKinds.StatOf(n))))
                {
                    case 1: added++; break;
                    case 0: updated++; break;
                    default: missing.Add("Candy_" + n); break;
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
