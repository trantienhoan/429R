using System.Collections.Generic;
using Game.Gates;
using UnityEditor;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// User 2026-11-26: "there is something wrong about gate 1. There should be no stage anymore. The game flow is that
    /// Player teleported in the scene, open chest, find hat, hit hat the get recipe, put the right ingredient in the
    /// cauldron, smoke particle up on cauldron, cooking countdown start, comes bugs, monsters... defend the cauldron and
    /// survive until the end, cauldron finishes cooking and drop the seed, take the seed to move on."
    /// Tools > 429 Game > PlayGround > Use One Cook (Seed At The End), re-runnable, prefabs only (never the scene):
    /// - Play_Ground_Z: Gate Run OFF (no rounds, no ROUND n/3 line, no round chests, no giant pumpkin; kept on the prefab),
    ///   Play Ground Game kept on for its look only (Use Rounds off: the lifted fog and light), the PlayGround's own start
    ///   chest spawner (Cauldron_Pos: the cauldron chest, whose seed pops the cauldron out) back ON;
    /// - Fire_Woods_n_Cauldron: Tools > 429 Game > PlayGround > Use Defend The Cauldron (the one cook: hat, recipe, fire,
    ///   the 5-minute countdown, the monsters and the mini bosses with their gear chests), then Seed At The End on and its
    ///   Finale Seed (The_Seed) set: the cook done, the pot bursts and The_Seed drops where it stood; taking it goes on to
    ///   the rooftop (Gate Exit).
    /// "Use Gate Run (3 Rounds)" puts the rounds back.
    /// </summary>
    public static class PlayGroundOneCookMenu
    {
        private const string Log = "[PlayGround One Cook]";
        private const string PlayGroundPath = "Assets/Prefabs/Bases/Play_Ground_Z.prefab";
        private const string CauldronPath = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string SeedPath = "Assets/Prefabs/Items/Chest/The_Seed.prefab";

        [MenuItem("Tools/429 Game/PlayGround/Use One Cook (Seed At The End)")]
        public static void Use()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var notes = new List<string>();

            // 1. The PlayGround: no Gate Run, its own start chest, the look kept.
            PlayGroundGateMenu.Backup(notes);
            var root = PrefabUtility.LoadPrefabContents(PlayGroundPath);
            try
            {
                if (root.TryGetComponent(out GateRun run) && run.enabled) { run.enabled = false; notes.Add("Gate Run off (kept)"); }
                if (root.TryGetComponent(out PlayGroundGame game))
                {
                    game.enabled = true;
                    if (game.useRounds) { game.useRounds = false; notes.Add("Play Ground Game: the look only"); }
                    EditorUtility.SetDirty(game);
                }
                PlayGroundGateMenu.Spawners(root, true, notes);
                PrefabUtility.SaveAsPrefabAsset(root, PlayGroundPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 2. The cauldron: the one cook (its own set-up keeps tuned numbers), then the seed at the end.
            CauldronDefenceMenu.UseDefence();
            var cauldron = PrefabUtility.LoadPrefabContents(CauldronPath);
            try
            {
                var defence = cauldron.GetComponent<CauldronDefence>();
                if (defence == null)
                {
                    Debug.LogError($"{Log} {CauldronPath} has no Cauldron Defence after Use Defend The Cauldron; the cauldron is unchanged.");
                }
                else
                {
                    var so = new SerializedObject(defence);
                    so.FindProperty("seedAtTheEnd").boolValue = true;
                    var seed = so.FindProperty("finaleSeed");
                    if (seed.objectReferenceValue == null) seed.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SeedPath);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    defence.enabled = true;
                    notes.Add("cauldron: one cook, Seed At The End on, seed " + (seed.objectReferenceValue != null ? seed.objectReferenceValue.name : "MISSING"));
                    PrefabUtility.SaveAsPrefabAsset(cauldron, CauldronPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(cauldron);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Gate 1 = one cook, defend the cauldron, The_Seed at the end: {string.Join("; ", notes)}.");
        }

        [MenuItem("Tools/429 Game/PlayGround/Use One Cook (Seed At The End)", true)]
        private static bool CanUse() => !EditorApplication.isPlaying;
    }
}
