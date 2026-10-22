using System.Collections.Generic;
using System.IO;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Game.EditorTools.DangerFlameSetupMenu;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > VFX > Seal The Whack Barrels: the rooftop whack barrels (Wood_Barrel_1 to 8) each get a magic
    /// seal, like the locks on a big door that all have to be broken:
    /// - Barrel_Seal (in place of the old foot glow "Slot"): a slowly turning seal on the floor (rings, runes, four keyhole
    ///   marks and a star), a red glow under it, rings of light climbing the barrel and gold sparks rising;
    /// - Barrel_Seal_Break (under Wood_Barrel_Drop_Pos, which the barrel's Health FSM switches on as it breaks): the seal
    ///   flashes and blows outward, a shockwave ring, gold shards and a glass-break sound. Detach On Enable lets it outlive
    ///   the barrel.
    /// It draws the seal and ring textures (Assets/Data/Effects/Textures), makes the materials (Assets/Data/Effects/Seal
    /// *.mat) and the two prefabs (Assets/Prefabs/Light). Running it again rebuilds them; the barrels keep their instances.
    /// </summary>
    public static class BarrelSealSetupMenu
    {
        private const string TextureFolder = "Assets/Data/Effects/Textures/";
        private const string SealTexturePath = TextureFolder + "Seal_Circle.png";
        private const string RingTexturePath = TextureFolder + "Seal_Ring.png";
        private const string GlowTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string BreakSound = "Assets/Audio/SFX/glass_break_2.wav";
        private const string SealPath = "Assets/Prefabs/Light/Barrel_Seal.prefab";
        private const string BreakPath = "Assets/Prefabs/Light/Barrel_Seal_Break.prefab";
        private const string BarrelFolder = "Assets/Prefabs/BedRoom/Wood_Barrel_";
        private const int BarrelCount = 8;
        // Just above the rooftop floor (its top is 2 cm), in the barrel's own units (the barrels are scaled 1.1).
        private const float FloorLift = 0.045f;

        // Gold for the seal, red for the glow under it.
        private static readonly Color Gold = new(1f, 0.72f, 0.28f);

        [MenuItem("Tools/429 Game/VFX/Seal The Whack Barrels")]
        public static void Run()
        {
            DrawTextures();
            var sealMat = MakeMaterial("Seal Circle", SealTexturePath, true);
            var ringMat = MakeMaterial("Seal Ring", RingTexturePath, true);
            var glowMat = MakeMaterial("Danger Glow", GlowTexture, true);
            var seal = BuildSeal(sealMat, ringMat, glowMat);
            var shatter = BuildBreak(sealMat, ringMat, glowMat);
            var notes = new List<string>();
            for (int i = 1; i <= BarrelCount; i++) notes.Add(PutOnBarrel(BarrelFolder + i + ".prefab", seal, shatter));
            AssetDatabase.SaveAssets();
            Debug.Log("[Barrel Seal] built the seal prefabs; " + string.Join("; ", notes));
        }

        // ---- the lasting seal ----

        private static GameObject BuildSeal(Material sealMat, Material ringMat, Material glowMat)
        {
            var root = new GameObject("Barrel_Seal");
            try
            {
                // The seal itself: one particle that lives for good, turning slowly.
                var circle = NewSystem(Child(root, "Seal Circle"), sealMat, ParticleSystemRenderMode.HorizontalBillboard, 1);
                Forever(circle, 1.64f);
                circle.GetComponent<ParticleSystemRenderer>().sortingFudge = -5f;
                var main = circle.main;
                main.startColor = new Color(Gold.r, Gold.g, Gold.b, 0.95f);
                var spin = circle.rotationOverLifetime;
                spin.enabled = true;
                spin.z = 0.35f;

                // A red glow under it, gently pulsing (overlapping long puffs).
                var glow = NewSystem(Child(root, "Seal Glow"), glowMat, ParticleSystemRenderMode.HorizontalBillboard, 4);
                var glowMain = glow.main;
                glowMain.startLifetime = 2.4f;
                glowMain.startSize = new MinMaxCurve(1.7f, 1.9f);
                var glowRate = glow.emission;
                glowRate.rateOverTime = 1f;
                var glowShape = glow.shape;
                glowShape.enabled = false;
                Fade(glow, new[] { Key(1f, 0.22f, 0.08f, 0f), Key(1f, 0.15f, 0.05f, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
                glow.GetComponent<ParticleSystemRenderer>().sortingFudge = 5f;

                // Rings of light climbing the barrel, like a binding spell.
                var bands = NewSystem(Child(root, "Binding Rings"), ringMat, ParticleSystemRenderMode.HorizontalBillboard, 6);
                var bandMain = bands.main;
                bandMain.startLifetime = 2.6f;
                bandMain.startSize = 0.95f;
                var bandRate = bands.emission;
                bandRate.rateOverTime = 0.9f;
                var bandShape = bands.shape;
                bandShape.enabled = false;
                var climb = bands.velocityOverLifetime;
                climb.enabled = true;
                climb.space = ParticleSystemSimulationSpace.Local;
                climb.x = new MinMaxCurve(0f);
                climb.y = new MinMaxCurve(0.3f);
                climb.z = new MinMaxCurve(0f);
                Fade(bands, new[] { Key(1f, 0.85f, 0.45f, 0f), Key(Gold.r, Gold.g, Gold.b, 0.5f), Key(1f, 0.35f, 0.1f, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
                Shrink(bands, 1f, 1f, 0.92f);

                // Gold sparks rising off the seal's edge.
                var sparks = NewSystem(Child(root, "Seal Sparks"), glowMat, ParticleSystemRenderMode.Billboard, 30);
                var sparkMain = sparks.main;
                sparkMain.startLifetime = new MinMaxCurve(1.2f, 2f);
                sparkMain.startSize = new MinMaxCurve(0.025f, 0.05f);
                var sparkRate = sparks.emission;
                sparkRate.rateOverTime = 8f;
                var sparkShape = sparks.shape;
                sparkShape.enabled = true;
                sparkShape.shapeType = ParticleSystemShapeType.Circle;
                sparkShape.radius = 0.72f;
                sparkShape.radiusThickness = 0f;
                sparkShape.rotation = new Vector3(90f, 0f, 0f);
                var rise = sparks.velocityOverLifetime;
                rise.enabled = true;
                rise.space = ParticleSystemSimulationSpace.Local;
                rise.x = new MinMaxCurve(-0.05f, 0.05f);
                rise.y = new MinMaxCurve(0.3f, 0.7f);
                rise.z = new MinMaxCurve(-0.05f, 0.05f);
                Fade(sparks, new[] { Key(1f, 0.9f, 0.55f, 0f), Key(1f, 0.45f, 0.12f, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
                Wobble(sparks, 0.2f, 1.5f);

                return PrefabUtility.SaveAsPrefabAsset(root, SealPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- the seal breaking ----

        private static GameObject BuildBreak(Material sealMat, Material ringMat, Material glowMat)
        {
            var root = new GameObject("Barrel_Seal_Break");
            try
            {
                root.AddComponent<DetachOnEnable>();

                // The seal flashes white-gold and blows outward.
                var flash = NewSystem(Child(root, "Seal Flash"), sealMat, ParticleSystemRenderMode.HorizontalBillboard, 1);
                OneShot(flash, 1, 0.6f, 2.6f);
                Shrink(flash, 0.63f, 0.8f, 1f);
                Fade(flash, new[] { Key(1f, 0.95f, 0.8f, 0f), Key(Gold.r, Gold.g, Gold.b, 0.4f), Key(1f, 0.3f, 0.1f, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
                var flashSpin = flash.rotationOverLifetime;
                flashSpin.enabled = true;
                flashSpin.z = 3f;

                // A shockwave ring racing out along the floor.
                var wave = NewSystem(Child(root, "Shockwave"), ringMat, ParticleSystemRenderMode.HorizontalBillboard, 1);
                OneShot(wave, 1, 0.45f, 3.6f);
                Shrink(wave, 0.25f, 0.6f, 1f);
                Fade(wave, new[] { Key(1f, 0.95f, 0.75f, 0f), Key(1f, 0.5f, 0.15f, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

                // Gold shards bursting up and out, falling back.
                var shards = NewSystem(Child(root, "Shards"), glowMat, ParticleSystemRenderMode.Stretch, 50);
                OneShot(shards, 40, 0.8f, 0.07f);
                var shardMain = shards.main;
                shardMain.startLifetime = new MinMaxCurve(0.5f, 1.0f);
                shardMain.startSpeed = new MinMaxCurve(2.5f, 5f);
                shardMain.startSize = new MinMaxCurve(0.04f, 0.08f);
                shardMain.gravityModifier = 1.2f;
                var shardShape = shards.shape;
                shardShape.enabled = true;
                shardShape.shapeType = ParticleSystemShapeType.Hemisphere;
                shardShape.radius = 0.45f;
                shardShape.rotation = new Vector3(-90f, 0f, 0f);
                shards.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                var stretch = shards.GetComponent<ParticleSystemRenderer>();
                stretch.lengthScale = 2f;
                stretch.velocityScale = 0.04f;
                Fade(shards, new[] { Key(1f, 0.92f, 0.6f, 0f), Key(1f, 0.5f, 0.12f, 0.6f), Key(0.9f, 0.15f, 0.05f, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });

                var sound = root.AddComponent<AudioSource>();
                sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BreakSound);
                sound.playOnAwake = true;
                sound.volume = 0.8f;
                sound.spatialBlend = 1f;
                sound.minDistance = 2f;
                sound.maxDistance = 25f;

                return PrefabUtility.SaveAsPrefabAsset(root, BreakPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A particle that stays as long as the barrel: one, emitted once, living for good.
        private static void Forever(ParticleSystem ps, float size)
        {
            var main = ps.main;
            main.startLifetime = 100000f;
            main.startSize = size;
            main.prewarm = false;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, 1) });
            var shape = ps.shape;
            shape.enabled = false;
        }

        // Played once when it's switched on: Count particles at once, living Life seconds, Size big.
        private static void OneShot(ParticleSystem ps, short count, float life, float size)
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.duration = 1f;
            main.startLifetime = life;
            main.startSize = size;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, count) });
            var shape = ps.shape;
            shape.enabled = false;
        }

        // ---- the barrels ----

        private static string PutOnBarrel(string path, GameObject seal, GameObject shatter)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            string name = Path.GetFileNameWithoutExtension(path);
            if (stage != null && stage.assetPath == path) return name + " is open in Prefab Mode: skipped";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return name + " missing";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // The old foot glow goes; the seal takes its place.
                var old = root.transform.Find("Slot");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                if (root.transform.Find(seal.name) == null)
                {
                    var fresh = (GameObject)PrefabUtility.InstantiatePrefab(seal, root.transform);
                    fresh.transform.localPosition = new Vector3(0f, FloorLift, 0f);
                    fresh.transform.SetSiblingIndex(0);
                }
                var drop = root.transform.Find("Wood_Barrel_Drop_Pos");
                if (drop != null && drop.Find(shatter.name) == null)
                {
                    var burst = (GameObject)PrefabUtility.InstantiatePrefab(shatter, drop);
                    burst.transform.position = root.transform.TransformPoint(new Vector3(0f, FloorLift, 0f));
                    burst.transform.rotation = root.transform.rotation;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return name + " sealed" + (drop == null ? " (no Wood_Barrel_Drop_Pos: no break effect)" : "");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---- textures ----

        private static void DrawTextures()
        {
            Directory.CreateDirectory(TextureFolder);
            Save(SealTexturePath, DrawSeal(512));
            Save(RingTexturePath, DrawRing(256));
        }

        private static void Save(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private struct Stroke
        {
            public Vector2 A, B;
            public float Width;
        }

        // The seal: double outer ring with ticks, a band of runes with four keyhole marks, an inner ring and a six-point
        // star. The barrel covers the middle half, so the detail is in the outer band. White on clear; the material tints.
        private static Texture2D DrawSeal(int size)
        {
            var strokes = new List<Stroke>();
            var dots = new List<(Vector2 centre, float radius)>();
            var rings = new List<(float radius, float width)> { (0.965f, 0.022f), (0.905f, 0.008f), (0.70f, 0.014f), (0.665f, 0.006f) };

            // Ticks between the two outer rings.
            for (int i = 0; i < 72; i++)
            {
                var dir = Dir(i * 5f);
                strokes.Add(new Stroke { A = dir * 0.915f, B = dir * (i % 6 == 0 ? 0.955f : 0.94f), Width = 0.006f });
            }

            // Runes, with a keyhole at the four quarters.
            var random = new System.Random(429);
            for (int i = 0; i < 24; i++)
            {
                float angle = i * 15f + 7.5f;
                var n = Dir(angle);
                var t = new Vector2(-n.y, n.x);
                var centre = n * 0.80f;
                if (i % 6 == 0)
                {
                    // Keyhole: a round head and a tapering slot, in a small frame.
                    dots.Add((centre + n * 0.018f, 0.022f));
                    strokes.Add(new Stroke { A = centre + n * 0.01f, B = centre - n * 0.04f, Width = 0.022f });
                    for (int k = 0; k < 16; k++)
                    {
                        var a = centre + Rot(t, n, k * 22.5f) * 0.062f;
                        var b = centre + Rot(t, n, (k + 1) * 22.5f) * 0.062f;
                        strokes.Add(new Stroke { A = a, B = b, Width = 0.008f });
                    }
                    continue;
                }
                // A rune: 2-3 strokes between points of a 3 x 3 grid in a small box.
                int count = 2 + random.Next(2);
                for (int s = 0; s < count; s++)
                {
                    Vector2 P(int gx, int gy) => centre + t * ((gx - 1) * 0.022f) + n * ((gy - 1) * 0.04f);
                    int x0 = random.Next(3), y0 = random.Next(3), x1 = random.Next(3), y1 = random.Next(3);
                    if (x0 == x1 && y0 == y1) y1 = (y1 + 2) % 3;
                    strokes.Add(new Stroke { A = P(x0, y0), B = P(x1, y1), Width = 0.011f });
                }
            }

            // Six-point star touching the inner ring.
            for (int tri = 0; tri < 2; tri++)
            {
                for (int k = 0; k < 3; k++)
                {
                    var a = Dir(90f + tri * 60f + k * 120f) * 0.665f;
                    var b = Dir(90f + tri * 60f + (k + 1) * 120f) * 0.665f;
                    strokes.Add(new Stroke { A = a, B = b, Width = 0.01f });
                }
            }

            var pixels = new Color32[size * size];
            float pixel = 2f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) * pixel - 1f, (y + 0.5f) * pixel - 1f);
                    float r = p.magnitude;
                    float best = 0f;
                    foreach (var (radius, width) in rings) best = Mathf.Max(best, Ink(Mathf.Abs(r - radius), width, pixel));
                    if (r > 0.6f && r < 1f)
                    {
                        foreach (var s in strokes) best = Mathf.Max(best, Ink(SegmentDistance(p, s.A, s.B), s.Width, pixel));
                        foreach (var (centre, radius) in dots) best = Mathf.Max(best, Ink(Mathf.Max(0f, (p - centre).magnitude - radius), 0f, pixel));
                    }
                    else
                    {
                        // Only the star reaches inside the inner ring.
                        for (int i = strokes.Count - 6; i < strokes.Count; i++)
                            best = Mathf.Max(best, Ink(SegmentDistance(p, strokes[i].A, strokes[i].B), strokes[i].Width, pixel));
                    }
                    // A faint glow over the whole outer band.
                    if (r > 0.66f && r < 0.98f) best = Mathf.Max(best, 0.12f);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(best) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // A thin glowing ring (the binding rings and the shockwave).
        private static Texture2D DrawRing(int size)
        {
            var pixels = new Color32[size * size];
            float pixel = 2f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) * pixel - 1f, (y + 0.5f) * pixel - 1f);
                    float d = Mathf.Abs(p.magnitude - 0.88f);
                    float a = Mathf.Max(Ink(d, 0.035f, pixel), Mathf.Exp(-d / 0.04f) * 0.55f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // Solid inside the line (width), a soft edge one pixel wide, and a little glow round it.
        private static float Ink(float distance, float width, float pixel)
        {
            float edge = width * 0.5f;
            float solid = 1f - Mathf.Clamp01((distance - edge) / pixel);
            float glow = Mathf.Exp(-Mathf.Max(0f, distance - edge) / 0.012f) * 0.4f;
            return Mathf.Max(solid, glow);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            return (p - (a + ab * t)).magnitude;
        }

        private static Vector2 Dir(float degrees) => new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        // A unit direction at Degrees in the frame (tangent t, radial n).
        private static Vector2 Rot(Vector2 t, Vector2 n, float degrees) =>
            t * Mathf.Cos(degrees * Mathf.Deg2Rad) + n * Mathf.Sin(degrees * Mathf.Deg2Rad);
    }
}
