using System.Collections.Generic;
using System.Linq;
using Game.Rooms;
using UnityEditor;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Set Up Picture Puzzle: the jigsaw picture mini game on every broken picture
    /// (Assets/Prefabs/Items/Deco/Picture_Hang_*_Broken.prefab).
    /// - Builds Assets/Prefabs/Items/Deco/Picture_Puzzle_Glow.prefab, the magic glow shaped to Picture_Hang_4's frame
    ///   (1.04 x 1.05 m, the picture facing +Z): a soft warm glow pulsing along the frame, small sparkles drifting off
    ///   its edges towards the room, a few star twinkles. It keeps going while the game is stopped for the puzzle.
    /// - Builds Picture_Puzzle_Space.prefab: the stars, faint coloured clouds and floating dust round the player while
    ///   the puzzle is played, and the backdrop material (429/Space Backdrop) that darkens the room behind and lets
    ///   the picture show in front of any wall.
    /// - Makes the broken pictures that aren't made yet: for each Assets/Models/**/Picture_Hang_X_Broken.fbx (the
    ///   frame, its whole painting Picture_Hang_X_Complete and the 9 pieces Picture_Hang_X_Puzzle_N, cut in Blender) with
    ///   no Picture_Hang_X_Broken prefab, it copies the whole picture's prefab (Picture_Hang_X: its collider, frozen
    ///   rigidbody, Health FSM, Wall Hung Prop) and puts the model in it (linked: a new export updates it), turned and
    ///   moved to sit just where the whole picture's painting does, so both stand right on the same spot. Its
    ///   materials are mapped to the game's ones, as Picture_Hang_4_Broken.fbx's are. A bigger puzzle gets its own
    ///   rules (Rules: Picture_Hang_8_Broken, 7 x 4, keeps 16 pieces up, 90 s, 30 candies).
    /// - On each broken picture: the old GrowthParticles go and the glow goes in, on its frame and sized to it; the root's own
    ///   frame drawing is switched off (its child draws the same frame); the whole painting starts hidden; and Picture
    ///   Puzzle is added and filled in (the pieces, the whole painting, candies, small cockroaches, effects, sounds).
    ///   One open in Prefab Mode is set up there and saved, unless it has unsaved changes (then it's left).
    /// Safe to run again: it rebuilds both prefabs and their materials (Assets/Data/Effects/Picture *.mat).
    /// </summary>
    public static class PicturePuzzleSetupMenu
    {
        private const string GlowPath = "Assets/Prefabs/Items/Deco/Picture_Puzzle_Glow.prefab";
        private const string SpacePath = "Assets/Prefabs/Items/Deco/Picture_Puzzle_Space.prefab";
        private const string DecoFolder = "Assets/Prefabs/Items/Deco";
        // Where the broken pictures' models are looked for (any folder under it: Livingroom_Stuffs, Bedroom_Stuffs).
        private const string ModelFolder = "Assets/Models";
        // Its model's materials are mapped to the game's own: the new broken pictures' get the same.
        private const string MappedModel = "Assets/Models/Livingroom_Stuffs/Picture_Hang_4_Broken.fbx";
        // The frame the glow is shaped to (Picture_Hang_4's collider): other frames get it scaled to theirs.
        private static readonly Vector2 GlowFrame = new(1.038f, 1.054f);
        // The bigger puzzles' rules (the user's pick, 2026-10-27): pieces that stay on the frame, seconds, candies won.
        // Every other picture keeps Picture Puzzle's own (3 stay, 60 s, 12 candies).
        private static readonly Dictionary<string, (int stay, float seconds, int candies)> Rules = new()
        {
            { "Picture_Hang_8_Broken", (16, 90f, 30) }, // 7 x 4 = 28 pieces: 12 to catch
        };
        private const string SoftTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string StarTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Flare02.png";
        private const string Lana = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string BugPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Cockroach_Small.prefab";
        private const string CandyFolder = "Assets/Prefabs/Items/Breakable/";
        // Drawn after the space's backdrop (Overlay + 1) and the picture (Overlay + 2), like the puzzle's own copies.
        private const int OverFadeQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 3;
        private const string BackdropPath = "Assets/Data/Effects/Picture Space Backdrop.mat";
        // The frame: its middle line (between the painting's edge and the frame's outside), and just in front of it.
        private static readonly Vector3 FrameSize = new(0.99f, 1.01f, 0f);
        private const float InFront = 0.065f;

        [MenuItem("Tools/429 Game/Rooms/Set Up Picture Puzzle")]
        public static void Run()
        {
            var notes = new List<string>();
            var glow = BuildGlow();
            notes.Add("built " + GlowPath);
            var space = BuildSpace();
            notes.Add("built " + SpacePath);
            var backdrop = BuildBackdrop();
            notes.Add(backdrop != null ? "built " + BackdropPath : "429/Space Backdrop shader MISSING");
            notes.AddRange(MakeBrokenPictures());
            foreach (var path in BrokenPictures()) notes.Add(ApplyToPicture(path, glow, space, backdrop));
            AssetDatabase.SaveAssets();
            Debug.Log("[Picture Puzzle] " + string.Join("; ", notes));
        }

        // ---------- The glow on the frame ----------

        private static GameObject BuildGlow()
        {
            var soft = DangerFlameSetupMenu.MakeMaterial("Picture Glow", SoftTexture, true);
            var star = DangerFlameSetupMenu.MakeMaterial("Picture Glow Twinkle", StarTexture, true);
            var root = new GameObject("Picture_Puzzle_Glow");
            try
            {
                EdgeGlow(DangerFlameSetupMenu.Child(root, "Edge Glow"), soft);
                Sparkles(DangerFlameSetupMenu.Child(root, "Sparkles"), soft);
                Twinkles(DangerFlameSetupMenu.Child(root, "Twinkles"), star);
                // It keeps sparkling while the game is stopped for the puzzle.
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
                {
                    var main = ps.main;
                    main.useUnscaledTime = true;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, GlowPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Particles start on the frame's four edges (or anywhere over it).
        private static void OnFrame(ParticleSystem ps, bool edgesOnly)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = edgesOnly ? ParticleSystemShapeType.BoxEdge : ParticleSystemShapeType.Box;
            shape.scale = FrameSize;
            shape.position = new Vector3(0f, 0f, InFront);
        }

        // A soft warm glow all round the frame, gently pulsing.
        private static void EdgeGlow(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 90);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(1.3f, 1.9f);
            main.startSize = new MinMaxCurve(0.12f, 0.22f);
            var emission = ps.emission;
            emission.rateOverTime = 42f;
            OnFrame(ps, true);
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 0.62f, 0.22f, 0f), DangerFlameSetupMenu.Key(1f, 0.42f, 0.1f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.26f, 0.45f), new GradientAlphaKey(0f, 1f) });
            DangerFlameSetupMenu.Shrink(ps, 0.7f, 1f, 0.85f);
        }

        // Little sparks drifting off the frame towards the room, rising a little.
        private static void Sparkles(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 50);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(1.2f, 2.0f);
            main.startSize = new MinMaxCurve(0.018f, 0.04f);
            var emission = ps.emission;
            emission.rateOverTime = 18f;
            OnFrame(ps, true);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new MinMaxCurve(-0.03f, 0.03f);
            velocity.y = new MinMaxCurve(0.02f, 0.07f);
            velocity.z = new MinMaxCurve(0.03f, 0.08f);
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 0.92f, 0.6f, 0f), DangerFlameSetupMenu.Key(1f, 0.55f, 0.15f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            DangerFlameSetupMenu.Wobble(ps, 0.04f, 1.2f);
        }

        // A few four-point stars popping on and off round the frame and over the picture.
        private static void Twinkles(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 10);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(0.35f, 0.6f);
            main.startSize = new MinMaxCurve(0.06f, 0.12f);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new MinMaxGradient(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.75f, 0.35f));
            var emission = ps.emission;
            emission.rateOverTime = 3.5f;
            OnFrame(ps, false);
            Pop(ps);
        }

        // Each one pops up and is gone again: a quick flash, spinning.
        private static void Pop(ParticleSystem ps)
        {
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new MinMaxCurve(-2f, 2f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
        }

        // ---------- The space round the player ----------

        private static GameObject BuildSpace()
        {
            var soft = OverFade(DangerFlameSetupMenu.MakeMaterial("Picture Space Glow", SoftTexture, true));
            var star = OverFade(DangerFlameSetupMenu.MakeMaterial("Picture Space Star", StarTexture, true));
            var root = new GameObject("Picture_Puzzle_Space");
            try
            {
                Stars(DangerFlameSetupMenu.Child(root, "Stars"), soft);
                BrightStars(DangerFlameSetupMenu.Child(root, "Bright Stars"), star);
                Clouds(DangerFlameSetupMenu.Child(root, "Clouds"), soft);
                Dust(DangerFlameSetupMenu.Child(root, "Dust"), soft);
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
                {
                    var main = ps.main;
                    main.useUnscaledTime = true;
                    main.scalingMode = ParticleSystemScalingMode.Shape;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, SpacePath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material BuildBackdrop()
        {
            var shader = Shader.Find("429/Space Backdrop");
            if (shader == null) return null;
            var material = AssetDatabase.LoadAssetAtPath<Material>(BackdropPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, BackdropPath);
            }
            material.shader = shader;
            material.color = new Color(0.01f, 0f, 0.03f, 0.92f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material OverFade(Material material)
        {
            material.renderQueue = OverFadeQueue;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Particles on a ball round the player's eyes (shell) or all through it.
        private static void Ball(ParticleSystem ps, float radius, bool shell)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = shell ? 0f : 1f;
        }

        // Hundreds of small stars far round, fading in and out.
        private static void Stars(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 320);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(5f, 9f);
            main.startSize = new MinMaxCurve(0.014f, 0.034f);
            main.startColor = new MinMaxGradient(new Color(0.85f, 0.9f, 1f), new Color(1f, 0.92f, 0.8f));
            var emission = ps.emission;
            emission.rateOverTime = 45f;
            Ball(ps, 3.2f, true);
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 1f, 1f, 0f), DangerFlameSetupMenu.Key(1f, 1f, 1f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.7f, 0.8f), new GradientAlphaKey(0f, 1f) });
        }

        // A few big four-point stars twinkling here and there.
        private static void BrightStars(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 16);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(0.9f, 1.8f);
            main.startSize = new MinMaxCurve(0.07f, 0.14f);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new MinMaxGradient(new Color(0.8f, 0.88f, 1f), new Color(1f, 0.9f, 0.7f));
            var emission = ps.emission;
            emission.rateOverTime = 7f;
            Ball(ps, 3f, true);
            Pop(ps);
        }

        // Faint purple and teal clouds far out, so it isn't plain black.
        private static void Clouds(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 16);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(9f, 13f);
            main.startSize = new MinMaxCurve(1.4f, 2.6f);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new MinMaxGradient(new Color(0.45f, 0.2f, 0.75f), new Color(0.15f, 0.45f, 0.6f));
            var emission = ps.emission;
            emission.rateOverTime = 1.4f;
            Ball(ps, 4f, true);
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 1f, 1f, 0f), DangerFlameSetupMenu.Key(1f, 1f, 1f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.13f, 0.4f), new GradientAlphaKey(0f, 1f) });
            ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 3f;
        }

        // Tiny specks floating slowly all round the player: weightless.
        private static void Dust(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 90);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(4f, 6f);
            main.startSize = new MinMaxCurve(0.006f, 0.013f);
            main.startSpeed = new MinMaxCurve(0.01f, 0.04f);
            main.startColor = new MinMaxGradient(new Color(0.8f, 0.85f, 1f), new Color(1f, 0.85f, 0.6f));
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            Ball(ps, 1.4f, false);
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 1f, 1f, 0f), DangerFlameSetupMenu.Key(1f, 1f, 1f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            DangerFlameSetupMenu.Wobble(ps, 0.03f, 0.6f);
        }

        // ---------- Making the broken pictures ----------

        private static List<string> BrokenPictures()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("Picture_Hang t:Prefab", new[] { DecoFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("_Broken.prefab")) paths.Add(path);
            }
            paths.Sort();
            return paths;
        }

        private static List<string> MakeBrokenPictures()
        {
            var notes = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("Picture_Hang t:Model", new[] { ModelFolder }))
            {
                var modelPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!modelPath.EndsWith("_Broken.fbx")) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(modelPath);
                string prefabPath = DecoFolder + "/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue;
                string wholePath = DecoFolder + "/" + name.Substring(0, name.Length - "_Broken".Length) + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(wholePath) == null)
                {
                    notes.Add(name + ": no " + wholePath + " to make it from");
                    continue;
                }
                // Only the frame (its painting and pieces under it) belongs in the file: anything exported with it (the
                // whole picture, say) makes Unity wrap them all in an empty, and it'd show in the game too.
                var exported = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (!exported.TryGetComponent(out MeshFilter _))
                {
                    var extra = new List<string>();
                    foreach (Transform t in exported.transform)
                        if (t.name != name) extra.Add(t.name);
                    notes.Add(name + ".fbx has more than the frame in it (" + string.Join(", ", extra) +
                              "): export just " + name + " with its children, then run this again");
                    continue;
                }
                // The puzzle reads the frame's +Z as its front and puts each piece back unturned in it: a broken copy
                // turned in Blender with its rotation applied has its painting facing another way and its pieces
                // turned inside it (the menu turns the frame to match the whole picture itself).
                string turnedWhy = TurnedInside(exported);
                if (turnedWhy != null)
                {
                    notes.Add(name + ".fbx: " + turnedWhy + " (was the " + name + " turned in Blender with its rotation " +
                              "applied?). Keep its front on local -Y with nothing turned inside it, export again, then run this again");
                    continue;
                }
                notes.Add(MapMaterials(modelPath, wholePath));
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                // A copy of the whole picture (its own new prefab, not a variant), then the model goes in.
                if (!AssetDatabase.CopyAsset(wholePath, prefabPath))
                {
                    notes.Add(name + ": couldn't copy " + wholePath);
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    root.name = name;
                    // The model inside it, linked, lined up with the whole picture: the same way round and the same
                    // middle, so either one stands right on the same spot (a whole picture can be set up its own way:
                    // Picture_Hang_8 faces -X and hangs from its top edge). The root keeps its own drawing (switched
                    // off, the child draws the frame).
                    var frame = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                    frame.name = name;
                    var (turn, at) = LineUp(root, model);
                    frame.transform.localRotation = turn;
                    frame.transform.localPosition = at;
                    frame.transform.localScale = Vector3.one;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    notes.Add("made " + prefabPath + " from " + System.IO.Path.GetFileName(wholePath));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return notes;
        }

        // Why a broken model's painting or pieces don't sit the way the puzzle reads them, or null when they do.
        private static string TurnedInside(GameObject model)
        {
            foreach (Transform t in model.transform)
            {
                if (t.name.EndsWith("_Complete") && t.TryGetComponent(out MeshFilter painting) && painting.sharedMesh != null)
                {
                    var front = t.localRotation * PaintingNormal(painting.sharedMesh, null, null);
                    if (Vector3.Dot(front, Vector3.forward) < 0.9f) return "its painting faces " + front.ToString("F0") + " in the frame, not +Z";
                }
                if (t.name.Contains("_Puzzle_") && Quaternion.Angle(t.localRotation, Quaternion.identity) > 1f)
                    return t.name + " is turned " + t.localEulerAngles.ToString("F0") + " inside the frame";
            }
            return null;
        }

        // How the broken frame sits in the whole picture's prefab: turned round the up axis (in quarter turns) so its
        // painting faces where the whole picture's painting faces, and moved so their middles meet.
        private static (Quaternion turn, Vector3 at) LineUp(GameObject whole, GameObject model)
        {
            Transform painting = null;
            foreach (Transform t in model.transform) if (t.name.EndsWith("_Complete")) painting = t;
            if (!whole.TryGetComponent(out MeshFilter wholeMesh) || wholeMesh.sharedMesh == null || painting == null ||
                !painting.TryGetComponent(out MeshFilter paintingMesh) || paintingMesh.sharedMesh == null ||
                !model.TryGetComponent(out MeshFilter frameMesh) || frameMesh.sharedMesh == null)
                return (Quaternion.identity, Vector3.zero);
            // The whole picture's painting: its triangles on the same spot of the texture as the broken one's painting
            // (a frame and its painting can share one material, so the material alone can't tell them apart).
            var uvs = paintingMesh.sharedMesh.uv;
            var spot = uvs.Length > 0 ? new Rect(uvs[0], Vector2.zero) : new Rect();
            foreach (var uv in uvs) spot = Rect.MinMaxRect(Mathf.Min(spot.xMin, uv.x), Mathf.Min(spot.yMin, uv.y), Mathf.Max(spot.xMax, uv.x), Mathf.Max(spot.yMax, uv.y));
            var wholeFront = PaintingNormal(wholeMesh.sharedMesh, whole.GetComponent<MeshRenderer>(), spot);
            var frameFront = painting.localRotation * PaintingNormal(paintingMesh.sharedMesh, null, null);
            float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(frameFront, Vector3.up), Vector3.ProjectOnPlane(wholeFront, Vector3.up), Vector3.up);
            var turn = Quaternion.Euler(0f, Mathf.Round(yaw / 90f) * 90f, 0f);
            return (turn, wholeMesh.sharedMesh.bounds.center - turn * frameMesh.sharedMesh.bounds.center);
        }

        // Which way a mesh's painting faces: its triangles' area-weighted normal, wood left out; with a texture spot,
        // only the triangles drawn from it (if there are any).
        private static Vector3 PaintingNormal(Mesh mesh, MeshRenderer draw, Rect? spot)
        {
            var sum = Vector3.zero;
            var v = mesh.vertices;
            var uv = mesh.uv;
            for (int pass = spot.HasValue && uv.Length == v.Length ? 0 : 1; pass < 2 && sum == Vector3.zero; pass++)
            {
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var material = draw != null && s < draw.sharedMaterials.Length ? draw.sharedMaterials[s] : null;
                    if (material != null && material.name.StartsWith("Wood")) continue;
                    var tris = mesh.GetTriangles(s);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        if (pass == 0)
                        {
                            var middle = (uv[tris[i]] + uv[tris[i + 1]] + uv[tris[i + 2]]) / 3f;
                            var near = spot.Value;
                            near.xMin -= 0.002f; near.yMin -= 0.002f; near.xMax += 0.002f; near.yMax += 0.002f;
                            if (!near.Contains(middle)) continue;
                        }
                        sum += Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
                    }
                }
            }
            return sum.sqrMagnitude > 0f ? sum.normalized : Vector3.forward;
        }

        // The frame's own outline: its back board (the biggest flat face looking at the wall), so trim on the frame
        // (Picture_Hang_1's corner blocks) doesn't stretch the glow round it.
        private static Bounds FrameBack(Mesh mesh)
        {
            var v = mesh.vertices;
            var tris = mesh.triangles;
            var areaAt = new Dictionary<int, float>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                var n = Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
                if (n.sqrMagnitude <= 0f || n.normalized.z > -0.9f) continue;
                int at = Mathf.RoundToInt(v[tris[i]].z * 1000f);
                areaAt[at] = (areaAt.TryGetValue(at, out var a) ? a : 0f) + n.magnitude * 0.5f;
            }
            int plane = 0;
            float most = -1f;
            foreach (var pair in areaAt) if (pair.Value > most) { most = pair.Value; plane = pair.Key; }
            if (most <= 0f) return mesh.bounds;
            bool any = false;
            var b = new Bounds();
            for (int i = 0; i < tris.Length; i += 3)
            {
                var n = Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
                if (n.sqrMagnitude <= 0f || n.normalized.z > -0.9f || Mathf.RoundToInt(v[tris[i]].z * 1000f) != plane) continue;
                for (int k = 0; k < 3; k++)
                {
                    if (!any) { b = new Bounds(v[tris[i + k]], Vector3.zero); any = true; }
                    else b.Encapsulate(v[tris[i + k]]);
                }
            }
            return b;
        }

        // The model's materials go to the game's own, by name: as Picture_Hang_4_Broken.fbx's do, and as the whole
        // picture's own model's do (Picture_Hang_1..3 are on Bedroom_Furniture_Set1 -> Bedroom_Set_1_Mat).
        private static string MapMaterials(string modelPath, string wholePath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null) return modelPath + ": materials not mapped";
            var sources = new List<ModelImporter>();
            if (AssetImporter.GetAtPath(MappedModel) is ModelImporter mapped) sources.Add(mapped);
            var whole = AssetDatabase.LoadAssetAtPath<GameObject>(wholePath);
            if (whole != null && whole.TryGetComponent(out MeshFilter wholeMesh) && wholeMesh.sharedMesh != null &&
                AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(wholeMesh.sharedMesh)) is ModelImporter own)
                sources.Add(own);
            var have = new HashSet<AssetImporter.SourceAssetIdentifier>(importer.GetExternalObjectMap().Keys);
            int added = 0;
            foreach (var source in sources)
            {
                foreach (var pair in source.GetExternalObjectMap())
                {
                    if (pair.Key.type != typeof(Material) || have.Contains(pair.Key) || pair.Value == null) continue;
                    importer.AddRemap(pair.Key, pair.Value);
                    have.Add(pair.Key);
                    added++;
                }
            }
            if (added > 0) importer.SaveAndReimport();
            return System.IO.Path.GetFileName(modelPath) + ": " + added + " materials mapped";
        }

        // ---------- On a picture ----------

        private static string ApplyToPicture(string picturePath, GameObject glow, GameObject space, Material backdrop)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(picturePath);
            // Open in Prefab Mode with nothing unsaved: it's set up right there (what's shown stays what's saved).
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            bool inStage = stage != null && stage.assetPath == picturePath;
            if (inStage && stage.scene.isDirty)
                return name + " has unsaved changes in Prefab Mode: save or close it, then run this again";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(picturePath) == null) return picturePath + " missing";
            var root = inStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(picturePath);
            var done = new List<string>();
            try
            {
                // The old glow (and its "shadow"), and a glow from an earlier run, go.
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || t == root.transform) continue;
                    if (t.name == "GrowthParticles" || t.name == "Picture_Puzzle_Glow")
                    {
                        done.Add("removed " + t.name);
                        Object.DestroyImmediate(t.gameObject);
                    }
                }
                var glowObject = (GameObject)PrefabUtility.InstantiatePrefab(glow, root.transform);
                // On the frame, as it sits in the prefab; shaped to Picture_Hang_4's frame, so stretched to this one's.
                var frame = FindChild(root.transform, root.name);
                var frameMesh = frame != null && frame.TryGetComponent(out MeshFilter frameFilter) ? frameFilter.sharedMesh : null;
                var glowScale = Vector3.one;
                if (frameMesh != null)
                {
                    var outline = FrameBack(frameMesh);
                    var middle = frame.TransformPoint(new Vector3(outline.center.x, outline.center.y, 0f));
                    glowObject.transform.localPosition = root.transform.InverseTransformPoint(middle);
                    glowObject.transform.localRotation = Quaternion.Inverse(root.transform.rotation) * frame.rotation;
                    glowScale = new Vector3(outline.size.x / GlowFrame.x, outline.size.y / GlowFrame.y, 1f);
                }
                else
                {
                    glowObject.transform.localPosition = Vector3.zero;
                    glowObject.transform.localRotation = Quaternion.identity;
                    if (root.TryGetComponent(out BoxCollider box))
                        glowScale = new Vector3(box.size.x / GlowFrame.x, box.size.y / GlowFrame.y, 1f);
                }
                glowObject.transform.localScale = glowScale;
                done.Add("glow in (scale " + glowScale.x.ToString("0.##") + " x " + glowScale.y.ToString("0.##") + ")");

                // The child draws the same frame: the root's drawing would draw it twice (once with the wrong texture).
                if (frame != null && root.TryGetComponent(out MeshRenderer own) && frame.GetComponent<MeshRenderer>() != null && own.enabled)
                {
                    own.enabled = false;
                    done.Add("root's frame drawing off (the child draws it)");
                }

                var puzzle = root.GetComponent<PicturePuzzle>();
                bool added = puzzle == null;
                if (added)
                {
                    puzzle = root.AddComponent<PicturePuzzle>();
                    done.Add("Picture Puzzle added");
                }
                var so = new SerializedObject(puzzle);
                // A bigger puzzle's own rules, set once (when it's added: changes made in the Inspector later stay).
                if (added && Rules.TryGetValue(name, out var rules))
                {
                    so.FindProperty("startPlaced").intValue = rules.stay;
                    so.FindProperty("timeLimit").floatValue = rules.seconds;
                    so.FindProperty("rewardCandies").intValue = rules.candies;
                    done.Add(rules.stay + " stay, " + rules.seconds + " s, " + rules.candies + " candies");
                }
                var pieces = new SortedDictionary<int, Transform>();
                GameObject complete = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(t.name, @"_Puzzle_(\d+)$");
                    if (m.Success) pieces[int.Parse(m.Groups[1].Value)] = t;
                    if (t.name.EndsWith("_Complete")) complete = t.gameObject;
                }
                // The whole painting shows only when it's done (it'd sit on the pieces).
                if (complete != null) complete.SetActive(false);
                var list = so.FindProperty("pieces");
                list.arraySize = pieces.Count;
                int i = 0;
                foreach (var t in pieces.Values) list.GetArrayElementAtIndex(i++).objectReferenceValue = t;
                so.FindProperty("complete").objectReferenceValue = complete;
                so.FindProperty("glow").objectReferenceValue = glowObject;
                so.FindProperty("spacePrefab").objectReferenceValue = space;
                so.FindProperty("backdropMaterial").objectReferenceValue = backdrop;
                so.FindProperty("spaceDim").floatValue = 0.97f;
                so.FindProperty("showDistance").floatValue = 1.2f;
                so.FindProperty("grabReach").floatValue = 0.12f;
                // The grab beam: plain white (its colour comes from the beam), additive.
                so.FindProperty("rayMaterial").objectReferenceValue = DangerFlameSetupMenu.MakeMaterial("Picture Grab Beam", "", true);
                var fireworks = AssetDatabase.LoadAssetAtPath<GameObject>(FireworkSetupMenu.PrefabPath);
                if (fireworks == null) fireworks = FireworkSetupMenu.Build();
                so.FindProperty("winFireworks").objectReferenceValue = fireworks;
                var candies = so.FindProperty("candyPrefabs");
                var candyList = new List<GameObject>();
                for (int n = 1; n <= 8; n++)
                {
                    var c = AssetDatabase.LoadAssetAtPath<GameObject>(CandyFolder + "Candy_" + n + ".prefab");
                    if (c != null) candyList.Add(c);
                }
                candies.arraySize = candyList.Count;
                for (int n = 0; n < candyList.Count; n++) candies.GetArrayElementAtIndex(n).objectReferenceValue = candyList[n];
                so.FindProperty("bugPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BugPath);
                var effects = so.FindProperty("breakEffects");
                var effectPaths = new[] { Lana + "Hit_Blast_BLack.prefab", Lana + "Smoke_Blast.prefab" };
                effects.arraySize = effectPaths.Length;
                for (int n = 0; n < effectPaths.Length; n++)
                    effects.GetArrayElementAtIndex(n).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(effectPaths[n]);
                Sound(so, "startSound", "whoosh.wav");
                Sound(so, "snapSound", "wood_get_hit_1.wav");
                Sound(so, "turnSound", "wood_get_hit_2.wav");
                Sound(so, "wrongSound", "shop_buy_fail.wav");
                Sound(so, "tickSound", "bomb_beep.wav");
                Sound(so, "winSound", "A_gentle_miracle_sound_2.wav");
                Sound(so, "breakSound", "wood_furniture_break_complete.wav");
                Sound(so, "buzzSound", "bee_fly.wav");
                Music(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                done.Add(pieces.Count + " pieces, complete " + (complete != null ? complete.name : "MISSING") + ", " + candyList.Count + " candy kinds");
                PrefabUtility.SaveAsPrefabAsset(root, picturePath);
            }
            finally
            {
                if (!inStage) PrefabUtility.UnloadPrefabContents(root);
            }
            return name + ": " + string.Join(", ", done) + (inStage ? " (set up in Prefab Mode and saved)" : "");
        }

        private static void Sound(SerializedObject so, string field, string file)
        {
            so.FindProperty(field).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + file);
        }

        // The user's puzzle songs (ElevenLabs, 2026-10-28): two of 60 s, two of 90 s; each puzzle plays one whose length
        // fits its Time Limit. Set where the list is empty (an edited list stays), streamed like the game's other songs.
        public static void Music(SerializedObject so)
        {
            var list = so.FindProperty("music");
            if (list == null || list.arraySize > 0) return;
            var songs = PuzzleSongs.Select(file => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + file)).Where(c => c != null).ToArray();
            list.arraySize = songs.Length;
            for (int n = 0; n < songs.Length; n++)
            {
                list.GetArrayElementAtIndex(n).objectReferenceValue = songs[n];
                if (!(AssetImporter.GetAtPath(Sfx + songs[n].name + ".wav") is AudioImporter importer)) continue;
                var settings = importer.defaultSampleSettings;
                if (settings.loadType == AudioClipLoadType.Streaming) continue;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static readonly string[] PuzzleSongs =
        {
            "Puzzle_Clock_Rush_60_1.wav", "Puzzle_Clock_Rush_60_2.wav", "Puzzle_Countdown_90_1.wav", "Last_Piece_Standing_90_2.wav",
        };

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root)
                if (child.name == name) return child;
            return null;
        }
    }
}
