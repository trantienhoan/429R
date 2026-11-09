using System.Collections.Generic;
using System.Linq;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Set Up Slime Pins: makes Gate 2's ("Strike the Slimes") pin pieces in
    /// Prefabs/Bowling. Safe to run again (it makes them anew; the originals are never touched).
    /// - Slime_Pin_Pop: a variant of Flying_Slime with a smaller burst (6 big blobs, 8 droplets), wings off, no drips.
    /// - Slime_Pin_Flyer: a variant of Slime_Bomber at scale 0.6, flying 3.5 m/s, living 22 s.
    /// - Slime_Pin: a physics pin (Rigidbody 1.5 kg, tiny foot box, capsule, beehive_pin material, speaker, Slime Pin)
    ///   with a child "Shape" (pivot at the foot) holding the Flying_Slime model, wings off, in Flying_Slime's skin.
    /// </summary>
    public static class SlimePinSetupMenu
    {
        private const string MenuPath = "Tools/429 Game/Bowling/Set Up Slime Pins";
        private const string Folder = "Assets/Prefabs/Bowling";
        private const string PinPath = Folder + "/Slime_Pin.prefab";
        private const string PopPath = Folder + "/Slime_Pin_Pop.prefab";
        private const string FlyerPath = Folder + "/Slime_Pin_Flyer.prefab";

        private const string SlimeSource = "Assets/Prefabs/Enemies/Flying_Slime.prefab";
        private const string BomberSource = "Assets/Prefabs/Enemies/Slime_Bomber.prefab";
        private const string ModelSource = "Assets/Models/Enemies/Flying_Slime.fbx";
        private const string PinMaterial = "Assets/Prefabs/PlayGround/beehive_pin.physicMaterial";
        private const string Puff = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string ShiverClip = "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/slime_000.ogg";
        private const string HatchClip = "Assets/Audio/SFX/Balloon_Pop.wav";

        private const int PopBigBlobs = 6;
        private const int PopDroplets = 8;
        private const float FlyerScale = 0.6f;
        private const float FlyerSpeed = 3.5f;
        private const float FlyerLife = 22f;
        private const float PinMass = 1.5f;
        private static readonly Vector2 StartSize = new(0.66f, 0.24f);   // height, width (metres)
        private const float CapsuleLift = 0.02f;

        [MenuItem(MenuPath)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Bowling] Stop play mode first."); return; }
            Debug.Log("[Bowling] Set Up Slime Pins:\n" + string.Join("\n", Run()));
        }

        [MenuItem(MenuPath, true)]
        private static bool MenuCheck() => !EditorApplication.isPlaying;

        /// <summary>Makes Slime_Pin_Pop, Slime_Pin_Flyer and Slime_Pin. Returns what it did, one line each.</summary>
        public static List<string> Run()
        {
            var notes = new List<string>();
            if (EditorApplication.isPlaying)
            {
                notes.Add("Stop play mode first; nothing made.");
                Debug.LogError("[Bowling] Set Up Slime Pins: stop play mode first.");
                return notes;
            }
            var missing = new[] { SlimeSource, BomberSource, ModelSource, PinMaterial, Puff, ShiverClip, HatchClip }
                .Where(p => AssetDatabase.LoadAssetAtPath<Object>(p) == null).ToList();
            if (missing.Count > 0)
            {
                Debug.LogError("[Bowling] Missing, nothing built: " + string.Join(", ", missing));
                notes.Add("Nothing built (missing assets).");
                return notes;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Bowling");

            notes.Add(MakePop());
            notes.Add(MakeFlyer());
            AssetDatabase.SaveAssets();
            notes.Add(MakePin());
            AssetDatabase.SaveAssets();
            return notes;
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        // ---------- Slime_Pin_Pop ----------

        private static string MakePop()
        {
            var build = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(SlimeSource), build);
                root.name = "Slime_Pin_Pop";
                root.tag = "Untagged";
                int wings = HideWings(root);
                var look = root.GetComponent<FlyingSlime>();
                if (look == null) return "Slime_Pin_Pop: NOT MADE (Flying_Slime has no Flying Slime)";
                var so = new SerializedObject(look);
                Set(so, "popBigBlobs", PopBigBlobs);
                Set(so, "popDroplets", PopDroplets);
                Set(so, "dripsPerSecond", 0f);
                SetBool(so, "destroyOnPop", true);
                so.ApplyModifiedPropertiesWithoutUndo();
                ShadowsOff(root);
                PrefabUtility.SaveAsPrefabAsset(root, PopPath, out bool saved);
                return saved
                    ? $"Slime_Pin_Pop: Flying_Slime variant, {PopBigBlobs} blobs + {PopDroplets} droplets (+ shell) a pop, wings off ({wings}), no drips"
                    : "Slime_Pin_Pop: COULD NOT SAVE " + PopPath;
            }
            finally { EditorSceneManager.ClosePreviewScene(build); }
        }

        // ---------- Slime_Pin_Flyer ----------

        private static string MakeFlyer()
        {
            var build = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BomberSource), build);
                root.name = "Slime_Pin_Flyer";
                root.transform.localScale = Vector3.one * FlyerScale;
                var bomber = root.GetComponent<SlimeBomber>();
                if (bomber == null) return "Slime_Pin_Flyer: NOT MADE (Slime_Bomber has no Slime Bomber)";
                var so = new SerializedObject(bomber);
                Set(so, "flySpeed", FlyerSpeed);
                Set(so, "lifeSeconds", FlyerLife);
                so.ApplyModifiedPropertiesWithoutUndo();
                ShadowsOff(root);
                PrefabUtility.SaveAsPrefabAsset(root, FlyerPath, out bool saved);
                return saved
                    ? $"Slime_Pin_Flyer: Slime_Bomber variant, scale {FlyerScale}, fly {FlyerSpeed} m/s, life {FlyerLife} s, tag {root.tag}"
                    : "Slime_Pin_Flyer: COULD NOT SAVE " + FlyerPath;
            }
            finally { EditorSceneManager.ClosePreviewScene(build); }
        }

        // ---------- Slime_Pin ----------

        private static string MakePin()
        {
            var pop = Load<GameObject>(PopPath);
            if (pop == null) return "Slime_Pin: NOT MADE (no Slime_Pin_Pop)";
            var physics = Load<PhysicsMaterial>(PinMaterial);
            var skins = Skins();

            var build = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Slime_Pin");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, build);
                var shape = new GameObject("Shape").transform;
                shape.SetParent(root.transform, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ModelSource), build);
                model.name = "Model";
                model.transform.SetParent(shape, false);

                // Wings folded (off); the rest in Flying_Slime's own materials, no shadows.
                int wings = HideWings(model);
                Renderer body = null;
                int skinned = 0;
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (skins.TryGetValue(r.name, out var mats))
                    {
                        r.sharedMaterials = mats;
                        skinned++;
                    }
                    r.lightProbeUsage = LightProbeUsage.BlendProbes;
                    if (r.name == "Slime_Body") body = r;
                }
                ShadowsOff(root);
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.tag = "Untagged";
                    t.gameObject.layer = 0;
                }
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                // Foot at Shape's origin: the model's bottom on it, centred.
                var bounds = LocalBounds(shape, model);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var size = bounds.size;
                shape.localScale = new Vector3(StartSize.y / Mathf.Max(0.001f, size.x), StartSize.x / Mathf.Max(0.001f, size.y),
                                               StartSize.y / Mathf.Max(0.001f, size.x));

                var rigid = root.AddComponent<Rigidbody>();
                rigid.mass = PinMass;
                rigid.linearDamping = 0f;
                rigid.angularDamping = 0.05f;
                rigid.useGravity = true;
                rigid.interpolation = RigidbodyInterpolation.Interpolate;
                rigid.collisionDetectionMode = CollisionDetectionMode.Continuous;

                var foot = root.AddComponent<BoxCollider>();
                foot.center = new Vector3(0f, 0.02f, 0f);
                foot.size = new Vector3(0.08f, 0.04f, 0.08f);
                foot.sharedMaterial = physics;

                var capsule = root.AddComponent<CapsuleCollider>();
                float tall = StartSize.x - CapsuleLift;
                capsule.direction = 1;
                capsule.radius = Mathf.Min(StartSize.y * 0.5f, tall * 0.5f);
                capsule.height = tall;
                capsule.center = new Vector3(0f, CapsuleLift + tall * 0.5f, 0f);
                capsule.sharedMaterial = physics;

                var speaker = root.AddComponent<AudioSource>();
                speaker.playOnAwake = false;
                speaker.loop = false;
                speaker.spatialBlend = 1f;
                speaker.rolloffMode = AudioRolloffMode.Linear;
                speaker.minDistance = 1f;
                speaker.maxDistance = 15f;
                speaker.dopplerLevel = 0f;

                var pin = root.AddComponent<SlimePin>();
                var so = new SerializedObject(pin);
                so.FindProperty("shape").objectReferenceValue = shape;
                so.FindProperty("body").objectReferenceValue = body;
                so.FindProperty("foot").objectReferenceValue = foot;
                so.FindProperty("capsule").objectReferenceValue = capsule;
                so.FindProperty("speaker").objectReferenceValue = speaker;
                so.FindProperty("modelSize").vector3Value = size;
                so.FindProperty("startSize").vector2Value = StartSize;
                so.FindProperty("capsuleLift").floatValue = CapsuleLift;
                so.FindProperty("popPrefab").objectReferenceValue = pop;
                so.FindProperty("puff").objectReferenceValue = Load<GameObject>(Puff);
                so.FindProperty("shiverSound").objectReferenceValue = Load<AudioClip>(ShiverClip);
                so.FindProperty("hatchSound").objectReferenceValue = Load<AudioClip>(HatchClip);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PinPath, out bool saved);
                if (!saved) return "Slime_Pin: COULD NOT SAVE " + PinPath;
                if (body == null) Debug.LogWarning("[Bowling] Slime_Pin: the model has no Slime_Body; the pin won't take its colour.");
                return $"Slime_Pin: mass {PinMass}, foot 0.08 x 0.04 x 0.08, capsule, {(physics != null ? physics.name : "NO")} material, " +
                       $"model {size.x:0.###} x {size.y:0.###} x {size.z:0.###} m at scale 1 -> {StartSize.x} tall x {StartSize.y} wide, " +
                       $"wings off ({wings}), {skinned} renderers in Flying_Slime's skin, body {(body != null ? "found" : "NOT FOUND")}";
            }
            finally { EditorSceneManager.ClosePreviewScene(build); }
        }

        // Flying_Slime prefab's materials by renderer name (its model's parts).
        private static Dictionary<string, Material[]> Skins()
        {
            var skins = new Dictionary<string, Material[]>();
            var slime = Load<GameObject>(SlimeSource);
            foreach (var r in slime.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!skins.ContainsKey(r.name)) skins[r.name] = r.sharedMaterials;
            }
            return skins;
        }

        // The bounds of Under's shown meshes, in Space's frame.
        private static Bounds LocalBounds(Transform space, GameObject under)
        {
            bool any = false;
            var b = new Bounds();
            var toSpace = space.worldToLocalMatrix;
            foreach (var filter in under.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null) continue;
                var m = toSpace * filter.transform.localToWorldMatrix;
                var mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            if (!any) b = new Bounds(new Vector3(0f, 0.165f, 0f), new Vector3(0.26f, 0.33f, 0.25f));
            return b;
        }

        private static int HideWings(GameObject under)
        {
            int n = 0;
            foreach (var t in under.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Slime_Wing_L" && t.name != "Slime_Wing_R") continue;
                t.gameObject.SetActive(false);
                n++;
            }
            if (n < 2) Debug.LogWarning($"[Bowling] {under.name}: found {n} of 2 wings (Slime_Wing_L/R).");
            return n;
        }

        private static void ShadowsOff(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void Set(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[Bowling] No field " + name + " on " + so.targetObject.GetType().Name); return; }
            if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
            else p.floatValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[Bowling] No field " + name + " on " + so.targetObject.GetType().Name); return; }
            p.boolValue = value;
        }
    }
}
