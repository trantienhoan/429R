using System.Collections.Generic;
using Game.Combat;
using Game.Inventory;
using Game.Shopping;
using Game.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Menu commands for the knight's legendary sword and shield and for bullets (Tools > 429 Game > Weapons).</summary>
    public static class KnightGearSetupMenu
    {
        private const string SwordToken = "Assets/Prefabs/Items/Pie_Knight_Sword.prefab";
        private const string ShieldToken = "Assets/Prefabs/Items/Pie_Knight_Shield.prefab";
        private const string SwordPath = "Assets/Prefabs/Items/Pie_Knight_Sword_Legendary.prefab";
        private const string ShieldPath = "Assets/Prefabs/Items/Pie_Knight_Shield_Legendary.prefab";
        private const int Price = 100;
        // How often each comes up against the other legendary specials (equal). How often a legendary comes up at all is
        // the catalog's Legendary Chance (10%, 50% in the boss fight).
        private const float Chance = 1f;
        private const float BossChance = 1f;

        private const string PoofEffect = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        private const string BlockEffect = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab";
        private const string PoofSound = "Assets/Audio/SFX/bubble_pop_1.wav";
        private const string BlockSound = "Assets/Audio/SFX/metal_object_hit_1.wav";
        private const string GrowSound = "Assets/Audio/SFX/whoosh.wav";
        private const string CandyFolder = "Assets/Prefabs/Items/Breakable";

        // The legendary sword hits like nothing else: its best hit does 12 (a fist's does 5, a hammer's 8).
        private const float SwordPower = 2.5f;
        private const float SwordDamage = 2.4f;
        // A shield bash is a medium hit.
        private const float ShieldDamage = 0.8f;
        // They don't wear out; their time limit is what ends them.
        private const float Unbreakable = 99999f;

        private static readonly string[] Bullets =
        {
            "Assets/Prefabs/Weapon/Elon_Bullet.prefab",
            "Assets/Prefabs/Weapon/Bus_Boong_Bullet.prefab",
            "Assets/Prefabs/Weapon/Boong_Skull_Bullet.prefab",
        };

        /// <summary>
        /// Makes the shop's legendary sword and shield: variants of the knight's gear tokens (Pie_Knight_Sword/Shield,
        /// which the chests still use as gear pick-ups) that are real weapons. Picked up, not collected; hold the trigger
        /// to grow them to twice the size; unbreakable, but they turn into candies 210 s after they're first picked up;
        /// the sword hits hardest of all, the shield blocks. Then lists both as rare legendary special offers for 100 each.
        /// Running it again updates them.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Make Legendary Knight Gear")]
        public static void MakeLegendaryKnightGear()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[Knight Gear] Close the prefab you're editing (Prefab Mode) first, then run it again.");
                return;
            }

            var notes = new List<string>();
            if (!MakeVariant(SwordToken, SwordPath, SetUpSword, notes) | !MakeVariant(ShieldToken, ShieldPath, SetUpShield, notes))
            {
                Debug.LogError("[Knight Gear] " + string.Join("; ", notes));
                return;
            }

            var sword = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
            var shield = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPath);
            var swordItem = MakeItem(sword, "Knight Sword");
            var shieldItem = MakeItem(shield, "Knight Shield");

            var catalog = FindCatalog();
            if (catalog != null)
            {
                bool changed = catalog.EditorSetSpecial(swordItem, Price, Chance, BossChance, true) |
                              catalog.EditorSetSpecial(shieldItem, Price, Chance, BossChance, true);
                if (changed)
                {
                    EditorUtility.SetDirty(catalog);
                    notes.Add($"both are legendary special offers in '{catalog.name}' for {Price}");
                }
            }
            else notes.Add("WARNING: no shop catalog found, add them to the specials by hand");

            InventorySetupMenu.EnsureAssets();
            Selection.activeObject = sword;
            Debug.Log("[Knight Gear] Done: " + string.Join("; ", notes) + ".");
        }

        [MenuItem("Tools/429 Game/Weapons/Make Legendary Knight Gear", true)]
        private static bool CanMakeLegendaryKnightGear()
        {
            return !EditorApplication.isPlaying;
        }

        /// <summary>
        /// Adds Ignore Shooter to the guns' bullets, so a bullet appearing against its gun doesn't knock the gun out of
        /// place in the hand.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Stop Bullets Hitting Their Gun")]
        public static void StopBulletsHittingTheirGun()
        {
            var done = new List<string>();
            foreach (var path in Bullets)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<IgnoreShooter>() != null) continue;
                    root.AddComponent<IgnoreShooter>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log(done.Count > 0 ? $"[Bullets] Ignore Shooter added to {string.Join(", ", done)}." : "[Bullets] Every bullet already has Ignore Shooter.");
        }

        // A variant of the gear token, made once, then set up (again) in place.
        private static bool MakeVariant(string tokenPath, string path, System.Action<GameObject> setUp, List<string> notes)
        {
            var token = AssetDatabase.LoadAssetAtPath<GameObject>(tokenPath);
            if (token == null)
            {
                notes.Add($"'{tokenPath}' is missing");
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                // Built in a preview scene, so the open scene isn't touched.
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var copy = (GameObject)PrefabUtility.InstantiatePrefab(token, scene);
                    PrefabUtility.SaveAsPrefabAsset(copy, path);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
                notes.Add($"made '{path}'");
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SetUpCommon(root);
                setUp(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return true;
        }

        // A weapon, not a gear pick-up: grabbable, not collected on touch, falls like other things, never breaks, and its
        // trigger no longer swaps models (Legendary Gear grows it instead).
        private static void SetUpCommon(GameObject root)
        {
            root.tag = "Weapon";
            var grab = root.GetComponent<XRGrabInteractable>();
            grab.enabled = true;
            var serialized = new SerializedObject(grab);
            foreach (var events in new[] { "m_Activated", "m_Deactivated" })
            {
                var calls = serialized.FindProperty(events + ".m_PersistentCalls.m_Calls");
                for (int i = 0; i < calls.arraySize; i++)
                    calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_CallState").intValue = 0; // Off
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                // The token's "picked up = collected" FSM (it sets the GAMESTAGES gear and shrinks away).
                if (fsm.FsmName == "Rotate and Wait") fsm.enabled = false;
                if (fsm.FsmName == "Health")
                {
                    var health = fsm.FsmVariables.FindFsmFloat("health");
                    if (health != null) health.Value = Unbreakable;
                }
            }

            var body = root.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            if (!root.TryGetComponent(out LegendaryGear gear)) gear = root.AddComponent<LegendaryGear>();
            var settings = new SerializedObject(gear);
            SetArray(settings.FindProperty("candies"), Candies());
            settings.FindProperty("poofEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PoofEffect);
            settings.FindProperty("poofSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PoofSound);
            settings.FindProperty("growSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(GrowSound);
            settings.FindProperty("growSeconds").floatValue = 0.4f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // The small sword shows; the big copy's mesh and collider are switched off, leaving its glow and light to show
        // while the sword is big.
        private static void SetUpSword(GameObject root)
        {
            var big = root.transform.Find("Pie_Knight_Sword");
            big.gameObject.SetActive(false);
            foreach (var renderer in big.GetComponents<Renderer>()) renderer.enabled = false;
            foreach (var collider in big.GetComponents<Collider>()) collider.enabled = false;

            SetWeaponPower(root, SwordPower, SwordDamage);
            var settings = new SerializedObject(root.GetComponent<LegendaryGear>());
            SetArray(settings.FindProperty("showWhileBig"), new Object[] { big.gameObject });
            settings.FindProperty("blocks").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // The token shows a seed; the real shield is its big copy, shown always. Its light is on while it's big.
        private static void SetUpShield(GameObject root)
        {
            var seed = root.transform.Find("Shield");
            if (seed != null) seed.gameObject.SetActive(false);
            var shield = root.transform.Find("Pie_Knight_Shield");
            shield.gameObject.SetActive(true);
            var light = shield.Find("Point Light");

            SetWeaponPower(root, 1f, ShieldDamage);
            var settings = new SerializedObject(root.GetComponent<LegendaryGear>());
            SetArray(settings.FindProperty("showWhileBig"), light != null ? new Object[] { light.gameObject } : new Object[0]);
            settings.FindProperty("blocks").boolValue = true;
            settings.FindProperty("blockSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(BlockSound);
            settings.FindProperty("blockEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BlockEffect);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetWeaponPower(GameObject root, float power, float damage)
        {
            if (!root.TryGetComponent(out WeaponPower weapon)) weapon = root.AddComponent<WeaponPower>();
            weapon.Power = power;
            weapon.Damage = damage;
        }

        private static ItemDefinition MakeItem(GameObject prefab, string displayName)
        {
            var item = SetupUtility.GetOrCreateItemForPrefab(prefab);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static Object[] Candies()
        {
            var list = new List<Object>();
            for (int i = 1; i <= 8; i++)
            {
                var candy = AssetDatabase.LoadAssetAtPath<GameObject>($"{CandyFolder}/Candy_{i}.prefab");
                if (candy != null) list.Add(candy);
            }
            return list.ToArray();
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static ShopCatalog FindCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ShopCatalog));
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<ShopCatalog>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }
    }
}
