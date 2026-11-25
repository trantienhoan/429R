using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Lair > Arrange Storage Room (user, 2026-11-25: "re-arrange the scene", plan
    /// Exports/Lair_Concept/storage_plan_now_vs_concept.png, "Yes, go"). Moves spawn markers of the storage room's
    /// furniture sets (--MiniBedroom--, --MiniBedroom_Chaos-- under -GAMESTAGES) to their places in the plan: the rug and
    /// the weapon crate on it go from the middle (they lay on the rune circle) to the west side, so the middle stays
    /// open as in the concept. Also (later the same day) the north-east box tower away from the user's third wardrobe and
    /// the portrait back to the south part of the east wall (Places). Re-runnable (a marker already there is left);
    /// "Arrange Storage Room: Back As Before" puts them back. Changes the scene: save it after.
    /// </summary>
    public static class LairArrangeStorageMenu
    {
        private const string Tag = "[Lair Storage Room]";
        private static readonly string[] Roots = { "--MiniBedroom--", "--MiniBedroom_Chaos--" };

        // What moves: the prefab a marker makes, where it stood (x, z) and where it goes (x, z); its height and turn stay.
        private static readonly (string prefab, Vector2 from, Vector2 to)[] Moves =
        {
            ("Bedroom_Matt", new Vector2(-0.62f, 0.44f), new Vector2(-2.45f, -1.75f)),
            ("Wood_Box_Weapon_Main", new Vector2(-0.62f, 0.44f), new Vector2(-2.45f, -1.75f)),
        };

        // Points of the plain set put at a place by name: where they were (the user's layout before), where they go.
        // User 2026-11-25: "move the NE box points away from the wardrobe" (their third wardrobe stands in the north east
        // corner): the 14 boxes of the north-east tower are stacked again as the room first had them (the moved upper layer
        // stood 0.28 m off the lower one, so its boxes fell on the floor), 0.9 m further south against the east wall,
        // between the shelf and the wardrobe (about 0.24 m clear of each; the outer column 0.72 m lower, as the user moved
        // its two bottom boxes to the south end of the wall); and "this picture is at the wrong place, it
        // should be on the right": the portrait's point goes back to the south part of the east wall, where the room had it.
        private static readonly (string point, Vector3 was, Vector3 to)[] Places =
        {
            ("Paper_Box_5_Pos (1)", new Vector3(3.2064f, 0.0170f, 2.2927f), new Vector3(3.2150f, 0.0170f, 2.4890f)),
            ("Paper_Box_4_Pos (1)", new Vector3(3.2146f, 0.2980f, 2.1238f), new Vector3(3.2232f, 0.2980f, 2.3201f)),
            ("Paper_Box_3_Pos (1)", new Vector3(3.2300f, 0.6369f, 2.4600f), new Vector3(3.2386f, 0.6369f, 2.6563f)),
            ("Paper_Box_2_Pos (1)", new Vector3(3.2205f, 0.8356f, 2.4679f), new Vector3(3.2291f, 0.8356f, 2.6642f)),
            ("Paper_Box_2_Pos (2)", new Vector3(3.1774f, 0.8356f, 2.0817f), new Vector3(3.1860f, 0.8356f, 2.2780f)),
            ("Paper_Box_3_Pos (2)", new Vector3(3.1617f, 0.7430f, 1.8317f), new Vector3(3.4377f, 0.0230f, 1.9497f)),
            ("Paper_Box_5_Pos (4)", new Vector3(2.9390f, 1.3590f, 2.3710f), new Vector3(3.2150f, 1.3590f, 2.4890f)),
            ("Paper_Box_5_Pos (5)", new Vector3(3.2005f, 1.3590f, 1.8080f), new Vector3(3.4765f, 0.6390f, 1.9260f)),
            ("Paper_Box_4_Pos (4)", new Vector3(3.1720f, 1.6400f, 1.8161f), new Vector3(3.4480f, 0.9200f, 1.9341f)),
            ("Paper_Box_3_Pos (4)", new Vector3(3.1617f, 2.0850f, 1.8317f), new Vector3(3.4377f, 1.3650f, 1.9497f)),
            ("Paper_Box_2_Pos (3)", new Vector3(2.9100f, 2.1776f, 2.1600f), new Vector3(3.1860f, 2.1776f, 2.2780f)),
            ("Paper_Box_4_Pos (5)", new Vector3(2.9472f, 1.6400f, 2.2021f), new Vector3(3.2232f, 1.6400f, 2.3201f)),
            ("Paper_Box_3_Pos (5)", new Vector3(2.9626f, 1.9789f, 2.5383f), new Vector3(3.2386f, 1.9789f, 2.6563f)),
            ("Paper_Box_2_Pos (4)", new Vector3(2.9531f, 2.1776f, 2.5462f), new Vector3(3.2291f, 2.1776f, 2.6642f)),
            ("Picture_4_Pos", new Vector3(3.5326f, 1.3700f, 2.4493f), new Vector3(3.7400f, 1.3700f, -2.1190f)),
        };

        [MenuItem("Tools/429 Game/Lair/Arrange Storage Room")]
        public static void Arrange() => Run(false);

        [MenuItem("Tools/429 Game/Lair/Arrange Storage Room: Back As Before")]
        public static void BackAsBefore() => Run(true);

        [MenuItem("Tools/429 Game/Lair/Arrange Storage Room", true)]
        [MenuItem("Tools/429 Game/Lair/Arrange Storage Room: Back As Before", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Run(bool back)
        {
            var changes = new List<string>();
            foreach (var rootName in Roots)
            {
                var root = FindSceneObject(rootName);
                if (root == null)
                {
                    Debug.LogWarning($"{Tag} No '{rootName}' in the open scene (skipped).");
                    continue;
                }
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (fsm.gameObject == root) continue;
                    var made = Made(fsm);
                    if (made == null) continue;
                    foreach (var (prefab, from, to) in Moves)
                    {
                        if (made != prefab) continue;
                        var t = fsm.transform;
                        var here = new Vector2(t.position.x, t.position.z);
                        var start = back ? to : from;
                        var goal = back ? from : to;
                        if (Vector2.Distance(here, goal) < 0.05f || Vector2.Distance(here, start) > 0.05f) continue;
                        Undo.RecordObject(t, "Arrange Storage Room");
                        t.position = new Vector3(goal.x, t.position.y, goal.y);
                        changes.Add($"{rootName}/{t.name} ({prefab}) -> ({goal.x}, {goal.y})");
                        EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                    }
                }
            }
            // The named points (plain set only).
            var plain = FindSceneObject(Roots[0]);
            if (plain != null)
            {
                foreach (var (point, was, to) in Places)
                {
                    var t = plain.transform.Find(point);
                    if (t == null)
                    {
                        Debug.LogWarning($"{Tag} {Roots[0]} has no '{point}' (skipped).");
                        continue;
                    }
                    var start = back ? to : was;
                    var goal = back ? was : to;
                    if (Vector3.Distance(t.position, goal) < 0.01f) continue;
                    if (Vector3.Distance(t.position, start) > 0.03f)
                    {
                        Debug.LogWarning($"{Tag} {point} is at {t.position}, not where this menu expects it ({start}); left there.");
                        continue;
                    }
                    Undo.RecordObject(t, "Arrange Storage Room");
                    t.position = goal;
                    changes.Add($"{Roots[0]}/{point} -> {goal}");
                    EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                }
            }
            Debug.Log(changes.Count == 0
                ? $"{Tag} Nothing to move (already {(back ? "as before" : "arranged")})."
                : $"{Tag} {(back ? "Back as before" : "Arranged")}: {string.Join("; ", changes)}. SAVE 2 Game Scene.");
        }

        // The prefab a marker's FSM makes (its first Create Object), or null.
        private static string Made(PlayMakerFSM fsm)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            foreach (var state in fsm.Fsm.States)
            {
                state.LoadActions();
                foreach (var action in state.Actions)
                    if (action is CreateObject create && create.gameObject != null && create.gameObject.Value != null)
                        return create.gameObject.Value.name;
            }
            return null;
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == name && go.scene.IsValid() && go.scene.isLoaded) return go;
            return null;
        }
    }
}
