using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Lair > Set Up Lair Story Props (user, 2026-11-25, the storage-room lair plan): the story props,
    /// each remodelled from one of the user's own models (user: "remodel them instead of creating new", "follow my style
    /// and texture"). Re-runnable; prefabs and assets only, no scene change.
    /// - Lair_Glow_Jar: Empty_Bottle_4 (a breakable, throwable bottle) with its neck and cork widened into a mason jar
    ///   (Exports/Lair/make_lair_jar.py -> Models/Lair/Lair_Glow_Jar.fbx); its own texture, glowing green (the Fairy's
    ///   drained light): Materials/Lair/Lair_Glow_Jar_Mat on M_Textures/Lair/Lair_Glow_Jar(.png, _Glow.png), cut from the
    ///   bottle's own patch of the atlas. Still breaks and throws like the bottle.
    /// - Lair_Sipping_Pumpkin + Lair_Jar_Huddle: the runners' own body at a third of the size, its face a steady green,
    ///   four round a glowing jar.
    /// - Lair_Torn_Banner: Banner_Hang with the cloth nibbled (Exports/Lair/make_lair_banner.py).
    /// - Lair_Candle_Green: Candle_Land burning green (flame material copy, sparks, light).
    /// - Lair_Specimen_Board: Picture_Hang_5 showing M_Textures/Lair/Lair_Specimen_Card.png (our spider turning pumpkin).
    /// - LairDecor: the wall vine, bug hole, claw scratches and the nest with its chewed box.
    /// Placing: Place Storage Room Webs (wall pieces), Place Storage Room Story Props (spawn points in the scene).
    /// </summary>
    public static class LairStoryPropsMenu
    {
        private const string Tag = "[Lair Story Props]";
        private const string PrefabFolder = "Assets/Prefabs/Lair/";
        private const string MaterialFolder = "Assets/Materials/Lair/";
        private const string BottlePath = "Assets/Prefabs/Items/Breakable/Empty_Bottle_4.prefab";
        private const string JarModel = "Assets/Models/Lair/Lair_Glow_Jar.fbx";

        [MenuItem("Tools/429 Game/Lair/Set Up Lair Story Props")]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(Tag + " Stop play mode first; nothing changed.");
                return;
            }
            var done = new List<string>();
            Jar(done);
            Huddle(done);
            Banner(done);
            Candle(done);
            Specimen(done);
            LairDecor.Build(done);
            Wardrobes(done);
            AgedFurniture(done);
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + " " + (done.Count > 0 ? string.Join("; ", done) : "nothing to do") + ". No scene change.");
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Lair Story Props", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Jar(List<string> done)
        {
            var mesh = ModelMesh(JarModel, done);
            if (mesh == null) return;
            var bottle = AssetDatabase.LoadAssetAtPath<GameObject>(BottlePath);
            var look = bottle != null ? bottle.GetComponentInChildren<MeshRenderer>() : null;
            if (look == null)
            {
                Debug.LogError($"{Tag} {BottlePath} is missing; no jar.");
                return;
            }
            // The bottle's own painted patch of the atlas, cut into a small texture of the jar's own: the glass a lighter
            // green, and a glow map that lights the glass only (not the cork). The UVs stay; the material's tiling
            // frames the patch (as the portrait's does).
            var glow = MaterialCopy(look.sharedMaterial, "Lair_Glow_Jar_Mat", done);
            var atlas = look.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
            var frame = JarTextures(mesh, atlas, done);
            if (frame.HasValue)
            {
                var (baseTex, glowTex, tiling, offset) = frame.Value;
                glow.SetTexture("_BaseMap", baseTex);
                glow.SetTextureScale("_BaseMap", tiling);
                glow.SetTextureOffset("_BaseMap", offset);
                glow.SetTexture("_EmissionMap", glowTex);
                // No metallic/normal maps of the atlas on the small texture's frame.
                if (glow.HasProperty("_MetallicGlossMap")) glow.SetTexture("_MetallicGlossMap", null);
                if (glow.HasProperty("_BumpMap")) glow.SetTexture("_BumpMap", null);
                glow.SetKeyword(new UnityEngine.Rendering.LocalKeyword(glow.shader, "_METALLICSPECGLOSSMAP"), false);
                glow.SetKeyword(new UnityEngine.Rendering.LocalKeyword(glow.shader, "_NORMALMAP"), false);
                if (glow.HasProperty("_Smoothness")) glow.SetFloat("_Smoothness", 0.7f);
                if (glow.HasProperty("_Metallic")) glow.SetFloat("_Metallic", 0f);
            }
            glow.SetColor("_BaseColor", Color.white);
            glow.SetColor("_EmissionColor", new Color(1.3f, 1.3f, 1.3f, 1f));
            glow.SetKeyword(new UnityEngine.Rendering.LocalKeyword(glow.shader, "_EMISSION"), true);
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(glow);

            var root = PrefabUtility.LoadPrefabContents(BottlePath);
            try
            {
                root.name = "Lair_Glow_Jar";
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) f.sharedMesh = mesh;
                foreach (var c in root.GetComponentsInChildren<MeshCollider>(true)) c.sharedMesh = mesh;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = glow;
                Directory.CreateDirectory(PrefabFolder);
                bool had = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Lair_Glow_Jar.prefab") != null;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "Lair_Glow_Jar.prefab");
                if (!had) done.Add("made " + PrefabFolder + "Lair_Glow_Jar.prefab (from Empty_Bottle_4)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Small pumpkins drinking the Green round a jar: the laughing pumpkins' own body (pumpkin_evil, its atlas) at a
        // third of the size, the candle light of the face a steady green (the runners' face glow layer).
        private const float SippingSize = 0.36f;

        private static void Huddle(List<string> done)
        {
            var runner = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Breakable/Pumpkin_1.prefab");
            var jar = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Lair_Glow_Jar.prefab");
            var glowSettings = Game.Story.StoryPumpkinGlowSettings.Instance;
            MeshRenderer body = null;
            if (runner != null)
                foreach (var r in runner.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name.StartsWith("pumpkin")) { body = r; break; }
            if (body == null || jar == null || glowSettings == null || glowSettings.FaceMesh == null || glowSettings.FaceMaterial == null)
            {
                Debug.LogError($"{Tag} The runner's body, the jar or the pumpkin face glow is missing; no huddle.");
                return;
            }
            var face = MaterialCopy(glowSettings.FaceMaterial, "Lair_Face_Glow_Static_Mat", done);
            face.SetColor("_BaseColor", new Color(0.24f, 1.15f, 0.3f, 1f));
            EditorUtility.SetDirty(face);

            // One sipping pumpkin.
            var pumpkin = new GameObject("Lair_Sipping_Pumpkin");
            try
            {
                var look = new GameObject("Body");
                look.transform.SetParent(pumpkin.transform, false);
                // The body's pose inside the runner (its model turn and scale), at a third of the size.
                look.transform.localPosition = runner.transform.InverseTransformPoint(body.transform.position) * SippingSize;
                look.transform.localRotation = Quaternion.Inverse(runner.transform.rotation) * body.transform.rotation;
                look.transform.localScale = body.transform.lossyScale * SippingSize;
                look.AddComponent<MeshFilter>().sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
                var r = look.AddComponent<MeshRenderer>();
                r.sharedMaterials = body.sharedMaterials;
                var glowFace = new GameObject("Face_Glow");
                glowFace.transform.SetParent(look.transform, false);
                glowFace.AddComponent<MeshFilter>().sharedMesh = glowSettings.FaceMesh;
                var fr = glowFace.AddComponent<MeshRenderer>();
                fr.sharedMaterial = face;
                fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var bounds = r.bounds;
                var ball = pumpkin.AddComponent<SphereCollider>();
                ball.center = pumpkin.transform.InverseTransformPoint(bounds.center);
                ball.radius = bounds.extents.y;   // sits on the floor (the body is wider than tall)
                var rb = pumpkin.AddComponent<Rigidbody>();
                rb.mass = 3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                bool had = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Lair_Sipping_Pumpkin.prefab") != null;
                PrefabUtility.SaveAsPrefabAsset(pumpkin, PrefabFolder + "Lair_Sipping_Pumpkin.prefab");
                if (!had) done.Add("made " + PrefabFolder + "Lair_Sipping_Pumpkin.prefab");
            }
            finally
            {
                Object.DestroyImmediate(pumpkin);
            }

            // The huddle: a glowing jar with four of them round it, each facing the jar.
            var sipping = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Lair_Sipping_Pumpkin.prefab");
            var huddle = new GameObject("Lair_Jar_Huddle");
            try
            {
                var j = (GameObject)PrefabUtility.InstantiatePrefab(jar, huddle.transform);
                j.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                float[] angles = { 20f, 105f, 200f, 290f };
                float[] sizes = { 1f, 0.85f, 1.1f, 0.92f };
                for (int i = 0; i < angles.Length; i++)
                {
                    var p = (GameObject)PrefabUtility.InstantiatePrefab(sipping, huddle.transform);
                    var dir = Quaternion.Euler(0f, angles[i], 0f) * Vector3.forward;
                    p.transform.localPosition = dir * 0.27f + Vector3.up * 0.01f;
                    p.transform.localRotation = Quaternion.LookRotation(-dir, Vector3.up);
                    p.transform.localScale = Vector3.one * sizes[i];
                }
                bool had = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Lair_Jar_Huddle.prefab") != null;
                PrefabUtility.SaveAsPrefabAsset(huddle, PrefabFolder + "Lair_Jar_Huddle.prefab");
                if (!had) done.Add("made " + PrefabFolder + "Lair_Jar_Huddle.prefab");
            }
            finally
            {
                Object.DestroyImmediate(huddle);
            }
        }

        // The clover banner nibbled by the bugs: Banner_Hang with the cut model (Exports/Lair/make_lair_banner.py), the
        // same atlas, collider and wall hanging.
        private static void Banner(List<string> done)
        {
            const string source = "Assets/Prefabs/Items/Deco/Banner_Hang.prefab";
            var mesh = ModelMesh("Assets/Models/Lair/Lair_Torn_Banner.fbx", done);
            if (mesh == null || AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
            {
                Debug.LogError($"{Tag} Banner_Hang or Models/Lair/Lair_Torn_Banner.fbx is missing; no torn banner.");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                root.name = "Lair_Torn_Banner";
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) f.sharedMesh = mesh;
                SavePrefab(root, "Lair_Torn_Banner", done);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // A candle burning green (the Fairy's stolen light): Candle_Land with its flame, sparks and light turned green; it
        // still lights, breaks and swings like the candle.
        private static void Candle(List<string> done) => GreenCandle("Assets/Prefabs/Light/Candle_Land.prefab", "Lair_Candle_Green", done);

        // A green-burning copy of a candle prefab (its flame, sparks and light green; everything else as it is). Also used
        // for the lair rooms' hanging candles (Lair_Candle_Hang_Green, from Candle_Hang: Candle Items Menu).
        internal static void GreenCandle(string source, string name, List<string> done)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
            {
                Debug.LogError($"{Tag} {source} is missing; no {name}.");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                root.name = name;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps.name.Contains("Smoke")) continue;
                    var main = ps.main;
                    main.startColor = Green(main.startColor);
                    var over = ps.colorOverLifetime;
                    if (over.enabled) over.color = Green(over.color);
                    // The flame's material adds its own orange-red colour and glow: a green copy of it.
                    var look = ps.GetComponent<ParticleSystemRenderer>();
                    var flame = look != null ? look.sharedMaterial : null;
                    if (flame == null || !flame.HasProperty("_EmissionColor")) continue;
                    var green = MaterialCopy(flame, "Lair_Candle_Flame_Green_Mat", done);
                    if (green.HasProperty("_BaseColor")) green.SetColor("_BaseColor", Green(flame.GetColor("_BaseColor")));
                    green.SetColor("_EmissionColor", Green(flame.GetColor("_EmissionColor")));
                    EditorUtility.SetDirty(green);
                    look.sharedMaterial = green;
                }
                foreach (var l in root.GetComponentsInChildren<Light>(true)) l.color = new Color(0.35f, 1f, 0.3f, 1f);
                SavePrefab(root, name, done);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Color Green(Color c)
        {
            float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return new Color(v * 0.3f, v, v * 0.22f, c.a);
        }

        private static Gradient Green(Gradient g)
        {
            if (g == null) return null;
            var keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Green(keys[i].color);
            var n = new Gradient { mode = g.mode };
            n.SetKeys(keys, g.alphaKeys);
            return n;
        }

        private static ParticleSystem.MinMaxGradient Green(ParticleSystem.MinMaxGradient g)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(Green(g.color));
                case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(Green(g.colorMin), Green(g.colorMax));
                case ParticleSystemGradientMode.Gradient: return new ParticleSystem.MinMaxGradient(Green(g.gradient));
                case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(Green(g.gradientMin), Green(g.gradientMax));
                default:
                    g.gradient = Green(g.gradient);   // RandomColor picks from its gradient
                    return g;
            }
        }

        // The specimen board: Picture_Hang_5 (the portrait's frame model, dark wood) showing the specimen card
        // (M_Textures/Lair/Lair_Specimen_Card.png, painted by Exports/Lair/paint_specimen_card.py from renders of our own
        // spider: the old look turning into the pumpkin look) on its canvas's own UVs, framed by the material's tiling.
        private const string SpecimenTexture = "Assets/M_Textures/Lair/Lair_Specimen_Card.png";

        private static void Specimen(List<string> done)
        {
            const string source = "Assets/Prefabs/Items/Deco/Picture_Hang_5.prefab";
            var card = AssetDatabase.LoadAssetAtPath<Texture2D>(SpecimenTexture);
            if (card == null || AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
            {
                Debug.LogError($"{Tag} {source} or {SpecimenTexture} is missing; no specimen board.");
                return;
            }
            if (AssetImporter.GetAtPath(SpecimenTexture) is TextureImporter importer &&
                (importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize != 512))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
            var root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                root.name = "Lair_Specimen_Board";
                var look = root.GetComponentInChildren<MeshRenderer>();
                var slots = look.sharedMaterials;
                int canvas = System.Array.FindIndex(slots, m => m != null && m.name == "Bedroom_Set_2_Mat");
                if (canvas < 0)
                {
                    Debug.LogError($"{Tag} Picture_Hang_5 has no Bedroom_Set_2_Mat canvas; no specimen board.");
                    return;
                }
                var mesh = look.GetComponent<MeshFilter>().sharedMesh;
                var uv = mesh.uv;
                float u0 = 1, u1 = 0, v0 = 1, v1 = 0;
                foreach (var i in mesh.GetTriangles(canvas))
                {
                    u0 = Mathf.Min(u0, uv[i].x); u1 = Mathf.Max(u1, uv[i].x);
                    v0 = Mathf.Min(v0, uv[i].y); v1 = Mathf.Max(v1, uv[i].y);
                }
                var mat = MaterialCopy(slots[canvas], "Lair_Specimen_Mat", done);
                var tiling = new Vector2(1f / (u1 - u0), 1f / (v1 - v0));
                mat.SetTexture("_BaseMap", card);
                mat.SetTextureScale("_BaseMap", tiling);
                mat.SetTextureOffset("_BaseMap", new Vector2(-u0 * tiling.x, -v0 * tiling.y));
                mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_EmissionMap"))
                {
                    // A little light of its own (as the portrait's), so the card reads in the dark room.
                    mat.SetTexture("_EmissionMap", card);
                    mat.SetColor("_EmissionColor", new Color(0.22f, 0.22f, 0.2f, 1f));
                    mat.SetKeyword(new UnityEngine.Rendering.LocalKeyword(mat.shader, "_EMISSION"), true);
                }
                EditorUtility.SetDirty(mat);
                slots[canvas] = mat;
                // Its frame in the aged wood, as the room's furniture (user 2026-11-25).
                var agedFrame = AgedWood(done);
                for (int i = 0; i < slots.Length; i++)
                    if (agedFrame != null && slots[i] != null && slots[i].name == "Wood_Furniture_Mat") slots[i] = agedFrame;
                look.sharedMaterials = slots;
                SavePrefab(root, "Lair_Specimen_Board", done);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The storage room's wardrobes (MiniBedroom_Shelf_01) as lair furniture (user 2026-11-25: "Aged wood + lair
        // remodel"). The wood: Lair_Wood_Aged_Mat, a copy of the shared Wood_Furniture_Mat (92 prefabs use that one,
        // so it stays): darker and redder, the grain twice as deep, a soft sheen, no metal (the original has its
        // roughness map in the Metallic slot). Tried in Play first: the panels and edges show again in the room's flat
        // light. The models (Exports/Lair/make_lair_wardrobe.py): Lair_Wardrobe with iron hinges and corner straps;
        // Lair_Wardrobe_Ajar with one door open on a dark inside full of green eyes, claw scratches on the other door
        // and an egg sac hanging from the crown. Both keep the wardrobe's box collider, health, bug horde and break
        // into Lair_Wardrobe_Drop (the same pieces in the aged wood).
        private static void Wardrobes(List<string> done)
        {
            const string source = "Assets/Prefabs/BedRoom/MiniBedroom_Shelf_01.prefab";
            const string dropSource = "Assets/Prefabs/Items/Broken_Stuffs/Shelf_01_Drop.prefab";
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wood_Furniture_Mat.mat");
            var iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bedroom_Metal_Mat.mat");
            var atlas = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bedroom_Set_1_Mat.mat");
            var glow = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Lair_Glow_Jar_Mat.mat");
            var closedMesh = ModelMesh("Assets/Models/Lair/Lair_Wardrobe.fbx", done);
            var ajarMesh = ModelMesh("Assets/Models/Lair/Lair_Wardrobe_Ajar.fbx", done);
            if (wood == null || iron == null || atlas == null || glow == null || closedMesh == null || ajarMesh == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
            {
                Debug.LogError($"{Tag} The wardrobe, its models or materials are missing; no lair wardrobes.");
                return;
            }
            var aged = AgedWood(done);

            // The broken pieces in the aged wood.
            GameObject drop = null;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(dropSource) != null)
            {
                var pieces = PrefabUtility.LoadPrefabContents(dropSource);
                try
                {
                    pieces.name = "Lair_Wardrobe_Drop";
                    foreach (var r in pieces.GetComponentsInChildren<Renderer>(true))
                    {
                        var slots = r.sharedMaterials;
                        for (int i = 0; i < slots.Length; i++) if (slots[i] == wood) slots[i] = aged;
                        r.sharedMaterials = slots;
                    }
                    SavePrefab(pieces, "Lair_Wardrobe_Drop", done);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(pieces);
                }
                drop = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Lair_Wardrobe_Drop.prefab");
            }

            foreach (var (name, mesh) in new[] { ("Lair_Wardrobe", closedMesh), ("Lair_Wardrobe_Ajar", ajarMesh) })
            {
                var root = PrefabUtility.LoadPrefabContents(source);
                try
                {
                    root.name = name;
                    root.GetComponent<MeshFilter>().sharedMesh = mesh;
                    // Submeshes in the model's slot order: wood, iron, the dark inside, the eyes.
                    var slots = new[] { aged, iron, atlas, glow };
                    var look = root.GetComponent<MeshRenderer>();
                    var use = new Material[mesh.subMeshCount];
                    for (int i = 0; i < use.Length; i++) use[i] = slots[Mathf.Min(i, slots.Length - 1)];
                    look.sharedMaterials = use;
                    // It breaks into the aged pieces.
                    if (drop != null)
                    {
                        foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                        {
                            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                            foreach (var state in fsm.Fsm.States)
                            {
                                state.LoadActions();
                                bool changed = false;
                                foreach (var action in state.Actions)
                                {
                                    if (action is HutongGames.PlayMaker.Actions.CreateObject create && create.gameObject.Value != null &&
                                        create.gameObject.Value.name == "Shelf_01_Drop")
                                    {
                                        create.gameObject.Value = drop;
                                        changed = true;
                                    }
                                }
                                if (changed) state.SaveActions();
                            }
                        }
                    }
                    if (name == "Lair_Wardrobe_Ajar") WardrobeExtras(root);
                    SavePrefab(root, name, done);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private const string FurnitureWood = "Assets/Materials/Wood_Furniture_Mat.mat";

        // Lair_Wood_Aged_Mat: a copy of the shared furniture wood (Wood_Furniture_Mat, 92 prefabs: left as it is), darker
        // and redder, the grain twice as deep, a soft sheen, no metal (the original has its roughness map in the Metallic
        // slot). The storage room's wardrobes, table, shelves and specimen board wear it.
        internal static Material AgedWood(List<string> done)
        {
            var wood = AssetDatabase.LoadAssetAtPath<Material>(FurnitureWood);
            if (wood == null) return null;
            var aged = MaterialCopy(wood, "Lair_Wood_Aged_Mat", done);
            aged.SetColor("_BaseColor", new Color(0.62f, 0.5f, 0.42f, 1f));
            if (aged.HasProperty("_BumpScale")) aged.SetFloat("_BumpScale", 2.2f);
            aged.SetTexture("_MetallicGlossMap", null);
            aged.SetKeyword(new UnityEngine.Rendering.LocalKeyword(aged.shader, "_METALLICSPECGLOSSMAP"), false);
            aged.SetFloat("_Metallic", 0f);
            aged.SetFloat("_Smoothness", 0.3f);
            if (aged.HasProperty("_EmissionColor")) aged.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(aged);
            return aged;
        }

        // The storage room's table and shelves in the aged wood (user 2026-11-25: "change the table, shelf, picture frame
        // to the same texture as the shelf"): copies, so the same table and shelf in the other rooms stay as they are.
        // Everything else is kept (the shelf still fills itself with boxes); their broken pieces are aged copies too.
        private static void AgedFurniture(List<string> done)
        {
            AgedCopy("Assets/Prefabs/BedRoom/Livingroom_Table_2_small.prefab", "Lair_Table_Small", done);
            AgedCopy("Assets/Prefabs/BedRoom/Bedroom_Stuffs_Shelf.prefab", "Lair_Stuffs_Shelf", done);
            // The chairs and the hat table (user: "help the chair with new material also. this table also").
            AgedCopy("Assets/Prefabs/BedRoom/Livingroom_Chair.prefab", "Lair_Chair", done);
            AgedCopy("Assets/Prefabs/BedRoom/Bedroom_Coffer+Magic_Hats.prefab", "Lair_Hat_Table", done);
        }

        // A copy of a prefab with the furniture wood swapped for the aged wood; any broken pieces it makes (a Create
        // Object of a Broken_Stuffs prefab in that wood) become an aged copy "<name>_Drop" too. Returns the copy.
        internal static GameObject AgedCopy(string source, string name, List<string> done)
        {
            var wood = AssetDatabase.LoadAssetAtPath<Material>(FurnitureWood);
            var aged = AgedWood(done);
            if (wood == null || aged == null || AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
            {
                Debug.LogError($"{Tag} {source} or the furniture wood is missing; no {name}.");
                return null;
            }
            var root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                root.name = name;
                int swapped = Age(root, wood, aged);
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.Fsm.States)
                    {
                        state.LoadActions();
                        bool changed = false;
                        foreach (var action in state.Actions)
                        {
                            if (!(action is HutongGames.PlayMaker.Actions.CreateObject create) || create.gameObject.Value == null) continue;
                            var made = create.gameObject.Value;
                            string madePath = AssetDatabase.GetAssetPath(made);
                            if (!madePath.Contains("/Broken_Stuffs/") || !UsesMaterial(made, wood)) continue;
                            var drop = AgedCopy(madePath, name + "_Drop", done);
                            if (drop == null) continue;
                            create.gameObject.Value = drop;
                            changed = true;
                        }
                        if (changed) state.SaveActions();
                    }
                }
                if (swapped == 0) Debug.LogWarning($"{Tag} {source} has no {wood.name}; {name} looks as before.");
                SavePrefab(root, name, done);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
        }

        // Every slot in this wood (its own renderers, not things it makes) to the aged wood; how many.
        private static int Age(GameObject root, Material wood, Material aged)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != wood) continue;
                    slots[i] = aged;
                    n++;
                }
                r.sharedMaterials = slots;
            }
            return n;
        }

        private static bool UsesMaterial(GameObject prefab, Material m)
        {
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var s in r.sharedMaterials)
                    if (s == m) return true;
            return false;
        }

        // The open wardrobe's extras (the model's front faces +X; the closed door is on +Z, the open one on -Z): claw
        // scratches on the closed door, an egg sac on a thread from the crown in front of the open door.
        private static void WardrobeExtras(GameObject root)
        {
            var scratches = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Lair_Claw_Scratches.prefab");
            if (scratches != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(scratches, root.transform);
                s.transform.localPosition = new Vector3(0.276f, 1.2f, 0.25f);
                s.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up) * Quaternion.Euler(0f, 0f, 12f);
                s.transform.localScale = Vector3.one * 0.85f;
            }
            var sac = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Lair_Web_Strand_Sac.prefab");
            if (sac != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(sac, root.transform);
                s.transform.localPosition = new Vector3(0.29f, 2.16f, -0.3f);
                s.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                s.transform.localScale = Vector3.one * 0.7f;
            }
        }

        private static void SavePrefab(GameObject root, string name, List<string> done)
        {
            Directory.CreateDirectory(PrefabFolder);
            string path = PrefabFolder + name + ".prefab";
            bool had = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            if (!had) done.Add("made " + path);
        }

        // The jar's patch of the atlas (its UVs' box, padded) as two small textures: the colour (glass lighter green,
        // the cork as painted) and the glow (green on the glass only), with the tiling and offset that frame them.
        private static (Texture2D, Texture2D, Vector2, Vector2)? JarTextures(Mesh mesh, Texture2D atlas, List<string> done)
        {
            string atlasPath = atlas != null ? AssetDatabase.GetAssetPath(atlas) : null;
            if (string.IsNullOrEmpty(atlasPath) || !File.Exists(atlasPath))
            {
                Debug.LogError($"{Tag} The bottle's texture file wasn't found; the jar keeps the atlas.");
                return null;
            }
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(atlasPath));
            int W = src.width, H = src.height;
            var uv = mesh.uv;
            float u0 = 1, u1 = 0, v0 = 1, v1 = 0;
            foreach (var p in uv) { u0 = Mathf.Min(u0, p.x); u1 = Mathf.Max(u1, p.x); v0 = Mathf.Min(v0, p.y); v1 = Mathf.Max(v1, p.y); }
            const int Pad = 8;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(u0 * W) - Pad), x1 = Mathf.Min(W, Mathf.CeilToInt(u1 * W) + Pad);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(v0 * H) - Pad), y1 = Mathf.Min(H, Mathf.CeilToInt(v1 * H) + Pad);
            int w = x1 - x0, h = y1 - y0;
            var px = src.GetPixels(x0, y0, w, h);
            Object.DestroyImmediate(src);
            var colour = new Color[px.Length];
            var light = new Color[px.Length];
            var glassGreen = new Color(0.42f, 0.86f, 0.38f);
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                bool glass = c.g > c.r * 1.12f && c.g > c.b * 1.12f;
                float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                colour[i] = glass ? Color.Lerp(c, glassGreen, 0.55f) : c;
                light[i] = glass ? new Color(0.22f, 0.75f, 0.18f) * (0.55f + 1.2f * lum) : Color.black;
                colour[i].a = light[i].a = 1f;
            }
            string folder = "Assets/M_Textures/Lair/";
            Directory.CreateDirectory(folder);
            var baseTex = SavePng(folder + "Lair_Glow_Jar.png", colour, w, h, done);
            var glowTex = SavePng(folder + "Lair_Glow_Jar_Glow.png", light, w, h, done);
            var tiling = new Vector2((float)W / w, (float)H / h);
            var offset = new Vector2(-x0 / (float)w, -y0 / (float)h);
            return (baseTex, glowTex, tiling, offset);
        }

        private static Texture2D SavePng(string path, Color[] pixels, int w, int h, List<string> done)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            bool had = File.Exists(path);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            if (!had) done.Add("made " + path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // The model's mesh, imported without materials or animation.
        internal static Mesh ModelMesh(string path, List<string> done)
        {
            if (AssetImporter.GetAtPath(path) is ModelImporter importer &&
                (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation))
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.SaveAndReimport();
                done.Add("import " + Path.GetFileNameWithoutExtension(path));
            }
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is Mesh m) return m;
            Debug.LogError($"{Tag} {path} has no mesh (run its Blender script in Exports/Lair).");
            return null;
        }

        private static Material MaterialCopy(Material source, string name, List<string> done)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = MaterialFolder + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(source) { name = name };
            AssetDatabase.CreateAsset(m, path);
            done.Add("made " + path);
            return m;
        }
    }
}
