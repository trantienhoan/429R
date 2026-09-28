using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Menu command that adds the enemy limit to the scene (Tools > 429 Game > Enemies).</summary>
    public static class EnemyLimitSetupMenu
    {
        [MenuItem("Tools/429 Game/Enemies/Set Up Enemy Limit")]
        public static void SetUpEnemyLimit()
        {
            var limit = Object.FindAnyObjectByType<EnemyLimit>(FindObjectsInactive.Include);
            bool created = limit == null;
            if (created)
            {
                var go = new GameObject("Enemy Limit");
                Undo.RegisterCreatedObjectUndo(go, "Create Enemy Limit");
                limit = go.AddComponent<EnemyLimit>();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Selection.activeGameObject = limit.gameObject;
            Debug.Log(created
                ? "[Enemy Limit] Created 'Enemy Limit' in the scene. Set the numbers in the Inspector, then save the scene."
                : "[Enemy Limit] The scene already has one; it's selected.", limit);
        }
    }
}
