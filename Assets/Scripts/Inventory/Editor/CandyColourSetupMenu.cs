using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Bonus;
using Game.EditorTools;
using Game.Enemies;
using Game.PlayGround;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Inventory.Editor
{
    /// <summary>
    /// Tools > 429 Game > Balance 2026-11-10 > Candy Colours (user, 2026-11-10: six candies, one per stat, and six slime
    /// colours to match the candies, a slime dropping its own colour's candy). Reads Assets/Data/Inventory/Candy_Colours.asset
    /// (makes it the first time, or again if it still lists a retired candy or the first placeholder palette, with the
    /// colours the user picked from the slime picture: Candy_1 Pumpkin Candy orange, 2 teal, 4 red-orange, 5 Marshmallow
    /// coral, 7 olive, 8 red) and:
    /// - gives every row its Slime Skin: Materials/Flying_Slime/Slime_Candy_N_Mat, Slime_Blue_Mat's painted look in the
    ///   row's colour (made once, its colour set again every run);
    /// - dresses the big PlayGround slimes in their candy's skin and sets their Colour Candy: Slime_Big = Candy_1,
    ///   Slime_Big_Blue = Candy_2, Slime_Big_Yellow = Candy_4, Slime_Big_Red = Candy_8 (the prefab names stay); the bowling
    ///   showdown slimes, variants of these, keep the look they had (an override); Slime_Small's Colour Candy is emptied,
    ///   so a split drops its parent's colour, or random when the parent has none;
    /// - Colour Candies on Paint_Bees_Round (one per paint colour), matched by hue (Candy Colours' For Colour);
    /// - links the table to the cauldron (Fire_Woods_n_Cauldron): a wrong recipe's slime comes in the colour of a candy
    ///   the recipe still needs;
    /// - gives the Rainbow Slime its own rainbow material (429/Slime Lit with Rainbow on), so it is rainbow in the Editor
    ///   too, not only once its Flying Slime script runs (user, 2026-11-10: "make rainbow Slime rainbow colour").
    /// It never touches the candies themselves (user, 2026-11-10: their materials, glow rings, recipe pictures and the
    /// deco candies are the user's own work). Set each row's colour to the candy's real colour after repainting it, and
    /// run this again so the slimes match. Every file it changes is first copied to
    /// Documents/429R_claude_backups/2026-11-10/candies. Running it again gives the same result.
    /// </summary>
    public static class CandyColourSetupMenu
    {
        private const string MenuPath = "Tools/429 Game/Balance 2026-11-10/Candy Colours";
        private const string Tag = "[Candy Colours] ";
        private const string TablePath = "Assets/Data/Inventory/Candy_Colours.asset";
        private const string SlimeFolder = "Assets/Prefabs/Enemies/PLAYGROUND/";
        private const string RoundPath = "Assets/Prefabs/Bases/Paint_Bees_Round.prefab";
        private const string CauldronPath = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        // One skin per candy, by candy number.
        private const string SkinPath = "Assets/Materials/Flying_Slime/Slime_Candy_{0}_Mat.mat";
        private const string RainbowSlimePath = "Assets/Prefabs/Enemies/Rainbow_Slime.prefab";
        private const string RainbowSkinPath = "Assets/Materials/Flying_Slime/Rainbow_Slime_Lit_Mat.mat";
        // The painted slime look a new colour's skin is copied from.
        private const string SkinSourcePath = "Assets/Materials/Flying_Slime/Slime_Blue_Mat.mat";
        private const string BackupDay = "2026-11-10";
        private const string BackupArea = "candies";

        // The 6 candies in the game (Candy Kinds) and their slime colour (user, 2026-11-10: the retinted slimes from the
        // slime-and-candy picture, matched to the remodelled candies). A new table starts with these.
        private static readonly (int candy, string name, string hex)[] Defaults =
        {
            (1, "Orange", "#F0A030"),    // Pumpkin Candy (candy corn), Max Health
            (2, "Teal", "#3FA39A"),      // teal ball, Weapon Efficiency
            (4, "RedOrange", "#D2541C"), // reversed candy corn, Move Speed
            (5, "Coral", "#E8857A"),     // Marshmallow, Toughness
            (7, "Olive", "#A39A2A"),     // olive-iris eyeball, Throw Power
            (8, "Red", "#D0282C"),       // red-iris eyeball, Fist Size
        };

        // The first placeholder palette (before the candies were remodelled): a table still exactly like it is updated
        // to the colours above.
        private static readonly string[] OldNames = { "Orange", "Blue", "Yellow", "Pink", "Green", "Red" };

        // The big PlayGround slimes and the candy each one drops (and wears the colour of).
        private static readonly (string name, int candy)[] Slimes =
        {
            ("Slime_Big", 1),
            ("Slime_Big_Blue", 2),
            ("Slime_Big_Yellow", 4),
            ("Slime_Big_Red", 8),
        };

        // Variants of those slimes that keep their own look (the bowling showdown, user 2026-11-08).
        private static readonly string[] KeepLook =
        {
            "Assets/Prefabs/Bowling/Slime_Showdown_Blue.prefab",
            "Assets/Prefabs/Bowling/Slime_Showdown_Green.prefab",
            "Assets/Prefabs/Bowling/Slime_Showdown_Red.prefab",
            "Assets/Prefabs/Bowling/Slime_Showdown_Yellow.prefab",
        };

        // Only ever split off a big one, which hands it its own colour candy (Slime Body's Made By Split): its own stays
        // empty, so a split off a slime with no colour drops random candies (white/rainbow/no colour = random).
        private const string SmallSlime = "Slime_Small";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(Tag + "Stop play mode first.");
                return;
            }
            var changed = new List<string>();
            var notes = new List<string>();
            string backup = BackupFolder();
            int copied = Backup(backup, new[] { TablePath });
            var table = LoadOrMakeTable(changed);
            var rows = table.Rows.Where(r => r.candy != null).ToList();
            if (rows.Count < table.Rows.Count) notes.Add($"{table.Rows.Count - rows.Count} row(s) of the table have no candy: skipped");

            // Everything below changes only these: copy them first.
            var paths = new List<string>();
            paths.AddRange(Slimes.Select(s => SlimeFolder + s.name + ".prefab"));
            paths.Add(SlimeFolder + SmallSlime + ".prefab");
            paths.Add(RoundPath);
            paths.Add(CauldronPath);
            paths.AddRange(KeepLook);
            paths.Add(RainbowSlimePath);
            paths.Add(RainbowSkinPath);
            foreach (int n in CandyKinds.Numbers) paths.Add(string.Format(SkinPath, n));
            copied += Backup(backup, paths);

            // 0. Each candy's slime skin, in its colour.
            var skins = SetSlimeSkins(table, changed, notes);

            // 1. The big slimes wear their candy's skin and drop that candy (they hand both to their splits). The bowling
            // showdown slimes are variants of them: their look is read first and given back after.
            var looks = KeepLook.ToDictionary(p => p, ReadLook);
            var mapping = new List<string>();
            foreach (var (name, number) in Slimes)
            {
                var candy = AssetDatabase.LoadAssetAtPath<GameObject>(CandyKinds.PathOf(number));
                skins.TryGetValue(number, out var skin);
                if (candy == null)
                {
                    notes.Add($"{name}: no Candy_{number} (left as it is)");
                    continue;
                }
                mapping.Add($"{name} -> {candy.name}");
                if (Edit(SlimeFolder + name + ".prefab", notes, root => SetSlimeCandy(root, candy, notes) | Dress(root, skin)))
                    changed.Add($"{name} Colour Candy {candy.name}, skin {(skin != null ? skin.name : "unchanged")}");
            }
            foreach (var pair in looks)
            {
                if (pair.Value == null) continue;
                if (Edit(pair.Key, notes, root => GiveLookBack(root, pair.Value)))
                    changed.Add($"{Path.GetFileNameWithoutExtension(pair.Key)} keeps its own look");
            }
            // Emptied every run (an earlier run gave it Candy_3), so it always takes its parent's (review, 2026-11-10).
            mapping.Add($"{SmallSlime} -> its parent's");
            if (Edit(SlimeFolder + SmallSlime + ".prefab", notes, root => SetSlimeCandy(root, null, notes)))
                changed.Add($"{SmallSlime} Colour Candy emptied");

            // 2. Paint Bees: each paint colour's candy (the rainbow bee and the perfect round stay random).
            if (Edit(RoundPath, notes, root => SetRoundCandies(root, table, mapping, notes)))
                changed.Add("Paint_Bees_Round Colour Candies");

            // 3. The cauldron's link to the table (its wrong-recipe slime's colour).
            if (Edit(CauldronPath, notes, root => SetCauldronTable(root, table, notes)))
                changed.Add("Fire_Woods_n_Cauldron Candy Colours");

            // 4. The Rainbow Slime: rainbow in the Editor too.
            var rainbow = RainbowSkin(changed, notes);
            if (rainbow != null && Edit(RainbowSlimePath, notes, root => DressRainbow(root, rainbow, notes)))
                changed.Add("Rainbow_Slime wears " + rainbow.name);

            // Only what this menu changed: never every changed asset in the project (review, 2026-11-10).
            AssetDatabase.SaveAssetIfDirty(table);
            foreach (var skin in skins.Values) AssetDatabase.SaveAssetIfDirty(skin);
            if (rainbow != null) AssetDatabase.SaveAssetIfDirty(rainbow);
            string colours = string.Join(", ", rows.Select(r => $"{r.candy.name} {r.colourName} #{ColorUtility.ToHtmlStringRGB(r.colour)}"));
            Debug.Log(Tag + "Colours: " + colours + ". Drops: " + string.Join(", ", mapping) + ". " +
                      (changed.Count > 0 ? "Changed: " + string.Join(", ", changed) : "Nothing to change (already done)") +
                      $". Backed up {copied} files to {backup}" +
                      (notes.Count > 0 ? ". NOTES: " + string.Join("; ", notes) : "") + ".");
        }

        [MenuItem(MenuPath, true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        // The table: made the first time with the default colours; a table that has rows keeps them (tuned by hand), unless
        // a row still names a retired candy (Candy_3, Candy_6): then it starts again from the defaults.
        private static CandyColours LoadOrMakeTable(List<string> changed)
        {
            var table = AssetDatabase.LoadAssetAtPath<CandyColours>(TablePath);
            if (table == null)
            {
                SetupUtility.EnsureFolder(Path.GetDirectoryName(TablePath).Replace('\\', '/'));
                table = ScriptableObject.CreateInstance<CandyColours>();
                AssetDatabase.CreateAsset(table, TablePath);
                changed.Add(TablePath);
            }
            var so = new SerializedObject(table);
            var rows = so.FindProperty("rows");
            bool retired = false;
            for (int i = 0; i < rows.arraySize; i++)
            {
                var candy = rows.GetArrayElementAtIndex(i).FindPropertyRelative("candy").objectReferenceValue;
                if (candy == null) continue;
                foreach (int n in CandyKinds.Retired) retired |= candy.name == "Candy_" + n;
            }
            // Still the first placeholder palette, untouched: update it to the user's colours (user, 2026-11-10).
            bool placeholder = rows.arraySize == OldNames.Length;
            for (int i = 0; placeholder && i < rows.arraySize; i++)
                placeholder = rows.GetArrayElementAtIndex(i).FindPropertyRelative("colourName").stringValue == OldNames[i];
            if (rows.arraySize == 0 || retired || placeholder)
            {
                rows.arraySize = Defaults.Length;
                for (int i = 0; i < Defaults.Length; i++)
                {
                    var row = rows.GetArrayElementAtIndex(i);
                    row.FindPropertyRelative("candy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CandyKinds.PathOf(Defaults[i].candy));
                    row.FindPropertyRelative("colourName").stringValue = Defaults[i].name;
                    row.FindPropertyRelative("colour").colorValue = Hex(Defaults[i].hex);
                    row.FindPropertyRelative("slimeSkin").objectReferenceValue = null;
                }
                if (retired) changed.Add(TablePath + " (started again: it listed a retired candy)");
                else if (placeholder) changed.Add(TablePath + " (placeholder colours -> the slime picture's colours)");
            }
            if (so.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssetIfDirty(table);
            }
            return table;
        }

        private static bool SetSlimeCandy(GameObject root, GameObject candy, List<string> notes)
        {
            var body = root.GetComponentInChildren<SlimeBody>(true);
            if (body == null)
            {
                notes.Add($"{root.name}: no Slime Body");
                return false;
            }
            var so = new SerializedObject(body);
            var field = so.FindProperty("colourCandy");
            if (field == null)
            {
                notes.Add($"{root.name}: Slime Body has no Colour Candy yet (let the scripts compile, then run this again)");
                return false;
            }
            if (field.objectReferenceValue == candy) return false;
            field.objectReferenceValue = candy;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // Every row's slime skin: Slime_Candy_N_Mat, made from Slime_Blue_Mat's painted look, in the row's colour (made
        // once, its colour set again every run). Returns candy number -> skin.
        private static Dictionary<int, Material> SetSlimeSkins(CandyColours table, List<string> changed, List<string> notes)
        {
            var skins = new Dictionary<int, Material>();
            var source = AssetDatabase.LoadAssetAtPath<Material>(SkinSourcePath);
            var so = new SerializedObject(table);
            var rows = so.FindProperty("rows");
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                var candy = row.FindPropertyRelative("candy").objectReferenceValue as GameObject;
                if (candy == null || !int.TryParse(candy.name.Replace("Candy_", ""), out int number)) continue;
                var colour = row.FindPropertyRelative("colour").colorValue;
                string path = string.Format(SkinPath, number);
                var skin = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (skin == null)
                {
                    if (source == null)
                    {
                        notes.Add($"no {SkinSourcePath}: {candy.name} has no slime skin");
                        continue;
                    }
                    skin = new Material(source) { name = Path.GetFileNameWithoutExtension(path) };
                    AssetDatabase.CreateAsset(skin, path);
                    changed.Add(path);
                }
                colour.a = 1f;
                if (skin.GetColor("_BaseColor") != colour)
                {
                    skin.SetColor("_BaseColor", colour);
                    EditorUtility.SetDirty(skin);
                    changed.Add($"{skin.name} #{ColorUtility.ToHtmlStringRGB(colour)}");
                }
                skins[number] = skin;
                var slot = row.FindPropertyRelative("slimeSkin");
                if (slot.objectReferenceValue == skin) continue;
                slot.objectReferenceValue = skin;
                changed.Add($"{candy.name} slime skin {skin.name}");
            }
            if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(table);
            return skins;
        }

        // Every slot of every solid renderer = this skin (what Slime Body's Recolour does at runtime). False if they all
        // were already, or there's no skin.
        private static bool Dress(GameObject root, Material skin)
        {
            if (skin == null) return false;
            bool any = false;
            foreach (var look in root.GetComponentsInChildren<Renderer>(true))
            {
                if (look is ParticleSystemRenderer || look is TrailRenderer || look is LineRenderer) continue;
                any |= SetMaterials(look, skin);
            }
            return any;
        }

        // The materials of each solid renderer, by its path under the root: what a showdown slime looks like now.
        private static Dictionary<string, Material[]> ReadLook(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var look = new Dictionary<string, Material[]>();
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                look[PathOf(r.transform, prefab.transform)] = r.sharedMaterials;
            }
            return look;
        }

        // Gives a showdown slime back the look it had (an override where its base changed). False if nothing differed.
        private static bool GiveLookBack(GameObject root, Dictionary<string, Material[]> look)
        {
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!look.TryGetValue(PathOf(r.transform, root.transform), out var mats)) continue;
                var so = new SerializedObject(r);
                var slots = so.FindProperty("m_Materials");
                bool differ = slots.arraySize != mats.Length;
                for (int i = 0; !differ && i < mats.Length; i++) differ = slots.GetArrayElementAtIndex(i).objectReferenceValue != mats[i];
                if (!differ) continue;
                slots.arraySize = mats.Length;
                for (int i = 0; i < mats.Length; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = mats[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                any = true;
            }
            return any;
        }

        private static string PathOf(Transform t, Transform root)
        {
            string path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return t == root ? "" : path;
        }

        // Every material slot of the renderer = this material (through its SerializedObject, so a nested prefab's renderer
        // gets an override). False if they all were already.
        private static bool SetMaterials(Renderer look, Material material)
        {
            var so = new SerializedObject(look);
            var slots = so.FindProperty("m_Materials");
            bool any = false;
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                if (slot.objectReferenceValue == material) continue;
                slot.objectReferenceValue = material;
                any = true;
            }
            if (any) so.ApplyModifiedPropertiesWithoutUndo();
            return any;
        }

        // The Rainbow Slime's own skin: a copy of the Flying Slime's 429/Slime Lit skin with Rainbow on and a white paint
        // colour (the rainbow stripes show as they are). Its Flying Slime script sets the same at runtime, so the game
        // looks the same; this makes it rainbow in the Editor and the prefab view too. Made once, its settings set again.
        private static Material RainbowSkin(List<string> changed, List<string> notes)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RainbowSlimePath);
            var slime = prefab != null ? prefab.GetComponentInChildren<FlyingSlime>(true) : null;
            var body = slime != null ? new SerializedObject(slime).FindProperty("body").objectReferenceValue as Renderer : null;
            var source = body != null ? body.sharedMaterial : null;
            var skin = AssetDatabase.LoadAssetAtPath<Material>(RainbowSkinPath);
            if (skin == null)
            {
                if (source == null || source == AssetDatabase.LoadAssetAtPath<Material>(RainbowSkinPath) || !source.HasProperty("_Rainbow"))
                {
                    notes.Add($"{RainbowSlimePath}: no 429/Slime Lit body skin to copy, so no rainbow material was made");
                    return null;
                }
                skin = new Material(source) { name = Path.GetFileNameWithoutExtension(RainbowSkinPath) };
                AssetDatabase.CreateAsset(skin, RainbowSkinPath);
                changed.Add(RainbowSkinPath);
            }
            bool set = false;
            if (skin.GetFloat("_Rainbow") != 1f) { skin.SetFloat("_Rainbow", 1f); skin.EnableKeyword("_RAINBOW_ON"); set = true; }
            if (skin.GetColor("_BaseColor") != Color.white) { skin.SetColor("_BaseColor", Color.white); set = true; }
            if (set)
            {
                EditorUtility.SetDirty(skin);
                changed.Add(skin.name + " rainbow on");
            }
            return skin;
        }

        // The Rainbow Slime's body and both wings wear the rainbow skin (its eyes and stem keep theirs).
        private static bool DressRainbow(GameObject root, Material skin, List<string> notes)
        {
            var slime = root.GetComponentInChildren<FlyingSlime>(true);
            if (slime == null)
            {
                notes.Add("Rainbow_Slime: no Flying Slime");
                return false;
            }
            var so = new SerializedObject(slime);
            bool any = false;
            var body = so.FindProperty("body").objectReferenceValue as Renderer;
            if (body != null) any |= SetFirstMaterial(body, skin);
            foreach (var wing in new[] { "wingLeft", "wingRight" })
            {
                var t = so.FindProperty(wing).objectReferenceValue as Transform;
                if (t != null && t.TryGetComponent<Renderer>(out var r)) any |= SetFirstMaterial(r, skin);
            }
            return any;
        }

        // The first material slot only (the skin); other slots stay.
        private static bool SetFirstMaterial(Renderer look, Material material)
        {
            var so = new SerializedObject(look);
            var slots = so.FindProperty("m_Materials");
            if (slots.arraySize == 0 || slots.GetArrayElementAtIndex(0).objectReferenceValue == material) return false;
            slots.GetArrayElementAtIndex(0).objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool SetCauldronTable(GameObject root, CandyColours table, List<string> notes)
        {
            var cauldron = root.GetComponentInChildren<Cauldron>(true);
            if (cauldron == null)
            {
                notes.Add($"{root.name}: no Cauldron");
                return false;
            }
            var so = new SerializedObject(cauldron);
            var field = so.FindProperty("candyColours");
            if (field == null)
            {
                notes.Add($"{root.name}: Cauldron has no Candy Colours yet (let the scripts compile, then run this again)");
                return false;
            }
            if (field.objectReferenceValue == table) return false;
            field.objectReferenceValue = table;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // One candy per paint colour, in the order of the round's Colours.
        private static bool SetRoundCandies(GameObject root, CandyColours table, List<string> mapping, List<string> notes)
        {
            var round = root.GetComponentInChildren<PaintBeesRound>(true);
            if (round == null)
            {
                notes.Add($"{root.name}: no Paint Bees Round");
                return false;
            }
            var so = new SerializedObject(round);
            var colours = so.FindProperty("colours");
            var list = so.FindProperty("colourCandies");
            if (colours == null || list == null)
            {
                notes.Add($"{root.name}: Paint Bees Round has no Colour Candies yet (let the scripts compile, then run this again)");
                return false;
            }
            bool any = list.arraySize != colours.arraySize;
            list.arraySize = colours.arraySize;
            for (int i = 0; i < colours.arraySize; i++)
            {
                var colour = colours.GetArrayElementAtIndex(i).colorValue;
                var candy = table.ForColour(colour);
                mapping.Add($"paint #{ColorUtility.ToHtmlStringRGB(colour)} -> {(candy != null ? candy.name : "random")}");
                var slot = list.GetArrayElementAtIndex(i);
                if (slot.objectReferenceValue == candy) continue;
                slot.objectReferenceValue = candy;
                any = true;
            }
            if (any) so.ApplyModifiedPropertiesWithoutUndo();
            return any;
        }

        // Opens the prefab on its own, changes it and saves it (only if the change says it changed something). A prefab
        // open in Prefab Mode is skipped: saving under it would be lost or overwritten.
        private static bool Edit(string path, List<string> notes, System.Func<GameObject, bool> change)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add("no " + path);
                return false;
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
            {
                notes.Add(path + " is open in Prefab Mode: skipped (close it, then run this again)");
                return false;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!change(root)) return false;
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) notes.Add("couldn't save " + path);
                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Documents/429R_claude_backups/2026-11-10/candies. The first run's copies are the untouched originals, so a
        // later run copies into its own time-stamped folder inside it.
        private static string BackupFolder()
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", BackupDay, BackupArea);
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
                folder = Path.Combine(folder, "run_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            return folder;
        }

        // Copies each file that's there now (and its .meta), keeping its Assets/... path. Returns how many.
        private static int Backup(string folder, IEnumerable<string> assetPaths)
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            int copied = 0;
            foreach (var path in assetPaths.Where(p => !string.IsNullOrEmpty(p)).Distinct())
            {
                foreach (var file in new[] { path, path + ".meta" })
                {
                    string from = Path.Combine(project, file);
                    if (!File.Exists(from)) continue;
                    string to = Path.Combine(folder, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    File.Copy(from, to, true);
                    copied++;
                }
            }
            return copied;
        }

        private static Color Hex(string html) => ColorUtility.TryParseHtmlString(html, out var colour) ? colour : Color.white;
    }
}
