using System.Collections.Generic;
using System.IO;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Game.Story;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Boss Escape (user, 2026-11-25): when a room's laughing pumpkin breaks, the room's
    /// mini boss pops out of it, runs off and digs into the floor; then the room goes on as before, and the boss comes
    /// back out of its hole at the end. Re-runnable: makes the hole (mesh + prefab), the settings
    /// (Resources/Story Boss Escape Settings; tuned values stay) and, in GAMESTAGES, one "Boss Escape ..." state per room
    /// between its waiting states and its wall drop. "Undo Boss Escape" puts GAMESTAGES back. Save the scene after.
    /// </summary>
    public static class StoryBossEscapeMenu
    {
        private const string Tag = "[Story Boss Escape]";
        private const string SettingsPath = "Assets/Resources/Story Boss Escape Settings.asset";
        private const string MeshFolder = "Assets/Models/Story";
        private const string MeshPath = MeshFolder + "/Story_Burrow_Hole_Mesh.asset";
        private const string PrefabFolder = "Assets/Prefabs/Story";
        private const string HolePath = PrefabFolder + "/Story_Burrow_Hole.prefab";
        private const string DigDustPath = PrefabFolder + "/Story_Dig_Dust.prefab";
        private const string DirtMatPath = "Assets/Materials/Bowling/Bowling_Dirt_Mat.mat";
        private const string DarkMatPath = "Assets/Materials/Whack/Whack_Hole_Mat.mat";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Puff_Burst_Out.prefab";
        private const string DustPath = "Assets/Prefabs/Enemies/Boss_FX/Boss_Scrape_Dust.prefab";
        private const string PopSoundPath = "Assets/Audio/SFX/Bug_Pop.wav";
        private const string DigSoundPath = "Assets/Audio/SFX/earth_rumble.wav";
        private const string SeedPath = "Assets/Prefabs/Items/Chest/Seed_Bracer.prefab";
        private const string BossFolder = "Assets/Prefabs/Enemies/Enemies_Smarter/";

        // Each room: its escape state, its fight (wall drop), the states that wait for its laughing pumpkin, its boss.
        private static readonly (string escape, string fight, string[] waiting, string boss)[] Rooms =
        {
            ("Boss Escape Mini", "Mini Enemy", new[] { "Mini Stuffs", "Mini Stuffs Chaos", "Mini Pumpkins" }, "Spider_Boss"),
            ("Boss Escape BR", "Enemy1 - Wait", new[] { "BR_Stuffs", "BR_Chaos", "BR_Pumpkins" }, "Spider_Boss_PumpkinButt"),
            ("Boss Escape LR", "Enemy2 - Wait", new[] { "LR - Stuffs", "LR - Chaos" }, "Cockroach_Boss"),
            ("Boss Escape Jenga", "Enemy 3 Stuffs", new[] { "Jenga Tower", "Jenga Tower 2" }, "Cockroach_Boss_PumpkinButt"),
        };

        [MenuItem("Tools/429 Game/Story/Set Up Boss Escape")]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(Tag + " Stop play mode first; nothing changed.");
                return;
            }
            var done = new List<string>();
            var hole = MakeHole(done);
            MakeSettings(hole, done);
            bool sceneChanged = Stages(done);
            Debug.Log(Tag + " " + (done.Count > 0 ? string.Join("; ", done) : "everything was already set up") + "." +
                      (sceneChanged ? " SAVE 2 Game Scene." : ""));
        }

        [MenuItem("Tools/429 Game/Story/Set Up Boss Escape", true)]
        private static bool CanSetUp() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/429 Game/Story/Undo Boss Escape")]
        public static void UndoEscape()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(Tag + " Stop play mode first; nothing changed.");
                return;
            }
            var gs = FindStages();
            if (gs == null) return;
            var f = gs.Fsm;
            Undo.RecordObject(gs, "Undo Boss Escape");
            var changes = new List<string>();
            var escapes = Rooms.Select(r => r.escape).ToArray();
            foreach (var room in Rooms)
            {
                foreach (var state in f.States)
                {
                    if (escapes.Contains(state.Name)) continue;
                    foreach (var t in state.Transitions)
                    {
                        if (t.ToState != room.escape) continue;
                        t.ToState = room.fight;
                        changes.Add($"'{state.Name}' {t.EventName} -> '{room.fight}'");
                    }
                }
            }
            int before = f.States.Length;
            f.States = f.States.Where(s => !escapes.Contains(s.Name)).ToArray();
            if (f.States.Length != before) changes.Add($"{before - f.States.Length} Boss Escape states removed");
            if (changes.Count == 0)
            {
                Debug.Log(Tag + " GAMESTAGES has no Boss Escape states; nothing changed.", gs);
                return;
            }
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            Debug.Log(Tag + " Back as before: " + string.Join("; ", changes) + ". SAVE 2 Game Scene.", gs);
        }

        [MenuItem("Tools/429 Game/Story/Undo Boss Escape", true)]
        private static bool CanUndo() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- GAMESTAGES ----

        private static bool Stages(List<string> done)
        {
            var gs = FindStages();
            if (gs == null) return false;
            var f = gs.Fsm;
            Undo.RecordObject(gs, "Set Up Boss Escape");
            var changes = new List<string>();
            foreach (var room in Rooms)
            {
                var fight = f.GetState(room.fight);
                if (fight == null)
                {
                    Debug.LogError($"{Tag} GAMESTAGES has no '{room.fight}' state; {room.escape} skipped.", gs);
                    continue;
                }
                var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossFolder + room.boss + ".prefab");
                if (boss == null)
                {
                    Debug.LogError($"{Tag} No {BossFolder}{room.boss}.prefab; {room.escape} skipped.");
                    continue;
                }

                var escape = f.GetState(room.escape);
                if (escape == null)
                {
                    var near = fight.Position;
                    escape = new FsmState(f)
                    {
                        Name = room.escape,
                        Position = new Rect(near.x + near.width + 40f, near.y - 70f, near.width, near.height),
                        Actions = new FsmStateAction[0],
                        Transitions = new FsmTransition[0],
                    };
                    f.States = f.States.Append(escape).ToArray();
                    changes.Add($"'{room.escape}' made");
                }
                escape.LoadActions();
                if (escape.ActionData.ActionCount != escape.Actions.Length)
                {
                    Debug.LogError($"{Tag} '{room.escape}' has actions that couldn't be read; it was left as it is.", gs);
                    continue;
                }
                var action = escape.Actions.OfType<StoryBossEscapes>().FirstOrDefault();
                if (action == null)
                {
                    action = new StoryBossEscapes();
                    action.Reset();
                    escape.Actions = escape.Actions.Append(action).ToArray();
                    changes.Add($"'{room.escape}' plays {room.boss}'s escape");
                }
                if (action.boss == null || action.boss.UseVariable || action.boss.Value != boss) action.boss = new FsmGameObject { Value = boss };
                if (action.fightState == null || action.fightState.Value != room.fight) action.fightState = new FsmString { Value = room.fight };
                escape.SaveActions();
                Point(escape, "FINISHED", room.fight, changes);

                // Its waiting states go to the escape instead of straight to the wall drop.
                foreach (var name in room.waiting)
                {
                    var waiting = f.GetState(name);
                    if (waiting == null)
                    {
                        Debug.LogWarning($"{Tag} GAMESTAGES has no '{name}' state (skipped).", gs);
                        continue;
                    }
                    foreach (var t in waiting.Transitions)
                    {
                        if (t.ToState != room.fight) continue;
                        t.ToState = room.escape;
                        changes.Add($"'{name}' {t.EventName} -> '{room.escape}'");
                    }
                }
                // Anything else that still starts the fight straight away (told, not changed).
                foreach (var state in f.States)
                {
                    if (state == escape || room.waiting.Contains(state.Name)) continue;
                    foreach (var t in state.Transitions)
                    {
                        if (t.ToState == room.fight)
                            Debug.Log($"{Tag} Note: '{state.Name}' {t.EventName} still goes straight to '{room.fight}' (no escape there).", gs);
                    }
                }
            }
            if (changes.Count == 0) return false;
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            done.Add("GAMESTAGES: " + string.Join("; ", changes));
            return true;
        }

        // The state's transition on this event goes to this state (made or re-pointed).
        private static void Point(FsmState state, string eventName, string to, List<string> changes)
        {
            var t = state.Transitions.FirstOrDefault(x => x.EventName == eventName);
            if (t == null)
            {
                var e = eventName == "FINISHED" ? FsmEvent.Finished : FsmEvent.GetFsmEvent(eventName);
                state.Transitions = state.Transitions.Append(new FsmTransition { FsmEvent = e, ToState = to }).ToArray();
                changes.Add($"'{state.Name}' {eventName} -> '{to}'");
            }
            else if (t.ToState != to)
            {
                t.ToState = to;
                changes.Add($"'{state.Name}' {eventName} -> '{to}'");
            }
        }

        private static PlayMakerFSM FindStages()
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.FsmName == "GAMESTAGES" && x.gameObject.scene.IsValid());
            if (gs == null)
            {
                Debug.LogError(Tag + " No GAMESTAGES FSM in the open scene (open 2 Game Scene).");
                return null;
            }
            if (!gs.Fsm.Initialized) gs.Fsm.Init(gs);
            return gs;
        }

        // ---- Settings ----

        private static void MakeSettings(GameObject hole, List<string> done)
        {
            var settings = AssetDatabase.LoadAssetAtPath<StoryBossEscapeSettings>(SettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                settings = ScriptableObject.CreateInstance<StoryBossEscapeSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                done.Add("made " + SettingsPath);
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(DustPath);
            var brown = MakeDigDust(source, done);
            settings.FillEmpty(
                AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath),
                AssetDatabase.LoadAssetAtPath<AudioClip>(PopSoundPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(DustPath),
                AssetDatabase.LoadAssetAtPath<AudioClip>(DigSoundPath),
                hole,
                AssetDatabase.LoadAssetAtPath<GameObject>(SeedPath));
            if (settings.UseDigDust(source, brown)) done.Add("the dig throws up " + brown.name);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // ---- The dig dust: the boss slide's dust with its white puffs turned to dirt (its brown chips stay) ----

        private static readonly Color DirtDark = new(0.3f, 0.21f, 0.13f, 1f), DirtLight = new(0.5f, 0.38f, 0.25f, 1f);

        private static GameObject MakeDigDust(GameObject source, List<string> done)
        {
            if (source == null) return null;
            var copy = (GameObject)Object.Instantiate(source);
            try
            {
                copy.name = "Story_Dig_Dust";
                foreach (var particles in copy.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particles.main;
                    main.startColor = Earthy(main.startColor);
                }
                bool had = AssetDatabase.LoadAssetAtPath<GameObject>(DigDustPath) != null;
                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, DigDustPath);
                if (!had) done.Add("made " + DigDustPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        // Grey colours (the smoke) become dirt; coloured ones (the brown chips) stay.
        private static ParticleSystem.MinMaxGradient Earthy(ParticleSystem.MinMaxGradient colours)
        {
            switch (colours.mode)
            {
                case ParticleSystemGradientMode.Color:
                    colours.color = Dirt(colours.color, 0.5f);
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    colours.colorMin = Dirt(colours.colorMin, 0f);
                    colours.colorMax = Dirt(colours.colorMax, 1f);
                    break;
                default:
                    if (colours.gradient != null) colours.gradient = Dirt(colours.gradient, 0.5f);
                    if (colours.gradientMin != null) colours.gradientMin = Dirt(colours.gradientMin, 0f);
                    if (colours.gradientMax != null) colours.gradientMax = Dirt(colours.gradientMax, 1f);
                    break;
            }
            return colours;
        }

        private static Color Dirt(Color c, float light)
        {
            float most = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), least = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            if (most - least > 0.15f || c.r + c.g + c.b < 1.2f) return c;
            var dirt = Color.Lerp(DirtDark, DirtLight, light);
            dirt.a = c.a;
            return dirt;
        }

        private static Gradient Dirt(Gradient g, float light)
        {
            var keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Dirt(keys[i].color, light);
            var copy = new Gradient { mode = g.mode };
            copy.SetKeys(keys, g.alphaKeys);
            return copy;
        }

        // ---- The hole: a dark middle in a ring of thrown-up dirt, radius 1 ----

        private static GameObject MakeHole(List<string> done)
        {
            Directory.CreateDirectory(MeshFolder);
            Directory.CreateDirectory(PrefabFolder);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool newMesh = mesh == null;
            if (newMesh) mesh = new Mesh();
            BuildHole(mesh);
            if (newMesh)
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
                done.Add("made " + MeshPath);
            }
            else EditorUtility.SetDirty(mesh);

            var dirt = AssetDatabase.LoadAssetAtPath<Material>(DirtMatPath);
            var dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMatPath);
            var go = new GameObject("Story_Burrow_Hole");
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var look = go.AddComponent<MeshRenderer>();
                look.sharedMaterials = new[] { dirt, dark };
                look.shadowCastingMode = ShadowCastingMode.Off;
                look.receiveShadows = true;
                look.lightProbeUsage = LightProbeUsage.Off;
                look.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.AddComponent<StoryBurrowHole>();
                bool had = AssetDatabase.LoadAssetAtPath<GameObject>(HolePath) != null;
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, HolePath);
                if (!had) done.Add("made " + HolePath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void BuildHole(Mesh mesh)
        {
            const int Segments = 20;
            var random = new System.Random(429);
            float Noise(float amount) => 1f + ((float)random.NextDouble() * 2f - 1f) * amount;
            var verts = new List<Vector3>();
            var dirtTris = new List<int>();
            var darkTris = new List<int>();

            var inner = new Vector3[Segments];
            var crest = new Vector3[Segments];
            var outer = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                inner[i] = dir * (0.6f * Noise(0.08f)) + Vector3.up * 0.012f;
                crest[i] = dir * (0.8f * Noise(0.07f)) + Vector3.up * (0.1f * Noise(0.35f));
                outer[i] = dir * (1f * Noise(0.08f));
            }

            // The dark middle.
            int middle = verts.Count;
            verts.Add(Vector3.up * 0.012f);
            int darkRing = verts.Count;
            verts.AddRange(inner);
            for (int i = 0; i < Segments; i++)
                Up(verts, darkTris, middle, darkRing + i, darkRing + (i + 1) % Segments);

            // The dirt: the hole's lip up to the crest, then down to the floor.
            int lip = verts.Count;
            verts.AddRange(inner);
            int top = verts.Count;
            verts.AddRange(crest);
            int foot = verts.Count;
            verts.AddRange(outer);
            for (int i = 0; i < Segments; i++)
            {
                int j = (i + 1) % Segments;
                Up(verts, dirtTris, lip + i, top + i, top + j);
                Up(verts, dirtTris, lip + i, top + j, lip + j);
                Up(verts, dirtTris, top + i, foot + i, foot + j);
                Up(verts, dirtTris, top + i, foot + j, top + j);
            }

            mesh.Clear();
            mesh.name = "Story_Burrow_Hole_Mesh";
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(dirtTris, 0);
            mesh.SetTriangles(darkTris, 1);
            mesh.SetUVs(0, verts.Select(v => new Vector2(v.x * 0.5f + 0.5f, v.z * 0.5f + 0.5f)).ToList());
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        // A triangle facing up (or outward and up), whatever order its corners came in.
        private static void Up(List<Vector3> verts, List<int> tris, int a, int b, int c)
        {
            var normal = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (normal.y < 0f) (b, c) = (c, b);
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }
    }
}
