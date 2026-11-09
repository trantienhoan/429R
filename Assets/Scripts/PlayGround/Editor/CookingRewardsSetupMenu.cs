using System.Collections.Generic;
using System.Linq;
using Game.PlayGround;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Set Up Cooking Rewards (user, 2026-11-06):
    /// - each cooking round's chest (PG_Wood/PG_Treasure_Chest_and_Seed_Chest / Head / Shield, rounds 1-3) bursts
    ///   candies and gold EXP orbs as it opens, more each round (Open_Chest: Play End Game Jackpot + Spill Candies, with a
    ///   small firework and the miracle sound);
    /// - the cauldron (Fire_Woods_n_Cauldron) gets Cooking Rewards: the first clear per save slot gives a stat point for
    ///   good (a big firework and a notice); later clears just the pot's usual candies.
    /// Safe to run again: it updates the numbers instead of adding twice.
    /// </summary>
    public static class CookingRewardsSetupMenu
    {
        private const string ChestFolder = "Assets/Prefabs/PlayGround/PG_Wood/";
        private const string Cauldron = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string Sound = "Assets/Audio/SFX/A_gentle_miracle_sound_1.wav";
        private const string SmallFirework = "Assets/Prefabs/Light/Firework_Win.prefab";
        private const string BigFirework = "Assets/Prefabs/Light/Firework_Big.prefab";

        // Round chest: (chest, candies, EXP, orb seconds)
        private static readonly (string chest, int candies, int exp, float seconds)[] Rounds =
        {
            ("PG_Treasure_Chest_and_Seed_Chest", 8, 40, 1.5f),
            ("PG_Treasure_Chest_and_Seed_Head", 12, 60, 1.8f),
            ("PG_Treasure_Chest_and_Seed_Shield", 16, 80, 2.1f),
        };

        [MenuItem("Tools/429 Game/PlayGround/Set Up Cooking Rewards")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Cooking Rewards] Stop play mode first."); return; }
            Debug.Log("[Cooking Rewards] " + string.Join("\n", Run()));
        }

        public static List<string> Run()
        {
            var notes = new List<string>();
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(Sound);
            var small = AssetDatabase.LoadAssetAtPath<GameObject>(SmallFirework);
            var big = AssetDatabase.LoadAssetAtPath<GameObject>(BigFirework);
            var candies = Enumerable.Range(1, 8)
                .Select(i => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Items/Breakable/Candy_{i}.prefab"))
                .Where(c => c != null).ToArray();

            foreach (var (chest, candyCount, exp, seconds) in Rounds)
            {
                var path = ChestFolder + chest + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add("No " + path); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var fsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Chest_Control");
                    var open = fsm != null ? fsm.Fsm.GetState("Open_Chest") : null;
                    if (open == null) { notes.Add(chest + ": no Chest_Control / Open_Chest"); continue; }
                    open.LoadActions();
                    var jackpot = open.Actions.OfType<PlayEndGameJackpot>().FirstOrDefault();
                    if (jackpot == null)
                    {
                        jackpot = new PlayEndGameJackpot();
                        jackpot.Reset();
                        open.Actions = open.Actions.Append(jackpot).ToArray();
                    }
                    jackpot.exp = exp;
                    jackpot.seconds = seconds;
                    jackpot.orbsPerBurst = 2;
                    jackpot.sound = new FsmObject { Value = sound };
                    jackpot.effects = small != null ? new[] { new FsmGameObject { Value = small } } : new FsmGameObject[0];
                    var spill = open.Actions.OfType<SpillCandies>().FirstOrDefault();
                    if (spill == null)
                    {
                        spill = new SpillCandies();
                        spill.Reset();
                        open.Actions = open.Actions.Append(spill).ToArray();
                    }
                    spill.count = candyCount;
                    spill.candies = candies.Select(c => new FsmGameObject { Value = c }).ToArray();
                    spill.seconds = 1.2f;
                    spill.upSpeed = new Vector2(1.8f, 3f);
                    spill.outSpeed = new Vector2(0.6f, 1.4f);
                    open.SaveActions();
                    EditorUtility.SetDirty(fsm);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add($"{chest}: opens with {candyCount} candies + {exp} EXP in gold orbs ({seconds} s), Firework_Win");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var cauldron = PrefabUtility.LoadPrefabContents(Cauldron);
            try
            {
                var rewards = cauldron.GetComponent<CookingRewards>();
                if (rewards == null) rewards = cauldron.AddComponent<CookingRewards>();
                var so = new SerializedObject(rewards);
                so.FindProperty("effect").objectReferenceValue = big;
                so.FindProperty("sound").objectReferenceValue = sound;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(cauldron, Cauldron);
                notes.Add("Fire_Woods_n_Cauldron: Cooking Rewards (first clear per save slot: +1 stat point for good, Firework_Big, notice)");
            }
            finally { PrefabUtility.UnloadPrefabContents(cauldron); }
            return notes;
        }
    }
}
