using System.Collections.Generic;
using Game.Enemies;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Add Enemy Jiggle (user, 2026-10-29: "can we add jiggle effect to enemy?").
    /// Bugs: gives the spiders, cockroaches and bees (the Enemies_Smarter base prefabs; their Small, Normal, Boss and
    /// other variants get it from them) the pumpkins' jelly jiggle when they're hurt. The jiggle can't go on the model
    /// itself (Spider Brain lifts, turns and swells it), so an empty "Jiggle" pivot goes between the root and the model,
    /// with Jiggle on it: it watches the bug's health FSM and squashes around the model's origin (Around), so a flying
    /// bee jiggles in the air.
    /// The pumpkin boss ("can the Pumpkin_Boss_Z only jiggle where it get punched? arm, leg, body, head, butt"): Part
    /// Jiggle on its root, one part per pumpkin of its body (its brain's Body Pumpkins: their bones and middles), and
    /// hit spheres on its arms and shins too (Set Up Pumpkin Boss's list), so a punch there lands. A whole-body pivot
    /// from before is taken out again.
    /// Running it again only sets the numbers again.
    /// </summary>
    public static class EnemyJiggleMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Enemies_Smarter";
        private const string BossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        private const string PivotName = "Jiggle";

        // A little lighter and quicker than a pumpkin's (bugs are hit often and knocked back too).
        private const float Squash = 0.25f;
        private const float Tilt = 12f;
        private const float WobblesPerSecond = 5f;
        private const float SettleTime = 0.7f;

        [MenuItem("Tools/429 Game/Enemies/Add Enemy Jiggle")]
        public static void Run()
        {
            var log = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<SpiderBrain>() == null) continue;
                if (PrefabUtility.IsPartOfVariantPrefab(asset)) continue;
                log.Add(Open(path) ?? AddTo(path));
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPath) != null) log.Add(Open(BossPath) ?? AddPartsTo(BossPath));
            AssetDatabase.SaveAssets();
            Debug.Log("[Enemy Jiggle] " + string.Join("; ", log));
        }

        // A note if the prefab is open in Prefab Mode (it's skipped then), otherwise null.
        private static string Open(string path)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.assetPath == path
                ? System.IO.Path.GetFileNameWithoutExtension(path) + " is open in Prefab Mode: skipped (close it and run again)"
                : null;
        }

        private static string AddTo(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var brain = new SerializedObject(root.GetComponent<SpiderBrain>());
                var models = brain.FindProperty("models").objectReferenceValue as Transform;
                if (models == null) return root.name + ": no Models on its Spider Brain, skipped";

                string note = "pivot already there";
                var pivot = models.parent;
                if (pivot == root.transform)
                {
                    pivot = new GameObject(PivotName).transform;
                    pivot.SetParent(root.transform, false);
                    pivot.SetSiblingIndex(models.GetSiblingIndex());
                    models.SetParent(pivot, false);
                    note = "pivot added";
                }

                string healthFsm = "";
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName.EndsWith("Health")) healthFsm = fsm.FsmName;
                }

                if (!pivot.TryGetComponent(out Jiggle jiggle)) jiggle = pivot.gameObject.AddComponent<Jiggle>();
                var settings = new SerializedObject(jiggle);
                settings.FindProperty("around").objectReferenceValue = models;
                settings.FindProperty("healthFsm").stringValue = healthFsm;
                settings.FindProperty("squash").floatValue = Squash;
                settings.FindProperty("tilt").floatValue = Tilt;
                settings.FindProperty("wobblesPerSecond").floatValue = WobblesPerSecond;
                settings.FindProperty("settleTime").floatValue = SettleTime;
                settings.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return root.name + ": " + note + ", jiggles on " + healthFsm;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AddPartsTo(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var brain = new SerializedObject(root.GetComponent<PumpkinBossBrain>());
                var model = brain.FindProperty("model").objectReferenceValue as Transform;
                if (model == null) return root.name + ": no Model on its brain, skipped";

                var notes = new List<string>();
                var pivot = model.parent;
                if (pivot != root.transform && pivot.name == PivotName && pivot.parent == root.transform)
                {
                    // The pivot rests at no offset, so the model keeps its own place.
                    model.SetParent(root.transform, false);
                    model.SetSiblingIndex(pivot.GetSiblingIndex());
                    Object.DestroyImmediate(pivot.gameObject);
                    notes.Add("whole-body jiggle taken off");
                }
                int spheres = PumpkinBossSetupMenu.AddHitSpheres(model);
                if (spheres > 0) notes.Add(spheres + " hit spheres added");

                if (!root.TryGetComponent(out PartJiggle jiggle)) jiggle = root.AddComponent<PartJiggle>();
                var pumpkins = brain.FindProperty("bodyPumpkins");
                var settings = new SerializedObject(jiggle);
                var parts = settings.FindProperty("parts");
                parts.arraySize = pumpkins.arraySize;
                for (int i = 0; i < pumpkins.arraySize; i++)
                {
                    var pumpkin = pumpkins.GetArrayElementAtIndex(i);
                    var part = parts.GetArrayElementAtIndex(i);
                    part.FindPropertyRelative("bone").stringValue = pumpkin.FindPropertyRelative("bone").stringValue;
                    part.FindPropertyRelative("centre").vector3Value = pumpkin.FindPropertyRelative("offset").vector3Value;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.Insert(0, "jiggles where it's hit (" + parts.arraySize + " parts)");
                return root.name + ": " + string.Join(", ", notes);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
