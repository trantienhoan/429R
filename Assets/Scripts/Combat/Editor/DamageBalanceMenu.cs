using System.Collections.Generic;
using System.Text;
using Game.Combat;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Sets the Damage (and for new ones the Power) of every weapon and throwable, so they hit by what they are: a
    /// fist's best hit does 5, a hammer's 8, a heavy throw (barrel, chair) 6, a medium one (wood box, pumpkin) 4, a light
    /// one (book, paper box, candy) 2, a bottle 2 plus its blast. Throwables fly at 15-40 m/s (their Throw Velocity
    /// Scale), far past the fastest swing, so without this every throw did a fist's best.
    /// Weapons keep their Power (how easily they land big hits); things without a Weapon Power get one (Power 1).
    /// Run again after changing the table; values set by hand on a prefab are overwritten, so change them here.
    /// </summary>
    public static class DamageBalanceMenu
    {
        // Damage multipliers. A fist is 1: its best hit does 5 (Hit Settings), so these do 5 x the number at best.
        private const float Hammer = 1.6f;   // 8
        private const float Heavy = 1.2f;    // 6: broom, heavy throws
        private const float FistLike = 1f;   // 5: flip-flops, guns swung as clubs
        private const float Medium = 0.8f;   // 4
        private const float Soft = 0.6f;     // 3: the pillow
        private const float Small = 0.4f;    // 2: and bottles, which also blow up

        private static readonly (string path, float damage, float power)[] Table =
        {
            // Weapons (they keep their Power; 0 = keep).
            ("Weapon/Hammer_Tool", Hammer, 0f),
            ("Weapon/Hammer_Boong", Hammer, 0f),
            ("Weapon/Hammer_Boong 2ND", Hammer, 0f),
            ("Weapon/Hammer_Boong_Bone", Hammer, 0f),
            ("Weapon/Hammer_Broom", Heavy, 0f),
            ("Weapon/Hammer_Pillow", Soft, 0f),
            ("Weapon/Flip_Flop_L", FistLike, 0f),
            ("Weapon/Flip_Flop_R", FistLike, 0f),
            ("Weapon/Flip_Flop2_L", FistLike, 0f),
            ("Weapon/Flip_Flop2_R", FistLike, 0f),
            ("Weapon/Boong_Gun", FistLike, 0f),
            ("Weapon/Bus_Boong", FistLike, 0f),
            ("Weapon/Elon_Boong", FistLike, 0f),
            ("Items/Pie_Knight_Sword", 1.4f, 1.5f),
            // The shop's legendary sword (best hit 12) and shield (Tools > 429 Game > Weapons > Make Legendary Knight Gear).
            ("Items/Pie_Knight_Sword_Legendary", 2.4f, 2.5f),
            ("Items/Pie_Knight_Shield_Legendary", Medium, 0f),

            // Heavy throws.
            ("BedRoom/Wood_Barrel", Heavy, 0f),
            ("BedRoom/Wood_Barrel2", Heavy, 0f),
            ("PlayGround/Wood_Barrel2", Heavy, 0f),
            ("BedRoom/Livingroom_Chair", Heavy, 0f),
            ("BedRoom/Bedroom_Work_Table_Chair", Heavy, 0f),
            ("BedRoom/Bedroom_Work_Table_Chair_Metal", Heavy, 0f),
            ("BedRoom/Bedroom_Candy_Basket", Heavy, 0f),
            ("BedRoom/Bedroom_Candy_Basket_Metal", Heavy, 0f),
            ("BedRoom/Bedroom_Candy_Basket_P", Heavy, 0f),
            ("BedRoom/Livingroom_Cloth_Hanger_Tree", Heavy, 0f),
            ("PlayGround/Lolipop_Basket", Heavy, 0f),
            ("Items/Metal_Block/Jenga_Block.003", Heavy, 0f),

            // Medium throws. Pumpkins too: the tree keeps growing them, and they last a few throws.
            ("Items/Breakable/Pumpkin_On_Tree", Medium, 0f),
            ("Items/Breakable/Pumpkin_2", Medium, 0f),
            ("Items/Breakable/Pumpkin_51", Medium, 0f),
            ("Items/Breakable/pumpkin_Z", Medium, 0f),
            ("Items/Breakable/pumpkin_bag", Medium, 0f),
            ("BedRoom/Wood_Box", Medium, 0f),
            ("BedRoom/Wood_Box_Weapon_Main", Medium, 0f),
            ("BedRoom/Bedroom_Treasure_Chest", Medium, 0f),
            ("BedRoom/LivingRoom_Stool", Medium, 0f),
            ("BedRoom/Bedroom_Bed_Stool_Metal", Medium, 0f),
            ("BedRoom/Livingroom_Wood_Holder", Medium, 0f),
            ("Light/Pumpkin_Candle", Medium, 0f),
            ("Light/Pumpkin_Candle_Purple", Medium, 0f),
            ("Light/Skull_And_Candle", Medium, 0f),
            ("Items/Pie_Knight_Shield", Medium, 0f),

            // Bottles: a light hit, then the blast (their Grenade) does its own damage.
            ("Items/Breakable/Empty_Bottle_1", Small, 0f),
            ("Items/Breakable/Empty_Bottle_2", Small, 0f),
            ("Items/Breakable/Empty_Bottle_3", Small, 0f),
            ("Items/Breakable/Empty_Bottle_4", Small, 0f),
            ("Items/Breakable/Empty_Bottle_5", Small, 0f),

            // Light throws.
            ("Items/Breakable/Bedroom_Book_1", Small, 0f),
            ("Items/Breakable/Bedroom_Book_2", Small, 0f),
            ("Items/Breakable/Bedroom_Book_3", Small, 0f),
            ("Items/Breakable/Bedroom_Book_4", Small, 0f),
            ("Items/Breakable/Bedroom_Book_5", Small, 0f),
            ("Items/Breakable/Bedroom_Book_6", Small, 0f),
            ("Items/Breakable/Bedroom_Book_7", Small, 0f),
            ("Items/Breakable/Bedroom_Book_8", Small, 0f),
            ("Items/Breakable/Bedroom_Book_9", Small, 0f),
            ("Items/Breakable/Bedroom_Book_10", Small, 0f),
            ("Items/Breakable/Bedroom_Book_11", Small, 0f),
            ("Items/Breakable/Bedroom_Book_12", Small, 0f),
            ("Items/Breakable/Bedroom_Book_13", Small, 0f),
            ("Items/Breakable/Paper_Box_0", Small, 0f),
            ("Items/Breakable/Paper_Box_1", Small, 0f),
            ("Items/Breakable/Paper_Box_2", Small, 0f),
            ("Items/Breakable/Paper_Box_3", Small, 0f),
            ("Items/Breakable/Paper_Box_4", Small, 0f),
            ("Items/Breakable/Paper_Box_5", Small, 0f),
            ("Items/Breakable/Candy_1", Small, 0f),
            ("Items/Breakable/Candy_2", Small, 0f),
            ("Items/Breakable/Candy_3", Small, 0f),
            ("Items/Breakable/Candy_4", Small, 0f),
            ("Items/Breakable/Candy_5", Small, 0f),
            ("Items/Breakable/Candy_6", Small, 0f),
            ("Items/Breakable/Candy_7", Small, 0f),
            ("Items/Breakable/Candy_8", Small, 0f),
            ("Items/Breakable/Skull_2", Small, 0f),
            ("Items/Breakable/Skull_3", Small, 0f),
            ("Items/Breakable/Web_Ball_On_Web", Small, 0f),
            ("Items/Breakable/Pumpkin_00", Small, 0f),
            ("PlayGround/Lolipop_1", Small, 0f),
            ("PlayGround/Lolipop_2", Small, 0f),
            ("PlayGround/Lolipop_3", Small, 0f),
            ("PlayGround/Lolipop_4", Small, 0f),
            ("PlayGround/Lolipop_5", Small, 0f),
            ("PlayGround/Lolipop_6", Small, 0f),
            ("PlayGround/Lolipop_7", Small, 0f),
            ("PlayGround/Dropped_Bone_Fuel", Small, 0f),
            ("PlayGround/Dropped_Wood_Fuel", Small, 0f),
            ("Light/Candle_Fat", Small, 0f),
            ("Light/Candle_Land", Small, 0f),
            ("Light/Candle_Land_2", Small, 0f),
            ("Items/Broken_Stuffs/Candle_Hang_Drop", Small, 0f),
            ("BedRoom/Wood_Cup", Small, 0f),
            ("BedRoom/Bedroom_Clock", Small, 0f),
            ("Items/Hat_Black", Small, 0f),
            ("Items/Hat_Blue", Small, 0f),
            ("Items/Hat_Green", Small, 0f),
        };

        [MenuItem("Tools/429 Game/Combat/Balance Damage")]
        public static void BalanceDamage()
        {
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[Damage Balance] Close the prefab you're editing (Prefab Mode) first, then run it again.");
                return;
            }

            // Base prefabs before their variants, so a variant picks up its base's Weapon Power instead of getting a second.
            var rows = new List<(string path, float damage, float power, int depth)>();
            var missing = new List<string>();
            foreach (var (name, damage, power) in Table)
            {
                var path = $"Assets/Prefabs/{name}.prefab";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    missing.Add(name);
                    continue;
                }
                rows.Add((path, damage, power, VariantDepth(asset)));
            }
            rows.Sort((a, b) => a.depth.CompareTo(b.depth));

            var report = new StringBuilder();
            int changed = 0;
            foreach (var (path, damage, power, _) in rows)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<Rigidbody>() == null)
                    {
                        missing.Add(root.name + " (no Rigidbody on its root)");
                        continue;
                    }
                    bool added = !root.TryGetComponent(out WeaponPower weapon);
                    if (added) weapon = root.AddComponent<WeaponPower>();

                    float oldDamage = weapon.Damage, oldPower = weapon.Power;
                    weapon.Damage = damage;
                    if (power > 0f) weapon.Power = power;
                    if (!added && Mathf.Approximately(oldDamage, damage) && Mathf.Approximately(oldPower, weapon.Power)) continue;

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                    report.AppendLine($"  {root.name}: damage x{damage} (best hit {Mathf.Max(1f, Mathf.Floor(5f * damage + 0.5f))}), power {weapon.Power}{(added ? " (new)" : "")}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Damage Balance] {changed} prefab(s) changed:\n{report}" +
                      (missing.Count > 0 ? $"Not found: {string.Join(", ", missing)}" : ""));
        }

        [MenuItem("Tools/429 Game/Combat/Balance Damage", true)]
        private static bool CanBalanceDamage()
        {
            return !EditorApplication.isPlaying;
        }

        private static int VariantDepth(GameObject asset)
        {
            int depth = 0;
            for (var source = PrefabUtility.GetCorrespondingObjectFromSource(asset); source != null; source = PrefabUtility.GetCorrespondingObjectFromSource(source))
                depth++;
            return depth;
        }
    }
}
