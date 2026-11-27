using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Combat.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Combat > Balance Weapons (re-runnable; "Weapons: Back To The Old Numbers" undoes it). User
    /// 2026-11-27: "find all the weapon in the game, help me balance their damage power and their health, durable stats.
    /// Check the cooking pan also, rotate its attach by 90, so when Player hold the pan and smash stuff, they smash with
    /// the flat surface of the pan."
    ///
    /// How the numbers work (EnemyHitDamage + Hit Settings): a hit's power = the speed where it lands x the weapon's Power /
    /// the enemy's Toughness; under 3 it does nothing, 3-8 its Damage, 8-16 twice (hard), 16+ three times (smash). Held
    /// in two hands (Two Handed Grip) it does x1.5 (the bat x1.6) and wears x2. A weapon wears the same way on itself:
    /// the speed of the knock / its own Toughness, so with Toughness 4 a knock from 12 m/s takes 1 of its Health, from
    /// 32 m/s 2; at 0 it breaks into its pieces.
    ///
    /// Before (2026-11-27): the hammers (Toughness 10) only wore from 30 m/s, so they never broke, while their Health went
    /// from 10 to 35; the frying pan hit like a hammer, stunned 1.6 s and was the most durable (Health 40); the broom was
    /// the weakest and the most fragile (16, Toughness 5); flip-flops, boongs and the Skull Gun never wore either.
    /// Now every weapon wears from 12 m/s (Toughness 4) and its Health is how many such knocks it takes (round 2, user:
    /// "make them more durable"): heavy (hammers, bat) 60 - pan 50 - broom 45 - light (flip-flops, boongs) 40 - Skull Gun,
    /// pillow 25. Every weapon Damage 2, so a weapon hit beats a bare punch (Damage 1): at 10 m/s on a normal enemy
    /// hammers and bat 6, pan, broom, flip-flops, boongs, Skull Gun, pillow 4 (a punch 3); on a tough enemy (Toughness 2)
    /// at 14 m/s every weapon 4, a punch 3. Each Weapon Efficiency point adds +1 to a weapon hit, Fist Size only +1 to a
    /// punch every 3 points, so the gap grows with the stats. The legendary sword and
    /// shield are left as they are (rare, unbreakable, 210 s).
    /// Also sets the pan's grip turned 90 degrees round its handle: a swing leads with its flat face, not its rim.
    /// PanAndBatSetupMenu and DamageBalanceMenu hold the same numbers, so running them again keeps these.
    /// </summary>
    internal static class WeaponBalanceMenu
    {
        private const float Wear = 4f;   // every weapon wears from 12 m/s knocks

        // Round 2 (user 2026-11-27: "don't need to make them strong, make them more durable is better, balance them so
        // that hitting with weapon is better than punching, so losing that weapon is really a lost, player have to use
        // weapon carefully"): every weapon Damage 2 (a bare punch is Damage 1 with Power 2.6: 2 a punch, 3 a hard one),
        // the light ones' Power a little up, the strong ones unchanged; Health doubled. Wear stays: only knocks from
        // 12 m/s (Toughness 4) wear a weapon, so careful fighting keeps it, wild smashing into floors and walls breaks it.
        internal static readonly Dictionary<string, (float power, float damage, float health, float toughness, float stun, float knockBack)> Now = new()
        {
            { "Weapon/Hammer_Tool", (2f, 2f, 60f, Wear, 0f, 0f) },
            { "Weapon/Hammer_Boong", (2f, 2f, 60f, Wear, 0f, 0f) },
            { "Weapon/Hammer_Boong 2ND", (2f, 2f, 60f, Wear, 0f, 0f) },
            { "Weapon/Hammer_Boong_Bone", (2f, 2f, 60f, Wear, 0f, 0f) },
            { "Weapon/Weapon_Baseball_Bat", (2f, 2f, 60f, Wear, 0f, 2.5f) },
            { "Weapon/Weapon_Frying_Pan", (1.5f, 2f, 50f, Wear, 1.2f, 0f) },
            { "Weapon/Hammer_Broom", (1.4f, 2f, 45f, Wear, 0f, 0f) },
            { "Weapon/Flip_Flop_L", (1.3f, 2f, 40f, Wear, 0f, 0f) },
            { "Weapon/Flip_Flop_R", (1.3f, 2f, 40f, Wear, 0f, 0f) },
            { "Weapon/Flip_Flop2_L", (1.3f, 2f, 40f, Wear, 0f, 0f) },
            { "Weapon/Flip_Flop2_R", (1.3f, 2f, 40f, Wear, 0f, 0f) },
            { "Weapon/Elon_Boong", (1.3f, 2f, 40f, Wear, 0f, 0f) },
            { "Weapon/Bus_Boong", (1.3f, 2f, 40f, Wear, 0f, 0f) },
            { "Weapon/Boong_Gun", (1.2f, 2f, 25f, Wear, 0f, 0f) },
            { "Weapon/Hammer_Pillow", (1.15f, 2f, 25f, Wear, 0f, 0f) },   // 1.15: never below a bare punch (a hard punch on a tough enemy is 3)
        };

        // As they were before 2026-11-27 (for the undo).
        private static readonly Dictionary<string, (float power, float damage, float health, float toughness, float stun, float knockBack)> Old = new()
        {
            { "Weapon/Hammer_Tool", (2f, 2f, 10f, 10f, 0f, 0f) },
            { "Weapon/Hammer_Boong", (2f, 2f, 29f, 10f, 0f, 0f) },
            { "Weapon/Hammer_Boong 2ND", (2f, 2f, 35f, 10f, 0f, 0f) },
            { "Weapon/Hammer_Boong_Bone", (2f, 2f, 10f, 10f, 0f, 0f) },
            { "Weapon/Weapon_Baseball_Bat", (2.2f, 2f, 24f, 5f, 0f, 2.5f) },
            { "Weapon/Weapon_Frying_Pan", (1.7f, 2f, 40f, 6f, 1.6f, 0f) },
            { "Weapon/Hammer_Broom", (1.3f, 1f, 16f, 5f, 0f, 0f) },
            { "Weapon/Flip_Flop_L", (1.2f, 1f, 21f, 10f, 0f, 0f) },
            { "Weapon/Flip_Flop_R", (1.2f, 1f, 21f, 10f, 0f, 0f) },
            { "Weapon/Flip_Flop2_L", (1.2f, 1f, 21f, 10f, 0f, 0f) },
            { "Weapon/Flip_Flop2_R", (1.2f, 1f, 21f, 10f, 0f, 0f) },
            { "Weapon/Elon_Boong", (1.3f, 1f, 12f, 10f, 0f, 0f) },
            { "Weapon/Bus_Boong", (1.3f, 1f, 12f, 10f, 0f, 0f) },
            { "Weapon/Boong_Gun", (1.6f, 1f, 5f, 6.666667f, 0f, 0f) },
            { "Weapon/Hammer_Pillow", (0.6f, 1f, 7f, 8.333333f, 0f, 0f) },
        };

        private const string PanPath = "Assets/Prefabs/Weapon/Weapon_Frying_Pan.prefab";
        private const string BroomPath = "Assets/Prefabs/Weapon/Hammer_Broom.prefab";
        /// <summary>The pan's grip turned this far round its handle (its flat face leads a swing; the broom's hold, end
        /// for end, put its rim first).</summary>
        internal const float PanGripTurn = 90f;

        [MenuItem("Tools/429 Game/Combat/Balance Weapons")]
        private static void Balance()
        {
            var notes = new List<string>();
            foreach (var pair in Now) notes.Add(Apply(pair.Key, pair.Value));
            notes.Add(TurnPanGrip(true));
            Debug.Log("[Weapons] Balanced:\n" + string.Join("\n", notes));
        }

        [MenuItem("Tools/429 Game/Combat/Weapons: Back To The Old Numbers")]
        private static void Back()
        {
            var notes = new List<string>();
            foreach (var pair in Old) notes.Add(Apply(pair.Key, pair.Value));
            notes.Add(TurnPanGrip(false));
            Debug.Log("[Weapons] Back to the numbers before 2026-11-27:\n" + string.Join("\n", notes));
        }

        private static string Apply(string path, (float power, float damage, float health, float toughness, float stun, float knockBack) row)
        {
            string full = "Assets/Prefabs/" + path + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(full) == null) return path + ": MISSING";
            var root = PrefabUtility.LoadPrefabContents(full);
            try
            {
                var notes = new List<string>();
                if (root.TryGetComponent(out WeaponPower weapon))
                {
                    var so = new SerializedObject(weapon);
                    so.FindProperty("power").floatValue = row.power;
                    so.FindProperty("damage").floatValue = row.damage;
                    so.FindProperty("stunSeconds").floatValue = row.stun;
                    so.FindProperty("knockBackTimes").floatValue = row.knockBack;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else notes.Add("no Weapon Power");
                if (root.TryGetComponent(out EnemyHitDamage wear))
                {
                    var so = new SerializedObject(wear);
                    so.FindProperty("toughness").floatValue = row.toughness;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else notes.Add("no Enemy Hit Damage (no wear)");
                bool health = false;
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "Health") continue;
                    var so = new SerializedObject(fsm);
                    var floats = so.FindProperty("fsm.variables.floatVariables");
                    for (int i = 0; i < floats.arraySize; i++)
                    {
                        var v = floats.GetArrayElementAtIndex(i);
                        if (v.FindPropertyRelative("name").stringValue != "health") continue;
                        v.FindPropertyRelative("value").floatValue = row.health;
                        health = true;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (!health) notes.Add("no Health FSM health");
                PrefabUtility.SaveAsPrefabAsset(root, full);
                return $"{path}: power {row.power}, damage {row.damage}, health {row.health}, toughness {row.toughness:0.##}" +
                       (row.stun > 0f ? $", stun {row.stun}" : "") + (row.knockBack > 0f ? $", knock-back x{row.knockBack}" : "") +
                       (notes.Count > 0 ? " (" + string.Join(", ", notes) + ")" : "");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The pan's grip: the broom's, end for end, then (turned) 90 degrees round the handle.</summary>
        internal static Quaternion PanGrip(Quaternion broomGrip, bool turned) =>
            Quaternion.Euler(180f, 0f, 0f) * broomGrip * Quaternion.Euler(0f, turned ? PanGripTurn : 0f, 0f);

        private static string TurnPanGrip(bool turned)
        {
            var broom = AssetDatabase.LoadAssetAtPath<GameObject>(BroomPath);
            var broomAttach = broom != null ? broom.transform.Find("Attach") : null;
            if (broomAttach == null || AssetDatabase.LoadAssetAtPath<GameObject>(PanPath) == null) return "pan grip: NOT turned (no broom or pan)";
            var root = PrefabUtility.LoadPrefabContents(PanPath);
            try
            {
                var attach = root.transform.Find("Attach");
                if (attach == null) return "pan grip: no Attach";
                attach.localRotation = PanGrip(broomAttach.localRotation, turned);
                PrefabUtility.SaveAsPrefabAsset(root, PanPath);
                return "pan grip: " + (turned ? "turned 90 degrees (flat face first)" : "as before (rim first)") + ", local " + attach.localEulerAngles.ToString("F0");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
