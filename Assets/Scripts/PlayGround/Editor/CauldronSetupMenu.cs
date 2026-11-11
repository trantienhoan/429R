using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Set Up Cauldron. Sets the Cauldron on Fire_Woods_n_Cauldron for the PlayGround's
    /// three cooking rounds:
    /// - Rounds: 3 / 4 / 5 candies + 1 Pumpkin_On_Tree, making Pumpkin_8 / 9 / 10, cooking 30 / 45 / 60 s.
    /// - A wrong recipe boils 2.5 s, then Slime_Big lands next to the pot with a puff (Done Effect Scale 2.5; run Set Up
    ///   PlayGround Slimes first).
    /// - Push back: a head within 0.55 m of the pot's middle is pushed 0.6 m, at most every 2 s.
    /// - Bell: a punch or a weapon hit rings Clear,_resonant_bell_1/2 and lets a PG_White_Slime out (3 at most, 3 s apart).
    /// - The candy pot: 8 candies, 3 s cook, rings every 4-6 s, no seed and no candies, a big smoke burst (scale 6).
    /// - Makes Magical_Pot/Bubble_Tower from the pot's coloured bubbles (points) and hooks it up.
    /// Only the Cauldron component (and the new Bubble_Tower) is changed: its Damage FSM only plays a metal clank, so
    /// it stays. Open Unity once first so the bell clips are imported. Safe to run again; close the prefab first.
    /// </summary>
    public static class CauldronSetupMenu
    {
        private const string MenuName = "Set Up Cauldron";
        private const string CauldronPath = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string Breakable = "Assets/Prefabs/Items/Breakable/";
        private const string SlimeFolder = "Assets/Prefabs/Enemies/PLAYGROUND/";
        private const string SmokePath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string TowerName = "Bubble_Tower";

        private static readonly string[] Results = { "Pumpkin_8", "Pumpkin_9", "Pumpkin_10" };
        private static readonly int[] RecipeCandies = { 3, 4, 5 };
        private static readonly int[] RecipePumpkins = { 1, 1, 1 };
        private static readonly float[] CookSeconds = { 30f, 45f, 60f };
        private static readonly string[] BellClips = { "Clear,_resonant_bell_1.wav", "Clear,_resonant_bell_2.wav" };
        // Done Effect Scale and Break Effect Scale replace the smoke's own scale (Smoke_Flash_Small_Puff's root is 3).
        private const float DoneSmokeScale = 2.5f;
        private const float BreakSmokeScale = 6f;

        [MenuItem("Tools/429 Game/PlayGround/Set Up Cauldron")]
        public static void Run()
        {
            var notes = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CauldronPath) == null)
            {
                Debug.LogWarning($"[{MenuName}] no '{CauldronPath}', nothing done.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(CauldronPath);
            try
            {
                var cauldron = root.GetComponent<Cauldron>();
                if (cauldron == null)
                {
                    Debug.LogWarning($"[{MenuName}] no Cauldron on '{CauldronPath}', nothing done.");
                    return;
                }
                var so = new SerializedObject(cauldron);
                var tower = MakeBubbleTower(root, so, notes);
                SetRounds(so, notes);
                SetWrongRecipe(so, notes);
                SetPushAndBell(so, notes);
                SetCandyPot(so, notes);
                so.FindProperty("bubbleTower").objectReferenceValue = tower;
                so.FindProperty("towerSmall").floatValue = 0.4f;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CauldronPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[{MenuName}] Fire_Woods_n_Cauldron: " + string.Join("; ", notes));
        }

        // Three dishes, each with its own cook time; the candies and the pumpkin recipes ask for.
        private static void SetRounds(SerializedObject so, List<string> notes)
        {
            var candies = new List<Object>();
            foreach (int n in Game.Inventory.CandyKinds.Numbers)
            {
                var candy = Load<GameObject>(Breakable + "Candy_" + n + ".prefab");
                if (candy != null) candies.Add(candy);
            }
            if (candies.Count > 0) SetArray(so.FindProperty("candies"), candies.ToArray());
            else Warn(notes, "none of the 6 candies found, candies kept");

            var pumpkin = Load<GameObject>(Breakable + "Pumpkin_On_Tree.prefab");
            if (pumpkin != null) so.FindProperty("pumpkin").objectReferenceValue = pumpkin;
            else Warn(notes, "no Pumpkin_On_Tree, pumpkin kept");

            var rounds = so.FindProperty("rounds");
            rounds.arraySize = Results.Length;
            for (int i = 0; i < Results.Length; i++)
            {
                var dish = rounds.GetArrayElementAtIndex(i);
                dish.FindPropertyRelative("pumpkins").intValue = RecipePumpkins[i];
                dish.FindPropertyRelative("candies").intValue = RecipeCandies[i];
                var result = Load<GameObject>(Breakable + Results[i] + ".prefab");
                if (result != null) dish.FindPropertyRelative("result").objectReferenceValue = result;
                else Warn(notes, $"no {Results[i]}, round {i + 1}'s result kept");
                dish.FindPropertyRelative("results").intValue = 1;
                dish.FindPropertyRelative("cookSeconds").floatValue = CookSeconds[i];
            }
            so.FindProperty("cookSeconds").floatValue = CookSeconds[0];
            so.FindProperty("maxKinds").intValue = 3;
            so.FindProperty("hatEachRound").boolValue = true;
            so.FindProperty("spitToMiddle").boolValue = true;
            notes.Add($"{Results.Length} rounds ({string.Join("/", CookSeconds)} s), {candies.Count} candies");
        }

        // A wrong recipe boils a moment, then spits out the plain big slime.
        private static void SetWrongRecipe(SerializedObject so, List<string> notes)
        {
            var slime = Load<GameObject>(SlimeFolder + "Slime_Big.prefab");
            if (slime != null) so.FindProperty("wrongResult").objectReferenceValue = slime;
            else Warn(notes, "no Slime_Big yet (run Set Up PlayGround Slimes, then this again), wrong result kept");
            so.FindProperty("minBadPumpkins").intValue = 1;
            so.FindProperty("maxBadPumpkins").intValue = 1;
            so.FindProperty("wrongBoilSeconds").floatValue = 2.5f;
            so.FindProperty("spitDistance").floatValue = 2.5f;
            var done = so.FindProperty("doneEffect");
            if (done.objectReferenceValue == null) done.objectReferenceValue = Load<GameObject>(SmokePath);
            // A little under a stock puff (the cook-done burst and the slime's landing).
            so.FindProperty("doneEffectScale").floatValue = DoneSmokeScale;
            notes.Add("wrong recipe: " + (slime != null ? slime.name : "?") + " after 2.5 s, puff " + DoneSmokeScale);
        }

        private static void SetPushAndBell(SerializedObject so, List<string> notes)
        {
            so.FindProperty("pushRadius").floatValue = 0.55f;
            so.FindProperty("pushDistance").floatValue = 0.6f;
            so.FindProperty("pushCooldown").floatValue = 2f;
            so.FindProperty("pushMinShare").floatValue = 1f;

            var bells = new List<Object>();
            foreach (var clip in BellClips)
            {
                var sound = Load<AudioClip>(Sfx + clip);
                if (sound != null) bells.Add(sound);
                else Warn(notes, $"'{clip}' not imported yet (open Unity so it imports, then run this again)");
            }
            if (bells.Count > 0) SetArray(so.FindProperty("bellSounds"), bells.ToArray());
            so.FindProperty("bellVolume").floatValue = 1f;

            var white = Load<GameObject>(SlimeFolder + "PG_White_Slime.prefab");
            if (white != null) SetArray(so.FindProperty("wildSlimes"), new Object[] { white });
            else Warn(notes, "no PG_White_Slime, wild slimes kept");
            so.FindProperty("maxWildSlimes").intValue = 3;
            so.FindProperty("slimeCooldown").floatValue = 3f;
            so.FindProperty("bellHitSpeed").floatValue = 1.5f;
            notes.Add($"push 0.55/0.6/2 s, {bells.Count} bell clips, wild slime {(white != null ? white.name : "?")} x3");
        }

        // The finale: candies only, a short cook, timed rings, and a big smoke burst with nothing left behind.
        private static void SetCandyPot(SerializedObject so, List<string> notes)
        {
            so.FindProperty("mysteryCount").intValue = 8;
            so.FindProperty("mysteryCookSeconds").floatValue = 3f;
            so.FindProperty("finaleBellEvery").vector2Value = new Vector2(4f, 6f);
            so.FindProperty("finalSeed").objectReferenceValue = null;
            so.FindProperty("finalCandies").intValue = 0;
            var smoke = Load<GameObject>(SmokePath);
            if (smoke != null) so.FindProperty("breakEffect").objectReferenceValue = smoke;
            else Warn(notes, "no Smoke_Flash_Small_Puff, break effect kept");
            // Twice a stock puff (its root is 3), as big as a bursting bug pumpkin's.
            so.FindProperty("breakEffectScale").floatValue = BreakSmokeScale;
            notes.Add("candy pot 8 candies, 3 s, no seed, smoke " + BreakSmokeScale);
        }

        // Magical_Pot/Bubble_Tower: a copy of the pot's coloured bubbles (points) that streams up in a tall column. A
        // sibling of Cauldron_Surface, so the surface's own speed-up leaves it alone; off until the cauldron runs it.
        private static ParticleSystem MakeBubbleTower(GameObject root, SerializedObject so, List<string> notes)
        {
            var pot = so.FindProperty("pot").objectReferenceValue as Transform;
            if (pot == null) pot = root.transform.Find("Magical_Pot");
            var points = pot != null ? FindChild(pot, "points") : null;
            if (pot == null || points == null || !points.TryGetComponent<ParticleSystem>(out var source))
            {
                Warn(notes, "no Magical_Pot/.../points, no bubble tower");
                return so.FindProperty("bubbleTower").objectReferenceValue as ParticleSystem;
            }

            var tower = pot.Find(TowerName);
            bool made = tower == null;
            if (made)
            {
                tower = Object.Instantiate(source.gameObject, pot).transform;
                tower.name = TowerName;
                for (int i = tower.childCount - 1; i >= 0; i--) Object.DestroyImmediate(tower.GetChild(i).gameObject);
            }
            // Where the bubbles are, pointing up, the same size.
            tower.SetPositionAndRotation(points.position, points.rotation);
            var big = points.lossyScale;
            var parent = pot.lossyScale;
            tower.localScale = new Vector3(big.x / Safe(parent.x), big.y / Safe(parent.y), big.z / Safe(parent.z));

            var system = tower.GetComponent<ParticleSystem>();
            var main = system.main;
            main.loop = true;
            main.prewarm = false;
            main.playOnAwake = false;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 3f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            main.maxParticles = Mathf.Max(main.maxParticles, 80);
            var emission = system.emission;
            emission.enabled = true;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            emission.rateOverTime = 14f;
            var shape = system.shape;
            shape.angle = 0f;
            shape.radius = 0.6f;
            notes.Add(made ? "Bubble_Tower made" : "Bubble_Tower updated");
            return system;
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName) return child;
            }
            return null;
        }

        private static float Safe(float value) => Mathf.Abs(value) < 0.0001f ? 1f : value;

        private static void Warn(List<string> notes, string text)
        {
            notes.Add(text);
            Debug.LogWarning($"[{MenuName}] {text}");
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
