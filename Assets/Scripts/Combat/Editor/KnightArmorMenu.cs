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
    ///
    /// Tools > 429 Game > Items > Knight Armor: Rarer, Grab And Eat (user 2026-11-26: "spawn too many ... make them
    /// really rare", "Armor thing should have green aura like the sword and shield", "make the armor act like candies,
    /// let's player can grab and eat it", the sword and shield back once fixed, "even rarer than the rest of armor";
    /// her flowers out). Re-runnable; sets:
    /// - Knight Armor Settings: Room Chance 0.08, Weapon Room Chance 0.03, Chest Chance 0.05, the legendary sword and
    ///   shield as the weapons, no keepsakes;
    /// - the 4 pickups: no touch bubble; a box round the model, a candy's grab (velocity tracking, held where grabbed),
    ///   eaten at the Mouth; the legendary sword's green aura (GrowthParticles) at their size.
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
        [MenuItem("Tools/429 Game/Items/Knight Armor: Rarer, Grab And Eat", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // User 2026-11-26 (see the class notes).
        private const float RoomChance = 0.08f, WeaponRoomChance = 0.03f, ChestChance = 0.05f;
        private const string MouthTag = "Mouth";
        // The green aura: the legendary sword's (its size fits a 0.6 m thing; smaller pieces get a smaller one).
        private const string AuraSource = Items + "Pie_Knight_Sword_Legendary.prefab";
        private const string AuraName = "GrowthParticles";

        [MenuItem("Tools/429 Game/Items/Knight Armor: Rarer, Grab And Eat")]
        public static void RarerGrabAndEat()
        {
            var done = new List<string>();
            var settings = AssetDatabase.LoadAssetAtPath<KnightArmorSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError($"{Tag} No {SettingsPath}: run Set Up Knight Armor Hunt first.");
                return;
            }
            var so = new SerializedObject(settings);
            so.FindProperty("roomChance").floatValue = RoomChance;
            so.FindProperty("weaponRoomChance").floatValue = WeaponRoomChance;
            so.FindProperty("chestChance").floatValue = ChestChance;
            var weapons = Weapons.Select(n => (Object)AssetDatabase.LoadAssetAtPath<GameObject>(Items + n + ".prefab")).Where(w => w != null).ToArray();
            SetList(so.FindProperty("weapons"), weapons);
            SetList(so.FindProperty("keepsakes"), new Object[0]);
            // Not in spider webs: the 2026-11-26 run hid the legendary sword in one (9 webs looked like a room's set).
            var skip = so.FindProperty("skipNames");
            bool hasWeb = false;
            for (int i = 0; i < skip.arraySize; i++) hasWeb |= skip.GetArrayElementAtIndex(i).stringValue == "Web";
            if (!hasWeb)
            {
                skip.arraySize++;
                skip.GetArrayElementAtIndex(skip.arraySize - 1).stringValue = "Web";
                done.Add("never in webs");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            done.Add($"room chance {RoomChance}, weapon room chance {WeaponRoomChance}, chest chance {ChestChance}; " +
                     $"weapons {string.Join(", ", weapons.Select(w => w.name))}; no keepsakes (her flowers out)");

            foreach (var piece in Pieces)
            {
                string path = $"{PickupFolder}/Knight_Armor_{piece.id}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    done.Add($"no {path}");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    done.Add(root.name + ": " + MakeEdible(root, piece.size));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Rarer, grab and eat: {string.Join("; ", done)}. Prefabs and assets only: no scene change.");
        }

        private static void SetList(SerializedProperty list, Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // Like a candy: grabbed with a hand (held where it was grabbed, comes to the hand from afar), eaten at the mouth.
        // Floats (kinematic) until it is first grabbed; the pickup lets it fall after. And the green aura. Re-runnable.
        private static string MakeEdible(GameObject root, float size)
        {
            var notes = new List<string>();
            // The old touch bubble goes: a bubble this big would be eaten a hand's width from the mouth.
            foreach (var bubble in root.GetComponents<SphereCollider>())
            {
                if (!bubble.isTrigger) continue;
                Object.DestroyImmediate(bubble);
                notes.Add("touch bubble removed");
            }

            // A box round the model: what the hand grabs, what lies on the floor, what touches the mouth.
            if (ModelBounds(root, out var bounds))
            {
                if (!root.TryGetComponent(out BoxCollider box)) box = root.AddComponent<BoxCollider>();
                box.isTrigger = false;
                box.center = bounds.center;
                box.size = bounds.size;
                notes.Add($"box {bounds.size.x:F2} x {bounds.size.y:F2} x {bounds.size.z:F2} m");
            }

            if (!root.TryGetComponent(out Rigidbody body)) body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.mass = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // A candy's grab (Candy_1's XR Grab Interactable values).
            if (!root.TryGetComponent(out UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab))
                grab = root.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            var g = new SerializedObject(grab);
            g.FindProperty("m_InteractionLayers.m_Bits").intValue = 1;
            g.FindProperty("m_DistanceCalculationMode").intValue = 1;   // collider position
            g.FindProperty("m_SelectMode").intValue = 0;                // one hand
            g.FindProperty("m_MovementType").intValue = 0;              // velocity tracking
            g.FindProperty("m_UseDynamicAttach").boolValue = true;      // held where it was grabbed
            g.FindProperty("m_MatchAttachPosition").boolValue = true;
            g.FindProperty("m_MatchAttachRotation").boolValue = true;
            g.FindProperty("m_SnapToColliderVolume").boolValue = true;
            g.FindProperty("m_ReinitializeDynamicAttachEverySingleGrab").boolValue = true;
            g.FindProperty("m_AttachEaseInTime").floatValue = 0.42f;
            g.FindProperty("m_ThrowOnDetach").boolValue = true;
            g.FindProperty("m_ThrowVelocityScale").floatValue = 1.5f;
            g.FindProperty("m_ForceGravityOnDetach").boolValue = false;
            g.FindProperty("m_RetainTransformParent").boolValue = true;
            g.FindProperty("m_FarAttachMode").intValue = 1;             // a far grab brings it to the hand
            g.ApplyModifiedPropertiesWithoutUndo();
            notes.Add("grab like a candy");

            if (root.TryGetComponent(out KnightArmorPickup pickup))
            {
                var p = new SerializedObject(pickup);
                p.FindProperty("touchTag").stringValue = MouthTag;
                p.ApplyModifiedPropertiesWithoutUndo();
                notes.Add("eaten at the mouth");
            }

            // The green aura of the legendary sword, made over each run of the menu.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(AuraSource);
            var sourceAura = source != null ? source.transform.Find(AuraName) : null;
            if (sourceAura == null) notes.Add($"no {AuraName} in {AuraSource} (no aura)");
            else
            {
                var old = root.transform.Find(AuraName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var aura = Object.Instantiate(sourceAura.gameObject, root.transform, false);
                aura.name = AuraName;
                aura.transform.localPosition = Vector3.zero;
                aura.transform.localRotation = sourceAura.localRotation;
                aura.transform.localScale = sourceAura.localScale * Mathf.Clamp(size / 0.6f, 0.6f, 1f);
                aura.SetActive(true);
                notes.Add($"green aura x{aura.transform.localScale.x:F3}");
            }
            return string.Join(", ", notes);
        }

        // The model's meshes together, in the root's own space.
        private static bool ModelBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            var toRoot = root.transform.worldToLocalMatrix;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;
                var local = filter.sharedMesh.bounds;
                var m = toRoot * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any;
        }

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

            var pickup = root.AddComponent<KnightArmorPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("pieceId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            // Grabbed and eaten like a candy, in the green aura (2026-11-26).
            MakeEdible(root, size);

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
