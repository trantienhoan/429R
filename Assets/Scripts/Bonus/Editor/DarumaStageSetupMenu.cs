using System.Collections.Generic;
using System.Linq;
using Game.Bonus;
using Game.Enemies;
using Game.Locomotion;
using Game.PlayGround;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bonus > Set Up Special Stage 3 (user, 2026-11-06): the Giant Slime's "red light, green light"
    /// bridge (Darume Stage). Builds:
    /// - Prefabs/Enemies/Daruma_Bomber: a fast Slime_Bomber (it has a bridge to cross);
    /// - Materials/Special_Stage_3/Stage3_Edge_Glow_Mat: the glowing edges;
    /// - Prefabs/Bonus/Special_Stage_3: a start platform walled at the back, a 1.4 m x 24 m bridge, the host's platform
    ///   with the Giant Slime (x3.5) at the far end, Fall Edges all round, lamps, the timer and sign texts, the music notes
    ///   it shows while singing, and the Darume Stage with its sounds (Audio/SFX/Special_Stage_3: synthesized song notes,
    ///   look, gotcha; whoosh, the miracle sound, cute_laugh, count_down) and prizes (200 EXP, 30 candies, Firework_Big);
    /// - Fire_Woods_n_Cauldron: Special Stage 3 Trigger (the mystery pot's break starts it; the stage, the seed a win
    ///   hands over, Items/Chest/The_Seed, and 1.2 s for the smoke).
    /// Safe to run again (it builds the stage anew and keeps the bomber's tuned values; the trigger's stage, seed and
    /// start delay are set again, its other values kept).
    /// Tools > 429 Game > Bonus > Hook Special Stage 3 To Cauldron (Hook) does only the last step, on the stage as it is
    /// (it never rebuilds Special_Stage_3).
    /// </summary>
    public static class DarumaStageSetupMenu
    {
        private const string StagePath = "Assets/Prefabs/Bonus/Special_Stage_3.prefab";
        private const string BomberPath = "Assets/Prefabs/Enemies/Daruma_Bomber.prefab";
        private const string MatFolder = "Assets/Materials/Special_Stage_3";
        private const string Cauldron = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string SeedPath = "Assets/Prefabs/Items/Chest/The_Seed.prefab";
        private const float StartDelay = 1.2f;
        private const string Sfx = "Assets/Audio/SFX/";

        private const float BridgeWidth = 1.4f;
        private const float BridgeStart = 1.5f;
        private const float BridgeEnd = 25.5f;
        private const float HostZ = 29f;
        private const float HostScale = 3.5f;

        [MenuItem("Tools/429 Game/Bonus/Set Up Special Stage 3")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Special Stage 3] Stop play mode first."); return; }
            Debug.Log("[Special Stage 3] " + string.Join("\n", Run()));
        }

        public static List<string> Run()
        {
            var notes = new List<string>();
            var bomber = MakeBomber(notes);
            var glow = MakeGlowMaterial();
            var stage = MakeStage(bomber, glow, notes);
            HookCauldron(stage, notes);
            AssetDatabase.SaveAssets();
            return notes;
        }

        [MenuItem("Tools/429 Game/Bonus/Hook Special Stage 3 To Cauldron")]
        public static void HookMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Hook Special Stage 3 To Cauldron] Stop play mode first."); return; }
            Hook();
        }

        /// <summary>
        /// Only the cauldron's Special Stage 3 Trigger: the stage (Special_Stage_3 as it is, never rebuilt), the seed
        /// (Items/Chest/The_Seed) and the 1.2 s start delay. Safe to run again.
        /// </summary>
        public static void Hook()
        {
            var notes = new List<string>();
            var prefab = Load<GameObject>(StagePath);
            var stage = prefab != null ? prefab.GetComponent<DarumaStage>() : null;
            if (stage == null) Debug.LogWarning("[Hook Special Stage 3 To Cauldron] No Daruma Stage at " + StagePath + " (run Set Up Special Stage 3 first): the trigger's stage is left as it is.");
            HookCauldron(stage, notes);
            AssetDatabase.SaveAssets();
            Debug.Log("[Hook Special Stage 3 To Cauldron] " + string.Join("; ", notes));
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static SlimeBomber MakeBomber(List<string> notes)
        {
            if (Load<GameObject>(BomberPath) == null)
            {
                var source = Load<GameObject>("Assets/Prefabs/Enemies/Slime_Bomber.prefab");
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    inst.name = "Daruma_Bomber";
                    var so = new SerializedObject(inst.GetComponent<SlimeBomber>());
                    so.FindProperty("flySpeed").floatValue = 7f;
                    so.FindProperty("diveSpeed").vector2Value = new Vector2(4f, 9f);
                    so.FindProperty("hoverDistance").floatValue = 1.2f;
                    so.FindProperty("hoverSeconds").floatValue = 0.2f;
                    so.FindProperty("windUpSeconds").floatValue = 0.15f;
                    so.FindProperty("faceRadius").floatValue = 0.3f;
                    so.FindProperty("faceShare").floatValue = 0.3f;
                    so.FindProperty("lifeSeconds").floatValue = 10f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(inst, BomberPath);
                }
                finally { Object.DestroyImmediate(inst); }
                notes.Add("Daruma_Bomber: a Slime_Bomber variant, flies 7 m/s, dives 4-9, hovers 0.2 s, face hit 0.3 of a bite");
            }
            else notes.Add("Daruma_Bomber: kept (already made)");
            return Load<GameObject>(BomberPath).GetComponent<SlimeBomber>();
        }

        private static Material MakeGlowMaterial()
        {
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Special_Stage_3");
            var path = MatFolder + "/Stage3_Edge_Glow_Mat.mat";
            var m = Load<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(1f, 0.45f, 0.95f));
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material, bool collide = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Edge(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.AddComponent<FallEdge>();   // before sizing: its Reset sizes a fresh box
            var box = go.GetComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
            box.isTrigger = true;
            go.layer = 2;
        }

        private static Light Lamp(Transform parent, string name, Vector3 at, Color colour, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = colour;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        private static TMP_Text Text(Transform parent, string name, Vector3 at, Vector3 euler, string text, float size, Vector2 box)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localEulerAngles = euler;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = box;
            t.color = Color.white;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(40, 10, 50, 255);
            return t;
        }

        private static DarumaStage MakeStage(SlimeBomber bomber, Material glow, List<string> notes)
        {
            var concrete = Load<Material>("Assets/Materials/Concrete_Mat.mat");
            var root = new GameObject("Special_Stage_3");
            try
            {
                var t = root.transform;
                // Floors (tops at 0), the wall behind the start and the low walls round it.
                Box(t, "Start Platform", new Vector3(0f, -0.15f, 0f), new Vector3(3.2f, 0.3f, 3f), concrete);
                Box(t, "Back Wall", new Vector3(0f, 1.5f, -1.65f), new Vector3(3.6f, 3f, 0.3f), concrete);
                Box(t, "Start Wall L", new Vector3(-1.7f, 0.55f, 0f), new Vector3(0.2f, 1.1f, 3f), concrete);
                Box(t, "Start Wall R", new Vector3(1.7f, 0.55f, 0f), new Vector3(0.2f, 1.1f, 3f), concrete);
                float bridgeLength = BridgeEnd - BridgeStart;
                Box(t, "Bridge", new Vector3(0f, -0.15f, (BridgeStart + BridgeEnd) * 0.5f), new Vector3(BridgeWidth, 0.3f, bridgeLength), concrete);
                Box(t, "Host Platform", new Vector3(0f, -0.15f, HostZ), new Vector3(7f, 0.3f, 7f), concrete);
                // Glowing edges so the drop shows in the dark.
                foreach (float x in new[] { -BridgeWidth * 0.5f + 0.025f, BridgeWidth * 0.5f - 0.025f })
                    Box(t, "Edge Glow", new Vector3(x, 0.015f, (BridgeStart + BridgeEnd) * 0.5f), new Vector3(0.05f, 0.03f, bridgeLength), glow, false);
                Box(t, "Back Wall Glow", new Vector3(0f, 3.02f, -1.65f), new Vector3(3.6f, 0.05f, 0.32f), glow, false);
                Box(t, "Host Platform Glow", new Vector3(0f, 0.015f, HostZ - 3.47f), new Vector3(7f, 0.03f, 0.05f), glow, false);

                // Fall Edges all round (not over the floors: the platforms are wider than the bridge).
                float mid = (BridgeStart + BridgeEnd) * 0.5f;
                Edge(t, "Fall Edge Bridge L", new Vector3(-BridgeWidth * 0.5f - 10f, 1.5f, mid), new Vector3(20f, 5f, bridgeLength));
                Edge(t, "Fall Edge Bridge R", new Vector3(BridgeWidth * 0.5f + 10f, 1.5f, mid), new Vector3(20f, 5f, bridgeLength));
                Edge(t, "Fall Edge Host L", new Vector3(-3.5f - 10f, 1.5f, HostZ), new Vector3(20f, 5f, 7f));
                Edge(t, "Fall Edge Host R", new Vector3(3.5f + 10f, 1.5f, HostZ), new Vector3(20f, 5f, 7f));
                Edge(t, "Fall Edge Behind Host", new Vector3(0f, 1.5f, HostZ + 3.5f + 10f), new Vector3(47f, 5f, 20f));
                Edge(t, "Fall Edge Start L", new Vector3(-1.8f - 10f, 1.5f, 0f), new Vector3(20f, 5f, 3f));
                Edge(t, "Fall Edge Start R", new Vector3(1.8f + 10f, 1.5f, 0f), new Vector3(20f, 5f, 3f));
                Edge(t, "Fall Edge Behind Start", new Vector3(0f, 1.5f, -1.8f - 10f), new Vector3(47f, 5f, 20f));

                // Lamps along the way, the host's light, and the red look light (on while it looks).
                foreach (float z in new[] { 6f, 14f, 22f }) Lamp(t, "Lamp", new Vector3(0f, 2.6f, z), new Color(0.75f, 0.45f, 1f), 3f, 9f);
                Lamp(t, "Host Light", new Vector3(0f, 4.5f, HostZ - 3f), new Color(1f, 0.92f, 0.85f), 5f, 11f);
                var look = Lamp(t, "Look Light", new Vector3(0f, 3.2f, HostZ - 2.2f), new Color(1f, 0.15f, 0.1f), 9f, 20f);

                // The host: the Giant Slime, big, at the far end, facing the start.
                var hostPrefab = Load<GameObject>("Assets/Prefabs/Bonus/Paint_Giant_Slime.prefab");
                var host = (GameObject)PrefabUtility.InstantiatePrefab(hostPrefab, t);
                host.name = "Host Giant Slime";
                host.transform.localPosition = new Vector3(0f, 0f, HostZ);
                host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                host.transform.localScale = Vector3.one * HostScale;
                var notesFx = Load<GameObject>("Assets/Prefabs/Effects/Music_Notes_Loop.prefab");
                GameObject sing = null;
                if (notesFx != null)
                {
                    sing = (GameObject)PrefabUtility.InstantiatePrefab(notesFx, t);
                    sing.name = "Song Notes";
                    sing.transform.localPosition = new Vector3(0f, 3.1f, HostZ);
                    sing.transform.localScale = Vector3.one * 4f;
                }

                // Places.
                var start = new GameObject("Start Spot").transform; start.SetParent(t, false); start.localPosition = new Vector3(0f, 0f, 0.2f);
                var pathStart = new GameObject("Path Start").transform; pathStart.SetParent(t, false); pathStart.localPosition = new Vector3(0f, 0f, BridgeStart);
                var pathEnd = new GameObject("Path End").transform; pathEnd.SetParent(t, false); pathEnd.localPosition = new Vector3(0f, 0f, BridgeEnd);

                // Signs: the timer and the big status over the host; the rules ahead on the left of the start (about 3 m
                // away, turned to face it, so it doesn't fill the left of the view; 2026-11-06 note).
                var timer = Text(t, "Timer", new Vector3(0f, 5.4f, HostZ), Vector3.zero, "60", 24f, new Vector2(6f, 3f));
                var status = Text(t, "Status", new Vector3(0f, 7.4f, HostZ), Vector3.zero, "", 16f, new Vector2(14f, 4f));
                Text(t, "Rules", new Vector3(-2.1f, 1.7f, 2.6f), new Vector3(0f, -41.2f, 0f),
                    "<size=140%><color=#FF9EE5>SPECIAL STAGE 3</color></size>\nWalk while it <color=#7FFFD4>SINGS</color>.\n<color=#FF6B5E>FREEZE</color> when it LOOKS!\nTouch the Giant Slime\nbefore time runs out.",
                    3f, new Vector2(3.2f, 2.7f));

                var game = root.AddComponent<DarumaStage>();
                var so = new SerializedObject(game);
                so.FindProperty("startSpot").objectReferenceValue = start;
                so.FindProperty("host").objectReferenceValue = host.GetComponent<GiantSlime>();
                so.FindProperty("pathStart").objectReferenceValue = pathStart;
                so.FindProperty("pathEnd").objectReferenceValue = pathEnd;
                var noteList = so.FindProperty("notes");
                var clips = Enumerable.Range(1, 9).Select(i => Load<AudioClip>($"{Sfx}Special_Stage_3/song_{i:00}.wav")).Where(c => c != null).ToArray();
                noteList.arraySize = clips.Length;
                for (int i = 0; i < clips.Length; i++) noteList.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                so.FindProperty("singEffect").objectReferenceValue = sing;
                so.FindProperty("turnSound").objectReferenceValue = Load<AudioClip>(Sfx + "whoosh.wav");
                so.FindProperty("lookSound").objectReferenceValue = Load<AudioClip>(Sfx + "Special_Stage_3/look.wav");
                so.FindProperty("lookLight").objectReferenceValue = look;
                so.FindProperty("caughtSound").objectReferenceValue = Load<AudioClip>(Sfx + "Special_Stage_3/gotcha.wav");
                so.FindProperty("bomber").objectReferenceValue = bomber;
                var candies = so.FindProperty("candyPrefabs");
                var candy = Game.Inventory.CandyKinds.Numbers.Select(i => Load<GameObject>($"Assets/Prefabs/Items/Breakable/Candy_{i}.prefab")).Where(c => c != null).ToArray();
                candies.arraySize = candy.Length;
                for (int i = 0; i < candy.Length; i++) candies.GetArrayElementAtIndex(i).objectReferenceValue = candy[i];
                so.FindProperty("winEffect").objectReferenceValue = Load<GameObject>("Assets/Prefabs/Light/Firework_Big.prefab");
                so.FindProperty("winSound").objectReferenceValue = Load<AudioClip>(Sfx + "A_gentle_miracle_sound_1.wav");
                so.FindProperty("laughSound").objectReferenceValue = Load<AudioClip>(Sfx + "cute_laugh.wav");
                so.FindProperty("tickSound").objectReferenceValue = Load<AudioClip>(Sfx + "count_down.wav");
                so.FindProperty("timerText").objectReferenceValue = timer;
                so.FindProperty("statusText").objectReferenceValue = status;
                // Its skin: the flying slime's paint texture, so the green (sings) and red (looks) show clean.
                so.FindProperty("hostPaint").objectReferenceValue = Load<Texture2D>("Assets/M_Textures/Flying_Slime/Flying_Slime_Paint.png");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, StagePath);
                notes.Add("Special_Stage_3: bridge 1.4 x " + bridgeLength + " m, Giant Slime x" + HostScale + " at " + HostZ + " m, 8 Fall Edges, " + clips.Length + " song notes, " + candy.Length + " candy kinds");
            }
            finally { Object.DestroyImmediate(root); }
            return Load<GameObject>(StagePath).GetComponent<DarumaStage>();
        }

        // The cauldron's trigger: the stage (kept as it is when none is given), the seed a win hands over, the start delay.
        private static void HookCauldron(DarumaStage stage, List<string> notes)
        {
            if (Load<GameObject>(Cauldron) == null)
            {
                Debug.LogWarning("[Special Stage 3] No cauldron prefab at " + Cauldron + ": not hooked.");
                notes.Add("Fire_Woods_n_Cauldron: missing, not hooked");
                return;
            }
            var seed = Load<GameObject>(SeedPath);
            if (seed == null) Debug.LogWarning("[Special Stage 3] No seed prefab at " + SeedPath + ": a win sets GAMESTAGES The_Seed itself (no item, no heal).");
            var root = PrefabUtility.LoadPrefabContents(Cauldron);
            try
            {
                var trigger = root.GetComponent<SpecialStage3Trigger>();
                if (trigger == null) trigger = root.AddComponent<SpecialStage3Trigger>();
                var so = new SerializedObject(trigger);
                if (stage != null) so.FindProperty("stage").objectReferenceValue = stage;
                so.FindProperty("seed").objectReferenceValue = seed;
                so.FindProperty("delay").floatValue = StartDelay;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Cauldron);
                var hooked = so.FindProperty("stage").objectReferenceValue;
                notes.Add("Fire_Woods_n_Cauldron: Special Stage 3 Trigger (pot break) stage " + (hooked != null ? hooked.name : "NONE") +
                          ", seed " + (seed != null ? seed.name : "NONE") + ", delay " + StartDelay + " s");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
