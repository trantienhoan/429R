using System.Collections.Generic;
using System.Linq;
using Game.Rooms;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Set Up Magic Hats + Pumpkin Rooms (user, 2026-11-06). Safe to run again.
    /// - The 3-hat coffer (Mini Bedroom, Bedroom) picks Chaos, Stuffs or the new Pumpkin room, a third each
    ///   (Random_Chaos_Stuffs_Pumpkin); the 2-hat Living Room table picks Chaos or Stuffs 50/50 (Random_Chaos_Stuffs_Half).
    ///   Random_Chaos_Stuffs itself (2 Stuffs : 1 Chaos) is left for Pumpkin_7's witch hat.
    /// - Skull_Pumpkin: Skull_Stuffs made a laughing pumpkin (pumpkin_evil, skull sized, laughs like the PlayGround's
    ///   laughing pumpkins, breaks into small pumpkin pieces); it fills the global Skull_Pumpkin (added to the globals).
    /// - The hats lose their aura once picked (Magic Hat Aura); the skulls pop up out of the hat's spot with a spin and the
    ///   magic hat sound, and the player near them breaks them (Skull Reveal).
    /// - In the open scene (2 Game Scene): the pumpkin rooms --MiniBedroom_Pumpkins-- and --Bedroom_Pumpkins-- (the stage
    ///   pumpkin's spot from the Stuffs room and a Pumpkin Room Fill: 35 / 45 Pumpkin_On_Tree raining in, refilled), and
    ///   GAMESTAGES: the Pumpkin Skull's touch ("pumpkin") in MiniBR_MagicTable and BR_Magic_Table, leading to Smoke P Wait
    ///   -> Mini Pumpkins -> Mini Enemy and SmokeP Wait 2 -> BR_Pumpkins -> Enemy1 - Wait (copies of the Stuffs states).
    ///   SAVE the scene afterwards.
    /// </summary>
    public static class MagicHatsSetupMenu
    {
        private const string SkullStuffs = "Assets/Prefabs/Items/Breakable/Skull_Stuffs.prefab";
        private const string SkullChaos = "Assets/Prefabs/Items/Breakable/Skull_Chaos.prefab";
        private const string SkullPumpkin = "Assets/Prefabs/Items/Breakable/Skull_Pumpkin.prefab";
        private const string Picker = "Assets/Prefabs/Random_Item/Random_Chaos_Stuffs.prefab";
        private const string PickerPumpkin = "Assets/Prefabs/Random_Item/Random_Chaos_Stuffs_Pumpkin.prefab";
        private const string PickerHalf = "Assets/Prefabs/Random_Item/Random_Chaos_Stuffs_Half.prefab";
        private const string Coffer = "Assets/Prefabs/BedRoom/Bedroom_Coffer+Magic_Hats.prefab";
        private const string LivingRoomTable = "Assets/Prefabs/BedRoom/Livingroom_Table_2+Magic_Hat.prefab";
        private static readonly string[] Hats = { "Assets/Prefabs/Items/Hat_Green.prefab", "Assets/Prefabs/Items/Hat_Black.prefab", "Assets/Prefabs/Items/Hat_Blue.prefab" };
        private const string PumpkinModel = "Assets/Models/Bedroom_Stuffs/pumpkin_evil.fbx";
        private const string PumpkinMesh = "pumpkin_evil";
        private const string PumpkinDrop = "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_evil_drop.prefab";
        private const string PumpkinDropSmall = "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_evil_drop_small.prefab";
        private const string Laugh = "Assets/Audio/SFX/laughing_pumpkin_1.wav";
        private const string RevealSound = "Assets/Audio/SFX/magic_hat_spit_card.wav";
        private const string Filler = "Assets/Prefabs/Items/Breakable/Pumpkin_On_Tree.prefab";
        // The skull is 0.31 m across, the pumpkin 0.56 m.
        private const float PumpkinSkullScale = 0.55f;
        private const string GlobalName = "Skull_Pumpkin";
        private const string PumpkinEvent = "pumpkin";

        private struct Room
        {
            public string source, name, marker, stagePumpkin, enemyManager, table, smokeFrom, smokeName, stuffsState, pumpkinState;
            public int count;
        }

        private static readonly Room[] Rooms =
        {
            new Room { source = "--MiniBedroom--", name = "--MiniBedroom_Pumpkins--", marker = "Pumpkin_1_Pos", stagePumpkin = "Pumpkin_1",
                enemyManager = "-EnemyManager 0", table = "MiniBR_MagicTable", smokeFrom = "Smoke Wait", smokeName = "Smoke P Wait",
                stuffsState = "Mini Stuffs", pumpkinState = "Mini Pumpkins", count = 35 },
            new Room { source = "--Bedroom--", name = "--Bedroom_Pumpkins--", marker = "Pumpkin_2_Random_Pos", stagePumpkin = "Pumpkin_2",
                enemyManager = "-EnemyManager 1", table = "BR_Magic_Table", smokeFrom = "Smoke Wait 2", smokeName = "SmokeP Wait 2",
                stuffsState = "BR_Stuffs", pumpkinState = "BR_Pumpkins", count = 45 },
        };

        [MenuItem("Tools/429 Game/Rooms/Set Up Magic Hats + Pumpkin Rooms")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Magic Hats] Stop play mode first. Nothing changed.");
                return;
            }
            var notes = Run();
            Debug.Log("[Magic Hats] " + string.Join("\n", notes));
            EditorUtility.DisplayDialog("Set Up Magic Hats + Pumpkin Rooms", string.Join("\n", notes.Take(30)), "OK");
        }

        public static List<string> Run()
        {
            var notes = new List<string>();
            AddGlobal(notes);
            MakeSmallDrop(notes);
            MakePumpkinSkull(notes);
            MakePicker(PickerPumpkin, new[] { SkullChaos, SkullStuffs, SkullPumpkin }, notes);
            MakePicker(PickerHalf, new[] { SkullChaos, SkullStuffs }, notes);
            SetUpTable(Coffer, PickerPumpkin, true, notes);
            SetUpTable(LivingRoomTable, PickerHalf, false, notes);
            foreach (var hat in Hats) AddHatAura(hat, notes);
            foreach (var skull in new[] { SkullChaos, SkullStuffs, SkullPumpkin }) AddReveal(skull, notes);
            AssetDatabase.SaveAssets();
            SetUpScene(notes);
            return notes;
        }

        // ---------- Assets ----------

        private static void AddGlobal(List<string> notes)
        {
            var globals = PlayMakerGlobals.Instance;
            if (globals.Variables.FindFsmGameObject(GlobalName) != null) return;
            globals.Variables.GameObjectVariables = globals.Variables.GameObjectVariables.Append(new FsmGameObject(GlobalName)).ToArray();
            EditorUtility.SetDirty(globals);
            notes.Add("Global variable " + GlobalName + " added.");
        }

        private static void MakeSmallDrop(List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PumpkinDropSmall) != null) return;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PumpkinDrop);
            if (source == null) { notes.Add("No " + PumpkinDrop + ": the pumpkin skull breaks into skull pieces."); return; }
            var made = (GameObject)PrefabUtility.InstantiatePrefab(source);
            made.transform.localScale = source.transform.localScale * PumpkinSkullScale;
            PrefabUtility.SaveAsPrefabAsset(made, PumpkinDropSmall);
            Object.DestroyImmediate(made);
            notes.Add("Made " + PumpkinDropSmall + " (pumpkin_evil_drop x" + PumpkinSkullScale + ").");
        }

        private static void MakePumpkinSkull(List<string> notes)
        {
            bool made = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SkullPumpkin) == null)
            {
                if (!AssetDatabase.CopyAsset(SkullStuffs, SkullPumpkin)) { notes.Add("Couldn't copy Skull_Stuffs to Skull_Pumpkin."); return; }
                made = true;
            }
            var mesh = AssetDatabase.LoadAllAssetsAtPath(PumpkinModel).OfType<Mesh>().FirstOrDefault(m => m.name == PumpkinMesh);
            var laugh = AssetDatabase.LoadAssetAtPath<AudioClip>(Laugh);
            var drop = AssetDatabase.LoadAssetAtPath<GameObject>(PumpkinDropSmall);
            var root = PrefabUtility.LoadPrefabContents(SkullPumpkin);
            try
            {
                root.name = "Skull_Pumpkin";
                if (mesh != null)
                {
                    if (root.TryGetComponent(out MeshFilter filter)) filter.sharedMesh = mesh;
                    if (root.TryGetComponent(out MeshCollider shape)) { shape.sharedMesh = mesh; shape.convex = true; }
                }
                root.transform.localScale = Vector3.one * PumpkinSkullScale;
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.FsmStates)
                    {
                        state.LoadActions();
                        bool changed = false;
                        foreach (var action in state.Actions)
                        {
                            if (action is GetOwner owner) { owner.storeGameObject = Global(); changed = true; }
                            else if (action is AudioPlay play && laugh != null && play.oneShotClip != null && !play.oneShotClip.UseVariable) { play.oneShotClip.Value = laugh; changed = true; }
                            else if (action is CreateObject create && drop != null && fsm.gameObject.name == "Skull_Drop_Pos" && !create.gameObject.UseVariable) { create.gameObject.Value = drop; changed = true; }
                        }
                        if (changed) state.SaveActions();
                    }
                    EditorUtility.SetDirty(fsm);
                }
                PrefabUtility.SaveAsPrefabAsset(root, SkullPumpkin);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            notes.Add((made ? "Made " : "Updated ") + SkullPumpkin + " (pumpkin_evil, x" + PumpkinSkullScale + ", laughs, small pumpkin pieces, fills " + GlobalName + ").");
        }

        private static FsmGameObject Global() => new FsmGameObject(GlobalName) { UseVariable = true };

        private static void MakePicker(string path, string[] skulls, List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null && !AssetDatabase.CopyAsset(Picker, path)) { notes.Add("Couldn't copy " + Picker); return; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = System.IO.Path.GetFileNameWithoutExtension(path);
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    // Straight into the FSM's saved variables (an FsmArray's own Save Changes doesn't reach the prefab).
                    var so = new SerializedObject(fsm);
                    var arrays = so.FindProperty("fsm.variables.arrayVariables");
                    if (arrays == null) continue;
                    for (int i = 0; i < arrays.arraySize; i++)
                    {
                        var array = arrays.GetArrayElementAtIndex(i);
                        if (array.FindPropertyRelative("name").stringValue != "StuffsArray") continue;
                        var refs = array.FindPropertyRelative("objectReferences");
                        refs.arraySize = skulls.Length;
                        for (int k = 0; k < skulls.Length; k++)
                            refs.GetArrayElementAtIndex(k).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(skulls[k]);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            notes.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + string.Join(" / ", skulls.Select(System.IO.Path.GetFileNameWithoutExtension)) + " (each " + (100 / skulls.Length) + "%).");
        }

        // The table's hats make this picker; the coffer also waits for the Pumpkin Skull's touch (it breaks the table).
        private static void SetUpTable(string path, string picker, bool pumpkin, List<string> notes)
        {
            var pick = AssetDatabase.LoadAssetAtPath<GameObject>(picker);
            var root = PrefabUtility.LoadPrefabContents(path);
            int states = 0;
            try
            {
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "Magic_Hat_GAME") continue;
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.FsmStates)
                    {
                        if (!state.Name.EndsWith("_Random_Stuffs")) continue;
                        state.LoadActions();
                        foreach (var create in state.Actions.OfType<CreateObject>()) if (!create.gameObject.UseVariable) create.gameObject.Value = pick;
                        if (pumpkin && !state.Actions.OfType<TriggerEvent>().Any(t => Watches(t, GlobalName)))
                            state.Actions = state.Actions.Append(Touch(GlobalName, "next")).ToArray();
                        state.SaveActions();
                        states++;
                    }
                    EditorUtility.SetDirty(fsm);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            notes.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + states + " hats make " + pick.name + (pumpkin ? ", the Pumpkin Skull's touch breaks it too" : "") + ".");
        }

        private static bool Watches(TriggerEvent trigger, string globalName) =>
            trigger.gameObject != null && trigger.gameObject.GameObject != null && trigger.gameObject.GameObject.UseVariable && trigger.gameObject.GameObject.Name == globalName;

        // The player walking into the global's object sends this event (like the skulls' other Trigger Events).
        private static TriggerEvent Touch(string globalName, string eventName)
        {
            var touch = new TriggerEvent();
            touch.Reset();
            touch.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject(globalName) { UseVariable = true } };
            touch.trigger = TriggerType.OnTriggerEnter;
            touch.collideTag = new FsmString { Value = "Player" };
            touch.sendEvent = FsmEvent.GetFsmEvent(eventName);
            touch.storeCollider = new FsmGameObject { UseVariable = true };
            return touch;
        }

        private static void AddHatAura(string path, List<string> notes)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<MagicHatAura>() != null) return;
                root.AddComponent<MagicHatAura>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.Add(root.name + ": loses its aura once picked.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AddReveal(string path, List<string> notes)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var reveal = root.GetComponent<SkullReveal>();
                bool fresh = reveal == null;
                if (fresh) reveal = root.AddComponent<SkullReveal>();
                var so = new SerializedObject(reveal);
                // No extra puff: the skull's own Smoke_Blast puffs already, and more smoke hid the pop.
                so.FindProperty("effects").arraySize = 0;
                if (so.FindProperty("sound").objectReferenceValue == null) so.FindProperty("sound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(RevealSound);
                var trigger = root.transform.Find("Trigger");
                if (trigger != null) so.FindProperty("touch").objectReferenceValue = trigger.GetComponent<SphereCollider>();
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                if (fresh) notes.Add(root.name + ": pops up out of the hat, the player near it breaks it.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---------- The scene ----------

        private static void SetUpScene(List<string> notes)
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES" && f.gameObject.scene.IsValid());
            if (gs == null) { notes.Add("No GAMESTAGES in the open scene: open 2 Game Scene and run it again for the pumpkin rooms."); return; }
            var f = gs.Fsm;
            if (!f.Initialized) f.Init(gs);
            Undo.RecordObject(gs, "Pumpkin Rooms");
            if (!f.Events.Any(e => e.Name == PumpkinEvent)) f.Events = f.Events.Append(FsmEvent.GetFsmEvent(PumpkinEvent)).ToArray();
            foreach (var room in Rooms)
            {
                var set = MakeSet(gs, room, notes);
                if (set == null) continue;
                Hook(f, room, set, notes);
            }
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            notes.Add("SAVE the scene to keep the pumpkin rooms and the GAMESTAGES changes.");
        }

        private static GameObject MakeSet(PlayMakerFSM gs, Room room, List<string> notes)
        {
            var source = gs.transform.Find(room.source);
            if (source == null) { notes.Add("No " + room.source + " under " + gs.name + "."); return null; }
            var setT = gs.transform.Find(room.name);
            GameObject set;
            if (setT == null)
            {
                set = new GameObject(room.name);
                set.transform.SetParent(gs.transform, false);
                set.transform.SetPositionAndRotation(source.position, source.rotation);
                set.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
                set.SetActive(false);
                // Like the Stuffs set, it switches itself off a moment after it came on (so it can come on again).
                var own = source.GetComponent<PlayMakerFSM>();
                if (own != null && UnityEditorInternal.ComponentUtility.CopyComponent(own)) UnityEditorInternal.ComponentUtility.PasteComponentAsNew(set);
                Undo.RegisterCreatedObjectUndo(set, "Pumpkin Rooms");
                notes.Add("Made " + room.name + ".");
            }
            else set = setT.gameObject;

            // The stage's laughing pumpkin, from the Stuffs room (the same spot or spots).
            if (set.transform.Find(room.marker) == null)
            {
                var marker = source.Find(room.marker);
                if (marker == null) notes.Add("No " + room.marker + " in " + room.source + ": no stage pumpkin in " + room.name + "!");
                else
                {
                    var copy = Object.Instantiate(marker.gameObject, set.transform);
                    copy.name = marker.name;
                    copy.transform.SetPositionAndRotation(marker.position, marker.rotation);
                }
            }

            // The fill: the floor's area from the Stuffs room's spots (its furniture stood all over the room).
            var fillT = set.transform.Find("Pumpkin_Room_Fill");
            if (fillT == null)
            {
                fillT = new GameObject("Pumpkin_Room_Fill").transform;
                fillT.SetParent(set.transform, false);
            }
            var fill = fillT.GetComponent<PumpkinRoomFill>() ?? fillT.gameObject.AddComponent<PumpkinRoomFill>();
            var spots = source.Cast<Transform>().Select(t => t.position).ToList();
            float floorY = spots.Count > 0 ? spots.Min(p => p.y) : source.position.y;
            var area = new Bounds(spots.Count > 0 ? spots[0] : source.position, Vector3.zero);
            foreach (var p in spots) area.Encapsulate(new Vector3(p.x, floorY, p.z));
            // A little inside the furniture's spots (they stand against the walls): drops stay clear of the walls.
            area.Expand(new Vector3(-0.3f, 0f, -0.3f));
            fillT.position = new Vector3(area.center.x, floorY, area.center.z);
            Transform em = null;
            foreach (var top in gs.gameObject.scene.GetRootGameObjects())
            {
                em = top.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == room.enemyManager);
                if (em != null) break;
            }
            var so = new SerializedObject(fill);
            so.FindProperty("fill").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Filler);
            so.FindProperty("startCount").intValue = room.count;
            so.FindProperty("refillBelow").intValue = 12;
            so.FindProperty("refillCount").intValue = 12;
            so.FindProperty("area").boundsValue = area;
            so.FindProperty("floorY").floatValue = floorY;
            so.FindProperty("stagePumpkin").stringValue = room.stagePumpkin;
            so.FindProperty("enemyManager").objectReferenceValue = em != null ? em.gameObject : null;
            so.FindProperty("fightState").stringValue = room.stuffsState == "Mini Stuffs" ? "Mini Enemy" : "Enemy1 - Wait";
            so.ApplyModifiedPropertiesWithoutUndo();
            notes.Add(room.name + ": " + room.count + " pumpkins over " + area.size.x.ToString("0.0") + " x " + area.size.z.ToString("0.0") + " m, refill 12 below 12" + (em == null ? ", NO " + room.enemyManager + " found" : "") + ".");
            return set;
        }

        // Table state: + the Pumpkin Skull's touch -> smoke state (Smoke Wait's copy, the Pumpkin Skull broken) -> room
        // state (the Stuffs state's copy, the pumpkin set on) -> the same fight.
        private static void Hook(Fsm f, Room room, GameObject set, List<string> notes)
        {
            var table = f.GetState(room.table);
            var smokeSource = f.GetState(room.smokeFrom);
            var stuffs = f.GetState(room.stuffsState);
            if (table == null || smokeSource == null || stuffs == null) { notes.Add("GAMESTAGES lacks " + room.table + " / " + room.smokeFrom + " / " + room.stuffsState + "."); return; }

            // The room state.
            var roomState = f.GetState(room.pumpkinState);
            if (roomState == null)
            {
                roomState = new FsmState(stuffs) { Name = room.pumpkinState, Position = Shifted(stuffs.Position, 0f, 70f) };
                roomState.LoadActions();
                foreach (var on in roomState.Actions.OfType<ActivateGameObject>()) on.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject { Value = set } };
                roomState.Transitions = stuffs.Transitions.Select(t => new FsmTransition { FsmEvent = t.FsmEvent, ToState = t.ToState }).ToArray();
                roomState.SaveActions();
                f.States = f.States.Append(roomState).ToArray();
                notes.Add("GAMESTAGES: '" + room.pumpkinState + "' (a copy of '" + room.stuffsState + "' with " + set.name + ").");
            }

            // The smoke state.
            var smoke = f.GetState(room.smokeName);
            if (smoke == null)
            {
                smoke = new FsmState(smokeSource) { Name = room.smokeName, Position = Shifted(smokeSource.Position, 0f, 70f) };
                smoke.LoadActions();
                foreach (var send in smoke.Actions.OfType<SendEvent>())
                    send.eventTarget.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = Global() };
                smoke.Transitions = new[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = room.pumpkinState } };
                smoke.SaveActions();
                f.States = f.States.Append(smoke).ToArray();
                notes.Add("GAMESTAGES: '" + room.smokeName + "' (breaks the Pumpkin Skull, then '" + room.pumpkinState + "').");
            }

            // The table state waits for the Pumpkin Skull's touch too.
            table.LoadActions();
            if (!table.Actions.OfType<TriggerEvent>().Any(t => Watches(t, GlobalName)))
            {
                table.Actions = table.Actions.Append(Touch(GlobalName, PumpkinEvent)).ToArray();
                table.SaveActions();
            }
            if (!table.Transitions.Any(t => t.EventName == PumpkinEvent))
                table.Transitions = table.Transitions.Append(new FsmTransition { FsmEvent = FsmEvent.GetFsmEvent(PumpkinEvent), ToState = room.smokeName }).ToArray();
            notes.Add("GAMESTAGES: '" + room.table + "' -> " + PumpkinEvent + " -> '" + room.smokeName + "'.");
        }

        private static Rect Shifted(Rect r, float x, float y) => new(r.x + x, r.y + y, r.width, r.height);
    }
}
