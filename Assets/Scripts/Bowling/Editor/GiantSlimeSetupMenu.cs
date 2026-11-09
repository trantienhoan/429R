using System;
using System.Collections.Generic;
using System.Linq;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Set Up Giant Slime And Showdown: makes Gate 2's ("Strike the Slimes") slimes in
    /// Prefabs/Bowling. Safe to run again (it updates them in place, so links to them stay good).
    /// - Giant_Flying_Slime: a variant of Prefabs/Enemies/Flying_Slime (the look only), slower wing beats, brighter glow,
    ///   fewer drips, plus a looping wing sound (bee_fly) and the Giant Flying Slime puppet. No collider, no brain, no
    ///   Enemy tag. Root scale 1 (the stage sizes it).
    /// - Slime_Showdown_Red / Blue / Green / Yellow: variants of the PlayGround's Slime_Big_Red, Slime_Big_Blue and
    ///   Slime_Big (Green, and Yellow in Slime_Yellow_Mat), bigger, with their own health and no small splits. The
    ///   PlayGround's own slimes are never changed.
    /// </summary>
    public static class GiantSlimeSetupMenu
    {
        private const string MenuPath = "Tools/429 Game/Bowling/Set Up Giant Slime And Showdown";
        private const string Folder = "Assets/Prefabs/Bowling";
        private const string GiantPath = Folder + "/Giant_Flying_Slime.prefab";
        private const string ShowdownPath = Folder + "/Slime_Showdown_{0}.prefab";

        private const string FlyingSlimePath = "Assets/Prefabs/Enemies/Flying_Slime.prefab";
        private const string PlayGround = "Assets/Prefabs/Enemies/PLAYGROUND/";
        private const string BigPath = PlayGround + "Slime_Big.prefab";
        private const string BigRedPath = PlayGround + "Slime_Big_Red.prefab";
        private const string BigBluePath = PlayGround + "Slime_Big_Blue.prefab";
        private const string YellowMaterialPath = "Assets/Materials/Flying_Slime/Slime_Yellow_Mat.mat";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string Kenney = "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/";

        private const string WingSound = Sfx + "bee_fly.wav";
        private static readonly string[] PoopSounds = { Kenney + "slime_000.ogg", Kenney + "slime_001.ogg" };
        private static readonly string[] LaughSounds = { Sfx + "cute_laugh.wav", Sfx + "cute_laugh_2.wav" };
        private static readonly string[] HitSounds = { Kenney + "slime_001.ogg" };
        private static readonly string[] BurstSounds = { Sfx + "balloon_explode_1.wav" };
        private static readonly string[] WingOffSounds = { Sfx + "Balloon_Pop.wav" };
        private static readonly string[] FallSounds = { Sfx + "Cartoon_fall_1.wav", Sfx + "Cartoon_fall_2.wav" };
        private static readonly string[] LandSounds = { Sfx + "pumpkin_face_splat.wav" };
        private static readonly string[] LeaveSounds = { Sfx + "fairy_fly_away.wav" };

        private sealed class Showdown
        {
            public string colour;
            public string parent;
            public Vector2 scale;
            public Vector2Int health;
            public bool yellow;
            // Its own pace and trait (user, 2026-11-08: 4 speeds and traits, so they reach the player one by one).
            public float speed = 1f;          // its size class's Speed
            public float run = 3f;            // Run Speed (m/s at Speed 1, before the brain's 0.7)
            public float rest = 0.4f;         // seconds between its attacks
            public float specialBelow = -1f;  // its special from this share of health (-1: as its parent)
            public float specialDistance = -1f;
            public string specialFrom;        // copy the special (e.g. the Ticking Bomb) from this prefab's brain
            public string trait;
        }

        // Bigger than the PlayGround's, no small splits, and each its own pace and trait, fastest first:
        // Green 3.6 m/s, Blue 2.4, Red 1.6 (charges), Yellow 1.1 (bomb): over the 18 m lanes about 5 / 9 / 13 / 18 s.
        private static readonly Showdown[] Showdowns =
        {
            new Showdown { colour = "Red", parent = BigRedPath, scale = new Vector2(2.3f, 2.5f), health = new Vector2Int(100, 110),
                speed = 0.72f, run = 3.2f, rest = 0.4f, specialBelow = 1f, specialDistance = 7f, trait = "Bull: slow walk, charges from 7 m at any health (pushes)" },
            new Showdown { colour = "Blue", parent = BigBluePath, scale = new Vector2(1.9f, 2.1f), health = new Vector2Int(70, 80),
                speed = 1.15f, run = 3f, rest = 0.4f, trait = "Sticky: slowing globs + sticky bite, Giant Web" },
            new Showdown { colour = "Green", parent = BigPath, scale = new Vector2(1.7f, 1.9f), health = new Vector2Int(45, 55),
                speed = 1.7f, run = 3f, rest = 0.15f, trait = "Quick: fastest, rapid little bites" },
            new Showdown { colour = "Yellow", parent = BigPath, scale = new Vector2(2.5f, 2.7f), health = new Vector2Int(90, 100), yellow = true,
                speed = 0.5f, run = 3.2f, rest = 0.6f, specialFrom = PlayGround + "Slime_Big_Yellow.prefab", specialBelow = 0.3f,
                trait = "Bomb: slowest and biggest, heavy slam, Ticking Bomb at low health" },
        };

        [MenuItem(MenuPath)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Bowling] Stop play mode first.");
                return;
            }
            Run();
        }

        [MenuItem(MenuPath, true)]
        private static bool MenuCheck() => !EditorApplication.isPlaying;

        /// <summary>Makes Giant_Flying_Slime and the 4 Slime_Showdown_* prefabs. True when all 5 were saved.</summary>
        public static bool Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Bowling] Stop play mode first.");
                return false;
            }
            var notes = new List<string>();
            var needed = new List<string> { FlyingSlimePath, BigPath, BigRedPath, BigBluePath, YellowMaterialPath, PuffPath, WingSound };
            needed.AddRange(PoopSounds);
            needed.AddRange(LaughSounds);
            needed.AddRange(BurstSounds);
            needed.AddRange(WingOffSounds);
            needed.AddRange(FallSounds);
            needed.AddRange(LandSounds);
            needed.AddRange(LeaveSounds);
            var missing = needed.Distinct().Where(p => AssetDatabase.LoadAssetAtPath<Object>(p) == null).ToList();
            if (missing.Count > 0)
            {
                Debug.LogError("[Bowling] Set Up Giant Slime And Showdown: missing, nothing built: " + string.Join(", ", missing));
                return false;
            }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Bowling");

            int made = 0;
            if (Build(GiantPath, FlyingSlimePath, "Giant_Flying_Slime", root => FillGiant(root, notes), notes)) made++;
            foreach (var showdown in Showdowns)
            {
                var path = string.Format(ShowdownPath, showdown.colour);
                var name = "Slime_Showdown_" + showdown.colour;
                if (Build(path, showdown.parent, name, root => FillShowdown(root, showdown, notes), notes)) made++;
            }
            AssetDatabase.SaveAssets();

            bool ok = made == 1 + Showdowns.Length;
            var text = "[Bowling] Set Up Giant Slime And Showdown: " + made + "/" + (1 + Showdowns.Length) + " prefabs saved.\n" + string.Join("\n", notes);
            if (ok) Debug.Log(text);
            else Debug.LogError(text);
            return ok;
        }

        // A variant of Parent at Path: the existing one updated in place (its links kept), or a new one made from an
        // instance of Parent in a preview scene. Fill sets it up. True when saved.
        private static bool Build(string path, string parentPath, string name, Action<GameObject> fill, List<string> notes)
        {
            var parent = AssetDatabase.LoadAssetAtPath<GameObject>(parentPath);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool inPlace = existing != null && PrefabUtility.GetPrefabAssetType(existing) == PrefabAssetType.Variant &&
                           PrefabUtility.GetCorrespondingObjectFromSource(existing) == parent;
            if (existing != null && !inPlace) notes.Add($"{name}: the old one was not a variant of {parentPath}: made anew");

            var scene = default(UnityEngine.SceneManagement.Scene);
            GameObject root = null;
            try
            {
                if (inPlace) root = PrefabUtility.LoadPrefabContents(path);
                else
                {
                    scene = EditorSceneManager.NewPreviewScene();
                    root = (GameObject)PrefabUtility.InstantiatePrefab(parent, scene);
                }
                root.name = name;
                fill(root);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                notes.Add(saved ? $"{name}: {(inPlace ? "updated" : "made")} ({path})" : $"ERROR: {name}: could not save {path}");
                return saved;
            }
            catch (Exception e)
            {
                notes.Add($"ERROR: {name} not built: {e.Message}");
                Debug.LogException(e);
                return false;
            }
            finally
            {
                if (inPlace)
                {
                    if (root != null) PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    if (root != null) Object.DestroyImmediate(root);
                    if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        // ---------- Giant_Flying_Slime ----------

        private static void FillGiant(GameObject root, List<string> notes)
        {
            root.tag = "Untagged";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // A puppet: nothing that hits, is hit, or thinks.
            var removed = Strip(root);
            int untagged = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.CompareTag("Untagged")) continue;
                t.gameObject.tag = "Untagged";
                untagged++;
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            var look = root.GetComponent<FlyingSlime>();
            if (look == null) throw new InvalidOperationException("Flying_Slime has no Flying Slime component");
            var lso = new SerializedObject(look);
            P(lso, "flapsPerSecond").floatValue = 2f;
            P(lso, "fastFlapsPerSecond").floatValue = 3.5f;
            P(lso, "fastSpeed").floatValue = 9f;
            P(lso, "glow").floatValue = 0.35f;
            P(lso, "dripsPerSecond").floatValue = 0.6f;
            P(lso, "destroyOnPop").boolValue = true;
            lso.ApplyModifiedPropertiesWithoutUndo();

            var wingLeft = FindPart(root.transform, "Slime_Wing_L");
            var wingRight = FindPart(root.transform, "Slime_Wing_R");
            if (wingLeft == null || wingRight == null) notes.Add("WARNING: Giant_Flying_Slime: wing(s) not found under Model: it can't lose them");

            var voice = root.GetComponent<AudioSource>();
            if (voice == null) voice = root.AddComponent<AudioSource>();
            voice.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WingSound);
            voice.loop = true;
            voice.playOnAwake = false;
            voice.volume = 0f;
            voice.pitch = 0.55f;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 3f;
            voice.maxDistance = 35f;
            voice.dopplerLevel = 0f;

            var giant = root.GetComponent<GiantFlyingSlime>();
            if (giant == null) giant = root.AddComponent<GiantFlyingSlime>();
            var so = new SerializedObject(giant);
            P(so, "look").objectReferenceValue = look;
            P(so, "wingLeft").objectReferenceValue = wingLeft;
            P(so, "wingRight").objectReferenceValue = wingRight;
            P(so, "voice").objectReferenceValue = voice;
            P(so, "puff").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath);
            Clips(P(so, "poopSounds"), PoopSounds);
            Clips(P(so, "laughSounds"), LaughSounds);
            Clips(P(so, "hitSounds"), HitSounds);
            Clips(P(so, "burstSounds"), BurstSounds);
            Clips(P(so, "wingOffSounds"), WingOffSounds);
            Clips(P(so, "fallSounds"), FallSounds);
            Clips(P(so, "landSounds"), LandSounds);
            Clips(P(so, "leaveSounds"), LeaveSounds);
            P(so, "bob").floatValue = 0.25f;
            P(so, "bobSpeed").floatValue = 2f;
            P(so, "bank").floatValue = 12f;
            P(so, "oneWingRoll").floatValue = 20f;
            so.ApplyModifiedPropertiesWithoutUndo();

            int colliders = root.GetComponentsInChildren<Collider>(true).Length;
            notes.Add($"Giant_Flying_Slime: wings {(wingLeft != null ? "L" : "-")}{(wingRight != null ? "R" : "-")}, colliders {colliders}, " +
                      $"untagged {untagged}, removed: {(removed.Count > 0 ? string.Join(", ", removed) : "nothing")}");
        }

        // Removes every Collider, Rigidbody, Enemy Hit Damage and PlayMaker FSM under it. Returns "Type xN" lines.
        private static List<string> Strip(GameObject root)
        {
            var counts = new Dictionary<string, int>();
            var parts = root.GetComponentsInChildren<Component>(true)
                .Where(c => c != null && (c is Collider || c is Rigidbody || c is PlayMakerFSM || c.GetType().Name == "EnemyHitDamage"))
                .ToList();
            foreach (var c in parts)
            {
                var name = c.GetType().Name;
                try
                {
                    Object.DestroyImmediate(c);
                    counts[name] = counts.TryGetValue(name, out var n) ? n + 1 : 1;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bowling] Giant_Flying_Slime: could not remove {name} on {c.name}: {e.Message}");
                }
            }
            return counts.Select(p => p.Value > 1 ? $"{p.Key} x{p.Value}" : p.Key).ToList();
        }

        // ---------- Slime_Showdown_* ----------

        private static void FillShowdown(GameObject root, Showdown showdown, List<string> notes)
        {
            var brain = root.GetComponent<SpiderBrain>();
            if (brain == null) throw new InvalidOperationException("no Spider Brain on " + showdown.parent);
            var so = new SerializedObject(brain);
            var boss = P(so, "boss");
            R(boss, "scale").vector2Value = showdown.scale;
            R(boss, "health").vector2IntValue = showdown.health;
            R(boss, "speed").floatValue = showdown.speed;
            R(boss, "rest").floatValue = showdown.rest;
            P(so, "runSpeed").floatValue = showdown.run;
            if (!string.IsNullOrEmpty(showdown.specialFrom))
            {
                var from = AssetDatabase.LoadAssetAtPath<GameObject>(showdown.specialFrom);
                var fromBrain = from != null ? from.GetComponent<SpiderBrain>() : null;
                if (fromBrain == null) notes.Add($"WARNING: Slime_Showdown_{showdown.colour}: no brain on {showdown.specialFrom} (special not copied)");
                else
                {
                    var fso = new SerializedObject(fromBrain);
                    var it = fso.GetIterator();
                    bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false;
                        if (it.name.StartsWith("special") || it.name.StartsWith("bomb")) so.CopyFromSerializedProperty(it);
                    }
                }
            }
            if (showdown.specialBelow >= 0f) P(so, "specialBelowHealth").floatValue = showdown.specialBelow;
            if (showdown.specialDistance >= 0f) P(so, "specialDistance").floatValue = showdown.specialDistance;
            so.ApplyModifiedPropertiesWithoutUndo();

            var body = root.GetComponent<SlimeBody>();
            if (body != null)
            {
                var bso = new SerializedObject(body);
                P(bso, "splitInto").objectReferenceValue = null;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }
            else notes.Add($"WARNING: Slime_Showdown_{showdown.colour}: no Slime Body (splits not turned off)");

            // Its Health FSM starts at the lowest (the brain picks its health at Start; a rise shows no damage number).
            bool fsmSet = false;
            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (!fsm.FsmName.EndsWith("Health")) continue;
                var fso = new SerializedObject(fsm);
                var floats = fso.FindProperty("fsm.variables.floatVariables");
                if (floats == null || !floats.isArray) continue;
                for (int i = 0; i < floats.arraySize; i++)
                {
                    var item = floats.GetArrayElementAtIndex(i);
                    if (item.FindPropertyRelative("name")?.stringValue != "health") continue;
                    var value = item.FindPropertyRelative("value");
                    if (value == null) continue;
                    value.floatValue = showdown.health.x;
                    fsmSet = true;
                }
                fso.ApplyModifiedPropertiesWithoutUndo();
            }
            if (!fsmSet) notes.Add($"WARNING: Slime_Showdown_{showdown.colour}: no Health FSM 'health' found (left as is)");

            int painted = 0;
            if (showdown.yellow)
            {
                var yellow = AssetDatabase.LoadAssetAtPath<Material>(YellowMaterialPath);
                var model = root.transform.Find("Model");
                foreach (var r in (model != null ? model : root.transform).GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = yellow;
                    r.sharedMaterials = mats;
                    painted++;
                }
            }

            notes.Add($"Slime_Showdown_{showdown.colour}: scale {showdown.scale.x:0.0}-{showdown.scale.y:0.0}, health " +
                      $"{showdown.health.x}-{showdown.health.y}, runs {showdown.run * showdown.speed * 0.7f:0.0} m/s, {showdown.trait}, no splits{(showdown.yellow ? $", Slime_Yellow_Mat on {painted} renderer(s)" : "")}");
        }

        // ---------- Helpers ----------

        private static Transform FindPart(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static void Clips(SerializedProperty list, string[] paths)
        {
            var clips = paths.Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p)).Where(c => c != null).ToArray();
            list.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        private static SerializedProperty P(SerializedObject so, string name) =>
            so.FindProperty(name) ?? throw new InvalidOperationException($"no field '{name}' on {so.targetObject.GetType().Name}");

        private static SerializedProperty R(SerializedProperty parent, string name) =>
            parent.FindPropertyRelative(name) ?? throw new InvalidOperationException($"no field '{parent.propertyPath}.{name}'");
    }
}
