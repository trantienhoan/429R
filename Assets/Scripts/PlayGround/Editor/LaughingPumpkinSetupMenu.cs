using System.Collections.Generic;
using Game.Combat;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Set Up Laughing Pumpkins.
    /// The cauldron's laughing pumpkins (Prefabs/Items/Breakable: Pumpkin_8, 9, 10 for rounds 1, 2, 3) get the catch-me
    /// game (Laughing Pumpkin, user 2026-11-07): they run only a few metres (3 / 4 / 5 m), then are stunned with stars
    /// (4 / 3.5 / 3 s), hide close by (4.5 / 5.5 / 6.5 m) inside the PlayGround, are pulled back in if they leave it, and
    /// turn into the real Pumpkin Boss after 300 s uncaught. They become grabbable from close range only (Near Grab
    /// Only), drop a candy every metre they run, and get a sane mass.
    /// Pumpkin_9 and Pumpkin_10's Health FSM no longer sets PlayGround_Manager's Pumpkin_09 / Pumpkin_10 when they break
    /// (state Drop_Crack): the seeds grow the PlayGround now. Pumpkin_Big (the old round 4) is left alone. Safe to run again.
    /// </summary>
    public static class LaughingPumpkinSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Items/Breakable/";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string Fx = "Assets/Lana Studio/Hyper Casual FX/Prefabs/";
        private const string HitSettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private const string BossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        private const string StunStarsPath = "Assets/Prefabs/Enemies/Hit_FX/Stun_Stars.prefab";
        private const string RumblePath = Sfx + "Low,_rumbling_hmmmm.wav";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private static readonly string[] CloseGrabPumpkins = { "Pumpkin_0", "Pumpkin_1", "Pumpkin_2", "Pumpkin_4", "Pumpkin_6" };
        private static readonly string[] OldSeedBools = { "Pumpkin_09", "Pumpkin_10" };

        // Round 1, 2, 3 (user, 2026-11-07).
        private static readonly (string name, float runMetres, float runSeconds, float stunSeconds, float hideRadius, float calmRange)[] Runners =
        {
            ("Pumpkin_8", 3f, 2.5f, 4f, 4.5f, 5f),
            ("Pumpkin_9", 4f, 2.5f, 3.5f, 5.5f, 5f),
            ("Pumpkin_10", 5f, 3f, 3f, 6.5f, 5.5f),
        };

        private struct Refs
        {
            public GameObject boss;
            public GameObject stars;
            public AudioClip rumble;
            public GameObject puff;
            public HitSettings hitSettings;
            public Object[] blastEffects;
            public AudioClip blastSound;
            public Object[] laughs;
            public Object[] hits;
        }

        [MenuItem("Tools/429 Game/PlayGround/Set Up Laughing Pumpkins")]
        public static void Run()
        {
            var missing = new List<string>();
            var refs = new Refs
            {
                boss = Load<GameObject>(BossPath, missing),
                stars = Load<GameObject>(StunStarsPath, missing),
                rumble = Load<AudioClip>(RumblePath, missing),
                puff = Load<GameObject>(PuffPath, missing),
                hitSettings = Load<HitSettings>(HitSettingsPath, missing),
                blastEffects = new Object[]
                {
                    Load<GameObject>(Fx + "Flash/Flash_round_yellow.prefab", missing),
                    Load<GameObject>(Fx + "Confetti/Hit_Blast.prefab", missing),
                    Load<GameObject>(PuffPath, missing),
                },
                blastSound = Load<AudioClip>(Sfx + "pumpkin_explode_1.wav", missing),
                laughs = Clips(missing, "laughing_pumpkin_1", "laughing_pumpkin_2", "laughing_pumpkin_3", "laughing_pumpkin_4"),
                hits = Clips(missing, "pumpkin_laugh_1", "pumpkin_laugh_2", "pumpkin_laugh_3", "pumpkin_laugh_4", "laughing_pumpkin_5"),
            };
            refs.blastEffects = System.Array.FindAll(refs.blastEffects, e => e != null);

            var log = new List<string>();
            foreach (var row in Runners)
            {
                string path = Folder + row.name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[Laughing Pumpkins] no '{path}', skipped.");
                    log.Add(row.name + " missing");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SetUpRunner(root, row, refs, missing);
                    int off = StopSeedBools(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add($"{row.name} {row.runMetres} m / {row.stunSeconds} s" + (off > 0 ? $" ({off} seed bool off)" : ""));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            foreach (var what in missing) Debug.LogWarning("[Laughing Pumpkins] missing: " + what);
            Debug.Log("[Laughing Pumpkins] set up: " + string.Join(", ", log) + (missing.Count > 0 ? $"; {missing.Count} missing (see warnings)" : ""));
        }

        /// <summary>
        /// Tools > 429 Game > Items > Close Grab For Pumpkins: the main game's breakable pumpkins (Pumpkin_0, 1, 2, 4, 6) can
        /// be picked up, but only with a hand right at them (no grabbing from afar with the ray). Safe to run again.
        /// </summary>
        [MenuItem("Tools/429 Game/Items/Close Grab For Pumpkins")]
        public static void CloseGrab()
        {
            var done = new List<string>();
            foreach (var name in CloseGrabPumpkins)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { done.Add(name + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!root.TryGetComponent<XRGrabInteractable>(out var grab))
                    {
                        grab = root.AddComponent<XRGrabInteractable>();
                        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                        grab.throwOnDetach = true;
                        grab.useDynamicAttach = true;
                    }
                    if (!root.TryGetComponent<NearGrabOnly>(out _)) root.AddComponent<NearGrabOnly>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Close Grab] " + string.Join(", ", done));
        }

        private static void SetUpRunner(GameObject root,
            (string name, float runMetres, float runSeconds, float stunSeconds, float hideRadius, float calmRange) row,
            Refs refs, List<string> missing)
        {
            var body = root.GetComponent<Rigidbody>();
            body.mass = 25f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            if (!root.TryGetComponent<XRGrabInteractable>(out var grab)) grab = root.AddComponent<XRGrabInteractable>();
            grab.enabled = true;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.useDynamicAttach = true;
            if (!root.TryGetComponent<NearGrabOnly>(out var near)) near = root.AddComponent<NearGrabOnly>();
            near.enabled = true;

            if (!root.TryGetComponent<LaughingPumpkin>(out var pumpkin)) pumpkin = root.AddComponent<LaughingPumpkin>();
            var so = new SerializedObject(pumpkin);
            string where = row.name + ".LaughingPumpkin.";
            SetBool(so, "canRun", true, where, missing);
            // Running: a few metres, then stunned.
            SetFloat(so, "runMetres", row.runMetres, where, missing);
            SetFloat(so, "runSeconds", row.runSeconds, where, missing);
            SetFloat(so, "regenPerSecond", 0.5f, where, missing);
            SetFloat(so, "restBeforeRegen", 3f, where, missing);
            SetFloat(so, "stunSeconds", row.stunSeconds, where, missing);
            SetObject(so, "stunStars", refs.stars, where, missing);
            SetFloat(so, "stunStarsSize", 1f, where, missing);
            // The player.
            SetFloat(so, "fleeRange", 3f, where, missing);
            SetFloat(so, "calmRange", row.calmRange, where, missing);
            // Hiding close by, inside the PlayGround.
            SetFloat(so, "hideRadius", row.hideRadius, where, missing);
            SetFloat(so, "arenaMargin", 0.6f, where, missing);
            SetFloat(so, "keepFromCauldron", 1.8f, where, missing);
            SetFloat(so, "leashSeconds", 2f, where, missing);
            SetObject(so, "leashPuff", refs.puff, where, missing);
            SetFloat(so, "leashPuffScale", 0.5f, where, missing);
            // Hits.
            SetObject(so, "hitSettings", refs.hitSettings, where, missing);
            SetFloat(so, "metresPerDamage", 1.5f, where, missing);
            // Exploding and sounds.
            SetArray(so, "handBlastEffects", refs.blastEffects, where, missing);
            SetObject(so, "handBlastSound", refs.blastSound, where, missing);
            SetArray(so, "laughs", refs.laughs, where, missing);
            SetArray(so, "hitSounds", refs.hits, where, missing);
            // Turning into the Pumpkin Boss (the runner pumpkins' Wait and Transform + Warn Before Transform).
            SetObject(so, "bossPrefab", refs.boss, where, missing);
            SetFloat(so, "transformAfter", 300f, where, missing);
            SetFloat(so, "warnLast", 60f, where, missing);
            SetFloat(so, "lowestPitch", 0.7f, where, missing);
            SetFloat(so, "rumbleLast", 10f, where, missing);
            SetObject(so, "rumbleSound", refs.rumble, where, missing);
            SetFloat(so, "rumbleVolume", 1f, where, missing);
            SetFloat(so, "trembleEvery", 0.6f, where, missing);
            SetFloat(so, "overdueSeconds", 5f, where, missing);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (root.TryGetComponent<Game.Inventory.CandyTrail>(out var trail))
            {
                var t = new SerializedObject(trail);
                SetFloat(t, "spacing", 1f, row.name + ".CandyTrail.", missing);
                var max = t.FindProperty("maxCandies");
                if (max != null) max.intValue = 60;
                else missing.Add(row.name + ".CandyTrail.maxCandies");
                t.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Switches off the leftover Set Fsm Bool (PlayGround_Manager Pumpkin_09 / Pumpkin_10) in the Health FSM's
        // Drop_Crack state: the second driver of the PlayGround's growing. Returns how many it switched off.
        private static int StopSeedBools(GameObject root)
        {
            int off = 0;
            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "Health") continue;
                var state = fsm.Fsm.GetState("Drop_Crack");
                if (state == null) continue;
                bool changed = false;
                foreach (var action in state.Actions)
                {
                    if (!(action is SetFsmBool set) || !set.Enabled || set.variableName == null) continue;
                    if (System.Array.IndexOf(OldSeedBools, set.variableName.Value) < 0) continue;
                    set.Enabled = false;
                    changed = true;
                    off++;
                }
                if (!changed) continue;
                state.SaveActions();
                EditorUtility.SetDirty(fsm);
            }
            return off;
        }

        private static T Load<T>(string path, List<string> missing) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) missing.Add(path);
            return asset;
        }

        private static Object[] Clips(List<string> missing, params string[] names)
        {
            var list = new List<Object>();
            foreach (var name in names)
            {
                var clip = Load<AudioClip>(Sfx + name + ".wav", missing);
                if (clip != null) list.Add(clip);
            }
            return list.ToArray();
        }

        private static void SetFloat(SerializedObject so, string field, float value, string where, List<string> missing)
        {
            var property = so.FindProperty(field);
            if (property != null) property.floatValue = value;
            else missing.Add(where + field);
        }

        private static void SetBool(SerializedObject so, string field, bool value, string where, List<string> missing)
        {
            var property = so.FindProperty(field);
            if (property != null) property.boolValue = value;
            else missing.Add(where + field);
        }

        // Only when there's something to set: a missing asset never empties a field that's already filled.
        private static void SetObject(SerializedObject so, string field, Object value, string where, List<string> missing)
        {
            var property = so.FindProperty(field);
            if (property == null) { missing.Add(where + field); return; }
            if (value != null) property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject so, string field, Object[] values, string where, List<string> missing)
        {
            var array = so.FindProperty(field);
            if (array == null) { missing.Add(where + field); return; }
            if (values.Length == 0) return;
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
