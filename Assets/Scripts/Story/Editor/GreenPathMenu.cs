using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Story;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Green Path (user 2026-11-27: "a new way to go to the next room ... when we pick
    /// up the piece of the seed, from that point create a green light path, check the light that works as border of
    /// boss area, follow that green light path will reach to a zone to teleport player back to the center and the new
    /// room will come, like normal. We can use that green light path to lead Player at the start of the game to reach
    /// to the Fairy in tutorial stage. At the end of that green light path, when player reach that teleport zone,
    /// everything should be dark there only the green light path, then poof, create a teleport tube run around Player
    /// then move them to center"). Re-runnable; "Undo Green Path" takes the GAMESTAGES changes out (looks and settings stay).
    /// 1. Looks, rebuilt every run: Data/Effects/Green Path Line.mat (the boss arena border's line, seen from both
    ///    sides), Teleport Tube.mat + .png (light streaks, both sides), Prefabs/Story/Green_Path (the band, embers, soft
    ///    floor glows, pulse sparks, the zone: ring, glow, curtain, embers, light, hum) and Prefabs/Story/Teleport_Tube.
    ///    The particles use the bonfires' Danger Glow, the border's own glow.
    /// 2. Resources/Green Path Settings: made if missing; its two prefabs set, empty sounds filled.
    /// 3. GAMESTAGES (the open 2 Game Scene; SAVE IT after): each room's fight state, where it waited for the seed piece
    ///    and then went straight to its clean-up, now goes to a new "Green Path - ..." state first (Story Green Path, To
    ///    Exit Zone: the zone at the spot below, the player sent to the room's middle), and that state's FINISHED goes on
    ///    to the clean-up (the purple hole, the room goes, the next comes, as before). "Move to Fairy" gets a Story Green
    ///    Path (Lead To Target: Fairy_Pos) last, so the walk to the Fairy has the path too.
    /// The zones (the rooms sit at the world's middle; each moves to the nearest walkable floor within Exit Snap): start
    /// rooftop north-east between the stone gates; storage room south-east; bedroom by the west W/C door; living room by
    /// the east double door (fixed, does nothing); playroom (the living room x1.5) east. Change one in its state's action.
    /// </summary>
    public static class GreenPathMenu
    {
        private const string Tag = "[Green Path]";
        private const string SettingsPath = "Assets/Resources/Green Path Settings.asset";
        private const string PathPrefabPath = "Assets/Prefabs/Story/Green_Path.prefab";
        private const string TubePrefabPath = "Assets/Prefabs/Story/Teleport_Tube.prefab";
        private const string HallwayPrefabPath = "Assets/Prefabs/Story/Green_Hallway.prefab";
        private const string FloorMaterialPath = "Assets/Data/Effects/Green Hallway Floor.mat";
        private const string PoleMaterialPath = "Assets/Data/Effects/Green Hallway Pole.mat";
        private const string OrbMaterialPath = "Assets/Data/Effects/Green Hallway Orb.mat";
        private const string RooftopPath = "Assets/Prefabs/Bases/Rooftop_Base_Z.prefab";
        private const string LineSourcePath = "Assets/Data/Effects/Boss Arena Line.mat";
        private const string GlowPath = "Assets/Data/Effects/Danger Glow.mat";
        private const string LinePath = "Assets/Data/Effects/Green Path Line.mat";
        private const string TubeMaterialPath = "Assets/Data/Effects/Teleport Tube.mat";
        private const string TubeTexturePath = "Assets/Data/Effects/Teleport Tube.png";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string SciFi = "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/";
        private const string LeadState = "Move to Fairy";
        private const string FairyPoint = "Fairy_Pos";

        // The fight state, its event to the clean-up, the clean-up, the new state, the zone's spot.
        private static readonly (string fight, string ev, string clean, string name, Vector3 exit)[] Rooms =
        {
            ("Stage 1 - The Start", "next", "Clean Stage 1", "Green Path - Start", new Vector3(0f, 0f, 14.35f)),
            ("Mini Enemy", "next", "Clean Mini", "Green Path - Storage", new Vector3(0.05f, 0f, -3.75f)),
            ("Enemy1 - Wait", "next", "Clean Enemy 1", "Green Path - Bedroom", new Vector3(-3.75f, 0f, -2.3f)),
            ("Enemy2 - Wait", "FINISHED", "Clean Enemy 2", "Green Path - Living Room", new Vector3(4.5f, 0f, 1.5f)),
            ("Enemy 3 Stuffs", "FINISHED", "Clean Enemy 3", "Green Path - Playroom", new Vector3(10.65f, 0f, 0f)),
        };

        private static readonly Color Green = new Color(0.55f, 1f, 0.45f, 1f);

        [MenuItem("Tools/429 Game/Story/Set Up Green Path")]
        public static void SetUp()
        {
            var lineSource = AssetDatabase.LoadAssetAtPath<Material>(LineSourcePath);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowPath);
            if (lineSource == null || glow == null)
            {
                Debug.LogError($"{Tag} Needs {LineSourcePath} (Set Up Boss Arena Border makes it) and {GlowPath}; nothing changed.");
                return;
            }
            var notes = new List<string>();
            var line = LineMaterial(lineSource);
            var tubeMaterial = TubeMaterial(glow);
            Directory.CreateDirectory(Path.GetDirectoryName(PathPrefabPath));
            BuildPath(line, glow);
            BuildTube(tubeMaterial, line, glow);
            BuildHallway(line, glow);
            notes.Add("looks built (Green_Path, Teleport_Tube, Green_Hallway)");
            // The start's exit is on the rooftop's north end wall (its middle line 0.65 m past the exit spot).
            var start = Rooms[0];
            var outward = Mathf.Abs(start.exit.x) >= Mathf.Abs(start.exit.z) ? new Vector3(Mathf.Sign(start.exit.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(start.exit.z));
            var doorways = RooftopDoorways(outward);
            notes.Add($"rooftop doorway meshes: {doorways.Count}");
            notes.Add(Settings(doorways));
            notes.AddRange(Wire(false));
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} {string.Join("; ", notes)}.");
        }

        [MenuItem("Tools/429 Game/Story/Undo Green Path")]
        public static void UndoGreenPath()
        {
            var notes = Wire(true);
            Debug.Log($"{Tag} {(notes.Count == 0 ? "nothing to undo" : string.Join("; ", notes))}.");
        }

        [MenuItem("Tools/429 Game/Story/Set Up Green Path", true)]
        [MenuItem("Tools/429 Game/Story/Undo Green Path", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---------- GAMESTAGES ----------

        private static List<string> Wire(bool undo)
        {
            var notes = new List<string>();
            PlayMakerFSM stages = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!f.gameObject.scene.IsValid() || f.FsmName != "GAMESTAGES") continue;
                stages = f;
                break;
            }
            if (stages == null)
            {
                notes.Add("GAMESTAGES NOT CHANGED: open 2 Game Scene and run it again");
                return notes;
            }
            if (!stages.Fsm.Initialized) stages.Fsm.Init(stages);
            var fsm = stages.Fsm;
            bool changed = false;
            foreach (var room in Rooms)
            {
                var fight = fsm.GetState(room.fight);
                var clean = fsm.GetState(room.clean);
                var mine = fsm.GetState(room.name);
                var exit = fight?.Transitions.FirstOrDefault(t => t.EventName == room.ev);
                if (undo)
                {
                    if (exit != null && exit.ToState == room.name)
                    {
                        exit.ToState = room.clean;
                        notes.Add($"'{room.fight}' {room.ev} -> '{room.clean}' again");
                        changed = true;
                    }
                    if (mine != null)
                    {
                        fsm.States = fsm.States.Where(s => s != mine).ToArray();
                        notes.Add($"'{room.name}' removed");
                        changed = true;
                    }
                    continue;
                }
                if (fight == null || clean == null || exit == null)
                {
                    notes.Add($"'{room.fight}' ({room.ev}) or '{room.clean}' missing: skipped");
                    continue;
                }
                if (mine == null)
                {
                    var action = new StoryGreenPath();
                    action.Reset();
                    action.mode = StoryGreenPath.Mode.ToExitZone;
                    action.exitPosition = new FsmVector3 { Value = room.exit };
                    action.centre = new FsmVector3 { Value = Vector3.zero };
                    var at = fight.Position;
                    mine = new FsmState(fsm)
                    {
                        Name = room.name,
                        Position = new Rect((at.x + clean.Position.x) * 0.5f + 40f, (at.y + clean.Position.y) * 0.5f + 40f, at.width, at.height),
                        Actions = new FsmStateAction[] { action },
                        Transitions = new[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = room.clean } },
                    };
                    fsm.States = fsm.States.Append(mine).ToArray();
                    mine.SaveActions();
                    notes.Add($"'{room.name}' made (zone at {room.exit})");
                    changed = true;
                }
                else
                {
                    mine.LoadActions();
                    var path = mine.Actions.OfType<StoryGreenPath>().FirstOrDefault();
                    if (path != null && mine.ActionData.ActionCount == mine.Actions.Length &&
                        (path.exitPosition == null || path.exitPosition.Value != room.exit))
                    {
                        path.exitPosition = new FsmVector3 { Value = room.exit };
                        mine.SaveActions();
                        notes.Add($"'{room.name}' exit -> {room.exit}");
                        changed = true;
                    }
                    var on = mine.Transitions.FirstOrDefault(t => t.EventName == "FINISHED");
                    if (on == null)
                    {
                        mine.Transitions = mine.Transitions.Append(new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = room.clean }).ToArray();
                        changed = true;
                    }
                    else if (on.ToState != room.clean)
                    {
                        on.ToState = room.clean;
                        changed = true;
                    }
                }
                if (exit.ToState != room.name)
                {
                    notes.Add($"'{room.fight}' {room.ev} -> '{room.name}' (was '{exit.ToState}')");
                    exit.ToState = room.name;
                    changed = true;
                }
            }

            // The walk to the Fairy at the start.
            var lead = fsm.GetState(LeadState);
            if (lead == null) notes.Add($"no '{LeadState}': no path to the Fairy");
            else
            {
                lead.LoadActions();
                if (lead.ActionData.ActionCount != lead.Actions.Length)
                    notes.Add($"'{LeadState}' has an action that could not be read: left alone (saving would drop it)");
                else
                {
                    bool has = lead.Actions.OfType<StoryGreenPath>().Any();
                    if (undo && has)
                    {
                        lead.Actions = lead.Actions.Where(a => !(a is StoryGreenPath)).ToArray();
                        lead.SaveActions();
                        notes.Add($"path to the Fairy out of '{LeadState}'");
                        changed = true;
                    }
                    else if (!undo && !has)
                    {
                        var fairy = SceneObject(stages.gameObject.scene, FairyPoint);
                        if (fairy == null) notes.Add($"no {FairyPoint} in the scene: no path to the Fairy");
                        else
                        {
                            var action = new StoryGreenPath();
                            action.Reset();
                            action.mode = StoryGreenPath.Mode.LeadToTarget;
                            action.target = new FsmGameObject { Value = fairy };
                            lead.Actions = lead.Actions.Append(action).ToArray();
                            lead.SaveActions();
                            notes.Add($"path to the Fairy ({FairyPoint}) last in '{LeadState}'");
                            changed = true;
                        }
                    }
                }
            }
            if (changed)
            {
                EditorUtility.SetDirty(stages);
                EditorSceneManager.MarkSceneDirty(stages.gameObject.scene);
                notes.Add("SAVE 2 Game Scene");
            }
            else notes.Add(undo ? "GAMESTAGES already as before" : "GAMESTAGES already set");
            return notes;
        }

        private static GameObject SceneObject(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        // ---------- settings ----------

        private static string Settings(List<(Mesh whole, Mesh opened)> doorways)
        {
            var settings = AssetDatabase.LoadAssetAtPath<GreenPathSettings>(SettingsPath);
            bool made = false;
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                settings = ScriptableObject.CreateInstance<GreenPathSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                made = true;
            }
            var so = new SerializedObject(settings);
            so.FindProperty("pathPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PathPrefabPath);
            so.FindProperty("tubePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(TubePrefabPath);
            so.FindProperty("hallwayPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(HallwayPrefabPath);
            var list = so.FindProperty("doorways");
            list.arraySize = doorways.Count;
            for (int i = 0; i < doorways.Count; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("whole").objectReferenceValue = doorways[i].whole;
                item.FindPropertyRelative("opened").objectReferenceValue = doorways[i].opened;
            }
            Fill(so, "appearSound", Sfx + "A_magical_glowing.wav");
            Fill(so, "zoneHum", SciFi + "forceField_000.ogg");
            Fill(so, "poofSound", Sfx + "stage_swap_poof_1.wav");
            Fill(so, "tubeSound", SciFi + "forceField_002.ogg");
            Fill(so, "jumpSound", Sfx + "fairy_teleport_blink_1.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            return (made ? "made " : "updated ") + SettingsPath;
        }

        private static void Fill(SerializedObject so, string field, string clipPath)
        {
            var p = so.FindProperty(field);
            if (p != null && p.objectReferenceValue == null) p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        }

        // ---------- materials ----------

        // The boss border's line material, seen from both sides (the band lies flat on the floor).
        private static Material LineMaterial(Material source)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(LinePath);
            if (m == null)
            {
                m = new Material(source) { name = "Green Path Line" };
                AssetDatabase.CreateAsset(m, LinePath);
            }
            m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // The glow with streaks of light that run up the tube, seen from inside too.
        private static Material TubeMaterial(Material glow)
        {
            var texture = TubeTexture();
            var m = AssetDatabase.LoadAssetAtPath<Material>(TubeMaterialPath);
            if (m == null)
            {
                m = new Material(glow) { name = "Teleport Tube" };
                AssetDatabase.CreateAsset(m, TubeMaterialPath);
            }
            m.mainTexture = texture;
            m.mainTextureScale = Vector2.one;
            m.mainTextureOffset = Vector2.zero;
            m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Light streaks: each column its own brightness with soft dashes running up it; repeats both ways.
        private static Texture2D TubeTexture()
        {
            var full = Path.Combine(Path.GetDirectoryName(Application.dataPath), TubeTexturePath);
            if (!File.Exists(full))
            {
                const int w = 128, h = 64;
                var random = new System.Random(429);
                var column = new float[w];
                var phase = new float[w];
                var dashes = new int[w];
                for (int x = 0; x < w; x++)
                {
                    column[x] = random.NextDouble() < 0.28 ? 0.7f + 0.3f * (float)random.NextDouble() : 0.06f + 0.3f * (float)random.NextDouble();
                    phase[x] = (float)random.NextDouble();
                    dashes[x] = 1 + random.Next(3);
                }
                var soft = new float[w];
                for (int x = 0; x < w; x++) soft[x] = 0.25f * column[(x + w - 1) % w] + 0.5f * column[x] + 0.25f * column[(x + 1) % w];
                var picture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                {
                    float v = (y + 0.5f) / h;
                    for (int x = 0; x < w; x++)
                    {
                        float wave = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (v * dashes[x] + phase[x]));
                        float a = Mathf.Clamp01(soft[x] * (0.35f + 0.65f * wave));
                        picture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
                AssetDatabase.ImportAsset(TubeTexturePath);
                if (AssetImporter.GetAtPath(TubeTexturePath) is TextureImporter importer)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TubeTexturePath);
        }

        // ---------- the path ----------

        private static void BuildPath(Material line, Material glow)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Green_Path");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var path = root.AddComponent<GreenPath>();
                var band = Line(root.transform, "Path Line", line, true, LineAlignment.TransformZ, 0.26f);
                band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                band.numCapVertices = 4;
                band.numCornerVertices = 3;

                var embers = Particles(root.transform, "Path Embers", glow, 500, ParticleSystemRenderMode.Billboard,
                    new Vector2(1.2f, 2.2f), new Vector2(0.035f, 0.075f), new Color(0.45f, 1f, 0.35f), new Color(0.8f, 1f, 0.65f),
                    new Vector2(0.15f, 0.5f), 0.2f, 0.12f, 0.6f);
                var floorGlow = Particles(root.transform, "Path Glow", glow, 220, ParticleSystemRenderMode.HorizontalBillboard,
                    new Vector2(1.6f, 2.6f), new Vector2(0.45f, 0.75f), new Color(0.35f, 1f, 0.3f, 0.2f), new Color(0.5f, 1f, 0.4f, 0.28f),
                    Vector2.zero, 0f, 0.3f, 0.65f);
                var sparks = Particles(root.transform, "Path Sparks", glow, 300, ParticleSystemRenderMode.Billboard,
                    new Vector2(0.3f, 0.6f), new Vector2(0.03f, 0.07f), new Color(0.75f, 1f, 0.7f), new Color(1f, 1f, 0.9f),
                    new Vector2(0.1f, 0.4f), 0.15f, 0.05f, 0.4f);

                var zone = Child(root.transform, "Zone");
                var ring = Line(zone, "Zone Ring", line, true, LineAlignment.TransformZ, 0.07f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.loop = true;
                var zoneGlow = Particles(zone, "Zone Glow", glow, 24, ParticleSystemRenderMode.HorizontalBillboard,
                    new Vector2(1.2f, 1.8f), new Vector2(1.4f, 1.8f), new Color(0.4f, 1f, 0.35f, 0.3f), new Color(0.55f, 1f, 0.45f, 0.38f),
                    Vector2.zero, 0f, 0.3f, 0.6f);
                var curtain = Particles(zone, "Zone Curtain", glow, 220, ParticleSystemRenderMode.VerticalBillboard,
                    new Vector2(1.4f, 2.2f), new Vector2(1f, 1f), new Color(0.45f, 1f, 0.4f, 0.16f), new Color(0.6f, 1f, 0.5f, 0.12f),
                    new Vector2(0.03f, 0.12f), 0f, 0.3f, 0.7f);
                var curtainMain = curtain.main;
                curtainMain.startSize3D = true;
                curtainMain.startSizeX = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
                curtainMain.startSizeY = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
                curtainMain.startSizeZ = 1f;
                var zoneEmbers = Particles(zone, "Zone Embers", glow, 220, ParticleSystemRenderMode.Billboard,
                    new Vector2(1.2f, 2f), new Vector2(0.04f, 0.08f), new Color(0.5f, 1f, 0.4f), new Color(0.85f, 1f, 0.7f),
                    new Vector2(0.5f, 1.1f), 0.25f, 0.12f, 0.6f);
                var lightGo = Child(zone, "Zone Light");
                lightGo.localPosition = Vector3.up * 0.7f;
                var light = lightGo.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.5f, 1f, 0.42f);
                light.range = 3.5f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                var hum = Child(zone, "Zone Hum").gameObject.AddComponent<AudioSource>();
                hum.playOnAwake = false;
                hum.loop = true;
                hum.volume = 0f;
                hum.spatialBlend = 1f;
                hum.rolloffMode = AudioRolloffMode.Linear;
                hum.minDistance = 1f;
                hum.maxDistance = 9f;
                hum.dopplerLevel = 0f;
                var voice = Child(root.transform, "Voice").gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0.6f;
                voice.minDistance = 2f;
                voice.maxDistance = 15f;
                voice.dopplerLevel = 0f;

                var so = new SerializedObject(path);
                so.FindProperty("line").objectReferenceValue = band;
                so.FindProperty("embers").objectReferenceValue = embers;
                so.FindProperty("glow").objectReferenceValue = floorGlow;
                so.FindProperty("sparks").objectReferenceValue = sparks;
                so.FindProperty("zone").objectReferenceValue = zone;
                so.FindProperty("zoneRing").objectReferenceValue = ring;
                so.FindProperty("zoneGlow").objectReferenceValue = zoneGlow;
                so.FindProperty("zoneCurtain").objectReferenceValue = curtain;
                so.FindProperty("zoneEmbers").objectReferenceValue = zoneEmbers;
                so.FindProperty("zoneLight").objectReferenceValue = light;
                so.FindProperty("zoneHum").objectReferenceValue = hum;
                so.FindProperty("voice").objectReferenceValue = voice;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PathPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // ---------- the tube ----------

        private static void BuildTube(Material tubeMaterial, Material line, Material glow)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Teleport_Tube");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var tube = root.AddComponent<TeleportTube>();
                var body = Child(root.transform, "Tube");
                var filter = body.gameObject.AddComponent<MeshFilter>();
                var renderer = body.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = tubeMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var rings = new LineRenderer[3];
                for (int i = 0; i < rings.Length; i++)
                {
                    rings[i] = Line(root.transform, "Ring " + (i + 1), line, false, LineAlignment.View, 0.035f);
                    rings[i].loop = true;
                }
                var sparks = Particles(root.transform, "Tube Sparks", glow, 400, ParticleSystemRenderMode.Billboard,
                    new Vector2(0.5f, 0.9f), new Vector2(0.03f, 0.06f), new Color(0.6f, 1f, 0.5f), new Color(0.95f, 1f, 0.85f),
                    Vector2.zero, 0.1f, 0.08f, 0.6f);
                var sound = Child(root.transform, "Tube Sound").gameObject.AddComponent<AudioSource>();
                sound.playOnAwake = false;
                sound.loop = true;
                sound.volume = 0f;
                sound.spatialBlend = 0f;

                var so = new SerializedObject(tube);
                so.FindProperty("tubeMesh").objectReferenceValue = filter;
                so.FindProperty("tube").objectReferenceValue = renderer;
                var list = so.FindProperty("rings");
                list.arraySize = rings.Length;
                for (int i = 0; i < rings.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = rings[i];
                so.FindProperty("sparks").objectReferenceValue = sparks;
                so.FindProperty("sound").objectReferenceValue = sound;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TubePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // ---------- the hallway ----------

        // The dark hallway out of the room: its floor (a dark box, solid and a teleport area like the rooms' own), a light
        // line for its edges, a post with a green orb, a green lamp, embers and the orbs' soft glow; Green Hallway sizes
        // and places them at run time.
        private static void BuildHallway(Material line, Material glow)
        {
            var floorMaterial = LitMaterial(FloorMaterialPath, "Green Hallway Floor", new Color(0.045f, 0.05f, 0.045f), 0.35f);
            // (the posts' pole material is gone with the poles)
            if (AssetDatabase.LoadAssetAtPath<Material>(PoleMaterialPath) != null) AssetDatabase.DeleteAsset(PoleMaterialPath);
            var orbMaterial = AssetDatabase.LoadAssetAtPath<Material>(OrbMaterialPath);
            if (orbMaterial == null)
            {
                orbMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Green Hallway Orb" };
                AssetDatabase.CreateAsset(orbMaterial, OrbMaterialPath);
            }
            orbMaterial.SetColor("_BaseColor", new Color(0.62f, 1f, 0.5f, 1f));
            EditorUtility.SetDirty(orbMaterial);

            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Green_Hallway");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var hall = root.AddComponent<GreenHallway>();
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(root.transform, false);
                var floorRenderer = floor.GetComponent<MeshRenderer>();
                floorRenderer.sharedMaterial = floorMaterial;
                floorRenderer.shadowCastingMode = ShadowCastingMode.Off;
                int teleportLayer = LayerMask.NameToLayer("Teleport");
                if (teleportLayer >= 0) floor.layer = teleportLayer;
                var area = floor.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
                var rooftop = AssetDatabase.LoadAssetAtPath<GameObject>(RooftopPath);
                var source = rooftop != null ? rooftop.transform.Find("Teleport Area") : null;
                var sourceArea = source != null ? source.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>() : null;
                if (sourceArea != null) area.interactionLayers = sourceArea.interactionLayers;

                var edge = Line(root.transform, "Edge", line, true, LineAlignment.TransformZ, 0.1f);
                edge.numCapVertices = 3;

                // A runway light: a flat green dot on the floor (user 2026-11-27: "remove the pole, leave only the green dot
                // on the ground, like the one that lit the way for plane").
                var post = new GameObject("Light Dot");
                post.transform.SetParent(root.transform, false);
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "Dot";
                Object.DestroyImmediate(orb.GetComponent<Collider>());
                orb.transform.SetParent(post.transform, false);
                orb.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                orb.transform.localScale = new Vector3(0.14f, 0.06f, 0.14f);
                var orbRenderer = orb.GetComponent<MeshRenderer>();
                orbRenderer.sharedMaterial = orbMaterial;
                orbRenderer.shadowCastingMode = ShadowCastingMode.Off;
                post.SetActive(false);

                var lampGo = Child(root.transform, "Lamp");
                var lamp = lampGo.gameObject.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = new Color(0.5f, 1f, 0.42f);
                lamp.range = 3.4f;
                lamp.intensity = 0f;
                lamp.shadows = LightShadows.None;
                lampGo.gameObject.SetActive(false);

                var embers = Particles(root.transform, "Embers", glow, 400, ParticleSystemRenderMode.Billboard,
                    new Vector2(1.2f, 2.2f), new Vector2(0.035f, 0.07f), new Color(0.45f, 1f, 0.35f), new Color(0.8f, 1f, 0.65f),
                    new Vector2(0.2f, 0.6f), 0.2f, 0.12f, 0.6f);
                var lampGlow = Particles(root.transform, "Lamp Glow", glow, 200, ParticleSystemRenderMode.Billboard,
                    new Vector2(0.9f, 1.3f), new Vector2(0.3f, 0.42f), new Color(0.45f, 1f, 0.4f, 0.45f), new Color(0.6f, 1f, 0.5f, 0.55f),
                    Vector2.zero, 0f, 0.3f, 0.6f);

                var so = new SerializedObject(hall);
                so.FindProperty("floor").objectReferenceValue = floor.transform;
                so.FindProperty("edge").objectReferenceValue = edge;
                so.FindProperty("post").objectReferenceValue = post;
                so.FindProperty("lamp").objectReferenceValue = lamp;
                so.FindProperty("embers").objectReferenceValue = embers;
                so.FindProperty("lampGlow").objectReferenceValue = lampGlow;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, HallwayPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static Material LitMaterial(string path, string name, Color colour, float smoothness)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- the rooftop's doorway ----------

        // The start rooftop's way out is the end of its north arm. Its walls (with their crenellations) are one mesh, the
        // battlements, under a bone the rooftop's Animator holds every frame (so they can't sink like the rooms' kit
        // pieces): a copy with that end wall cut away is saved as a mesh asset and Green Path Run swaps it in while the
        // hallway is there. The cut is everything inside the end wall's own collider box (it sits under the battlements,
        // so the box holds in any pose), a little bigger. Made fresh every run.
        private static List<(Mesh whole, Mesh opened)> RooftopDoorways(Vector3 outward)
        {
            var result = new List<(Mesh, Mesh)>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RooftopPath);
            if (prefab == null) return result;
            string wallName = outward.z > 0.5f ? "Wall North End" : outward.z < -0.5f ? "Wall South End" : outward.x > 0.5f ? "Wall East End" : "Wall West End";
            var preview = EditorSceneManager.NewPreviewScene();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            try
            {
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.name.EndsWith("Battlements") || !r.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
                    BoxCollider wall = null;
                    foreach (var c in r.GetComponentsInChildren<BoxCollider>(true))
                        if (c.name == wallName) wall = c;
                    if (wall == null)
                    {
                        Debug.LogWarning($"{Tag} {r.name} has no '{wallName}' collider: no rooftop doorway.");
                        continue;
                    }
                    // The wall's box in the battlements' own space, a little bigger (0.25 m round, 0.6 m over the top).
                    var toLocal = r.transform.worldToLocalMatrix * wall.transform.localToWorldMatrix;
                    var half = wall.size * 0.5f;
                    var box = new Bounds(toLocal.MultiplyPoint3x4(wall.center), Vector3.zero);
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = wall.center + new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
                        box.Encapsulate(toLocal.MultiplyPoint3x4(corner));
                    }
                    box.Expand(0.5f);
                    box.max += Vector3.up * 0.35f;
                    var whole = filter.sharedMesh;
                    var vertices = whole.vertices;
                    var cut = Object.Instantiate(whole);
                    cut.name = whole.name + " (" + wallName + " open)";
                    int removed = 0;
                    for (int sub = 0; sub < cut.subMeshCount; sub++)
                    {
                        var tris = cut.GetTriangles(sub);
                        var keep = new List<int>(tris.Length);
                        for (int i = 0; i + 2 < tris.Length; i += 3)
                        {
                            var mid = (vertices[tris[i]] + vertices[tris[i + 1]] + vertices[tris[i + 2]]) / 3f;
                            if (box.Contains(mid))
                            {
                                removed++;
                                continue;
                            }
                            keep.Add(tris[i]);
                            keep.Add(tris[i + 1]);
                            keep.Add(tris[i + 2]);
                        }
                        cut.SetTriangles(keep, sub);
                    }
                    string path = "Assets/Data/Effects/Rooftop Doorway - " + whole.name + ".asset";
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(cut, path);
                    result.Add((whole, AssetDatabase.LoadAssetAtPath<Mesh>(path)));
                    Debug.Log($"{Tag} {whole.name}: {removed} of {whole.triangles.Length / 3} triangles cut ('{wallName}') -> {path}");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            // The first try's cut of the rooftop's main model (it has no wall there) is not used any more.
            if (AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Data/Effects/Rooftop Doorway - Rooftop_Base_Z.asset") != null)
                AssetDatabase.DeleteAsset("Assets/Data/Effects/Rooftop Doorway - Rooftop_Base_Z.asset");
            return result;
        }

        // ---------- parts ----------

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static LineRenderer Line(Transform parent, string name, Material material, bool world, LineAlignment alignment, float width)
        {
            var line = Child(parent, name).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = world;
            line.positionCount = 0;
            line.alignment = alignment;
            line.widthMultiplier = width;
            line.textureMode = LineTextureMode.Stretch;
            line.startColor = Green;
            line.endColor = Green;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return line;
        }

        // A world-space system the path emits itself (its own emission off): lifetimes, sizes and colours between two
        // values, drifting up (metres a second), a little noise, fading in and out (shares of its life).
        private static ParticleSystem Particles(Transform parent, string name, Material material, int max,
            ParticleSystemRenderMode mode, Vector2 life, Vector2 size, Color colourA, Color colourB, Vector2 up, float noise,
            float fadeIn, float fadeOutFrom)
        {
            var go = Child(parent, name).gameObject;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            if (up.y > 0f)
            {
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
                velocity.y = new ParticleSystem.MinMaxCurve(up.x, up.y);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            }
            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.6f;
                n.scrollSpeed = 0.3f;
                n.quality = ParticleSystemNoiseQuality.Low;
            }
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, fadeOutFrom), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            var sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }
    }
}
