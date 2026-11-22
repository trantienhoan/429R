using System.Collections.Generic;
using System.Linq;
using Game.Rooms;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ComponentUtility = UnityEditorInternal.ComponentUtility;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// The living room kit room (user, 2026-11-19: "create a kit of livingroom and playroom to replace the current
    /// livingroom_Base", then "Yes, build it").
    /// - Tools > 429 Game > Rooms > Build Living Room Kit Room: Assets/Prefabs/Bases/Livingroom_Kit_Base.prefab from the
    ///   kit (Assets/Models/Rooms/Livingroom_Kit.fbx, exported from Exports/Remodel/Livingroom_Kit.blend):
    ///   - the floor (9 x 9 m, never animated) and two sets: Living (the 9 x 9 m living room: the chained X door north,
    ///     the big double door east, the horseshoe shortcut door south, the old roof slab and lamp) and Play (the
    ///     playroom cross at 1x: a 7.5 m square with four 1.5 m arms, their floors, the old breakable brick wall at each
    ///     arm's end and the old breakable ceiling; GAMESTAGES makes the whole room 1.5x before it comes, as before);
    ///   - copied from the old room (Livingroom_Base_Z) so the game works as before: its Info FSM (the global
    ///     Livingroom_Base), Drop Wall Hung Props, the fall edges, the teleport area, Create_Pumpkin_3_Pos, the room
    ///     light at the old path GAMESTAGES switches, the shortcut door with all its parts, the brick walls with their
    ///     portals and the ceiling with its light spot and drop;
    ///   - an Animator (Livingroom_Kit_Animator) with the old state names (COME is the default: the living room comes
    ///     when the room spawns), each playing the Room Kit's come or go.
    /// - Use Living Room Kit Room / Use Old Living Room Room: which room Livingroom_Base_Pos spawns (the open scene is
    ///   marked changed, not saved). Use Living Room Kit Room also fits the furniture: the living room is bigger than
    ///   the old one (hung props move out to its walls), the playroom smaller (pieces move in, groups together).
    /// </summary>
    public static class LivingroomKitSetupMenu
    {
        private const string KitFbx = "Assets/Models/Rooms/Livingroom_Kit.fbx";
        private const string OldRoom = "Assets/Prefabs/Bases/Livingroom_Base_Z.prefab";
        private const string NewRoom = "Assets/Prefabs/Bases/Livingroom_Kit_Base.prefab";
        private const string ControllerPath = "Assets/Models/Animator/Livingroom_Kit_Animator.controller";
        private const string SpawnName = "Livingroom_Base_Pos";
        private const string LightPath = "Livingroom_Base_Z_Armature/Wall_08/Rooftop_LivingRoom/Point Light";
        private const string ShortcutDoorPath = "Livingroom_Base_Z_Armature/outholes/Bedroom_Base_Z_Door";
        private const string CeilingPath = "Livingroom_Base_Z_Armature/Wall_01/Livingroom_Base_Z_Roof_Sub";
        private const string BrickName = "Brick_Wall_Breakable";
        private const float Room = 9f, Square = 7.5f, ArmHalf = 0.75f, Wall = 0.31f, Height = 3f, PlayScale = 1.5f;
        private const float Doorway = 3f, OldRoofHalf = 4.63f;
        // The old ceiling's underside was at 3.39 m (1x); the kit's walls are 3 m.
        private const float CeilingDrop = 0.39f;
        // The brick wall's middle above the floor and half its thickness.
        private const float BrickMiddle = 1.69f, BrickHalfDepth = 0.139f;

        private static readonly Dictionary<string, string> MaterialFor = new()
        {
            { "Livingroom_Wall", "Assets/Materials/Livingroom_Wall_Spooky.mat" },
            { "Bedroom_Wood_Floor", "Assets/Materials/Wood_Floor_Mat 1.mat" },
            { "Bedroom_Metal", "Assets/Materials/Bedroom_Metal_Mat 1.mat" },
        };

        // The living room's four sides (North +Z, East +X, South -Z, West -X), pieces left to right as seen from inside,
        // where the old room had its doors (sampled at the end of COME): the X door at the north wall's east end, the
        // big door in the east wall at z 0.6..3.1, the shortcut door at the south wall's west end.
        private static readonly string[][] LivingSides =
        {
            new[] { "LR_Kit_Wall_3m", "LR_Kit_Wall_3m", "LR_Kit_Door+X" },
            new[] { "LR_Kit_Wall_1p5m", "LR_Kit_Door_Big+Double", "LR_Kit_Wall_3m", "LR_Kit_Wall_1p5m" },
            new[] { "LR_Kit_Wall_3m", "LR_Kit_Wall_3m", "LR_Kit_Door+Shortcut" },
            new[] { "LR_Kit_Wall_3m", "LR_Kit_Wall_3m", "LR_Kit_Wall_3m" },
        };
        private static readonly (Vector3 inside, Vector3 start)[] Sides =
        {
            (Vector3.back, new Vector3(-Room / 2, 0f, Room / 2)),
            (Vector3.left, new Vector3(Room / 2, 0f, Room / 2)),
            (Vector3.forward, new Vector3(Room / 2, 0f, -Room / 2)),
            (Vector3.right, new Vector3(-Room / 2, 0f, -Room / 2)),
        };

        // The furniture sets the two stages spawn (under -GAMESTAGES).
        private static readonly string[] LivingSets = { "--Livingroom--", "--Livingroom_Chaos--" };
        private static readonly string[] PlaySets = { "--Livingroom_Jenga--", "--Livingroom_Jenga Chaos--" };

        private static Dictionary<string, Mesh> meshes;
        private static Dictionary<string, Material[]> materials;
        private static GameObject old;

        [MenuItem("Tools/429 Game/Rooms/Build Living Room Kit Room")]
        public static void Build()
        {
            old = AssetDatabase.LoadAssetAtPath<GameObject>(OldRoom);
            if (!LoadKit(out string problem) || old == null)
            {
                Debug.LogError("[Living Room Kit] " + (old == null ? "No " + OldRoom : problem) + ". Nothing made.");
                return;
            }
            var controller = MakeController();
            var scene = EditorSceneManager.NewPreviewScene();
            var notes = new List<string>();
            try
            {
                var root = new GameObject("Livingroom_Kit_Base");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.layer = old.layer;
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var kit = root.AddComponent<RoomKit>();
                foreach (var c in old.GetComponents<Component>())
                {
                    if (c is PlayMakerFSM || c is DropWallHungProps)
                    {
                        ComponentUtility.CopyComponent(c);
                        ComponentUtility.PasteComponentAsNew(root);
                        notes.Add(c is PlayMakerFSM f ? "FSM " + f.FsmName : c.GetType().Name);
                    }
                }
                if (root.TryGetComponent(out DropWallHungProps dropper))
                {
                    var dso = new SerializedObject(dropper);
                    dso.FindProperty("animator").objectReferenceValue = animator;
                    dso.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var name in new[] { "Teleport Area", "Fall Edges", "Create_Pumpkin_3_Pos" })
                {
                    var src = old.transform.Find(name);
                    if (src == null) continue;
                    var copy = Object.Instantiate(src.gameObject, root.transform);
                    copy.name = name;
                    copy.transform.SetPositionAndRotation(src.position, src.rotation);
                    notes.Add(name);
                }

                // The floor: 3 x 3 kit tiles, 9 x 9 m, the living room's; the playroom's arms bring their own.
                var floor = Child(root.transform, "Floor", root.layer);
                foreach (float x in new[] { -1.5f, 1.5f, 4.5f })
                    foreach (float z in new[] { -1.5f, 1.5f, 4.5f })
                        Piece("LR_Kit_Floor_3m", floor, new Vector3(x, 0f, z), Quaternion.identity);

                // The room light where GAMESTAGES looks for it, under the ceiling's middle.
                var oldLight = old.transform.Find(LightPath);
                var lightParent = Child(Child(Child(root.transform, "Livingroom_Base_Z_Armature", root.layer), "Wall_08", root.layer), "Rooftop_LivingRoom", root.layer);
                if (oldLight != null)
                {
                    var lamp = Object.Instantiate(oldLight.gameObject, lightParent);
                    lamp.name = "Point Light";
                    lamp.transform.localPosition = new Vector3(0f, Height - 0.35f, 0f);
                    lamp.transform.localRotation = oldLight.localRotation;
                    notes.Add("room light");
                }

                var living = MakeLiving(root.transform, notes);
                var play = MakePlay(root.transform, notes);
                var secret = MakeSecret(root.transform, living.root, lightParent.Find("Point Light"), kit, notes);

                var so = new SerializedObject(kit);
                var sets = so.FindProperty("sets");
                sets.arraySize = 3;
                SetEntry(sets.GetArrayElementAtIndex(0), "Living", living);
                SetEntry(sets.GetArrayElementAtIndex(1), "Play", play);
                SetEntry(sets.GetArrayElementAtIndex(2), "Secret", secret);
                var puffGuid = AssetDatabase.FindAssets("Smoke_Flash_Small_Puff t:Prefab").FirstOrDefault();
                if (puffGuid != null)
                    so.FindProperty("puff").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(puffGuid));
                so.ApplyModifiedPropertiesWithoutUndo();

                KitDoorFitMenu.Fit(root, notes);
                PrefabUtility.SaveAsPrefabAsset(root, NewRoom);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log($"[Living Room Kit] {NewRoom} built (floor 9 x 9 m; Living 9 x 9 m; Play: the cross at 1x, 1.5x in the game), with {string.Join(", ", notes)}. " +
                      "Tools > 429 Game > Rooms > Use Living Room Kit Room puts it in the stages.");
        }

        [MenuItem("Tools/429 Game/Rooms/Build Living Room Kit Room", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/429 Game/Rooms/Use Living Room Kit Room")]
        public static void UseKit() => Swap(NewRoom);

        [MenuItem("Tools/429 Game/Rooms/Use Old Living Room Room")]
        public static void UseOld() => Swap(OldRoom);

        [MenuItem("Tools/429 Game/Rooms/Use Living Room Kit Room", true)]
        [MenuItem("Tools/429 Game/Rooms/Use Old Living Room Room", true)]
        private static bool CanSwap() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---------------------------------------------------------------------------------------------------------

        private static bool LoadKit(out string problem)
        {
            meshes = AssetDatabase.LoadAllAssetsAtPath(KitFbx).OfType<Mesh>().ToDictionary(m => m.name);
            materials = new Dictionary<string, Material[]>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KitFbx);
            if (model == null || meshes.Count == 0)
            {
                problem = "No kit in " + KitFbx + " (export it from Exports/Remodel/Livingroom_Kit.blend)";
                return false;
            }
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                materials[mf.sharedMesh.name] = r.sharedMaterials
                    .Select(m => m != null && MaterialFor.TryGetValue(m.name, out var path) ? AssetDatabase.LoadAssetAtPath<Material>(path) : m)
                    .ToArray();
            }
            problem = null;
            return true;
        }

        private static AnimatorController MakeController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            // The old states and times: COME 2.67 s at speed 0.45 = 5.9 s (the default: it plays when the room
            // spawns), LivingRoom_GO 3.33 s, COME2 1 s at speed 0.25 = 4 s, GO4 1.67 s.
            var come = State(machine, "COME", "Living", true, 5.93f);
            machine.defaultState = come;
            State(machine, "LivingRoom_GO", "Living", false, 3.33f);
            // A checkpoint (CP Scale LR) spawns the room and plays COME2 at once: the living room goes away for it.
            State(machine, "COME2", "Play", true, 4.0f, "Living");
            State(machine, "GO4", "Play", false, 1.67f);
            foreach (var name in new[] { "LivingRoom_GO2", "LivingRoom_GO3", "Livingroom_Setup" }) machine.AddState(name);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorState State(AnimatorStateMachine machine, string name, string set, bool come, float seconds, params string[] hide)
        {
            var state = machine.AddState(name);
            var b = state.AddStateMachineBehaviour<RoomKitState>();
            b.set = set;
            b.come = come;
            b.seconds = seconds;
            b.hideAtOnce = hide;
            EditorUtility.SetDirty(b);
            return state;
        }

        // The living room: four sides of kit pieces, the doors, the roof (the old slab scaled to the room, its lamp).
        private static (Transform root, Transform roof) MakeLiving(Transform room, List<string> notes)
        {
            var set = Child(room, "Living", room.gameObject.layer);
            for (int i = 0; i < LivingSides.Length; i++)
            {
                var (inside, start) = Sides[i];
                var turn = Quaternion.LookRotation(inside, Vector3.up);
                var run = Vector3.Cross(inside, Vector3.up);
                float along = 0f;
                foreach (var entry in LivingSides[i])
                {
                    var names = entry.Split('+');
                    var part = Piece(names[0], set, start + run * along, turn);
                    // The X and double doors break (their own box blocks the doorway till then); the shortcut door's
                    // doorway keeps its stop behind the leaf, as the old room's.
                    Solid(part, names[0], names.Length < 2 || names[1] == "Shortcut");
                    if (names.Length > 1)
                    {
                        if (names[1] == "Shortcut") ShortcutDoor(part, notes);
                        // The X door (user, 2026-11-20: "the other door leads to fall edge"): the bedroom's W/C door's
                        // parts; behind it is only the north fall edge.
                        else if (names[1] == "X") LeafDoor(part, "LR_Kit_Leaf_X", "Locked_Door", "X_Door", notes);
                        // The double door: the bedroom's chained door's parts; behind it is the secret room.
                        else LeafDoor(part, "LR_Kit_Leaf_Double", "Chained_Door", "Double_Door", notes);
                    }
                    along += names[0] == "LR_Kit_Wall_1p5m" ? 1.5f : 3f;
                }
            }
            var roof = Child(set, "Roof", room.gameObject.layer);
            float slab = (Room / 2f + Wall) / OldRoofHalf;
            var top = Piece("LR_Kit_Roof", roof, Vector3.zero, Quaternion.identity);
            top.transform.localScale = new Vector3(slab, 1f, slab);
            top.AddComponent<BoxCollider>();
            Piece("LR_Kit_Lamp", roof, Vector3.zero, Quaternion.identity);
            return (set, roof);
        }

        // The playroom at 1x: the cross's walls round its outline (clockwise from above, the room on the right), the
        // arms' floors past the 9 m floor, the old brick walls closing the arms, the old breakable ceiling as its roof.
        // Where an arm leaves the square its walls start behind the square's wall (no faces on each other).
        private static (Transform root, Transform roof) MakePlay(Transform room, List<string> notes)
        {
            var set = Child(room, "Play", room.gameObject.layer);
            float c = Square / 2f, w = ArmHalf, s = c + Wall, e = s + 3f;
            var outline = new[]
            {
                new Vector2(-w, e), new Vector2(w, e), new Vector2(w, s), new Vector2(w, c), new Vector2(c, c), new Vector2(c, w),
                new Vector2(s, w), new Vector2(e, w), new Vector2(e, -w), new Vector2(s, -w), new Vector2(c, -w), new Vector2(c, -c),
                new Vector2(w, -c), new Vector2(w, -s), new Vector2(w, -e), new Vector2(-w, -e), new Vector2(-w, -s), new Vector2(-w, -c),
                new Vector2(-c, -c), new Vector2(-c, -w), new Vector2(-s, -w), new Vector2(-e, -w), new Vector2(-e, w), new Vector2(-s, w),
                new Vector2(-c, w), new Vector2(-c, c), new Vector2(-w, c), new Vector2(-w, s),
            };
            // The old brick walls by the bone they stood on: the arms' ends in the outline's order are north (Wall_04),
            // east (Wall_01), south (Wall_02), west (Wall_03).
            var oldBricks = old.transform.GetComponentsInChildren<Transform>(true).Where(t => t.name == BrickName).ToList();
            var bricks = new[] { "Wall_04", "Wall_01", "Wall_02", "Wall_03" }
                .Select(bone => oldBricks.FirstOrDefault(t => t.parent != null && t.parent.name == bone)).ToList();
            int brick = 0;
            for (int i = 0; i < outline.Length; i++)
            {
                var a = new Vector3(outline[i].x, 0f, outline[i].y);
                var b = new Vector3(outline[(i + 1) % outline.Length].x, 0f, outline[(i + 1) % outline.Length].y);
                float length = (b - a).magnitude;
                if (length < 1f) continue; // the end of the square's wall closes this bit
                var d = (b - a) / length;
                var turn = Quaternion.LookRotation(Vector3.Cross(Vector3.up, d), Vector3.up);
                if (length < 2f)
                {
                    // An arm's end: the old brick wall closes it (its face on the line), on a pivot that folds like a wall.
                    var end = Child(set, "Brick_End_" + "NESW"[brick % 4], room.gameObject.layer);
                    end.SetLocalPositionAndRotation((a + b) / 2f, turn);
                    if (brick < bricks.Count && bricks[brick] != null)
                    {
                        var copy = Object.Instantiate(bricks[brick].gameObject, end);
                        copy.name = BrickName;
                        copy.transform.SetLocalPositionAndRotation(new Vector3(0f, BrickMiddle, -BrickHalfDepth), Quaternion.identity);
                        copy.transform.localScale = Vector3.one;
                    }
                    brick++;
                    continue;
                }
                Solid(Piece("LR_Kit_Wall_3m", set, a, turn), "LR_Kit_Wall_3m");
            }
            notes.Add(brick + " brick walls");
            // The arms' floors, past the room's 9 m floor: they grow in with the playroom and stay, as the old floor grew.
            float f0 = Room / 2f;
            var north = ArmFloor(set, "Floor_N", -w, w, f0, e);
            ArmFloor(set, "Floor_S", -w, w, -e, -f0);
            ArmFloor(set, "Floor_E", f0, e, -w, w);
            ArmFloor(set, "Floor_W", -e, -f0, -w, w);
            // The living room's round-1 fall edges count only while "Livingroom_Floor" is smaller than 2: the old floor
            // bone grew past that with the playroom. This marker does the same (tiny until the playroom's floor comes).
            var marker = Child(north, "Livingroom_Floor", room.gameObject.layer);
            marker.localScale = Vector3.one * 2f;
            int edges = 0;
            foreach (var edge in room.GetComponentsInChildren<Game.Locomotion.FallEdge>(true))
            {
                var eso = new SerializedObject(edge);
                var size = eso.FindProperty("sizeOf");
                if (size == null || size.objectReferenceValue == null || size.objectReferenceValue.name != "Livingroom_Floor") continue;
                size.objectReferenceValue = marker;
                eso.ApplyModifiedPropertiesWithoutUndo();
                edges++;
            }
            notes.Add(edges + " round-1 fall edges watch the playroom's floor");
            // The old breakable ceiling (EM3's start breaks it), its underside on the walls' tops.
            Transform roof = null;
            var ceiling = old.transform.Find(CeilingPath);
            if (ceiling != null)
            {
                var copy = Object.Instantiate(ceiling.gameObject, set);
                copy.name = ceiling.name;
                copy.transform.SetLocalPositionAndRotation(new Vector3(0f, -CeilingDrop, 0f), Quaternion.identity);
                copy.transform.localScale = Vector3.one;
                roof = copy.transform;
                notes.Add("breakable ceiling");
            }
            return (set, roof);
        }

        private static Transform ArmFloor(Transform set, string name, float x0, float x1, float z0, float z1)
        {
            var group = Child(set, name, set.gameObject.layer);
            var middle = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            group.localPosition = middle;
            var tile = Piece("LR_Kit_Floor_3m", group, new Vector3(x1, 0f, z1) - middle, Quaternion.identity);
            tile.transform.localScale = new Vector3((x1 - x0) / 3f, 1f, (z1 - z0) / 3f);
            return group;
        }

        private static GameObject Piece(string mesh, Transform parent, Vector3 at, Quaternion turn)
        {
            var go = new GameObject(mesh);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(at, turn);
            if (meshes.TryGetValue(mesh, out var m)) go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials.TryGetValue(mesh, out var mats) ? mats : new Material[0];
            return go;
        }

        // Walls block like the old ones: a kinematic body, box colliders (round a doorway: its sides, its head, and a
        // stop behind the leaf in the opening, as the old room's "outholes" boxes) and a carving NavMesh obstacle.
        private static void Solid(GameObject part, string kind, bool stop = true)
        {
            var b = part.GetComponent<MeshFilter>().sharedMesh.bounds;
            var body = part.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            if (kind == "LR_Kit_Door" || kind == "LR_Kit_Door_Big")
            {
                // The pieces run from x = 0 to -3 (Unity), the doorway centred, from the floor up to 2.08.
                float wide = kind == "LR_Kit_Door_Big" ? 2.47f : 1.40f, top = 2.08f;
                float a = -1.5f - wide / 2f, c = -1.5f + wide / 2f;
                Box(part, new Vector3(-3f, 0f, -Wall), new Vector3(a, Height, 0f));
                Box(part, new Vector3(c, 0f, -Wall), new Vector3(0f, Height, 0f));
                Box(part, new Vector3(a, top, -Wall), new Vector3(c, Height, 0f));
                if (stop) Box(part, new Vector3(a, 0f, -Wall), new Vector3(c, top, -Wall + 0.08f));
            }
            else
            {
                Box(part, b.min, b.max);
            }
            var obstacle = part.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = b.center;
            obstacle.size = b.size;
            obstacle.carving = true;
        }

        private static void Box(GameObject part, Vector3 lo, Vector3 hi)
        {
            var box = part.AddComponent<BoxCollider>();
            box.center = (lo + hi) / 2f;
            box.size = hi - lo;
        }

        // The old shortcut door with all its parts (health, wobble, break pieces, the coin toss) on the kit's leaf.
        // The kit script turned the old door model half round and centred it in the doorway, its planks' back just
        // behind the frame: kit = half * old + shift, in the old door's own frame.
        private static void ShortcutDoor(GameObject doorway, List<string> notes)
        {
            var src = old.transform.Find(ShortcutDoorPath);
            if (src == null || !meshes.TryGetValue("LR_Kit_Leaf_Horseshoe", out var leaf)) return;
            var oldLook = src.Find("Jiggle/Door_Mesh");
            if (oldLook == null) oldLook = src;
            var oldLeaf = oldLook.GetComponent<MeshFilter>() != null ? oldLook.GetComponent<MeshFilter>().sharedMesh : null;
            if (oldLeaf == null) return;
            var door = Object.Instantiate(src.gameObject, doorway.transform);
            door.name = "Shortcut_Door";
            var half = Quaternion.Euler(0f, 180f, 0f);
            var ob = oldLeaf.bounds;
            ob.center += src.InverseTransformPoint(oldLook.position);
            var shift = new Vector3(-Doorway / 2f + ob.center.x, -ob.min.y, leaf.bounds.min.z + ob.max.z);
            door.transform.localPosition = Vector3.zero;
            door.transform.localRotation = Quaternion.identity;
            door.transform.localScale = Vector3.one;
            foreach (Transform child in door.transform)
            {
                var was = src.Find(child.name);
                if (was == null) continue;
                child.localPosition = shift + half * was.localPosition;
                child.localRotation = half * was.localRotation;
            }
            var jiggle = door.transform.Find("Jiggle");
            if (jiggle != null)
            {
                jiggle.localRotation = Quaternion.identity;
                var look = jiggle.Find("Door_Mesh");
                if (look != null)
                {
                    look.localPosition = -jiggle.localPosition;
                    look.localRotation = Quaternion.identity;
                    look.localScale = Vector3.one;
                    look.GetComponent<MeshFilter>().sharedMesh = leaf;
                }
            }
            else if (door.TryGetComponent(out MeshFilter mf))
            {
                mf.sharedMesh = leaf;
            }
            if (door.TryGetComponent(out BoxCollider box) && src.TryGetComponent(out BoxCollider oldBox))
            {
                box.center = shift + half * oldBox.center;
                box.size = oldBox.size;
            }
            notes.Add("shortcut door (old door's parts on the kit leaf)");
        }

        // A breakable door on a kit leaf: the bedroom kit room's door (Health 300, Damage, break pieces, wobble) with this
        // leaf's look and box. The leaf is in the doorway's frame, so the door shares the doorway's pivot.
        private static GameObject LeafDoor(GameObject doorway, string leafName, string from, string name, List<string> notes)
        {
            var bedroom = AssetDatabase.LoadAssetAtPath<GameObject>(BedroomKitPath);
            var src = bedroom != null ? bedroom.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == from) : null;
            if (src == null || !meshes.TryGetValue(leafName, out var leaf))
            {
                notes.Add("no " + from + " in " + BedroomKitPath + " (build the bedroom kit room first): " + leafName + " doesn't break");
                Piece(leafName, doorway.transform, Vector3.zero, Quaternion.identity);
                return null;
            }
            var door = Object.Instantiate(src.gameObject, doorway.transform);
            door.name = name;
            door.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            door.transform.localScale = Vector3.one;
            door.GetComponent<MeshFilter>().sharedMesh = leaf;
            if (materials.TryGetValue(leafName, out var looks)) door.GetComponent<MeshRenderer>().sharedMaterials = looks;
            var b = leaf.bounds;
            if (door.TryGetComponent(out BoxCollider box))
            {
                box.center = b.center;
                box.size = b.size;
            }
            var spot = door.transform.Find("Door_Break_Drop_Pos");
            if (spot != null) spot.localPosition = new Vector3(b.center.x, b.min.y + 0.01f, b.center.z);
            notes.Add(name + " breaks (" + from + "'s parts on " + leafName + ")");
            return door;
        }

        // ---------------------------------------------------------------------------------------------------------
        // The secret room (user, 2026-11-20: "a another secret room for livingroom also, located behind that double
        // doors. Reward = 33 candies or giant cockroach boss with 33 health ... EnemyManager On = break secret room"):
        // the bedroom's, east of the living room, sharing its east wall (the double door's), the same size (9 x 9 m),
        // no door or window of its own. A Secret set the Secret Room shows, its floor, teleport area and light.

        private const string BedroomKitPath = "Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab";
        private const float SecretX = Room + Wall;
        private static readonly string[][] SecretSides =
        {
            new[] { "LR_Kit_Wall_3m", "LR_Kit_Wall_3m", "LR_Kit_Wall_3m" },
            new[] { "LR_Kit_Wall_3m", "LR_Kit_Wall_3m", "LR_Kit_Wall_3m" },
            new[] { "LR_Kit_Wall_3m", "LR_Kit_Wall_3m", "LR_Kit_Wall_3m" },
            null,
        };

        private static (Transform root, Transform roof) MakeSecret(Transform room, Transform living, Transform roomLight, RoomKit kit, List<string> notes)
        {
            int layer = room.gameObject.layer;
            var set = Child(room, "Secret", layer);
            set.localPosition = new Vector3(SecretX, 0f, 0f);
            for (int i = 0; i < Sides.Length; i++)
            {
                if (SecretSides[i] == null) continue;
                var (inside, start) = Sides[i];
                var turn = Quaternion.LookRotation(inside, Vector3.up);
                var run = Vector3.Cross(inside, Vector3.up);
                float along = 0f;
                foreach (var piece in SecretSides[i])
                {
                    Solid(Piece(piece, set, start + run * along, turn), piece);
                    along += 3f;
                }
            }
            // Its roof: the living room's, not reaching over the shared wall (the living room's roof covers that).
            var roof = Child(set, "Roof", layer);
            float half = (Room / 2f + Wall) / OldRoofHalf, halfX = ((Room + Wall) / 2f) / OldRoofHalf;
            var top = Piece("LR_Kit_Roof", roof, new Vector3(Wall / 2f, 0f, 0f), Quaternion.identity);
            top.transform.localScale = new Vector3(halfX, 1f, half);
            top.AddComponent<BoxCollider>();
            Piece("LR_Kit_Lamp", roof, Vector3.zero, Quaternion.identity);

            // Its floor: 3 x 3 tiles from the living room's floor edge (x 4.5) to the secret room's outer wall, so the
            // doorway under the shared wall has floor too (tiles stretched 7%).
            var floor = Child(room, "Secret_Floor", layer);
            float x0 = Room / 2f, x1 = SecretX + Room / 2f + Wall;
            if (meshes.TryGetValue("LR_Kit_Floor_3m", out var tile))
            {
                var tb = tile.bounds;
                float k = (x1 - x0) / 3f / tb.size.x;
                for (int i = 0; i < 3; i++)
                    foreach (float z in new[] { -1.5f, 1.5f, 4.5f })
                    {
                        float lo = x0 + i * (x1 - x0) / 3f;
                        var t = Piece("LR_Kit_Floor_3m", floor, new Vector3(lo - tb.min.x * k, 0f, z), Quaternion.identity);
                        t.transform.localScale = new Vector3(k, 1f, 1f);
                    }
            }
            GameObject teleport = null;
            var livingTeleport = room.Find("Teleport Area");
            if (livingTeleport != null)
            {
                teleport = Object.Instantiate(livingTeleport.gameObject, room);
                teleport.name = "Secret Teleport Area";
                teleport.transform.localPosition = livingTeleport.localPosition + new Vector3(SecretX, 0f, 0f);
            }
            Light secretLight = null;
            if (roomLight != null)
            {
                var copy = Object.Instantiate(roomLight.gameObject, room);
                copy.name = "Secret Room Light";
                copy.transform.localPosition = new Vector3(SecretX, Height - 0.35f, 0f);
                foreach (var fsm in copy.GetComponents<PlayMakerFSM>()) Object.DestroyImmediate(fsm);
                secretLight = copy.GetComponent<Light>();
            }

            var door = living.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Double_Door");
            var secretRoom = room.gameObject.AddComponent<SecretRoom>();
            var so = new SerializedObject(secretRoom);
            so.FindProperty("kit").objectReferenceValue = kit;
            so.FindProperty("hostSet").stringValue = "Living";
            so.FindProperty("secretSet").stringValue = "Secret";
            so.FindProperty("door").objectReferenceValue = door != null ? door.gameObject : null;
            so.FindProperty("doorwayObstacle").objectReferenceValue = door != null && door.parent != null ? door.parent.GetComponent<NavMeshObstacle>() : null;
            so.FindProperty("floor").objectReferenceValue = floor;
            so.FindProperty("teleportArea").objectReferenceValue = teleport;
            so.FindProperty("roomLight").objectReferenceValue = secretLight;
            so.FindProperty("inside").boundsValue = new Bounds(new Vector3(SecretX, Height / 2f, 0f), new Vector3(Room - 0.2f, Height, Room - 0.2f));
            // Its fall edges: the east ones move back past it, the round-1 north and south ones reach along it.
            so.FindProperty("alongX").boolValue = true;
            so.FindProperty("farEdge").objectReferenceValue = Edge(room, "Fall Edge Round 1 East");
            var more = so.FindProperty("moreFarEdges");
            more.arraySize = 1;
            more.GetArrayElementAtIndex(0).objectReferenceValue = Edge(room, "Fall Edge East");
            so.FindProperty("sideEdgeA").objectReferenceValue = Edge(room, "Fall Edge Round 1 North");
            so.FindProperty("sideEdgeB").objectReferenceValue = Edge(room, "Fall Edge Round 1 South");
            so.FindProperty("moreSideEdges").arraySize = 0;
            so.FindProperty("newFarEdge").floatValue = x1 + 0.03f;
            so.FindProperty("openPuff").objectReferenceValue = SecretRoomSetupMenu.Find("Smoke_Puff_Burst_Out");
            so.FindProperty("breakPuff").objectReferenceValue = SecretRoomSetupMenu.Find("Smoke_Flash_Five_Puffs");
            so.FindProperty("openSound").objectReferenceValue = SecretRoomSetupMenu.Sound("whoosh");
            so.FindProperty("candySound").objectReferenceValue = SecretRoomSetupMenu.Sound("Clear,_resonant_bell_1");
            so.FindProperty("spiderSound").objectReferenceValue = SecretRoomSetupMenu.Sound("cockroach_screech");
            var pumpkin = AssetDatabase.LoadAssetAtPath<GameObject>(SecretRoomSetupMenu.GiantPumpkinPath);
            so.FindProperty("giantPumpkin").objectReferenceValue = pumpkin != null ? pumpkin : SecretRoomSetupMenu.GiantPumpkin(notes);
            so.FindProperty("giantSpider").objectReferenceValue = SecretRoomSetupMenu.GiantCockroach(notes);
            so.FindProperty("pumpkinSpot").vector3Value = new Vector3(SecretX, 0f, 0f);
            so.FindProperty("brokenPieces").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SecretRoomSetupMenu.PiecesPath);
            var candies = SecretRoomSetupMenu.Candies();
            var list = so.FindProperty("candies");
            list.arraySize = candies.Length;
            for (int i = 0; i < candies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = candies[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            notes.Add("secret room behind the double door" + (door == null ? " (NO breakable double door)" : ""));
            return (set, roof);
        }

        private static BoxCollider Edge(Transform room, string name)
        {
            var t = room.Find("Fall Edges/" + name);
            return t != null ? t.GetComponent<BoxCollider>() : null;
        }

        private static Transform Child(Transform parent, string name, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void SetEntry(SerializedProperty entry, string name, (Transform root, Transform roof) set)
        {
            entry.FindPropertyRelative("name").stringValue = name;
            entry.FindPropertyRelative("root").objectReferenceValue = set.root;
            entry.FindPropertyRelative("roof").objectReferenceValue = set.roof;
        }

        // Which room prefab the stages' spawn point (Livingroom_Base_Pos) makes; the kit room also fits the furniture.
        private static void Swap(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var spawn = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == SpawnName && g.scene == scene && g.hideFlags == HideFlags.None);
            if (prefab == null || spawn == null)
            {
                Debug.LogError("[Living Room Kit] " + (prefab == null ? "No " + prefabPath + " (build it first)" : "No " + SpawnName + " in " + scene.name) + ". Nothing changed.");
                return;
            }
            int changed = 0;
            foreach (var fsm in spawn.GetComponents<PlayMakerFSM>())
            {
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                foreach (var state in fsm.FsmStates)
                {
                    state.LoadActions();
                    bool edit = false;
                    foreach (var create in state.Actions.OfType<HutongGames.PlayMaker.Actions.CreateObject>())
                    {
                        if (create.gameObject.Value == prefab) continue;
                        create.gameObject.Value = prefab;
                        edit = true;
                    }
                    if (!edit) continue;
                    Undo.RecordObject(fsm, "Living room");
                    state.SaveActions();
                    EditorUtility.SetDirty(fsm);
                    changed++;
                }
            }
            string fitted = "";
            if (prefabPath == NewRoom)
            {
                var moved = FitFurniture(scene, spawn.transform);
                fitted = moved.Count == 0 ? " The furniture already fits it." : $" Moved {moved.Count} furniture spawn point(s) to fit its walls: {string.Join("; ", moved)}.";
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Living Room Kit] {SpawnName} now makes {prefab.name} ({changed} action(s) changed).{fitted} Save the scene to keep it.");
        }

        // ---------------------------------------------------------------------------------------------------------
        // Furniture fit. The living room sets: in from a wall a piece pokes into; a hung piece (picture, candle) out to
        // the new wall it hung on (the room is 0.45 m wider than the old one). The playroom sets (the room is 1.5x by
        // then): into the cross (its square or an arm), the smallest move. Touching pieces move as one group; a group
        // pushes along what it would bump; a piece that falls when made starts under the ceiling.

        private static List<string> FitFurniture(UnityEngine.SceneManagement.Scene scene, Transform roomSpot)
        {
            var moved = new List<string>();
            if (Quaternion.Angle(roomSpot.rotation, Quaternion.identity) > 1f)
            {
                Debug.LogWarning("[Living Room Kit] " + SpawnName + " is turned: the furniture was not checked against the kit walls.");
                return moved;
            }
            var spots = CollectSpots(scene, LivingSets.Concat(PlaySets).ToArray(), roomSpot);
            foreach (var set in spots.Select(s => s.set).Distinct().ToList())
            {
                bool play = PlaySets.Contains(set);
                var list = spots.Where(s => s.set == set).ToList();
                moved.AddRange(FitSet(set, list, play));
            }
            return moved;
        }

        private class Spot
        {
            public Transform at;
            public string set, what;
            public Vector3 lo, hi;
            public bool falls, hung;
        }

        private static List<Spot> CollectSpots(UnityEngine.SceneManagement.Scene scene, string[] setNames, Transform roomSpot)
        {
            var points = new Dictionary<Transform, (List<GameObject> makes, string set)>();
            foreach (var set in Resources.FindObjectsOfTypeAll<GameObject>()
                         .Where(g => g.scene == scene && g.hideFlags == HideFlags.None && setNames.Contains(g.name)))
            {
                foreach (var fsm in set.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.FsmStates)
                    {
                        state.LoadActions();
                        foreach (var make in state.Actions.OfType<HutongGames.PlayMaker.Actions.CreateObject>())
                        {
                            if (!make.position.IsNone) continue;
                            var options = new List<GameObject>();
                            if (make.gameObject.Value != null) options.Add(make.gameObject.Value);
                            else if (make.gameObject.UseVariable)
                            {
                                foreach (var pick in state.Actions.OfType<HutongGames.PlayMaker.Actions.ArrayGetRandom>())
                                {
                                    var array = pick.array.UseVariable ? fsm.FsmVariables.GetFsmArray(pick.array.Name) : pick.array;
                                    if (array?.Values != null) options.AddRange(array.Values.OfType<GameObject>());
                                }
                            }
                            if (options.Count == 0) continue;
                            var at = make.spawnPoint.Value != null ? make.spawnPoint.Value.transform : fsm.transform;
                            if (at == set.transform || at.GetComponentsInChildren<PlayMakerFSM>(true).Any(f => f.transform != at)) continue;
                            if (!points.TryGetValue(at, out var entry)) points[at] = entry = (new List<GameObject>(), set.name);
                            foreach (var option in options) if (!entry.makes.Contains(option)) entry.makes.Add(option);
                        }
                    }
                }
            }
            var spots = new List<Spot>();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var pair in points)
                {
                    var at = pair.Key;
                    if (!Extents(pair.Value.makes, at, preview, out var lo, out var hi))
                    {
                        lo = at.position - new Vector3(0.15f, 0f, 0.15f);
                        hi = at.position + new Vector3(0.15f, 0.3f, 0.15f);
                    }
                    bool hung = pair.Value.makes.Any(p => p.GetComponentInChildren<WallHungProp>(true) != null);
                    bool falls = !hung && pair.Value.makes.Any(p => p.GetComponentsInChildren<Rigidbody>(true).Any(b => !b.isKinematic));
                    spots.Add(new Spot { at = at, set = pair.Value.set, what = pair.Value.makes[0].name, lo = lo - roomSpot.position, hi = hi - roomSpot.position, falls = falls, hung = hung });
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            return spots;
        }

        private static List<string> FitSet(string set, List<Spot> list, bool play)
        {
            var moved = new List<string>();
            int n = list.Count;
            // Living room sets: only what stands in or against the living room (some of their pieces wait outside its
            // walls for the bigger playroom, e.g. the corner webs at 5.6 m); playroom sets: the whole cross.
            float reach = play ? (Square / 2f + Wall + 3f) * PlayScale + 1f : Room / 2f + 0.3f;
            var stays = list.Select(s =>
            {
                var middle = (s.lo + s.hi) / 2f;
                return Mathf.Abs(middle.x) > reach || Mathf.Abs(middle.z) > reach || s.hi.x - s.lo.x > 8f || s.hi.z - s.lo.z > 8f
                       || s.hi.y < 0.05f || (s.lo.y < 0.02f && s.hi.y - s.lo.y < 0.06f);
            }).ToArray();
            var group = Enumerable.Range(0, n).ToArray();
            int Find(int i)
            {
                while (group[i] != i) i = group[i] = group[group[i]];
                return i;
            }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (!stays[i] && !stays[j] && Touch(list[i].lo, list[i].hi, list[j].lo, list[j].hi, 0.03f)) group[Find(i)] = Find(j);
            var shift = new Dictionary<int, Vector3>();
            for (int pass = 0; pass < 30; pass++)
            {
                shift.Clear();
                var lo = new Dictionary<int, Vector3>();
                var hi = new Dictionary<int, Vector3>();
                var hungGroup = new HashSet<int>();
                for (int i = 0; i < n; i++)
                {
                    if (stays[i]) continue;
                    int g = Find(i);
                    lo[g] = lo.TryGetValue(g, out var l) ? Vector3.Min(l, list[i].lo) : list[i].lo;
                    hi[g] = hi.TryGetValue(g, out var h) ? Vector3.Max(h, list[i].hi) : list[i].hi;
                    if (list[i].hung) hungGroup.Add(g);
                }
                foreach (var g in lo.Keys) shift[g] = play ? CrossShift(lo[g], hi[g]) : SquareShift(lo[g], hi[g], hungGroup.Contains(g));
                bool joined = false;
                for (int i = 0; i < n; i++)
                {
                    if (stays[i]) continue;
                    for (int j = i + 1; j < n; j++)
                    {
                        if (stays[j] || Find(i) == Find(j)) continue;
                        Vector3 di = shift[Find(i)], dj = shift[Find(j)];
                        if (di == dj || !Touch(list[i].lo + di, list[i].hi + di, list[j].lo + dj, list[j].hi + dj, -0.005f)) continue;
                        group[Find(i)] = Find(j);
                        joined = true;
                    }
                }
                if (!joined) break;
            }
            float ceiling = Height * (play ? PlayScale : 1f) - 0.03f;
            for (int i = 0; i < n; i++)
            {
                if (stays[i] || !shift.TryGetValue(Find(i), out var d)) continue;
                if (list[i].falls && list[i].hi.y > ceiling && list[i].lo.y > 0.05f) d.y = Mathf.Max(ceiling - list[i].hi.y, -list[i].lo.y);
                if (d.sqrMagnitude < 0.005f * 0.005f) continue;
                var at = list[i].at;
                Undo.RecordObject(at, "Fit furniture to the kit room");
                at.position += d;
                EditorUtility.SetDirty(at);
                moved.Add($"{set}/{at.name} ({list[i].what}) by {d.x:0.00}, {d.y:0.00}, {d.z:0.00}");
            }
            return moved;
        }

        // The living room (1x): in from a wall it pokes into; a hung group close to a wall (it hung on the old wall,
        // 0.23 m further in) out to touch it.
        private static Vector3 SquareShift(Vector3 lo, Vector3 hi, bool hung)
        {
            float limit = Room / 2f - 0.01f;
            var d = new Vector3(Clamp(lo.x, hi.x, -limit, limit), 0f, Clamp(lo.z, hi.z, -limit, limit));
            if (d != Vector3.zero || !hung) return d;
            float east = limit - hi.x, west = lo.x + limit, north = limit - hi.z, south = lo.z + limit;
            float near = Mathf.Min(Mathf.Min(east, west), Mathf.Min(north, south));
            if (near < 0.02f || near > 0.6f) return d;
            if (near == east) d.x = east;
            else if (near == west) d.x = -west;
            else if (near == north) d.z = north;
            else d.z = -south;
            return d;
        }

        // The playroom (1.5x): the smallest move that puts the group inside the square or inside one arm.
        private static Vector3 CrossShift(Vector3 lo, Vector3 hi)
        {
            float c = Square / 2f * PlayScale - 0.01f, w = ArmHalf * PlayScale - 0.01f, e = (Square / 2f + Wall + 3f) * PlayScale - 0.01f;
            var regions = new[] { (-c, c, -c, c), (-w, w, -e, e), (-e, e, -w, w) };
            Vector3 best = Vector3.zero;
            float bestLength = float.MaxValue;
            foreach (var (x0, x1, z0, z1) in regions)
            {
                if (hi.x - lo.x > x1 - x0 || hi.z - lo.z > z1 - z0) continue;
                var d = new Vector3(Clamp(lo.x, hi.x, x0, x1), 0f, Clamp(lo.z, hi.z, z0, z1));
                if (d == Vector3.zero) return d;
                if (d.magnitude < bestLength)
                {
                    bestLength = d.magnitude;
                    best = d;
                }
            }
            return best;
        }

        private static float Clamp(float lo, float hi, float min, float max) => lo < min ? min - lo : hi > max ? max - hi : 0f;

        private static bool Touch(Vector3 aLo, Vector3 aHi, Vector3 bLo, Vector3 bHi, float margin) =>
            aLo.x - margin <= bHi.x && bLo.x - margin <= aHi.x &&
            aLo.y - margin <= bHi.y && bLo.y - margin <= aHi.y &&
            aLo.z - margin <= bHi.z && bLo.z - margin <= aHi.z;

        private static bool Extents(List<GameObject> prefabs, Transform at, UnityEngine.SceneManagement.Scene preview, out Vector3 lo, out Vector3 hi)
        {
            lo = Vector3.positiveInfinity;
            hi = Vector3.negativeInfinity;
            foreach (var prefab in prefabs)
            {
                var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                made.transform.SetPositionAndRotation(at.position, at.rotation);
                Vector3 ownLo = Vector3.positiveInfinity, ownHi = Vector3.negativeInfinity;
                Reach(made, ref ownLo, ref ownHi);
                lo = Vector3.Min(lo, ownLo);
                hi = Vector3.Max(hi, ownHi);
                // A pack of spawn points with no look of its own (a playroom gate of Jenga blocks): what its points
                // make, where they make it. (Pieces that have a look keep it: their own spawns are what they leave
                // when broken.)
                foreach (var fsm in ownLo.x <= ownHi.x ? new PlayMakerFSM[0] : made.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (fsm.gameObject == made) continue;
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.FsmStates)
                    {
                        state.LoadActions();
                        foreach (var make in state.Actions.OfType<HutongGames.PlayMaker.Actions.CreateObject>())
                        {
                            if (make.gameObject.Value == null || make.spawnPoint.Value != null && make.spawnPoint.Value != fsm.gameObject) continue;
                            var inner = (GameObject)PrefabUtility.InstantiatePrefab(make.gameObject.Value, preview);
                            inner.transform.SetPositionAndRotation(fsm.transform.position, fsm.transform.rotation);
                            Reach(inner, ref lo, ref hi);
                            Object.DestroyImmediate(inner);
                        }
                    }
                }
                Object.DestroyImmediate(made);
            }
            return lo.x <= hi.x;
        }

        private static void Reach(GameObject made, ref Vector3 lo, ref Vector3 hi)
        {
            foreach (var r in made.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : (r.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null);
                if (mesh == null) continue;
                var m = r.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    lo = Vector3.Min(lo, p);
                    hi = Vector3.Max(hi, p);
                }
            }
        }
    }
}
