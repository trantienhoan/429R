using System.Collections.Generic;
using Game.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Set Up PlayGround Field.
    /// - Makes tall-grass hiding places for the laughing pumpkins (Prefabs/PlayGround/PG_Grass_Hideout_1..3: patches of
    ///   the PlayGround grass models with a Hideout, walked through by everyone; Shy Grass makes them sway, and duck
    ///   when the player comes close) and concrete cover
    ///   (PG_Concrete_Block / PG_Concrete_Pillar, from the concrete rock models, scaled up).
    /// - Puts a PlayGround Field on Play_Ground_Z: after every seed it cleans the field, fits the arena to the grown fence
    ///   and scatters trees, bushes, grass, concrete, the skull, barrels, a pipe and a bench or table (more each seed), then
    ///   hides the magic hat by one of them. Everything it puts down comes from Prefabs/PlayGround. Change the lists and
    ///   numbers on its PlayGround Field.
    /// Running it again keeps the grass patches as they are (only adds a missing Hideout or Shy Grass), remakes the
    /// concrete prefabs and refills the field's lists.
    /// Tools > 429 Game > PlayGround > Set Up PlayGround Walls (user, 2026-11-07; run after Unity has imported the
    /// 7-clip Play_Ground_Z.fbx):
    /// - PlayGround_Z.controller: adds the states Play_Ground_Z_GO, _GO2, _COME3 and _GO3 (the FBX clips of those names,
    ///   Write Defaults on, speed 1, no transitions; the PlayGround Field plays them from C#) and deletes the old trigger
    ///   transitions COME -> COME_2 (COME2) and COME_2 -> Setup (COME3), so a stray trigger can't shrink the field. The
    ///   COME2 / COME3 parameters stay.
    /// - Play_Ground_Z's PlayGround Field: walls (its Animator), the GO / COME states, the phases (PlayGround_Phase_2, _3),
    ///   Scatter off (the random layout only while a phase isn't built), PG_Candy_Basket among the Clean Names.
    /// - Its PlayGround_Manager FSM: Phase 1 goes straight to State 1, so the old Bool Test -> COME2 -> Phase 2 -> COME3
    ///   chain never runs.
    /// Safe to run again.
    /// </summary>
    public static class PlayGroundFieldSetupMenu
    {
        private const string PlayGroundPath = "Assets/Prefabs/Bases/Play_Ground_Z.prefab";
        private const string ControllerPath = "Assets/Models/Animator/PlayGround_Z.controller";
        private const string WallsModelPath = "Assets/Models/Walls/Play_Ground_Z.fbx";
        private const string ManagerFsm = "PlayGround_Manager";

        // The states the PlayGround Field plays: GO at cook start (rounds 1-3), COME after seed 1 and 2.
        private static readonly string[] GoStates = { "Play_Ground_Z_GO", "Play_Ground_Z_GO2", "Play_Ground_Z_GO3" };
        private static readonly string[] ComeStates = { "Play_Ground_Z_COME_2", "Play_Ground_Z_COME3" };
        private static readonly string[] Phases = { "PlayGround_Phase_2", "PlayGround_Phase_3" };
        // States added to the controller (COME_2 is there already), with their spot in the Animator window.
        private static readonly (string name, Vector3 at)[] NewStates =
        {
            ("Play_Ground_Z_GO", new Vector3(650f, 140f, 0f)),
            ("Play_Ground_Z_GO2", new Vector3(900f, 60f, 0f)),
            ("Play_Ground_Z_COME3", new Vector3(900f, 140f, 0f)),
            ("Play_Ground_Z_GO3", new Vector3(1150f, 100f, 0f)),
        };
        // The old trigger transitions (their file IDs in the controller): COME -> COME_2 on COME2, COME_2 -> Setup on COME3.
        private static readonly long[] OldTransitionIds = { 3314044288345581760, 6249633201807267869 };
        private static readonly (string from, string to, string trigger)[] OldTransitions =
        {
            ("Play_Ground_Z_COME", "Play_Ground_Z_COME_2", "COME2"),
            ("Play_Ground_Z_COME_2", "Setup", "COME3"),
        };
        private const string Folder = "Assets/Prefabs/PlayGround/";
        private const string Models = "Assets/Models/PlayGround/";
        private const string Puff = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string PopSound = "Assets/Audio/SFX/bubble_pop_1.wav";
        private const string GrowSound = "Assets/Audio/SFX/whoosh.wav";

        // Each grass patch: (model, x, z, turn degrees, size).
        private static readonly (string model, float x, float z, float turn, float size)[][] Grass =
        {
            new[] { ("grass_3_medium", 0f, 0f, 0f, 1f), ("grass_1_big", 0.6f, 0.4f, 40f, 1.15f), ("grass_2_big", -0.55f, -0.45f, 120f, 1.1f), ("grass_1_medium", -0.5f, 0.55f, 200f, 1.1f) },
            new[] { ("grass_3_medium", 0f, 0f, 90f, 1.05f), ("grass_2_big", 0.55f, -0.5f, 10f, 1.15f), ("grass_2_medium", -0.6f, 0.35f, 250f, 1.1f), ("grass_1_big", 0.1f, 0.7f, 300f, 1.1f) },
            new[] { ("grass_3_medium", -0.7f, 0f, 0f, 1f), ("grass_3_medium", 0.75f, 0.1f, 180f, 0.95f), ("grass_1_big", 0f, -0.55f, 70f, 1.15f), ("grass_2_big", 0.1f, 0.6f, 160f, 1.1f) },
        };

        [MenuItem("Tools/429 Game/PlayGround/Set Up PlayGround Field")]
        public static void SetUp()
        {
            var made = new List<string>();
            var grass = new List<GameObject>();
            for (int i = 0; i < Grass.Length; i++)
            {
                var prefab = MakeGrass("PG_Grass_Hideout_" + (i + 1), Grass[i]);
                if (prefab != null) { grass.Add(prefab); made.Add(prefab.name); }
            }
            var concrete = new List<GameObject>();
            var block = MakeConcrete("PG_Concrete_Block", "rock_1_big", new Vector3(2.4f, 2.4f, 2.4f));
            if (block != null) { concrete.Add(block); made.Add(block.name); }
            var pillar = MakeConcrete("PG_Concrete_Pillar", "rock_2_big", new Vector3(1.7f, 4.2f, 1.7f));
            if (pillar != null) { concrete.Add(pillar); made.Add(pillar.name); }

            var root = PrefabUtility.LoadPrefabContents(PlayGroundPath);
            try
            {
                if (!root.TryGetComponent<PlayGroundField>(out var field)) field = root.AddComponent<PlayGroundField>();
                var so = new SerializedObject(field);

                var fence = new List<Object>();
                foreach (var part in new[] { "Bone_1", "Bone_2", "Bone_3", "Bone_4", "Bone_5", "Gate" })
                {
                    var t = root.transform.Find("Play_Ground_Z_Armature/" + part);
                    if (t != null) fence.Add(t);
                }
                SetArray(so.FindProperty("fence"), fence);
                var area = root.GetComponentInChildren<GhostRoamArea>(true);
                so.FindProperty("roamArea").objectReferenceValue = area;
                so.FindProperty("teleportFloor").objectReferenceValue = area != null ? area.transform.Find("Cube") : null;
                so.FindProperty("puff").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Puff);
                so.FindProperty("popSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PopSound);
                so.FindProperty("growSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(GrowSound);

                var kinds = so.FindProperty("kinds");
                kinds.arraySize = 0;
                AddKind(kinds, "Trees", Prefabs(Folder + "Big_Tree_01_01", Folder + "Big_Tree_01_03_1", Folder + "Big_Tree_01_04"), 2, 1, new Vector2(0.9f, 1.1f), 0.8f, true, true);
                AddKind(kinds, "Bushes", Prefabs(Folder + "bushes_01_01", Folder + "bushes_01_03", Folder + "bushes_01_04"), 2, 1, new Vector2(0.9f, 1.15f), 0.4f, false, true);
                AddKind(kinds, "Tall grass", grass, 4, 1, new Vector2(0.95f, 1.15f), 0.3f, false, true);
                AddKind(kinds, "Giant skull", Prefabs(Folder + "Giant_Skull"), 1, 0, Vector2.one, 0.8f, true, true);
                AddKind(kinds, "Concrete", concrete, 2, 1, new Vector2(0.85f, 1.15f), 0.5f, true, true);
                AddKind(kinds, "Barrels", Prefabs(Folder + "Wood_Barrel2"), 3, 1, Vector2.one, 0.3f, false, false);
                AddKind(kinds, "Pipes", Prefabs(Folder + "Cement_Pipe"), 1, 0, Vector2.one, 0.6f, true, true);
                AddKind(kinds, "Bench or table", Prefabs(Folder + "PG_Bench", Folder + "Table_2_small"), 1, 0, Vector2.one, 0.5f, false, true);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayGroundPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayGround Field] made " + string.Join(", ", made) + "; Play_Ground_Z has a PlayGround Field.");
        }

        [MenuItem("Tools/429 Game/PlayGround/Set Up PlayGround Field", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        [MenuItem("Tools/429 Game/PlayGround/Set Up PlayGround Walls")]
        public static void SetUpWalls()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[PlayGround Walls] Stop play mode first."); return; }
            var notes = new List<string>();
            SetUpController(notes);
            SetUpFieldWalls(notes);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayGround Walls] " + string.Join("; ", notes));
        }

        [MenuItem("Tools/429 Game/PlayGround/Set Up PlayGround Walls", true)]
        private static bool NotPlayingWalls() => !EditorApplication.isPlaying;

        // The controller: the new GO / COME3 states from the FBX clips, and the old trigger transitions gone.
        private static void SetUpController(List<string> notes)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || controller.layers.Length == 0)
            {
                Debug.LogWarning($"[PlayGround Walls] No animator controller at '{ControllerPath}': skipped.");
                notes.Add("no controller");
                return;
            }
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(WallsModelPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) clips[clip.name] = clip;
            }

            var machine = controller.layers[0].stateMachine;
            int added = 0, updated = 0;
            foreach (var (name, at) in NewStates)
            {
                var clip = FindClip(clips, name);
                if (clip == null)
                {
                    Debug.LogWarning($"[PlayGround Walls] No clip '{name}' in {WallsModelPath}: let Unity import the new FBX (7 clips), then run this again.");
                    notes.Add("MISSING clip " + name);
                    continue;
                }
                if (AnimationUtility.GetAnimationClipSettings(clip).loopTime)
                    Debug.LogWarning($"[PlayGround Walls] Clip '{clip.name}' loops: untick Loop Time on the FBX's Animation tab, so the walls stay put.");

                var state = FindState(machine, name);
                if (state == null)
                {
                    state = machine.AddState(name, at);
                    added++;
                }
                else if (state.motion == clip && state.writeDefaultValues && Mathf.Approximately(state.speed, 1f))
                {
                    continue;
                }
                else
                {
                    updated++;
                }
                state.motion = clip;
                state.writeDefaultValues = true;
                state.speed = 1f;
                EditorUtility.SetDirty(state);
            }

            int removed = 0;
            foreach (var child in machine.states)
            {
                var from = child.state;
                foreach (var transition in from.transitions)
                {
                    if (!IsOldTransition(from, transition)) continue;
                    from.RemoveTransition(transition);
                    removed++;
                }
            }

            EditorUtility.SetDirty(controller);
            notes.Add($"PlayGround_Z.controller: {added} states added, {updated} updated, {removed} old transitions deleted");
        }

        // The FBX clip for a state: the same name, or a take ending in it (e.g. 'Play_Ground_Z|GO').
        private static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, string state)
        {
            if (clips.TryGetValue(state, out var exact)) return exact;
            string take = state.Substring("Play_Ground_Z_".Length);
            foreach (var pair in clips)
            {
                if (pair.Key.EndsWith("|" + take) || pair.Key.EndsWith("_" + take)) return pair.Value;
            }
            return null;
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (var child in machine.states)
            {
                if (child.state != null && child.state.name == name) return child.state;
            }
            return null;
        }

        // One of the two old trigger transitions: by its file ID, or by where it goes and on which trigger.
        private static bool IsOldTransition(AnimatorState from, AnimatorStateTransition transition)
        {
            if (transition == null) return false;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(transition, out string _, out long id) &&
                System.Array.IndexOf(OldTransitionIds, id) >= 0) return true;
            string to = transition.destinationState != null ? transition.destinationState.name : "";
            foreach (var (oldFrom, oldTo, trigger) in OldTransitions)
            {
                if (from.name != oldFrom || to != oldTo) continue;
                foreach (var condition in transition.conditions)
                {
                    if (condition.parameter == trigger) return true;
                }
            }
            return false;
        }

        // Play_Ground_Z: the PlayGround Field's walls, phases and clean names, and the old FSM chain cut short.
        private static void SetUpFieldWalls(List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayGroundPath) == null)
            {
                Debug.LogWarning($"[PlayGround Walls] No '{PlayGroundPath}': skipped.");
                notes.Add("no Play_Ground_Z");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(PlayGroundPath);
            try
            {
                if (!root.TryGetComponent<PlayGroundField>(out var field))
                {
                    field = root.AddComponent<PlayGroundField>();
                    notes.Add("PlayGround Field added (run Set Up PlayGround Field for its lists)");
                }
                var walls = root.GetComponent<Animator>();
                if (walls == null) notes.Add("Play_Ground_Z has no Animator");
                var so = new SerializedObject(field);
                so.FindProperty("walls").objectReferenceValue = walls;
                SetStrings(so.FindProperty("goStates"), GoStates);
                SetStrings(so.FindProperty("comeStates"), ComeStates);
                SetStrings(so.FindProperty("phases"), Phases);
                so.FindProperty("scatter").boolValue = false;
                var clean = so.FindProperty("cleanNames");
                bool hasBasket = false;
                for (int i = 0; i < clean.arraySize; i++)
                {
                    if (clean.GetArrayElementAtIndex(i).stringValue == "PG_Candy_Basket") hasBasket = true;
                }
                if (!hasBasket)
                {
                    clean.arraySize++;
                    clean.GetArrayElementAtIndex(clean.arraySize - 1).stringValue = "PG_Candy_Basket";
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                var missing = new List<string>();
                foreach (var phase in Phases)
                {
                    if (FindChild(root.transform, phase) == null) missing.Add(phase);
                }
                notes.Add("Play_Ground_Z: walls, GO/COME states, phases set, Scatter off, PG_Candy_Basket cleaned" +
                          (missing.Count > 0 ? $"; not built yet: {string.Join(", ", missing)} (random layout until then)" : ""));
                notes.Add(StopOldPhaseChain(root));
                PrefabUtility.SaveAsPrefabAsset(root, PlayGroundPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // PlayGround_Manager's Phase 1 goes straight to State 1 (Phase_1 stays on), so Bool Test -> COME2 -> Phase 2 ->
        // COME3 never runs: the PlayGround Field drives the walls now.
        private static string StopOldPhaseChain(GameObject root)
        {
            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != ManagerFsm) continue;
                var from = fsm.Fsm.GetState("Phase 1");
                var end = fsm.Fsm.GetState("State 1");
                if (from == null || end == null) return ManagerFsm + ": no 'Phase 1' or 'State 1' state, left as it is";
                bool changed = false;
                foreach (var transition in from.Transitions)
                {
                    if (transition.FsmEvent == null || transition.FsmEvent.Name != "FINISHED" || transition.ToState == end.Name) continue;
                    transition.ToState = end.Name;
                    changed = true;
                }
                if (changed) EditorUtility.SetDirty(fsm);
                return ManagerFsm + (changed ? ": Phase 1 now goes to State 1" : ": Phase 1 already goes to State 1");
            }
            return "no " + ManagerFsm + " FSM";
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var part in root.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == name) return part;
            }
            return null;
        }

        private static void SetStrings(SerializedProperty array, string[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        // A patch of grass: the models side by side, and a trigger Hideout over them (the space a pumpkin hides in).
        private static GameObject MakeGrass(string name, (string model, float x, float z, float turn, float size)[] parts)
        {
            // Already made (and maybe rearranged by hand): only what's missing is added; the grass isn't touched.
            string path = Folder + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                var existing = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    if (!existing.TryGetComponent<Hideout>(out _)) { existing.AddComponent<Hideout>(); changed = true; }
                    if (!existing.TryGetComponent<Game.VFX.ShyGrass>(out _)) { existing.AddComponent<Game.VFX.ShyGrass>(); changed = true; }
                    return changed ? PrefabUtility.SaveAsPrefabAsset(existing, path) : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(existing);
                }
            }

            // Built in a preview scene, so the open scene isn't touched.
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                root.layer = LayerMask.NameToLayer("Ignore Raycast");
                var box = new Bounds();
                bool any = false;
                foreach (var (model, x, z, turn, size) in parts)
                {
                    var look = Look(root.transform, model, new Vector3(x, 0f, z), turn, Vector3.one * size);
                    if (look == null) continue;
                    var b = look.GetComponent<MeshRenderer>().bounds;
                    if (any) box.Encapsulate(b);
                    else box = b;
                    any = true;
                }
                if (!any) return null;

                // A little inside the leaves, up to about the grass's height.
                var trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(box.center.x, 0.5f, box.center.z);
                trigger.size = new Vector3(box.size.x * 0.8f, 1f, box.size.z * 0.8f);
                root.AddComponent<Hideout>();
                // Sways lightly; ducks shyly when the player comes close (and then hides nothing).
                root.AddComponent<Game.VFX.ShyGrass>();
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // Solid cover from a concrete rock model, scaled up: a collider the size of it and a kinematic body (it doesn't
        // get pushed about).
        private static GameObject MakeConcrete(string name, string model, Vector3 scale)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var look = Look(root.transform, model, Vector3.zero, 0f, scale);
                if (look == null) return null;
                var b = look.GetComponent<MeshRenderer>().bounds;
                var solid = root.AddComponent<BoxCollider>();
                solid.center = b.center;
                solid.size = b.size * 0.92f;
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.mass = 5000f;
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // A model's mesh and materials as a child (its own turn and scale from the model kept), its bottom on the floor.
        private static GameObject Look(Transform parent, string model, Vector3 at, float turn, Vector3 scale)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            var filter = source != null ? source.GetComponentInChildren<MeshFilter>() : null;
            var shown = source != null ? source.GetComponentInChildren<MeshRenderer>() : null;
            if (filter == null || shown == null) return null;

            var look = new GameObject(model);
            look.layer = parent.gameObject.layer;
            look.transform.SetParent(parent, false);
            look.transform.localRotation = Quaternion.Euler(0f, turn, 0f) * filter.transform.localRotation;
            look.transform.localScale = Vector3.Scale(filter.transform.lossyScale, scale);
            look.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = look.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = shown.sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            look.transform.localPosition = at;
            // Sit on the floor.
            float bottom = renderer.bounds.min.y - parent.position.y;
            look.transform.localPosition = at - Vector3.up * bottom;
            return look;
        }

        private static List<GameObject> Prefabs(params string[] paths)
        {
            var list = new List<GameObject>();
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
                if (prefab != null) list.Add(prefab);
                else Debug.LogWarning("[PlayGround Field] missing " + path + ".prefab");
            }
            return list;
        }

        private static void AddKind(SerializedProperty kinds, string label, List<GameObject> prefabs, int count, int morePerSeed,
            Vector2 scale, float gap, bool blocksPaths, bool hatCover)
        {
            int i = kinds.arraySize;
            kinds.arraySize = i + 1;
            var kind = kinds.GetArrayElementAtIndex(i);
            kind.FindPropertyRelative("label").stringValue = label;
            SetArray(kind.FindPropertyRelative("prefabs"), prefabs.ConvertAll(p => (Object)p));
            kind.FindPropertyRelative("count").intValue = count;
            kind.FindPropertyRelative("morePerSeed").intValue = morePerSeed;
            kind.FindPropertyRelative("scale").vector2Value = scale;
            kind.FindPropertyRelative("gap").floatValue = gap;
            kind.FindPropertyRelative("blocksPaths").boolValue = blocksPaths;
            kind.FindPropertyRelative("hatCover").boolValue = hatCover;
        }

        private static void SetArray(SerializedProperty array, List<Object> values)
        {
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
