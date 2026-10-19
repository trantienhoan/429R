using System.Collections.Generic;
using Game.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Game.EditorTools
{
    /// <summary>Menu commands for how weapons are held: fixed grips and two-handed weapons (Tools > 429 Game > Weapons).</summary>
    public static class WeaponGripSetupMenu
    {
        // Held by their grip (Attach) every time, not wherever the hand happens to grab them.
        private static readonly string[] FixedGripHammers =
        {
            "Assets/Prefabs/Weapon/Hammer_Boong.prefab",
            "Assets/Prefabs/Weapon/Hammer_Boong 2ND.prefab",
            "Assets/Prefabs/Weapon/Hammer_Tool.prefab",
            PillowPath,
        };

        private static readonly string[] TwoHandedWeapons =
        {
            "Assets/Prefabs/Weapon/Hammer_Broom.prefab",
            PillowPath,
            "Assets/Prefabs/Items/Pie_Knight_Sword_Legendary.prefab",
        };

        private const string PillowPath = "Assets/Prefabs/Weapon/Hammer_Pillow.prefab";
        private const string AttachName = "Attach";
        // The pillow has no handle: it's held by the middle of one short end (it's 0.79 m long along x, 0.32 thick along
        // y, 0.51 wide along z), 5 cm in from the edge. Turned like the other hammers' grips (Hammer_Tool, Hammer_Boong):
        // the pillow reaches up and forward from the hand at 45 degrees, its broad side facing forward-down, so an
        // overhead swing lands flat, and its width goes across the hand.
        private static readonly Vector3 PillowGripPosition = new(-0.35f, 0.01f, 0.02f);
        private static readonly Quaternion PillowGripRotation = Quaternion.LookRotation(new Vector3(1f, 1f, 0f).normalized, new Vector3(1f, -1f, 0f).normalized);

        /// <summary>
        /// The hammers (Hammer_Boong, Hammer_Boong 2ND, Hammer_Tool, Hammer_Pillow) are held by their grip every time
        /// instead of wherever the hand grabs them (Use Dynamic Attach off). The pillow, which had no grip, gets one at
        /// one end. Then the broom, the pillow and the legendary knight sword can be held in two hands (Two Handed Grip):
        /// harder hits, faster wear. Running it again only fixes what isn't set up; a grip moved by hand stays.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Set Up Weapon Grips")]
        public static void SetUpWeaponGrips()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[Weapon Grips] Close the prefab you're editing (Prefab Mode) first, then run it again.");
                return;
            }

            var notes = new List<string>();
            foreach (var path in FixedGripHammers) EditPrefab(path, notes, root => FixGrip(root, path == PillowPath, notes));
            foreach (var path in TwoHandedWeapons) EditPrefab(path, notes, root => MakeTwoHanded(root, false, notes));
            Debug.Log("[Weapon Grips] " + string.Join("; ", notes) + ".");
        }

        [MenuItem("Tools/429 Game/Weapons/Set Up Weapon Grips", true)]
        private static bool CanSetUpWeaponGrips() => !EditorApplication.isPlaying;

        /// <summary>
        /// Makes the selected weapons holdable in two hands (Two Handed Grip, XR Grab Interactable's Select Mode Multiple,
        /// an XR General Grab Transformer that turns with two hands but doesn't scale). Works on prefabs selected in the
        /// Project window and on objects in the scene. Running it again changes nothing; tuned values stay.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Make Selected Two-Handed")]
        public static void MakeSelectedTwoHanded()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[Weapon Grips] Close the prefab you're editing (Prefab Mode) first, then run it again.");
                return;
            }

            var notes = new List<string>();
            var prefabsDone = new HashSet<string>();
            Undo.SetCurrentGroupName("Make Selected Two-Handed");
            foreach (var go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    var path = AssetDatabase.GetAssetPath(go);
                    if (string.IsNullOrEmpty(path) || !prefabsDone.Add(path)) continue;
                    EditPrefab(path, notes, root => MakeTwoHanded(root, false, notes));
                    continue;
                }

                var grab = go.GetComponentInParent<XRGrabInteractable>(true);
                if (grab == null)
                {
                    notes.Add($"{go.name}: nothing to grab on it, skipped");
                    continue;
                }
                if (MakeTwoHanded(grab.gameObject, true, notes)) EditorSceneManager.MarkSceneDirty(grab.gameObject.scene);
            }
            if (notes.Count == 0) notes.Add("select weapon prefabs (Project window) or weapons in the scene first");
            Debug.Log("[Weapon Grips] " + string.Join("; ", notes) + ".");
        }

        [MenuItem("Tools/429 Game/Weapons/Make Selected Two-Handed", true)]
        private static bool CanMakeSelectedTwoHanded() => !EditorApplication.isPlaying && Selection.gameObjects.Length > 0;

        // Opens the prefab, lets 'change' change it, and saves it only if something changed.
        private static void EditPrefab(string path, List<string> notes, System.Func<GameObject, bool> change)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add($"'{path}' is missing");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (change(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Use Dynamic Attach off, so the hand always holds it by its Attach. 'Make Grip': give it an Attach at the
        // pillow's grip if it has none (one made before, maybe moved by hand since, is used as it is).
        private static bool FixGrip(GameObject root, bool makeGrip, List<string> notes)
        {
            var grab = root.GetComponent<XRGrabInteractable>();
            if (grab == null)
            {
                notes.Add($"{root.name}: no XR Grab Interactable, skipped");
                return false;
            }

            bool changed = false;
            if (grab.attachTransform == null && makeGrip)
            {
                var attach = root.transform.Find(AttachName);
                if (attach == null)
                {
                    attach = new GameObject(AttachName).transform;
                    attach.SetParent(root.transform, false);
                    attach.SetLocalPositionAndRotation(PillowGripPosition, PillowGripRotation);
                    notes.Add($"{root.name}: made its grip (Attach) at one end");
                }
                grab.attachTransform = attach;
                changed = true;
            }
            if (grab.attachTransform == null) notes.Add($"WARNING {root.name} has no Attach: it's held by its middle");

            if (grab.useDynamicAttach)
            {
                grab.useDynamicAttach = false;
                changed = true;
                notes.Add($"{root.name}: held by its grip");
            }
            else if (!changed) notes.Add($"{root.name}: grip already fixed");
            return changed;
        }

        // Select Mode Multiple, an XR General Grab Transformer (turns with two hands, no two-handed scaling) and a Two
        // Handed Grip. Values already there are kept, except two-handed scaling, which is always off.
        private static bool MakeTwoHanded(GameObject root, bool undo, List<string> notes)
        {
            var grab = root.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = root.GetComponentInChildren<XRGrabInteractable>(true);
            if (grab == null)
            {
                notes.Add($"{root.name}: nothing to grab on it, skipped");
                return false;
            }
            var go = grab.gameObject;
            bool changed = false;

            if (!go.TryGetComponent(out XRGeneralGrabTransformer transformer))
            {
                transformer = undo ? Undo.AddComponent<XRGeneralGrabTransformer>(go) : go.AddComponent<XRGeneralGrabTransformer>();
                var fresh = new SerializedObject(transformer);
                fresh.FindProperty("m_TwoHandedRotationMode").intValue = (int)XRGeneralGrabTransformer.TwoHandedRotationMode.FirstHandDirectedTowardsSecondHand;
                Apply(fresh, undo);
                changed = true;
            }
            var transformerSettings = new SerializedObject(transformer);
            var scaling = transformerSettings.FindProperty("m_AllowTwoHandedScaling");
            if (scaling.boolValue)
            {
                scaling.boolValue = false;
                Apply(transformerSettings, undo);
                changed = true;
            }

            var grabSettings = new SerializedObject(grab);
            var mode = grabSettings.FindProperty("m_SelectMode");
            if (mode.intValue != (int)InteractableSelectMode.Multiple)
            {
                mode.intValue = (int)InteractableSelectMode.Multiple;
                changed = true;
            }
            // Grab transformers listed by hand: the two-handed list must have this one too (otherwise XRI finds it itself).
            var single = grabSettings.FindProperty("m_StartingSingleGrabTransformers");
            var multiple = grabSettings.FindProperty("m_StartingMultipleGrabTransformers");
            if ((single.arraySize > 0 || multiple.arraySize > 0) && !Contains(multiple, transformer))
            {
                multiple.arraySize++;
                multiple.GetArrayElementAtIndex(multiple.arraySize - 1).objectReferenceValue = transformer;
                changed = true;
            }
            Apply(grabSettings, undo);

            if (!go.TryGetComponent<TwoHandedGrip>(out _))
            {
                if (undo) Undo.AddComponent<TwoHandedGrip>(go);
                else go.AddComponent<TwoHandedGrip>();
                changed = true;
            }

            string grip = grab.attachTransform == null ? " (no Attach: the first hand holds it where it grabs)"
                : grab.useDynamicAttach ? " (Use Dynamic Attach is on: the first hand holds it where it grabs)" : "";
            notes.Add(changed ? $"{go.name}: two-handed{grip}" : $"{go.name}: already two-handed{grip}");
            return changed;
        }

        private static bool Contains(SerializedProperty array, Object value)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) return true;
            }
            return false;
        }

        private static void Apply(SerializedObject settings, bool undo)
        {
            if (undo) settings.ApplyModifiedProperties();
            else settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
