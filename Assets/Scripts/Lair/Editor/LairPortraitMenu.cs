using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Lair > Set Up Lair Portrait (user, 2026-11-25, the lair concept: in every lair a framed portrait
    /// of the evil pumpkin holding the Fairy in a jar; the storage room's is the first, the pumpkin still small). Remodels
    /// the storage room's picture (its marker Picture_4_Pos picks Picture_Hang_4 or its puzzle Picture_Hang_4_Broken):
    /// - the painting: Materials/Lair/Lair_Portrait_Mat shows M_Textures/Lair/Lair_Portrait_Room1.png (the portrait from
    ///   Exports/Lair_Concept/concept_v1_corners.png, straightened) on the canvas's own UVs (its tiling and offset frame the
    ///   canvas's patch of the furniture atlas, which a white hat also uses, so the atlas is not repainted); the 9 puzzle
    ///   pieces use the same UVs, so the puzzle shows the portrait too;
    /// - the frame: the room's aged wood (Materials/Lair/Lair_Wood_Aged_Mat, user 2026-11-25: "change the frame's texture
    ///   to the new aged wood texture"; it was a flat gold, Lair_Gilded_Frame_Mat, now unused).
    /// Re-runnable. Prefabs and assets only, no scene change.
    /// </summary>
    public static class LairPortraitMenu
    {
        private const string Tag = "[Lair Portrait]";
        private const string PicturePath = "Assets/Prefabs/Items/Deco/Picture_Hang_4.prefab";
        private const string PuzzlePath = "Assets/Prefabs/Items/Deco/Picture_Hang_4_Broken.prefab";
        private const string PortraitTexture = "Assets/M_Textures/Lair/Lair_Portrait_Room1.png";
        private const string Folder = "Assets/Materials/Lair/";
        private const string CanvasMaterial = "Bedroom_Set_2_Mat";
        private const string FrameMaterial = "Wood_Furniture_Mat";

        [MenuItem("Tools/429 Game/Lair/Set Up Lair Portrait")]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(Tag + " Stop play mode first; nothing changed.");
                return;
            }
            var picture = AssetDatabase.LoadAssetAtPath<GameObject>(PicturePath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitTexture);
            if (picture == null || texture == null)
            {
                Debug.LogError($"{Tag} {PicturePath} or {PortraitTexture} is missing; nothing changed.");
                return;
            }
            if (AssetImporter.GetAtPath(PortraitTexture) is TextureImporter importer &&
                (importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize != 512))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }

            // The canvas's patch of the atlas, from the picture's own canvas UVs.
            var look = picture.GetComponentInChildren<MeshRenderer>();
            var mesh = look.GetComponent<MeshFilter>().sharedMesh;
            int canvas = System.Array.FindIndex(look.sharedMaterials, m => m != null && (m.name == CanvasMaterial || m.name == "Lair_Portrait_Mat"));
            if (canvas < 0)
            {
                Debug.LogError($"{Tag} Picture_Hang_4 has no {CanvasMaterial} canvas; nothing changed.");
                return;
            }
            var uv = mesh.uv;
            var tris = mesh.GetTriangles(canvas);
            float u0 = 1, u1 = 0, v0 = 1, v1 = 0;
            foreach (var i in tris)
            {
                u0 = Mathf.Min(u0, uv[i].x); u1 = Mathf.Max(u1, uv[i].x);
                v0 = Mathf.Min(v0, uv[i].y); v1 = Mathf.Max(v1, uv[i].y);
            }
            var done = new List<string>();
            var source = look.sharedMaterials[canvas];
            var portrait = Copy(source, "Lair_Portrait_Mat", done);
            portrait.SetTexture("_BaseMap", texture);
            var tiling = new Vector2(1f / (u1 - u0), 1f / (v1 - v0));
            portrait.SetTextureScale("_BaseMap", tiling);
            portrait.SetTextureOffset("_BaseMap", new Vector2(-u0 * tiling.x, -v0 * tiling.y));
            portrait.SetColor("_BaseColor", Color.white);
            if (portrait.HasProperty("_EmissionMap"))
            {
                // A little light of its own, so the painting reads in the dark room (the jar's green most of all).
                portrait.SetTexture("_EmissionMap", texture);
                portrait.SetColor("_EmissionColor", new Color(0.28f, 0.3f, 0.28f, 1f));
                portrait.SetKeyword(new UnityEngine.Rendering.LocalKeyword(portrait.shader, "_EMISSION"), true);
            }
            EditorUtility.SetDirty(portrait);

            // The frame in the room's aged wood (as the wardrobes, table and shelves), no longer gold.
            var frame = LairStoryPropsMenu.AgedWood(done);
            AssetDatabase.SaveAssets();

            Swap(PicturePath, portrait, frame, done);
            Swap(PuzzlePath, portrait, frame, done);
            Debug.Log($"{Tag} Canvas UVs u {u0:F4}-{u1:F4} v {v0:F4}-{v1:F4} -> tiling {tiling}. " + string.Join("; ", done) + ". No scene change.");
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Lair Portrait", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static Material Copy(Material source, string name, List<string> done)
        {
            Directory.CreateDirectory(Folder);
            string path = Folder + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(source) { name = name };
            AssetDatabase.CreateAsset(m, path);
            done.Add("made " + path);
            return m;
        }

        // The canvas slots wear the portrait; the frame's wood the aged wood (the puzzle pieces keep their wooden backs).
        private static void Swap(string path, Material portrait, Material frame, List<string> done)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = 0;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = r.sharedMaterials;
                    bool piece = r.name.Contains("_Puzzle_");
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] == null) continue;
                        if (slots[i].name == CanvasMaterial) { slots[i] = portrait; changed++; }
                        else if (frame != null && !piece && (slots[i].name == FrameMaterial || slots[i].name == "Lair_Gilded_Frame_Mat") && slots[i] != frame) { slots[i] = frame; changed++; }
                    }
                    r.sharedMaterials = slots;
                }
                if (changed == 0) return;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                done.Add($"{Path.GetFileNameWithoutExtension(path)}: {changed} slots");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
