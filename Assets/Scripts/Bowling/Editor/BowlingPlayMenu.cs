using HutongGames.PlayMaker;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Play Bowling (Play mode) (gate 2 test, 2026-11-23, like Ball Valley's): the arena as
    /// GAMESTAGES' Path 2 ON makes it (a fresh Currently Spawned Stuffs, Bowling_Arena at (0, 0, 0)) and the player in its
    /// middle facing lane 0. GAMESTAGES isn't moved: after the giant pumpkin the seed is out, but no rooftop comes (Gate
    /// Exit waits, then gives the view back). For the whole gate (rooftop, Whack 6) jump GAMESTAGES to "Clean for Path 2".
    /// The arena's test buttons: right-click its Bowling Stage (Eat The Seed, Strike The First Rack, Hit Slime 0, Next
    /// Phase, Showdown Now, Win Now).
    /// </summary>
    public static class BowlingPlayMenu
    {
        private const string Log = "[Bowling]";
        private const string MenuPath = "Tools/429 Game/Bowling/Play Bowling (Play mode)";
        private const string ArenaPath = "Assets/Prefabs/Bowling/Bowling_Arena.prefab";
        private const float StartYaw = 45f;   // lane 0, as Path 2 ON

        [MenuItem(MenuPath)]
        public static void Play()
        {
            if (!EditorApplication.isPlaying) { Debug.LogError($"{Log} Play Bowling works in Play mode: press Play first."); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath);
            if (prefab == null || prefab.GetComponent<BowlingStage>() == null)
            {
                Debug.LogError($"{Log} No {ArenaPath} with a Bowling Stage: run Tools > 429 Game > Bowling > Set Up Bowling Arena first (out of Play mode).");
                return;
            }
            if (BowlingStage.Running != null) Object.Destroy(BowlingStage.Running.gameObject);
            var global = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            if (global != null && global.Value != null) Object.Destroy(global.Value);
            var manager = GameObject.Find("-LevelManger");
            var holder = new GameObject("CurrentlySpawnedStuffs");
            if (manager != null) holder.transform.SetParent(manager.transform);
            holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (global != null) global.Value = holder;
            int frame = Time.frameCount;
            EditorApplication.CallbackFunction spawn = null;
            spawn = () =>
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= spawn; return; }
                if (Time.frameCount <= frame + 1) return;
                EditorApplication.update -= spawn;
                if (holder == null) return;
                PutPlayerInTheMiddle();
                var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, holder.transform);
                go.name = prefab.name;
                Debug.Log($"{Log} Playing gate 2: Bowling_Arena at (0, 0, 0), the player in its middle. Stop Play mode when done.", go);
            };
            EditorApplication.update += spawn;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanPlay() => EditorApplication.isPlaying;

        // The head over the arena's middle, the feet on the dirt, facing lane 0.
        private static void PutPlayerInTheMiddle()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null) return;
            var head = origin.Camera.transform;
            var body = origin.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            var f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            float yaw = f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f).eulerAngles.y : 0f;
            origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(yaw, StartYaw));
            var feet = new Vector3(head.position.x, origin.transform.position.y, head.position.z);
            var to = origin.transform.position - new Vector3(feet.x, 0f, feet.z);
            to.y = BowlingLayout.DirtTop;
            origin.transform.position = to;
            Physics.SyncTransforms();
            if (body != null) body.enabled = true;
        }
    }
}
