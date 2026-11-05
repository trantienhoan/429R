using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > UI > Use Socake Font Everywhere (user 2026-11-05: "use Socake font for the other signs and
    /// notice and everything"). Socake (Assets/Fonts/socake: free for personal and commercial use) becomes:
    /// - TextMesh Pro's default font (every text made by code that doesn't pick one: notices, floating words, coins...),
    ///   with Roboto Bold as its fallback for any letter Socake lacks (it has the 95 plain ASCII ones);
    /// - the font of every text and every font setting in the prefabs and the OPEN scenes (run it again with another
    ///   scene open, e.g. 1 Start Scene; the scenes then need saving);
    /// - the letter sheet of the text materials made for the old fonts (HUD Text Overlay, Save Notice Text, Damage
    ///   Number...): their look (overlay, outline) stays, now drawn with Socake. The fonts' own materials aren't touched.
    /// Bold is taken off the texts it changes (Socake is heavy already; faux bold fills its holes).
    /// Safe to run again. Copies of the materials and TMP Settings before the first run: _claude/backups/socake_2026-11-05.
    /// </summary>
    public static class SocakeFontMenu
    {
        private const string SocakePath = "Assets/Fonts/socake/Socake SDF.asset";
        private const string FallbackPath = "Assets/Fonts/roboto/Roboto-Bold SDF.asset";
        private const string BackupFolder = "K:/WORK_2026/08_04/_claude/backups/socake_2026-11-05/";
        private static readonly string[] SkipFolders = { "Assets/_Recovery", "Assets/XRI_Examples", "Assets/Plugins/TextMesh Pro/Examples", "Assets/Samples" };

        [MenuItem("Tools/429 Game/UI/Use Socake Font Everywhere")]
        public static void Menu()
        {
            var notes = Run();
            Debug.Log("[Socake] " + string.Join("\n", notes));
            EditorUtility.DisplayDialog("Use Socake Font Everywhere", string.Join("\n", notes.Take(30)), "OK");
        }

        public static List<string> Run()
        {
            var notes = new List<string>();
            var socake = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SocakePath);
            if (socake == null) { notes.Add("No " + SocakePath + "."); return notes; }
            Directory.CreateDirectory(BackupFolder);
            ShaderUtilities.GetShaderPropertyIDs();

            // The old fonts: every other TMP font asset.
            var oldFonts = new HashSet<TMP_FontAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null && font != socake) oldFonts.Add(font);
            }
            var oldAtlases = new HashSet<Texture>();
            foreach (var f in oldFonts) foreach (var t in f.atlasTextures ?? new Texture2D[0]) if (t != null) oldAtlases.Add(t);

            // Socake's fallback: Roboto Bold for letters it lacks.
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            if (fallback != null && (socake.fallbackFontAssetTable == null || !socake.fallbackFontAssetTable.Contains(fallback)))
            {
                socake.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                socake.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(socake);
                notes.Add("Socake falls back on Roboto Bold for letters it lacks.");
            }

            // TextMesh Pro's default font.
            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var path = AssetDatabase.GetAssetPath(settings);
                Backup(path);
                var so = new SerializedObject(settings);
                var def = so.FindProperty("m_defaultFontAsset");
                if (def.objectReferenceValue != socake)
                {
                    def.objectReferenceValue = socake;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    notes.Add("TextMesh Pro's default font: Socake.");
                }
            }

            // Text materials drawn with an old font's letter sheet (not the fonts' own): now Socake's.
            int mats = 0;
            var retargeted = new HashSet<Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Skipped(path)) continue;
                // Only text materials of their own (a .mat): never a font's own material, which lives inside the font.
                if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(Material)) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null || !m.shader.name.StartsWith("TextMeshPro") || !m.HasProperty(ShaderUtilities.ID_MainTex)) continue;
                var tex = m.GetTexture(ShaderUtilities.ID_MainTex);
                if (tex == null || !oldAtlases.Contains(tex)) continue;
                Backup(path);
                Retarget(m, socake);
                retargeted.Add(m);
                mats++;
                notes.Add("Material " + Path.GetFileNameWithoutExtension(path) + ": now Socake.");
            }

            // Prefabs that point at an old font (by its GUID in the file).
            var oldGuids = new HashSet<string>(oldFonts.Select(f => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f))));
            int prefabs = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Skipped(path)) continue;
                string text;
                try { text = File.ReadAllText(path); } catch { continue; }
                if (!oldGuids.Any(text.Contains)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = Convert(root, socake, oldFonts, retargeted);
                    if (n > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabs++;
                        notes.Add("Prefab " + Path.GetFileNameWithoutExtension(path) + ": " + n + " font settings.");
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // The open scenes.
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                int n = 0;
                foreach (var root in scene.GetRootGameObjects()) n += Convert(root, socake, oldFonts, retargeted);
                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    notes.Add("Scene " + scene.name + ": " + n + " font settings (SAVE the scene).");
                }
            }
            AssetDatabase.SaveAssets();
            notes.Insert(0, "Socake everywhere: " + mats + " materials, " + prefabs + " prefabs.");
            return notes;
        }

        // Every font setting under root (texts, and fonts other scripts hold) that's an old font: Socake. A text's material
        // goes with it: the old font's own material becomes Socake's; a retargeted preset stays (it's Socake's now).
        private static int Convert(GameObject root, TMP_FontAsset socake, HashSet<TMP_FontAsset> oldFonts, HashSet<Material> retargeted)
        {
            int changed = 0;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform) continue;
                if (component is TMP_Text text)
                {
                    if (text.font != null && oldFonts.Contains(text.font))
                    {
                        var oldMaterial = text.fontSharedMaterial;
                        bool own = oldMaterial == null || oldMaterial == text.font.material;
                        Undo.RecordObject(text, "Socake");
                        text.font = socake;
                        text.fontSharedMaterial = own || !retargeted.Contains(oldMaterial) ? socake.material : oldMaterial;
                        text.fontStyle &= ~FontStyles.Bold;
                        EditorUtility.SetDirty(text);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                        changed++;
                    }
                    continue;
                }
                var so = new SerializedObject(component);
                var it = so.GetIterator();
                bool any = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue is TMP_FontAsset f && oldFonts.Contains(f))
                    {
                        it.objectReferenceValue = socake;
                        any = true;
                        changed++;
                    }
                }
                if (any)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
            }
            return changed;
        }

        // A text material drawn with Socake's letter sheet (its SDF settings too), its own look kept.
        private static void Retarget(Material m, TMP_FontAsset socake)
        {
            var atlas = socake.atlasTexture;
            m.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            var source = socake.material;
            foreach (var id in new[] { ShaderUtilities.ID_TextureWidth, ShaderUtilities.ID_TextureHeight, ShaderUtilities.ID_GradientScale,
                         ShaderUtilities.ID_WeightNormal, ShaderUtilities.ID_WeightBold })
            {
                if (m.HasProperty(id) && source.HasProperty(id)) m.SetFloat(id, source.GetFloat(id));
            }
            ShaderUtilities.UpdateShaderRatios(m);
            EditorUtility.SetDirty(m);
        }

        private static bool Skipped(string path) => SkipFolders.Any(path.StartsWith);

        private static void Backup(string assetPath)
        {
            var to = Path.Combine(BackupFolder, Path.GetFileName(assetPath));
            if (!File.Exists(to)) File.Copy(assetPath, to);
        }
    }
}
