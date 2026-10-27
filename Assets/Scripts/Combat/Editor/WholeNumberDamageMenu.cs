using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Bonus;
using Game.Combat;
using Game.Enemies;
using Game.Leveling;
using Game.PlayGround;
using Game.Weapons;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Whole-number damage (user, 2026-10-27): a punch does 1, a hard punch 2, a weapon its own whole number (Hit
    /// Settings, Weapon Power). Two steps, run in order with 2 Game Scene open, then save the scene:
    /// 1. Halve Health And Blasts (once): hits now do about half what they did (1 or 2 instead of 1-2 or 3-5), so every
    ///    bug, boss, piece of furniture, door and pumpkin that took those hits gets half its health (whole, at least 1),
    ///    and every blast half its damage. Props (books, pumpkins, weapons) keep theirs: they lost 1 a hit before too.
    ///    Marked done in the Hit Settings asset, so running it again changes nothing.
    /// 2. Props Use Hit Damage: every prop whose Damage FSM still counts its own hits (1 for any bump faster than its
    ///    Check Force speed) gets Enemy Hit Damage (Furniture ticked) to count them by the same rule as everything else.
    ///    Its Toughness is its old speed / the Hit Settings' Min Hit Power, so the same bump still just counts; a hard one
    ///    now counts 2. The Damage FSM keeps its smoke, sound and shake, and its FIREKILL / Fairy Shield kills.
    /// The whack pumpkins (Fair Hits), the chase pumpkins (Touch Only Damage) and the laughing pumpkin keep their own rules.
    /// </summary>
    public static class WholeNumberDamageMenu
    {
        private const string HitSettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private const string Prefabs = "Assets/Prefabs";

        // ---------- 1. Halve health and blasts ----------

        [MenuItem("Tools/429 Game/Combat/Whole Numbers/1. Halve Health And Blasts (once)")]
        public static void HalveHealthAndBlasts()
        {
            if (!Ready()) return;
            var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<HitSettings>(HitSettingsPath));
            var done = settings.FindProperty("healthHalved");
            if (done.boolValue)
            {
                Debug.Log("[Whole Numbers] Health and blasts were halved already. Nothing changed.");
                return;
            }

            var log = new StringBuilder();
            int changed = HalvePrefabs(PrefabPaths(), log);

            var scene = SceneManager.GetActiveScene();
            var sceneNotes = new List<string>();
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    var notes = new List<string>();
                    HalveOn(t.gameObject, false, notes);
                    if (t.TryGetComponent(out PlayerLevel level))
                    {
                        var so = new SerializedObject(level);
                        var max = so.FindProperty("maxBlastDamage");
                        if (max.floatValue > 5f)
                        {
                            max.floatValue = 5f;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            notes.Add("max throw blast 5");
                        }
                    }
                    if (notes.Count > 0) sceneNotes.Add($"  {t.name}: {string.Join(", ", notes)}");
                }
            }
            if (sceneNotes.Count > 0) EditorSceneManager.MarkSceneDirty(scene);

            done.boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Whole Numbers] Halved on {changed} prefab(s) and {sceneNotes.Count} scene object(s) in " +
                      $"'{scene.name}' (save the scene):\n{log}{string.Join("\n", sceneNotes)}");
        }

        // Opened as prefab contents (bases before variants): a component from another prefab (a variant's base, a nested
        // prefab, a model) only has its own overrides halved, the rest comes from its halved prefab. Added Only: just the
        // components added here. Returns how many prefabs changed.
        private static int HalvePrefabs(IEnumerable<string> paths, StringBuilder log, bool addedOnly = false)
        {
            int changed = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var notes = new List<string>();
                    foreach (var t in root.GetComponentsInChildren<Transform>(true)) HalveOn(t.gameObject, addedOnly, notes);
                    if (notes.Count == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                    log.AppendLine($"  {path.Substring(Prefabs.Length + 1)}: {string.Join(", ", notes)}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            return changed;
        }

        // The repair of 2026-10-27: the first run halved the variants through their loaded assets, which saved their
        // inherited (old) values as overrides; those files were put back from the backup. This halves every variant's
        // own overrides once, the right way.
        public static string HalveVariantsOnly()
        {
            var log = new StringBuilder();
            var variants = PrefabPaths().Where(p =>
                PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant);
            int changed = HalvePrefabs(variants, log);
            return $"{changed} variant(s):\n{log}";
        }

        // The second repair: a prefab made on a model (or another prefab) counts as a variant, and the components added
        // on top of it (its health, its blast) were skipped by both runs. This halves just those, once.
        public static string HalveAddedOnVariants()
        {
            var log = new StringBuilder();
            var variants = PrefabPaths().Where(p =>
                PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant);
            int changed = HalvePrefabs(variants, log, true);
            return changed + " prefab(s):" + System.Environment.NewLine + log;
        }

        // Halves the health and blast numbers of the components on this object. A component from a prefab (a variant's
        // base, a nested prefab, a model, or a scene instance's prefab) only has its own overrides halved (the rest comes
        // from its prefab); one added here is halved in full. Added Only: only those.
        private static void HalveOn(GameObject go, bool addedOnly, List<string> notes)
        {
            bool takesHits = go.TryGetComponent<EnemyHitDamage>(out _) || go.TryGetComponent<LaughingPumpkin>(out _);
            if (takesHits)
            {
                foreach (var fsm in go.GetComponents<PlayMakerFSM>())
                {
                    if (!fsm.FsmName.EndsWith("Health")) continue;
                    bool inherited = FromPrefab(fsm);
                    if (addedOnly && inherited) continue;
                    var so = new SerializedObject(fsm);
                    var floats = so.FindProperty("fsm.variables.floatVariables");
                    for (int i = 0; i < floats.arraySize; i++)
                    {
                        var variable = floats.GetArrayElementAtIndex(i);
                        if (variable.FindPropertyRelative("name").stringValue != "health") continue;
                        Halve(variable.FindPropertyRelative("value"), inherited, "health", notes);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Halve<SpiderBrain>(go, addedOnly, notes, "small.health", "normal.health", "boss.health", "anomalyHealth");
            Halve<BonusRound>(go, addedOnly, notes, "giantHealth");
            Halve<NestingPumpkin>(go, addedOnly, notes, "health");
            Halve<GhostTree>(go, addedOnly, notes, "health");
            Halve<LaughingPumpkin>(go, addedOnly, notes, "candyEveryHealth", "handBlastDamage");
            Halve<Grenade>(go, addedOnly, notes, "damage", "chargeDamage");
            Halve<BoongSmoke>(go, addedOnly, notes, "damage");
            Halve<Overheat>(go, addedOnly, notes, "damage");
            Halve<BowlingPumpkin>(go, addedOnly, notes, "smashDamage", "blastDamage");
            Halve<Fist>(go, addedOnly, notes, "boongBlastDamage");
        }

        private static void Halve<T>(GameObject go, bool addedOnly, List<string> notes, params string[] names) where T : Component
        {
            foreach (var component in go.GetComponents<T>())
            {
                bool inherited = FromPrefab(component);
                if (addedOnly && inherited) continue;
                var so = new SerializedObject(component);
                foreach (var name in names) Halve(so.FindProperty(name), inherited, $"{typeof(T).Name}.{name}", notes);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Halve(SerializedProperty p, bool inherited, string label, List<string> notes)
        {
            if (p == null || (inherited && !p.prefabOverride)) return;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float:
                    if (p.floatValue <= 0f) return;
                    float was = p.floatValue;
                    p.floatValue = Half(was);
                    notes.Add($"{label} {was:0.##}->{p.floatValue:0}");
                    break;
                case SerializedPropertyType.Vector2Int:
                    var v = p.vector2IntValue;
                    if (v == Vector2Int.zero) return;
                    p.vector2IntValue = new Vector2Int((int)Half(v.x), (int)Half(v.y));
                    notes.Add($"{label} {v}->{p.vector2IntValue}");
                    break;
                case SerializedPropertyType.Vector2:
                    var f = p.vector2Value;
                    if (f == Vector2.zero) return;
                    p.vector2Value = new Vector2(Half(f.x), Half(f.y));
                    notes.Add($"{label} {f}->{p.vector2Value}");
                    break;
            }
        }

        // The component comes from another prefab (or a model), not added on this one.
        private static bool FromPrefab(Component component) => PrefabUtility.GetCorrespondingObjectFromSource(component) != null;

        // Half, rounded (2.5 -> 3), whole, at least 1.
        private static float Half(float value) => value <= 0f ? value : Mathf.Max(1f, Mathf.Floor(value / 2f + 0.5f));

        // ---------- 2. Props use hit damage ----------

        private const string DamageFsm = "Damage";
        private const string HitState = "Deal 1 damage";

        [MenuItem("Tools/429 Game/Combat/Whole Numbers/2. Props Use Hit Damage")]
        public static void PropsUseHitDamage()
        {
            if (!Ready()) return;
            var settingsAsset = AssetDatabase.LoadAssetAtPath<HitSettings>(HitSettingsPath);
            float minPower = new SerializedObject(settingsAsset).FindProperty("minHitPower").floatValue;
            var log = new StringBuilder();
            var skipped = new List<string>();
            int converted = 0;

            foreach (var path in PrefabPaths())
            {
                // Opened fresh: a variant of a converted prop already has it all from its base.
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!NeedsConverting(root, out var why))
                    {
                        if (why != null) skipped.Add($"{root.name} ({why})");
                        continue;
                    }
                    string note = Convert(root, settingsAsset, minPower);
                    if (note == null) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    converted++;
                    log.AppendLine($"  {path.Substring(Prefabs.Length + 1)}: {note}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            // Props made in the scene itself (not from a prefab).
            var scene = SceneManager.GetActiveScene();
            int inScene = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var fsm in go.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (fsm.FsmName != DamageFsm || PrefabUtility.IsPartOfPrefabInstance(fsm)) continue;
                    if (!NeedsConverting(fsm.gameObject, out _)) continue;
                    string note = Convert(fsm.gameObject, settingsAsset, minPower);
                    if (note == null) continue;
                    inScene++;
                    log.AppendLine($"  (scene) {fsm.name}: {note}");
                }
            }
            if (inScene > 0) EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Whole Numbers] {converted} prop prefab(s) and {inScene} scene prop(s) now count hits by the hit " +
                      $"rule (toughness = old speed / {minPower}):\n{log}" +
                      (skipped.Count > 0 ? $"Left alone: {string.Join(", ", skipped)}" : ""));
        }

        // A prop whose Damage FSM counts its own hits, and nothing else counts them. Why: said for the left-alone ones
        // worth mentioning (their own rules), null otherwise.
        private static bool NeedsConverting(GameObject go, out string why)
        {
            why = null;
            var fsm = go.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == DamageFsm);
            if (fsm == null || go.TryGetComponent<EnemyHitDamage>(out _)) return false;
            if (go.TryGetComponent<FairHits>(out _)) { why = "whack pumpkin"; return false; }
            if (go.TryGetComponent<TouchOnlyDamage>(out _)) { why = "chase pumpkin"; return false; }
            if (go.TryGetComponent<LaughingPumpkin>(out _)) { why = "laughing pumpkin"; return false; }
            if (go.TryGetComponent<SpiderBrain>(out _) || go.TryGetComponent<PumpkinBossBrain>(out _)) return false;
            var state = fsm.FsmStates.FirstOrDefault(s => s.Name == HitState);
            if (state == null) return false;
            state.LoadActions();
            return state.Actions.Any(a => a.Enabled && a is FloatSubtract) && state.Actions.Any(a => a.Enabled && a is SetFsmFloat);
        }

        // Adds the Enemy Hit Damage and quiets the Damage FSM's counting. What was done, or null if it couldn't be.
        private static string Convert(GameObject go, HitSettings settingsAsset, float minPower)
        {
            var component = go.GetComponents<PlayMakerFSM>().First(f => f.FsmName == DamageFsm);
            var fsm = component.Fsm;
            foreach (var s in fsm.States) s.LoadActions();
            var hit = fsm.States.First(s => s.Name == HitState);

            // Where its health is: the Get Fsm Float of the hit state, on its own object.
            var get = hit.Actions.OfType<GetFsmFloat>().FirstOrDefault(a => a.Enabled);
            if (get == null || get.gameObject == null || get.gameObject.OwnerOption != OwnerDefaultOption.UseOwner)
                return null;
            string healthFsm = get.fsmName.Value, healthVariable = get.variableName.Value;
            if (go.GetComponents<PlayMakerFSM>().All(f => f.FsmName != healthFsm)) return null;

            // Its old speed: the Check Force compare whose "more" leads to the hit state. None: any knock counts.
            float speed = 0f;
            string shake = null;
            foreach (var state in fsm.States)
            {
                foreach (var compare in state.Actions.OfType<FloatCompare>().Where(a => a.Enabled))
                {
                    string to = compare.greaterThan == null ? null : state.Transitions.FirstOrDefault(t => t.EventName == compare.greaterThan.Name)?.ToState;
                    if (to == HitState) speed = Mathf.Max(speed, compare.float2.Value);
                }
            }
            shake = fsm.States.FirstOrDefault(s => s.Name.ToLowerInvariant() == "shake")?.Name;

            // Bumps by anything count only if an untagged Collision Event listened for them (a tagged one: only those).
            var collisions = fsm.States.SelectMany(s => s.Actions.OfType<CollisionEvent>()).Where(a => a.Enabled).ToList();
            bool anyBump = collisions.Count == 0 || collisions.Any(a => string.IsNullOrEmpty(a.collideTag.Value) || a.collideTag.Value == "Untagged");

            // The FSM: Collision Events off (Trigger Events, the FIREKILL / Fairy Shield kills, stay on); the hit state no
            // longer lowers the health; transitions only the switched-off actions sent are gone.
            foreach (var state in fsm.States)
            {
                var off = new HashSet<string>();
                foreach (var action in state.Actions)
                {
                    if (action is CollisionEvent listens && listens.Enabled)
                    {
                        listens.Enabled = false;
                        if (listens.sendEvent != null) off.Add(listens.sendEvent.Name);
                    }
                    else if (state == hit && action.Enabled && (action is GetFsmFloat || action is FloatSubtract || action is SetFsmFloat))
                    {
                        action.Enabled = false;
                    }
                }
                if (off.Count == 0) continue;
                var stillSent = new HashSet<string>();
                bool stillRuns = false;
                foreach (var action in state.Actions.Where(a => a.Enabled))
                {
                    stillRuns = true;
                    foreach (var field in action.GetType().GetFields())
                    {
                        if (field.GetValue(action) is FsmEvent e && e != null) stillSent.Add(e.Name);
                    }
                }
                state.Transitions = state.Transitions
                    .Where(t => stillSent.Contains(t.EventName) || (t.EventName == FsmEvent.Finished.Name && stillRuns) ||
                                (!off.Contains(t.EventName) && t.EventName != FsmEvent.Finished.Name))
                    .ToArray();
            }

            var events = fsm.Events.GroupBy(e => e.Name).Select(g => g.First()).ToList();
            var globals = fsm.GlobalTransitions.ToList();
            void Global(string eventName, string toState)
            {
                if (toState == null || globals.Any(t => t.EventName == eventName)) return;
                var e = FsmEvent.GetFsmEvent(eventName);
                if (events.All(x => x.Name != eventName)) events.Add(e);
                globals.Add(new FsmTransition { FsmEvent = e, ToState = toState });
            }
            Global("Damage", HitState);
            Global("ShakeSmall", shake);
            fsm.Events = events.ToArray();
            fsm.GlobalTransitions = globals.ToArray();
            foreach (var s in fsm.States) s.SaveActions();
            EditorUtility.SetDirty(component);

            var hits = go.AddComponent<EnemyHitDamage>();
            var so = new SerializedObject(hits);
            float toughness = speed > 0f ? Mathf.Max(0.1f, speed / Mathf.Max(0.1f, minPower)) : 1f;
            so.FindProperty("settings").objectReferenceValue = settingsAsset;
            so.FindProperty("toughness").floatValue = toughness;
            so.FindProperty("hitCooldown").floatValue = 0.3f;
            so.FindProperty("knockBack").boolValue = false;
            so.FindProperty("exp").intValue = 0;
            so.FindProperty("expOrbs").boolValue = false;
            so.FindProperty("hideHitEffect").boolValue = true;
            so.FindProperty("healthFsm").stringValue = healthFsm;
            so.FindProperty("healthVariable").stringValue = healthVariable;
            so.FindProperty("hitEvent").stringValue = "Damage";
            so.FindProperty("weakHitEvent").stringValue = "ShakeSmall";
            // The Damage FSM stays on for its smoke, sound, shake and kills.
            so.FindProperty("oldDamageFsm").stringValue = "";
            so.FindProperty("furniture").boolValue = true;
            so.FindProperty("bumpShare").floatValue = anyBump ? 1f : 0f;
            so.FindProperty("wieldedShare").floatValue = 1f;
            so.FindProperty("settleSeconds").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return $"old speed {speed:0.#} -> toughness {toughness:0.##}{(anyBump ? "" : ", only the player's hits")}{(shake != null ? ", weak hits shake" : "")}";
        }

        // ---------- shared ----------

        private static bool Ready()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Whole Numbers] Stop play mode first.");
                return false;
            }
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[Whole Numbers] Close the prefab you're editing (Prefab Mode) first, then run it again.");
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<HitSettings>(HitSettingsPath) == null)
            {
                Debug.LogError($"[Whole Numbers] No Hit Settings at {HitSettingsPath}.");
                return false;
            }
            return true;
        }

        // Every prefab, bases before their variants.
        private static List<string> PrefabPaths()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            int Depth(string path)
            {
                int depth = 0;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                while (go != null && PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Variant && depth < 10)
                {
                    go = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    depth++;
                }
                return depth;
            }
            var depths = paths.ToDictionary(p => p, Depth);
            return paths.OrderBy(p => depths[p]).ThenBy(p => p).ToList();
        }
    }
}
