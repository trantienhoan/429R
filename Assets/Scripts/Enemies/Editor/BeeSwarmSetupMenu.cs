using System.Collections.Generic;
using System.Linq;
using Game.Enemies;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up EnemyManager 4 Bee Swarm And Music. -EnemyManager 4 becomes the extra swarm of
    /// the rooftop whack fights:
    /// - Whack Pumpkin 6 and 7 turn it on when they die (they let a boss bee out: Bee_Boss, Bee_Boss_PumpkinButt);
    /// - Fight Music: when it turns on, the pumpkin music player is destroyed and a fight song plays (enemy_fun_music,
    ///   enemy_fun2_music the next time); when it turns off, the song fades out;
    /// - its 'Bee Swarm' state keeps spawning Bee_Small (now and then a Bee_Normal) at its Bee Spawns on the rooftop
    ///   until the boss bee is killed; then the small bees pop one after another and it turns itself off.
    /// The two songs are set to stream (like the other songs). Needs 2 Game Scene open. Running it again changes nothing.
    /// </summary>
    public static class BeeSwarmSetupMenu
    {
        private const string ManagerName = "-EnemyManager 4";
        private const string Bees = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string FightSongPath = "Assets/Audio/SFX/enemy_fun_music.wav";
        private const string SecondSongPath = "Assets/Audio/SFX/enemy_fun2_music.wav";
        private const string SwarmState = "Bee Swarm";
        private const string EndState = "Deactivate Self";
        private const string SpawnsName = "Bee Spawns";
        // On the rooftop floor (13 x 13 m at the world's middle), 4.3 m around the lid, clear of the railings and barrels.
        private static readonly Vector3[] SpawnSpots =
        {
            new(2.15f, 0f, 3.72f), new(4.3f, 0f, 0f), new(2.15f, 0f, -3.72f),
            new(-2.15f, 0f, -3.72f), new(-4.3f, 0f, 0f), new(-2.15f, 0f, 3.72f),
        };
        // Whack pumpkin objects and the FSM (and state) that lets their boss out.
        private static readonly (string pumpkin, string fsm)[] WhackPumpkins =
        {
            ("-Wood_Barrels_Whack_Pumpkin_6-", "GAME-Pumpkin6"),
            ("-Wood_Barrels_Whack_Pumpkin_7-", "GAME-Pumpkin7"),
        };

        [MenuItem("Tools/429 Game/Enemies/Set Up EnemyManager 4 Bee Swarm And Music")]
        public static void SetUp()
        {
            var manager = Fsm(ManagerName, "EnemyManager");
            if (manager == null)
            {
                Debug.LogError($"[Bee Swarm] No '{ManagerName}' with an EnemyManager FSM in the open scene: open 2 Game Scene first. Nothing changed.");
                return;
            }

            var notes = Apply(manager);
            if (notes.Count == 0)
            {
                Debug.Log("[Bee Swarm] Already set up.");
                return;
            }
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            EditorSceneManager.SaveScene(manager.gameObject.scene);
            Debug.Log("[Bee Swarm] " + string.Join("; ", notes) + ".");
        }

        [MenuItem("Tools/429 Game/Enemies/Set Up EnemyManager 4 Bee Swarm And Music", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        /// <summary>Does the set-up without saving the scene; returns what changed.</summary>
        public static List<string> Apply(PlayMakerFSM manager)
        {
            var notes = new List<string>();
            if (StreamSong(FightSongPath) | StreamSong(SecondSongPath)) notes.Add("the fight songs stream");
            if (AddMusic(manager.gameObject)) notes.Add("Fight Music");
            var spawns = BeeSpawns(manager.transform, notes);
            if (AddSwarm(manager, spawns)) notes.Add($"'{SwarmState}': Bee_Small (sometimes Bee_Normal) until the boss bee is killed");
            foreach (var (pumpkin, fsm) in WhackPumpkins)
            {
                if (TurnOnWhenDead(Fsm(pumpkin, fsm), manager.gameObject)) notes.Add($"{pumpkin} turns it on when it dies");
            }
            if (notes.Count > 0) EditorUtility.SetDirty(manager);
            return notes;
        }

        private static PlayMakerFSM Fsm(string objectName, string fsmName) =>
            Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == fsmName && f.gameObject.name == objectName);

        // Long songs stream from disk like the other songs, instead of sitting decompressed in memory (30 MB each).
        private static bool StreamSong(string path)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return false;
            var settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming && settings.quality <= 0.7f) return false;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return true;
        }

        private static bool AddMusic(GameObject manager)
        {
            if (!manager.TryGetComponent(out FightMusic music)) music = manager.AddComponent<FightMusic>();
            var so = new SerializedObject(music);
            var songs = so.FindProperty("songs");
            if (songs.arraySize > 0) return false;
            songs.arraySize = 2;
            songs.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FightSongPath);
            songs.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SecondSongPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // Spawn points on the rooftop (its GhostSpawns were set out for the PlayGround, mostly off the roof).
        private static Transform BeeSpawns(Transform manager, List<string> notes)
        {
            var spawns = manager.Find(SpawnsName);
            if (spawns != null) return spawns;
            spawns = new GameObject(SpawnsName).transform;
            spawns.SetParent(manager, false);
            for (int i = 0; i < SpawnSpots.Length; i++)
            {
                var spot = new GameObject("Bee Spawn " + (i + 1)).transform;
                spot.SetParent(spawns, false);
                spot.position = SpawnSpots[i];
            }
            notes.Add($"{SpawnSpots.Length} Bee Spawns on the rooftop");
            return spawns;
        }

        private static bool AddSwarm(PlayMakerFSM manager, Transform spawns)
        {
            var fsm = manager.Fsm;
            if (!fsm.Initialized) fsm.Init(manager);
            if (fsm.States.Any(s => s.Name == SwarmState)) return PointStartAt(fsm, SwarmState);
            var init = fsm.States.FirstOrDefault(s => s.Name == fsm.StartState);
            var end = fsm.States.FirstOrDefault(s => s.Name == EndState);
            var holder = SpawnedStuffHolder();
            if (init == null || end == null || holder == null)
            {
                Debug.LogWarning($"[Bee Swarm] '{ManagerName}' needs its start state and '{EndState}', and Whack Pumpkin 6's " +
                                 "Spawn From Barrels (for CurrentlySpawnedStuffs). No swarm added.");
                return false;
            }

            var swarm = new KeepSpawning
            {
                enemies = new[] { Prefab("Bee_Small"), Prefab("Bee_Normal") },
                weights = new FsmFloat[] { 7f, 1f },
                spawnPoints = new FsmGameObject { Value = spawns.gameObject },
                parent = new FsmGameObject(holder),
                minWait = 2f,
                maxWait = 3.5f,
                maxAlive = 5,
                untilGone = new[] { Prefab("Bee_Boss"), Prefab("Bee_Boss_PumpkinButt") },
                bossWait = 8f,
                stopWhen = new FsmBool[0],
                popWhenStopped = true,
                popGap = 0.12f,
                stoppedEvent = null,
            };
            var state = new FsmState(fsm)
            {
                Name = SwarmState,
                Position = new Rect(init.Position.x, init.Position.y + 90f, 140f, 32f),
                Actions = new FsmStateAction[] { swarm },
                Transitions = new[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = end.Name } },
            };
            fsm.States = fsm.States.Append(state).ToArray();
            PointStartAt(fsm, state.Name);
            // Only the new state's actions are saved: the others stay exactly as they were.
            state.SaveActions();
            return true;
        }

        // The start state finishes into the swarm. Whether it changed anything.
        private static bool PointStartAt(Fsm fsm, string stateName)
        {
            var init = fsm.States.FirstOrDefault(s => s.Name == fsm.StartState);
            if (init == null) return false;
            bool changed = false;
            foreach (var t in init.Transitions.Where(t => t.EventName == FsmEvent.Finished.Name && t.ToState != stateName))
            {
                t.ToState = stateName;
                t.ToFsmState = null;
                changed = true;
            }
            return changed;
        }

        private static FsmGameObject Prefab(string name) =>
            new() { Value = AssetDatabase.LoadAssetAtPath<GameObject>(Bees + name + ".prefab") };

        // The CurrentlySpawnedStuffs variable, as Whack Pumpkin 6 already uses it.
        private static FsmGameObject SpawnedStuffHolder()
        {
            var pumpkin = Fsm(WhackPumpkins[0].pumpkin, WhackPumpkins[0].fsm);
            if (pumpkin == null) return null;
            foreach (var state in pumpkin.Fsm.States)
            {
                state.LoadActions();
                var spawn = state.Actions.OfType<SpawnFromBarrels>().FirstOrDefault();
                if (spawn != null && spawn.parent != null && spawn.parent.UseVariable) return spawn.parent;
            }
            return null;
        }

        // The state where the whack pumpkin dies and lets its boss out turns the manager on first.
        private static bool TurnOnWhenDead(PlayMakerFSM pumpkin, GameObject manager)
        {
            if (pumpkin == null) return false;
            if (!pumpkin.Fsm.Initialized) pumpkin.Fsm.Init(pumpkin);
            foreach (var state in pumpkin.Fsm.States)
            {
                state.LoadActions();
                if (!state.Actions.OfType<SpawnFromBarrels>().Any()) continue;
                if (state.Actions.OfType<ActivateGameObject>().Any(a => a.gameObject.GameObject?.Value == manager)) return false;
                var activate = new ActivateGameObject();
                activate.Reset();
                activate.gameObject = new FsmOwnerDefault
                {
                    OwnerOption = OwnerDefaultOption.SpecifyGameObject,
                    GameObject = new FsmGameObject { Value = manager },
                };
                activate.activate = true;
                activate.recursive = false;
                state.Actions = state.Actions.Prepend(activate).ToArray();
                state.SaveActions();
                EditorUtility.SetDirty(pumpkin);
                return true;
            }
            return false;
        }
    }
}
