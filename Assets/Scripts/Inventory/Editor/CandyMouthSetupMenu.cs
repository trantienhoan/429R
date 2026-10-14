using System.Linq;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Inventory.Editor
{
    /// <summary>
    /// Tools > 429 Game > Player > Eat Candy Only At The Mouth.
    /// Candies used to be eaten by touching the player's body trigger (tagged Player) with a 0.6 m trigger bubble of
    /// their own, so holding one near the chest ate it. Now:
    /// - a small trigger ball "Mouth" (tag Mouth, its own kinematic Rigidbody) sits under the Main Camera, a little
    ///   below and in front of the eyes;
    /// - each Candy_1..8 eats on touching the Mouth instead of the Player (its Damage FSM "Wait for Collision"), and its
    ///   trigger bubble shrinks from 0.6 to Candy Trigger Radius.
    /// Safe to run again.
    /// </summary>
    public static class CandyMouthSetupMenu
    {
        private const string MouthTag = "Mouth";
        private const float MouthRadius = 0.09f;
        private static readonly Vector3 MouthOffset = new(0f, -0.09f, 0.07f);
        private const float CandyTriggerRadius = 0.2f;
        private const string CandyFolder = "Assets/Prefabs/Items/Breakable/";

        [MenuItem("Tools/429 Game/Player/Eat Candy Only At The Mouth")]
        public static void Run()
        {
            if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(MouthTag))
                UnityEditorInternal.InternalEditorUtility.AddTag(MouthTag);

            string mouth = AddMouth();
            int candies = 0;
            for (int i = 1; i <= 8; i++)
            {
                if (FixCandy(CandyFolder + "Candy_" + i + ".prefab")) candies++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Candy] {mouth}; {candies} candies now eaten only at the mouth.");
        }

        private static string AddMouth()
        {
            var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (cam == null) return "no Main Camera in the open scene (Mouth not added)";

            var found = cam.transform.Find("Mouth");
            var mouth = found != null ? found.gameObject : new GameObject("Mouth");
            if (found == null) Undo.RegisterCreatedObjectUndo(mouth, "Add Mouth");
            mouth.transform.SetParent(cam.transform, false);
            mouth.transform.localPosition = MouthOffset;
            mouth.transform.localRotation = Quaternion.identity;
            mouth.transform.localScale = Vector3.one;
            mouth.tag = MouthTag;
            mouth.layer = 0;

            if (!mouth.TryGetComponent<SphereCollider>(out var ball)) ball = mouth.AddComponent<SphereCollider>();
            ball.isTrigger = true;
            ball.radius = MouthRadius;
            ball.center = Vector3.zero;

            // Its own kinematic body, so it isn't part of the XR Origin's body (no extra trigger calls on the rig).
            if (!mouth.TryGetComponent<Rigidbody>(out var body)) body = mouth.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            EditorUtility.SetDirty(mouth);
            EditorSceneManager.MarkSceneDirty(mouth.scene);
            return "Mouth added under " + cam.name + " (save the scene)";
        }

        private static bool FixCandy(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return false;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "Damage") continue;
                    foreach (var state in fsm.Fsm.States)
                    {
                        bool stateChanged = false;
                        foreach (var action in state.Actions)
                        {
                            if (action is TriggerEvent trigger && trigger.collideTag.Value == "Player")
                            {
                                trigger.collideTag.Value = MouthTag;
                                stateChanged = true;
                            }
                        }
                        if (stateChanged)
                        {
                            state.SaveActions();
                            changed = true;
                        }
                    }
                    if (changed) EditorUtility.SetDirty(fsm);
                }
                foreach (var ball in root.GetComponents<SphereCollider>())
                {
                    if (!ball.isTrigger || ball.radius <= CandyTriggerRadius) continue;
                    ball.radius = CandyTriggerRadius;
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
