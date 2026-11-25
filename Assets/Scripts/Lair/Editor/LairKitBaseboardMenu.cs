using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Lair > Kit Walls Without Baseboards (user 2026-11-25: "since we have a wood thing texture on
    /// the wall, we can remove the wood under it"). The bedroom kit's wallpaper (Bedroom_Wallpaper_Spooky) now has dark
    /// wood panelling painted on its bottom 0.9 m, so the kit's own wooden baseboard strip (12 cm high, in the wood
    /// material) doubled it. Every wall piece of Prefabs/Bases/Bedroom_Kit_Base.prefab (the storage room, the bedroom and
    /// the secret room; they share that wallpaper) that wears the wallpaper gets a copy of its mesh without the wood
    /// triangles lying wholly in the bottom 12.5 cm: on the plain walls that is the whole strip, on the door and window
    /// pieces only the strips beside the opening (their frames keep their legs). The copies keep the submeshes (an empty
    /// one stays empty), so the materials are untouched; colliders are boxes, unchanged. Copies in Models/Lair/Kit.
    /// Re-runnable; "Back As Before" puts the original meshes back. Re-run it after Build Bedroom Kit Room (which remakes
    /// the kit from Models/Rooms/Bedroom_Kit.fbx). Prefab only, no scene change.
    /// </summary>
    public static class LairKitBaseboardMenu
    {
        private const string Tag = "[Lair Kit]";
        private const string KitPath = "Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab";
        private const string Folder = "Assets/Models/Lair/Kit/";
        private const string Suffix = "_NoBaseboard";
        private const float StripTop = 0.125f;     // the strip is 0.12 m high

        [MenuItem("Tools/429 Game/Lair/Kit Walls Without Baseboards")]
        public static void Remove() => Run(false);

        [MenuItem("Tools/429 Game/Lair/Kit Walls Without Baseboards: Back As Before")]
        public static void BackAsBefore() => Run(true);

        [MenuItem("Tools/429 Game/Lair/Kit Walls Without Baseboards", true)]
        [MenuItem("Tools/429 Game/Lair/Kit Walls Without Baseboards: Back As Before", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Run(bool back)
        {
            var root = PrefabUtility.LoadPrefabContents(KitPath);
            var made = new Dictionary<Mesh, Mesh>();
            var changes = new Dictionary<string, int>();
            try
            {
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var look = mf.GetComponent<MeshRenderer>();
                    var mesh = mf.sharedMesh;
                    if (look == null || mesh == null || InLair(mf.transform)) continue;
                    if (back)
                    {
                        if (!mesh.name.EndsWith(Suffix)) continue;
                        var original = Original(mesh);
                        if (original == null)
                        {
                            Debug.LogError($"{Tag} No original mesh for {mesh.name}; {mf.name} left.");
                            continue;
                        }
                        mf.sharedMesh = original;
                        Count(changes, original.name);
                        continue;
                    }
                    if (mesh.name.EndsWith(Suffix)) continue;
                    int wood = WoodSlot(look, mesh);
                    if (wood < 0) continue;
                    if (!made.TryGetValue(mesh, out var trimmed))
                    {
                        trimmed = Trimmed(mesh, wood);
                        made[mesh] = trimmed;
                    }
                    if (trimmed == null) continue;
                    mf.sharedMesh = trimmed;
                    Count(changes, mesh.name);
                }
                if (changes.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, KitPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            var list = new List<string>();
            foreach (var kv in changes) list.Add($"{kv.Key} x{kv.Value}");
            Debug.Log(changes.Count == 0
                ? $"{Tag} Nothing to change (already {(back ? "as before" : "without baseboards")})."
                : $"{Tag} {(back ? "Baseboards back" : "Baseboards removed")} on {string.Join(", ", list)}. Prefab only, no scene change.");
        }

        // The wood submesh of a wall piece that wears the bedroom kit wallpaper, or -1.
        private static int WoodSlot(MeshRenderer look, Mesh mesh)
        {
            var slots = look.sharedMaterials;
            bool wallpaper = false;
            int wood = -1;
            for (int i = 0; i < slots.Length && i < mesh.subMeshCount; i++)
            {
                if (slots[i] == null) continue;
                if (slots[i].name.StartsWith("Bedroom_Wall")) wallpaper = true;
                else if (slots[i].name.StartsWith("Wood_")) wood = i;
            }
            return wallpaper ? wood : -1;
        }

        // A copy without the wood triangles wholly below the strip's top; null if there are none.
        private static Mesh Trimmed(Mesh source, int wood)
        {
            var vertices = source.vertices;
            var keep = new List<int>();
            var tris = source.GetTriangles(wood);
            int dropped = 0;
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (vertices[tris[i]].y <= StripTop && vertices[tris[i + 1]].y <= StripTop && vertices[tris[i + 2]].y <= StripTop)
                {
                    dropped++;
                    continue;
                }
                keep.Add(tris[i]);
                keep.Add(tris[i + 1]);
                keep.Add(tris[i + 2]);
            }
            if (dropped == 0) return null;
            Directory.CreateDirectory(Folder);
            string path = Folder + source.name + Suffix + ".asset";
            var copy = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = copy == null;
            if (fresh) copy = new Mesh();
            else copy.Clear();
            copy.name = source.name + Suffix;
            copy.indexFormat = source.indexFormat;
            copy.SetVertices(new List<Vector3>(vertices));
            copy.SetNormals(new List<Vector3>(source.normals));
            if (source.tangents.Length == vertices.Length) copy.SetTangents(new List<Vector4>(source.tangents));
            for (int ch = 0; ch < 4; ch++)
            {
                var uv = new List<Vector2>();
                source.GetUVs(ch, uv);
                if (uv.Count == vertices.Length) copy.SetUVs(ch, uv);
            }
            if (source.colors.Length == vertices.Length) copy.SetColors(new List<Color>(source.colors));
            copy.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
                copy.SetTriangles(s == wood ? keep.ToArray() : source.GetTriangles(s), s, false);
            copy.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(copy, path);
            else EditorUtility.SetDirty(copy);
            Debug.Log($"{Tag} {source.name}: {dropped} of {tris.Length / 3} wood triangles below {StripTop} m removed -> {path}");
            return copy;
        }

        // The kit's own mesh a copy was made from (by name, in the kit model or next to it).
        private static Mesh Original(Mesh copy)
        {
            string name = copy.name.Substring(0, copy.name.Length - Suffix.Length);
            foreach (var path in new[] { "Assets/Models/Rooms/Bedroom_Kit.fbx", "Assets/Models/Rooms/" + name + ".asset" })
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is Mesh m && m.name == name) return m;
            return null;
        }

        private static bool InLair(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.StartsWith("Lair_")) return true;
            return false;
        }

        private static void Count(Dictionary<string, int> changes, string name)
        {
            changes.TryGetValue(name, out int n);
            changes[name] = n + 1;
        }
    }
}
