using System.Collections.Generic;
using Game.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.EditorTools
{
    /// <summary>Menu command that makes guns overheat when spammed (Tools > 429 Game > Weapons).</summary>
    public static class OverheatSetupMenu
    {
        private const string BoongGun = "Assets/Prefabs/Weapon/Boong_Gun.prefab";
        private const string WarningSound = "Assets/Audio/SFX/elon_boong_beep_beep.wav";
        private const string Smoke = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        // The Boong gun's model; with no such child, nothing shakes.
        private const string ShakePart = "Boong_Bone_Gun_Trigger";

        /// <summary>
        /// Adds Overheat (with the grenade's explosion effects and sound, a beep and a smoke puff) to the selected gun
        /// prefabs, or with none selected, to Boong_Gun. Guns that already overheat keep their settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Make Boong Gun Overheat")]
        public static void MakeOverheat()
        {
            var paths = HitRumbleSetupMenu.SelectedPrefabPaths();
            if (paths.Count == 0) paths.Add(BoongGun);

            var effects = new List<Object>();
            foreach (var path in GrenadeSetupMenu.Effects)
            {
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (effect != null) effects.Add(effect);
            }

            var made = new List<string>();
            var skipped = new List<string>();
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    skipped.Add($"'{path}' (missing)");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Only things you hold and fire; guns that already overheat keep their settings.
                    if (root.GetComponent<XRGrabInteractable>() == null || root.GetComponent<Overheat>() != null)
                    {
                        skipped.Add(root.name);
                        continue;
                    }

                    var serialized = new SerializedObject(root.AddComponent<Overheat>());
                    var list = serialized.FindProperty("effects");
                    list.arraySize = effects.Count;
                    for (int i = 0; i < effects.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
                    serialized.FindProperty("sound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(GrenadeSetupMenu.Sound);
                    serialized.FindProperty("warningSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(WarningSound);
                    serialized.FindProperty("smoke").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Smoke);
                    serialized.FindProperty("shakePart").objectReferenceValue = FindChild(root.transform, ShakePart);
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    made.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"[Overheat] Overheats now: {(made.Count > 0 ? string.Join(", ", made) : "nothing new")}" +
                      (skipped.Count > 0 ? $". Skipped (already overheats, or can't be held): {string.Join(", ", skipped)}" : "") +
                      ". Shots, seconds, fuse and blast are on its Overheat component.");
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName) return t;
            }
            return null;
        }
    }
}
