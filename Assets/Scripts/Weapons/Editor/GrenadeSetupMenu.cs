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
        private static readonly string[] Effects =
        {
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab",
        };
        private const string Sound = "Assets/Audio/SFX/explosion-312361.wav";

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
