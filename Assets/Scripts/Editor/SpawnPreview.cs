using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Shows, in the Scene view only, what each spawn point makes when the game runs: every PlayMaker FSM whose start
    /// state has a Create Object (e.g. the PlayGround's Big_Tree_01_04_Pos, Table_2_Pos, Wood_Barrel2_Pos) draws its
    /// prefab where it will appear, with its real materials and size. Nothing is added to the scene or prefab, the Game
    /// view and builds never see it, and play mode doesn't draw it (the real objects are there then). Works in the scene
    /// and in Prefab Mode. A selected spawn point also shows the prefab's name. Switch it with
    /// Tools > 429 Game > Show Spawn Previews.
    /// </summary>
    [InitializeOnLoad]
    public static class SpawnPreview
    {
        private const string MenuPath = "Tools/429 Game/Show Spawn Previews";
        private const string PrefKey = "429.SpawnPreview.On";
        // Spawn points are looked up again this often (seconds), and when the hierarchy or stage changes.
        private const double RefreshSeconds = 2.0;

        private struct Part
        {
            public Mesh Mesh;
            public Material[] Materials;
            public Matrix4x4 Local;   // relative to the prefab's root (without the root's own scale)
        }

        private struct Spawner
        {
            public PlayMakerFSM Fsm;
            public CreateObject Action;
            public GameObject Prefab;
        }

        private static readonly List<Spawner> spawners = new();
        private static readonly Dictionary<GameObject, Part[]> looks = new();
        private static double nextRefresh;

        private static bool On
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static SpawnPreview()
        {
            SceneView.duringSceneGui += Draw;
            EditorApplication.hierarchyChanged += Forget;
            PrefabStage.prefabStageOpened += _ => Forget();
            PrefabStage.prefabStageClosing += _ => Forget();
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => Forget();
            // A prefab changed (e.g. a prop's model or size): its look is read again.
            EditorApplication.projectChanged += Forget;
            AssemblyReloadEvents.beforeAssemblyReload += () => looks.Clear();
        }

        [MenuItem(MenuPath, priority = 2000)]
        private static void Toggle()
        {
            On = !On;
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleCheck()
        {
            Menu.SetChecked(MenuPath, On);
            return true;
        }

        private static void Forget()
        {
            nextRefresh = 0;
            looks.Clear();
            SceneView.RepaintAll();
        }

        private static void Draw(SceneView view)
        {
            if (!On || EditorApplication.isPlayingOrWillChangePlaymode || Event.current.type != EventType.Repaint) return;
            if (EditorApplication.timeSinceStartup >= nextRefresh) Refresh();

            var camera = view.camera;
            foreach (var spawner in spawners)
            {
                if (spawner.Fsm == null || !spawner.Fsm.isActiveAndEnabled) continue;
                var at = SpawnPose(spawner, out var turn);
                var parts = LookOf(spawner.Prefab);
                // The prefab's own scale is read every time, so a change to it shows at once.
                var place = Matrix4x4.TRS(at, turn, spawner.Prefab.transform.localScale);
                foreach (var part in parts)
                {
                    if (part.Mesh == null) continue;
                    var matrix = place * part.Local;
                    int count = Mathf.Min(part.Mesh.subMeshCount, part.Materials.Length);
                    for (int i = 0; i < count; i++)
                    {
                        if (part.Materials[i] == null) continue;
                        Graphics.DrawMesh(part.Mesh, matrix, part.Materials[i], 0, camera, i, null, false, false);
                    }
                }
                if (Selection.activeGameObject == spawner.Fsm.gameObject)
                    Handles.Label(at + Vector3.up * 0.3f, spawner.Prefab.name, EditorStyles.whiteBoldLabel);
            }
        }

        // Where Create Object puts it: the spawn point (or the FSM's own object), plus the offset; its rotation or the
        // spawn point's.
        private static Vector3 SpawnPose(Spawner spawner, out Quaternion turn)
        {
            var action = spawner.Action;
            var point = action.spawnPoint != null && action.spawnPoint.Value != null ? action.spawnPoint.Value.transform : null;
            Vector3 at;
            Vector3 euler;
            if (point != null || action.spawnPoint == null || action.spawnPoint.UseVariable)
            {
                // A spawn point set at runtime (a variable) is taken to be the FSM's own object.
                if (point == null) point = spawner.Fsm.transform;
                at = point.position + (action.position != null && !action.position.IsNone ? action.position.Value : Vector3.zero);
                euler = action.rotation != null && !action.rotation.IsNone ? action.rotation.Value : point.eulerAngles;
            }
            else
            {
                at = action.position != null && !action.position.IsNone ? action.position.Value : Vector3.zero;
                euler = action.rotation != null && !action.rotation.IsNone ? action.rotation.Value : Vector3.zero;
            }
            turn = Quaternion.Euler(euler);
            return at;
        }

        // Every FSM in the open stage (the scene, or the prefab in Prefab Mode) whose start state makes a prefab.
        private static void Refresh()
        {
            nextRefresh = EditorApplication.timeSinceStartup + RefreshSeconds;
            spawners.Clear();
            foreach (var fsm in StageUtility.GetCurrentStageHandle().FindComponentsOfType<PlayMakerFSM>())
            {
                if (fsm == null || fsm.Fsm == null) continue;
                var start = fsm.Fsm.GetState(fsm.Fsm.StartState);
                if (start == null) continue;
                HutongGames.PlayMaker.FsmStateAction[] actions;
                try { actions = start.Actions; }
                catch { continue; }
                if (actions == null) continue;
                foreach (var a in actions)
                {
                    if (a is not CreateObject make || !make.Enabled || make.gameObject == null) continue;
                    var prefab = make.gameObject.Value;
                    if (prefab == null) continue;
                    // A visible copy already stands under the spawn point: no need to draw it twice.
                    if (HasVisibleCopy(fsm.transform, prefab.name)) continue;
                    spawners.Add(new Spawner { Fsm = fsm, Action = make, Prefab = prefab });
                }
            }
        }

        private static bool HasVisibleCopy(Transform spawn, string name)
        {
            foreach (Transform child in spawn)
            {
                if (child.gameObject.activeInHierarchy && child.name.StartsWith(name)) return true;
            }
            return false;
        }

        // The prefab's meshes and materials, relative to its root (the root's own scale is added when drawn: Create
        // Object keeps it).
        private static Part[] LookOf(GameObject prefab)
        {
            if (looks.TryGetValue(prefab, out var parts)) return parts;
            var list = new List<Part>();
            var root = prefab.transform;
            var toRoot = root.worldToLocalMatrix;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !ActiveUnder(renderer.transform, root)) continue;
                Mesh mesh = null;
                if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                else if (renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                if (mesh == null) continue;
                list.Add(new Part { Mesh = mesh, Materials = renderer.sharedMaterials, Local = toRoot * renderer.transform.localToWorldMatrix });
            }
            parts = list.ToArray();
            looks[prefab] = parts;
            return parts;
        }

        // Switched on all the way up to the prefab's root (a prefab asset isn't "in the hierarchy", so check by hand).
        private static bool ActiveUnder(Transform t, Transform root)
        {
            for (var at = t; at != null; at = at.parent)
            {
                if (!at.gameObject.activeSelf) return false;
                if (at == root) break;
            }
            return true;
        }
    }
}
