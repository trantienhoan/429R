using System.Collections.Generic;
using System.IO;
using Game.Combat;
using Game.Enemies;
using Game.VFX;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Pumpkin Boss Slide Setup (user, 2026-11-11: the remodel's giant pumpkin butt, "only
    /// sliding for now", a bit bigger, its pumpkin cracks). Sets up pumpkin_boss_Z.prefab for the sliding true form, on the
    /// model as it is imported now:
    /// - sizes 10% up (user: "a little bigger"): head and upper body 1.87, true form 2.31 (were 1.7 / 1.7 / 2.1);
    /// - the giant pumpkin's collider, made from the mesh itself: the triangles that move 100% with the Hips (the butt's
    ///   own submesh, Pumpkin Boss Butt Split) below the Waist, in the bind pose, as a convex Mesh Collider on a child of
    ///   the Hips ("Butt_Collider"; the mesh is saved as Models/Enemies/Pumpkin_Boss_Z_Butt_Collider.asset and made anew
    ///   each run, so after a re-export just run this again), with a Hit Zone taking a fifth of the hits' damage (user:
    ///   "punching or hitting its hips below part only do 1/5 damage"). The old little Hips sphere and the Shin spheres
    ///   (the legs are gone; they sat inside the pumpkin) go; the Waist sphere stays (full damage above the pumpkin);
    /// - the death burst and the jiggle: no knees, the hips' pumpkin is the giant one (its middle and size measured);
    /// - Pumpkin Boss Brain: the pumpkin's radius, the Hips' rest pose from the FBX (Hold Hips Still), the butt's
    ///   collider and material slot, the crack overlay (Materials/Boss/Boss_Butt_Cracks.mat, made if missing) and the
    ///   grinding sound (giant_ball_rolling, the bowling pumpkin's roll) where empty.
    /// - its slide pose (user, 2026-11-11, picked from renders): Models/Enemies/Pumpkin_Boss_Z_slide_pose.anim, one still
    ///   pose looped, made anew in place each run (its GUID stays): every curve of the transform clip held at 1.40 s (arms
    ///   up in a V, roaring), except the Head's own bones below it (eyes, face, jaw, stems: its normal face), held at
    ///   idle_true's 0 s; the "slide_pose" state on Pumpkin_Boss_Z_Brain.controller (added if missing, also after Set Up
    ///   Pumpkin Boss's rebuild of the controller), Slide Move State slide_pose and Slide Stand State idle_true;
    /// - the user's values (2026-11-11): Butt Break Share 0.2, Stun Guard Seconds 17;
    /// - the crack overlay (user, 2026-11-11: realistic cracks): Materials/Boss/Boss_Butt_Cracks_Map.png imported as data
    ///   (no colour space, wraps round, no compression), on the crack material with the pumpkin's cylinder (Crack Axis,
    ///   measured), and the material put on the prefab's renderer past its last slot (it draws over the pumpkin, nothing
    ///   while there are no cracks): that way a build keeps the mesh's rest positions the cracks are drawn from.
    /// The prefab file is copied to Documents/429R_claude_backups/2026-11-11/boss_slide/ before it's changed (the first
    /// copy is kept). Running it again changes nothing more. Not in Play mode, not with the prefab open in Prefab Mode.
    /// Set Up Pumpkin Boss (the full rebuild) runs the same set-up at its end.
    /// </summary>
    public static class PumpkinBossSlideMenu
    {
        private const string MenuPath = "Tools/429 Game/Enemies/Pumpkin Boss Slide Setup";
        private const string BossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        private const string FbxPath = "Assets/Models/Enemies/Pumpkin_Boss_Z.fbx";
        private const string ColliderMeshPath = "Assets/Models/Enemies/Pumpkin_Boss_Z_Butt_Collider.asset";
        private const string CrackMaterialPath = "Assets/Materials/Boss/Boss_Butt_Cracks.mat";
        private const string CrackShader = "429/Boss/Crack Overlay";
        /// <summary>The crack map the overlay reads (Rebuild Boss Cracks makes it anew from the user's painting).</summary>
        public const string CrackMapPath = "Assets/Materials/Boss/Boss_Butt_Cracks_Map.png";
        private const string GrindPath = "Assets/Audio/SFX/giant_ball_rolling.wav";
        private const string BackupName = "pumpkin_boss_Z_before_slide_setup.prefab";
        private const string ControllerPath = "Assets/Prefabs/Enemies/Pumpkin_Boss_Z_Brain.controller";
        private const string PosePath = "Assets/Models/Enemies/Pumpkin_Boss_Z_slide_pose.anim";
        private const string PoseState = "slide_pose", StandState = "idle_true";
        // The pose: the transform clip at 1.40 s, the face from idle_true at 0 s (user, 2026-11-11).
        private const string PoseClip = "transform", FaceClip = "idle_true", FaceUnder = "Head";
        private const float PoseAt = 1.40f, FaceAt = 0f;
        // The user's values (2026-11-11).
        private const float ButtBreakShare = 0.2f, StunGuardSeconds = 17f;
        private const string ButtChild = "Butt_Collider";
        private const string HipsName = "Hips", WaistName = "Waist";
        private static readonly string[] GoneBones = { "Shin.L", "Shin.R" };
        // Sizes, 10% up (user, 2026-11-11).
        private const float HeadScale = 1.87f, UpperScale = 1.87f, TrueScale = 2.31f;
        // Hits on the pumpkin take this share off its health.
        private const float ButtDamageShare = 0.2f;
        // When the mesh can't be read: the pumpkin as measured in Blender (model units): its middle 0.62 up, 1.56 wide and
        // 1.26 tall.
        private static readonly Vector3 FallbackMiddle = new(0f, 0.62f, 0f), FallbackRadii = new(0.78f, 0.63f, 0.78f);

        [MenuItem(MenuPath)]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Pumpkin Boss Slide] Stop Play mode first: it changes the prefab.");
                return;
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == BossPath)
            {
                Debug.LogWarning("[Pumpkin Boss Slide] pumpkin_boss_Z is open in Prefab Mode: close it and run this again.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPath) == null)
            {
                Debug.LogError("[Pumpkin Boss Slide] " + BossPath + " is missing.");
                return;
            }
            var notes = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(BossPath);
            try
            {
                if (!Apply(root, notes))
                {
                    Debug.Log("[Pumpkin Boss Slide] Already set up, nothing changed:\n" + string.Join("\n", notes));
                    return;
                }
                string backup = Backup(BossPath);
                PrefabUtility.SaveAsPrefabAsset(root, BossPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Pumpkin Boss Slide] pumpkin_boss_Z set up (" + backup + "):\n" + string.Join("\n", notes));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanSetUp() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>
        /// Sets up the boss's loaded prefab contents (see the class summary). Notes says what it did; true if anything
        /// changed. Assets it needs (the collider mesh, the crack material) are made or updated on the way.
        /// </summary>
        public static bool Apply(GameObject root, List<string> notes)
        {
            var brain = root.GetComponent<PumpkinBossBrain>();
            if (brain == null)
            {
                notes.Add("no Pumpkin Boss Brain on " + root.name + ": nothing done");
                return false;
            }
            var so = new SerializedObject(brain);
            var model = so.FindProperty("model").objectReferenceValue as Transform;
            if (model == null)
            {
                notes.Add("the brain has no Model: run Set Up Pumpkin Boss first");
                return false;
            }
            bool changed = false;
            changed |= SetFloat(so, "headScale", HeadScale, notes);
            changed |= SetFloat(so, "upperScale", UpperScale, notes);
            changed |= SetFloat(so, "trueScale", TrueScale, notes);
            changed |= SetFloat(so, "buttBreakShare", ButtBreakShare, notes);
            changed |= SetFloat(so, "stunGuardSeconds", StunGuardSeconds, notes);
            // Its slide pose: the clip, its state on the controller, and the states it slides and stands in.
            if (MakeSlidePose(notes))
            {
                changed |= SetString(so, "slideMoveState", PoseState, notes);
                changed |= SetString(so, "slideStandState", StandState, notes);
            }

            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) bones.TryAdd(t.name, t);
            bones.TryGetValue(HipsName, out var hips);
            if (hips == null)
            {
                notes.Add("no Hips bone under the Model: no pumpkin collider");
                so.ApplyModifiedPropertiesWithoutUndo();
                return changed;
            }

            // The pumpkin, measured on the imported mesh (in the Hips' bind space).
            var pumpkin = Measure(notes);
            changed |= SetBool(so, "hipsRestSet", true, notes);
            changed |= SetVector(so, "hipsRestPosition", pumpkin.hipsAt, notes);
            changed |= SetQuaternion(so, "hipsRestRotation", pumpkin.hipsTurn, notes);
            changed |= SetFloat(so, "pumpkinRadius", Round(pumpkin.radius), notes);

            // The old spheres go: the little hips one (the pumpkin's collider takes its place) and the knees'.
            foreach (var name in GoneBones) changed |= RemoveSphere(bones, name, notes);
            changed |= RemoveSphere(bones, HipsName, notes);

            // The pumpkin's collider on a child of the Hips, its units the model's (the Hips' own are 100 times bigger).
            var mesh = SaveColliderMesh(pumpkin, notes);
            var child = hips.Find(ButtChild);
            if (child == null)
            {
                child = new GameObject(ButtChild).transform;
                child.SetParent(hips, false);
                notes.Add(ButtChild + ": made under " + HipsName);
                changed = true;
            }
            var scale = Vector3.one * pumpkin.hipsUnit;
            if (child.localPosition != Vector3.zero || child.localRotation != Quaternion.identity || child.localScale != scale)
            {
                child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                child.localScale = scale;
                changed = true;
            }
            if (child.gameObject.layer != hips.gameObject.layer) { child.gameObject.layer = hips.gameObject.layer; changed = true; }
            if (!child.CompareTag("Enemy")) { child.gameObject.tag = "Enemy"; changed = true; }
            var hull = child.GetComponent<MeshCollider>();
            if (hull == null) { hull = child.gameObject.AddComponent<MeshCollider>(); changed = true; }
            if (!hull.convex || hull.sharedMesh != mesh || hull.isTrigger)
            {
                hull.convex = true;
                hull.isTrigger = false;
                hull.sharedMesh = mesh;
                notes.Add(ButtChild + ": convex Mesh Collider from " + Path.GetFileName(ColliderMeshPath));
                changed = true;
            }
            var zone = child.GetComponent<HitZone>();
            if (zone == null) { zone = child.gameObject.AddComponent<HitZone>(); changed = true; }
            if (!Mathf.Approximately(zone.DamageShare, ButtDamageShare))
            {
                zone.DamageShare = ButtDamageShare;
                EditorUtility.SetDirty(zone);
                notes.Add(ButtChild + ": Hit Zone, a fifth of the damage");
                changed = true;
            }
            changed |= SetObject(so, "buttCollider", hull, notes);
            var waistSphere = bones.TryGetValue(WaistName, out var waist) ? waist.GetComponent<SphereCollider>() : null;
            // The Waist sphere covers the waist pumpkin (full damage above the giant one; user, 2026-11-11: "fix its collider").
            if (waistSphere != null && pumpkin.waistMeasured &&
                ((waistSphere.center - pumpkin.waistCenter).sqrMagnitude > 1e-12f || !Mathf.Approximately(waistSphere.radius, pumpkin.waistRadius)))
            {
                waistSphere.center = pumpkin.waistCenter;
                waistSphere.radius = pumpkin.waistRadius;
                EditorUtility.SetDirty(waistSphere);
                changed = true;
            }
            notes.Add(waistSphere != null
                ? $"Waist sphere (radius {waistSphere.radius / BoneUnits(waist, model):0.00} model units): full damage above the pumpkin"
                : "NO Waist sphere: run Set Up Pumpkin Boss or Enemy Jiggle's hit spheres");

            // The butt's material slot (Pumpkin Boss Butt Split gives it its own submesh) and the crack overlay.
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            int parts = skin != null && skin.sharedMesh != null ? skin.sharedMesh.subMeshCount : 0;
            if (parts > PumpkinBossButtSplit.ButtSubmesh)
            {
                changed |= SetInt(so, "buttMaterialSlot", PumpkinBossButtSplit.ButtSubmesh, notes);
                if (skin.sharedMaterials.Length < parts) notes.Add($"the renderer has {skin.sharedMaterials.Length} materials for {parts} parts: give the butt's slot a material");
            }
            else notes.Add($"the model has {parts} material slot(s): Butt Material Slot left as it is (no cracks drawn until the butt has its own)");
            var crack = CrackMaterial(notes);
            if (crack != null)
            {
                changed |= FillObject(so, "buttCrackMaterial", crack, notes);
                changed |= SetUpCrackMaterial(crack, pumpkin.box, notes);
                // On the renderer past its last slot: it draws over the last part (the pumpkin), and a build keeps the
                // mesh's rest positions for it.
                if (skin != null && parts > PumpkinBossButtSplit.ButtSubmesh && PumpkinBossButtSplit.ButtSubmesh == parts - 1)
                {
                    var mats = skin.sharedMaterials;
                    if (mats.Length != parts + 1 || mats[parts] != crack)
                    {
                        var more = new Material[parts + 1];
                        for (int i = 0; i < parts; i++) more[i] = i < mats.Length ? mats[i] : mats[mats.Length - 1];
                        more[parts] = crack;
                        skin.sharedMaterials = more;
                        notes.Add("crack overlay on the renderer past its last slot");
                        changed = true;
                    }
                }
            }
            var grind = AssetDatabase.LoadAssetAtPath<AudioClip>(GrindPath);
            if (grind != null) changed |= FillObject(so, "slideGrind", grind, notes);
            else notes.Add(GrindPath + " is missing: no grinding sound");

            // Its death burst and its jiggle: the knees go, the hips' pumpkin is the giant one, last.
            changed |= SetBodyPumpkins(so, pumpkin, notes);
            so.ApplyModifiedPropertiesWithoutUndo();
            changed |= SetJiggleParts(root, so, notes);
            return changed;
        }

        // ---------- measuring the pumpkin ----------

        private struct Pumpkin
        {
            public Vector3[] points;   // the collider's corners, model units, in the collider child's space
            public int[] triangles;
            public Vector3 middleInHips;   // the pumpkin's middle in the Hips' own space (tiny: 0.01 = a model unit)
            public Bounds box;             // the pumpkin in the model's space (its rest pose)
            public float width;            // across, model units
            public float radius;           // round its middle on the floor, model units
            public float hipsUnit;         // the Hips' own unit in model units (0.01)
            public Vector3 hipsAt;         // the Hips' rest pose in the model's space
            public Quaternion hipsTurn;
            public bool waistMeasured;     // the waist pumpkin, for the Waist hit sphere, in the Waist's own space
            public Vector3 waistCenter;
            public float waistRadius;
        }

        // The giant pumpkin from the imported mesh: the butt's submesh (every triangle moving 100% with the Hips), only the
        // corners below the Waist, all in the bind pose, taken into the Hips' space. The mesh can't be read: a squashed
        // ball the pumpkin's size.
        private static Pumpkin Measure(List<string> notes)
        {
            var result = new Pumpkin { hipsUnit = 0.01f, hipsTurn = Quaternion.identity, hipsAt = new Vector3(0f, 0.868f, 0f) };
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var skin = fbx != null ? fbx.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            var mesh = skin != null ? skin.sharedMesh : null;
            int hips = -1, waist = -1;
            if (skin != null)
            {
                hips = System.Array.FindIndex(skin.bones, b => b != null && b.name == HipsName);
                waist = System.Array.FindIndex(skin.bones, b => b != null && b.name == WaistName);
            }
            Matrix4x4[] binds = null;
            Vector3[] vertices = null;
            BoneWeight[] weights = null;
            int[] tris = null;
            try
            {
                if (mesh != null && hips >= 0)
                {
                    binds = mesh.bindposes;
                    vertices = mesh.vertices;
                    weights = mesh.boneWeights;
                    tris = mesh.subMeshCount > PumpkinBossButtSplit.ButtSubmesh ? mesh.GetTriangles(PumpkinBossButtSplit.ButtSubmesh) : mesh.GetTriangles(0);
                }
            }
            catch (System.Exception e)
            {
                notes.Add("couldn't read the mesh (" + e.Message + ")");
                vertices = null;
            }
            // Mesh space is the model's (the skinned mesh sits at the model's origin, unturned). Bind pose of a bone = mesh
            // space to the bone's own.
            Matrix4x4 toHips;
            if (binds != null && hips < binds.Length)
            {
                toHips = binds[hips];
                var hipsInModel = toHips.inverse;
                result.hipsAt = hipsInModel.MultiplyPoint3x4(Vector3.zero);
                result.hipsTurn = hipsInModel.rotation;
            }
            else
            {
                var hipsT = fbx != null ? FindDeep(fbx.transform, HipsName) : null;
                if (hipsT != null)
                {
                    toHips = hipsT.worldToLocalMatrix * fbx.transform.localToWorldMatrix;
                    result.hipsAt = fbx.transform.InverseTransformPoint(hipsT.position);
                    result.hipsTurn = Quaternion.Inverse(fbx.transform.rotation) * hipsT.rotation;
                }
                else toHips = Matrix4x4.TRS(-result.hipsAt * 0.01f, Quaternion.identity, Vector3.one * 0.01f);
                notes.Add("no bind pose read: the Hips from the FBX's own bones");
            }
            result.hipsUnit = toHips.lossyScale.x > 0f ? toHips.lossyScale.x : 0.01f;
            // Model space to the collider child's (the Hips' space in model units).
            var toChild = Matrix4x4.Scale(Vector3.one / result.hipsUnit) * toHips;

            var picked = new List<Vector3>();
            var index = new Dictionary<int, int>();
            var outTris = new List<int>();
            int above = 0;
            if (vertices != null && vertices.Length > 0 && weights != null && weights.Length == vertices.Length && tris != null)
            {
                var hipsPoint = toHips.inverse.MultiplyPoint3x4(Vector3.zero);
                var waistPoint = waist >= 0 && waist < binds.Length ? binds[waist].inverse.MultiplyPoint3x4(Vector3.zero) : hipsPoint + Vector3.up * 0.25f;
                var up = (waistPoint - hipsPoint).normalized;
                // The whole pumpkin, its top too (2026-11-11: cut at the Waist, its top 0.2 model units had no collider and
                // hits there went through). The butt's submesh is only the giant pumpkin, so nothing of the waist comes in.
                bool Keep(int v) => weights[v].boneIndex0 == hips && weights[v].weight0 >= 0.999f;
                var seen = new HashSet<int>();
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    foreach (int v in new[] { a, b, c })
                    {
                        if (seen.Add(v) && weights[v].boneIndex0 == hips && weights[v].weight0 >= 0.999f && !Keep(v)) above++;
                    }
                    if (!Keep(a) || !Keep(b) || !Keep(c)) continue;
                    foreach (int v in new[] { a, b, c })
                    {
                        if (!index.TryGetValue(v, out int at))
                        {
                            at = picked.Count;
                            index[v] = at;
                            picked.Add(vertices[v]);
                        }
                        outTris.Add(at);
                    }
                }
            }
            if (picked.Count >= 4)
            {
                notes.Add($"pumpkin measured on the mesh: {picked.Count} corners");
            }
            else
            {
                notes.Add("pumpkin NOT measured on the mesh (no Hips-only triangles read): a squashed ball 1.56 x 1.26 model units used");
                picked.Clear();
                outTris.Clear();
                MakeBall(picked, outTris);
            }
            // Its size in model space.
            var box = new Bounds(picked[0], Vector3.zero);
            foreach (var p in picked) box.Encapsulate(p);
            result.width = Mathf.Max(box.size.x, box.size.z);
            result.radius = (box.size.x + box.size.z) * 0.25f;
            result.middleInHips = toHips.MultiplyPoint3x4(box.center);
            result.box = box;
            notes.Add($"pumpkin: middle {box.center.ToString("F3")} (model), {box.size.x:0.00} x {box.size.y:0.00} x {box.size.z:0.00}, radius {result.radius:0.00}; Hips at {result.hipsAt.ToString("F3")}, unit {result.hipsUnit:0.#####}");
            result.points = new Vector3[picked.Count];
            for (int i = 0; i < picked.Count; i++) result.points[i] = toChild.MultiplyPoint3x4(picked[i]);
            result.triangles = outTris.ToArray();

            // The waist pumpkin (the corners moving mostly with the Waist), for the Waist hit sphere: it was half its width.
            if (vertices != null && weights != null && weights.Length == vertices.Length && binds != null && waist >= 0 && waist < binds.Length)
            {
                bool any = false;
                var waistBox = new Bounds();
                for (int v = 0; v < vertices.Length; v++)
                {
                    if (weights[v].boneIndex0 != waist || weights[v].weight0 < 0.5f) continue;
                    if (!any) { waistBox = new Bounds(vertices[v], Vector3.zero); any = true; }
                    else waistBox.Encapsulate(vertices[v]);
                }
                if (any)
                {
                    result.waistMeasured = true;
                    result.waistCenter = binds[waist].MultiplyPoint3x4(waistBox.center);
                    result.waistRadius = Mathf.Max(waistBox.extents.x, waistBox.extents.z) * binds[waist].lossyScale.x;
                    notes.Add($"waist pumpkin: middle {waistBox.center.ToString("F3")} (model), {waistBox.size.x:0.00} x {waistBox.size.y:0.00} x {waistBox.size.z:0.00}");
                }
            }
            return result;
        }

        // A squashed ball (12 x 8) the pumpkin's size, model space.
        private static void MakeBall(List<Vector3> points, List<int> triangles)
        {
            const int Around = 12, Rings = 8;
            for (int r = 0; r <= Rings; r++)
            {
                float v = Mathf.PI * r / Rings;
                for (int a = 0; a < Around; a++)
                {
                    float u = 2f * Mathf.PI * a / Around;
                    var dir = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                    points.Add(FallbackMiddle + Vector3.Scale(dir, FallbackRadii));
                }
            }
            for (int r = 0; r < Rings; r++)
            {
                for (int a = 0; a < Around; a++)
                {
                    int i0 = r * Around + a, i1 = r * Around + (a + 1) % Around, i2 = i0 + Around, i3 = i1 + Around;
                    triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
                    triangles.Add(i1); triangles.Add(i2); triangles.Add(i3);
                }
            }
        }

        // The collider's mesh asset, made or (same file, same GUID) refilled.
        private static Mesh SaveColliderMesh(Pumpkin pumpkin, List<string> notes)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ColliderMeshPath);
            bool made = mesh == null;
            if (made) mesh = new Mesh();
            mesh.Clear();
            mesh.name = Path.GetFileNameWithoutExtension(ColliderMeshPath);
            mesh.vertices = pumpkin.points;
            mesh.triangles = pumpkin.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (made) AssetDatabase.CreateAsset(mesh, ColliderMeshPath);
            else EditorUtility.SetDirty(mesh);
            notes.Add($"{Path.GetFileName(ColliderMeshPath)} {(made ? "made" : "refreshed")}: {pumpkin.points.Length} corners, {pumpkin.triangles.Length / 3} triangles");
            return mesh;
        }

        // The crack map imported as data, on the crack material with the pumpkin's cylinder (its middle, bottom and top).
        private static bool SetUpCrackMaterial(Material crack, Bounds box, List<string> notes)
        {
            bool changed = false;
            var map = CrackMapReady(notes);
            if (map != null && crack.HasProperty("_CrackMap") && crack.GetTexture("_CrackMap") != map)
            {
                crack.SetTexture("_CrackMap", map);
                notes.Add("crack material: the crack map");
                changed = true;
            }
            var axis = new Vector4(Round(box.center.x), Round(box.center.z), Round(box.min.y), Round(box.max.y));
            if (crack.HasProperty("_CrackAxis") && crack.GetVector("_CrackAxis") != axis)
            {
                crack.SetVector("_CrackAxis", axis);
                notes.Add($"crack material: the pumpkin's cylinder {axis}");
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(crack);
            return changed;
        }

        /// <summary>
        /// The crack map as the overlay needs it: data (no colour space), wrapping round (x) and not up and down (y),
        /// smooth, with mipmaps, no compression (a distance field: compression would make its edges blocky). Imported again
        /// only if a setting was off. Null when there's no map.
        /// </summary>
        public static Texture2D CrackMapReady(List<string> notes)
        {
            if (AssetImporter.GetAtPath(CrackMapPath) is not TextureImporter importer)
            {
                notes.Add(CrackMapPath + " is missing: no cracks drawn");
                return null;
            }
            bool off = importer.textureType != TextureImporterType.Default || importer.sRGBTexture ||
                       importer.alphaSource != TextureImporterAlphaSource.None || importer.wrapModeU != TextureWrapMode.Repeat ||
                       importer.wrapModeV != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear ||
                       !importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                       importer.npotScale != TextureImporterNPOTScale.None;
            var android = importer.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != TextureImporterFormat.RGB24) off = true;
            if (off)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                android.overridden = true;
                android.format = TextureImporterFormat.RGB24;
                android.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
                notes.Add(CrackMapPath + ": imported as data (wraps round, no compression)");
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CrackMapPath);
        }

        private static Material CrackMaterial(List<string> notes)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CrackMaterialPath);
            if (material != null) return material;
            var shader = Shader.Find(CrackShader);
            if (shader == null)
            {
                notes.Add("shader " + CrackShader + " not found (Assets/Shader/BossCrackOverlay.shader): no crack material");
                return null;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Boss")) AssetDatabase.CreateFolder("Assets/Materials", "Boss");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(CrackMaterialPath) };
            AssetDatabase.CreateAsset(material, CrackMaterialPath);
            notes.Add(CrackMaterialPath + " made");
            return material;
        }

        // ---------- the prefab's parts ----------

        private static bool RemoveSphere(Dictionary<string, Transform> bones, string bone, List<string> notes)
        {
            if (!bones.TryGetValue(bone, out var t)) return false;
            bool any = false;
            foreach (var sphere in t.GetComponents<SphereCollider>())
            {
                Object.DestroyImmediate(sphere);
                any = true;
            }
            if (any) notes.Add(bone + ": hit sphere removed");
            return any;
        }

        // The death burst: no knees; the hips' pumpkin is the giant one (its middle in the Hips' space, its width), last.
        private static bool SetBodyPumpkins(SerializedObject so, Pumpkin pumpkin, List<string> notes)
        {
            var list = so.FindProperty("bodyPumpkins");
            if (list == null || !list.isArray) return false;
            bool changed = false;
            float last = 0f;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var entry = list.GetArrayElementAtIndex(i);
                string bone = entry.FindPropertyRelative("bone").stringValue;
                if (System.Array.IndexOf(GoneBones, bone) >= 0)
                {
                    list.DeleteArrayElementAtIndex(i);
                    notes.Add("death burst: " + bone + " removed");
                    changed = true;
                    continue;
                }
                if (bone != HipsName) last = Mathf.Max(last, entry.FindPropertyRelative("at").floatValue);
            }
            int hipsAt = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("bone").stringValue == HipsName) hipsAt = i;
            }
            if (hipsAt < 0)
            {
                hipsAt = list.arraySize;
                list.arraySize++;
                list.GetArrayElementAtIndex(hipsAt).FindPropertyRelative("bone").stringValue = HipsName;
                changed = true;
            }
            var hipsEntry = list.GetArrayElementAtIndex(hipsAt);
            var offset = hipsEntry.FindPropertyRelative("offset");
            var size = hipsEntry.FindPropertyRelative("size");
            var at = hipsEntry.FindPropertyRelative("at");
            var middle = RoundSmall(pumpkin.middleInHips);
            float width = Round(pumpkin.width);
            float when = Mathf.Max(last + 0.16f, 0.66f);
            if ((offset.vector3Value - middle).sqrMagnitude > 1e-12f || !Mathf.Approximately(size.floatValue, width) || !Mathf.Approximately(at.floatValue, when))
            {
                offset.vector3Value = middle;
                size.floatValue = width;
                at.floatValue = when;
                notes.Add($"death burst: Hips = the giant pumpkin ({width:0.00} model units, at {when:0.00} s)");
                changed = true;
            }
            return changed;
        }

        // Part Jiggle follows the body pumpkins (bone and middle), as Enemy Jiggle's menu sets it.
        private static bool SetJiggleParts(GameObject root, SerializedObject brain, List<string> notes)
        {
            if (!root.TryGetComponent(out PartJiggle jiggle)) return false;
            var pumpkins = brain.FindProperty("bodyPumpkins");
            var settings = new SerializedObject(jiggle);
            var parts = settings.FindProperty("parts");
            bool changed = parts.arraySize != pumpkins.arraySize;
            parts.arraySize = pumpkins.arraySize;
            for (int i = 0; i < pumpkins.arraySize; i++)
            {
                var pumpkin = pumpkins.GetArrayElementAtIndex(i);
                var part = parts.GetArrayElementAtIndex(i);
                string bone = pumpkin.FindPropertyRelative("bone").stringValue;
                var middle = pumpkin.FindPropertyRelative("offset").vector3Value;
                if (part.FindPropertyRelative("bone").stringValue != bone || part.FindPropertyRelative("centre").vector3Value != middle)
                {
                    part.FindPropertyRelative("bone").stringValue = bone;
                    part.FindPropertyRelative("centre").vector3Value = middle;
                    changed = true;
                }
            }
            if (!changed) return false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            notes.Add("Part Jiggle: " + parts.arraySize + " parts, as the body pumpkins");
            return true;
        }

        // ---------- the slide pose ----------

        // The slide pose clip (made anew in place, so its GUID stays) and its state on the boss's controller. True when the
        // state is there to use.
        private static bool MakeSlidePose(List<string> notes)
        {
            AnimationClip pose = null, face = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (asset is not AnimationClip clip || clip.name.StartsWith("__preview")) continue;
                if (clip.name == PoseClip) pose = clip;
                else if (clip.name == FaceClip) face = clip;
            }
            if (pose == null)
            {
                notes.Add($"slide pose: no '{PoseClip}' clip in {Path.GetFileName(FbxPath)}: not made");
                return false;
            }
            var bindings = AnimationUtility.GetCurveBindings(pose);
            var curves = new AnimationCurve[bindings.Length];
            int fromFace = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                float value = AnimationUtility.GetEditorCurve(pose, binding).Evaluate(PoseAt);
                // The Head's own bones below it (eyes, face, jaw, stems): its normal face.
                if (face != null && UnderHead(binding.path))
                {
                    var faceCurve = AnimationUtility.GetEditorCurve(face, binding);
                    if (faceCurve != null)
                    {
                        value = faceCurve.Evaluate(FaceAt);
                        fromFace++;
                    }
                }
                curves[i] = new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f, value));
            }
            var clipAsset = AssetDatabase.LoadAssetAtPath<AnimationClip>(PosePath);
            bool made = clipAsset == null;
            if (made) clipAsset = new AnimationClip { name = Path.GetFileNameWithoutExtension(PosePath) };
            clipAsset.ClearCurves();
            clipAsset.frameRate = pose.frameRate;
            AnimationUtility.SetEditorCurves(clipAsset, bindings, curves);
            var settings = AnimationUtility.GetAnimationClipSettings(clipAsset);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clipAsset, settings);
            if (made) AssetDatabase.CreateAsset(clipAsset, PosePath);
            else EditorUtility.SetDirty(clipAsset);
            notes.Add($"slide pose {(made ? "made" : "refreshed")}: {bindings.Length} curves held ({PoseClip} at {PoseAt:0.00} s, {fromFace} face curves from {FaceClip} at {FaceAt:0} s)");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                notes.Add("slide pose: no " + ControllerPath + ": no state");
                return false;
            }
            var machine = controller.layers[0].stateMachine;
            AnimatorState state = null;
            foreach (var child in machine.states)
            {
                if (child.state.name == PoseState) state = child.state;
            }
            if (state == null)
            {
                state = machine.AddState(PoseState);
                notes.Add($"'{PoseState}' state added to {Path.GetFileName(ControllerPath)}");
            }
            if (state.motion != clipAsset || !state.writeDefaultValues)
            {
                state.motion = clipAsset;
                state.writeDefaultValues = true;
                EditorUtility.SetDirty(controller);
            }
            bool stand = false;
            foreach (var child in machine.states)
            {
                if (child.state.name == StandState) stand = true;
            }
            if (!stand) notes.Add($"no '{StandState}' state on the controller: it stands in move");
            return true;
        }

        // A curve on one of the Head's own bones below it (the path runs through "Head" and on).
        private static bool UnderHead(string path)
        {
            var parts = path.Split('/');
            int head = System.Array.IndexOf(parts, FaceUnder);
            return head >= 0 && head < parts.Length - 1;
        }

        // ---------- small things ----------

        private static float BoneUnits(Transform bone, Transform model) =>
            Mathf.Max(0.0001f, Mathf.Abs(bone.lossyScale.x) / Mathf.Max(0.0001f, Mathf.Abs(model.lossyScale.x)));

        private static float Round(float value) => Mathf.Round(value * 1000f) / 1000f;

        private static Vector3 RoundSmall(Vector3 v) =>
            new(Mathf.Round(v.x * 1e6f) / 1e6f, Mathf.Round(v.y * 1e6f) / 1e6f, Mathf.Round(v.z * 1e6f) / 1e6f);

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static bool SetFloat(SerializedObject so, string name, float value, List<string> notes)
        {
            var p = so.FindProperty(name);
            if (p == null) { notes.Add(name + ": not on the brain"); return false; }
            if (Mathf.Approximately(p.floatValue, value)) return false;
            notes.Add($"{name}: {p.floatValue:0.###} -> {value:0.###}");
            p.floatValue = value;
            return true;
        }

        private static bool SetString(SerializedObject so, string name, string value, List<string> notes)
        {
            var p = so.FindProperty(name);
            if (p == null || p.stringValue == value) return false;
            notes.Add($"{name}: {p.stringValue} -> {value}");
            p.stringValue = value;
            return true;
        }

        private static bool SetInt(SerializedObject so, string name, int value, List<string> notes)
        {
            var p = so.FindProperty(name);
            if (p == null || p.intValue == value) return false;
            notes.Add($"{name}: {p.intValue} -> {value}");
            p.intValue = value;
            return true;
        }

        private static bool SetBool(SerializedObject so, string name, bool value, List<string> notes)
        {
            var p = so.FindProperty(name);
            if (p == null || p.boolValue == value) return false;
            notes.Add($"{name}: {p.boolValue} -> {value}");
            p.boolValue = value;
            return true;
        }

        private static bool SetVector(SerializedObject so, string name, Vector3 value, List<string> notes)
        {
            var p = so.FindProperty(name);
            value = new Vector3(Round(value.x), Round(value.y), Round(value.z));
            if (p == null || (p.vector3Value - value).sqrMagnitude < 1e-8f) return false;
            notes.Add($"{name}: {p.vector3Value.ToString("F3")} -> {value.ToString("F3")}");
            p.vector3Value = value;
            return true;
        }

        private static bool SetQuaternion(SerializedObject so, string name, Quaternion value, List<string> notes)
        {
            var p = so.FindProperty(name);
            if (p == null || Quaternion.Angle(p.quaternionValue, value) < 0.01f) return false;
            notes.Add($"{name}: {p.quaternionValue.eulerAngles.ToString("F1")} -> {value.eulerAngles.ToString("F1")}");
            p.quaternionValue = value;
            return true;
        }

        private static bool SetObject(SerializedObject so, string name, Object value, List<string> notes)
        {
            var p = so.FindProperty(name);
            if (p == null || p.objectReferenceValue == value) return false;
            notes.Add($"{name}: {(p.objectReferenceValue != null ? p.objectReferenceValue.name : "none")} -> {(value != null ? value.name : "none")}");
            p.objectReferenceValue = value;
            return true;
        }

        // Only an empty slot is filled (one chosen by hand is kept).
        private static bool FillObject(SerializedObject so, string name, Object value, List<string> notes)
        {
            var p = so.FindProperty(name);
            if (p == null || p.objectReferenceValue != null) return false;
            p.objectReferenceValue = value;
            notes.Add($"{name}: none -> {value.name}");
            return true;
        }

        // A copy of the prefab file as it is now, once (a later run keeps the first copy: the one from before the change).
        private static string Backup(string assetPath)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-11", "boss_slide");
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, BackupName);
            if (File.Exists(to)) return "backup from an earlier run kept";
            File.Copy(assetPath, to);
            File.Copy(assetPath + ".meta", to + ".meta", true);
            return "backed up to " + to;
        }
    }
}
