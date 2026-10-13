using System.Collections.Generic;
using Game.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.EditorTools
{
    /// <summary>Menu command that turns weapons into grenades (Tools > 429 Game > Weapons).</summary>
    public static class GrenadeSetupMenu
    {
        private static readonly string[] FlipFlops =
        {
            "Assets/Prefabs/Weapon/Flip_Flop_L.prefab",
            "Assets/Prefabs/Weapon/Flip_Flop_R.prefab",
            "Assets/Prefabs/Weapon/Flip_Flop2_L.prefab",
            "Assets/Prefabs/Weapon/Flip_Flop2_R.prefab",
        };

        // A flash, a burst and a puff of smoke from the Hyper Casual FX pack read as one explosion about 3 m across.
        internal static readonly string[] Effects =
        {
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab",
        };
        internal const string Sound = "Assets/Audio/SFX/explosion-312361.wav";
        // A bottle about to go off in the hand warns with the Boong gun's overheat beep; while shaken, it rattles.
        private const string WarningSound = "Assets/Audio/SFX/elon_boong_beep_beep.wav";
        private const string ShakeSound = "Assets/Audio/SFX/poision_bottle_shake.wav";

        // Seconds shaken and the blast's reach then: small enough that a blast doesn't break a whole room at once.
        private static readonly Vector2[] BottleCharge = { new(1f, 0.5f), new(2f, 1f), new(2.5f, 1.25f), new(3f, 1.5f) };

        private static readonly string[] Bottles =
        {
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_1.prefab",
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_2.prefab",
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_3.prefab",
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_4.prefab",
            "Assets/Prefabs/Items/Breakable/Empty_Bottle_5.prefab",
        };

        /// <summary>
        /// Makes the empty bottles shake-to-charge grenades (see Grenade): shaken 1 s they blow 0.5 m wide, 2 s 1 m,
        /// 2.5 s 1.25 m, and at 3 s they go off in the hand (1.5 m). They rattle while shaken and beep a warning just
        /// before going off in the hand. Also has the explosion, rattle and beep sounds loaded with the scene, so they
        /// don't stutter the first time. Bottles keep their other grenade settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Make Bottles Shake To Charge")]
        public static void MakeBottlesShakeToCharge()
        {
            var beep = AssetDatabase.LoadAssetAtPath<AudioClip>(WarningSound);
            var rattle = AssetDatabase.LoadAssetAtPath<AudioClip>(ShakeSound);
            var made = new List<string>();
            foreach (var path in Bottles)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[Grenade] '{path}' doesn't exist; skipped.");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var grenade = root.GetComponent<Grenade>();
                    if (grenade == null)
                    {
                        Debug.LogWarning($"[Grenade] '{path}' isn't a grenade yet (Make Flip-Flops Explode with it selected first); skipped.");
                        continue;
                    }
                    var serialized = new SerializedObject(grenade);
                    serialized.FindProperty("shakeToCharge").boolValue = true;
                    var steps = serialized.FindProperty("charge");
                    steps.arraySize = BottleCharge.Length;
                    for (int i = 0; i < BottleCharge.Length; i++) steps.GetArrayElementAtIndex(i).vector2Value = BottleCharge[i];
                    if (serialized.FindProperty("warningSound").objectReferenceValue == null)
                        serialized.FindProperty("warningSound").objectReferenceValue = beep;
                    if (serialized.FindProperty("shakeSound").objectReferenceValue == null)
                        serialized.FindProperty("shakeSound").objectReferenceValue = rattle;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    made.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            int preloaded = PreloadSounds(Sound, WarningSound, ShakeSound);
            Debug.Log($"[Grenade] Shake to charge on: {string.Join(", ", made)}." +
                      (preloaded > 0 ? $" {preloaded} explosion sound(s) now load with the scene." : ""));
        }

        /// <summary>
        /// Has these sounds loaded with the scene (Preload Audio Data), so the first time one plays the game doesn't
        /// stop to load it. Returns how many changed.
        /// </summary>
        internal static int PreloadSounds(params string[] paths)
        {
            int changed = 0;
            foreach (var path in paths)
            {
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                var settings = importer.defaultSampleSettings;
                if (settings.preloadAudioData) continue;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                changed++;
            }
            return changed;
        }

        /// <summary>
        /// Adds Grenade (with the explosion effects and sound) and Hit Rumble to the selected weapon prefabs, or with
        /// none selected, to the four flip-flops. Prefabs that are already grenades keep their settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Make Flip-Flops Explode")]
        public static void MakeExplosive()
        {
            var paths = HitRumbleSetupMenu.SelectedPrefabPaths();
            bool fromSelection = paths.Count > 0;
            if (!fromSelection) paths.AddRange(FlipFlops);

            var effects = new List<GameObject>();
            foreach (var path in Effects)
            {
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (effect != null) effects.Add(effect);
                else Debug.LogWarning($"[Grenade] The explosion effect '{path}' is missing; left out.");
            }
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(Sound);
            if (sound == null) Debug.LogWarning($"[Grenade] The explosion sound '{Sound}' is missing; grenades will be silent.");

            var made = new List<string>();
            int skipped = 0;
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[Grenade] '{path}' doesn't exist; skipped.");
                    skipped++;
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Only things you can throw; weapons that already are grenades keep their settings.
                    if (root.GetComponent<XRGrabInteractable>() == null || root.GetComponent<Grenade>() != null)
                    {
                        skipped++;
                        continue;
                    }

                    var serialized = new SerializedObject(root.AddComponent<Grenade>());
                    var list = serialized.FindProperty("effects");
                    list.arraySize = effects.Count;
                    for (int i = 0; i < effects.Count; i++)
                        list.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
                    serialized.FindProperty("sound").objectReferenceValue = sound;
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    // Held, it buzzes your hand when it hits things too.
                    if (root.GetComponent<HitRumble>() == null) root.AddComponent<HitRumble>();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    made.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            string where = fromSelection ? "the selected prefabs" : "the flip-flops";
            Debug.Log(made.Count > 0
                ? $"[Grenade] {made.Count} weapon(s) now explode when thrown: {string.Join(", ", made)}. {skipped} skipped (not grabbable, or already grenades)."
                : $"[Grenade] Nothing to change in {where}: {skipped} prefab(s) are not grabbable or already grenades.");
        }
    }
}
