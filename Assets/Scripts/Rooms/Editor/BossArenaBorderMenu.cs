using Game.Locomotion;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Rooms.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up Boss Arena Border (user 2026-11-26: "I want to improve the boss fight area, I
    /// want some light or can just be some particle to light up the border of that area, when Player move out of that
    /// area, they will just fall by dropping at the Fall Edge."). Re-runnable; prefab only (no scene change).
    /// On Assets/Prefabs/Bases/Boss_Area.prefab (made by GAMESTAGES "BOSS" after the rooftop has shrunk away; its floor
    /// is an invisible 15 x 15 m box) it adds "Arena Border", sized from that floor:
    /// - Border Line: a glowing band on the ground along the 4 edges (Boss Arena Line: the bonfires' Danger Glow with a
    ///   soft band picture of its own, Boss Arena Line.png),
    /// - Border Floor Glow + Border Flames: smaller copies of the corner bonfires' own ground glow and flame core,
    /// - Border Embers + Border Curtain: embers rising and a faint glowing curtain;
    ///   all four spread evenly along the edges by Boss Arena Border,
    /// - 4 Fall Edge boxes from the line outwards (70 m, past the scene's invisible ground): stepping over the line = the
    ///   usual fall (drop, 55 damage, put back); enemies can't walk out (their NavMesh gets a hole there).
    /// The root is scaled unevenly (0.687, 1.743, 0.722), so Arena Border undoes that and works in metres.
    /// Remove Boss Arena Border takes it all off again.
    /// </summary>
    public static class BossArenaBorderMenu
    {
        private const string Tag = "[Boss Arena Border]";
        private const string PrefabPath = "Assets/Prefabs/Bases/Boss_Area.prefab";
        private const string GlowPath = "Assets/Data/Effects/Danger Glow.mat";
        private const string BonfirePath = "Assets/Prefabs/Light/Danger_Flame.prefab";
        private const string LinePath = "Assets/Data/Effects/Boss Arena Line.mat";
        private const string LineTexturePath = "Assets/Data/Effects/Boss Arena Line.png";
        private const string BorderName = "Arena Border";
        private const float Reach = 70f;
        private const float LineWidth = 0.36f;

        private static readonly Color LineColor = new Color(1f, 0.55f, 0.18f, 1f);

        [MenuItem("Tools/429 Game/Enemies/Set Up Boss Arena Border")]
        public static void SetUp()
        {
            var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowPath);
            var bonfire = AssetDatabase.LoadAssetAtPath<GameObject>(BonfirePath);
            var bonfireFlame = bonfire != null ? bonfire.transform.Find("Flame Core") : null;
            var bonfireGlow = bonfire != null ? bonfire.transform.Find("Ground Glow") : null;
            if (glow == null || bonfireFlame == null || bonfireGlow == null)
            {
                Debug.LogError($"{Tag} {GlowPath} or {BonfirePath} (Flame Core, Ground Glow) is missing; nothing changed.");
                return;
            }
            var line = LineMaterial(glow);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var floor = FindFloor(root.transform);
                if (floor == null)
                {
                    Debug.LogError($"{Tag} no floor box (child 'Cube' with a Box Collider) in {PrefabPath}; nothing changed.");
                    return;
                }
                var old = root.transform.Find(BorderName);
                if (old != null) Object.DestroyImmediate(old.gameObject);

                // The floor box's corners, in metres from the root (the root's own uneven scale included).
                var min = Vector3.positiveInfinity;
                var max = Vector3.negativeInfinity;
                var c = floor.center;
                var h = floor.size * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                    var world = floor.transform.TransformPoint(corner) - root.transform.position;
                    min = Vector3.Min(min, world);
                    max = Vector3.Max(max, world);
                }
                float hx = (max.x - min.x) * 0.5f;
                float hz = (max.z - min.z) * 0.5f;
                float top = max.y;

                var scale = root.transform.localScale;
                var border = new GameObject(BorderName).transform;
                border.SetParent(root.transform, false);
                border.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                border.position = root.transform.position + new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);

                MakeLine(border, line, hx, hz, top);
                var floorGlow = CopyOf(border, "Border Floor Glow", bonfireGlow.GetComponent<ParticleSystem>(), 0.4f, 300);
                var flames = CopyOf(border, "Border Flames", bonfireFlame.GetComponent<ParticleSystem>(), 0.35f, 400);
                var embers = MakeEmbers(border, glow);
                var curtain = MakeCurtain(border, glow);
                var look = border.gameObject.AddComponent<BossArenaBorder>();
                look.halfSize = new Vector2(hx, hz);
                look.floorY = top;
                look.layers = new[]
                {
                    new BossArenaBorder.Layer { system = floorGlow, perSecond = 40f, height = 0.02f },
                    new BossArenaBorder.Layer { system = flames, perSecond = 140f, height = 0.1f },
                    new BossArenaBorder.Layer { system = embers, perSecond = 70f, height = 0.04f },
                    new BossArenaBorder.Layer { system = curtain, perSecond = 45f, height = 0.75f },
                };

                MakeEdge(border, "Fall Edge North", new Vector3(0f, top + 1.5f, (hz + Reach) * 0.5f), new Vector3(2f * Reach, 5f, Reach - hz));
                MakeEdge(border, "Fall Edge South", new Vector3(0f, top + 1.5f, -(hz + Reach) * 0.5f), new Vector3(2f * Reach, 5f, Reach - hz));
                MakeEdge(border, "Fall Edge East", new Vector3((hx + Reach) * 0.5f, top + 1.5f, 0f), new Vector3(Reach - hx, 5f, 2f * hz));
                MakeEdge(border, "Fall Edge West", new Vector3(-(hx + Reach) * 0.5f, top + 1.5f, 0f), new Vector3(Reach - hx, 5f, 2f * hz));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"{Tag} done: border {2f * hx:F2} x {2f * hz:F2} m at floor height {top:F3}, line + embers + curtain, 4 Fall Edges out to {Reach} m.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/429 Game/Enemies/Remove Boss Arena Border")]
        public static void Remove()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var old = root.transform.Find(BorderName);
                if (old == null)
                {
                    Debug.Log($"{Tag} there is no border on {PrefabPath}.");
                    return;
                }
                Object.DestroyImmediate(old.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"{Tag} removed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static BoxCollider FindFloor(Transform root)
        {
            foreach (Transform child in root)
                if (child.name == "Cube" && child.TryGetComponent(out BoxCollider box)) return box;
            return null;
        }

        // The bonfires' glow material with its own picture: a soft band, bright in the middle, the same all along, so the
        // line renderer can stretch it round the whole border (the glow's own Circle01 is clamped, it can't tile).
        private static Material LineMaterial(Material glow)
        {
            var texture = LineTexture();
            var line = AssetDatabase.LoadAssetAtPath<Material>(LinePath);
            if (line == null)
            {
                line = new Material(glow) { name = "Boss Arena Line" };
                AssetDatabase.CreateAsset(line, LinePath);
            }
            line.mainTexture = texture;
            line.mainTextureScale = Vector2.one;
            line.mainTextureOffset = Vector2.zero;
            EditorUtility.SetDirty(line);
            AssetDatabase.SaveAssets();
            return line;
        }

        private static Texture2D LineTexture()
        {
            var full = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), LineTexturePath);
            if (!System.IO.File.Exists(full))
            {
                const int w = 8, h = 64;
                var picture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                {
                    float d = ((y + 0.5f) / h - 0.5f) * 2f;   // -1 .. 1 across the band
                    float a = Mathf.Clamp01(0.7f * Mathf.Exp(-d * d * 5f) + 0.6f * Mathf.Exp(-d * d * 45f));
                    if (y == 0 || y == h - 1) a = 0f;
                    for (int x = 0; x < w; x++) picture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                System.IO.File.WriteAllBytes(full, picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
                AssetDatabase.ImportAsset(LineTexturePath);
                if (AssetImporter.GetAtPath(LineTexturePath) is TextureImporter importer)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(LineTexturePath);
        }

        // A copy of one of the bonfire's particle systems (same look), made smaller and handed to Boss Arena Border to
        // emit along the edges.
        private static ParticleSystem CopyOf(Transform border, string name, ParticleSystem source, float size, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(border, false);
            var ps = go.AddComponent<ParticleSystem>();
            EditorUtility.CopySerialized(source, ps);
            EditorUtility.CopySerialized(source.GetComponent<ParticleSystemRenderer>(), go.GetComponent<ParticleSystemRenderer>());
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            // (the size multiplier only scales the upper of two constants, so both are set)
            var start = main.startSize;
            main.startSize = start.mode == ParticleSystemCurveMode.TwoConstants
                ? new ParticleSystem.MinMaxCurve(start.constantMin * size, start.constantMax * size)
                : new ParticleSystem.MinMaxCurve(start.constant * size);
            main.maxParticles = maxParticles;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var velocity = ps.velocityOverLifetime;
            if (velocity.enabled) velocity.speedModifierMultiplier *= Mathf.Max(0.4f, size);
            var noise = ps.noise;
            if (noise.enabled) noise.strengthMultiplier *= size;
            var subs = ps.subEmitters;
            subs.enabled = false;
            var lights = ps.lights;
            lights.enabled = false;
            if (main.startLifetime.constantMin < 0.5f) main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            return ps;
        }

        private static void MakeLine(Transform border, Material material, float hx, float hz, float top)
        {
            var go = new GameObject("Border Line");
            go.transform.SetParent(border, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            float y = top + 0.03f;
            line.positionCount = 4;
            line.SetPositions(new[]
            {
                new Vector3(-hx, y, -hz), new Vector3(hx, y, -hz), new Vector3(hx, y, hz), new Vector3(-hx, y, hz),
            });
            line.widthMultiplier = LineWidth;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.startColor = LineColor;
            line.endColor = LineColor;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static ParticleSystem NewSystem(Transform border, string name, Material material, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(border, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = maxParticles;
            var emission = ps.emission;
            emission.enabled = false;   // Boss Arena Border emits them along the edges
            var shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }

        private static Gradient Fade(float inAt, float outFrom)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // Small embers rising off the line and drifting.
        private static ParticleSystem MakeEmbers(Transform border, Material glow)
        {
            var ps = NewSystem(border, "Border Embers", glow, 400);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.12f), new Color(1f, 0.78f, 0.3f));
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = Fade(0.12f, 0.6f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            return ps;
        }

        // A faint glow standing up along the line, so the edge shows from across the arena.
        private static ParticleSystem MakeCurtain(Transform border, Material glow)
        {
            var ps = NewSystem(border, "Border Curtain", glow, 300);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.7f, 1.0f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(1.4f, 2.0f);
            main.startSizeZ = 1f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.42f, 0.1f, 0.18f), new Color(1f, 0.55f, 0.15f, 0.13f));
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = Fade(0.3f, 0.7f);
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
            return ps;
        }

        // FallEdge first (its Reset makes the box a trigger on Ignore Raycast), then the size.
        private static void MakeEdge(Transform border, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(border, false);
            go.AddComponent<FallEdge>();
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = center;
            box.size = size;
            go.layer = 2;
        }
    }
}
