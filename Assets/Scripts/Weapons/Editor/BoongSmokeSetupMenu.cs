using System.Collections.Generic;
using Game.Leveling;
using Game.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Weapons > Set Up Boong Smoking: the five boongs (Hammer_Boong and Hammer_Boong 2ND give the
    /// Green Boong bonus, Hammer_Boong_Bone the Bone Boong one, Elon_Boong Elon's, Bus_Boong the bus') get a Boong Smoke:
    /// held the right way up with the mouthpiece at the mouth and a lit candle at the bowl, they catch fire, burn for
    /// 2.5 s and explode into the bonus. It makes the two points it needs, "Inhale" at the mouthpiece opening and "Burn"
    /// just above the bowl, on the bone that carries each part (so they follow the fire animation), and on the hammer
    /// boongs, which have no flame of their own, a small "Boong Flame" in the bowl (no light, for Quest). Running it
    /// again keeps the points where they are and the numbers on Boong Smoke as they are (tuned by hand); it only fills
    /// in what's missing. Up Axis (the way the bowl's opening faces) and Max Tilt (75) are set on every run. Boong_Gun
    /// (the Skull Gun) is no boong and is left out.
    /// </summary>
    public static class BoongSmokeSetupMenu
    {
        private const string InhaleName = "Inhale";
        private const string BurnName = "Burn";
        private const string FlameName = "CandleFlame";
        private const string NewFlameName = "Boong Flame";
        private const string FireState = "Fire";

        private const string FlamePrefab = "Assets/XRI_Examples/Props/Prefabs/CandleFlame.prefab";
        private const string SmokePrefab = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        // A gloopy bubbling loop (4.7 s), and a whoosh when the bowl catches.
        private const string BubbleSound = "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/slime_001.ogg";
        private const string IgniteSound = "Assets/Audio/SFX/whoosh.wav";
        // The new flame's size (like the bone boong's CandleFlame, 0.15).
        private const float NewFlameScale = 0.15f;
        // The Burn point sits this far above the bowl's cup (along the way the cup faces), where a flame is held.
        private const float AboveBowl = 0.015f;
        // How far the bowl may lean from facing straight up, in degrees ("hold it so the bowl faces up").
        private const float MaxTilt = 75f;

        private sealed class Boong
        {
            public string Path;
            public Buff Buff;
            // "The right way up" is the bowl facing up: the way the bowl's opening faces.
            public Vector3 Up => BowlFaces.normalized;
            // Root-local points (metres at the boong's own scale), measured on the meshes, and the bone each goes on.
            public Vector3 Inhale;
            public string InhaleOn;
            public float InhaleRadius;
            public Vector3 Bowl;
            public Vector3 BowlFaces;
            public string BurnOn;
            public float BurnRadius;
            public string Shake;
            public string Swell;
            public bool BreakApart;
        }

        // The mouthpiece's outer rim and the bowl cup's outer rim give the radii.
        private static readonly Boong[] Boongs =
        {
            new()
            {
                Path = "Assets/Prefabs/Weapon/Hammer_Boong.prefab", Buff = Buff.GreenBoong,
                Inhale = new Vector3(0f, 0f, 0.157f), InhaleOn = "Boong_E031_Armature/Bone", InhaleRadius = 0.059f,
                Bowl = new Vector3(0f, 0.202f, -0.180f), BowlFaces = new Vector3(0f, 0.75f, 0.67f),
                BurnOn = "Boong_E031_Armature/Bone/Bone.001", BurnRadius = 0.04f,
                Shake = "Boong_E031_Armature", BreakApart = true,
            },
            new()
            {
                Path = "Assets/Prefabs/Weapon/Hammer_Boong 2ND.prefab", Buff = Buff.GreenBoong,
                Inhale = new Vector3(0f, 0f, 0.157f), InhaleOn = "Boong_E031_Armature/Bone", InhaleRadius = 0.059f,
                Bowl = new Vector3(0f, 0.202f, -0.180f), BowlFaces = new Vector3(0f, 0.75f, 0.67f),
                BurnOn = "Boong_E031_Armature/Bone/Bone.001", BurnRadius = 0.04f,
                Shake = "Boong_E031_Armature", BreakApart = true,
            },
            new()
            {
                Path = "Assets/Prefabs/Weapon/Hammer_Boong_Bone.prefab", Buff = Buff.BoneBoong,
                Inhale = new Vector3(0.009f, -0.001f, 0.679f), InhaleOn = "Boong_Bone/Boong_Bone_Armature/Bone.001", InhaleRadius = 0.035f,
                Bowl = new Vector3(0.018f, 0.314f, 0.095f), BowlFaces = new Vector3(0f, 0.93f, 0.37f),
                BurnOn = "Boong_Bone/Boong_Bone_Armature/Bone.001/Bone/Bone.003/Bone.004", BurnRadius = 0.05f,
                Shake = "Boong_Bone", BreakApart = true,
            },
            new()
            {
                Path = "Assets/Prefabs/Weapon/Elon_Boong.prefab", Buff = Buff.ElonBoong,
                Inhale = new Vector3(-0.270f, 0.269f, 0f), InhaleOn = "Boong", InhaleRadius = 0.055f,
                Bowl = new Vector3(0.305f, 0.172f, 0f), BowlFaces = new Vector3(0.33f, 0.94f, 0f),
                BurnOn = "Boong", BurnRadius = 0.04f,
                Shake = "Boong", Swell = "Boong", BreakApart = false,
            },
            new()
            {
                Path = "Assets/Prefabs/Weapon/Bus_Boong.prefab", Buff = Buff.BusBoong,
                Inhale = new Vector3(-0.283f, 0f, -0.485f), InhaleOn = "Boong", InhaleRadius = 0.063f,
                Bowl = new Vector3(0.172f, 0f, -0.393f), BowlFaces = new Vector3(0f, 0f, -1f),
                BurnOn = "Boong", BurnRadius = 0.045f,
                Shake = "Boong", Swell = "Boong", BreakApart = true,
            },
        };

        [MenuItem("Tools/429 Game/Weapons/Set Up Boong Smoking")]
        public static void SetUp()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[Boong Smoke] Close the prefab you're editing (Prefab Mode) first, then run it again.");
                return;
            }

            var effects = new List<GameObject>();
            foreach (var path in GrenadeSetupMenu.Effects)
            {
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (effect != null) effects.Add(effect);
                else Debug.LogWarning($"[Boong Smoke] The explosion effect '{path}' is missing; left out.");
            }
            var assets = new Shared
            {
                Effects = effects,
                Sound = Load<AudioClip>(GrenadeSetupMenu.Sound),
                Smoke = Load<GameObject>(SmokePrefab),
                Bubbles = Load<AudioClip>(BubbleSound),
                Ignite = Load<AudioClip>(IgniteSound),
                Flame = Load<GameObject>(FlamePrefab),
            };

            var done = new List<string>();
            foreach (var boong in Boongs)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(boong.Path);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(boong.Path) == null)
                {
                    done.Add(name + " (missing, skipped)");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(boong.Path);
                try
                {
                    done.Add(name + ": " + SetUpOne(root, boong, assets));
                    PrefabUtility.SaveAsPrefabAsset(root, boong.Path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            int preloaded = GrenadeSetupMenu.PreloadSounds(BubbleSound, IgniteSound, GrenadeSetupMenu.Sound);
            Debug.Log("[Boong Smoke] " + string.Join("; ", done) + "." +
                      (preloaded > 0 ? $" {preloaded} sound(s) now load with the scene." : "") +
                      " Points, reaches, tilt, fire time and blast are on each boong's Boong Smoke (the Scene view shows the" +
                      " points: cyan mouthpiece, orange bowl).");
        }

        [MenuItem("Tools/429 Game/Weapons/Set Up Boong Smoking", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private sealed class Shared
        {
            public List<GameObject> Effects;
            public AudioClip Sound;
            public GameObject Smoke;
            public AudioClip Bubbles;
            public AudioClip Ignite;
            public GameObject Flame;
        }

        private static string SetUpOne(GameObject root, Boong boong, Shared assets)
        {
            var notes = new List<string>();
            var smoke = root.GetComponent<BoongSmoke>();
            bool isNew = smoke == null;
            if (isNew) smoke = root.AddComponent<BoongSmoke>();
            notes.Add(isNew ? "added" : "already there (numbers kept)");

            var inhale = Point(root, InhaleName, boong.Inhale, boong.InhaleOn, out bool madeInhale);
            var bowl = boong.Bowl + boong.BowlFaces.normalized * AboveBowl;
            var burn = Point(root, BurnName, bowl, boong.BurnOn, out bool madeBurn);
            if (madeInhale || madeBurn) notes.Add("made " + (madeInhale && madeBurn ? "both points" : madeInhale ? "Inhale" : "Burn"));

            var animator = FireAnimator(root);
            var flame = Flame(root, burn, boong.Up, assets.Flame, out bool madeFlame);
            if (madeFlame) notes.Add("made " + NewFlameName);

            var so = new SerializedObject(smoke);
            // Always: the parts it works with (found again each time).
            so.FindProperty("inhale").objectReferenceValue = inhale;
            so.FindProperty("burn").objectReferenceValue = burn;
            SetIfEmpty(so, "animator", animator);
            SetIfEmpty(so, "flame", flame);
            SetIfEmpty(so, "shakePart", Find(root.transform, boong.Shake));
            SetIfEmpty(so, "swellPart", Find(root.transform, boong.Swell));
            SetIfEmpty(so, "smoke", assets.Smoke);
            SetIfEmpty(so, "igniteSound", assets.Ignite);
            SetIfEmpty(so, "bubbleSound", assets.Bubbles);
            SetIfEmpty(so, "sound", assets.Sound);
            var list = so.FindProperty("effects");
            if (list.arraySize == 0)
            {
                list.arraySize = assets.Effects.Count;
                for (int i = 0; i < assets.Effects.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = assets.Effects[i];
            }

            // Always: which way is up (the bowl facing up, so the fixed hammer grips work) and how far it may lean.
            var upAxis = so.FindProperty("upAxis");
            var maxTilt = so.FindProperty("maxTilt");
            if (upAxis.vector3Value != boong.Up || !Mathf.Approximately(maxTilt.floatValue, MaxTilt))
            {
                upAxis.vector3Value = boong.Up;
                maxTilt.floatValue = MaxTilt;
                if (!isNew) notes.Add("bowl-up axis set");
            }

            // Only the first time: the numbers (tuned by hand afterwards).
            if (isNew)
            {
                so.FindProperty("buff").enumValueIndex = (int)boong.Buff;
                so.FindProperty("inhaleRadius").floatValue = boong.InhaleRadius;
                so.FindProperty("burnRadius").floatValue = boong.BurnRadius;
                so.FindProperty("breakApart").boolValue = boong.BreakApart;
                so.FindProperty("fireState").stringValue = FireState;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            notes.Add(animator != null ? "fire animation on " + animator.name : "no fire animation (swells instead)");
            return string.Join(", ", notes);
        }

        // The point named 'name' anywhere on the boong (kept where it is), or a new one at 'at' (root-local) on the bone
        // 'on' (a path from the root; the root itself when it isn't there). Its size is the boong's own, so its reach
        // grows with the bone it's on.
        private static Transform Point(GameObject root, string name, Vector3 at, string on, out bool made)
        {
            var found = FindByName(root.transform, name);
            made = found == null;
            if (found != null) return found;

            var point = new GameObject(name).transform;
            point.SetParent(root.transform, false);
            point.localPosition = at;
            point.localRotation = Quaternion.identity;
            point.localScale = Vector3.one;
            var bone = Find(root.transform, on);
            if (bone != null) point.SetParent(bone, true);
            return point;
        }

        // The Animator that has the fire state (the hammer boongs: on the root; the bone boong: on its model).
        private static Animator FireAnimator(GameObject root)
        {
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController is not AnimatorController controller) continue;
                foreach (var layer in controller.layers)
                {
                    foreach (var state in layer.stateMachine.states)
                    {
                        if (state.state.name == FireState) return animator;
                    }
                    // Only the first layer is played by Boong Smoke.
                    break;
                }
            }
            return null;
        }

        // The boong's own flame (CandleFlame), or on the hammer boongs a small copy of the candle flame (its particles
        // only) in the bowl, pointing out of it, off until it burns.
        private static GameObject Flame(GameObject root, Transform burn, Vector3 up, GameObject flamePrefab, out bool made)
        {
            made = false;
            foreach (Transform child in root.transform)
            {
                if (child.name == FlameName) return child.gameObject;
            }
            var existing = FindByName(root.transform, NewFlameName);
            if (existing != null) return existing.gameObject;
            if (flamePrefab == null || burn == null) return null;

            var flame = (GameObject)Object.Instantiate(flamePrefab, burn, false);
            flame.name = NewFlameName;
            // No light: one more realtime light is too much on Quest for a little flame.
            foreach (var light in flame.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light.gameObject);
            flame.transform.localPosition = Vector3.zero;
            flame.transform.rotation = root.transform.rotation * Quaternion.FromToRotation(Vector3.forward, up);
            flame.transform.localScale = Vector3.one * NewFlameScale;
            foreach (var particles in flame.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                // Grows with the bowl (the head grows 3x as it burns).
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            flame.SetActive(false);
            made = true;
            return flame;
        }

        private static void SetIfEmpty(SerializedObject so, string field, Object value)
        {
            var property = so.FindProperty(field);
            if (property != null && property.objectReferenceValue == null && value != null) property.objectReferenceValue = value;
        }

        // A child by its path from the root; failing that, anything below with the path's last name.
        private static Transform Find(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var found = root.Find(path);
            if (found != null) return found;
            int slash = path.LastIndexOf('/');
            return FindByName(root, slash >= 0 ? path.Substring(slash + 1) : path);
        }

        private static Transform FindByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name == name) return t;
            }
            return null;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogWarning($"[Boong Smoke] '{path}' is missing; left out.");
            return asset;
        }
    }
}
