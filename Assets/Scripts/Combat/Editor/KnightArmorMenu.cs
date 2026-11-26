using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Leveling;
using UnityEditor;
using UnityEngine;

namespace Game.Combat.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Items > Set Up Knight Armor Hunt (user 2026-11-26: "hide all Knight's armor in random place ...
    /// each part give an amount of extra armor, armor break first then health ... extra stats for that run"; the sword and
    /// shield are the legendary weapons, found to grab, never eaten for stats). Re-runnable: it makes only what's missing
    /// and never overwrites your edits:
    /// - Prefabs/Items/Knight_Armor/Knight_Armor_Helmet, _Chest, _Bracers, _Boots: the armor set's meshes
    ///   (Models/Knight_Armors, from Knight_429.blend) in its material, a soft light, a touch trigger and a Knight Armor
    ///   Pickup;
    /// - Resources/Knight Armor Settings: the 4 pieces (Helmet 15 + Toughness, Chest 25 + Max Health, Bracers 15 + Fist
    ///   Size, Boots 15 + Move Speed, 2 points each), the legendary sword and shield, the glint, puff, sounds and icon;
    /// - a Knight Armor Chest on the 5 seed piece chests (Treasure_Chest_and_Seed_*).
    /// Prefabs and assets only: nothing in the scene (the Knight Armor Hider makes itself in Play).
    /// </summary>
    public static class KnightArmorMenu
    {
        private const string Tag = "[Knight Armor]";
        private const string SettingsPath = "Assets/Resources/Knight Armor Settings.asset";
        private const string PickupFolder = "Assets/Prefabs/Items/Knight_Armor";
        private const string Items = "Assets/Prefabs/Items/";
        private const string Sfx = "Assets/Audio/SFX/";
        // The armor set (Knight_429.blend): its meshes and its material. (The old Pie_Knight_* item prefabs show a seed
        // now, not the armor.)
        private const string Models = "Assets/Models/Knight_Armors/";
        private const string ArmorMaterial = "Assets/Materials/Knight_Armour_Sword_Shield_Mat.mat";

        // id, model (Models/Knight_Armors), armor, stat, size (longest side, metres)
        private static readonly (string id, string model, float armor, Stat stat, float size)[] Pieces =
        {
            ("Helmet", "Pie_Knight_Helmet", 15f, Stat.Toughness, 0.42f),
            ("Chest", "Pie_Knight_Chest_Piece", 25f, Stat.MaxHealth, 0.42f),
            ("Bracers", "Pie_Knight_Bracer", 15f, Stat.FistSize, 0.32f),
            ("Boots", "Pie_Knight_Boots", 15f, Stat.MoveSpeed, 0.34f),
        };
        private const float Points = 2f;
        private static readonly string[] Weapons = { "Pie_Knight_Sword_Legendary", "Pie_Knight_Shield_Legendary" };
        private static readonly string[] Chests =
            { "Treasure_Chest_and_Seed_Bracer", "Treasure_Chest_and_Seed_Shield", "Treasure_Chest_and_Seed_Head",
              "Treasure_Chest_and_Seed_Chest", "Treasure_Chest_and_Seed_Boot" };

        [MenuItem("Tools/429 Game/Items/Set Up Knight Armor Hunt")]
        public static void SetUp()
        {
            var done = new List<string>();
            var pickups = new Dictionary<string, GameObject>();
            foreach (var piece in Pieces) pickups[piece.id] = MakePickup(piece.id, piece.model, piece.size, done);
            MakeSettings(pickups, done);
            MarkChests(done);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} " + (done.Count > 0 ? string.Join("; ", done) : "everything was already set up") +
                      ". Prefabs and assets only: no scene change.");
        }

        [MenuItem("Tools/429 Game/Items/Set Up Knight Armor Hunt", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // The pickup: the armor set's own mesh and material (never the old items' FSM: it sets GAMESTAGES' gear bools,
        // the rooms' progress).
        private static GameObject MakePickup(string id, string modelName, float size, List<string> done)
        {
            string path = $"{PickupFolder}/Knight_Armor_{id}.prefab";
            string source = Models + modelName + ".fbx";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // Kept as it is when it shows the armor set's mesh (made over, same file, only when it shows something else:
            // the first version of this menu copied the old items, which show a seed now).
            if (existing != null && (fbx == null || ShowsModel(existing, source))) return existing;
            if (fbx == null)
            {
                Debug.LogWarning($"{Tag} Missing {source}: no {id} pickup.");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArmorMaterial);
            if (material == null) Debug.LogWarning($"{Tag} Missing {ArmorMaterial}: the {id} keeps the model's own material.");
            Directory.CreateDirectory(PickupFolder);

            var root = new GameObject("Knight_Armor_" + id);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            foreach (var look in fbx.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = look is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : look.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                var part = new GameObject(look.name);
                part.transform.SetParent(model, false);
                // Where it sits in the model (the model's root at the origin).
                var m = fbx.transform.worldToLocalMatrix * look.transform.localToWorldMatrix;
                part.transform.localPosition = m.GetColumn(3);
                part.transform.localRotation = m.rotation;
                part.transform.localScale = m.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = material != null
                    ? Enumerable.Repeat(material, Mathf.Max(1, mesh.subMeshCount)).ToArray()
                    : look.sharedMaterials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Centred on the root, its longest side Size metres.
            var bounds = new Bounds();
            bool any = false;
            foreach (var look in model.GetComponentsInChildren<MeshRenderer>())
            {
                var b = look.bounds;
                if (!any) { bounds = b; any = true; }
                else bounds.Encapsulate(b);
            }
            if (any)
            {
                float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                float scale = longest > 0.0001f ? size / longest : 1f;
                model.localScale = Vector3.one * scale;
                model.localPosition = -bounds.center * scale;
            }

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.55f);
            glow.intensity = 1.5f;
            glow.range = 1.5f;
            glow.shadows = LightShadows.None;

            var touch = root.AddComponent<SphereCollider>();
            touch.isTrigger = true;
            touch.radius = Mathf.Max(0.25f, size * 0.7f);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var pickup = root.AddComponent<KnightArmorPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("pieceId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            done.Add("made " + path);
            return saved;
        }

        private static bool ShowsModel(GameObject prefab, string modelPath)
        {
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh) == modelPath) return true;
            return false;
        }

        private static void MakeSettings(Dictionary<string, GameObject> pickups, List<string> done)
        {
            var settings = AssetDatabase.LoadAssetAtPath<KnightArmorSettings>(SettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                settings = ScriptableObject.CreateInstance<KnightArmorSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                done.Add("made " + SettingsPath);
            }
            var so = new SerializedObject(settings);

            var pieces = so.FindProperty("pieces");
            if (pieces.arraySize == 0)
            {
                pieces.arraySize = Pieces.Length;
                for (int i = 0; i < Pieces.Length; i++)
                {
                    var p = pieces.GetArrayElementAtIndex(i);
                    p.FindPropertyRelative("id").stringValue = Pieces[i].id;
                    p.FindPropertyRelative("armor").floatValue = Pieces[i].armor;
                    p.FindPropertyRelative("stat").intValue = (int)Pieces[i].stat;
                    p.FindPropertyRelative("points").floatValue = Points;
                }
                done.Add("pieces Helmet 15 + Toughness, Chest 25 + Max Health, Bracers 15 + Fist Size, Boots 15 + Move Speed");
            }
            // Empty pickup slots are filled (a re-run after a pickup prefab was made).
            for (int i = 0; i < pieces.arraySize; i++)
            {
                var p = pieces.GetArrayElementAtIndex(i);
                var slot = p.FindPropertyRelative("pickup");
                string id = p.FindPropertyRelative("id").stringValue;
                if (slot.objectReferenceValue == null && pickups.TryGetValue(id, out var prefab) && prefab != null)
                    slot.objectReferenceValue = prefab;
            }

            FillList(so, "weapons", Weapons.Select(n => (Object)AssetDatabase.LoadAssetAtPath<GameObject>(Items + n + ".prefab")), done);
            FillOne(so, "glint", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lana Studio/Hyper Casual FX/Prefabs/Sparkle/Sparkle_ellow.prefab"), done);
            FillOne(so, "foundPuff", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab"), done);
            FillOne(so, "foundSound", Clip("A_magical_glowing.wav"), done);
            FillOne(so, "equipSound", Clip("A_magic_armored_fist.wav"), done);
            FillList(so, "hitSounds", new Object[] { Clip("metal_hit_1.wav"), Clip("metal_hit_2.wav") }, done);
            FillOne(so, "breakSound", Clip("glass_break_1.wav"), done);
            var icon = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Knight_Helmet_Bar.png").OfType<Sprite>().FirstOrDefault();
            FillOne(so, "icon", icon, done);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void MarkChests(List<string> done)
        {
            foreach (var name in Chests)
            {
                string path = Items + "Chest/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"{Tag} Missing {path} (skipped).");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<KnightArmorChest>() != null) continue;
                    root.AddComponent<KnightArmorChest>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(name + " can hold one");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static AudioClip Clip(string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + file);
            if (clip == null) Debug.LogWarning($"{Tag} Missing {Sfx}{file}.");
            return clip;
        }

        private static void FillOne(SerializedObject so, string field, Object value, List<string> done)
        {
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue != null || value == null) return;
            p.objectReferenceValue = value;
            done.Add($"{field} = {value.name}");
        }

        private static void FillList(SerializedObject so, string field, IEnumerable<Object> values, List<string> done)
        {
            var p = so.FindProperty(field);
            if (p == null || p.arraySize > 0) return;
            var list = values.Where(v => v != null).ToArray();
            if (list.Length == 0) return;
            p.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            done.Add($"{field} = {string.Join(", ", list.Select(v => v.name))}");
        }
    }
}
