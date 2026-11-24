using System.Collections.Generic;
using Game.Rooms;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Run Kit Wallpaper On (user, 2026-11-24: in the storage room the same plaster hole sat cut
    /// in half beside every wall joint; then "Yes, living room too"). Each kit room's Room Kit gets its wallpaper and how far
    /// the picture spans across, so each wall piece's paper is shifted by where it stands along its side and the picture
    /// runs on across the joints:
    /// - the bedroom kit (storage room, bedroom, secret room): Bedroom_Wall_Spooky, 6 m (tools/bedroom_kit.py lays 0.5 of
    ///   it on 3 m);
    /// - the living room kit (living room, playroom, secret room): Livingroom_Wall_Spooky, 3 m (tools/livingroom_kit.py:
    ///   once every 3 m).
    /// The Build ... Kit Room menus set the same. Running it again changes nothing more. Prefabs only: no scene change.
    /// </summary>
    public static class KitWallpaperMenu
    {
        private const string Tag = "[Kit Wallpaper]";
        public const string BedroomKit = "Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab";
        public const string BedroomPaper = "Assets/Materials/Bedroom_Wall_Spooky.mat";
        public const float BedroomPaperMetres = 6f;
        public const string LivingroomKit = "Assets/Prefabs/Bases/Livingroom_Kit_Base.prefab";
        public const string LivingroomPaper = "Assets/Materials/Livingroom_Wall_Spooky.mat";
        public const float LivingroomPaperMetres = 3f;

        private static readonly (string kit, string paper, float metres)[] Kits =
        {
            (BedroomKit, BedroomPaper, BedroomPaperMetres),
            (LivingroomKit, LivingroomPaper, LivingroomPaperMetres),
        };

        [MenuItem("Tools/429 Game/Rooms/Run Kit Wallpaper On")]
        public static void Run()
        {
            var notes = new List<string>();
            foreach (var (path, paperPath, metres) in Kits)
            {
                var paper = AssetDatabase.LoadAssetAtPath<Material>(paperPath);
                if (paper == null)
                {
                    notes.Add("no " + paperPath);
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var kit = root.GetComponent<RoomKit>();
                    if (kit == null)
                    {
                        notes.Add(path + " has no Room Kit (run its Build ... Kit Room menu)");
                        continue;
                    }
                    int walls = 0;
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        if (System.Array.IndexOf(r.sharedMaterials, paper) >= 0) walls++;
                    if (!Apply(kit, metres, paper))
                    {
                        notes.Add(root.name + " already done");
                        continue;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add($"{root.name}: {paper.name}, {metres} m across, on {walls} wall pieces");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log(Tag + " " + string.Join("; ", notes) + ". No scene change.");
        }

        /// <summary>Gives a Room Kit its wallpaper; false when it already has exactly this.</summary>
        public static bool Apply(RoomKit kit, float metres, Material paper)
        {
            var so = new SerializedObject(kit);
            var span = so.FindProperty("wallpaperMetres");
            var list = so.FindProperty("wallpapers");
            if (Mathf.Approximately(span.floatValue, metres) && list.arraySize == 1 &&
                list.GetArrayElementAtIndex(0).objectReferenceValue == paper) return false;
            span.floatValue = metres;
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = paper;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        [MenuItem("Tools/429 Game/Rooms/Run Kit Wallpaper On", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
