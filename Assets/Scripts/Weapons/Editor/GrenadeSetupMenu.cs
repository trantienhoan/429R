using System.Collections.Generic;
using Game.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.EditorTools
{
    /// <summary>Menu command that turns weapons into grenades (Tools > 429 Game > Weapons).</summary>
    public static class GrenadeSetupMenu
    {
        private static readonly string[] FlipFlops =
        {
            "Assets/Prefabs/Weapon/Flip_Flop_L.prefab",
            "Assets/Prefabs/Weapon/Flip_Flop_R.prefab",
            "Assets/Prefabs/Weapon/Flip_Flop2_L.prefab",
            "Assets/Prefabs/Weapon/Flip_Flop2_R.prefab",
        };

        // A flash, a burst and a puff of smoke from the Hyper Casual FX pack read as one explosion about 3 m across.
        internal static readonly string[] Effects =
        {
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab",
        };
        internal const string Sound = "Assets/Audio/SFX/explosion-312361.wav";
        // A bottle about to go off in the hand warns with the Boong gun's overheat beep; while shaken, it rattles.
        private const string WarningSound = "Assets/Audio/SFX/elon_boong_beep_beep.wav";
        private const string ShakeSound = "Assets/Audio/SFX/poision_bottle_shake.wav";

        // Seconds shaken and the blast's reach then: small enough that a blast doesn't break a whole room at once. The
        // first step already reaches well past a plain throw's 0.4 m.
        private static readonly Vector2[] BottleCharge = { new(1f, 0.6f), new(2f, 1f), new(2.5f, 1.25f), new(3f, 1.5f) };
        // A charged bottle's damage: at the first step of the charge, and at the last (always more than a plain throw).
        // Blast damage halved for whole-number damage (user, 2026-10-27).
        private static readonly Vector2 BottleChargeDamage = new(2f, 3f); // bottles were too strong (user, 2026-10-27)

        // Each kind of exploding throw at 1 Throw Power point (each point after it: +0.08 m reach, +10% damage). Books
        // are everywhere: a small splash. Bottles a bit more (shaken, much more). A flip-flop is a weapon given up: the
        // biggest plain blast. Each reaches far enough to catch a bug next to the one it hits; the player is hurt
        // (Damage, like a bug bite) only within it. Effect sizes: the flip-flops' 0.7 (Smaller Flip-Flop Blasts), the
        // others a little smaller.
        private static readonly (string name, string folder, float radius, float damage, float push, float effectSize)[] Blasts =
        {
            ("Bedroom_Book_", "Assets/Prefabs/Items/Breakable", 0.3f, 1f, 3.2f, 0.55f),
            ("Empty_Bottle_", "Assets/Prefabs/Items/Breakable", 0.4f, 1f, 4f, 0.65f),
            ("Flip_Flop", "Assets/Prefabs/Weapon", 0.5f, 2f, 4f, 0.7f),
        };

        private static readonly string[] Bottles =
        {
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_1.prefab",
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_2.prefab",
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_3.prefab",
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_4.prefab",
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_5.prefab",
        };

        /// <summary>
        /// Makes the empty bottles shake-to-charge grenades (see Grenade): shaken 1 s they blow 0.6 m wide, 2 s 1 m,
        /// 2.5 s 1.25 m, and at 3 s they go off in the hand (1.5 m); 4 damage at the first step, 8 at the last. They
        /// rattle while shaken and beep a warning just before going off in the hand. Also has the explosion, rattle and
        /// beep sounds loaded with the scene, so they don't stutter the first time. Bottles keep their other grenade
        /// settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Make Bottles Shake To Charge")]
        public static void MakeBottlesShakeToCharge()
        {
            var beep = AssetDatabase.LoadAssetAtPath<AudioClip>(WarningSound);
            var rattle = AssetDatabase.LoadAssetAtPath<AudioClip>(ShakeSound);
            var made = new List<string>();
            foreach (var path in Bottles)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[Grenade] '{path}' doesn't exist; skipped.");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var grenade = root.GetComponent<Grenade>();
                    if (grenade == null)
                    {
                        Debug.LogWarning($"[Grenade] '{path}' isn't a grenade yet (Make Flip-Flops Explode with it selected first); skipped.");
                        continue;
                    }
                    var serialized = new SerializedObject(grenade);
                    serialized.FindProperty("shakeToCharge").boolValue = true;
                    var steps = serialized.FindProperty("charge");
                    steps.arraySize = BottleCharge.Length;
                    for (int i = 0; i < BottleCharge.Length; i++) steps.GetArrayElementAtIndex(i).vector2Value = BottleCharge[i];
                    serialized.FindProperty("chargeDamage").vector2Value = BottleChargeDamage;
                    if (serialized.FindProperty("warningSound").objectReferenceValue == null)
                        serialized.FindProperty("warningSound").objectReferenceValue = beep;
                    if (serialized.FindProperty("shakeSound").objectReferenceValue == null)
                        serialized.FindProperty("shakeSound").objectReferenceValue = rattle;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    made.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            int preloaded = PreloadSounds(Sound, WarningSound, ShakeSound);
            Debug.Log($"[Grenade] Shake to charge on: {string.Join(", ", made)}." +
                      (preloaded > 0 ? $" {preloaded} explosion sound(s) now load with the scene." : ""));
        }

        private const string GlowMaterialPath = "Assets/Data/Effects/Bottle Glow.mat";
        private const string GlowTexturePath = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";

        /// <summary>
        /// Gives each shake-to-charge bottle its glow (Charge Glow): a "Charge Glow" particle system in the middle of the
        /// bottle, in the bottle's own colour (worked out from its texture under its mesh, made brighter), invisible
        /// until it's shaken and growing as it charges. Running it again recolours and rebuilds the glow.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Add Bottle Glow")]
        public static void AddBottleGlow()
        {
            var material = GlowMaterial();
            var made = new List<string>();
            foreach (var path in Bottles)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<Grenade>() == null)
                    {
                        Debug.LogWarning($"[Grenade] '{path}' isn't a grenade; no glow.");
                        continue;
                    }
                    var body = BottleBody(root);
                    var color = BottleColor(body);
                    var glow = MakeGlow(root, body, material);
                    var charge = root.GetComponent<ChargeGlow>();
                    if (charge == null) charge = root.AddComponent<ChargeGlow>();
                    var serialized = new SerializedObject(charge);
                    serialized.FindProperty("glow").objectReferenceValue = glow;
                    serialized.FindProperty("color").colorValue = color;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    made.Add($"{root.name} #{ColorUtility.ToHtmlStringRGB(color)}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"[Grenade] Glow added: {string.Join(", ", made)}.");
        }

        // A soft round glow that adds light (the FX pack's own additive shader, which these effects already use).
        private static Material GlowMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("Mobile/Particles/Additive")) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath) };
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(GlowMaterialPath));
            AssetDatabase.CreateAsset(material, GlowMaterialPath);
            return material;
        }

        // The bottle's own look (not its smoke puffs).
        private static MeshRenderer BottleBody(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponent<MeshFilter>() != null && renderer.GetComponent<ParticleSystem>() == null) return renderer;
            }
            return null;
        }

        // The bottle's colour: its texture under its mesh, averaged by area, then brightened for a glow (same hue, more
        // saturated, full brightness; a nearly grey bottle glows white).
        private static Color BottleColor(MeshRenderer body)
        {
            if (body == null) return Color.white;
            var mesh = body.GetComponent<MeshFilter>().sharedMesh;
            var material = body.sharedMaterial;
            var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
            var tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            if (mesh == null || texture == null) return tint;

            // A readable copy of the texture.
            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, target);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            var uv = mesh.uv;
            var points = mesh.vertices;
            var triangles = mesh.triangles;
            var sum = Vector3.zero;
            float weight = 0f;
            var spots = new[] { new Vector3(1f / 3f, 1f / 3f, 1f / 3f), new Vector3(0.6f, 0.2f, 0.2f), new Vector3(0.2f, 0.6f, 0.2f), new Vector3(0.2f, 0.2f, 0.6f) };
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                float area = Vector3.Cross(points[triangles[i + 1]] - points[triangles[i]], points[triangles[i + 2]] - points[triangles[i]]).magnitude * 0.5f;
                foreach (var spot in spots)
                {
                    var at = uv[triangles[i]] * spot.x + uv[triangles[i + 1]] * spot.y + uv[triangles[i + 2]] * spot.z;
                    var c = pixels.GetPixelBilinear(at.x, at.y) * tint;
                    sum += new Vector3(c.r, c.g, c.b) * area;
                    weight += area;
                }
            }
            Object.DestroyImmediate(pixels);
            if (weight <= 0f) return tint;
            sum /= weight;
            Color.RGBToHSV(new Color(sum.x, sum.y, sum.z), out float hue, out float saturation, out _);
            float glowSaturation = saturation < 0.08f ? 0f : Mathf.Clamp01(Mathf.Max(saturation * 1.35f, 0.45f));
            return Color.HSVToRGB(hue, glowSaturation, 1f);
        }

        // The glow: a few soft round sprites round the middle of the bottle, following it, none until it's shaken
        // (Charge Glow sets their size, colour and number from the charge). Never more than a third of the view wide, so a
        // bottle held up to the face doesn't fill the headset.
        private static ParticleSystem MakeGlow(GameObject root, MeshRenderer body, Material material)
        {
            var old = root.transform.Find("Charge Glow");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Charge Glow");
            go.transform.SetParent(root.transform, false);
            var bounds = body != null ? body.bounds : new Bounds(root.transform.position, Vector3.one * 0.2f);
            go.transform.position = bounds.center;

            var glow = go.AddComponent<ParticleSystem>();
            glow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = glow.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSpeed = 0f;
            main.startSize = 0.12f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 40;
            var emission = glow.emission;
            emission.rateOverTime = 0f;
            var shape = glow.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.01f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.3f);
            var fade = glow.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var grow = glow.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.75f, 1f, 1.15f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 0.33f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return glow;
        }

        /// <summary>
        /// Makes the books (Bedroom_Book_*) small grenades: thrown hard at something (8 m/s, like a flip-flop) they blow
        /// up (the book's numbers in Blasts: 0.3 m reach, 1 damage, 3.2 push, effects at 0.55; sound at 0.8) and
        /// break into their pages. Running it again sets them to these numbers again.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Make Books Explode")]
        public static void MakeBooksExplode()
        {
            const float power = 0.8f;
            var book = Blasts[0];
            var effects = new List<GameObject>();
            foreach (var path in Effects)
            {
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (effect != null) effects.Add(effect);
            }
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(Sound);
            var made = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("Bedroom_Book_ t:Prefab", new[] { "Assets/Prefabs/Items/Breakable" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileName(path).StartsWith("Bedroom_Book_")) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<XRGrabInteractable>() == null) continue;
                    var grenade = root.GetComponent<Grenade>();
                    if (grenade == null) grenade = root.AddComponent<Grenade>();
                    var serialized = new SerializedObject(grenade);
                    serialized.FindProperty("hardThrowHandSpeed").floatValue = 3.5f;
                    serialized.FindProperty("radius").floatValue = book.radius;
                    serialized.FindProperty("damage").floatValue = book.damage;
                    serialized.FindProperty("push").floatValue = book.push;
                    serialized.FindProperty("playerEvent").stringValue = "Damage";
                    serialized.FindProperty("breakApart").boolValue = true;
                    serialized.FindProperty("shakeToCharge").boolValue = false;
                    var list = serialized.FindProperty("effects");
                    list.arraySize = effects.Count;
                    for (int i = 0; i < effects.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
                    serialized.FindProperty("effectSize").floatValue = book.effectSize;
                    serialized.FindProperty("sound").objectReferenceValue = sound;
                    serialized.FindProperty("volume").floatValue = power;
                    serialized.FindProperty("rumbleStrength").floatValue = power;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    made.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"[Grenade] {made.Count} book(s) now explode when thrown: {string.Join(", ", made)}.");
        }

        /// <summary>
        /// Sets every exploding throw's blast to its numbers in Blasts (reach, damage, push, effect size: books, bottles,
        /// flip-flops), and the bottles' charge (Bottle Charge, Bottle Charge Damage). Their other settings are kept.
        /// Running it again sets them to these numbers again.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Balance Explosive Throws")]
        public static void BalanceExplosiveThrows()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            var made = new List<string>();
            foreach (var blast in Blasts)
            {
                foreach (var guid in AssetDatabase.FindAssets(blast.name + " t:Prefab", new[] { blast.folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!System.IO.Path.GetFileName(path).StartsWith(blast.name)) continue;
                    if (stage != null && stage.assetPath == path)
                    {
                        Debug.LogError($"[Grenade] {path} is open in Prefab Mode: close it and run this again. It was skipped.");
                        continue;
                    }
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var grenade = root.GetComponent<Grenade>();
                        if (grenade == null) continue;
                        var serialized = new SerializedObject(grenade);
                        serialized.FindProperty("radius").floatValue = blast.radius;
                        serialized.FindProperty("damage").floatValue = blast.damage;
                        serialized.FindProperty("push").floatValue = blast.push;
                        serialized.FindProperty("effectSize").floatValue = blast.effectSize;
                        if (serialized.FindProperty("shakeToCharge").boolValue)
                        {
                            var steps = serialized.FindProperty("charge");
                            steps.arraySize = BottleCharge.Length;
                            for (int i = 0; i < BottleCharge.Length; i++) steps.GetArrayElementAtIndex(i).vector2Value = BottleCharge[i];
                            serialized.FindProperty("chargeDamage").vector2Value = BottleChargeDamage;
                        }
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        made.Add(root.name);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
            Debug.Log($"[Grenade] {made.Count} exploding throw(s) balanced: {string.Join(", ", made)}.");
        }

        /// <summary>
        /// Has these sounds loaded with the scene (Preload Audio Data), so the first time one plays the game doesn't
        /// stop to load it. Returns how many changed.
        /// </summary>
        internal static int PreloadSounds(params string[] paths)
        {
            int changed = 0;
            foreach (var path in paths)
            {
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                var settings = importer.defaultSampleSettings;
                if (settings.preloadAudioData) continue;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                changed++;
            }
            return changed;
        }

        /// <summary>
        /// Adds Grenade (with the explosion effects and sound) and Hit Rumble to the selected weapon prefabs, or with
        /// none selected, to the four flip-flops. Prefabs that are already grenades keep their settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Make Flip-Flops Explode")]
        public static void MakeExplosive()
        {
            var paths = HitRumbleSetupMenu.SelectedPrefabPaths();
            bool fromSelection = paths.Count > 0;
            if (!fromSelection) paths.AddRange(FlipFlops);

            var effects = new List<GameObject>();
            foreach (var path in Effects)
            {
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (effect != null) effects.Add(effect);
                else Debug.LogWarning($"[Grenade] The explosion effect '{path}' is missing; left out.");
            }
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(Sound);
            if (sound == null) Debug.LogWarning($"[Grenade] The explosion sound '{Sound}' is missing; grenades will be silent.");

            var made = new List<string>();
            int skipped = 0;
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[Grenade] '{path}' doesn't exist; skipped.");
                    skipped++;
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Only things you can throw; weapons that already are grenades keep their settings.
                    if (root.GetComponent<XRGrabInteractable>() == null || root.GetComponent<Grenade>() != null)
                    {
                        skipped++;
                        continue;
                    }

                    var serialized = new SerializedObject(root.AddComponent<Grenade>());
                    var list = serialized.FindProperty("effects");
                    list.arraySize = effects.Count;
                    for (int i = 0; i < effects.Count; i++)
                        list.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
                    serialized.FindProperty("sound").objectReferenceValue = sound;
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    // Held, it buzzes your hand when it hits things too.
                    if (root.GetComponent<HitRumble>() == null) root.AddComponent<HitRumble>();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    made.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            string where = fromSelection ? "the selected prefabs" : "the flip-flops";
            Debug.Log(made.Count > 0
                ? $"[Grenade] {made.Count} weapon(s) now explode when thrown: {string.Join(", ", made)}. {skipped} skipped (not grabbable, or already grenades)."
                : $"[Grenade] Nothing to change in {where}: {skipped} prefab(s) are not grabbable or already grenades.");
        }
    }
}
