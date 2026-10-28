using System.Collections.Generic;
using System.Linq;
using Game.Audio;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Audio > Set Up Music Playlists (user, 2026-10-28). Makes the game's Music Playlists in
    /// Assets/Data/Music and plays them with a Playlist Player:
    /// - The four radios. The Bedroom one: the two new chill songs first (Pumpkin Pillow Party, Pumpkin Playroom: 2.5 min
    ///   each, the 300 s before the pumpkin wakes), then its old songs. The others: their old songs (user: "Livingroom,
    ///   let's just use the old song for now"). A fist skips a song (with the radio scratch). Their Music FSM is
    ///   switched off (kept).
    /// - The bedroom fights (-EnemyManager 0 and 1 in the open scene): the Battle playlist (Spider Queen Rampage, Spider
    ///   Queen Showdown, 3 min each) one after another until the mini boss is down. BG Music's Audio Play is switched
    ///   off; its Damage to the radio stays. -EnemyManager 2 and 3 keep their own song. Save the scene afterwards.
    /// A playlist that already exists keeps its songs (edit them in the Inspector); running it again changes nothing else.
    /// </summary>
    public static class MusicPlaylistSetupMenu
    {
        private const string Folder = "Assets/Data/Music";
        private const string SkipSoundPath = "Assets/Audio/SFX/An_old_pumpkin_radio.wav";
        private static readonly string[] ChillSongs = { "Pumpkin Pillow Party", "Pumpkin Playroom" };
        private static readonly string[] BattleSongs = { "Spider Queen Rampage", "Spider Queen Showdown" };
        // Each fight with the Battle playlist and the bool its mini boss sets when it's down (-EnemyManager 2 and 3,
        // BigRoachDown and BigRoach1Down, keep their own song for now).
        private static readonly (string manager, string bossDown)[] Fights =
        {
            ("-EnemyManager 0", "WhiteSpiderDown"), ("-EnemyManager 1", "BigSpiderDown"),
        };

        [MenuItem("Tools/429 Game/Audio/Set Up Music Playlists")]
        public static void SetUp()
        {
            var chill = FindSongs(ChillSongs);
            var battle = FindSongs(BattleSongs);
            if (chill == null || battle == null) return;
            var skip = AssetDatabase.LoadAssetAtPath<AudioClip>(SkipSoundPath);
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Music");
            var notes = new List<string>();

            // Volumes as they sounded before (an Audio Play that waits for the clip plays it at its volume squared).
            var bedroom = Playlist("Bedroom Radio", chill.Concat(Songs("bedroom_bg_music", "bedroom_bg3_music", "bedroom_bg2_music")), 0.67f, false, skip, notes);
            var living = Playlist("Livingroom Radio", Songs("livingroom_bg_music", "livingroom_bg2_music", "livingroom_bg3_music"), 0.45f, true, skip, notes);
            var jack = Playlist("JACK Radio", Songs("hit_the_box_song_1", "hit_the_box_song_2", "hit_the_box_song_3"), 0.45f, true, skip, notes);
            var playground = Playlist("PlayGround Radio", Songs("Lovely_BG_Music", "PG_Song_2"), 0.45f, true, skip, notes);
            var fight = Playlist("Battle", battle, 0.64f, false, null, notes);
            AssetDatabase.SaveAssets();

            notes.Add(Radio("Assets/Prefabs/BedRoom/Bedroom_pumpkin_music_player.prefab", bedroom));
            notes.Add(Radio("Assets/Prefabs/BedRoom/Livingroom_pumpkin_music_player.prefab", living));
            notes.Add(Radio("Assets/Prefabs/BedRoom/Livingroom_pumpkin_music_player_JACK_.prefab", jack));
            notes.Add(Radio("Assets/Prefabs/PlayGround/PG_pumpkin_music_player.prefab", playground));
            foreach (var (manager, bossDown) in Fights) notes.Add(Battle(manager, bossDown, fight));
            Debug.Log("[Music Playlists] " + string.Join("; ", notes) + ". Save the scene.");
        }

        // The user's new songs, found by their titles (ElevenLabs names the files after them), set to stream like the
        // other songs. Null (and an error) if one isn't in the project yet.
        private static AudioClip[] FindSongs(string[] titles)
        {
            var all = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var found = new List<AudioClip>();
            foreach (var title in titles)
            {
                string key = Squash(title);
                var path = all.FirstOrDefault(p => Squash(System.IO.Path.GetFileNameWithoutExtension(p)).Contains(key));
                if (path == null)
                {
                    Debug.LogError($"[Music Playlists] No song called \"{title}\" in Assets/Audio yet: save it there first. Nothing changed.");
                    return null;
                }
                Stream(path);
                found.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            return found.ToArray();
        }

        private static string Squash(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        // Long songs are streamed (not unpacked into memory), as the game's other songs are.
        private static void Stream(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            var settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming) return;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static IEnumerable<AudioClip> Songs(params string[] names)
        {
            foreach (var name in names)
            {
                var guid = AssetDatabase.FindAssets(name + " t:AudioClip").FirstOrDefault(g =>
                    System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
                if (guid != null) yield return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        private static MusicPlaylist Playlist(string name, IEnumerable<AudioClip> songs, float volume, bool shuffle, AudioClip skip, List<string> notes)
        {
            string path = $"{Folder}/{name}.asset";
            var list = AssetDatabase.LoadAssetAtPath<MusicPlaylist>(path);
            if (list != null) return list;
            list = ScriptableObject.CreateInstance<MusicPlaylist>();
            list.songs = songs.ToArray();
            list.volume = volume;
            list.shuffle = shuffle;
            list.skipSound = skip;
            AssetDatabase.CreateAsset(list, path);
            notes.Add($"made {name} ({list.songs.Length} songs)");
            return list;
        }

        // A radio prefab: a Playlist Player (2D, a fist skips) instead of its Music FSM.
        private static string Radio(string path, MusicPlaylist list)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path) return $"{name}: open in Prefab Mode, skipped";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!root.TryGetComponent(out PlaylistPlayer player)) player = root.AddComponent<PlaylistPlayer>();
                var so = new SerializedObject(player);
                if (so.FindProperty("playlist").objectReferenceValue == null) so.FindProperty("playlist").objectReferenceValue = list;
                so.FindProperty("punchToSkip").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == "Music") fsm.enabled = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{name}: plays {list.name}";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // A fight in the open scene: the Battle playlist until its mini boss is down, instead of BG Music's one song.
        private static string Battle(string managerName, string bossDown, MusicPlaylist list)
        {
            var manager = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == managerName && t.gameObject.scene.IsValid());
            if (manager == null) return $"{managerName}: not in the open scene";
            var go = manager.gameObject;
            if (!go.TryGetComponent(out PlaylistPlayer player)) player = Undo.AddComponent<PlaylistPlayer>(go);
            var so = new SerializedObject(player);
            if (so.FindProperty("playlist").objectReferenceValue == null) so.FindProperty("playlist").objectReferenceValue = list;
            so.FindProperty("doneBool").stringValue = bossDown;
            so.FindProperty("doneFsm").stringValue = "EnemyManager";
            so.ApplyModifiedProperties();
            foreach (var fsm in go.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "BG Music") continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                foreach (var state in fsm.FsmStates)
                {
                    state.LoadActions();
                    bool changed = false;
                    foreach (var action in state.Actions.OfType<AudioPlay>())
                    {
                        if (!action.Enabled) continue;
                        action.Enabled = false;
                        changed = true;
                    }
                    if (changed)
                    {
                        Undo.RecordObject(fsm, "Battle playlist");
                        state.SaveActions();
                        EditorUtility.SetDirty(fsm);
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(go.scene);
            return $"{managerName}: {list.name} until {bossDown}";
        }
    }
}
