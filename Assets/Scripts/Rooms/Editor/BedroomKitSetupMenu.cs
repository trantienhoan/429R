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
    /// The bedroom kit room (user, 2026-11-19: build the new kit room with the old rooms' comings and goings).
    /// - Tools > 429 Game > Rooms > Build Bedroom Kit Room: Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab from the kit
    ///   (Assets/Models/Rooms/Bedroom_Kit.fbx, exported from Exports/Remodel/Bedroom_Kit.blend):
    ///   - the floor (9 x 9 m of kit tiles, never animated) and two sets on it, Mini and Big, each 7.5 x 7.5 m inside:
    ///     walls, corner posts, windows, wide doorways with their doors (Mini: the secret shortcut door; Big: the locked
    ///     W/C door and the chained horseshoe door), and a roof (the old slab, trim ring and lamp, scaled to the room);
    ///   - copied from the old room (Bedroom_Base_Z) so the game works as before: its Info FSM (it registers the room as
    ///     the global Bedroom_Base), Drop Wall Hung Props, the floor trigger, the fall edges, the teleport area, the room
    ///     light at the old path (Bedroom_Base_Z_Armature/Wall02/Rooftop/Point Light, which GAMESTAGES switches), and both
    ///     doors with all their parts (health, damage, wobble, break pieces, the shortcut's coin toss), on the kit's leaves;
    ///   - an Animator (Bedroom_Kit_Animator) with the old state names, each playing the Room Kit's come or go;
    ///   - (2026-11-20) the secret room behind the big room's chained door: a third set, Secret, north of it (the Secret
    ///     Room component opens it when that door breaks), with the giant pumpkin and Giant Spider of Secret Room Setup.
    /// - Use Bedroom Kit Room / Use Old Bedroom Room: which room the stage's Bedroom_Base_Pos spawns (the open scene is
    ///   marked changed, not saved). Use Bedroom Kit Room also moves the furniture spawn points whose pieces would poke
    ///   into the kit walls or ceiling in, just enough (the old mini room was 8.1 x 7.7 m, both old rooms 3.58 m high).
    /// </summary>
    public static class BedroomKitSetupMenu
    {
        private const string KitFbx = "Assets/Models/Rooms/Bedroom_Kit.fbx";
        private const string OldRoom = "Assets/Prefabs/Bases/Bedroom_Base_Z.prefab";
        private const string NewRoom = "Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab";
        private const string ControllerPath = "Assets/Models/Animator/Bedroom_Kit_Animator.controller";
        private const string SpawnName = "Bedroom_Base_Pos";
        private const string LightPath = "Bedroom_Base_Z_Armature/Wall02/Rooftop/Point Light";
        private const string SecretDoorPath = "Bedroom_Base_Z_Armature/Floor/Small_Room_Door/Bedroom_Base_Z_Door";
        private const string LockedDoorPath = "Bedroom_Base_Z_Armature/outline/Bedroom_Base_Z_Door";
        private const string ChainedDoorPath = "Bedroom_Base_Z_Armature/outline/Bedroom_Base_Z_Door 2";
        private const float Room = 7.5f, Wall = 0.345f, Height = 3f;
        private const float OldRoofSlab = 6.05f, OldRoofRim = 5.55f;

        private static readonly Dictionary<string, string> MaterialFor = new()
        {
            { "Bedroom_Wall", "Assets/Materials/Bedroom_Wall_Spooky.mat" },
            { "Bedroom_Wood_Floor", "Assets/Materials/Wood_Floor_Mat 1.mat" },
            { "Bedroom_Metal", "Assets/Materials/Bedroom_Metal_Mat.mat" },
            { "Bedroom_Furniture_Set1", "Assets/Materials/Bedroom_Set_1_Mat.mat" },
        };

        // Each set's four sides (North +Z, East +X, South -Z, West -X), pieces left to right as seen from inside. Where
        // the old rooms had them (sampled at the end of their come clips): the secret door on the small room's +Z wall
        // near x -3 (its -Z wall had a vent near x 3.6: a window here); the big room's W/C door on its -X wall near
        // z -1.7, its windows on the +X wall near z -1 and the -Z wall near x 1.3, and its second locked door (the
        // horseshoe one with the chain) on its +Z wall near x -1.4.
        private static readonly string[][] MiniSides =
        {
            new[] { "BR_Kit_Door_Wide+Secret", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m" },
            new[] { "BR_Kit_Wall_3m", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m" },
            new[] { "BR_Kit_Window", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m" },
            new[] { "BR_Kit_Wall_3m", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m" },
        };
        // The chained door (user, 2026-11-20: "move this door to the left, so the door and the welcome mat [are] closer to
        // the wall hang"): half a grid step west of where it was, its doorway centred at x -1.5, 0.15 m from the hung
        // shelf (x -2.94). Half-width (0.75 m) wall pieces fill the two ends. Behind it is the secret room.
        private static readonly string[][] BigSides =
        {
            new[] { HalfWall, "BR_Kit_Door_Wide+Chained", "BR_Kit_Wall_3m", HalfWall },
            new[] { "BR_Kit_Wall_3m", "BR_Kit_Window", "BR_Kit_Wall_1p5m" },
            new[] { "BR_Kit_Wall_1p5m", "BR_Kit_Window", "BR_Kit_Wall_3m" },
            new[] { "BR_Kit_Door_Wide+Locked", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m" },
        };
        // The secret room behind the chained door (user, 2026-11-20: "a new room almost mirrors that bedroom, same in size
        // and same with light but don't have the 2nd door and window"): north of the big room, sharing its north wall.
        // Its sides as above (its south side is the big room's north wall: none of its own).
        private static readonly string[][] SecretSides =
        {
            new[] { "BR_Kit_Wall_3m", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m" },
            new[] { "BR_Kit_Wall_3m", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m" },
            null,
            new[] { "BR_Kit_Wall_3m", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m" },
        };
        // Its middle: one room and one wall north of the big room's middle.
        private const float SecretZ = Room + Wall;
        // A half 1.5 m wall, cut from it (its wallpaper not squashed), saved next to the kit.
        private const string HalfWall = "BR_Kit_Wall_0p75m";
        private const string HalfWallPath = "Assets/Models/Rooms/BR_Kit_Wall_0p75m.asset";

        // Into the room, and the corner a side starts at (it runs along Cross(inside, up) from there).
        private static readonly (Vector3 inside, Vector3 start)[] Sides =
        {
            (Vector3.back, new Vector3(-Room / 2, 0f, Room / 2)),
            (Vector3.left, new Vector3(Room / 2, 0f, Room / 2)),
            (Vector3.forward, new Vector3(Room / 2, 0f, -Room / 2)),
            (Vector3.right, new Vector3(-Room / 2, 0f, -Room / 2)),
        };

        // The bedroom stages' furniture sets (under -GAMESTAGES), placed for the old rooms (the mini one was 8.1 x 7.7 m
        // inside, both 3.58 m high). The kit rooms are 7.5 x 7.5 m and 3 m high. Spawn points further out than FloorEdge
        // aren't in the room.
        private static readonly string[] FurnitureSets =
            { "--MiniBedroom--", "--MiniBedroom_Chaos--", "--MiniBedroom_Pumpkins--", "--Bedroom--", "--Bedroom_Chaos--", "--Bedroom_Pumpkins--" };
        private const float FloorEdge = 4.6f;

        private static Dictionary<string, Mesh> meshes;
        private static Dictionary<string, Material[]> materials;
        private static GameObject old;
        private static GameObject floorTrigger;
        // The secret door's leaf lost its chain and padlock (user, 2026-11-19: "so it looks more breakable"), so its break
        // pieces hide theirs. They still fall and turn into pumpkins like the rest, so the door gives as many pumpkins.
        private const string DropPath = "Assets/Prefabs/Items/Broken_Stuffs/Door_Break_Drop.prefab";
        private const string DropNoChainPath = "Assets/Prefabs/Items/Broken_Stuffs/Door_Break_Drop_No_Chain.prefab";
        private static readonly string[] ChainPieces =
            { "Bedroom_Base_Z_Door.001", "Bedroom_Base_Z_Door.002", "Bedroom_Base_Z_Door.003", "Bedroom_Base_Z_Door.004", "Bedroom_Base_Z_Door.010" };
        // The kit's wide doorway piece is this wide; its door leaf (the old door model) is centred in it.
        private const float Doorway = 3f;

        [MenuItem("Tools/429 Game/Rooms/Build Bedroom Kit Room")]
        public static void Build()
        {
            old = AssetDatabase.LoadAssetAtPath<GameObject>(OldRoom);
            if (!LoadKit(out string problem) || old == null)
            {
                Debug.LogError("[Bedroom Kit] " + (old == null ? "No " + OldRoom : problem) + ". Nothing made.");
                return;
            }
            var controller = MakeController();
            var scene = EditorSceneManager.NewPreviewScene();
            var notes = new List<string>();
            try
            {
                var root = new GameObject("Bedroom_Kit_Base");
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
                // Its own Animator, not the old room's.
                if (root.TryGetComponent(out DropWallHungProps dropper))
                {
                    var dso = new SerializedObject(dropper);
                    dso.FindProperty("animator").objectReferenceValue = animator;
                    dso.ApplyModifiedPropertiesWithoutUndo();
                }

                // What the old room had besides walls: the same places.
                foreach (var name in new[] { "Teleport Area", "Fall Edges" })
                {
                    var src = old.transform.Find(name);
                    if (src == null) continue;
                    var copy = Object.Instantiate(src.gameObject, root.transform);
                    copy.name = name;
                    copy.transform.SetPositionAndRotation(src.position, src.rotation);
                    notes.Add(name);
                }
                var oldFloor = old.transform.Find("Bedroom_Base_Z_Armature/Floor");
                floorTrigger = null;
                if (oldFloor != null)
                {
                    var trigger = new GameObject("Floor Trigger");
                    floorTrigger = trigger;
                    trigger.transform.SetParent(root.transform, false);
                    trigger.transform.SetPositionAndRotation(oldFloor.position, oldFloor.rotation);
                    trigger.layer = oldFloor.gameObject.layer;
                    foreach (var c in oldFloor.GetComponents<Component>())
                    {
                        if (c is Transform) continue;
                        ComponentUtility.CopyComponent(c);
                        ComponentUtility.PasteComponentAsNew(trigger);
                    }
                    notes.Add("floor trigger");
                }

                // The floor: 3 x 3 kit tiles, 9 x 9 m (the old floor was 9.08).
                var floor = Child(root.transform, "Floor", root.layer);
                foreach (float x in new[] { -1.5f, 1.5f, 4.5f })
                    foreach (float z in new[] { -1.5f, 1.5f, 4.5f })
                        Piece("BR_Kit_Floor_3m", floor, new Vector3(x, 0f, z), Quaternion.identity);

                // The room light, where GAMESTAGES looks for it.
                var oldLight = old.transform.Find(LightPath);
                var lightParent = Child(Child(Child(root.transform, "Bedroom_Base_Z_Armature", root.layer), "Wall02", root.layer), "Rooftop", root.layer);
                if (oldLight != null)
                {
                    var lamp = Object.Instantiate(oldLight.gameObject, lightParent);
                    lamp.name = "Point Light";
                    lamp.transform.localPosition = new Vector3(0f, Height - 0.3f, 0f);
                    lamp.transform.localRotation = oldLight.localRotation;
                    notes.Add("room light");
                }

                var mini = MakeSet(root.transform, "Mini", MiniSides, notes);
                var big = MakeSet(root.transform, "Big", BigSides, notes);
                var secret = MakeSecret(root.transform, big.root, lightParent.Find("Point Light"), kit, notes);

                var so = new SerializedObject(kit);
                var sets = so.FindProperty("sets");
                sets.arraySize = 3;
                SetEntry(sets.GetArrayElementAtIndex(0), "Mini", mini);
                SetEntry(sets.GetArrayElementAtIndex(1), "Big", big);
                SetEntry(sets.GetArrayElementAtIndex(2), "Secret", secret);
                var puffGuid = AssetDatabase.FindAssets("Smoke_Flash_Small_Puff t:Prefab").FirstOrDefault();
                if (puffGuid != null)
                {
                    so.FindProperty("puff").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(puffGuid));
                    notes.Add("puff " + AssetDatabase.GUIDToAssetPath(puffGuid));
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, NewRoom);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log($"[Bedroom Kit] {NewRoom} built (floor 9 x 9 m; Mini and Big sets 7.5 x 7.5 m), with {string.Join(", ", notes)}. " +
                      "Tools > 429 Game > Rooms > Use Bedroom Kit Room puts it in the bedroom stages.");
        }

        [MenuItem("Tools/429 Game/Rooms/Build Bedroom Kit Room", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/429 Game/Rooms/Use Bedroom Kit Room")]
        public static void UseKit() => Swap(NewRoom);

        [MenuItem("Tools/429 Game/Rooms/Use Old Bedroom Room")]
        public static void UseOld() => Swap(OldRoom);

        [MenuItem("Tools/429 Game/Rooms/Use Bedroom Kit Room", true)]
        [MenuItem("Tools/429 Game/Rooms/Use Old Bedroom Room", true)]
        private static bool CanSwap() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---------------------------------------------------------------------------------------------------------

        private static bool LoadKit(out string problem)
        {
            meshes = AssetDatabase.LoadAllAssetsAtPath(KitFbx).OfType<Mesh>().ToDictionary(m => m.name);
            materials = new Dictionary<string, Material[]>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KitFbx);
            if (model == null || meshes.Count == 0)
            {
                problem = "No kit in " + KitFbx + " (export it from Exports/Remodel/Bedroom_Kit.blend)";
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
            if (meshes.TryGetValue("BR_Kit_Wall_1p5m", out var full))
            {
                meshes[HalfWall] = MakeHalfWall(full);
                if (materials.TryGetValue("BR_Kit_Wall_1p5m", out var looks)) materials[HalfWall] = looks;
            }
            problem = null;
            return true;
        }

        private static AnimatorController MakeController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var start = machine.AddState("START");
            machine.defaultState = start;
            // The old room's states and times: mini come 2.67 s, big come 2.67 s at speed 0.666 = 4.0 s, go 3.33 s.
            State(machine, "Bedroom_Mini_COME", "Mini", true, 2.67f);
            State(machine, "Bedroom_Mini_GO", "Mini", false, 3.33f);
            State(machine, "Bedroom_COME", "Big", true, 4.0f);
            State(machine, "Bedroom_GO", "Big", false, 3.33f);
            // Old states nothing plays now: kept so a stray Animator Play finds them.
            foreach (var name in new[] { "Bedroom_Mini_GO2", "Bedroom_GO2", "Bedroom_Base_Z_Setup" }) machine.AddState(name);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void State(AnimatorStateMachine machine, string name, string set, bool come, float seconds)
        {
            var state = machine.AddState(name);
            var b = state.AddStateMachineBehaviour<RoomKitState>();
            b.set = set;
            b.come = come;
            b.seconds = seconds;
            EditorUtility.SetDirty(b);
        }

        private static (Transform root, Transform roof) MakeSet(Transform room, string name, string[][] sides, List<string> notes)
        {
            var set = Child(room, name, room.gameObject.layer);
            for (int i = 0; i < sides.Length; i++)
            {
                var (inside, start) = Sides[i];
                var turn = Quaternion.LookRotation(inside, Vector3.up);
                var run = Vector3.Cross(inside, Vector3.up);
                Solid(Piece("BR_Kit_Corner", set, start, turn), null);
                float along = 0f;
                foreach (var entry in sides[i])
                {
                    var names = entry.Split('+');
                    var part = Piece(names[0], set, start + run * along, turn);
                    Solid(part, names[0]);
                    if (names.Length > 1) Door(part, names[1], notes);
                    along += Length(names[0]);
                }
            }

            // The roof: the old slab (covering the walls), its trim ring (along the walls) and the lamp, as one part.
            var roof = Child(set, "Roof", room.gameObject.layer);
            float slab = (Room + 2f * Wall) / OldRoofSlab, ring = Room / OldRoofRim;
            var top = Piece("BR_Kit_Roof", roof, Vector3.zero, Quaternion.identity);
            top.transform.localScale = new Vector3(slab, 1f, slab);
            top.AddComponent<BoxCollider>();
            Piece("BR_Kit_Roof_Rim", roof, Vector3.zero, Quaternion.identity).transform.localScale = new Vector3(ring, 1f, ring);
            Piece("BR_Kit_Lamp", roof, Vector3.zero, Quaternion.identity);
            return (set, roof);
        }

        private static float Length(string piece) => piece == HalfWall ? 0.75f : piece == "BR_Kit_Wall_1p5m" ? 1.5f : 3f;

        // The secret room: its set (walls, corner posts, roof with its lamp) north of the big room, and, apart from the
        // set (the Secret Room shows them), its floor, its teleport area and its room light; then the Secret Room itself.
        private static (Transform root, Transform roof) MakeSecret(Transform room, Transform big, Transform roomLight, RoomKit kit, List<string> notes)
        {
            int layer = room.gameObject.layer;
            var set = Child(room, "Secret", layer);
            set.localPosition = new Vector3(0f, 0f, SecretZ);
            for (int i = 0; i < Sides.Length; i++)
            {
                var (inside, start) = Sides[i];
                var turn = Quaternion.LookRotation(inside, Vector3.up);
                var run = Vector3.Cross(inside, Vector3.up);
                // Its four inner corner posts (the south ones stand against the big room's north wall).
                Solid(Piece("BR_Kit_Corner", set, start, turn), null);
                if (SecretSides[i] == null) continue;
                float along = 0f;
                foreach (var piece in SecretSides[i])
                {
                    Solid(Piece(piece, set, start + run * along, turn), piece);
                    along += Length(piece);
                }
            }
            // Its roof: the bedroom's, not reaching over the big room's wall (that one has its own roof over it).
            var roof = Child(set, "Roof", layer);
            float slabX = (Room + 2f * Wall) / OldRoofSlab, slabZ = (Room + Wall) / OldRoofSlab, ring = Room / OldRoofRim;
            var top = Piece("BR_Kit_Roof", roof, new Vector3(0f, 0f, Wall / 2f), Quaternion.identity);
            top.transform.localScale = new Vector3(slabX, 1f, slabZ);
            top.AddComponent<BoxCollider>();
            Piece("BR_Kit_Roof_Rim", roof, Vector3.zero, Quaternion.identity).transform.localScale = new Vector3(ring, 1f, ring);
            Piece("BR_Kit_Lamp", roof, Vector3.zero, Quaternion.identity);

            // Its floor: 3 x 3 more kit tiles north of the big room's (z 4.5 to 13.5).
            var floor = Child(room, "Secret_Floor", layer);
            foreach (float x in new[] { -1.5f, 1.5f, 4.5f })
                foreach (float z in new[] { 7.5f, 10.5f, 13.5f })
                    Piece("BR_Kit_Floor_3m", floor, new Vector3(x, 0f, z), Quaternion.identity);
            // Its teleport area: the big room's, moved over it.
            GameObject teleport = null;
            var bigTeleport = room.Find("Teleport Area");
            if (bigTeleport != null)
            {
                teleport = Object.Instantiate(bigTeleport.gameObject, room);
                teleport.name = "Secret Teleport Area";
                teleport.transform.localPosition = bigTeleport.localPosition + new Vector3(0f, 0f, 9f);
            }
            // Its light: the bedroom's lamp light, its own (GAMESTAGES' light switching is the bedroom's).
            Light secretLight = null;
            if (roomLight != null)
            {
                var copy = Object.Instantiate(roomLight.gameObject, room);
                copy.name = "Secret Room Light";
                copy.transform.localPosition = new Vector3(0f, Height - 0.3f, SecretZ);
                foreach (var fsm in copy.GetComponents<PlayMakerFSM>()) Object.DestroyImmediate(fsm);
                secretLight = copy.GetComponent<Light>();
            }

            var door = big.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Chained_Door");
            var secretRoom = room.gameObject.AddComponent<SecretRoom>();
            var so = new SerializedObject(secretRoom);
            so.FindProperty("kit").objectReferenceValue = kit;
            so.FindProperty("door").objectReferenceValue = door != null ? door.gameObject : null;
            so.FindProperty("doorwayObstacle").objectReferenceValue = door != null && door.parent != null ? door.parent.GetComponent<NavMeshObstacle>() : null;
            so.FindProperty("floor").objectReferenceValue = floor;
            so.FindProperty("teleportArea").objectReferenceValue = teleport;
            so.FindProperty("roomLight").objectReferenceValue = secretLight;
            so.FindProperty("inside").boundsValue = new Bounds(new Vector3(0f, Height / 2f, SecretZ), new Vector3(Room - 0.2f, Height, Room - 0.2f));
            so.FindProperty("fallEdgeNorth").objectReferenceValue = Edge(room, "Fall Edge North");
            so.FindProperty("fallEdgeEast").objectReferenceValue = Edge(room, "Fall Edge East");
            so.FindProperty("fallEdgeWest").objectReferenceValue = Edge(room, "Fall Edge West");
            so.FindProperty("newNorthEdge").floatValue = 13.5f + 0.042f;
            so.FindProperty("openPuff").objectReferenceValue = SecretRoomSetupMenu.Find("Smoke_Puff_Burst_Out");
            so.FindProperty("breakPuff").objectReferenceValue = SecretRoomSetupMenu.Find("Smoke_Flash_Five_Puffs");
            so.FindProperty("openSound").objectReferenceValue = SecretRoomSetupMenu.Sound("whoosh");
            so.FindProperty("candySound").objectReferenceValue = SecretRoomSetupMenu.Sound("Clear,_resonant_bell_1");
            so.FindProperty("spiderSound").objectReferenceValue = SecretRoomSetupMenu.Sound("creepy_laugh_1");
            so.FindProperty("giantPumpkin").objectReferenceValue = SecretRoomSetupMenu.GiantPumpkin(notes);
            so.FindProperty("giantSpider").objectReferenceValue = SecretRoomSetupMenu.GiantSpider(notes);
            so.FindProperty("pumpkinSpot").vector3Value = new Vector3(0f, 0f, SecretZ);
            so.FindProperty("brokenPieces").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SecretRoomSetupMenu.PiecesPath);
            var candies = SecretRoomSetupMenu.Candies();
            var list = so.FindProperty("candies");
            list.arraySize = candies.Length;
            for (int i = 0; i < candies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = candies[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            notes.Add("secret room behind the chained door" + (door == null ? " (NO chained door found)" : ""));
            return (set, roof);
        }

        private static BoxCollider Edge(Transform room, string name)
        {
            var t = room.Find("Fall Edges/" + name);
            return t != null ? t.GetComponent<BoxCollider>() : null;
        }

        // The 1.5 m wall cut to 0.75 m: its far end (x -1.5) moves to x -0.75 and the texture there goes half way, so the
        // wallpaper keeps its size. The piece only has vertices at its two ends. Saved as a mesh asset (made again each
        // build).
        private static Mesh MakeHalfWall(Mesh full)
        {
            var v = full.vertices;
            var n = full.normals;
            var uv = full.uv;
            for (int i = 0; i < v.Length; i++)
            {
                if (Mathf.Abs(v[i].x + 1.5f) > 0.001f) continue;
                int partner = -1;
                for (int j = 0; j < v.Length && partner < 0; j++)
                    if (Mathf.Abs(v[j].x) < 0.001f && Mathf.Abs(v[j].y - v[i].y) < 0.001f && Mathf.Abs(v[j].z - v[i].z) < 0.001f
                        && Vector3.Dot(n[j], n[i]) > 0.99f) partner = j;
                v[i].x = -0.75f;
                if (partner >= 0) uv[i] = (uv[i] + uv[partner]) / 2f;
            }
            var half = Object.Instantiate(full);
            half.name = HalfWall;
            half.vertices = v;
            half.uv = uv;
            half.RecalculateBounds();
            half.RecalculateTangents();
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(HalfWallPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(half, HalfWallPath);
                return half;
            }
            EditorUtility.CopySerialized(half, asset);
            asset.name = HalfWall;
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(half);
            return asset;
        }

        private static GameObject Piece(string mesh, Transform parent, Vector3 at, Quaternion turn)
        {
            var go = new GameObject(mesh);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(at, turn);
            go.AddComponent<MeshFilter>().sharedMesh = meshes[mesh];
            go.AddComponent<MeshRenderer>().sharedMaterials = materials.TryGetValue(mesh, out var m) ? m : new Material[0];
            return go;
        }

        // Walls block like the old ones: a kinematic body, box colliders (round a doorway: its sides and head) and a
        // carving NavMesh obstacle.
        private static void Solid(GameObject part, string kind)
        {
            var b = part.GetComponent<MeshFilter>().sharedMesh.bounds;
            var body = part.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            if (kind == "BR_Kit_Door_Wide" || kind == "BR_Kit_Door")
            {
                // The pieces run from x = 0 to -3 (Unity), the doorway centred, from the floor up to 2.085.
                float w = kind == "BR_Kit_Door_Wide" ? 1.407f : 1.113f, top = 2.085f;
                float a = -1.5f - w / 2f, c = -1.5f + w / 2f;
                Box(part, new Vector3(-3f, 0f, b.min.z), new Vector3(a, Height, b.max.z));
                Box(part, new Vector3(c, 0f, b.min.z), new Vector3(0f, Height, b.max.z));
                Box(part, new Vector3(a, top, b.min.z), new Vector3(c, Height, b.max.z));
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

        // The old room's door, all its parts, on the kit's leaf, standing in this doorway (they share its pivot).
        private static void Door(GameObject doorway, string kind, List<string> notes)
        {
            bool secret = kind == "Secret", chained = kind == "Chained";
            var src = old.transform.Find(secret ? SecretDoorPath : chained ? ChainedDoorPath : LockedDoorPath);
            if (!meshes.TryGetValue(secret ? "BR_Kit_Door_Secret" : chained ? "BR_Kit_Door_Chained" : "BR_Kit_Door_Locked", out var leaf))
            {
                notes.Add("no kit leaf for the " + kind + " door (export the kit again)");
                return;
            }
            if (src == null || leaf == null) return;
            var oldLook = src.Find("Jiggle/Door_Mesh");
            if (oldLook == null) oldLook = src;
            var oldLeaf = oldLook.GetComponent<MeshFilter>() != null ? oldLook.GetComponent<MeshFilter>().sharedMesh : null;
            if (oldLeaf == null) return;
            var door = Object.Instantiate(src.gameObject, doorway.transform);
            door.name = secret ? "Secret_Door" : chained ? "Chained_Door" : "Locked_Door";

            // The kit script (bedroom_kit.py, leaf_faces) turned the old door model half round and centred it in the
            // doorway, standing on the floor, the back of its planks just behind the frame: kit = half * old + shift, in
            // the old door's own frame (its root is the hinge, half way up). The leaf's back (min z here) is the user's
            // untouched planks; the front lost the chain.
            var half = Quaternion.Euler(0f, 180f, 0f);
            var ob = oldLeaf.bounds;
            ob.center += src.InverseTransformPoint(oldLook.position);
            var shift = new Vector3(-Doorway / 2f + ob.center.x, -ob.min.y, leaf.bounds.min.z + ob.max.z);
            // The secret door swings on its hinge and stands ajar as the old one did (its FSM slams it shut when the
            // player steps on the floor); the locked door never moves and keeps the doorway's pivot.
            var origin = secret ? shift : Vector3.zero;
            door.transform.localPosition = origin;
            door.transform.localRotation = secret ? half * (Quaternion.Inverse(old.transform.rotation) * src.rotation) * Quaternion.Inverse(half) : Quaternion.identity;
            door.transform.localScale = Vector3.one;
            // Its parts (wobble pivot, break pieces' spot, smoke) where they were on the old leaf.
            foreach (Transform child in door.transform)
            {
                var was = src.Find(child.name);
                if (was == null) continue;
                child.localPosition = shift + half * was.localPosition - origin;
                child.localRotation = half * was.localRotation;
            }
            var jiggle = door.transform.Find("Jiggle");
            if (jiggle != null)
            {
                jiggle.localRotation = Quaternion.identity;
                var look = jiggle.Find("Door_Mesh");
                if (look != null)
                {
                    // The kit leaf is in the doorway's frame.
                    look.localPosition = -origin - jiggle.localPosition;
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
                // The old box held the planks only (not the chain): same box, same place on the leaf.
                box.center = shift + half * oldBox.center - origin;
                box.size = oldBox.size;
            }
            FixDoorFsms(door, notes);
            if (secret) SwapDrop(door, NoChainDrop(), notes);
            notes.Add(door.name + " (old door's parts on the kit leaf)");
        }

        // A variant of the door's break pieces with the chain and padlock pieces unseen.
        private static GameObject NoChainDrop()
        {
            var drop = AssetDatabase.LoadAssetAtPath<GameObject>(DropPath);
            if (drop == null) return null;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(drop, scene);
                foreach (var name in ChainPieces)
                {
                    var piece = copy.transform.Find(name);
                    if (piece != null && piece.TryGetComponent(out MeshRenderer look)) look.enabled = false;
                }
                return PrefabUtility.SaveAsPrefabAsset(copy, DropNoChainPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // The door's break spot drops this instead of the shared break pieces.
        private static void SwapDrop(GameObject door, GameObject drop, List<string> notes)
        {
            var spot = door.transform.Find("Door_Break_Drop_Pos");
            if (spot == null || drop == null) return;
            foreach (var fsm in spot.GetComponents<PlayMakerFSM>())
            {
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                foreach (var state in fsm.FsmStates)
                {
                    state.LoadActions();
                    bool edit = false;
                    foreach (var make in state.Actions.OfType<HutongGames.PlayMaker.Actions.CreateObject>())
                    {
                        if (make.gameObject.Value == null || make.gameObject.Value.name != "Door_Break_Drop") continue;
                        make.gameObject.Value = drop;
                        edit = true;
                    }
                    if (!edit) continue;
                    state.SaveActions();
                    notes.Add(door.name + " drops " + drop.name);
                }
            }
        }

        // The old door's FSMs, made right for the kit room: its "player stepped in" trigger watches the kit's floor
        // trigger (it watched the old room's Floor), and "close door" turns it shut against its own wall (it set the
        // world rotation, right only for the old wall).
        private static void FixDoorFsms(GameObject door, List<string> notes)
        {
            foreach (var fsm in door.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                foreach (var state in fsm.FsmStates)
                {
                    state.LoadActions();
                    bool edit = false;
                    foreach (var a in state.Actions)
                    {
                        if (a is HutongGames.PlayMaker.Actions.TriggerEvent trigger && floorTrigger != null
                            && trigger.gameObject.OwnerOption == HutongGames.PlayMaker.OwnerDefaultOption.SpecifyGameObject
                            && trigger.gameObject.GameObject.Value != null && trigger.gameObject.GameObject.Value.name == "Floor")
                        {
                            trigger.gameObject.GameObject.Value = floorTrigger;
                            edit = true;
                            notes.Add(fsm.FsmName + " '" + state.Name + "' watches the kit floor trigger");
                        }
                        if (a is HutongGames.PlayMaker.Actions.SetRotation turn && turn.space == Space.World && fsm.gameObject == door
                            && turn.gameObject.OwnerOption == HutongGames.PlayMaker.OwnerDefaultOption.UseOwner)
                        {
                            turn.space = Space.Self;
                            edit = true;
                            notes.Add(fsm.FsmName + " '" + state.Name + "' closes against its own wall");
                        }
                    }
                    if (edit) state.SaveActions();
                }
            }
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

        // Which room prefab the bedroom stages' spawn point (Bedroom_Base_Pos) makes.
        private static void Swap(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var spawn = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == SpawnName && g.scene == scene && g.hideFlags == HideFlags.None);
            if (prefab == null || spawn == null)
            {
                Debug.LogError("[Bedroom Kit] " + (prefab == null ? "No " + prefabPath + " (build it first)" : "No " + SpawnName + " in " + scene.name) + ". Nothing changed.");
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
                    Undo.RecordObject(fsm, "Bedroom room");
                    state.SaveActions();
                    EditorUtility.SetDirty(fsm);
                    changed++;
                }
            }
            string fitted = "";
            if (prefabPath == NewRoom)
            {
                var moved = FitFurniture(scene, spawn.transform);
                fitted = moved.Count == 0
                    ? " The furniture already fits it."
                    : $" Moved {moved.Count} furniture spawn point(s) in to fit its walls and ceiling: {string.Join("; ", moved)}.";
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Bedroom Kit] {SpawnName} now makes {prefab.name} ({changed} action(s) changed).{fitted} Save the scene to keep it.");
        }

        // Moves every furniture spawn point whose piece would poke into a kit wall (inside faces at +-3.75 m from the room's
        // middle) in, just enough. Touching pieces move as one group, and a group pushes along what it would bump. A piece
        // that falls when made starts under the 3 m ceiling. Returns what moved.
        private static List<string> FitFurniture(UnityEngine.SceneManagement.Scene scene, Transform roomSpot)
        {
            var moved = new List<string>();
            if (Quaternion.Angle(roomSpot.rotation, Quaternion.identity) > 1f)
            {
                Debug.LogWarning("[Bedroom Kit] " + SpawnName + " is turned: the furniture was not checked against the kit walls.");
                return moved;
            }
            // What each spawn point makes (a point can make more than one thing).
            var points = new Dictionary<Transform, List<GameObject>>();
            var setOf = new Dictionary<Transform, string>();
            foreach (var set in Resources.FindObjectsOfTypeAll<GameObject>()
                         .Where(g => g.scene == scene && g.hideFlags == HideFlags.None && FurnitureSets.Contains(g.name)))
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
                            // What it makes: its prefab, or (a picture spot) one picked at random from the state's array.
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
                            // Only single spots: never a set or a group of spots (moving it would move them all).
                            if (at == set.transform || at.GetComponentsInChildren<PlayMakerFSM>(true).Any(f => f.transform != at)) continue;
                            if (!points.TryGetValue(at, out var list)) points[at] = list = new List<GameObject>();
                            foreach (var option in options) if (!list.Contains(option)) list.Add(option);
                            setOf[at] = set.name;
                        }
                    }
                }
            }
            // Where each spot's pieces reach (room space). A spot whose piece shows no mesh here (a random box) counts as a
            // 30 cm box on it.
            // Whether it falls when made (a free body, not a hung prop): only those are ever moved down.
            var spots = new List<(Transform at, string set, string what, Vector3 lo, Vector3 hi, bool falls)>();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var pair in points)
                {
                    var at = pair.Key;
                    if (!Extents(pair.Value, at, preview, out var lo, out var hi))
                    {
                        lo = at.position - new Vector3(0.15f, 0f, 0.15f);
                        hi = at.position + new Vector3(0.15f, 0.3f, 0.15f);
                    }
                    bool falls = pair.Value.Any(p => p.GetComponentInChildren<WallHungProp>(true) == null
                                                     && p.GetComponentsInChildren<Rigidbody>(true).Any(b => !b.isKinematic));
                    spots.Add((at, setOf[at], pair.Value[0].name, lo - roomSpot.position, hi - roomSpot.position, falls));
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            // Pieces that touch (a stack, boxes on a shelf, a picture over a shelf) move as one group. A group's move that
            // would bump another piece takes that piece's group along. Each set is the room's furniture on its own.
            foreach (var set in spots.Select(s => s.set).Distinct().ToList())
            {
                var list = spots.Where(s => s.set == set).ToList();
                int n = list.Count;
                // Off the floor, floor-wide (the mini game's boss area), flat on it (a rug) or under it: never moved.
                var stays = list.Select(s => !InRoom(s.lo, s.hi) || s.hi.x - s.lo.x > 6f || s.hi.z - s.lo.z > 6f || s.hi.y < 0.05f
                                             || (s.lo.y < 0.02f && s.hi.y - s.lo.y < 0.06f)).ToArray();
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
                    for (int i = 0; i < n; i++)
                    {
                        if (stays[i]) continue;
                        int g = Find(i);
                        lo[g] = lo.TryGetValue(g, out var l) ? Vector3.Min(l, list[i].lo) : list[i].lo;
                        hi[g] = hi.TryGetValue(g, out var h) ? Vector3.Max(h, list[i].hi) : list[i].hi;
                    }
                    foreach (var g in lo.Keys) shift[g] = FitShift(lo[g], hi[g]);
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
                for (int i = 0; i < n; i++)
                {
                    if (stays[i] || !shift.TryGetValue(Find(i), out var d)) continue;
                    if (list[i].at.name == DoorMatSpot) continue; // the door mat follows its door instead (below)
                    // A piece that falls when made (the Chaos sets drop things from the air) starts under the ceiling, so
                    // it can't be pushed up through the roof. Hung things may reach into the ceiling (a corner web).
                    float top = Height - 0.03f;
                    if (list[i].falls && list[i].hi.y > top && list[i].lo.y > 0.05f) d.y = Mathf.Max(top - list[i].hi.y, -list[i].lo.y);
                    if (d.sqrMagnitude < 0.005f * 0.005f) continue;
                    var at = list[i].at;
                    Undo.RecordObject(at, "Fit furniture to the kit room");
                    at.position += d;
                    EditorUtility.SetDirty(at);
                    moved.Add($"{set}/{at.name} ({list[i].what}) by {d.x:0.00}, {d.y:0.00}, {d.z:0.00}");
                }
            }
            moved.AddRange(FollowDoor(spots.Where(s => s.at.name == DoorMatSpot).Select(s => (s.at, s.set, s.lo, s.hi)).ToList(), roomSpot));
            return moved;
        }

        // The big room's welcome mat lay in front of its chained door. On the kit's 1.5 m grid that door stands about
        // 0.6 m further east, so the mats (the tidy set's and the Chaos set's) move with it: the tidy one ends up centred
        // on the door, the Chaos one keeps its place next to it. Running it again changes nothing.
        private const string DoorMatSpot = "Bedroom_Matt_2_Pos";

        private static List<string> FollowDoor(List<(Transform at, string set, Vector3 lo, Vector3 hi)> mats, Transform roomSpot)
        {
            var moved = new List<string>();
            var room = AssetDatabase.LoadAssetAtPath<GameObject>(NewRoom);
            var door = room != null ? room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Chained_Door") : null;
            var leaf = door != null && door.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
            var tidy = mats.FirstOrDefault(m => m.set == "--Bedroom--");
            if (leaf == null || tidy.at == null) return moved;
            float doorX = room.transform.InverseTransformPoint(door.TransformPoint(leaf.bounds.center)).x;
            float dx = doorX - (tidy.lo.x + tidy.hi.x) / 2f;
            if (Mathf.Abs(dx) < 0.02f) return moved;
            foreach (var mat in mats)
            {
                Undo.RecordObject(mat.at, "Fit furniture to the kit room");
                mat.at.position += new Vector3(dx, 0f, 0f);
                EditorUtility.SetDirty(mat.at);
                moved.Add($"{mat.set}/{mat.at.name} (the door mat follows the chained door) by {dx:0.00}, 0.00, 0.00");
            }
            return moved;
        }

        private static bool InRoom(Vector3 lo, Vector3 hi)
        {
            var middle = (lo + hi) / 2f;
            return Mathf.Abs(middle.x) <= FloorEdge && Mathf.Abs(middle.z) <= FloorEdge;
        }

        private static bool Touch(Vector3 aLo, Vector3 aHi, Vector3 bLo, Vector3 bHi, float margin) =>
            aLo.x - margin <= bHi.x && bLo.x - margin <= aHi.x &&
            aLo.y - margin <= bHi.y && bLo.y - margin <= aHi.y &&
            aLo.z - margin <= bHi.z && bLo.z - margin <= aHi.z;

        // How far a group (room space) moves to clear the kit walls: in from a wall it pokes into; nothing for a group too
        // big to fit.
        private static Vector3 FitShift(Vector3 lo, Vector3 hi)
        {
            float limit = Room / 2f - 0.01f;
            var d = Vector3.zero;
            if (hi.x - lo.x < 2f * limit)
            {
                if (hi.x > limit) d.x = limit - hi.x;
                else if (lo.x < -limit) d.x = -limit - lo.x;
            }
            if (hi.z - lo.z < 2f * limit)
            {
                if (hi.z > limit) d.z = limit - hi.z;
                else if (lo.z < -limit) d.z = -limit - lo.z;
            }
            return d;
        }

        // Where what a spawn point makes reaches, in the world: its meshes' own vertices, so a tilted piece (a corner web)
        // isn't over-measured.
        private static bool Extents(List<GameObject> prefabs, Transform at, UnityEngine.SceneManagement.Scene preview, out Vector3 lo, out Vector3 hi)
        {
            lo = Vector3.positiveInfinity;
            hi = Vector3.negativeInfinity;
            foreach (var prefab in prefabs)
            {
                var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                made.transform.SetPositionAndRotation(at.position, at.rotation);
                foreach (var r in made.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    var mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : (r.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null);
                    if (mesh == null) continue;
                    var m = r.transform.localToWorldMatrix;
                    foreach (var v in mesh.vertices)
                    {
                        var w = m.MultiplyPoint3x4(v);
                        lo = Vector3.Min(lo, w);
                        hi = Vector3.Max(hi, w);
                    }
                }
                Object.DestroyImmediate(made);
            }
            return lo.x <= hi.x;
        }
    }
}
