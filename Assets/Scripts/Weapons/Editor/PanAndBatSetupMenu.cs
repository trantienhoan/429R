using System.Collections.Generic;
using System.Linq;
using Game.Inventory;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Weapons > Set Up Frying Pan And Baseball Bat (user, 2026-11-20: "only pan and baseball can be
    /// used. Check Broom prefab, do pan and base the same way, try different stats, I already make the broken parts").
    /// The user's models (Exports/Weapons_New/New_Weapons.blend, exported by tools/export_weapons.py) are
    /// Assets/Models/Weapons/Weapon_Frying_Pan(.fbx|_Broken.fbx) and Weapon_Baseball_Bat(...). Each weapon is a copy of
    /// Prefabs/Weapon/Hammer_Broom (grab, physics, WeaponPower, wear Health FSM, EnemyHitDamage furniture wear, smoke,
    /// sounds) with its own mesh, colliders, grip, stats and broken pieces (a copy of Broom_Broken_Part with the user's
    /// pieces). Then an item (Data/Inventory/Items, with an icon), a row in the Candy Shop and a place in Random_Weapon
    /// (what the weapon box drops). Running it again sets everything again (the prefabs and items keep their GUIDs).
    /// </summary>
    public static class PanAndBatSetupMenu
    {
        private const string BroomPath = "Assets/Prefabs/Weapon/Hammer_Broom.prefab";
        private const string BroomItemPath = "Assets/Data/Inventory/Items/Hammer_Broom.asset";
        private const string BroomBrokenPath = "Assets/Prefabs/Items/Broken_Stuffs/Broom_Broken_Part.prefab";
        private const string ShopPath = "Assets/Data/Shop/Candy Shop.asset";
        private const string RandomWeaponPath = "Assets/Prefabs/Random_Item/Random_Weapon.prefab";
        private const string WoodMat = "Assets/Materials/Wood_Furniture_Mat.mat";
        private const string MetalMat = "Assets/Materials/Bedroom_Metal_Mat.mat";

        private class Spec
        {
            public string name, display;
            public float power, damage; public int thrownBonus;
            public float health, toughness, mass;
            public bool twoHanded; public float twoHandBoost;
            public float gripY;          // the hand's spot up the handle (the user's Grip empty)
            public float headFrom;       // the head starts this high: the head's collider above, the handle's below
            public int price;
            public string[] subMaterials; // per submesh of the whole model
            // Tricks while held (user, 2026-11-20: "add the pan stun and bat knock-back tricks. Use metal_hit_1 2 for Pan")
            public float stunSeconds, knockBackTimes;
            public string[] hitSounds = new string[0];
        }

        // Stats against the broom (power 1.3, damage 1, wear health 16, two hands x1.5, 13 candies):
        // - the pan: a heavy iron hit (damage 2, power 1.7: hard hits come easier), very tough (40 wear), one hand;
        // - the bat: the hardest swing (power 2.2, damage 2; two hands x1.6 = 3), wears out a bit faster than the pan.
        private static readonly Spec[] Specs =
        {
            new Spec { name = "Weapon_Frying_Pan", display = "Frying Pan", power = 1.7f, damage = 2f, thrownBonus = 1,
                health = 40f, toughness = 6f, mass = 500f, twoHanded = false, gripY = 0.12f, headFrom = 0.29f, price = 16,
                subMaterials = new[] { WoodMat, MetalMat },
                stunSeconds = 1.6f, knockBackTimes = 0f, hitSounds = new[] { "Assets/Audio/SFX/metal_hit_1.wav", "Assets/Audio/SFX/metal_hit_2.wav" } },
            new Spec { name = "Weapon_Baseball_Bat", display = "Baseball Bat", power = 2.2f, damage = 2f, thrownBonus = 1,
                health = 24f, toughness = 5f, mass = 500f, twoHanded = true, twoHandBoost = 1.6f, gripY = 0.12f, headFrom = 0.45f, price = 18,
                subMaterials = new[] { WoodMat },
                stunSeconds = 0f, knockBackTimes = 2.5f },
        };

        [MenuItem("Tools/429 Game/Weapons/Set Up Frying Pan And Baseball Bat")]
        public static void Run()
        {
            var notes = new List<string>();
            var items = new List<ItemDefinition>();
            foreach (var s in Specs)
            {
                var broken = MakeBroken(s, notes);
                var prefab = MakeWeapon(s, broken, notes);
                var item = MakeItem(s, prefab, notes);
                if (item != null) items.Add(item);
            }
            AddToShop(items, notes);
            AddToRandomWeapon(items.Select(i => i.WorldPrefab).ToList(), notes);
            InventorySetupMenu.EnsureAssets();
            AssetDatabase.SaveAssets();
            Debug.Log("[Pan And Bat] " + string.Join(" | ", notes));
        }

        [MenuItem("Tools/429 Game/Weapons/Set Up Frying Pan And Baseball Bat", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static T CopyOnce<T>(string from, string to) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(to) == null) AssetDatabase.CopyAsset(from, to);
            return AssetDatabase.LoadAssetAtPath<T>(to);
        }

        // ---- broken pieces: the broom's (root FSM picks a crack sound, then goes; each piece falls, shrinks, goes)

        private static GameObject MakeBroken(Spec s, List<string> notes)
        {
            string path = $"Assets/Prefabs/Items/Broken_Stuffs/{s.name}_Broken_Part.prefab";
            CopyOnce<GameObject>(BroomBrokenPath, path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Weapons/{s.name}_Broken.fbx");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var template = root.transform.Cast<Transform>().FirstOrDefault(t => t.GetComponent<MeshFilter>() != null);
                var pieces = model.GetComponentsInChildren<MeshFilter>(true);
                var made = new List<GameObject>();
                foreach (var src in pieces)
                {
                    var go = Object.Instantiate(template.gameObject, root.transform);
                    go.name = src.name;
                    go.transform.localPosition = model.transform.InverseTransformPoint(src.transform.position);
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    go.GetComponent<MeshFilter>().sharedMesh = src.sharedMesh;
                    var uses = src.GetComponent<MeshRenderer>().sharedMaterials;
                    // one material per piece: metal if the piece is the pan, else wood (what the user's faces use)
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(src.name.EndsWith("break.001") ? MetalMat : WoodMat);
                    go.GetComponent<MeshRenderer>().sharedMaterials = new[] { mat };
                    var box = go.GetComponent<BoxCollider>();
                    box.center = src.sharedMesh.bounds.center;
                    box.size = Vector3.Max(src.sharedMesh.bounds.size, Vector3.one * 0.02f);
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb != null) rb.mass = 1f;
                    made.Add(go);
                }
                foreach (var old in root.transform.Cast<Transform>().Where(t => t.GetComponent<MeshFilter>() != null && !made.Contains(t.gameObject)).ToList())
                    Object.DestroyImmediate(old.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.Add($"{s.name}_Broken_Part: {made.Count} pieces");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ---- the weapon: the broom with its own look, colliders, grip and numbers

        private static GameObject MakeWeapon(Spec s, GameObject broken, List<string> notes)
        {
            string path = $"Assets/Prefabs/Weapon/{s.name}.prefab";
            CopyOnce<GameObject>(BroomPath, path);
            var mesh = AssetDatabase.LoadAllAssetsAtPath($"Assets/Models/Weapons/{s.name}.fbx").OfType<Mesh>().First();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = s.name;
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                root.GetComponent<MeshRenderer>().sharedMaterials = s.subMaterials.Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();

                // Colliders: the broom's two boxes, the first round the head, the second round the handle.
                var v = mesh.vertices;
                Bounds Fit(System.Func<Vector3, bool> keep)
                {
                    var b = new Bounds(); bool first = true;
                    foreach (var p in v) { if (!keep(p)) continue; if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
                    return b;
                }
                var boxes = root.GetComponents<BoxCollider>();
                var head = Fit(p => p.y >= s.headFrom);
                var handle = Fit(p => p.y < s.headFrom);
                boxes[0].center = head.center; boxes[0].size = Vector3.Max(head.size, Vector3.one * 0.03f);
                boxes[1].center = handle.center; boxes[1].size = Vector3.Max(handle.size, Vector3.one * 0.03f);

                // The grip: the broom's hold turned end for end (the broom's business end is down, these are up).
                var attach = root.transform.Find("Attach");
                var broomAttach = AssetDatabase.LoadAssetAtPath<GameObject>(BroomPath).transform.Find("Attach");
                attach.localPosition = new Vector3(0f, s.gripY, 0f);
                attach.localRotation = Quaternion.Euler(180f, 0f, 0f) * broomAttach.localRotation;

                // The smoke where it breaks, at the head.
                var smoke = root.transform.Find("Smoke_Blast");
                if (smoke != null) smoke.localPosition = head.center;

                // Its broken pieces.
                var spot = root.transform.Find("Broom_Broken_Part_Pos") ?? root.transform.Find(s.name + "_Broken_Part_Pos");
                spot.name = s.name + "_Broken_Part_Pos";
                foreach (var fsm in spot.GetComponents<PlayMakerFSM>())
                {
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    foreach (var st in fsm.FsmStates)
                    {
                        st.LoadActions();
                        bool edit = false;
                        foreach (var make in st.Actions.OfType<CreateObject>()) { make.gameObject.Value = broken; edit = true; }
                        if (edit) st.SaveActions();
                    }
                }

                // Numbers.
                var wp = new SerializedObject(root.GetComponent<Game.Combat.WeaponPower>());
                wp.FindProperty("power").floatValue = s.power;
                wp.FindProperty("damage").floatValue = s.damage;
                wp.FindProperty("thrownBonus").intValue = s.thrownBonus;
                wp.FindProperty("stunSeconds").floatValue = s.stunSeconds;
                wp.FindProperty("knockBackTimes").floatValue = s.knockBackTimes;
                var sounds = wp.FindProperty("hitSounds");
                sounds.arraySize = s.hitSounds.Length;
                for (int k = 0; k < s.hitSounds.Length; k++)
                    sounds.GetArrayElementAtIndex(k).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(s.hitSounds[k]);
                wp.ApplyModifiedPropertiesWithoutUndo();
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                    if (fsm.FsmName == "Health") SetFsmFloat(fsm, "health", s.health);
                var hits = new SerializedObject(root.GetComponent<Game.Combat.EnemyHitDamage>());
                hits.FindProperty("toughness").floatValue = s.toughness;
                hits.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<Rigidbody>().mass = s.mass;
                var two = root.GetComponent<Game.Weapons.TwoHandedGrip>();
                if (!s.twoHanded && two != null) Object.DestroyImmediate(two);
                else if (s.twoHanded && two != null)
                {
                    var t = new SerializedObject(two);
                    t.FindProperty("damageBoost").floatValue = s.twoHandBoost;
                    t.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.Add($"{s.name}: power {s.power}, damage {s.damage}, wear {s.health}, {(s.twoHanded ? "two hands x" + s.twoHandBoost : "one hand")}" +
                          (s.stunSeconds > 0f ? $", stun {s.stunSeconds} s" : "") + (s.knockBackTimes > 0f ? $", knock-back x{s.knockBackTimes}" : "") +
                          (s.hitSounds.Length > 0 ? $", {s.hitSounds.Length} hit sounds" : ""));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void SetFsmFloat(PlayMakerFSM fsm, string name, float value)
        {
            var so = new SerializedObject(fsm);
            var floats = so.FindProperty("fsm.variables.floatVariables");
            for (int i = 0; floats != null && i < floats.arraySize; i++)
            {
                var v = floats.GetArrayElementAtIndex(i);
                if (v.FindPropertyRelative("name").stringValue == name) v.FindPropertyRelative("value").floatValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- the item (inventory, shop), with an icon drawn from the prefab

        private static ItemDefinition MakeItem(Spec s, GameObject prefab, List<string> notes)
        {
            string path = $"Assets/Data/Inventory/Items/{s.name}.asset";
            var item = CopyOnce<ItemDefinition>(BroomItemPath, path);
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = s.name;
            so.FindProperty("displayName").stringValue = s.display;
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            so.FindProperty("icon").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            bool drawn = PrefabIconMaker.MakeNow(prefab, item);
            if (!drawn) PrefabIconMaker.Queue(prefab, item);
            notes.Add($"item {s.display}{(drawn ? " + icon" : " (icon queued)")}");
            return item;
        }

        private static void AddToShop(List<ItemDefinition> items, List<string> notes)
        {
            var shop = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ShopPath);
            var so = new SerializedObject(shop);
            var list = so.FindProperty("entries");
            foreach (var item in items)
            {
                var spec = Specs.First(x => x.name == item.name);
                int at = -1;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item) at = i;
                if (at < 0) { at = list.arraySize; list.arraySize++; }
                var e = list.GetArrayElementAtIndex(at);
                e.FindPropertyRelative("item").objectReferenceValue = item;
                e.FindPropertyRelative("price").intValue = spec.price;
                e.FindPropertyRelative("stock").intValue = 0;
                notes.Add($"shop: {spec.display} {spec.price} candies");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);
        }

        // The weapon box picks from its FSM's array variable "WeaponArray" (ArrayGetRandom uses the variable), so the
        // prefabs go into the FSM's saved variables.
        private static void AddToRandomWeapon(List<GameObject> prefabs, List<string> notes)
        {
            var root = PrefabUtility.LoadPrefabContents(RandomWeaponPath);
            try
            {
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    var so = new SerializedObject(fsm);
                    var arrays = so.FindProperty("fsm.variables.arrayVariables");
                    for (int i = 0; arrays != null && i < arrays.arraySize; i++)
                    {
                        var v = arrays.GetArrayElementAtIndex(i);
                        if (v.FindPropertyRelative("name").stringValue != "WeaponArray") continue;
                        var refs = v.FindPropertyRelative("objectReferences");
                        var have = new List<Object>();
                        for (int k = 0; k < refs.arraySize; k++) have.Add(refs.GetArrayElementAtIndex(k).objectReferenceValue);
                        foreach (var p in prefabs)
                        {
                            if (have.Contains(p)) continue;
                            refs.arraySize++;
                            refs.GetArrayElementAtIndex(refs.arraySize - 1).objectReferenceValue = p;
                            have.Add(p);
                        }
                        notes.Add($"weapon box: {refs.arraySize} weapons");
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, RandomWeaponPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
