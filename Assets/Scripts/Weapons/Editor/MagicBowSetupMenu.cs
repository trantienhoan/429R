using System.Collections.Generic;
using System.IO;
using Game.Combat;
using Game.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Weapons > Build Magic Bow: makes the Magic Bow and its endless light arrows (Assets/Prefabs/Weapon:
    /// Magic_Bow, Light_Arrow, Light_Arrow_Hit). The bow is a dark recurve with a leather grip, glowing gold gems at its
    /// tips and a glowing string; its meshes are made here (Assets/Data/Weapons/Magic Bow), so a modelled bow can replace
    /// the "Model" child later. Held like the Boong gun: the arrow points where the controller points. Running it again
    /// rebuilds everything (Weapon Power and the other numbers too).
    /// Tools > 429 Game > Cheats > Give Magic Bow drops one in front of the player while playing.
    /// </summary>
    public static class MagicBowSetupMenu
    {
        private const string BowPath = "Assets/Prefabs/Weapon/Magic_Bow.prefab";
        private const string ArrowPath = "Assets/Prefabs/Weapon/Light_Arrow.prefab";
        private const string HitPath = "Assets/Prefabs/Weapon/Light_Arrow_Hit.prefab";
        private const string DataFolder = "Assets/Data/Weapons/Magic Bow";
        private const string GlowTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string SoundFolder = "Assets/Audio/SFX/";

        // The bow: half its height, where the string sits behind the grip, and the arrow's way past the hand.
        private const float HalfLength = 0.55f;
        private const float GripHalf = 0.075f;
        private const float ArrowRestHeight = 0.09f;
        private const float ArrowLength = 0.8f;

        private static readonly Color Gold = new(1f, 0.78f, 0.38f);
        private static readonly Color PaleGold = new(1f, 0.92f, 0.7f);
        // The arrow's glowing body: warm gold, not white, so it reads as light rather than a blade.
        private static readonly Color ArrowColour = new(1f, 0.72f, 0.32f);

        [MenuItem("Tools/429 Game/Weapons/Build Magic Bow")]
        public static void Run()
        {
            Directory.CreateDirectory(DataFolder);
            var hit = BuildHitEffect();
            var arrow = BuildArrow(hit);
            BuildBow(arrow);
            AssetDatabase.SaveAssets();
            Debug.Log("[Magic Bow] Built " + BowPath + ", " + ArrowPath + " and " + HitPath + ".");
        }

        [MenuItem("Tools/429 Game/Cheats/Give Magic Bow")]
        public static void Give()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Magic Bow] Press Play first: the bow is dropped in front of the player.");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BowPath);
            var eyes = Camera.main;
            if (prefab == null || eyes == null) return;
            var forward = Vector3.ProjectOnPlane(eyes.transform.forward, Vector3.up).normalized;
            var at = eyes.transform.position + forward * 0.6f + Vector3.down * 0.5f;
            var bow = Object.Instantiate(prefab, at, Quaternion.LookRotation(forward));
            bow.name = prefab.name;
            Debug.Log("[Magic Bow] Dropped a Magic Bow in front of the player.", bow);
        }

        [MenuItem("Tools/429 Game/Cheats/Give Magic Bow", true)]
        private static bool CanGive() => EditorApplication.isPlaying;

        // ---- the bow ----

        private static void BuildBow(LightArrow arrowPrefab)
        {
            var wood = LitMaterial("Magic Bow Wood", new Color(0.17f, 0.09f, 0.12f), 0.38f, 0f);
            var leather = LitMaterial("Magic Bow Grip", new Color(0.08f, 0.05f, 0.04f), 0.2f, 0f);
            var glow = GlowMaterial("Magic Bow Glow", Gold, true);
            var stringGlow = GlowMaterial("Magic Bow String", PaleGold, false, false);
            var arrowGlow = GlowMaterial("Light Arrow", ArrowColour, true);
            var sparkle = GlowMaterial("Light Arrow Sparkle", Gold, false);

            var root = new GameObject("Magic_Bow") { tag = "Weapon" };
            var model = Child(root, "Model");
            var limbs = Child(model, "Limbs");
            limbs.AddComponent<MeshFilter>().sharedMesh = SaveMesh(BowMesh(), "Magic_Bow_Limbs");
            limbs.AddComponent<MeshRenderer>().sharedMaterials = new[] { wood, leather };

            var gem = SaveMesh(GemMesh(), "Magic_Bow_Gem");
            Vector3 top = LimbPoint(1f), bottom = LimbPoint(-1f);
            Gem(model, "Gem Top", gem, glow, top, 0.024f);
            Gem(model, "Gem Bottom", gem, glow, bottom, 0.024f);
            Gem(model, "Gem Rest", gem, glow, new Vector3(0f, ArrowRestHeight - 0.012f, 0.026f), 0.012f);

            // Grabbed by the grip: the controller's forward is the aim (like the Boong gun's barrel), its up along the bow.
            var attach = Child(root, "Attach");
            var arrowRest = Child(root, "Arrow Rest");
            arrowRest.transform.localPosition = new Vector3(0f, ArrowRestHeight, 0f);
            var stringTop = Child(root, "String Top");
            stringTop.transform.localPosition = top;
            var stringBottom = Child(root, "String Bottom");
            stringBottom.transform.localPosition = bottom;
            var nockRest = Child(root, "Nock Rest");
            nockRest.transform.localPosition = new Vector3(0f, ArrowRestHeight, Mathf.Lerp(bottom.z, top.z, 0.5f));

            var body = new List<Collider>
            {
                Capsule(root, "Collider Grip", Vector3.zero, Vector3.up * 0.13f, 0.026f, 0.32f),
                Capsule(root, "Collider Upper", LimbPoint(0.25f), LimbPoint(1f) - LimbPoint(0.25f), 0.02f, 0f),
                Capsule(root, "Collider Lower", LimbPoint(-0.25f), LimbPoint(-1f) - LimbPoint(-0.25f), 0.02f, 0f),
            };

            var line = Child(root, "String Line").AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.SetPositions(new[] { top, nockRest.transform.localPosition, bottom });
            line.widthMultiplier = 0.007f;
            line.numCapVertices = 2;
            line.sharedMaterial = stringGlow;
            line.colorGradient = Flat(PaleGold, 1f);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;

            // The string's handle: a small solid ball (the hands' near grab only sees solid colliders on Default).
            var handle = Child(root, "String");
            handle.transform.localPosition = nockRest.transform.localPosition;
            var handleBall = handle.AddComponent<SphereCollider>();
            handleBall.radius = 0.05f;
            var stringGrab = handle.AddComponent<XRSimpleInteractable>();
            stringGrab.colliders.Clear();
            stringGrab.colliders.Add(handleBall);

            var nocked = Child(root, "Nocked Arrow");
            nocked.transform.localPosition = nockRest.transform.localPosition;
            nocked.AddComponent<MeshFilter>().sharedMesh = SaveMesh(ArrowMesh(), "Light_Arrow");
            var nockedRenderer = nocked.AddComponent<MeshRenderer>();
            nockedRenderer.sharedMaterial = arrowGlow;
            nockedRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var fullDraw = FullDrawSparkles(Child(nocked, "Full Draw Sparkles"), sparkle);
            nocked.SetActive(false);

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.colliders.Clear();
            grab.colliders.AddRange(body);
            grab.attachTransform = attach.transform;
            grab.useDynamicAttach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = true;
            grab.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Single;

            var drawSound = Sound(root, "Draw Sound", "magic_bow_draw.wav", true);
            var shootSound = Sound(root, "Shoot Sound", null, false);

            var bow = root.AddComponent<MagicBow>();
            var so = new SerializedObject(bow);
            so.FindProperty("stringGrab").objectReferenceValue = stringGrab;
            so.FindProperty("stringTop").objectReferenceValue = stringTop.transform;
            so.FindProperty("stringBottom").objectReferenceValue = stringBottom.transform;
            so.FindProperty("nockRest").objectReferenceValue = nockRest.transform;
            so.FindProperty("arrowRest").objectReferenceValue = arrowRest.transform;
            so.FindProperty("stringLine").objectReferenceValue = line;
            so.FindProperty("nockedArrow").objectReferenceValue = nocked.transform;
            so.FindProperty("fullDrawSparkles").objectReferenceValue = fullDraw;
            so.FindProperty("arrowPrefab").objectReferenceValue = arrowPrefab;
            so.FindProperty("drawSound").objectReferenceValue = drawSound;
            so.FindProperty("shootSound").objectReferenceValue = shootSound;
            SetClips(so.FindProperty("shootClips"), "magic_bow_shoot_1.wav", "magic_bow_shoot_2.wav");
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BowPath);
            Object.DestroyImmediate(root);
        }

        // A point on the bow's middle line: -1 = the bottom tip, 1 = the top tip.
        private static Vector3 LimbPoint(float along)
        {
            float s = Mathf.Abs(along);
            return new Vector3(0f, along * HalfLength, Bend(s));
        }

        // How far the limb bends back towards the string (negative z) at this share of its length: a gentle curve whose
        // tips curl forward again (a recurve).
        private static float Bend(float s)
        {
            float curl = Mathf.Max(0f, (s - 0.78f) / 0.22f);
            return -0.2f * s * s + 0.05f * curl * curl;
        }

        private static float BendSlope(float s)
        {
            float curl = Mathf.Max(0f, (s - 0.78f) / 0.22f);
            return -0.4f * s + (s > 0.78f ? 0.1f * curl / 0.22f : 0f);
        }

        // The bow's body: a tube along the middle line, thick and round at the grip (its own material), flat and thin
        // towards the tips.
        private static Mesh BowMesh()
        {
            const int rings = 80, sides = 12;
            var vertices = new List<Vector3>();
            var limbFaces = new List<int>();
            var gripFaces = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float along = Mathf.Lerp(-1f, 1f, i / (float)rings);
                float y = along * HalfLength;
                float s = Mathf.Abs(along);
                var centre = LimbPoint(along);
                var tangent = new Vector3(0f, 1f, BendSlope(s) * Mathf.Sign(along) / HalfLength).normalized;
                var side = Vector3.right;
                var front = Vector3.Cross(side, tangent).normalized;

                // Limb size (width across, depth front-to-back), then the round grip blended in near the middle.
                float width = Mathf.Lerp(0.032f, 0.012f, s);
                float depth = Mathf.Lerp(0.026f, 0.013f, s);
                float grip = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GripHalf, GripHalf + 0.04f, Mathf.Abs(y)));
                width = Mathf.Lerp(width, 0.038f, grip);
                depth = Mathf.Lerp(depth, 0.044f, grip);

                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    vertices.Add(centre + side * (Mathf.Cos(a) * width * 0.5f) + front * (Mathf.Sin(a) * depth * 0.5f));
                }
                if (i == 0) continue;

                float middle = Mathf.Abs(Mathf.Lerp(-1f, 1f, (i - 0.5f) / rings) * HalfLength);
                var faces = middle < GripHalf ? gripFaces : limbFaces;
                int a0 = (i - 1) * sides, b0 = i * sides;
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    faces.AddRange(new[] { a0 + j, b0 + j, b0 + j1, a0 + j, b0 + j1, a0 + j1 });
                }
            }
            // Caps on both ends.
            int last = rings * sides;
            vertices.Add(LimbPoint(-1f));
            vertices.Add(LimbPoint(1f));
            int bottomCap = vertices.Count - 2, topCap = vertices.Count - 1;
            for (int j = 0; j < sides; j++)
            {
                int j1 = (j + 1) % sides;
                limbFaces.AddRange(new[] { bottomCap, j1, j });
                limbFaces.AddRange(new[] { topCap, last + j, last + j1 });
            }

            var mesh = new Mesh { name = "Magic_Bow_Limbs" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(limbFaces, 0);
            mesh.SetTriangles(gripFaces, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A small eight-sided gem (radius 1, its colour all from the glow texture's middle).
        private static Mesh GemMesh()
        {
            var v = new[] { Vector3.up * 1.3f, Vector3.down * 1.3f, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            var faces = new[] { 0, 4, 2, 0, 2, 5, 0, 5, 3, 0, 3, 4, 1, 2, 4, 1, 5, 2, 1, 3, 5, 1, 4, 3 };
            return FlatMesh("Magic_Bow_Gem", v, faces);
        }

        // The arrow of light, tail at its pivot, pointing along Z: a fletched tail, a thin shaft and a broad head.
        private static Mesh ArrowMesh()
        {
            var v = new List<Vector3>();
            var f = new List<int>();
            Spindle(v, f, 0f, 0.13f, 0.02f, 0.06f);
            Spindle(v, f, 0.02f, ArrowLength - 0.12f, 0.008f, 0.5f);
            Spindle(v, f, ArrowLength - 0.19f, ArrowLength, 0.03f, 0.35f);
            return FlatMesh("Light_Arrow", v.ToArray(), f.ToArray());
        }

        // A four-sided spindle along Z from 'from' to 'to', widest (radius) at 'widest' (0-1 along it).
        private static void Spindle(List<Vector3> v, List<int> f, float from, float to, float radius, float widest)
        {
            int start = v.Count;
            v.Add(new Vector3(0f, 0f, from));
            v.Add(new Vector3(0f, 0f, to));
            float z = Mathf.Lerp(from, to, widest);
            v.Add(new Vector3(radius, 0f, z));
            v.Add(new Vector3(0f, radius, z));
            v.Add(new Vector3(-radius, 0f, z));
            v.Add(new Vector3(0f, -radius, z));
            for (int i = 0; i < 4; i++)
            {
                int a = start + 2 + i, b = start + 2 + (i + 1) % 4;
                f.AddRange(new[] { start, b, a, start + 1, a, b });
            }
        }

        private static Mesh FlatMesh(string name, Vector3[] vertices, int[] faces)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(0.5f, 0.5f);
            mesh.uv = uv;
            mesh.triangles = faces;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = DataFolder + "/" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // Updated in place, so prefabs using it keep their reference.
            old.Clear();
            old.vertices = mesh.vertices;
            old.uv = mesh.uv;
            old.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++) old.SetTriangles(mesh.GetTriangles(i), i);
            old.RecalculateNormals();
            old.RecalculateBounds();
            EditorUtility.SetDirty(old);
            return old;
        }

        private static void Gem(GameObject parent, string name, Mesh mesh, Material material, Vector3 at, float size)
        {
            var gem = Child(parent, name);
            gem.transform.localPosition = at;
            gem.transform.localScale = Vector3.one * size;
            gem.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gem.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // A capsule from 'from' along 'along'; or, with a Length, one that long standing upright with its middle at 'from'.
        private static Collider Capsule(GameObject root, string name, Vector3 from, Vector3 along, float radius, float length)
        {
            var go = Child(root, name);
            float height = length > 0f ? length : along.magnitude + radius * 2f;
            var direction = length > 0f ? Vector3.up : along.normalized;
            go.transform.localPosition = length > 0f ? from : from + along * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = radius;
            capsule.height = height;
            return capsule;
        }

        private static ParticleSystem FullDrawSparkles(GameObject go, Material material)
        {
            go.transform.localPosition = new Vector3(0f, 0f, ArrowLength);
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 40);
            var main = ps.main;
            main.prewarm = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new MinMaxCurve(0.2f, 0.4f);
            main.startSize = new MinMaxCurve(0.02f, 0.05f);
            main.startSpeed = new MinMaxCurve(0.2f, 0.6f);
            main.startColor = new MinMaxGradient(Gold, Color.white);
            var emission = ps.emission;
            emission.rateOverTime = 45f;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;
            DangerFlameSetupMenu.Shrink(ps, 1f, 1f, 0f);
            return ps;
        }

        // ---- the arrow ----

        private static LightArrow BuildArrow(GameObject hitEffect)
        {
            var arrowGlow = GlowMaterial("Light Arrow", ArrowColour, true);
            var trailGlow = GlowMaterial("Light Arrow Trail", Gold, false);
            var sparkle = GlowMaterial("Light Arrow Sparkle", Gold, false);

            var root = new GameObject("Light_Arrow") { tag = "Weapon" };
            var body = Child(root, "Body");
            body.AddComponent<MeshFilter>().sharedMesh = SaveMesh(ArrowMesh(), "Light_Arrow");
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = arrowGlow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            // It hits with its tip: a ball a little bigger than the head, so near misses on small bugs still count.
            var tip = root.AddComponent<SphereCollider>();
            tip.center = new Vector3(0f, 0f, ArrowLength - 0.05f);
            tip.radius = 0.045f;

            var trailGo = Child(root, "Trail");
            trailGo.transform.localPosition = new Vector3(0f, 0f, ArrowLength * 0.5f);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = 0.04f;
            trail.colorGradient = Fade(Gold, 0.9f);
            trail.sharedMaterial = trailGlow;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            var sparklesGo = Child(root, "Sparkles");
            sparklesGo.transform.localPosition = new Vector3(0f, 0f, ArrowLength * 0.3f);
            var sparkles = DangerFlameSetupMenu.NewSystem(sparklesGo, sparkle, ParticleSystemRenderMode.Billboard, 40);
            var main = sparkles.main;
            main.prewarm = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new MinMaxCurve(0.25f, 0.45f);
            main.startSize = new MinMaxCurve(0.02f, 0.05f);
            main.startSpeed = new MinMaxCurve(0f, 0.3f);
            main.gravityModifier = 0.3f;
            main.startColor = new MinMaxGradient(Gold, Color.white);
            var emission = sparkles.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 6f;
            var shape = sparkles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;
            DangerFlameSetupMenu.Shrink(sparkles, 1f, 1f, 0f);

            var hitSound = Sound(root, "Hit Sound", null, false);
            hitSound.maxDistance = 25f;

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 0.05f;
            rigidbody.useGravity = false;
            rigidbody.linearDamping = 0f;
            rigidbody.angularDamping = 0f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Hit power = speed x 0.4: a full draw (48 m/s) is a top hit, a weak one (14 m/s) a light one. Damage 1.6 =
            // a hammer's: a full-draw shot does 8, one spider.
            var power = root.AddComponent<WeaponPower>();
            power.Power = 0.4f;
            power.Damage = 1.6f;

            var arrow = root.AddComponent<LightArrow>();
            var so = new SerializedObject(arrow);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("trail").objectReferenceValue = trail;
            so.FindProperty("sparkles").objectReferenceValue = sparkles;
            so.FindProperty("hitEffect").objectReferenceValue = hitEffect;
            so.FindProperty("hitSound").objectReferenceValue = hitSound;
            SetClips(so.FindProperty("hitClips"), "magic_arrow_hit_1.wav", "magic_arrow_hit_2.wav");
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, ArrowPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<LightArrow>();
        }

        // The burst where an arrow hits: a quick flash and a spray of gold sparks.
        private static GameObject BuildHitEffect()
        {
            var flashMaterial = GlowMaterial("Light Arrow Flash", PaleGold, false);
            var sparkMaterial = GlowMaterial("Light Arrow Sparkle", Gold, false);
            var root = new GameObject("Light_Arrow_Hit");

            var flash = DangerFlameSetupMenu.NewSystem(Child(root, "Flash"), flashMaterial, ParticleSystemRenderMode.Billboard, 2);
            OneShot(flash, 0.12f);
            var main = flash.main;
            main.startSize = 0.45f;
            main.startColor = Color.white;
            var burst = flash.emission;
            burst.SetBursts(new[] { new Burst(0f, 1) });
            DangerFlameSetupMenu.Shrink(flash, 0.6f, 1f, 0f);

            var sparks = DangerFlameSetupMenu.NewSystem(Child(root, "Sparks"), sparkMaterial, ParticleSystemRenderMode.Stretch, 40);
            OneShot(sparks, 0.4f);
            main = sparks.main;
            main.startLifetime = new MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new MinMaxCurve(1.5f, 4f);
            main.startSize = new MinMaxCurve(0.015f, 0.035f);
            main.gravityModifier = 0.6f;
            main.startColor = new MinMaxGradient(Gold, Color.white);
            burst = sparks.emission;
            burst.SetBursts(new[] { new Burst(0f, 22) });
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;
            var stretch = sparks.GetComponent<ParticleSystemRenderer>();
            stretch.velocityScale = 0.04f;
            stretch.lengthScale = 1f;
            DangerFlameSetupMenu.Shrink(sparks, 1f, 1f, 0f);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, HitPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void OneShot(ParticleSystem ps, float duration)
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.duration = duration;
            main.startLifetime = duration;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
        }

        // ---- shared bits ----

        private static GameObject Child(GameObject parent, string name) => DangerFlameSetupMenu.Child(parent, name);

        private static Material LitMaterial(string name, Color colour, float smoothness, float metallic)
        {
            string path = DataFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Additive, unlit glow. Meshes using it have every UV on the glow texture's bright middle. Without the texture
        // (Soft off) it's one even colour, e.g. the string, which would otherwise fade towards its ends.
        private static Material GlowMaterial(string name, Color colour, bool doubleSided, bool soft = true)
        {
            var material = DangerFlameSetupMenu.MakeMaterial(name, GlowTexture, true);
            if (!soft) material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", colour);
            if (doubleSided) material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AudioSource Sound(GameObject parent, string name, string clip, bool loop)
        {
            var source = Child(parent, name).AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.5f;
            source.maxDistance = 12f;
            source.dopplerLevel = 0f;
            if (clip != null) source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundFolder + clip);
            return source;
        }

        private static void SetClips(SerializedProperty list, params string[] clips)
        {
            list.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundFolder + clips[i]);
        }

        private static Gradient Flat(Color colour, float alpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
            return gradient;
        }

        private static Gradient Fade(Color colour, float alpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(colour, 0.3f), new GradientColorKey(colour, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
