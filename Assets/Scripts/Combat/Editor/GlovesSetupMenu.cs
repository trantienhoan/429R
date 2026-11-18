using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Combat;
using Game.Leveling;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Combat > Set Up Gloves (user, 2026-11-12: "When we press grip and trigger, we pop up a glove for
    /// really punching"). It makes what the gloves need, and only what's missing, so running it again changes nothing and
    /// never undoes your own edits:
    /// - placeholder glove meshes (a padded mitt with a thumb, and a cuff; a right one and its mirror for the left) in
    ///   Assets/Models/Gloves, and their materials in Assets/Data/Gloves (Boxing red, Throwing green, Soldier blue, a white
    ///   cuff);
    /// - one prefab per kind and hand in Assets/Prefabs/Player/Gloves (Glove_Boxing_L, Glove_Boxing_R, ...): its pivot
    ///   is the knuckles (+Z where the punch goes, +Y the top of the fist); its child Model is the look (put your own
    ///   model there later) and its child Punch Shape the collider that hits (a capsule round the mitt);
    /// - Assets/Resources/Glove Kinds.asset: the three kinds (Boxing +3 Fist Size, Throwing +3 Throw Power, Soldier +3
    ///   Weapon Efficiency; Soldier was called Warrior), the pop, its sounds and its puff of smoke (Smoke_Flash_Small_Puff,
    ///   2026-11-18); Boxing gets Easy Flying Punches (flying
    ///   punches are harder with the other gloves) and each kind its Unlocks (Boxing the three flying punches, Throwing
    ///   Explosive Objects, Soldier Impact); set once (Menu Version), a later run never undoes a change made by hand;
    /// - Assets/Resources/Blast Skills.asset (user, 2026-11-12): the numbers and looks of the exploding throws, Bomber
    ///   Man, Impact and Weapon Master (the punches' blast look: Flash_round_yellow, Hit_Blast, Smoke_Flash_Small_Puff;
    ///   never Smoke_Blast); made once, then only empty effect or sound slots are filled, and once (its Menu Version 1)
    ///   the doubled damage is written into an asset made before it (user, 2026-11-12);
    /// - on Knight.prefab, "Hanging Gloves" under his boong (Cay_boong): the pair he carries (Glove Pickup), with a
    ///   preview of the Boxing pair you can see in Prefab Mode (it goes when the game starts);
    /// - StartGamePumpkin's tutorial: its MAKE A FIST! page shows only to a player with gloves (Show Notice's Glove
    ///   Pages = 1).
    /// The scene is only checked (both Fists, the Fist Bump; the gloves find their kinds by themselves), except that a
    /// Fist whose harder flying-punch conditions (Flying punches without easy gloves) are missing gets them: they're new
    /// fields, so normally they already hold their defaults and nothing changes (then there's nothing to save); and it
    /// logs the scene's Player Level unlock points (Jab, Swing; Uppercut is a new field: 5). Every prefab or asset it
    /// changes is copied to Documents/429R_claude_backups/2026-11-12/gloves/ first (a later run keeps the first copy).
    /// Everything it does is in the Console.
    /// The two "(Play Mode)" items put a random pair on or take the gloves off, for trying them without the Knight.
    /// </summary>
    public static class GlovesSetupMenu
    {
        private const string SetUpPath = "Tools/429 Game/Combat/Set Up Gloves";
        private const string GivePath = "Tools/429 Game/Combat/Gloves: Put On A Random Pair (Play Mode)";
        private const string TakePath = "Tools/429 Game/Combat/Gloves: Take Off (Play Mode)";

        private const string ModelFolder = "Assets/Models/Gloves";
        private const string MaterialFolder = "Assets/Data/Gloves";
        private const string PrefabFolder = "Assets/Prefabs/Player/Gloves";
        private const string KindsPath = "Assets/Resources/Glove Kinds.asset";
        private const string KnightPath = "Assets/Prefabs/Player/Knight.prefab";
        private const string StartPumpkinPath = "Assets/Prefabs/Items/Breakable/StartGamePumpkin.prefab";
        private const string BoongName = "Cay_boong";
        private const string HangingName = "Hanging Gloves";
        private const string PopSoundPath = "Assets/Audio/SFX/glove_fwump.wav";
        private const string PopSoundFallback = "Assets/Audio/SFX/sofa_hit_1.wav";
        private const string PickupSoundPath = "Assets/Audio/SFX/glove_pickup.wav";
        private const string PickupSoundFallback = "Assets/Audio/SFX/shop_buy_success.wav";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string TutorialPicturePath = "Assets/M_Textures/Tutorial/Tutorial_3_Fist.png";
        private const string BackupArea = "gloves";

        // The three kinds to start with (user, 2026-11-12), each its stat and colour (the colours of its stat's candy).
        private static readonly (string id, string title, Stat stat, Color colour, bool easy, GloveUnlocks unlocks)[] StarterKinds =
        {
            ("Boxing", "Boxing Gloves", Stat.FistSize, new Color(0.80f, 0.08f, 0.06f), true, GloveUnlocks.FlyingPunches),
            ("Throwing", "Throwing Gloves", Stat.ThrowPower, new Color(0.13f, 0.60f, 0.20f), false, GloveUnlocks.ExplosiveObjects),
            ("Soldier", "Soldier Gloves", Stat.WeaponEfficiency, new Color(0.10f, 0.32f, 0.85f), false, GloveUnlocks.Impact),
        };
        // What the menu sets on Glove Kinds once (its Menu Version): 1 = Boxing's Easy Flying Punches; 2 = Warrior renamed
        // Soldier, and the kinds' Unlocks.
        private const int KindsMenuVersion = 2;
        private const string BlastSkillsPath = "Assets/Resources/Blast Skills.asset";
        // What the menu sets on Blast Skills once (its Menu Version): 1 = the doubled damage (user, 2026-11-12).
        private const int BlastSkillsMenuVersion = 1;
        // The blasts' look: the punches' (Fist's Boong Blast Effects without Smoke_Blast) and the bottles' bang.
        private static readonly string[] BlastEffectPaths =
        {
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab",
            "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab",
        };
        private const string BlastSoundPath = "Assets/Audio/SFX/explosion-312361.wav";
        private const string ImpactSoundPath = "Assets/Audio/SFX/fist_bump_punch.wav";
        // The Fist's harder flying-punch conditions (user, 2026-11-12), written only where a scene Fist has none.
        private static readonly (string property, float value)[] HardFlying =
        {
            ("hardJabSpeed", 3.2f), ("hardTopJabSpeed", 6f), ("hardJabSeconds", 0.32f), ("hardJabStraightness", 0.85f),
            ("hardJabAim", 40f), ("hardSwingSpeed", 3.8f), ("hardUppercutSpeed", 3.4f),
        };
        private static readonly Color CuffColour = new(0.93f, 0.91f, 0.86f);
        private const float StarterPoints = 3f;

        // The placeholder glove, in metres, in the glove's own space (pivot at the knuckles, +Z the punch, +Y the top of
        // the fist, +X the back of a right hand). The mitt: a rounded box, bigger in front (the padding) and below.
        private static readonly Vector3 MittCentre = new(0f, -0.02f, -0.025f);
        private const float MittBack = 0.058f, MittPalm = 0.048f, MittTop = 0.06f, MittBottom = 0.075f, MittFront = 0.085f, MittRear = 0.075f;
        private static readonly Vector3 ThumbCentre = new(-0.034f, 0.022f, -0.018f);
        private static readonly Vector3 ThumbSize = new(0.024f, 0.024f, 0.05f);
        private const float CuffFrom = -0.085f, CuffTo = -0.175f;
        private static readonly Vector2 CuffSize = new(0.047f, 0.054f);
        private const float CuffFlare = 1.08f;
        // The punch shape: a capsule along Z round the mitt (2026-11-18: fitted to the modelled glove, Models/Gloves/Gloves.fbx,
        // the user's chubby round fist; was centre y -0.02, radius 0.058, 0.16 long for the placeholder).
        private static readonly Vector3 PunchCentre = new(0f, -0.028f, -0.02f);
        private const float PunchRadius = 0.078f, PunchLength = 0.156f;
        // The pair on the Knight's boong: the string's length, how far apart the gloves hang, in metres.
        private const float StringLength = 0.09f, StringWidth = 0.006f, HangApart = 0.05f;

        [MenuItem(SetUpPath)]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Gloves] Stop Play mode first: it makes prefabs and assets.");
                return;
            }
            var log = new List<string>();
            bool pumpkinChanged = false;
            try
            {
                Folder(ModelFolder, log);
                Folder(MaterialFolder, log);
                Folder(PrefabFolder, log);
                Folder("Assets/Resources", log);

                var meshes = Meshes(log);
                var cuff = Material("Glove_Cuff", CuffColour, log);
                var prefabs = new Dictionary<string, (GameObject left, GameObject right)>();
                foreach (var kind in StarterKinds)
                {
                    var look = Material("Glove_" + kind.id, kind.colour, log);
                    var right = GlovePrefab(kind.id, false, meshes.mittR, meshes.cuffR, look, cuff, log);
                    var left = GlovePrefab(kind.id, true, meshes.mittL, meshes.cuffL, look, cuff, log);
                    prefabs[kind.id] = (left, right);
                }
                var kinds = KindsAsset(prefabs, log);
                BlastSkillsAsset(log);
                Knight(kinds, cuff, log);
                pumpkinChanged = StartPumpkin(log);
                CheckScene(log);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Debug.Log("[Gloves] Set Up Gloves:\n" + string.Join("\n", log));
            }
            // PlayMaker keeps the actions it already read from a prefab: a script reload makes it read the new ones.
            if (pumpkinChanged) EditorUtility.RequestScriptReload();
        }

        [MenuItem(SetUpPath, true)]
        private static bool CanSetUp() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(GivePath)]
        private static void Give()
        {
            var all = Gloves.Kinds != null ? Gloves.Kinds.All : null;
            if (all == null || all.Count == 0)
            {
                Debug.LogWarning("[Gloves] No Glove Kinds (run Set Up Gloves first).");
                return;
            }
            var kind = all[Random.Range(0, all.Count)];
            Gloves.PickUp(kind);
            Debug.Log($"[Gloves] Put on: {kind.title} (+{kind.points} {PlayerLevel.Name(kind.stat)}). Make a fist to see them.");
        }

        [MenuItem(GivePath, true)]
        private static bool CanGive() => EditorApplication.isPlaying;

        [MenuItem(TakePath)]
        private static void Take()
        {
            Gloves.TakeOff();
            Debug.Log("[Gloves] Gloves off: grip + trigger does nothing now.");
        }

        [MenuItem(TakePath, true)]
        private static bool CanTake() => EditorApplication.isPlaying;

        // ---- Meshes -------------------------------------------------------------------------------------------------

        private static (Mesh mittR, Mesh cuffR, Mesh mittL, Mesh cuffL) Meshes(List<string> log)
        {
            var mittR = MeshAsset("Glove_Placeholder_Mitt_R", () => Mitt(), log);
            var cuffR = MeshAsset("Glove_Placeholder_Cuff_R", () => Cuff(), log);
            var mittL = MeshAsset("Glove_Placeholder_Mitt_L", () => Mirror(Mitt()), log);
            var cuffL = MeshAsset("Glove_Placeholder_Cuff_L", () => Mirror(Cuff()), log);
            return (mittR, cuffR, mittL, cuffL);
        }

        private static Mesh MeshAsset(string name, System.Func<Mesh> make, List<string> log)
        {
            string path = $"{ModelFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                log.Add($"mesh {name}: already there, kept");
                return mesh;
            }
            mesh = make();
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            log.Add($"mesh {name}: made ({mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles)");
            return mesh;
        }

        // The mitt and its thumb (a right hand's: the thumb on the palm side, -X).
        private static Mesh Mitt()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddBlob(vertices, triangles, MittCentre, new Vector3(MittBack, MittTop, MittFront), new Vector3(MittPalm, MittBottom, MittRear),
                Quaternion.identity, 2.6f, 28, 18);
            AddBlob(vertices, triangles, ThumbCentre, ThumbSize, ThumbSize, Quaternion.Euler(-8f, -18f, 0f), 2.2f, 16, 10);
            return Finish(vertices, triangles);
        }

        // The cuff: a short tube round the wrist, a little wider at its open end, closed there.
        private static Mesh Cuff()
        {
            const int sides = 24;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var centre = new Vector2(MittCentre.x, MittCentre.y);
            for (int ring = 0; ring < 2; ring++)
            {
                float z = ring == 0 ? CuffFrom : CuffTo;
                float flare = ring == 0 ? 1f : CuffFlare;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    vertices.Add(new Vector3(centre.x + Mathf.Cos(a) * CuffSize.x * flare, centre.y + Mathf.Sin(a) * CuffSize.y * flare, z));
                }
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                // Outward faces (clockwise seen from outside, Unity's front).
                triangles.AddRange(new[] { i, sides + i, j });
                triangles.AddRange(new[] { j, sides + i, sides + j });
            }
            // The open end's cap (seen from behind).
            int middle = vertices.Count;
            vertices.Add(new Vector3(centre.x, centre.y, CuffTo));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                triangles.AddRange(new[] { middle, sides + j, sides + i });
            }
            return Finish(vertices, triangles);
        }

        // A rounded box (a superellipsoid: Roundness 2 is an egg, more is boxier), with its own size on each side:
        // Plus for +X/+Y/+Z, Minus for -X/-Y/-Z. Shared vertices all round (no seams), so the normals are smooth.
        private static void AddBlob(List<Vector3> vertices, List<int> triangles, Vector3 centre, Vector3 plus, Vector3 minus,
            Quaternion turn, float roundness, int sides, int rings)
        {
            float e = 2f / roundness;
            float Curve(float v) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), e);
            int start = vertices.Count;
            // The two poles (front and back, along Z), and rings between them.
            vertices.Add(centre + turn * new Vector3(0f, 0f, plus.z));
            for (int r = 1; r < rings; r++)
            {
                float lat = Mathf.PI * r / rings;
                float cz = Curve(Mathf.Cos(lat)), sz = Curve(Mathf.Sin(lat));
                for (int s = 0; s < sides; s++)
                {
                    float lon = Mathf.PI * 2f * s / sides;
                    float x = sz * Curve(Mathf.Cos(lon));
                    float y = sz * Curve(Mathf.Sin(lon));
                    var p = new Vector3(x * (x >= 0f ? plus.x : minus.x), y * (y >= 0f ? plus.y : minus.y), cz * (cz >= 0f ? plus.z : minus.z));
                    vertices.Add(centre + turn * p);
                }
            }
            vertices.Add(centre + turn * new Vector3(0f, 0f, -minus.z));
            int last = vertices.Count - 1;
            int Ring(int r, int s) => start + 1 + (r - 1) * sides + (s % sides);
            for (int s = 0; s < sides; s++)
            {
                triangles.AddRange(new[] { start, Ring(1, s + 1), Ring(1, s) });
                triangles.AddRange(new[] { last, Ring(rings - 1, s), Ring(rings - 1, s + 1) });
            }
            for (int r = 1; r < rings - 1; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    triangles.AddRange(new[] { Ring(r, s), Ring(r, s + 1), Ring(r + 1, s) });
                    triangles.AddRange(new[] { Ring(r, s + 1), Ring(r + 1, s + 1), Ring(r + 1, s) });
                }
            }
        }

        private static Mesh Finish(List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Facing check: the triangles must face out, away from the middle; turned round if they don't.
            if (FacesIn(mesh))
            {
                var flipped = mesh.triangles;
                for (int i = 0; i < flipped.Length; i += 3) (flipped[i + 1], flipped[i + 2]) = (flipped[i + 2], flipped[i + 1]);
                mesh.triangles = flipped;
                mesh.RecalculateNormals();
            }
            return mesh;
        }

        // Whether most of the mesh's normals point in, towards its middle (the winding came out backwards).
        private static bool FacesIn(Mesh mesh)
        {
            var middle = mesh.bounds.center;
            var v = mesh.vertices;
            var n = mesh.normals;
            int inward = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (Vector3.Dot(n[i], v[i] - middle) < 0f) inward++;
            }
            return inward > v.Length / 2;
        }

        // The left hand's: the right one mirrored (X turned round, the triangles' order too so they still face out).
        private static Mesh Mirror(Mesh right)
        {
            var vertices = right.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i].x = -vertices[i].x;
            var triangles = right.triangles;
            for (int i = 0; i < triangles.Length; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Object.DestroyImmediate(right);
            return mesh;
        }

        // ---- Materials and prefabs ---------------------------------------------------------------------------------

        private static Material Material(string name, Color colour, List<string> log)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                log.Add($"material {name}: already there, kept");
                return material;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            // A little leathery shine.
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.55f);
            // Emission on (black): Hot Hands makes the glove glow red through it.
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, path);
            log.Add($"material {name}: made ({shader.name}, colour #{ColorUtility.ToHtmlStringRGB(colour)})");
            return material;
        }

        // One glove (a kind, a hand): Model (Mitt + Cuff) to look at, Punch Shape (a capsule round the mitt) that hits.
        private static GameObject GlovePrefab(string kind, bool left, Mesh mitt, Mesh cuff, Material look, Material cuffLook, List<string> log)
        {
            string name = $"Glove_{kind}_{(left ? "L" : "R")}";
            string path = $"{PrefabFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                log.Add($"prefab {name}: already there, kept (your model and colliders stay)");
                return existing;
            }
            var root = new GameObject(name);
            try
            {
                var model = new GameObject("Model").transform;
                model.SetParent(root.transform, false);
                Part("Mitt", model, mitt, look);
                Part("Cuff", model, cuff, cuffLook);
                var shape = new GameObject("Punch Shape").transform;
                shape.SetParent(root.transform, false);
                var capsule = shape.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.center = PunchCentre;
                capsule.radius = PunchRadius;
                capsule.height = PunchLength;
                var made = PrefabUtility.SaveAsPrefabAsset(root, path);
                log.Add($"prefab {name}: made (Model: Mitt + Cuff, Punch Shape: capsule r {PunchRadius} m, {PunchLength} m long)");
                return made;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Part(string name, Transform under, Mesh mesh, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(under, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        // ---- Glove Kinds -----------------------------------------------------------------------------------------------

        private static GloveKinds KindsAsset(Dictionary<string, (GameObject left, GameObject right)> prefabs, List<string> log)
        {
            var kinds = AssetDatabase.LoadAssetAtPath<GloveKinds>(KindsPath);
            bool made = kinds == null;
            if (made)
            {
                kinds = ScriptableObject.CreateInstance<GloveKinds>();
                AssetDatabase.CreateAsset(kinds, KindsPath);
            }
            var notes = new List<string>();
            var so = new SerializedObject(kinds);
            var list = so.FindProperty("kinds");
            // The starter kinds only into an empty list (kinds you took out stay out); empty glove slots are filled.
            if (list.arraySize == 0)
            {
                foreach (var kind in StarterKinds)
                {
                    int i = list.arraySize;
                    list.InsertArrayElementAtIndex(i);
                    var entry = list.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("id").stringValue = kind.id;
                    entry.FindPropertyRelative("title").stringValue = kind.title;
                    entry.FindPropertyRelative("stat").intValue = (int)kind.stat;
                    entry.FindPropertyRelative("points").floatValue = StarterPoints;
                    entry.FindPropertyRelative("colour").colorValue = kind.colour;
                    entry.FindPropertyRelative("easyFlyingPunches").boolValue = kind.easy;
                    entry.FindPropertyRelative("unlocks").intValue = (int)kind.unlocks;
                    entry.FindPropertyRelative("left").objectReferenceValue = null;
                    entry.FindPropertyRelative("right").objectReferenceValue = null;
                    notes.Add($"{kind.id}: +{StarterPoints} {PlayerLevel.Name(kind.stat)}");
                }
            }
            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("id").stringValue;
                if (!prefabs.TryGetValue(id, out var pair)) continue;
                var leftSlot = entry.FindPropertyRelative("left");
                var rightSlot = entry.FindPropertyRelative("right");
                if (leftSlot.objectReferenceValue == null && pair.left != null)
                {
                    leftSlot.objectReferenceValue = pair.left;
                    notes.Add($"{id} Left: {pair.left.name}");
                }
                if (rightSlot.objectReferenceValue == null && pair.right != null)
                {
                    rightSlot.objectReferenceValue = pair.right;
                    notes.Add($"{id} Right: {pair.right.name}");
                }
            }
            // Once (Menu Version): Warrior renamed Soldier (2), the starter kinds' Easy Flying Punches (1: Boxing on) and
            // Unlocks (2); later changes by hand are kept.
            var version = so.FindProperty("menuVersion");
            if (version != null && version.intValue < KindsMenuVersion)
            {
                for (int i = 0; i < list.arraySize; i++)
                {
                    var entry = list.GetArrayElementAtIndex(i);
                    var idProperty = entry.FindPropertyRelative("id");
                    if (version.intValue < 2 && idProperty.stringValue == "Warrior")
                    {
                        idProperty.stringValue = "Soldier";
                        entry.FindPropertyRelative("title").stringValue = "Soldier Gloves";
                        notes.Add("Warrior renamed Soldier");
                    }
                    string id = idProperty.stringValue;
                    foreach (var kind in StarterKinds)
                    {
                        if (kind.id != id) continue;
                        var easy = entry.FindPropertyRelative("easyFlyingPunches");
                        if (version.intValue < 1 && easy.boolValue != kind.easy)
                        {
                            easy.boolValue = kind.easy;
                            notes.Add($"{id} Easy Flying Punches: {kind.easy}");
                        }
                        var unlocks = entry.FindPropertyRelative("unlocks");
                        if (version.intValue < 2 && unlocks.intValue != (int)kind.unlocks)
                        {
                            unlocks.intValue = (int)kind.unlocks;
                            notes.Add($"{id} Unlocks: {kind.unlocks}");
                        }
                    }
                }
                version.intValue = KindsMenuVersion;
                if (!made && notes.Count == 0) notes.Add("menu version " + KindsMenuVersion);
            }
            var pop = Clip(PopSoundPath, PopSoundFallback);
            Fill(so, "popSound", pop, notes);
            Fill(so, "awaySound", pop, notes);
            // The pop's puff of smoke (user, 2026-11-18): the shared small puff, never Lana's Smoke_Blast.
            Fill(so, "popPuff", AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath), notes);
            if (notes.Count > 0 && !made) Backup(KindsPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(kinds);
            log.Add(made
                ? $"Glove Kinds: made at {KindsPath} ({string.Join(", ", notes)})"
                : notes.Count > 0 ? $"Glove Kinds: filled in {string.Join(", ", notes)}" : "Glove Kinds: already set up, kept");
            return kinds;
        }

        // The Blast Skills asset (user, 2026-11-12): made with its numbers' defaults and the punches' blast look; on a later
        // run only empty effect and sound slots are filled.
        private static void BlastSkillsAsset(List<string> log)
        {
            var skills = AssetDatabase.LoadAssetAtPath<Game.Weapons.BlastSkills>(BlastSkillsPath);
            bool made = skills == null;
            if (made)
            {
                skills = ScriptableObject.CreateInstance<Game.Weapons.BlastSkills>();
                AssetDatabase.CreateAsset(skills, BlastSkillsPath);
            }
            var notes = new List<string>();
            var so = new SerializedObject(skills);
            var effects = BlastEffectPaths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(e => e != null).ToArray();
            foreach (var property in new[] { "thrownEffects", "impactEffects", "masterEffects" })
            {
                var list = so.FindProperty(property);
                if (list == null || list.arraySize > 0 || effects.Length == 0) continue;
                list.arraySize = effects.Length;
                for (int i = 0; i < effects.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
                notes.Add($"{property}: {string.Join(" + ", effects.Select(e => e.name))}");
            }
            var bang = AssetDatabase.LoadAssetAtPath<AudioClip>(BlastSoundPath);
            Fill(so, "thrownSound", bang, notes);
            Fill(so, "masterSound", bang, notes);
            Fill(so, "impactSound", AssetDatabase.LoadAssetAtPath<AudioClip>(ImpactSoundPath), notes);
            // Once (Menu Version 1): the user's doubled damage (2026-11-12), reaches kept, into an asset made before it.
            var version = so.FindProperty("menuVersion");
            if (version != null && version.intValue < BlastSkillsMenuVersion)
            {
                if (!made)
                {
                    SetVector(so, "thrownDamage", new Vector3(2f, 4f, 6f), notes);
                    SetVector(so, "grenadeDamageBonus", new Vector3(0f, 2f, 4f), notes);
                    SetVector(so, "impactDamage", new Vector3(2f, 4f, 6f), notes);
                    SetY(so, "bomber", 6f, notes);
                    SetY(so, "master", 8f, notes);
                }
                version.intValue = BlastSkillsMenuVersion;
                if (!made && notes.Count == 0) notes.Add("menu version " + BlastSkillsMenuVersion);
            }
            if (notes.Count > 0 && !made) Backup(BlastSkillsPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skills);
            log.Add(made
                ? $"Blast Skills: made at {BlastSkillsPath} ({string.Join(", ", notes)})"
                : notes.Count > 0 ? $"Blast Skills: filled in {string.Join(", ", notes)}" : "Blast Skills: already set up, kept");
        }

        private static void SetVector(SerializedObject so, string property, Vector3 value, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null || p.vector3Value == value) return;
            notes.Add($"{property}: {p.vector3Value} -> {value}");
            p.vector3Value = value;
        }

        // Only the middle number (a blast's damage in Bomber Man's and Weapon Master's reach / damage / push).
        private static void SetY(SerializedObject so, string property, float y, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null || Mathf.Approximately(p.vector3Value.y, y)) return;
            var v = p.vector3Value;
            notes.Add($"{property} damage: {v.y} -> {y}");
            v.y = y;
            p.vector3Value = v;
        }

        private static AudioClip Clip(string path, string fallback)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return clip != null ? clip : AssetDatabase.LoadAssetAtPath<AudioClip>(fallback);
        }

        // Fills an empty object slot (a filled one is yours: kept).
        private static void Fill(SerializedObject so, string property, Object value, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null || p.objectReferenceValue != null || value == null) return;
            p.objectReferenceValue = value;
            notes.Add($"{property}: {value.name}");
        }

        // ---- The Knight's pair -----------------------------------------------------------------------------------------

        private static void Knight(GloveKinds kinds, Material stringLook, List<string> log)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == KnightPath)
            {
                log.Add("Knight.prefab: open in Prefab Mode, skipped (close it and run again)");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KnightPath) == null)
            {
                log.Add("Knight.prefab: missing, skipped");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(KnightPath);
            try
            {
                var boong = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == BoongName);
                if (boong == null)
                {
                    log.Add($"Knight.prefab: no {BoongName} (his boong) found, skipped");
                    return;
                }
                var notes = new List<string>();
                var hanging = boong.Find(HangingName);
                bool made = hanging == null;
                if (made)
                {
                    hanging = new GameObject(HangingName).transform;
                    hanging.SetParent(boong, false);
                    hanging.localPosition = HangPoint(boong);
                    hanging.localRotation = Quaternion.identity;
                    hanging.localScale = Vector3.one;
                    notes.Add($"{HangingName}: made under {BoongName} at {hanging.localPosition:F3} (its upper part)");
                }
                var pickup = hanging.GetComponent<GlovePickup>();
                if (pickup == null)
                {
                    pickup = hanging.gameObject.AddComponent<GlovePickup>();
                    notes.Add("Glove Pickup: added");
                }

                // Pivot (in metres) > String, Left Slot, Right Slot.
                var pivot = Child(hanging, "Pivot", notes);
                float scale = Mathf.Abs(hanging.lossyScale.x);
                var wanted = scale > 0.0001f ? Vector3.one / scale : Vector3.one;
                if ((pivot.localScale - wanted).sqrMagnitude > 1e-6f)
                {
                    pivot.localScale = wanted;
                    notes.Add($"Pivot: scaled {wanted.x:0.###} (metres under it)");
                }
                var leftSlot = Child(pivot, "Left Slot", notes, new Vector3(-HangApart, -StringLength, 0f));
                var rightSlot = Child(pivot, "Right Slot", notes, new Vector3(HangApart, -StringLength, 0f));
                if (pivot.Find("String") == null)
                {
                    var lace = new GameObject("String");
                    lace.transform.SetParent(pivot, false);
                    lace.transform.localPosition = new Vector3(0f, -StringLength * 0.5f, 0f);
                    lace.transform.localScale = new Vector3(StringWidth, StringLength * 0.5f, StringWidth);
                    lace.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                    var renderer = lace.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = stringLook;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    notes.Add("String: made");
                }

                var so = new SerializedObject(pickup);
                Set(so, "pivot", pivot, notes);
                Set(so, "leftSlot", leftSlot, notes);
                Set(so, "rightSlot", rightSlot, notes);
                Fill(so, "pickupSound", Clip(PickupSoundPath, PickupSoundFallback), notes);
                Fill(so, "puff", AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath), notes);
                Fill(so, "tutorialPicture", AssetDatabase.LoadAssetAtPath<Texture2D>(TutorialPicturePath), notes);
                so.ApplyModifiedPropertiesWithoutUndo();

                // The preview (Prefab Mode only): the first kind's pair, hanging as it will in the game.
                var first = kinds != null && kinds.Count > 0 ? kinds.All[0] : null;
                Preview(leftSlot, first != null ? first.For(true) : null, true, notes);
                Preview(rightSlot, first != null ? first.For(false) : null, false, notes);

                if (notes.Count == 0)
                {
                    log.Add("Knight.prefab: already set up, kept");
                    return;
                }
                string backup = Backup(KnightPath);
                PrefabUtility.SaveAsPrefabAsset(root, KnightPath);
                log.Add($"Knight.prefab: {string.Join(", ", notes)}; saved ({backup})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Where the pair hangs, in the boong's own space: on its long axis, most of the way to its end that's higher in
        // the Knight's pose here (move "Hanging Gloves" along the boong in Prefab Mode if it looks wrong in the game).
        private static Vector3 HangPoint(Transform boong)
        {
            var filter = boong.GetComponent<MeshFilter>();
            var skin = boong.GetComponent<SkinnedMeshRenderer>();
            var mesh = filter != null ? filter.sharedMesh : skin != null ? skin.sharedMesh : null;
            if (mesh == null) return Vector3.zero;
            var b = mesh.bounds;
            var axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? Vector3.right : b.size.y >= b.size.z ? Vector3.up : Vector3.forward;
            float half = Vector3.Dot(b.extents, axis);
            var a = b.center + axis * half;
            var c = b.center - axis * half;
            var high = boong.TransformPoint(a).y >= boong.TransformPoint(c).y ? a : c;
            return Vector3.Lerp(b.center, high, 0.7f);
        }

        // A child by name (made where asked if it isn't there; one that's there keeps where you put it).
        private static Transform Child(Transform under, string name, List<string> notes, Vector3 at = default)
        {
            var child = under.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(under, false);
            child.localPosition = at;
            notes.Add($"{name}: made");
            return child;
        }

        private static void Set(SerializedObject so, string property, Object value, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null || p.objectReferenceValue == value) return;
            p.objectReferenceValue = value;
            notes.Add($"{property}: {value.name}");
        }

        // One glove of the preview under its slot, placed as Glove Pickup hangs it (EditorOnly, called Preview: the game
        // removes it). Kept when it's there already.
        private static void Preview(Transform slot, GameObject prefab, bool left, List<string> notes)
        {
            if (slot.Find("Preview") != null || prefab == null) return;
            var holder = new GameObject("Preview") { tag = "EditorOnly" };
            holder.transform.SetParent(slot, false);
            var look = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
            PrefabUtility.UnpackPrefabInstance(look, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var collider in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            look.transform.localRotation = Quaternion.Euler(90f, left ? -12f : 12f, 0f);
            look.transform.localScale = prefab.transform.localScale;
            var bounds = Gloves.LookBounds(look.transform);
            var cuff = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            look.transform.localPosition = -(look.transform.localRotation * Vector3.Scale(cuff, look.transform.localScale));
            notes.Add($"{slot.name} preview: {prefab.name}");
        }

        // ---- The start pumpkin's tutorial -----------------------------------------------------------------------------

        // Its MAKE A FIST! page (the last) only for a player who can punch (with gloves): Show Notice's Glove Pages = 1.
        private static bool StartPumpkin(List<string> log)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == StartPumpkinPath)
            {
                log.Add("StartGamePumpkin.prefab: open in Prefab Mode, skipped (close it and run again)");
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StartPumpkinPath) == null)
            {
                log.Add("StartGamePumpkin.prefab: missing, skipped");
                return false;
            }
            var root = PrefabUtility.LoadPrefabContents(StartPumpkinPath);
            try
            {
                var pumpkin = root.transform.Find("pumpkin_Start");
                var component = pumpkin != null ? pumpkin.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Health") : null;
                if (component == null)
                {
                    log.Add("StartGamePumpkin.prefab: no pumpkin_Start Health FSM, skipped");
                    return false;
                }
                var fsm = component.Fsm;
                foreach (var s in fsm.States) s.LoadActions();
                var end = fsm.States.FirstOrDefault(s => s.Name == "End 1");
                var notice = end != null ? end.Actions.OfType<ShowNotice>().FirstOrDefault() : null;
                if (notice == null)
                {
                    log.Add("StartGamePumpkin.prefab: no End 1 tutorial (Show Notice), skipped (run Menu > Set Up Start Pumpkin Tutorial first)");
                    return false;
                }
                int pages = notice.titles != null ? notice.titles.Length : 0;
                string lastTitle = pages > 0 && notice.titles[pages - 1] != null ? notice.titles[pages - 1].Value : "";
                if (notice.glovePages != null && notice.glovePages.Value == 1)
                {
                    log.Add($"StartGamePumpkin.prefab: its last tutorial page ({lastTitle}) is already for gloves only, kept");
                    return false;
                }
                if (lastTitle.IndexOf("FIST", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    log.Add($"StartGamePumpkin.prefab: its last tutorial page is \"{lastTitle}\", not the fist page: left alone (set Glove Pages by hand if you want)");
                    return false;
                }
                notice.glovePages = new FsmInt { Value = 1 };
                end.SaveActions();
                EditorUtility.SetDirty(component);
                string backup = Backup(StartPumpkinPath);
                PrefabUtility.SaveAsPrefabAsset(root, StartPumpkinPath);
                log.Add($"StartGamePumpkin.prefab: End 1's tutorial page \"{lastTitle}\" shows only with gloves on (Glove Pages 1); saved ({backup})");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---- The scene (checked, never changed) -------------------------------------------------------------------------

        private static void CheckScene(List<string> log)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "2 Game Scene")
            {
                log.Add($"scene: '{scene.name}' is open, not 2 Game Scene: not checked (nothing in it needs changing anyway)");
                return;
            }
            var fists = Object.FindObjectsByType<Fist>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var bump = Object.FindObjectsByType<FistBump>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var notice = Object.FindObjectsByType<Game.Menus.TutorialNotice>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            log.Add($"scene: {fists.Length} Fist(s) ({string.Join(", ", fists.Select(f => f.name))}; 2 expected), " +
                    $"{bump.Length} Fist Bump, {notice.Length} Tutorial Notice. The Fists find the gloves by themselves " +
                    "(Resources/Glove Kinds), the Knight brings the pair.");
            // The harder flying-punch conditions (user, 2026-11-12): only where a Fist has none (0) yet.
            bool changed = false;
            foreach (var fist in fists)
            {
                var so = new SerializedObject(fist);
                var set = new List<string>();
                var kept = new List<string>();
                foreach (var (property, value) in HardFlying)
                {
                    var p = so.FindProperty(property);
                    if (p == null)
                    {
                        set.Add(property + " missing (old Fist.cs?)");
                        continue;
                    }
                    if (p.floatValue > 0f)
                    {
                        kept.Add($"{property} {p.floatValue:0.##}");
                        continue;
                    }
                    p.floatValue = value;
                    set.Add($"{property} {value:0.##}");
                }
                if (so.ApplyModifiedPropertiesWithoutUndo()) changed = true;
                log.Add($"scene {fist.name}: harder flying punches " + (set.Count > 0 ? "set: " + string.Join(", ", set) + "; " : "") +
                        (kept.Count > 0 ? "already there: " + string.Join(", ", kept) : ""));
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                log.Add("scene: CHANGED (harder flying-punch values written): save the scene.");
            }
            else
            {
                log.Add("scene: not changed (the harder values are new fields holding their defaults already). Nothing to " +
                        "save; the next save of the scene just writes them into the file.");
            }
            // The unlock points (user, 2026-11-12): only logged. Uppercut's is a new field (5); the old Lock Swing And
            // Uppercut tick and Uppercut At Fist Size are gone from the code, so the scene's old values no longer count.
            foreach (var level in Object.FindObjectsByType<PlayerLevel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(level);
                string Read(string name) => so.FindProperty(name) is SerializedProperty p
                    ? p.isArray ? string.Join("/", Enumerable.Range(0, p.arraySize).Select(i => p.GetArrayElementAtIndex(i).intValue)) : p.intValue.ToString()
                    : "missing";
                log.Add($"scene {level.name}: unlock points: Jab {Read("jabAtFistSize")}, Swing {Read("swingAtFistSize")}, Uppercut " +
                        $"{Read("uppercutUnlockPoints")}, Swollen {Read("swollenHandAtFistSize")}, Triple {Read("triplePunchAtFistSize")}; " +
                        $"Explosive {Read("explosiveAtThrowPower")}, Bomber Man {Read("bomberManAtThrowPower")}; Impact " +
                        $"{Read("impactAtWeaponEfficiency")}, Weapon Master {Read("weaponMasterAtWeaponEfficiency")} (user: 1, 3, 5, 6, 10; " +
                        "1/4/7, 10; 1/4/7, 10)");
            }
        }

        // ---- Files ---------------------------------------------------------------------------------------------------------

        private static void Folder(string path, List<string> log)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) Folder(parent, log);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            log.Add("folder " + path + ": made");
        }

        // A copy of the file as it is now, once (a later run keeps the first copy: the one from before the change).
        private static string Backup(string assetPath)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-12", BackupArea);
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, Path.GetFileName(assetPath));
            if (File.Exists(to)) return "backup from an earlier run kept";
            File.Copy(assetPath, to);
            return "backed up to " + to;
        }
    }
}
