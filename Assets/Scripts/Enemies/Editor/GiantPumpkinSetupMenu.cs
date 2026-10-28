using System.Linq;
using Game.Enemies;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command that sets up the rooftop's giant pumpkins (Tools > 429 Game > Enemies).</summary>
    public static class GiantPumpkinSetupMenu
    {
        private const string GiantPath = "Assets/Prefabs/Enemies/Pumpkin_Boss.prefab";
        private const string RooftopPath = "Assets/Prefabs/Bases/Rooftop_Base_Z.prefab";
        private const string PoundEffectPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";

        private const float SmashHeight = 5f;
        private const string TreeSaveSoundPath = "Assets/Audio/SFX/A_magical_glowing.wav";
        private const string LaunchSoundPath = "Assets/Audio/SFX/Someone_gets_launche.wav";
        private const float HealthCheckDamage = 240f;

        /// <summary>
        /// Puts Giant Pumpkin on Pumpkin_Boss (its hands break the pumpkins they hit; its pounds are only the show), and
        /// makes the giants' hit one Giant Smash in the rooftop's End state (user, 2026-10-28): the player is thrown
        /// Smash Height up; the Pumpkin Tree, if it still stands, takes the hit, else the player lands into the Health
        /// Check, always 240 (the PlayerHealth FSM in the open scene: save it). Running it again changes nothing.
        /// </summary>
        [MenuItem("Tools/429 Game/Enemies/Set Up Giant Pumpkins")]
        public static void SetUp()
        {
            string giantNote, roofNote;

            var giant = PrefabUtility.LoadPrefabContents(GiantPath);
            try
            {
                if (giant.GetComponent<GiantPumpkin>() == null)
                {
                    var serialized = new SerializedObject(giant.AddComponent<GiantPumpkin>());
                    serialized.FindProperty("poundEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PoundEffectPath);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(giant, GiantPath);
                    giantNote = "added Giant Pumpkin to Pumpkin_Boss";
                }
                else giantNote = "Pumpkin_Boss already has Giant Pumpkin";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(giant);
            }

            var roof = PrefabUtility.LoadPrefabContents(RooftopPath);
            try
            {
                var info = roof.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Info");
                if (info == null)
                {
                    roofNote = "WARNING: Rooftop_Base_Z has no Info FSM";
                }
                else
                {
                    if (!info.Fsm.Initialized) info.Fsm.Init(info);
                    foreach (var s in info.FsmStates) s.LoadActions();
                    var end = info.FsmStates.FirstOrDefault(s => s.Name == "End");
                    if (end == null) roofNote = "WARNING: the rooftop's Info has no End state";
                    else if (end.Actions.Any(a => a == null || a.GetType().Name == "MissingAction"))
                        roofNote = "WARNING: an action in the rooftop's End couldn't be read, so it was left as it is";
                    else if (end.Actions.OfType<GiantSmash>().Any())
                    {
                        roofNote = "the rooftop's End already has the Giant Smash";
                        if (end.Actions.OfType<GiantSmash>().Aggregate(false, (changed, s) => SetSounds(s) | changed))
                        {
                            foreach (var s in info.FsmStates) s.SaveActions();
                            EditorUtility.SetDirty(info);
                            PrefabUtility.SaveAsPrefabAsset(roof, RooftopPath);
                            roofNote += " (its sound set)";
                        }
                    }
                    else
                    {
                        // The old hit on every player and the plain throw make way for the smash, before the big shake.
                        var actions = end.Actions.Where(a => !(a is SendEvent send && send.sendEvent != null && send.sendEvent.Name == "Pumpkin_Boss_Damage")).ToList();
                        int at = actions.FindIndex(a => a is LaunchPlayer);
                        if (at >= 0) actions.RemoveAt(at);
                        else
                        {
                            at = actions.FindIndex(a => a is SendEvent send && send.sendEvent != null && send.sendEvent.Name.StartsWith("Shake"));
                            if (at < 0) at = actions.Count;
                        }
                        var smash = new GiantSmash();
                        smash.Reset();
                        smash.height = SmashHeight;
                        SetSounds(smash);
                        actions.Insert(at, smash);
                        end.Actions = actions.ToArray();
                        foreach (var s in info.FsmStates) s.SaveActions();
                        EditorUtility.SetDirty(info);
                        PrefabUtility.SaveAsPrefabAsset(roof, RooftopPath);
                        roofNote = $"the rooftop's End has the Giant Smash ({SmashHeight} m up; the tree takes the hit, else the Health Check)";
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(roof);
            }

            Debug.Log($"[Giant Pumpkins] {giantNote}; {roofNote}; {HealthCheckAlways240()}.");
        }

        // The user's sounds (ElevenLabs, 2026-10-28) for the tree saving the player and for the throw, where the smash
        // has none yet.
        private static bool SetSounds(GiantSmash smash)
        {
            bool changed = false;
            if (smash.treeSaveSound == null || smash.treeSaveSound.Value == null)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(TreeSaveSoundPath);
                if (clip != null)
                {
                    smash.treeSaveSound = new FsmObject { Value = clip, ObjectType = typeof(AudioClip) };
                    if (smash.treeSaveVolume == null || smash.treeSaveVolume.IsNone) smash.treeSaveVolume = 1f;
                    changed = true;
                }
            }
            if (smash.launchSound == null || smash.launchSound.Value == null)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(LaunchSoundPath);
                if (clip != null)
                {
                    smash.launchSound = new FsmObject { Value = clip, ObjectType = typeof(AudioClip) };
                    if (smash.launchVolume == null || smash.launchVolume.IsNone) smash.launchVolume = 1f;
                    changed = true;
                }
            }
            return changed;
        }

        // The PlayerHealth FSM's Health Check (Pumpkin_Boss_Damage) in the open scene takes exactly Health Check Damage,
        // Toughness or not: a health check, not a hit.
        private static string HealthCheckAlways240()
        {
            int changed = 0, found = 0;
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm.FsmName != "PlayerHealth" || !fsm.gameObject.scene.IsValid() || EditorUtility.IsPersistent(fsm)) continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                var state = fsm.Fsm.GetState("Health Check");
                if (state == null) continue;
                found++;
                state.LoadActions();
                var take = state.Actions.OfType<FloatSubtractRandom>().FirstOrDefault();
                if (take == null) continue;
                bool fixedAlready = Mathf.Approximately(take.min.Value, HealthCheckDamage) && Mathf.Approximately(take.max.Value, HealthCheckDamage) &&
                                    (take.playerToughness == null || !take.playerToughness.Value);
                if (fixedAlready) continue;
                Undo.RecordObject(fsm, "Health Check 240");
                take.min = HealthCheckDamage;
                take.max = HealthCheckDamage;
                take.playerToughness = false;
                state.SaveActions();
                EditorUtility.SetDirty(fsm);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(fsm.gameObject.scene);
                changed++;
            }
            if (found == 0) return "no PlayerHealth Health Check in the open scene (open 2 Game Scene and run it again)";
            return changed > 0 ? $"Health Check now always takes {HealthCheckDamage} (save the scene)" : $"Health Check already takes {HealthCheckDamage}";
        }
    }
}
