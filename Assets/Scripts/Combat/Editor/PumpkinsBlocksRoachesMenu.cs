using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Three settings from one request (user, 2026-10-30), each its own menu and all safe to run again:
    /// - Tools > 429 Game > Combat > Held Pumpkins Explode: Held Break Blast on the runner pumpkins (Pumpkin_0, 1, 2, 4, 6,
    ///   their Chaos copies) and Pumpkin_00: broken in the player's hands, they go off like a mini boss's special; and
    ///   Held Still, so their Health FSM's Look At doesn't turn them in the hand.
    /// - Tools > 429 Game > Combat > Bugs Break Wooden Jenga Blocks: Bug Breakable on the wooden blocks (Items/Metal_Block
    ///   Jenga_Block.* that can break at all; the brass ones, health 9999, stay as they are): 3 bug hits break one.
    /// - Tools > 429 Game > Enemies > Roaches Come At Once: Cockroach_Smarter (and every cockroach variant) comes for the
    ///   player as soon as it appears (Notice On Spawn) and only gives up 30 m away (was 14): in the big Livingroom they
    ///   used to wander where they came out, out of sight.
    /// </summary>
    public static class PumpkinsBlocksRoachesMenu
    {
        private static readonly string[] Pumpkins =
        {
            "Pumpkin_00", "Pumpkin_0", "Pumpkin_1", "Pumpkin_1_Chaos", "Pumpkin_2", "Pumpkin_2_Chaos", "Pumpkin_4",
            "Pumpkin_4_Chaos", "Pumpkin_6", "Pumpkin_6_Chaos",
        };
        private const string PumpkinFolder = "Assets/Prefabs/Items/Breakable/";
        private const string BlockFolder = "Assets/Prefabs/Items/Metal_Block";
        private const string RoachPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Cockroach_Smarter.prefab";
        private const string FlashPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab";
        private const string HitBlastPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab";
        private const string SmallPuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string BlastSoundPath = "Assets/Audio/SFX/explosion-312361.wav";
        private const float RoachGiveUpRange = 30f;
        // Blocks with this much health or more never break (the brass ones: 9999).
        private const float Unbreakable = 999f;

        [MenuItem("Tools/429 Game/Combat/Held Pumpkins Explode")]
        public static void HeldPumpkins()
        {
            var effects = new[] { FlashPath, HitBlastPath, SmallPuffPath }.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(e => e != null).ToArray();
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(BlastSoundPath);
            var done = new List<string>();
            foreach (var name in Pumpkins)
            {
                string path = PumpkinFolder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || IsOpen(path, done)) continue;
                Edit(path, root =>
                {
                    if (!root.TryGetComponent(out HeldStill _)) root.AddComponent<HeldStill>();
                    if (!root.TryGetComponent(out HeldBreakBlast blast)) blast = root.AddComponent<HeldBreakBlast>();
                    var so = new SerializedObject(blast);
                    var list = so.FindProperty("effects");
                    list.arraySize = effects.Length;
                    for (int i = 0; i < effects.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
                    so.FindProperty("sound").objectReferenceValue = sound;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });
                done.Add(name);
            }
            Debug.Log("[Held Pumpkins] Held Break Blast on " + string.Join(", ", done) + ".");
        }

        [MenuItem("Tools/429 Game/Combat/Bugs Break Wooden Jenga Blocks")]
        public static void BugBlocks()
        {
            var puff = AssetDatabase.LoadAssetAtPath<GameObject>(SmallPuffPath);
            var done = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("Jenga_Block t:Prefab", new[] { BlockFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var health = asset.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Health")?.FsmVariables.FindFsmFloat("health");
                if (health == null || health.Value >= Unbreakable || IsOpen(path, done)) continue;
                Edit(path, root =>
                {
                    if (!root.TryGetComponent(out BugBreakable breakable)) breakable = root.AddComponent<BugBreakable>();
                    var so = new SerializedObject(breakable);
                    so.FindProperty("hitEffect").objectReferenceValue = puff;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });
                done.Add($"{asset.name} (health {health.Value})");
            }
            Debug.Log("[Jenga Blocks] Bugs break " + string.Join(", ", done) + ".");
        }

        [MenuItem("Tools/429 Game/Enemies/Roaches Come At Once")]
        public static void Roaches()
        {
            var done = new List<string>();
            if (IsOpen(RoachPath, done)) { Debug.LogWarning("[Roaches] " + done[0]); return; }
            Edit(RoachPath, root =>
            {
                var brain = root.GetComponent<SpiderBrain>();
                var so = new SerializedObject(brain);
                so.FindProperty("noticeOnSpawn").boolValue = true;
                so.FindProperty("loseInterestRange").floatValue = RoachGiveUpRange;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            Debug.Log($"[Roaches] Cockroach_Smarter: comes for the player as soon as it appears, gives up only {RoachGiveUpRange} m away.");
        }

        private static bool IsOpen(string path, List<string> notes)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != path) return false;
            notes.Add(System.IO.Path.GetFileNameWithoutExtension(path) + " is open in Prefab Mode: skipped");
            return true;
        }

        private static void Edit(string path, System.Action<GameObject> change)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                change(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
