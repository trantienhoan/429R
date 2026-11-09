using Game.PlayGround;
using UnityEditor;
using UnityEngine;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Set Up Bowling Ball: makes Prefabs/Bowling/Bowling_Ball from pumpkin_bowling (same
    /// model, body and grab), with its Bowling Roll Assist tuned for the arena: too heavy to throw (a lifted let-go
    /// leaves at 1.2 m/s at most and thuds down), bowled from hip height down (0.9 m), rolling at 5 to 9 m/s, steered
    /// a little toward the lane. pumpkin_bowling itself is left as it is. Safe to run again (it makes the ball anew).
    /// </summary>
    public static class BowlingBallSetupMenu
    {
        private const string SourcePath = "Assets/Prefabs/PlayGround/pumpkin_bowling.prefab";
        private const string Folder = "Assets/Prefabs/Bowling";
        /// <summary>Where the ball is saved.</summary>
        public const string BallPath = Folder + "/Bowling_Ball.prefab";
        private const string BallName = "Bowling_Ball";

        [MenuItem("Tools/429 Game/Bowling/Set Up Bowling Ball")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Bowling] Stop play mode first."); return; }
            Run();
        }

        [MenuItem("Tools/429 Game/Bowling/Set Up Bowling Ball", true)]
        public static bool MenuValid() => !EditorApplication.isPlaying;

        /// <summary>Makes (or remakes) Bowling_Ball. Returns false and logs an error when pumpkin_bowling is missing.</summary>
        public static bool Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath) == null)
            {
                Debug.LogError("[Bowling] No " + SourcePath + " to make the ball from: nothing made.");
                return false;
            }
            MakeFolder(Folder);

            var root = PrefabUtility.LoadPrefabContents(SourcePath);
            try
            {
                root.name = BallName;
                if (!root.TryGetComponent(out BowlingRollAssist roll)) roll = root.AddComponent<BowlingRollAssist>();

                var so = new SerializedObject(roll);
                Set(so, "maxReleaseHeight", 0.9f);
                Set(so, "dropSpeed", 1f);
                Set(so, "minRollSpeed", 5f);
                Set(so, "maxRollSpeed", 9f);
                Set(so, "straighten", 0.35f);
                Set(so, "aimAngle", 25f);
                Set(so, "hook", 0.25f);
                Set(so, "maxHookSpin", 1f);
                Set(so, "heaviestThrow", 1.2f);
                var aim = so.FindProperty("aimAt");
                if (aim != null) aim.objectReferenceValue = null; // the arena's Aim Finder picks the lane
                so.ApplyModifiedPropertiesWithoutUndo();

                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(root, BallPath, out bool ok);
                if (!ok || saved == null)
                {
                    Debug.LogError("[Bowling] Could not save " + BallPath + ".");
                    return false;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(BallPath);
            Debug.Log("[Bowling] Made " + BallPath + " from pumpkin_bowling: too heavy to throw (1.2 m/s), bowls from 0.9 m " +
                      "at 5-9 m/s, steered 35% toward the lane within 25 degrees, hook 0.25 (1 turn/s). pumpkin_bowling unchanged.");
            return true;
        }

        private static void Set(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogError("[Bowling] Bowling Roll Assist has no field " + name + "."); return; }
            p.floatValue = value;
        }

        private static void MakeFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            MakeFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
