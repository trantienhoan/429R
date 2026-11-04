using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Bonus;
using Game.Enemies;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bonus > Set Up Paint Bees Bonus: builds the Paint Bees bonus round ("Special Stage Type 2") and
    /// puts it after the Playroom (GAMESTAGES "Bonus Round PR"; the Bedroom keeps the nesting pumpkins). The bees fly to
    /// Pumpkin_Pillow_Party's beat (100 BPM). Safe to run again: it makes the parts anew (meshes, materials, textures and
    /// the five prefabs) and keeps what was tuned on Paint_Bees_Round. (One menu for the two versions built on the two
    /// PCs on 2026-11-03/04: the song, road, drips, puddles, floor splats and trails from the one, the liquid paint, the
    /// light and rainbow bees and their auras from the other.)
    /// - Paint_Bucket (Prefabs/Bonus): the pumpkin bucket (pumpkin_bucket.fbx at Bucket Size), its root at the paint's
    ///   surface; the paint (a fine disc that fills the bucket's inside at Paint Level, 429/Paint Liquid: it waves,
    ///   swirls and ripples where a hand goes in), a glow on the floor under it, paint dripping down its side and
    ///   splatting on the floor, and a puddle under it that grows.
    /// - Paint_Bee: Bee_Small's model (its three looks, its Animator) with the Paint Bee look (Bee_Paint_Base_Color /
    ///   _Emission: a light bee the round tints its colour; the rainbow bee: Bee_Rainbow_Base_Color / _Emission), an aura
    ///   (a soft glow and sparkles in its colour), a light trail, its buzzing wings (Wing Buzz) and a looping buzz.
    /// - Paint_Bees_Start: the glowing ring on the floor in the middle, the arrow over it and sparkles.
    /// - Paint_Bees_Road: the lit road the bees fly in on (runway lights, lights marching in on the beat, embers, a red
    ///   danger ring round the feet).
    /// - Paint_Bees_Round (Prefabs/Bases): the Paint Bees Round with all of the above, the song, Bonus_Stage_Sky, the
    ///   candies, the user's sounds, puff, on-beat flash, paint blobs, splats and Firework_Big.
    /// - The scene: "Bonus Round PR" plays Paint_Bees_Round; --Playroom--MiniGame--'s chest spot is switched off (no
    ///   chest: the round starts on the ring). Save the scene to keep it.
    /// </summary>
    public static class PaintBeesSetupMenu
    {
        private const string PartsFolder = "Assets/Prefabs/Bonus/";
        private const string MaterialFolder = "Assets/Materials/Paint_Bees/";
        private const string TextureFolder = "Assets/M_Textures/Paint_Bees/";
        private const string MeshPath = "Assets/Models/Bonus/Paint_Bees_Meshes.asset";
        private const string RoundPath = "Assets/Prefabs/Bases/Paint_Bees_Round.prefab";
        private const string BucketPath = PartsFolder + "Paint_Bucket.prefab";
        private const string BeePath = PartsFolder + "Paint_Bee.prefab";
        private const string StartPath = PartsFolder + "Paint_Bees_Start.prefab";
        private const string RoadPath = PartsFolder + "Paint_Bees_Road.prefab";

        private const string BucketModel = "Assets/Models/Bedroom_Stuffs/pumpkin_bucket.fbx";
        // The bucket is 67 cm across as modelled: this makes it about 44 cm, so four fit round the player.
        private const float BucketSize = 0.65f;
        // The paint's surface, a share of the way up the bucket's inside (its floor to its rim).
        private const float PaintLevel = 0.72f;
        // How high the round floats the paint (Paint Bees Round's Paint Height): where the floor glow goes under it.
        // The buckets stand on the floor (user, 2026-11-04: "set those bucket on the ground"): their paint is this high.
        private const float PaintHeight = 0.175f;

        private const string BeeSource = "Assets/Prefabs/Enemies/Enemies_Smarter/Bee_Small.prefab";
        private const string BeeMaterialSource = "Assets/Materials/Bee_Smol_Mat.mat";
        private const string BeeBaseMap = "Assets/M_Textures/Bee_Smol/Bee_Paint_Base_Color.png";
        private const string BeeEmissionMap = "Assets/M_Textures/Bee_Smol/Bee_Paint_Emission.png";
        // The rainbow bee's own look (painted every colour; not tinted).
        private const string RainbowBaseMap = "Assets/M_Textures/Bee_Smol/Bee_Rainbow_Base_Color.png";
        private const string RainbowEmissionMap = "Assets/M_Textures/Bee_Smol/Bee_Rainbow_Emission.png";
        // Import settings the bee textures copy (its Android size cap).
        private const string BeeTextureSettings = "Assets/M_Textures/Bee_Smol/Bee_Enemy_Base_Color.png";
        private const string BeeFlyClip = "Bee_Fly";
        private const string BeeModelFbx = "Assets/Models/Enemies/Bee_Smol_&_Anims.fbx";
        // A paint bee is this big (the model is ~64 cm long at 1): ~38 cm, a bit smaller than a small bee.
        private const float BeeSize = 0.6f;
        // Its body's radius at size 1 (to hit it), in metres.
        private const float BeeBodyRadius = 0.2f;

        private const string SoftTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle03.png";
        private const string RingTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle06.png";
        private const string StarTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Flare03.png";
        private const string LightTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string NoFogShader = "429/Particles/Unlit No Fog";
        // The splat with droplets for the floor (splats, puddles); the view's splat (with runs) is Paint_Splat.
        private const string FloorSplatTexture = TextureFolder + "Paint_Floor_Splat.png";

        private const string SkyPath = "Assets/Prefabs/Bases/Bonus_Stage_Sky.prefab";
        private const string CandyPath = "Assets/Prefabs/Items/Breakable/Candy_{0}.prefab";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string FireworksPath = "Assets/Prefabs/Light/Firework_Big.prefab";
        private const string FontPath = "Assets/Fonts/roboto/Roboto-Bold SDF.asset";
        private const string SongPath = "Assets/Audio/SFX/Pumpkin_Pillow_Party.wav";
        private const string FlashPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab";
        // The user's sounds (picked on the other PC, 2026-11-03).
        private static readonly string[] DipSounds = { "Assets/Audio/SFX/bubble_pop_1.wav", "Assets/Audio/SFX/pumpkin_face_splat.wav" };
        private static readonly string[] SwatSounds =
        {
            "Assets/Audio/SFX/Cartoon_bug_squash_1.wav",
            "Assets/Audio/SFX/Cartoon_bug_squash_2.wav",
            "Assets/Audio/SFX/Cartoon_bug_squash_3.wav",
        };
        private const string BucketSound = "Assets/Audio/SFX/magic_hat_spit_card.wav";
        private const string BoingSound = "Assets/Audio/SFX/bone_bonk_1.wav";
        private const string StingSound = "Assets/Audio/SFX/get_biten_1.wav";
        private const string TickSound = "Assets/Audio/Kenney Audio/kenney_interface-sounds/Audio/tick_002.ogg";
        private const string GoSound = "Assets/Audio/SFX/whoosh.wav";
        private const string RainbowSound = "Assets/Audio/SFX/A_magical_glowing.wav";
        private const string PerfectSound = "Assets/Audio/SFX/fairy_yippy.wav";
        // This PC's first picks (2026-11-04): a round that still has them gets the user's instead.
        private static readonly string[] OldPicks =
        {
            "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/slime_000.ogg", "Assets/Audio/SFX/bubble_pop_1.wav",
            "Assets/Audio/Kenney Audio/kenney_digital-audio/Audio/phaseJump1.ogg", "Assets/Audio/SFX/bug_Attack.wav",
            "Assets/Audio/Kenney Audio/kenney_digital-audio/Audio/powerUp2.ogg", "Assets/Audio/SFX/A_gentle_miracle_sound_1.wav",
        };
        private const string BuzzSound = "Assets/Audio/SFX/bee_fly.wav";

        private const string GameStagesState = "Bonus Round PR";
        private const string PlayroomStageName = "--Playroom--MiniGame--";
        private const string ChestSpotName = "Playroom_Minigame_Treasure_Pos";

        [MenuItem("Tools/429 Game/Bonus/Set Up Paint Bees Bonus")]
        private static void SetUpMenu()
        {
            var notes = SetUp();
            Debug.Log("[Paint Bees] " + string.Join("\n", notes));
            EditorUtility.DisplayDialog("Paint Bees Bonus", string.Join("\n", notes), "OK");
        }

        [MenuItem("Tools/429 Game/Bonus/Set Up Paint Bees Bonus", true)]
        private static bool CanSetUp() => !EditorApplication.isPlaying;

        /// <summary>Tools > 429 Game > Bonus > Use Paint Bees In Playroom: only the scene part (2 Game Scene open).</summary>
        [MenuItem("Tools/429 Game/Bonus/Use Paint Bees In Playroom")]
        private static void UseInPlayroom() => Debug.Log("[Paint Bees] " + string.Join("; ", HookScene()));

        /// <summary>Does the whole set-up; returns what it did.</summary>
        public static List<string> SetUp()
        {
            var notes = new List<string>();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath == RoundPath || stage.assetPath.StartsWith(PartsFolder + "Paint_")))
            {
                notes.Add($"Close {stage.assetPath} in Prefab Mode first. Nothing changed.");
                return notes;
            }
            foreach (var folder in new[] { PartsFolder, MaterialFolder, TextureFolder, Path.GetDirectoryName(MeshPath).Replace('\\', '/') + "/" })
                MakeFolder(folder);

            var meshes = MakeMeshes(out float innerRadius, out float paintY, out var bucketBounds);
            var textures = MakeTextures();
            var mats = MakeMaterials(textures);
            // The prefabs are put together in a scene of their own (not the open scene, nor a prefab open in Prefab Mode).
            build = EditorSceneManager.NewPreviewScene();
            try
            {
                notes.Add(MakeBucket(meshes, mats, innerRadius, paintY, bucketBounds));
                notes.Add(MakeBee(mats));
                notes.Add(MakeStart(meshes, mats));
                notes.Add(MakeRoad(mats));
            }
            finally { EditorSceneManager.ClosePreviewScene(build); }
            notes.Add(MakeRound(mats));
            notes.AddRange(HookScene());
            AssetDatabase.SaveAssets();
            return notes;
        }

        private static UnityEngine.SceneManagement.Scene build;

        // A new object in the build scene (children parented to it follow it there).
        private static GameObject NewRoot(string name)
        {
            var go = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, build);
            return go;
        }

        // ---------- Meshes: the paint disc and the arrow ----------

        private class Meshes
        {
            public Mesh paint;
            public Mesh arrow;
        }

        private static Meshes MakeMeshes(out float innerRadius, out float paintY, out Bounds bucketBounds)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BucketModel);
            var bucketMesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
            bucketBounds = bucketMesh.bounds;
            // The bucket's inside: its inward-facing wall (normals pointing at the middle), floor to rim.
            var verts = bucketMesh.vertices;
            var normals = bucketMesh.normals;
            var middle = bucketBounds.center;
            float floor = float.MaxValue, rim = float.MinValue;
            var wall = new List<(float y, float r)>();
            for (int i = 0; i < verts.Length; i++)
            {
                var off = new Vector3(verts[i].x - middle.x, 0f, verts[i].z - middle.z);
                var n = normals[i];
                bool inward = Vector3.Dot(new Vector3(n.x, 0f, n.z), off) < 0f && Mathf.Abs(n.y) < 0.7f;
                if (!inward || off.magnitude < 0.05f) continue;
                wall.Add((verts[i].y, off.magnitude));
                floor = Mathf.Min(floor, verts[i].y);
                rim = Mathf.Max(rim, verts[i].y);
            }
            if (wall.Count == 0) { floor = bucketBounds.min.y; rim = bucketBounds.max.y; wall.Add((rim, bucketBounds.extents.x * 0.8f)); }
            paintY = Mathf.Lerp(floor, rim, PaintLevel);
            // Inner radius at the paint: the nearest wall points round that height.
            float py = paintY;
            var near = wall.OrderBy(w => Mathf.Abs(w.y - py)).Take(Mathf.Max(8, wall.Count / 4)).ToList();
            innerRadius = near.Min(w => w.r) * 0.97f;

            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var all = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().ToList();
            var paint = all.FirstOrDefault(m => m.name == "Paint_Disc");
            var arrow = all.FirstOrDefault(m => m.name == "Paint_Bees_Arrow");
            bool fresh = asset == null;
            if (paint == null) paint = new Mesh { name = "Paint_Disc" };
            if (arrow == null) arrow = new Mesh { name = "Paint_Bees_Arrow" };
            BuildDisc(paint, 1f, 64);
            BuildArrow(arrow);
            if (fresh)
            {
                AssetDatabase.CreateAsset(paint, MeshPath);
                AssetDatabase.AddObjectToAsset(arrow, MeshPath);
            }
            else
            {
                if (!AssetDatabase.Contains(paint)) AssetDatabase.AddObjectToAsset(paint, MeshPath);
                if (!AssetDatabase.Contains(arrow)) AssetDatabase.AddObjectToAsset(arrow, MeshPath);
                EditorUtility.SetDirty(paint);
                EditorUtility.SetDirty(arrow);
            }
            AssetDatabase.SaveAssets();
            return new Meshes { paint = paint, arrow = arrow };
        }

        // A flat disc of radius 1 facing up, fine enough for the liquid shader's waves and ripples to bend it (16 rings of
        // 64: about 1 cm apart in a bucket), its middle a touch higher (a little dome of paint).
        private const int DiscRings = 16;
        private static void BuildDisc(Mesh mesh, float radius, int sides)
        {
            var v = new List<Vector3> { new(0f, 0.006f, 0f) };
            var uv = new List<Vector2> { new(0.5f, 0.5f) };
            var tris = new List<int>();
            for (int ring = 1; ring <= DiscRings; ring++)
            {
                float k = ring / (float)DiscRings;
                float r = radius * k;
                float y = 0.006f * (1f - k * k);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                    uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a) * r / radius, 0.5f + 0.5f * Mathf.Sin(a) * r / radius));
                }
            }
            for (int i = 0; i < sides; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % sides;
                tris.AddRange(new[] { 0, b, a });
            }
            for (int ring = 1; ring < DiscRings; ring++)
            {
                int inner = 1 + (ring - 1) * sides, outer = 1 + ring * sides;
                for (int i = 0; i < sides; i++)
                {
                    int a = inner + i, b = inner + (i + 1) % sides, c = outer + i, d = outer + (i + 1) % sides;
                    tris.AddRange(new[] { a, b, d, a, d, c });
                }
            }
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // A chunky arrow pointing down, tip at 0, 0.42 m tall, facing +Z (it turns to the player).
        private static void BuildArrow(Mesh mesh)
        {
            var outline = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.17f, 0.19f), new Vector2(0.065f, 0.19f), new Vector2(0.065f, 0.42f),
                new Vector2(-0.065f, 0.42f), new Vector2(-0.065f, 0.19f), new Vector2(-0.17f, 0.19f),
            };
            const float depth = 0.035f;
            var v = new List<Vector3>();
            var tris = new List<int>();
            // Front (+Z) and back: the arrow head and the shaft as convex pieces.
            int[][] pieces = { new[] { 0, 1, 6 }, new[] { 2, 3, 4, 5 } };
            foreach (float z in new[] { depth, -depth })
            {
                foreach (var piece in pieces)
                {
                    int start = v.Count;
                    foreach (int i in piece) v.Add(new Vector3(outline[i].x, outline[i].y, z));
                    for (int k = 1; k < piece.Length - 1; k++)
                    {
                        if (z > 0f) tris.AddRange(new[] { start, start + k, start + k + 1 });
                        else tris.AddRange(new[] { start, start + k + 1, start + k });
                    }
                }
            }
            // Sides.
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                int s = v.Count;
                v.Add(new Vector3(a.x, a.y, depth));
                v.Add(new Vector3(b.x, b.y, depth));
                v.Add(new Vector3(b.x, b.y, -depth));
                v.Add(new Vector3(a.x, a.y, -depth));
                tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Faces whose winding came out inward: flip the whole side ring if the first side faces in.
            var n = mesh.normals;
            int firstSide = v.Count - outline.Length * 4;
            var sideMid = (v[firstSide] + v[firstSide + 2]) * 0.5f;
            if (Vector3.Dot(n[firstSide], new Vector3(sideMid.x, sideMid.y - 0.2f, 0f)) < 0f)
            {
                for (int t = 0; t < tris.Count; t += 3)
                {
                    if (tris[t] < firstSide) continue;
                    (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
                }
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
            }
        }

        // ---------- Textures: a paint drop and a splat ----------

        private class Textures
        {
            public Texture2D drop;
            public Texture2D splat;
        }

        private static Textures MakeTextures()
        {
            return new Textures
            {
                drop = WriteTexture(TextureFolder + "Paint_Drop.png", 64, DropAlpha),
                splat = WriteTexture(TextureFolder + "Paint_Splat.png", 256, SplatAlpha),
            };
        }

        // A round drop with a bright spot top-left (white: the round tints it).
        private static Color DropAlpha(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01((0.92f - d) / 0.08f);
            float shine = Mathf.Clamp01(1f - Mathf.Sqrt((x + 0.35f) * (x + 0.35f) + (y - 0.35f) * (y - 0.35f)) / 0.3f);
            float shade = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(0.5f + 0.5f * (y - x) * 0.7f));
            float c = Mathf.Clamp01(shade + shine * 0.5f);
            return new Color(c, c, c, a);
        }

        // A splat: a lumpy blob with a few round spatters round it and drips running down.
        private static Color SplatAlpha(float x, float y)
        {
            float ang = Mathf.Atan2(y, x);
            float edge = 0.5f + 0.06f * Mathf.Sin(ang * 5f + 1.3f) + 0.05f * Mathf.Sin(ang * 9f + 0.4f) + 0.03f * Mathf.Sin(ang * 14f);
            float d = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01((edge - d) / 0.03f);
            // Spatters.
            var spots = new[] { (0.72f, 0.2f, 0.07f), (-0.66f, 0.38f, 0.05f), (0.3f, 0.74f, 0.06f), (-0.42f, -0.7f, 0.05f), (0.6f, -0.55f, 0.04f) };
            foreach (var (sx, sy, sr) in spots)
            {
                float sd = Mathf.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy));
                a = Mathf.Max(a, Mathf.Clamp01((sr - sd) / 0.02f));
            }
            // Drips: thin runs down from the blob's bottom, each ending in a round drop.
            var drips = new[] { (-0.22f, 0.045f, -0.85f), (0.08f, 0.035f, -0.72f), (0.3f, 0.04f, -0.92f) };
            foreach (var (dx, w, end) in drips)
            {
                if (y < -0.2f && y > end && Mathf.Abs(x - dx) < w) a = Mathf.Max(a, Mathf.Clamp01((w - Mathf.Abs(x - dx)) / 0.015f));
                float bd = Mathf.Sqrt((x - dx) * (x - dx) + (y - end) * (y - end));
                a = Mathf.Max(a, Mathf.Clamp01((w * 1.5f - bd) / 0.015f));
            }
            float c = Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(1f - d));
            return new Color(c, c, c, a);
        }

        private static Texture2D WriteTexture(string path, int size, System.Func<float, float, Color> paint)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size * 2f - 1f, y = (j + 0.5f) / size * 2f - 1f;
                    px[j * size + i] = paint(x, y);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Materials ----------

        private class Materials
        {
            public Material paint, glow, ripple, drop, splat, floorSplat, bee, rainbowBee, ring, sparkle, arrow, aura, trail,
                roadLight, lane, orb, danger;
        }

        private static Materials MakeMaterials(Textures textures)
        {
            var mats = new Materials();
            // The paint: liquid (429/Paint Liquid: waves, ripples, swirl, glints), its colour and glow set by the bucket.
            mats.paint = LiquidMaterial("Paint_Mat");
            mats.arrow = LitMaterial("Paint_Bees_Arrow_Mat", new Color(1f, 0.82f, 0.3f), 0.6f, true);
            mats.arrow.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.2f) * 0.9f);
            mats.glow = ParticleMaterial("Paint_Floor_Glow_Mat", SoftTexture, true, false);
            mats.ripple = ParticleMaterial("Paint_Ripple_Mat", null, true, false);
            mats.ring = ParticleMaterial("Paint_Bees_Ring_Mat", RingTexture, true, true);
            mats.ring.SetColor("_BaseColor", new Color(1f, 0.75f, 0.3f, 0.9f));
            mats.sparkle = ParticleMaterial("Paint_Bees_Sparkle_Mat", StarTexture, true, true);
            mats.aura = ParticleMaterial("Paint_Bee_Aura_Mat", SoftTexture, true, true);
            mats.trail = ParticleMaterial("Paint_Bee_Trail_Mat", null, true, true);
            mats.floorSplat = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorSplatTexture) != null
                ? ParticleMaterial("Paint_Floor_Splat_Mat", FloorSplatTexture, false, false) : mats.splat;
            // The road reaches past where the fog starts: no fog.
            mats.roadLight = ParticleMaterial("Paint_Road_Light_Mat", LightTexture, true, true);
            mats.lane = ParticleMaterial("Paint_Road_Lane_Mat", null, true, true);
            mats.orb = ParticleMaterial("Paint_Road_Orb_Mat", SoftTexture, true, true);
            mats.danger = ParticleMaterial("Paint_Road_Danger_Ring_Mat", RingTexture, true, true);
            mats.drop = ParticleMaterial("Paint_Drop_Mat", AssetDatabase.GetAssetPath(textures.drop), false, false);
            mats.splat = ParticleMaterial("Paint_Splat_Mat", AssetDatabase.GetAssetPath(textures.splat), false, false);
            mats.splat.renderQueue = (int)RenderQueue.Transparent + 50;

            // The bees: Bee_Smol_Mat's settings (normal map, gloss) with the light paint look (the round tints it), and
            // the rainbow bee's own rainbow paint.
            foreach (var path in new[] { BeeBaseMap, BeeEmissionMap, RainbowBaseMap, RainbowEmissionMap }) MatchImport(path);
            mats.bee = BeeMaterial("Paint_Bee_Mat", BeeBaseMap, BeeEmissionMap);
            mats.rainbowBee = BeeMaterial("Paint_Bee_Rainbow_Mat", RainbowBaseMap, RainbowEmissionMap);
            return mats;
        }

        private static Material BeeMaterial(string name, string baseMap, string emissionMap)
        {
            string path = MaterialFolder + name + ".mat";
            var source = AssetDatabase.LoadAssetAtPath<Material>(BeeMaterialSource);
            var bee = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (bee == null)
            {
                bee = new Material(source);
                AssetDatabase.CreateAsset(bee, path);
            }
            else bee.CopyPropertiesFromMaterial(source);
            bee.name = name;
            bee.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap));
            bee.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap));
            bee.SetColor("_BaseColor", Color.white);
            bee.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(emissionMap));
            bee.SetColor("_EmissionColor", Color.white);
            bee.EnableKeyword("_EMISSION");
            bee.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(bee);
            return bee;
        }

        // A bee texture takes the bee atlas's import settings (its size and the Android cap).
        private static void MatchImport(string path)
        {
            if (!(AssetImporter.GetAtPath(BeeTextureSettings) is TextureImporter from) || !(AssetImporter.GetAtPath(path) is TextureImporter to)) return;
            var android = from.GetPlatformTextureSettings("Android");
            var mine = to.GetPlatformTextureSettings("Android");
            if (to.maxTextureSize == from.maxTextureSize && to.sRGBTexture && mine.overridden == android.overridden && mine.maxTextureSize == android.maxTextureSize) return;
            to.maxTextureSize = from.maxTextureSize;
            to.sRGBTexture = true;
            mine.overridden = android.overridden;
            mine.maxTextureSize = android.maxTextureSize;
            mine.format = android.format;
            mine.compressionQuality = android.compressionQuality;
            to.SetPlatformTextureSettings(mine);
            to.SaveAndReimport();
        }

        // The paint's surface: 429/Paint Liquid (white: the bucket sets its colour and glow, and the ripple).
        private static Material LiquidMaterial(string name)
        {
            string path = MaterialFolder + name + ".mat";
            var shader = Shader.Find("429/Paint Liquid");
            if (shader == null) return LitMaterial(name, Color.white, 0.92f, true);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_EmissionColor", Color.black);
            m.renderQueue = -1;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LitMaterial(string name, Color colour, float smoothness, bool emissive)
        {
            string path = MaterialFolder + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // An unlit URP particle material (additive: glows; else alpha-blended), optionally free of the fog.
        private static Material ParticleMaterial(string name, string texturePath, bool additive, bool noFog)
        {
            string path = MaterialFolder + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            var fogless = noFog ? Shader.Find(NoFogShader) : null;
            m.shader = fogless != null ? fogless : Shader.Find("Universal Render Pipeline/Particles/Unlit");
            m.SetTexture("_BaseMap", string.IsNullOrEmpty(texturePath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- Paint_Bucket ----------

        private static string MakeBucket(Meshes meshes, Materials mats, float innerRadius, float paintY, Bounds bucketBounds)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BucketModel);
            var root = NewRoot("Paint_Bucket");
            try
            {
                var bucket = root.AddComponent<PaintBucket>();
                // The model: its paint level on the root, its middle under it.
                var look = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                look.name = "Bucket";
                look.transform.localRotation = Quaternion.identity;
                look.transform.localScale = Vector3.one * BucketSize;
                var meshTransform = look.GetComponentInChildren<MeshFilter>().transform;
                // The mesh sits on the model's own transform (offset in the FBX): measure in the model's space.
                var mid = look.transform.InverseTransformPoint(meshTransform.TransformPoint(new Vector3(bucketBounds.center.x, paintY, bucketBounds.center.z)));
                look.transform.localPosition = -mid * BucketSize;
                foreach (var c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                float r = innerRadius * BucketSize;
                var paint = new GameObject("Paint");
                paint.transform.SetParent(root.transform, false);
                paint.transform.localScale = new Vector3(r, 1f, r);
                paint.AddComponent<MeshFilter>().sharedMesh = meshes.paint;
                var paintRenderer = paint.AddComponent<MeshRenderer>();
                paintRenderer.sharedMaterial = mats.paint;
                paintRenderer.shadowCastingMode = ShadowCastingMode.Off;

                var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                glow.name = "Floor Glow";
                Object.DestroyImmediate(glow.GetComponent<Collider>());
                glow.transform.SetParent(root.transform, false);
                glow.transform.localPosition = new Vector3(0f, -PaintHeight + 0.015f, 0f);
                glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                glow.transform.localScale = Vector3.one * (r * 2f * 3.2f);
                var glowRenderer = glow.GetComponent<MeshRenderer>();
                glowRenderer.sharedMaterial = mats.glow;
                glowRenderer.shadowCastingMode = ShadowCastingMode.Off;

                // The puddle under it (it grows as the round goes on) and the floor its drips splat on.
                var puddle = Flat(root, "Puddle", mats.floorSplat, 0.3f);
                puddle.transform.localPosition = new Vector3(0f, -PaintHeight + 0.012f, 0f);
                var floor = new GameObject("Floor").transform;
                floor.SetParent(root.transform, false);
                floor.localPosition = new Vector3(0f, -PaintHeight, 0f);
                // The rim: how high over the paint, and how wide (from the model).
                float rimUp = (bucketBounds.max.y - paintY) * BucketSize;
                float rimOut = Mathf.Max(bucketBounds.extents.x, bucketBounds.extents.z) * BucketSize;
                var drips = Drips(root, mats.drop, mats.floorSplat, floor, rimUp, rimOut);

                // No ring over the paint at a dip: the liquid paint (429/Paint Liquid) ripples by itself.
                var so = new SerializedObject(bucket);
                so.FindProperty("drips").objectReferenceValue = drips;
                so.FindProperty("puddle").objectReferenceValue = puddle.GetComponent<Renderer>();
                var onFloor = so.FindProperty("onFloor");
                var floorParts = new Object[] { glow.transform, puddle.transform, floor };
                onFloor.arraySize = floorParts.Length;
                for (int i = 0; i < floorParts.Length; i++) onFloor.GetArrayElementAtIndex(i).objectReferenceValue = floorParts[i];
                so.FindProperty("paint").objectReferenceValue = paintRenderer;
                so.FindProperty("floorGlow").objectReferenceValue = glowRenderer;
                so.FindProperty("ripple").objectReferenceValue = null;
                so.FindProperty("reach").floatValue = r * 1.05f;
                // On the floor: still, no bob or rocking, and dipped from a little higher (hands reach down to it).
                so.FindProperty("bob").floatValue = 0f;
                so.FindProperty("tilt").floatValue = 0f;
                so.FindProperty("abovePaint").floatValue = 0.12f;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BucketPath);
            }
            finally { Object.DestroyImmediate(root); }
            return $"Paint_Bucket: bucket x{BucketSize}, paint {innerRadius * BucketSize * 2f * 100f:F0} cm across, {(paintY - bucketBounds.min.y) * BucketSize * 100f:F0} cm up";
        }

        // ---------- Paint_Bee ----------

        private static string MakeBee(Materials mats)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BeeSource);
            if (source == null) return "MISSING " + BeeSource;
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(source, build);
            var root = NewRoot("Paint_Bee");
            try
            {
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var models = copy.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bee_Models");
                var looks = models.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("bee_smol")).ToArray();
                models.SetParent(root.transform, false);
                models.localPosition = Vector3.zero;
                models.localRotation = Quaternion.identity;
                models.localScale = Vector3.one;
                foreach (var look in looks)
                {
                    look.sharedMaterial = mats.bee;
                    look.gameObject.SetActive(false);
                    look.updateWhenOffscreen = false;
                }
                if (looks.Length > 0) looks[0].gameObject.SetActive(true);
                var animator = models.GetComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // Its body's middle on the root (Bee_Fly pose): the round's hits and the sting measure from there.
                var fly = AssetDatabase.LoadAllAssetsAtPath(BeeModelFbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == BeeFlyClip);
                if (fly != null) fly.SampleAnimation(models.gameObject, 0.25f * fly.length);
                // The mean of its posed vertices (wings have few, so it's the body's middle).
                var centre = models.position;
                if (looks.Length > 0)
                {
                    var baked = new Mesh();
                    looks[0].BakeMesh(baked, true);
                    var sum = Vector3.zero;
                    var vs = baked.vertices;
                    foreach (var p in vs) sum += looks[0].transform.TransformPoint(p);
                    if (vs.Length > 0) centre = sum / vs.Length;
                    Object.DestroyImmediate(baked);
                }
                models.localPosition = -root.transform.InverseTransformPoint(centre);

                // The aura: a soft glow round it, and sparkles left behind as it flies (the bee tints them its colour).
                var glow = Aura(root.transform, "Aura Glow", mats.aura, false);
                var sparkles = Aura(root.transform, "Aura Sparkles", mats.sparkle, true);

                // Its light trail (it takes the bee's colour; red hot while it dives; every colour for the rainbow bee).
                var trail = new GameObject("Trail").AddComponent<TrailRenderer>();
                trail.transform.SetParent(root.transform, false);
                trail.time = 0.35f;
                trail.minVertexDistance = 0.02f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
                trail.numCapVertices = 2;
                trail.sharedMaterial = mats.trail;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;

                var buzzer = root.AddComponent<WingBuzz>();
                var sourceBuzz = source.GetComponent<WingBuzz>();
                if (sourceBuzz != null) EditorUtility.CopySerialized(sourceBuzz, buzzer);

                var buzz = root.AddComponent<AudioSource>();
                buzz.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BuzzSound);
                buzz.loop = true;
                buzz.playOnAwake = false;
                buzz.spatialBlend = 1f;
                buzz.minDistance = 0.6f;
                buzz.maxDistance = 12f;
                buzz.rolloffMode = AudioRolloffMode.Logarithmic;
                buzz.dopplerLevel = 0f;
                buzz.volume = 0.55f;

                var bee = root.AddComponent<PaintBee>();
                var so = new SerializedObject(bee);
                so.FindProperty("animator").objectReferenceValue = animator;
                var list = so.FindProperty("looks");
                list.arraySize = looks.Length;
                for (int i = 0; i < looks.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = looks[i].gameObject;
                so.FindProperty("buzz").objectReferenceValue = buzz;
                so.FindProperty("radius").floatValue = BeeBodyRadius;
                so.FindProperty("glow").floatValue = 1f;
                so.FindProperty("rainbowLook").objectReferenceValue = mats.rainbowBee;
                var auras = so.FindProperty("auras");
                auras.arraySize = 2;
                auras.GetArrayElementAtIndex(0).objectReferenceValue = glow;
                auras.GetArrayElementAtIndex(1).objectReferenceValue = sparkles;
                so.FindProperty("trail").objectReferenceValue = trail;
                so.ApplyModifiedPropertiesWithoutUndo();
                root.transform.localScale = Vector3.one * BeeSize;
                PrefabUtility.SaveAsPrefabAsset(root, BeePath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (copy != null) Object.DestroyImmediate(copy);
            }
            return $"Paint_Bee: Bee_Small's model at x{BeeSize}, Paint_Bee_Mat (rainbow: Paint_Bee_Rainbow_Mat), aura, Wing Buzz, buzz";
        }

        // One part of a bee's aura. Sizes are for a bee of size 1 (they scale with it: Hierarchy).
        private static ParticleSystem Aura(Transform bee, string name, Material material, bool sparkles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(bee, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            if (sparkles)
            {
                // Little stars twinkling off it and left behind where it flies.
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
                main.startColor = new Color(1f, 1f, 1f, 1f);
                main.gravityModifier = -0.03f;
                main.maxParticles = 30;
                emission.rateOverTime = 16f;
                shape.radius = 0.32f;
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f)));
            }
            else
            {
                // A soft glow round its body, breathing a little.
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.0f);
                main.startColor = new Color(1f, 1f, 1f, 0.22f);
                main.maxParticles = 10;
                emission.rateOverTime = 7f;
                shape.radius = 0.04f;
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(0f, 1f) });
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.85f), new Keyframe(1f, 1.1f)));
            }
            fade.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // ---------- Paint_Bees_Start ----------

        private static string MakeStart(Meshes meshes, Materials mats)
        {
            var root = NewRoot("Paint_Bees_Start");
            try
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ring.name = "Ring";
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = Vector3.one * 1.35f;
                var ringRenderer = ring.GetComponent<MeshRenderer>();
                ringRenderer.sharedMaterial = mats.ring;
                ringRenderer.shadowCastingMode = ShadowCastingMode.Off;

                var arrow = new GameObject("Arrow");
                arrow.transform.SetParent(root.transform, false);
                arrow.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                arrow.AddComponent<MeshFilter>().sharedMesh = meshes.arrow;
                var arrowRenderer = arrow.AddComponent<MeshRenderer>();
                arrowRenderer.sharedMaterial = mats.arrow;
                arrowRenderer.shadowCastingMode = ShadowCastingMode.Off;

                var sparkles = new GameObject("Sparkles");
                sparkles.transform.SetParent(root.transform, false);
                sparkles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var ps = sparkles.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.6f, 0.25f, 1f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 60;
                var emission = ps.emission;
                emission.rateOverTime = 18f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.5f;
                shape.radiusThickness = 0f;
                var fade = ps.colorOverLifetime;
                fade.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
                fade.color = g;
                var psRenderer = sparkles.GetComponent<ParticleSystemRenderer>();
                psRenderer.sharedMaterial = mats.sparkle;
                psRenderer.shadowCastingMode = ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(root, StartPath);
            }
            finally { Object.DestroyImmediate(root); }
            return "Paint_Bees_Start: ring, arrow, sparkles";
        }

        // ---------- The bucket's drips ----------

        // Paint running down the bucket's side from its rim (rimUp over the paint, rimOut from the middle): slow at
        // first (along the side), then falling off; each drop splats on the floor (Floor) where it lands.
        private static ParticleSystem Drips(GameObject bucket, Material drop, Material splat, Transform floor, float rimUp, float rimOut)
        {
            var drips = new GameObject("Drips").AddComponent<ParticleSystem>();
            drips.transform.SetParent(bucket.transform, false);
            drips.transform.localPosition = new Vector3(0f, rimUp * 0.5f, 0f);
            drips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = drips.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.05f), new Keyframe(0.32f, 0.07f), new Keyframe(0.42f, 1f), new Keyframe(1f, 1f)));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var emission = drips.emission;
            emission.rateOverTime = 5f;
            var shape = drips.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = rimOut;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var renderer = drips.GetComponent<ParticleSystemRenderer>();
            // Long as they fall, like drips.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1.3f;
            renderer.sharedMaterial = drop;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            PaintSplats.Add(drips, floor, splat, 1f, new Vector2(0.05f, 0.11f));
            return drips;
        }

        // ---------- Paint_Bees_Road ----------

        // The lit road the bees fly in on (the round lays it in front of the player from the 3-2-1 and drives it).
        private static string MakeRoad(Materials mats)
        {
            var root = NewRoot("Paint_Bees_Road");
            try
            {
                // Lights marching in along the lanes: gold far off, hot red close by, sparkling as they come.
                var marchers = Lights(root, "Marching Lights", mats.orb, 160);
                var march = marchers.colorOverLifetime;
                march.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.6f), new GradientColorKey(new Color(1f, 0.12f, 0.05f), 0.95f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.88f), new GradientAlphaKey(0f, 1f) });
                march.color = gradient;
                var grow = marchers.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));
                var spin = marchers.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(Mathf.PI);

                // The still runway lights along the lanes, and a glow at the far end of each.
                var runway = Lights(root, "Runway Lights", mats.roadLight, 160);
                var ends = Lights(root, "Lane Ends", mats.orb, 16);

                // Embers rising off the floor all over the road.
                var embers = new GameObject("Embers").AddComponent<ParticleSystem>();
                embers.transform.SetParent(root.transform, false);
                embers.transform.localPosition = new Vector3(0f, 0.02f, 3.8f);
                embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = embers.main;
                main.loop = true;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                main.gravityModifier = -0.04f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 100;
                var emission = embers.emission;
                emission.rateOverTime = 24f;
                var shape = embers.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(8.5f, 6.5f, 0.05f);
                shape.rotation = new Vector3(-90f, 0f, 0f);
                var noise = embers.noise;
                noise.enabled = true;
                noise.strength = 0.25f;
                noise.frequency = 0.6f;
                var burn = embers.colorOverLifetime;
                burn.enabled = true;
                var fire = new Gradient();
                fire.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.75f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 0.5f), new GradientColorKey(new Color(0.6f, 0.05f, 0.02f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
                burn.color = fire;
                var emberRenderer = embers.GetComponent<ParticleSystemRenderer>();
                emberRenderer.sharedMaterial = mats.roadLight;
                emberRenderer.shadowCastingMode = ShadowCastingMode.Off;

                // The danger ring round the player's feet.
                var danger = Flat(root, "Danger Ring", mats.danger, 1.7f);
                danger.transform.localPosition = new Vector3(0f, 0.02f, 0f);

                var road = root.AddComponent<PaintBeesRoad>();
                var so = new SerializedObject(road);
                so.FindProperty("marchers").objectReferenceValue = marchers;
                so.FindProperty("runway").objectReferenceValue = runway;
                so.FindProperty("laneEnds").objectReferenceValue = ends;
                so.FindProperty("embers").objectReferenceValue = embers;
                so.FindProperty("dangerRing").objectReferenceValue = danger.GetComponent<Renderer>();
                so.FindProperty("laneMaterial").objectReferenceValue = mats.lane;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, RoadPath);
            }
            finally { Object.DestroyImmediate(root); }
            return "Paint_Bees_Road: lanes, runway and marching lights, embers, danger ring";
        }

        // A particle system the road emits into itself (playing, with no emission of its own), in world space.
        private static ParticleSystem Lights(GameObject parent, string name, Material material, int most)
        {
            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent.transform, false);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 2f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = most;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        // A flat quad lying on the floor (facing up), this many metres across, with no collider.
        private static GameObject Flat(GameObject parent, string name, Material material, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * size;
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad;
        }

        // ---------- Paint_Bees_Round ----------

        private static string MakeRound(Materials mats)
        {
            bool made = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RoundPath) == null)
            {
                var temp = new GameObject("Paint_Bees_Round");
                temp.AddComponent<PaintBeesRound>();
                PrefabUtility.SaveAsPrefabAsset(temp, RoundPath);
                Object.DestroyImmediate(temp);
                made = true;
            }
            var root = PrefabUtility.LoadPrefabContents(RoundPath);
            var filled = new List<string>();
            try
            {
                var round = root.GetComponent<PaintBeesRound>();
                if (round == null) round = root.AddComponent<PaintBeesRound>();
                var so = new SerializedObject(round);
                Set(so, "stageSky", AssetDatabase.LoadAssetAtPath<GameObject>(SkyPath), filled);
                Set(so, "marker", AssetDatabase.LoadAssetAtPath<GameObject>(StartPath), filled);
                var bucket = AssetDatabase.LoadAssetAtPath<GameObject>(BucketPath);
                Set(so, "bucket", bucket != null ? bucket.GetComponent<PaintBucket>() : null, filled);
                var bee = AssetDatabase.LoadAssetAtPath<GameObject>(BeePath);
                Set(so, "bee", bee != null ? bee.GetComponent<PaintBee>() : null, filled);
                var road = AssetDatabase.LoadAssetAtPath<GameObject>(RoadPath);
                Set(so, "road", road != null ? road.GetComponent<PaintBeesRoad>() : null, filled);
                Set(so, "song", AssetDatabase.LoadAssetAtPath<AudioClip>(SongPath), filled);
                Set(so, "onBeatFlash", AssetDatabase.LoadAssetAtPath<GameObject>(FlashPath), filled);
                Set(so, "floorSplatMaterial", mats.floorSplat, filled);
                var candies = Enumerable.Range(1, 8).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(CandyPath, i))).Where(c => c != null).ToArray();
                SetList(so, "candies", candies, filled);
                // This PC's first sound picks go: the user's (from the other PC) come in their place.
                foreach (var field in new[] { "bucketSound", "boingSound", "stingSound", "goSound", "perfectSound" })
                {
                    var p = so.FindProperty(field);
                    if (p != null && p.objectReferenceValue != null && OldPicks.Contains(AssetDatabase.GetAssetPath(p.objectReferenceValue))) p.objectReferenceValue = null;
                }
                var dips = so.FindProperty("dipSounds");
                for (int i = 0; dips != null && i < dips.arraySize; i++)
                {
                    var clip = dips.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (clip != null && OldPicks.Contains(AssetDatabase.GetAssetPath(clip))) { dips.arraySize = 0; break; }
                }
                SetList(so, "dipSounds", DipSounds.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray(), filled);
                SetList(so, "swatSounds", SwatSounds.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray(), filled);
                Set(so, "bucketSound", AssetDatabase.LoadAssetAtPath<AudioClip>(BucketSound), filled);
                Set(so, "boingSound", AssetDatabase.LoadAssetAtPath<AudioClip>(BoingSound), filled);
                Set(so, "stingSound", AssetDatabase.LoadAssetAtPath<AudioClip>(StingSound), filled);
                Set(so, "tickSound", AssetDatabase.LoadAssetAtPath<AudioClip>(TickSound), filled);
                Set(so, "goSound", AssetDatabase.LoadAssetAtPath<AudioClip>(GoSound), filled);
                Set(so, "rainbowSound", AssetDatabase.LoadAssetAtPath<AudioClip>(RainbowSound), filled);
                Set(so, "perfectSound", AssetDatabase.LoadAssetAtPath<AudioClip>(PerfectSound), filled);
                Set(so, "puffEffect", AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath), filled);
                Set(so, "burstMaterial", mats.drop, filled);
                Set(so, "splatMaterial", mats.splat, filled);
                Set(so, "winFireworks", AssetDatabase.LoadAssetAtPath<GameObject>(FireworksPath), filled);
                Set(so, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), filled);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, RoundPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return (made ? "Made Paint_Bees_Round" : "Paint_Bees_Round") + (filled.Count > 0 ? ": " + string.Join(", ", filled) : ": already set up");
        }

        // Fills an empty slot (or one pointing at a missing thing); a slot set by hand to something else is kept.
        private static void Set(SerializedObject so, string field, Object value, List<string> filled)
        {
            var p = so.FindProperty(field);
            if (p == null || value == null || p.objectReferenceValue == value) return;
            if (p.objectReferenceValue != null && !IsOurs(p.objectReferenceValue)) return;
            p.objectReferenceValue = value;
            filled.Add(field);
        }

        private static void SetList(SerializedObject so, string field, Object[] values, List<string> filled)
        {
            var p = so.FindProperty(field);
            if (p == null || values.Length == 0 || p.arraySize > 0) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            filled.Add($"{values.Length} {field}");
        }

        // Things this menu makes itself (it may point the round at them again).
        private static bool IsOurs(Object thing)
        {
            string path = AssetDatabase.GetAssetPath(thing);
            // (Also the other PC's first parts: Prefabs/Bases/Paint_Bees, Data/Effects/Paint Bees *.)
            return path.StartsWith(PartsFolder + "Paint_") || path.StartsWith(MaterialFolder) ||
                   path.StartsWith("Assets/Prefabs/Bases/Paint_Bees/") || path.StartsWith("Assets/Data/Effects/Paint Bees");
        }

        // ---------- The scene ----------

        private static IEnumerable<string> HookScene()
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES" && f.gameObject.scene.IsValid());
            if (gs == null) { yield return "No GAMESTAGES in the open scene: open 2 Game Scene and run it again."; yield break; }
            var f = gs.Fsm;
            if (!f.Initialized) f.Init(gs);
            var round = AssetDatabase.LoadAssetAtPath<GameObject>(RoundPath);
            var state = f.GetState(GameStagesState);
            if (state == null) { yield return $"GAMESTAGES has no '{GameStagesState}': run Set Up Nesting Pumpkin Bonus first."; yield break; }
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) { yield return $"'{GameStagesState}' didn't load all its actions; left alone"; yield break; }
            var play = state.Actions.OfType<PlayBonusRound>().FirstOrDefault();
            if (play == null) { yield return $"'{GameStagesState}' has no Play Bonus Round: run Set Up Nesting Pumpkin Bonus first."; yield break; }
            Undo.RecordObject(gs, "Paint Bees Bonus");
            bool changed = false;
            if (play.round == null || play.round.Value != round)
            {
                changed = true;
                string was = play.round != null && play.round.Value != null ? play.round.Value.name : "nothing";
                play.round = new FsmGameObject { Value = round };
                state.SaveActions();
                EditorUtility.SetDirty(gs);
                yield return $"'{GameStagesState}' plays Paint_Bees_Round (was {was})";
            }
            else yield return $"'{GameStagesState}' already plays Paint_Bees_Round";

            var playroom = gs.transform.Find(PlayroomStageName);
            var chest = playroom != null ? playroom.Find(ChestSpotName) : null;
            if (chest == null) yield return $"No {PlayroomStageName}/{ChestSpotName}";
            else if (chest.gameObject.activeSelf)
            {
                Undo.RecordObject(chest.gameObject, "Paint Bees Bonus");
                chest.gameObject.SetActive(false);
                changed = true;
                yield return $"{ChestSpotName} switched off (no chest: the round starts on the ring)";
            }
            else yield return $"{ChestSpotName} already off";
            if (!changed) yield break;
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            yield return "Save the scene to keep the GAMESTAGES changes.";
        }

        private static void MakeFolder(string folder)
        {
            folder = folder.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            MakeFolder(parent + "/");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
