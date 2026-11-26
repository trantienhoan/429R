using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Bonus;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bonus > Set Up Whack-a-Pumpkin (Special Stage Type 4, user 2026-11-23). Re-runnable. It builds:
    /// - Prefabs/Bonus/Whack/Whack_Machine: a STAND-IN machine until the user's model: a half-ring cabinet (PlayGround
    ///   wood and metal) round the stand spot, 20 holes (8 near at 0.6 m, 12 far at 0.94 m, tops at 0.82 m), a pumpkin
    ///   head in each (looks: pumpkin_0, gold, rainbow, time = green with its "+2s" label, Skull), two mallet posts, a
    ///   scoreboard and a glowing ring.
    /// - Prefabs/Bonus/Whack/Whack_Hammer: the tool hammer without its breaking, damage and effects (a stand-in mallet).
    /// - Prefabs/Bonus/Whack/Whack_Round: the round (its tuned numbers are kept on a re-run; parts and sounds refilled).
    /// - The scene: GAMESTAGES "Bonus Round PR" (after the Playroom) plays Whack_Round instead of the slime game
    ///   (Paint_Bees_Round stays in the project, unplugged). The user saves the scene. Running "Set Up Paint Bees Bonus"
    ///   again would put the slime game back.
    /// </summary>
    public static class WhackSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Bonus/Whack";
        private const string MachinePath = Folder + "/Whack_Machine.prefab";
        private const string HammerPath = Folder + "/Whack_Hammer.prefab";
        private const string RoundPath = Folder + "/Whack_Round.prefab";
        private const string MeshFolder = "Assets/Models/Bonus/Whack";
        private const string MeshPath = MeshFolder + "/Whack_Standin_Meshes.asset";
        private const string MaterialFolder = "Assets/Materials/Whack";
        // The user's machine (Exports/Whack/Whack_Machine.blend, collection "Whack_Machine (export)"). When it's there it
        // replaces the stand-in: the heads go to its Hole_00 ... Hole_19 empties, the mallets to Post_L / Post_R, the
        // scoreboard text to Board, the candies out of Candy_Spout.
        private const string ModelPath = MeshFolder + "/Whack_Machine.fbx";
        // The user's wooden mallet (collection "Whack_Mallet (export)"; pivot = the grip, Mallet_Strike_Point in its head).
        private const string MalletPath = MeshFolder + "/Whack_Mallet.fbx";

        private const string GameStagesState = "Bonus Round PR";

        // The layout (metres, machine space: the stand spot at the origin, +Z ahead).
        public const float TableHeight = 0.82f;
        public const float InnerRadius = 0.42f;
        public const float OuterRadius = 1.12f;
        public const float HalfSpan = 90f;
        public const float NearRadius = 0.645f;
        public const float FarRadius = 0.925f;
        private static readonly float[] NearAngles = { -70f, -50f, -30f, -10f, 10f, 30f, 50f, 70f };
        // The user's machine (2026-11-23): evenly from -79.5 to +79.5 deg, clear of its end bands at +-87 deg.
        private static readonly float[] FarAngles = Enumerable.Range(0, 12).Select(i => -79.5f + 159f * i / 11f).ToArray();
        private const float HoleDiameter = 0.17f;
        private const float HeadWidth = 0.15f;
        // The pumpkin models face +Z here; turn them by this (degrees) if their faces point elsewhere.
        private const float FaceYaw = 0f;

        [MenuItem("Tools/429 Game/Bonus/Set Up Whack-a-Pumpkin")]
        public static void Run()
        {
            var log = new List<string>();
            MakeFolder(Folder);
            MakeFolder(MeshFolder);
            MakeFolder(MaterialFolder);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var meshes = Meshes(log);
                var hammer = BuildHammer(preview, log, out var headAxis, out float lowest);
                var machine = BuildMachine(preview, meshes, headAxis, lowest, log);
                BuildRound(machine, hammer, log);
                log.AddRange(HookScene());
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Whack-a-Pumpkin setup]\n- " + string.Join("\n- ", log));
        }

        // ---------- Meshes ----------

        private class StandinMeshes
        {
            public Mesh cabinet, rim, ring;
        }

        private static StandinMeshes Meshes(List<string> log)
        {
            var cabinet = Sector("Whack_Cabinet", InnerRadius, OuterRadius, HalfSpan, 0f, TableHeight, 64, true);
            var rim = Sector("Whack_Rim", OuterRadius - 0.012f, OuterRadius + 0.018f, HalfSpan + 0.6f, TableHeight - 0.05f, TableHeight + 0.012f, 64, true);
            var ring = Ring("Whack_Stand_Ring", 0.25f, 0.31f, 64);
            if (File.Exists(MeshPath)) AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(cabinet, MeshPath);
            AssetDatabase.AddObjectToAsset(rim, MeshPath);
            AssetDatabase.AddObjectToAsset(ring, MeshPath);
            AssetDatabase.ImportAsset(MeshPath);
            log.Add("Stand-in meshes: " + MeshPath);
            return new StandinMeshes { cabinet = cabinet, rim = rim, ring = ring };
        }

        // A solid ring sector from radius rIn to rOut, half-span degrees each side of +Z, from y0 to y1: submesh 0 the top,
        // submesh 1 the walls (outer, inner and the two ends). Each face has its own corners (hard edges).
        private static Mesh Sector(string name, float rIn, float rOut, float half, float y0, float y1, int seg, bool ends)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var top = new List<int>();
            var walls = new List<int>();
            Vector3 P(float r, float a, float y) => new(Mathf.Sin(a * Mathf.Deg2Rad) * r, y, Mathf.Cos(a * Mathf.Deg2Rad) * r);
            float A(int i) => -half + 2f * half * i / seg;

            for (int i = 0; i < seg; i++)
            {
                float a0 = A(i), a1 = A(i + 1);
                // Top.
                Quad(v, uv, top, P(rIn, a0, y1), P(rOut, a0, y1), P(rOut, a1, y1), P(rIn, a1, y1), Vector3.up, true, 0f);
                // Outer wall (faces out) and inner wall (faces the stand spot).
                var outward = P(1f, (a0 + a1) * 0.5f, 0f);
                Quad(v, uv, walls, P(rOut, a0, y0), P(rOut, a1, y0), P(rOut, a1, y1), P(rOut, a0, y1), outward, false, rOut * a0 * Mathf.Deg2Rad);
                Quad(v, uv, walls, P(rIn, a0, y0), P(rIn, a1, y0), P(rIn, a1, y1), P(rIn, a0, y1), -outward, false, rIn * a0 * Mathf.Deg2Rad);
            }
            if (ends)
            {
                foreach (float a in new[] { -half, half })
                {
                    var tangent = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, -Mathf.Sin(a * Mathf.Deg2Rad)) * Mathf.Sign(a);
                    Quad(v, uv, walls, P(rIn, a, y0), P(rOut, a, y0), P(rOut, a, y1), P(rIn, a, y1), tangent, false, 0f);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(walls, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A flat ring on the floor (all round).
        private static Mesh Ring(string name, float rIn, float rOut, int seg)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a0 = 360f * i / seg, a1 = 360f * (i + 1) / seg;
                Vector3 P(float r, float a) => new(Mathf.Sin(a * Mathf.Deg2Rad) * r, 0f, Mathf.Cos(a * Mathf.Deg2Rad) * r);
                int b = v.Count;
                v.Add(P(rIn, a0)); v.Add(P(rOut, a0)); v.Add(P(rOut, a1)); v.Add(P(rIn, a1));
                uv.Add(new Vector2(0f, (float)i / seg)); uv.Add(new Vector2(1f, (float)i / seg));
                uv.Add(new Vector2(1f, (float)(i + 1) / seg)); uv.Add(new Vector2(0f, (float)(i + 1) / seg));
                Tri(v, tris, b, b + 1, b + 2, Vector3.up);
                Tri(v, tris, b, b + 2, b + 3, Vector3.up);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A quad (a b c d round its edge) facing 'want'; top faces get planar UVs, walls (along, height).
        private static void Quad(List<Vector3> v, List<Vector2> uv, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                                 Vector3 want, bool planar, float along)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            if (planar)
            {
                uv.Add(new Vector2(a.x, a.z)); uv.Add(new Vector2(b.x, b.z)); uv.Add(new Vector2(c.x, c.z)); uv.Add(new Vector2(d.x, d.z));
            }
            else
            {
                float w = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
                uv.Add(new Vector2(along, a.y)); uv.Add(new Vector2(along + w, b.y)); uv.Add(new Vector2(along + w, c.y)); uv.Add(new Vector2(along, d.y));
            }
            Tri(v, tris, i, i + 1, i + 2, want);
            Tri(v, tris, i, i + 2, i + 3, want);
        }

        // A triangle wound so its front faces 'want' (Unity: front = Cross(b - a, c - a)).
        private static void Tri(List<Vector3> v, List<int> tris, int a, int b, int c, Vector3 want)
        {
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), want) < 0f) (b, c) = (c, b);
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        // ---------- Materials ----------

        private static Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        private static Material MakeMaterial(string name, Material copyOf, Color colour, bool unlit)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = copyOf != null ? new Material(copyOf) : new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            else if (copyOf != null) m.CopyPropertiesFromMaterial(copyOf);
            m.name = name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- The mallet ----------

        private static WhackHammer BuildHammer(Scene preview, List<string> log, out Vector3 headAxis, out float lowest)
        {
            headAxis = Vector3.forward;
            lowest = 0f;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapon/Hammer_Tool.prefab");
            if (source == null) { log.Add("MISSING Hammer_Tool"); return null; }
            var go = Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(go, preview);
            go.name = "Whack_Hammer";
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = source.transform.localScale;

            // Only the grab, the body and the look: no breaking, no damage, no effects.
            foreach (var fsm in go.GetComponentsInChildren<PlayMakerFSM>(true)) Object.DestroyImmediate(fsm);
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    string type = c.GetType().Name;
                    if (type is "EnemyHitDamage" or "TriggeredBoost" or "WeaponPower" or "HitRumble" or "AudioSource")
                        Object.DestroyImmediate(c);
                }
            }
            foreach (string child in new[] { "Smoke_Blast", "confetti_small", "Tool_Hammer_Drop_Pos" })
            {
                var t = FindChild(go.transform, child);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            var body = go.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            // The striking head: the bigger of its two box colliders (the other is the handle). Measured from the boxes
            // themselves: a collider's bounds aren't worked out in a preview scene.
            Vector3 Corner(BoxCollider box, int i) => box.transform.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
            float Volume(BoxCollider box)
            {
                var s = Vector3.Scale(box.size, box.transform.lossyScale);
                return Mathf.Abs(s.x * s.y * s.z);
            }
            var boxes = go.GetComponentsInChildren<BoxCollider>(true).OrderByDescending(Volume).ToList();
            Vector3 strike = new(0f, 0f, 0.25f);
            if (boxes.Count >= 2)
            {
                var headCentre = boxes[0].transform.TransformPoint(boxes[0].center);
                var handleCentre = boxes[1].transform.TransformPoint(boxes[1].center);
                strike = go.transform.InverseTransformPoint(headCentre);
                headAxis = (headCentre - handleCentre).normalized;
            }
            else if (boxes.Count == 1) strike = go.transform.InverseTransformPoint(boxes[0].transform.TransformPoint(boxes[0].center));

            // Standing on its post handle down, head up: its lowest point then (metres; the hammer is at the origin,
            // unturned), for the post height.
            var up = Quaternion.FromToRotation(headAxis, Vector3.up);
            lowest = float.MaxValue;
            foreach (var box in boxes)
                for (int i = 0; i < 8; i++) lowest = Mathf.Min(lowest, (up * (Corner(box, i) - go.transform.position)).y);
            if (lowest == float.MaxValue) lowest = -0.3f;
            float length = 0f;
            foreach (var box in boxes)
                for (int i = 0; i < 8; i++) length = Mathf.Max(length, (up * (Corner(box, i) - go.transform.position)).y - lowest);
            log.Add($"Whack_Hammer: {boxes.Count} boxes, {length:F2} m long, lowest {lowest:F2} m under its origin when upright");

            // The user's wooden mallet, held like the tool hammer: its grip where the tool hammer's hand grip (Attach) is,
            // its handle pointing to its head the same way, its striking faces where the tool hammer's are. The tool
            // hammer's look, bones and boxes go; the mallet gets two capsules (handle, head) shaped from its mesh.
            var mallet = PrepareMallet(log);
            bool wooden = false;
            var grab = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (mallet != null && boxes.Count >= 2 && grab != null && grab.attachTransform != null)
            {
                var oldAttach = grab.attachTransform;
                var attachPos = go.transform.InverseTransformPoint(oldAttach.position);
                var attachRot = Quaternion.Inverse(go.transform.rotation) * oldAttach.rotation;
                var toolHead = go.transform.InverseTransformPoint(boxes[0].transform.TransformPoint(boxes[0].center));
                var toolHandle = (toolHead - attachPos).normalized;
                var toolStrike = LongestAxis(boxes[0], go.transform, toolHandle);
                foreach (string part in new[] { "Tool_Hammer", "Tool_Hammer_Armature" })
                {
                    var t = FindChild(go.transform, part);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
                var animator = go.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator);
                var attach = Child(go.transform, "Attach", attachPos);
                attach.localRotation = attachRot;
                grab.attachTransform = attach;
                grab.colliders.Clear();

                var inst = Object.Instantiate(mallet);
                inst.name = "Mallet_Model";
                inst.transform.SetParent(go.transform, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                // The mallet's own frame (in its model space): the grip at its origin, the handle towards its head.
                var pts = new List<Vector3>();
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null)
                        foreach (var v in mf.sharedMesh.vertices) pts.Add(inst.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
                var marker = FindChild(inst.transform, "Mallet_Strike_Point");
                var aim = marker != null ? inst.transform.InverseTransformPoint(marker.position) : pts.OrderByDescending(v => v.sqrMagnitude).First();
                var h = aim.normalized;
                float Perp(Vector3 v) => (v - h * Vector3.Dot(v, h)).magnitude;
                var headPts = pts.Where(v => Perp(v) > 0.035f && Vector3.Dot(v, h) > 0.05f).ToList();
                if (headPts.Count == 0) headPts = pts.Where(v => Vector3.Dot(v, h) > Vector3.Dot(aim, h) - 0.05f).ToList();
                var hb = new Bounds(headPts[0], Vector3.zero);
                foreach (var v in headPts) hb.Encapsulate(v);
                var headCentre = hb.center;
                // The striking axis: of the axes square to the handle, the one the head is longest along.
                var candidates = new[] { Vector3.right, Vector3.up, Vector3.forward }
                    .Select(a => (a - h * Vector3.Dot(a, h)))
                    .Where(a => a.sqrMagnitude > 0.5f)
                    .Select(a => a.normalized).ToList();
                float Extent(Vector3 axis) => headPts.Max(v => Vector3.Dot(v, axis)) - headPts.Min(v => Vector3.Dot(v, axis));
                var strikeM = candidates.OrderByDescending(Extent).First();
                var sideM = Vector3.Cross(h, strikeM).normalized;
                float headLength = Extent(strikeM);
                float headRadius = Extent(sideM) * 0.5f;
                float pMin = pts.Min(v => Vector3.Dot(v, h));
                float headBottom = Vector3.Dot(headCentre, h) - headRadius;
                float handleRadius = Mathf.Clamp(pts.Where(v => Vector3.Dot(v, h) < headBottom).Select(Perp).DefaultIfEmpty(0.02f).Max(), 0.012f, 0.035f);
                var turn = Quaternion.LookRotation(toolHandle, toolStrike) * Quaternion.Inverse(Quaternion.LookRotation(h, strikeM));
                inst.transform.localRotation = turn;
                inst.transform.localPosition = attachPos;

                var handleCol = Child(inst.transform, "Collider_Handle", h * ((pMin + headBottom) * 0.5f));
                handleCol.localRotation = Quaternion.LookRotation(h, strikeM);
                var hc = handleCol.gameObject.AddComponent<CapsuleCollider>();
                hc.direction = 2;
                hc.radius = handleRadius;
                hc.height = headBottom - pMin + handleRadius;
                var headCol = Child(inst.transform, "Collider_Head", headCentre);
                headCol.localRotation = Quaternion.LookRotation(strikeM, h);
                var cc = headCol.gameObject.AddComponent<CapsuleCollider>();
                cc.direction = 2;
                cc.radius = headRadius;
                cc.height = Mathf.Max(headLength, headRadius * 2f);

                strike = go.transform.InverseTransformPoint(inst.transform.TransformPoint(headCentre));
                headAxis = toolHandle;
                var upright = Quaternion.FromToRotation(headAxis, Vector3.up);
                lowest = pts.Min(v => (upright * go.transform.InverseTransformPoint(inst.transform.TransformPoint(v))).y);
                wooden = true;
                log.Add($"Whack_Hammer = the user's wooden mallet: {(Vector3.Dot(aim, h) - pMin + headRadius):F2} m long, head {headLength:F2} m x {headRadius * 2f:F2} m, " +
                        $"handle radius {handleRadius:F3}; held at the tool hammer's grip; lowest {lowest:F2} m under its origin upright");
            }

            var whack = go.AddComponent<WhackHammer>();
            var so = new SerializedObject(whack);
            so.FindProperty("strikePoint").vector3Value = strike;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, HammerPath);
            Object.DestroyImmediate(go);
            log.Add($"Whack_Hammer ({(wooden ? "wooden mallet" : "stand-in = the tool hammer")}, nothing breaks): strike point {strike}, head axis {headAxis}");
            return saved != null ? saved.GetComponent<WhackHammer>() : null;
        }

        // ---------- The machine ----------

        private static WhackMachine BuildMachine(Scene preview, StandinMeshes meshes, Vector3 headAxis, float lowest, List<string> log)
        {
            var woodTop = Mat("Assets/Materials/PlayGround/PG_Wood_Furniture_Mat.mat");
            var woodBoards = Mat("Assets/Materials/PlayGround/PG_Wood_Boards_Mat.mat");
            var metal = Mat("Assets/Materials/PlayGround/PG_Metal_Mat.mat");
            var pumpkinMat = Mat("Assets/Materials/Bedroom_Set_1_Mat.mat");
            var gold = Mat("Assets/Prefabs/Items/Revive_Coin/Revive_Coin_Gold.mat");
            var ringMat = Mat("Assets/Materials/Paint_Bees/Paint_Bees_Ring_Mat.mat");
            var holeMat = MakeMaterial("Whack_Hole_Mat", null, new Color(0.03f, 0.02f, 0.04f), true);
            var clockMat = MakeMaterial("Whack_Pumpkin_Clock_Mat", pumpkinMat, new Color(0.55f, 1f, 0.7f), false);
            var rainbowMat = MakeMaterial("Whack_Pumpkin_Rainbow_Mat", pumpkinMat, Color.white, false);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/socake/Socake SDF.asset");
            var labelMat = LabelMaterial(font);
            var pumpkin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Bedroom_Stuffs/pumpkin_0.fbx");
            var skull = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Bedroom_Stuffs/Skull.fbx");

            var root = new GameObject("Whack_Machine");
            SceneManager.MoveGameObjectToScene(root, preview);
            var machine = root.AddComponent<WhackMachine>();
            var body = Child(root.transform, "Body", Vector3.zero);
            var bodyRb = body.gameObject.AddComponent<Rigidbody>();
            bodyRb.isKinematic = true;
            bodyRb.useGravity = false;

            // The user's model, or the stand-in cabinet and its metal rim.
            var model = PrepareModel(holeMat, log);
            Transform modelRoot = null;
            if (model != null)
            {
                var inst = Object.Instantiate(model);
                inst.name = "Model (" + model.name + ")";
                inst.transform.SetParent(body, false);
                // Its holes must be ahead of the stand spot (+Z): turned round if the export came out backwards.
                var front = FindChild(inst.transform, "Hole_13");
                if (front != null && body.InverseTransformPoint(front.position).z < 0f) inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                modelRoot = inst.transform;
            }
            else
            {
                var cab = Child(body, "Cabinet (stand-in)", Vector3.zero);
                cab.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes.cabinet;
                cab.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { woodTop, woodBoards };
                cab.gameObject.AddComponent<MeshCollider>().sharedMesh = meshes.cabinet;
                var rim = Child(body, "Rim (stand-in)", Vector3.zero);
                rim.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes.rim;
                rim.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { metal, metal };
            }
            // A spot of the model (its empty, in the body's space), or null (then the stand-in's).
            Vector3? ModelSpot(string spotName)
            {
                var t = modelRoot != null ? FindChild(modelRoot, spotName) : null;
                return t != null ? body.InverseTransformPoint(t.position) : (Vector3?)null;
            }

            // The holes and their heads.
            var holes = Child(body, "Holes", Vector3.zero);
            var heads = new List<WhackHead>();
            int n = 0;
            foreach (var (radius, angles) in new[] { (NearRadius, NearAngles), (FarRadius, FarAngles) })
            {
                foreach (float a in angles)
                {
                    var fromModel = ModelSpot("Hole_" + n.ToString("00"));
                    if (modelRoot != null && fromModel == null) log.Add("the model has no Hole_" + n.ToString("00") + ": the stand-in spot is used");
                    var at = fromModel ?? new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * radius, TableHeight, Mathf.Cos(a * Mathf.Deg2Rad) * radius);
                    heads.Add(BuildHead(holes, "Hole_" + n.ToString("00"), at, pumpkin, skull, pumpkinMat, gold, rainbowMat, clockMat,
                                        font, labelMat, metal, holeMat, modelRoot == null));
                    n++;
                }
            }

            // The mallet posts, one each side just behind the table's ends.
            var spots = new List<Transform>();
            float capTop = 0.97f;
            foreach (float side in new[] { -1f, 1f })
            {
                var modelPost = ModelSpot(side < 0f ? "Post_L" : "Post_R");
                if (modelPost != null)
                {
                    // The model's post top (the mallet floats there when the model has no post).
                    var mspot = Child(body, side < 0f ? "Hammer_Spot_L" : "Hammer_Spot_R", modelPost.Value + Vector3.up * (0.02f - lowest));
                    mspot.localRotation = Quaternion.AngleAxis(20f * side, Vector3.up) * Quaternion.FromToRotation(headAxis, Vector3.up);
                    spots.Add(mspot);
                    continue;
                }
                var post = Child(body, side < 0f ? "Post_L" : "Post_R", new Vector3(0.55f * side, 0f, -0.15f));
                Primitive(PrimitiveType.Cylinder, post, "Pole", new Vector3(0f, capTop * 0.5f, 0f), new Vector3(0.05f, capTop * 0.5f, 0.05f), metal, true);
                Primitive(PrimitiveType.Cylinder, post, "Cap", new Vector3(0f, capTop, 0f), new Vector3(0.13f, 0.015f, 0.13f), woodTop, true);
                var spot = Child(post, side < 0f ? "Hammer_Spot_L" : "Hammer_Spot_R", new Vector3(0f, capTop + 0.02f - lowest, 0f));
                // Handle down, head up, turned a little outwards.
                spot.localRotation = Quaternion.AngleAxis(20f * side, Vector3.up) * Quaternion.FromToRotation(headAxis, Vector3.up);
                spots.Add(spot);
            }

            // The scoreboard behind the far row.
            // The texts stand just in front of the panel's face (the model's Board empty is its centre; the stand-in's
            // panel face is at z 1.405 with its centre 1.55 high).
            var boardFace = ModelSpot("Board");
            var board = Child(body, "Scoreboard", boardFace != null
                ? new Vector3(boardFace.Value.x, boardFace.Value.y - 1.55f, boardFace.Value.z - 0.005f)
                : new Vector3(0f, 0f, 1.4f));
            if (boardFace == null)
            {
                foreach (float side in new[] { -1f, 1f })
                    Primitive(PrimitiveType.Cylinder, board, side < 0f ? "Leg_L" : "Leg_R", new Vector3(0.55f * side, 0.98f, 0.02f), new Vector3(0.05f, 0.98f, 0.05f), metal, true);
                Primitive(PrimitiveType.Cube, board, "Panel", new Vector3(0f, 1.55f, 0.03f), new Vector3(1.24f, 0.84f, 0.05f), woodBoards, true);
                Primitive(PrimitiveType.Cube, board, "Frame_Top", new Vector3(0f, 1.98f, 0.03f), new Vector3(1.3f, 0.04f, 0.07f), metal, false);
                Primitive(PrimitiveType.Cube, board, "Frame_Bottom", new Vector3(0f, 1.12f, 0.03f), new Vector3(1.3f, 0.04f, 0.07f), metal, false);
            }
            var title = Text(board, "Title", new Vector3(0f, 1.85f, -0.01f), "WHACK-A-PUMPKIN", 0.55f, new Color(1f, 0.55f, 0.15f), font, 1.2f);
            var time = Text(board, "Time", new Vector3(0f, 1.66f, -0.01f), "60", 1.6f, new Color(1f, 0.85f, 0.3f), font, 1.2f);
            var message = Text(board, "Message", new Vector3(0f, 1.43f, -0.012f), "", 0.62f, Color.white, font, 1.2f);
            var candies = Text(board, "Candies", new Vector3(-0.38f, 1.22f, -0.01f), "CANDIES  0", 0.42f, Color.white, font, 0.5f);
            var combo = Text(board, "Combo", new Vector3(0f, 1.22f, -0.01f), "", 0.42f, new Color(0.5f, 1f, 0.5f), font, 0.4f);
            var best = Text(board, "Best", new Vector3(0.38f, 1.22f, -0.01f), "", 0.42f, new Color(1f, 0.82f, 0.25f), font, 0.5f);
            title.fontStyle = FontStyles.Bold;
            var modelSpout = ModelSpot("Candy_Spout");
            var spout = modelSpout != null ? Child(body, "Candy_Spout", modelSpout.Value) : Child(board, "Candy_Spout", new Vector3(0f, 2.1f, -0.15f));

            // The ring to stand on.
            var ring = Child(root.transform, "Stand_Ring", new Vector3(0f, 0.012f, 0f));
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes.ring;
            var ringRenderer = ring.gameObject.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = ringMat;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.gameObject.SetActive(false);

            var so = new SerializedObject(machine);
            so.FindProperty("body").objectReferenceValue = body;
            var headsProp = so.FindProperty("heads");
            headsProp.arraySize = heads.Count;
            for (int i = 0; i < heads.Count; i++) headsProp.GetArrayElementAtIndex(i).objectReferenceValue = heads[i];
            var spotsProp = so.FindProperty("hammerSpots");
            spotsProp.arraySize = spots.Count;
            for (int i = 0; i < spots.Count; i++) spotsProp.GetArrayElementAtIndex(i).objectReferenceValue = spots[i];
            so.FindProperty("candySpout").objectReferenceValue = spout;
            so.FindProperty("standRing").objectReferenceValue = ring.gameObject;
            so.FindProperty("timeText").objectReferenceValue = time;
            so.FindProperty("candiesText").objectReferenceValue = candies;
            so.FindProperty("comboText").objectReferenceValue = combo;
            so.FindProperty("bestText").objectReferenceValue = best;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool builtFromModel = modelRoot != null;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, MachinePath);
            Object.DestroyImmediate(root);
            log.Add(builtFromModel
                ? $"Whack_Machine from the user's model: {heads.Count} heads at its Hole empties, mallets at Post_L/R, text on Board, candies from Candy_Spout"
                : $"Whack_Machine (stand-in): {heads.Count} holes, 2 mallet posts, scoreboard; table top {TableHeight} m, near row {NearRadius} m, far row {FarRadius} m");
            return saved != null ? saved.GetComponent<WhackMachine>() : null;
        }

        private static WhackHead BuildHead(Transform parent, string name, Vector3 at, GameObject pumpkin, GameObject skull,
                                           Material pumpkinMat, Material gold, Material rainbow, Material clock,
                                           TMP_FontAsset font, Material labelMat, Material metal, Material hole, bool discs = true)
        {
            var root = Child(parent, name, at);
            // Facing the stand spot.
            var toSpot = new Vector3(-at.x, 0f, -at.z);
            root.localRotation = Quaternion.LookRotation(toSpot.normalized, Vector3.up);
            var rb = root.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // The hole: a metal rim and the dark inside, on the table top.
            // (The user's model has real holes and rings of its own.)
            if (discs)
            {
                Primitive(PrimitiveType.Cylinder, root, "Hole_Rim", new Vector3(0f, 0.002f, 0f), new Vector3(HoleDiameter + 0.03f, 0.003f, HoleDiameter + 0.03f), metal, false);
                Primitive(PrimitiveType.Cylinder, root, "Hole_Dark", new Vector3(0f, 0.004f, 0f), new Vector3(HoleDiameter, 0.003f, HoleDiameter), hole, false);
            }

            var body = Child(root, "Head", Vector3.zero);
            var looks = new GameObject[5];
            looks[0] = Look(body, "Look_Normal", pumpkin, pumpkinMat);
            looks[1] = Look(body, "Look_Gold", pumpkin, gold);
            looks[2] = Look(body, "Look_Rainbow", pumpkin, rainbow);
            looks[3] = Look(body, "Look_Clock", pumpkin, clock);
            looks[4] = Look(body, "Look_Skull", skull, null);
            // The time head shows the seconds it gives ("+2s", set as it pops) on its front, instead of a clock (user,
            // 2026-11-23). It turns to the player's eyes while up; here it faces the stand spot, tilted up to eye height.
            TextMeshPro label = null;
            if (looks[3] != null)
            {
                var go = new GameObject("Time_Label");
                go.transform.SetParent(looks[3].transform, false);
                go.transform.localPosition = new Vector3(0f, 0.035f, 0.1f);
                go.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.7f, -0.75f).normalized, Vector3.up);
                label = go.AddComponent<TextMeshPro>();
                if (font != null) label.font = font;
                if (labelMat != null) label.fontSharedMaterial = labelMat;
                label.text = "+2s";
                label.fontSize = 0.75f;
                label.fontStyle = FontStyles.Bold;
                label.color = new Color(1f, 0.97f, 0.8f);
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.rectTransform.sizeDelta = new Vector2(0.3f, 0.12f);
            }

            var zone = body.gameObject.AddComponent<SphereCollider>();
            zone.isTrigger = true;
            zone.radius = 0.1f;
            zone.center = new Vector3(0f, 0.07f, 0f);

            var head = root.gameObject.AddComponent<WhackHead>();
            var so = new SerializedObject(head);
            var looksProp = so.FindProperty("looks");
            looksProp.arraySize = looks.Length;
            for (int i = 0; i < looks.Length; i++) looksProp.GetArrayElementAtIndex(i).objectReferenceValue = looks[i];
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("hitZone").objectReferenceValue = zone;
            so.FindProperty("clockLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            return head;
        }

        // The time head's label material: the game's font (Socake) with a dark outline, so "+2s" reads on the green
        // pumpkin and in the dark stage (a TextMeshPro material preset next to the other Whack materials).
        private static Material LabelMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            const string name = "Whack_Time_Label_SDF_Mat";
            string path = MaterialFolder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(font.material);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(font.material);
            m.name = name;
            m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.06f, 0.1f, 0.04f, 1f));
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            ShaderUtilities.UpdateShaderRatios(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // The user's mallet model with the game's wood on it (Blender names Wood_Furniture / Wood_Furniture_Mat), or null.
        private static GameObject PrepareMallet(List<string> log)
        {
            if (!(AssetImporter.GetAtPath(MalletPath) is ModelImporter importer)) return null;
            var wood = Mat("Assets/Materials/Wood_Furniture_Mat.mat");
            var metal = Mat("Assets/Materials/Bedroom_Metal_Mat.mat");
            var map = new Dictionary<string, Material>
            {
                { "Wood_Furniture", wood }, { "Wood_Furniture_Mat", wood }, { "Bedroom_Metal_Mat", metal },
            };
            var have = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (var pair in map)
            {
                if (pair.Value == null) continue;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                if (have.TryGetValue(id, out var current) && current == pair.Value) continue;
                importer.AddRemap(id, pair.Value);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MalletPath);
            if (model != null)
            {
                var mats = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct();
                log.Add($"The user's mallet {MalletPath}: materials {string.Join(", ", mats)}");
            }
            return model;
        }

        // A box collider's longest side (in 'root' space), not along 'notAlong'.
        private static Vector3 LongestAxis(BoxCollider box, Transform root, Vector3 notAlong)
        {
            var axes = new[]
            {
                root.InverseTransformVector(box.transform.TransformVector(new Vector3(box.size.x, 0f, 0f))),
                root.InverseTransformVector(box.transform.TransformVector(new Vector3(0f, box.size.y, 0f))),
                root.InverseTransformVector(box.transform.TransformVector(new Vector3(0f, 0f, box.size.z))),
            };
            return axes.Where(a => Mathf.Abs(Vector3.Dot(a.normalized, notAlong)) < 0.7f).OrderByDescending(a => a.magnitude).First().normalized;
        }

        // The user's machine model with the game's materials on it (its Blender material names mapped to the project's
        // own materials), or null if there's none yet.
        private static GameObject PrepareModel(Material holeMat, List<string> log)
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer)) return null;
            var map = new Dictionary<string, Material>
            {
                { "Wood_Furniture_Mat", Mat("Assets/Materials/Wood_Furniture_Mat.mat") },
                { "Wood_Boards_Mat", Mat("Assets/Materials/Wood_Floor_Mat.mat") },
                { "Bedroom_Metal_Mat", Mat("Assets/Materials/Bedroom_Metal_Mat.mat") },
                { "Whack_Hole_Dark_Mat", holeMat },
            };
            var have = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (var pair in map)
            {
                if (pair.Value == null) { log.Add("MISSING material for " + pair.Key); continue; }
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                if (have.TryGetValue(id, out var current) && current == pair.Value) continue;
                importer.AddRemap(id, pair.Value);
                changed = true;
            }
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) return null;
            var size = Bounds(model).size;
            var mats = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct();
            log.Add($"The user's model {ModelPath}: {size.x:F2} x {size.y:F2} x {size.z:F2} m, materials {string.Join(", ", mats)}");
            return model;
        }

        // A model as a head look: Head Width across, its bottom just under the hole's top, centred.
        private static GameObject Look(Transform parent, string name, GameObject model, Material material)
        {
            if (model == null) return null;
            var holder = new GameObject(name);
            var inst = Object.Instantiate(model);
            inst.transform.SetParent(holder.transform, false);
            inst.transform.localRotation = Quaternion.Euler(0f, FaceYaw, 0f);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in inst.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (material != null) r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var b = Bounds(holder);
            float width = Mathf.Max(b.size.x, b.size.z);
            if (width > 0.0001f) inst.transform.localScale *= HeadWidth / width;
            b = Bounds(holder);
            inst.transform.localPosition += new Vector3(-b.center.x, -0.035f - b.min.y, -b.center.z);
            holder.transform.SetParent(parent, false);
            holder.SetActive(false);
            return holder;
        }

        private static Bounds Bounds(GameObject go)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ---------- The round ----------

        private static void BuildRound(WhackMachine machine, WhackHammer hammer, List<string> log)
        {
            GameObject root;
            bool fresh = !File.Exists(RoundPath);
            root = fresh ? new GameObject("Whack_Round") : PrefabUtility.LoadPrefabContents(RoundPath);
            var round = root.GetComponent<WhackRound>() ?? root.AddComponent<WhackRound>();
            var so = new SerializedObject(round);
            void Ref(string field, Object value)
            {
                var p = so.FindProperty(field);
                if (p == null) { log.Add("no field " + field); return; }
                p.objectReferenceValue = value;
                if (value == null) log.Add("MISSING for " + field);
            }
            T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
            const string Sfx = "Assets/Audio/SFX/";
            const string Ui = "Assets/Audio/Kenney Audio/kenney_interface-sounds/Audio/";
            Ref("stageSky", Load<GameObject>("Assets/Prefabs/Bases/Bonus_Stage_Sky.prefab"));
            Ref("chest", Load<GameObject>("Assets/Prefabs/Items/Chest/Treasure_Chest_PUMPKIN_EXTRA.prefab"));
            Ref("machine", machine);
            Ref("hammer", hammer);
            Ref("puffEffect", Load<GameObject>("Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab"));
            Ref("riseSound", Load<AudioClip>(Sfx + "magic_hat_spit_card.wav"));
            Ref("tickSound", Load<AudioClip>(Ui + "tick_002.ogg"));
            Ref("goSound", Load<AudioClip>(Sfx + "whoosh.wav"));
            Ref("timeUpSound", Load<AudioClip>(Ui + "error_006.ogg"));
            Ref("popSound", Load<AudioClip>(Ui + "pluck_001.ogg"));
            Ref("goldSound", Load<AudioClip>(Sfx + "ka_ching.wav"));
            Ref("rainbowSound", Load<AudioClip>(Sfx + "A_magical_glowing.wav"));
            Ref("clockSound", Load<AudioClip>(Ui + "confirmation_002.ogg"));
            Ref("skullSound", Load<AudioClip>(Sfx + "laughing_pumpkin_1.mp3"));
            Ref("missSound", Load<AudioClip>(Sfx + "Pumpkin_leuleu1.wav"));
            Ref("comboSound", Load<AudioClip>(Sfx + "fairy_yippy.wav"));
            // The gold frenzy's start (user, 2026-11-23: "Golden frenzy"): the user's bell.
            Ref("frenzySound", Load<AudioClip>(Sfx + "Clear,_resonant_bell_2.wav"));
            Ref("font", Load<TMP_FontAsset>("Assets/Fonts/socake/Socake SDF.asset"));
            Ref("sparkleEffect", Load<GameObject>("Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab"));
            Ref("winFireworks", Load<GameObject>("Assets/Prefabs/Light/Firework_Win.prefab"));
            var whacks = so.FindProperty("whackSounds");
            whacks.arraySize = 3;
            for (int i = 0; i < 3; i++) whacks.GetArrayElementAtIndex(i).objectReferenceValue = Load<AudioClip>(Sfx + "pumpkin_get_hit_" + (i + 1) + ".wav");

            // The song (user, 2026-11-23): the jigsaw puzzle's two 90 s songs (streamed), one at random.
            var songs = new[] { Load<AudioClip>(Sfx + "Puzzle_Countdown_90_1.wav"), Load<AudioClip>(Sfx + "Last_Piece_Standing_90_2.wav") }
                .Where(c => c != null).ToArray();
            if (songs.Length == 0) log.Add("MISSING the jigsaw's 90 s songs (music)");
            var music = so.FindProperty("music");
            music.arraySize = songs.Length;
            for (int i = 0; i < songs.Length; i++) music.GetArrayElementAtIndex(i).objectReferenceValue = songs[i];

            // 2026-11-23 (user: "time and gold pumpkin should be rare to come"): moved once from the first numbers. A gold
            // head now gives 10 (was 5) so the candy total stays generous (user rule). Numbers tuned since are kept.
            void Migrate(string field, float from, float to)
            {
                var p = so.FindProperty(field);
                if (p == null) return;
                if (p.propertyType == SerializedPropertyType.Float && Mathf.Abs(p.floatValue - from) < 0.0001f) p.floatValue = to;
                else if (p.propertyType == SerializedPropertyType.Integer && p.intValue == Mathf.RoundToInt(from)) p.intValue = Mathf.RoundToInt(to);
                else return;
                log.Add($"{field} {from} -> {to}");
            }
            Migrate("goldChance", 0.1f, 0.04f);
            Migrate("clockChance", 0.06f, 0.03f);
            Migrate("goldCandies", 5f, 10f);

            // The candies: the nesting round's.
            var nesting = Load<GameObject>("Assets/Prefabs/Bases/Bonus_Round.prefab");
            var from = nesting != null ? nesting.GetComponent<BonusRound>() : null;
            if (from != null)
            {
                var src = new SerializedObject(from).FindProperty("candies");
                var dst = so.FindProperty("candies");
                dst.arraySize = src.arraySize;
                for (int i = 0; i < src.arraySize; i++) dst.GetArrayElementAtIndex(i).objectReferenceValue = src.GetArrayElementAtIndex(i).objectReferenceValue;
                log.Add("Candies: the nesting round's " + src.arraySize);
            }
            else log.Add("MISSING Bonus_Round (candies)");
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, RoundPath);
            if (fresh) Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
            log.Add("Whack_Round " + (fresh ? "made" : "refilled (tuned numbers kept)") + ": " + RoundPath);
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
            if (state == null) { yield return $"GAMESTAGES has no '{GameStagesState}'"; yield break; }
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) { yield return $"'{GameStagesState}' didn't load all its actions; left alone"; yield break; }
            var play = state.Actions.OfType<PlayBonusRound>().FirstOrDefault();
            if (play == null) { yield return $"'{GameStagesState}' has no Play Bonus Round"; yield break; }
            if (play.round != null && play.round.Value == round) { yield return $"'{GameStagesState}' already plays Whack_Round"; yield break; }
            Undo.RecordObject(gs, "Whack-a-Pumpkin");
            string was = play.round != null && play.round.Value != null ? play.round.Value.name : "nothing";
            play.round = new FsmGameObject { Value = round };
            state.SaveActions();
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            yield return $"'{GameStagesState}' now plays Whack_Round (was {was}; that prefab is kept, unplugged)";
            yield return "SAVE THE SCENE to keep it.";
        }

        // ---------- Helpers ----------

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        private static Transform Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPosition, Vector3 scale,
                                           Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;
            return go.transform;
        }

        // A world-space text (TextMeshPro) on the board, readable from the stand spot.
        private static TextMeshPro Text(Transform parent, string name, Vector3 localPosition, string text, float size, Color colour,
                                        TMP_FontAsset font, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = colour;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = new Vector2(width, 0.3f);
            return tmp;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void MakeFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) MakeFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
