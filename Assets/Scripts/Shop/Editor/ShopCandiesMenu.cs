using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Inventory;
using Game.Leveling;
using Game.Shopping;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Balance 2026-11-10 > Shop Candies And Candy Box (user, 2026-11-10):
    /// - The candies (Candy Kinds) on sale: each gets an item, Data/Inventory/Items/Candy_N (id Candy_N, World Prefab the
    ///   real candy, so a bought one is eaten like any candy), with a shop icon drawn from the candy as it is now into its
    ///   own file, Data/Inventory/Icons/Candy_N.png (the recipe pictures in Data/Cooking/Icons are only redrawn by
    ///   Remake Icons, user 2026-11-10). The
    ///   catalog's Candies list sells them at 6 wraps, 2 different ones a restock, on rows of their own that never take
    ///   a normal item's place (the panel adds those rows while the game runs, so the scene isn't changed).
    /// - The Candy Box, 26 wraps on the normal list: Prefabs/Items/Breakable/Candy_Box, a copy of Paper_Box_0 (its look,
    ///   grab, health and break) with a pink glow, breaking into Prefabs/Items/Broken_Stuffs/Candy_Box_Drop, a copy of
    ///   Paper_Box_0_Drop (its pieces and puff) without the boxes that one spawns, plus Candy Box Spill (one of each
    ///   candy, tossed gently). It isn't one of Paper_Box_1-5. Its item is Data/Inventory/Items/Candy_Box.
    /// Missing things are made; prices and counts are set to the user's numbers; a name or an icon made once is kept
    /// ("(Remake Icons)" draws the shop icons again after a remodel). Everything it changes is first copied to
    /// Documents/429R_claude_backups/2026-11-10/shop/Assets/... (the first copy is kept). Running it again changes
    /// nothing. Never in play mode; a prefab open in Prefab Mode is skipped. The scene isn't touched.
    /// </summary>
    public static class ShopCandiesMenu
    {
        private const string MenuPath = "Tools/429 Game/Balance 2026-11-10/Shop Candies And Candy Box";
        private const string IconsMenuPath = MenuPath + " (Remake Icons)";
        // The cauldron's recipe pictures (one per candy, linked by its Cooking Ingredient).
        private const string RecipeIconFolder = "Assets/Data/Cooking/Icons";
        private const string Tag = "[Shop Candies] ";

        // The user's numbers (2026-11-10).
        private const int CandyPrice = 6;
        private const int CandyRows = 2;
        private const int BoxPrice = 26;

        private const string CatalogPath = "Assets/Data/Shop/Candy Shop.asset";
        private const string DatabasePath = "Assets/Data/Inventory/ItemDatabase.asset";
        private const string IconFolder = "Assets/Data/Inventory/Icons";
        private const string PaperBoxPath = "Assets/Prefabs/Items/Breakable/Paper_Box_0.prefab";
        private const string PaperDropPath = "Assets/Prefabs/Items/Broken_Stuffs/Paper_Box_0_Drop.prefab";
        private const string BoxPath = "Assets/Prefabs/Items/Breakable/Candy_Box.prefab";
        private const string DropPath = "Assets/Prefabs/Items/Broken_Stuffs/Candy_Box_Drop.prefab";
        private const string BoxName = "Candy_Box";
        private const string BoxShownName = "Candy Box";
        private const string GlowName = "GrowthParticles";
        // Candy pink, so the Candy Box can be told from Paper_Box_0 (its glow is brown-orange).
        private static readonly Color BoxGlow = new(1f, 0.45f, 0.78f, 1f);

        // How the candies look today (the user's remodel, 2026-11-10); the stat's HUD tag goes after it.
        // Candy number -> degrees its shop icon is turned so its face shows (the eyeballs' iris points away from the
        // icon camera). Set after a test render (2026-11-10).
        private static readonly Dictionary<int, float> IconYaws = new()
        {
            { 7, 180f },
            { 8, 180f },
        };

        private static readonly Dictionary<int, string> Looks = new()
        {
            { 1, "Pumpkin Candy" },
            { 2, "Teal Candy" },
            { 4, "Candy Corn" },
            { 5, "Marshmallow" },
            { 7, "Olive Eyeball" },
            { 8, "Red Eyeball" },
        };

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (!CanRun()) return;
            var notes = new List<string>();
            bool fsmEdited = false;

            var candies = CandyPrefabs(notes);
            var drop = BuildDrop(candies.Select(c => c.prefab).ToArray(), notes);
            var box = drop != null ? BuildBox(drop, notes, ref fsmEdited) : null;

            var ours = new List<ItemDefinition>();
            var candyItems = new List<ItemDefinition>();
            foreach (var (number, prefab) in candies)
            {
                var item = EnsureItem("Candy_" + number, ShownName(number), prefab, notes);
                if (item == null) continue;
                candyItems.Add(item);
                ours.Add(item);
            }
            ItemDefinition boxItem = null;
            if (box != null)
            {
                boxItem = EnsureItem(BoxName, BoxShownName, box, notes);
                if (boxItem != null) ours.Add(boxItem);
            }

            FillCatalog(candyItems, boxItem, notes);
            ListInDatabase(ours, notes);
            SaveOwn(ours, notes);
            Debug.Log(Tag + "Done:\n" + string.Join("\n", notes));
            // PlayMaker keeps the actions it read from prefab assets: reload so the Candy Box's new drop is used.
            if (fsmEdited) EditorUtility.RequestScriptReload();
        }

        [MenuItem(MenuPath, true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        /// <summary>
        /// Draws the shop icons of the candy items and the Candy Box, and the 6 candies' recipe pictures in
        /// Data/Cooking/Icons, again from their prefabs as they are now (e.g. after
        /// a candy's remodel), into their own files in Data/Inventory/Icons (old ones backed up). An icon that isn't the
        /// shop's own file (one picked by hand) is left alone.
        /// </summary>
        [MenuItem(IconsMenuPath)]
        public static void RemakeIcons()
        {
            if (!CanRun()) return;
            var notes = new List<string>();
            foreach (int number in CandyKinds.Numbers) Redraw($"{SetupUtility.ItemsFolder}/Candy_{number}.asset", notes);
            Redraw($"{SetupUtility.ItemsFolder}/{BoxName}.asset", notes);
            // The cauldron's recipe pictures too (user, 2026-11-10: "redraw the recipe icons too").
            foreach (int number in CandyKinds.Numbers) RedrawRecipe(number, notes);
            Debug.Log(Tag + "Icons:\n" + string.Join("\n", notes));
        }

        [MenuItem(IconsMenuPath, true)]
        private static bool IconsNotPlaying() => !EditorApplication.isPlaying;

        private static bool CanRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(Tag + "Stop play mode first. Nothing changed.");
                return false;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError(Tag + "Unity is still compiling scripts: wait for it, then run this again. Nothing changed.");
                return false;
            }
            return true;
        }

        // ---- The candies ----

        private static List<(int number, GameObject prefab)> CandyPrefabs(List<string> notes)
        {
            var found = new List<(int, GameObject)>();
            foreach (int number in CandyKinds.Numbers)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyKinds.PathOf(number));
                if (prefab == null) notes.Add($"WARNING: no {CandyKinds.PathOf(number)}: Candy_{number} isn't sold or spilled.");
                else found.Add((number, prefab));
            }
            return found;
        }

        // E.g. "Candy Corn +SPD": how it looks, and the HUD's tag for its stat.
        private static string ShownName(int number) =>
            (Looks.TryGetValue(number, out var look) ? look : "Candy " + number) + " " + StatTag(CandyKinds.StatOf(number));

        private static string StatTag(Stat stat)
        {
            switch (stat)
            {
                case Stat.MaxHealth: return "+HP";
                case Stat.WeaponEfficiency: return "+WPN";
                case Stat.MoveSpeed: return "+SPD";
                case Stat.Toughness: return "+TOPH";
                case Stat.ThrowPower: return "+THROW";
                case Stat.FistSize: return "+FIST";
                default: return "+" + PlayerLevel.Name(stat).ToUpperInvariant();
            }
        }

        // ---- The Candy Box's drop ----

        // Candy_Box_Drop: Paper_Box_0_Drop's pieces and puff, without its box spawners, plus Candy Box Spill.
        private static GameObject BuildDrop(GameObject[] candies, List<string> notes)
        {
            bool made = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DropPath) == null)
            {
                if (!CopyFrom(PaperDropPath, DropPath, notes)) return null;
                made = true;
            }
            if (OpenInPrefabMode(DropPath))
            {
                notes.Add("Candy_Box_Drop is open in Prefab Mode: skipped (close it and run again).");
                return AssetDatabase.LoadAssetAtPath<GameObject>(DropPath);
            }

            var root = PrefabUtility.LoadPrefabContents(DropPath);
            try
            {
                var said = new List<string>();
                string dropName = Path.GetFileNameWithoutExtension(DropPath);
                if (root.name != dropName)
                {
                    said.Add($"named {root.name} -> {dropName}");
                    root.name = dropName;
                }

                // What Paper_Box_0_Drop made on top of its pieces (its Random_Bottle_Pos spots: Paper_Box_2-5) goes: only
                // candies come out of this one.
                foreach (var (spot, makes) in Spawners(root, notes))
                {
                    if (spot == null) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(spot))
                    {
                        notes.Add($"WARNING: Candy_Box_Drop: {spot.name} is part of another prefab, so it stays; it still makes {makes}.");
                        continue;
                    }
                    said.Add($"removed {spot.name} (it made {makes})");
                    Object.DestroyImmediate(spot);
                }
                Rename(root, said);

                var spill = root.GetComponent<CandyBoxSpill>();
                if (spill == null)
                {
                    spill = root.AddComponent<CandyBoxSpill>();
                    said.Add("added Candy Box Spill");
                }
                var props = new SerializedObject(spill);
                var list = props.FindProperty("candies");
                var old = new List<Object>();
                for (int i = 0; i < list.arraySize; i++) old.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
                if (!old.SequenceEqual(candies))
                {
                    list.arraySize = candies.Length;
                    for (int i = 0; i < candies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = candies[i];
                    props.ApplyModifiedPropertiesWithoutUndo();
                    said.Add($"spills [{Names(old)}] -> [{Names(candies)}]");
                }
                WarnLanaSmoke(root, "Candy_Box_Drop", notes);

                if (said.Count == 0)
                {
                    notes.Add("Candy_Box_Drop: already set up.");
                    return AssetDatabase.LoadAssetAtPath<GameObject>(DropPath);
                }
                if (!made) Backup(DropPath, notes);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, DropPath, out bool ok);
                notes.Add(ok ? $"{DropPath}{(made ? " (new, a copy of Paper_Box_0_Drop)" : "")}: {string.Join("; ", said)}."
                             : $"WARNING: couldn't save {DropPath}.");
                return ok ? saved : null;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Objects under the root whose FSMs create something, and what they create. One on the root itself is only reported.
        private static List<(GameObject spot, string makes)> Spawners(GameObject root, List<string> notes)
        {
            var found = new List<(GameObject, string)>();
            foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                var makes = new List<string>();
                foreach (var (_, action) in Actions(fsm, notes))
                {
                    if (action is CreateObject create && create.gameObject != null && create.gameObject.Value != null)
                        makes.Add(create.gameObject.Value.name);
                }
                if (makes.Count == 0) continue;
                if (fsm.gameObject == root)
                {
                    notes.Add($"WARNING: Candy_Box_Drop itself makes {string.Join(", ", makes)}: left as it is, check it.");
                    continue;
                }
                found.Add((fsm.gameObject, string.Join(", ", makes.Distinct())));
            }
            return found;
        }

        // ---- The Candy Box ----

        // Candy_Box: a copy of Paper_Box_0 whose break makes Candy_Box_Drop, with a pink glow.
        private static GameObject BuildBox(GameObject drop, List<string> notes, ref bool fsmEdited)
        {
            bool made = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoxPath) == null)
            {
                if (!CopyFrom(PaperBoxPath, BoxPath, notes)) return null;
                made = true;
            }
            if (OpenInPrefabMode(BoxPath))
            {
                notes.Add("Candy_Box is open in Prefab Mode: skipped (close it and run again).");
                return AssetDatabase.LoadAssetAtPath<GameObject>(BoxPath);
            }

            var paperDrop = AssetDatabase.LoadAssetAtPath<GameObject>(PaperDropPath);
            var root = PrefabUtility.LoadPrefabContents(BoxPath);
            try
            {
                var said = new List<string>();
                if (root.name != BoxName)
                {
                    said.Add($"named {root.name} -> {BoxName}");
                    root.name = BoxName;
                }

                // Its break makes Candy_Box_Drop instead of Paper_Box_0_Drop (the Create Object on its Drop_Pos).
                int makesDrop = 0;
                var others = new List<string>();
                var edited = new HashSet<FsmState>();
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    bool changed = false;
                    foreach (var (state, action) in Actions(fsm, notes))
                    {
                        if (!(action is CreateObject create) || create.gameObject == null || create.gameObject.Value == null) continue;
                        var makes = create.gameObject.Value;
                        if (makes == drop)
                        {
                            makesDrop++;
                        }
                        else if (makes == paperDrop && !create.gameObject.UseVariable)
                        {
                            create.gameObject.Value = drop;
                            edited.Add(state);
                            changed = true;
                            makesDrop++;
                            said.Add($"{fsm.gameObject.name} makes {makes.name} -> {drop.name}");
                        }
                        else others.Add($"{fsm.gameObject.name} makes {makes.name}");
                    }
                    if (!changed) continue;
                    foreach (var state in fsm.FsmStates)
                    {
                        if (edited.Contains(state)) state.SaveActions();
                    }
                    EditorUtility.SetDirty(fsm);
                    fsmEdited = true;
                }
                if (makesDrop == 0) notes.Add("WARNING: Candy_Box: no Create Object making Paper_Box_0_Drop or Candy_Box_Drop found, so it breaks into nothing. Check its Drop_Pos FSM.");
                if (others.Count > 0) notes.Add("Candy_Box also: " + string.Join("; ", others) + " (left as it is).");

                Rename(root, said);
                SetGlow(root, said, notes);
                WarnLanaSmoke(root, "Candy_Box", notes);

                if (said.Count == 0)
                {
                    notes.Add("Candy_Box: already set up.");
                    return AssetDatabase.LoadAssetAtPath<GameObject>(BoxPath);
                }
                if (!made) Backup(BoxPath, notes);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, BoxPath, out bool ok);
                notes.Add(ok ? $"{BoxPath}{(made ? " (new, a copy of Paper_Box_0)" : "")}: {string.Join("; ", said)}."
                             : $"WARNING: couldn't save {BoxPath}.");
                return ok ? saved : null;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Its own glow colour, so it can be told from Paper_Box_0 in the room.
        private static void SetGlow(GameObject root, List<string> said, List<string> notes)
        {
            var glow = FindChild(root.transform, GlowName);
            var particles = glow != null ? glow.GetComponent<ParticleSystem>() : null;
            if (particles == null)
            {
                notes.Add($"Candy_Box has no {GlowName} particles: its glow is as Paper_Box_0's.");
                return;
            }
            var main = particles.main;
            var old = main.startColor;
            if (old.mode == ParticleSystemGradientMode.Color && old.color == BoxGlow) return;
            main.startColor = new ParticleSystem.MinMaxGradient(BoxGlow);
            said.Add($"{GlowName} start colour {(old.mode == ParticleSystemGradientMode.Color ? old.color.ToString() : old.mode.ToString())} -> {BoxGlow}");
        }

        // Its parts called after Paper_Box (Paper_Box_0_Drop_Pos, the Paper_Box_1_Drop pieces) are called after the Candy Box.
        private static void Rename(GameObject root, List<string> said)
        {
            foreach (var part in root.GetComponentsInChildren<Transform>(true))
            {
                if (part == root.transform || !part.name.Contains("Paper_Box")) continue;
                string name = part.name.Replace("Paper_Box_1_Drop", "Candy_Box_Piece").Replace("Paper_Box_0", BoxName).Replace("Paper_Box", BoxName);
                said.Add($"renamed {part.name} -> {name}");
                part.name = name;
            }
        }

        // Only the shared puffs are used for new things, never Lana's Smoke_Blast: reported, not changed.
        private static void WarnLanaSmoke(GameObject root, string what, List<string> notes)
        {
            foreach (var part in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(part.gameObject)) continue;
                string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(part.gameObject);
                if (!string.IsNullOrEmpty(source) && source.Contains("Lana Studio"))
                    notes.Add($"WARNING: {what}/{part.name} is {source}: swap it for Prefabs/Effects/Smoke/Smoke_Puff.");
            }
        }

        // ---- Items and icons ----

        // The item at Items/<itemName>.asset (or one already selling the prefab), made if missing. A name or icon it
        // already has is kept; a missing icon is drawn from the prefab.
        private static ItemDefinition EnsureItem(string itemName, string shownName, GameObject prefab, List<string> notes)
        {
            string path = $"{SetupUtility.ItemsFolder}/{itemName}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = SetupUtility.FindAllItems().Find(i => i.WorldPrefab == prefab);
                if (item != null) notes.Add($"{itemName}: uses '{AssetDatabase.GetAssetPath(item)}', which already has {prefab.name} as its World Prefab.");
            }

            bool made = false;
            if (item == null)
            {
                SetupUtility.EnsureFolder(SetupUtility.ItemsFolder);
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
                var fresh = new SerializedObject(item);
                fresh.FindProperty("id").stringValue = itemName;
                fresh.FindProperty("displayName").stringValue = shownName;
                fresh.FindProperty("worldPrefab").objectReferenceValue = prefab;
                fresh.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
                made = true;
                notes.Add($"{path}: new item '{shownName}' (id {itemName}, World Prefab {prefab.name}).");
            }
            else
            {
                var props = new SerializedObject(item);
                var said = new List<string>();
                var world = props.FindProperty("worldPrefab");
                if (world.objectReferenceValue != prefab)
                {
                    said.Add($"World Prefab {Name(world.objectReferenceValue)} -> {prefab.name}");
                    world.objectReferenceValue = prefab;
                }
                var id = props.FindProperty("id");
                if (string.IsNullOrEmpty(id.stringValue))
                {
                    said.Add($"id '' -> {item.name}");
                    id.stringValue = item.name;
                }
                var shown = props.FindProperty("displayName");
                if (string.IsNullOrEmpty(shown.stringValue))
                {
                    said.Add($"name '' -> '{shownName}'");
                    shown.stringValue = shownName;
                }
                if (said.Count > 0)
                {
                    Backup(AssetDatabase.GetAssetPath(item), notes);
                    props.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                    notes.Add($"{AssetDatabase.GetAssetPath(item)}: {string.Join("; ", said)}.");
                }
            }

            if (item.Icon == null)
            {
                if (!made) Backup(AssetDatabase.GetAssetPath(item), notes);
                DrawIcon(item, prefab, notes);
            }
            return item;
        }

        // Saves only what this menu made or changed (its items, the catalog, the item database), never every changed
        // asset in the project: that once wrote an unrelated prefab the Editor had in memory (review, 2026-11-10).
        private static void SaveOwn(List<ItemDefinition> ours, List<string> notes)
        {
            foreach (var item in ours) AssetDatabase.SaveAssetIfDirty(item);
            var catalog = FindCatalog(notes);
            if (catalog != null) AssetDatabase.SaveAssetIfDirty(catalog);
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(ItemDatabase)))
            {
                var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (database != null) AssetDatabase.SaveAssetIfDirty(database);
            }
        }

        // How far each candy is turned for its shop icon: the eyeballs' iris faces away from the icon camera.
        private static float IconYaw(ItemDefinition item)
        {
            foreach (var pair in IconYaws)
            {
                if (item.name == "Candy_" + pair.Key) return pair.Value;
            }
            return 0f;
        }

        // The prefab as it is now, drawn into the shop's own icon file (Data/Inventory/Icons/<item>.png).
        private static void DrawIcon(ItemDefinition item, GameObject prefab, List<string> notes)
        {
            string iconPath = $"{IconFolder}/{item.name}.png";
            Backup(iconPath, notes);
            if (PrefabIconMaker.MakeNow(prefab, item, IconYaw(item))) notes.Add($"{iconPath}: icon drawn from {prefab.name}.");
            else
            {
                PrefabIconMaker.Queue(prefab, item);
                notes.Add($"{item.name}: nothing solid to draw, so Unity's preview is used: the icon appears in a moment.");
            }
        }

        // A candy's recipe picture (Data/Cooking/Icons/Candy_N.png, the file its Cooking Ingredient links), drawn again
        // from the candy as it is now, turned like its shop icon. The same file, so every link stays.
        private static void RedrawRecipe(int number, List<string> notes)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyKinds.PathOf(number));
            if (prefab == null)
            {
                notes.Add($"No Candy_{number}: its recipe picture is left as it is.");
                return;
            }
            string path = $"{RecipeIconFolder}/Candy_{number}.png";
            if (prefab.TryGetComponent<Game.PlayGround.CookingIngredient>(out var ingredient) && ingredient.Icon != null)
            {
                string linked = AssetDatabase.GetAssetPath(ingredient.Icon);
                if (linked.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) path = linked;
            }
            Backup(path, notes);
            float yaw = 0f;
            IconYaws.TryGetValue(number, out yaw);
            notes.Add(PrefabIconMaker.MakeSprite(prefab, path, yaw) != null
                ? $"{path}: recipe picture drawn from Candy_{number}."
                : $"Candy_{number}: nothing to draw, its recipe picture is unchanged.");
        }

        private static void Redraw(string itemPath, List<string> notes)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            if (item == null || item.WorldPrefab == null)
            {
                notes.Add($"No {itemPath} (or it has no World Prefab): run {MenuPath} first.");
                return;
            }
            string iconPath = $"{IconFolder}/{item.name}.png";
            if (item.Icon != null && AssetDatabase.GetAssetPath(item.Icon) != iconPath)
            {
                notes.Add($"{item.name}: its icon is {AssetDatabase.GetAssetPath(item.Icon)}, not the shop's own file: left alone.");
                return;
            }
            Backup(itemPath, notes);
            DrawIcon(item, item.WorldPrefab, notes);
        }

        // ---- The catalog and the item database ----

        private static void FillCatalog(List<ItemDefinition> candyItems, ItemDefinition boxItem, List<string> notes)
        {
            var catalog = FindCatalog(notes);
            if (catalog == null) return;
            string path = AssetDatabase.GetAssetPath(catalog);

            bool needed = catalog.CandyRows != CandyRows || catalog.Candies.Any(NotACandy) ||
                          candyItems.Any(item => PriceOf(catalog.Candies, item) != CandyPrice) ||
                          (boxItem != null && PriceOf(catalog.Entries, boxItem) != BoxPrice);
            if (!needed)
            {
                notes.Add($"{path}: already sells the {candyItems.Count} candies at {CandyPrice} ({CandyRows} a restock) and the Candy Box at {BoxPrice}.");
                return;
            }

            Backup(path, notes);
            var said = new List<string>();
            int dropped = catalog.EditorRemoveCandies(NotACandy);
            if (dropped > 0) said.Add($"{dropped} empty or retired candy listing(s) taken off");
            foreach (var item in candyItems)
            {
                int old = catalog.EditorSetCandy(item, CandyPrice);
                if (old != CandyPrice) said.Add(old < 0 ? $"candy row {item.name} at {CandyPrice} (new)" : $"candy row {item.name} {old} -> {CandyPrice}");
            }
            int oldRows = catalog.EditorSetCandyRows(CandyRows);
            if (oldRows != CandyRows) said.Add($"candy rows on sale {oldRows} -> {CandyRows}");
            if (boxItem != null)
            {
                int old = catalog.EditorSetEntry(boxItem, BoxPrice);
                if (old != BoxPrice) said.Add(old < 0 ? $"normal list: {boxItem.name} at {BoxPrice} (new)" : $"normal list: {boxItem.name} {old} -> {BoxPrice}");
            }
            EditorUtility.SetDirty(catalog);
            notes.Add($"{path}: {string.Join("; ", said)}.");
        }

        // The Candy Shop catalog; the one the open scene's shop uses should be the same.
        private static ShopCatalog FindCatalog(List<string> notes)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            if (catalog == null)
            {
                var guids = AssetDatabase.FindAssets("t:" + nameof(ShopCatalog));
                if (guids.Length > 0) catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (catalog == null)
            {
                notes.Add("WARNING: no shop catalog: nothing put on sale (run Tools > 429 Game > Shop > Set Up Candy Shop).");
                return null;
            }
            var shop = Object.FindAnyObjectByType<Shop>(FindObjectsInactive.Include);
            if (shop != null && shop.Catalog != null && shop.Catalog != catalog)
                notes.Add($"WARNING: the scene's shop sells from '{AssetDatabase.GetAssetPath(shop.Catalog)}', not '{AssetDatabase.GetAssetPath(catalog)}'.");
            return catalog;
        }

        // A candy rows' listing to take off: empty, or a candy that's gone or retired (Candy Kinds).
        private static bool NotACandy(ShopEntry entry)
        {
            if (entry == null || entry.item == null || entry.item.WorldPrefab == null) return true;
            string path = AssetDatabase.GetAssetPath(entry.item.WorldPrefab);
            return CandyKinds.Retired.Any(number => CandyKinds.PathOf(number) == path);
        }

        // The price of the item's first listing, or -1 if it isn't listed.
        private static int PriceOf(IReadOnlyList<ShopEntry> list, ItemDefinition item)
        {
            foreach (var entry in list)
            {
                if (entry != null && entry.item == item) return entry.price;
            }
            return -1;
        }

        // Save files find items by id in the item database, so new items must be listed there.
        private static void ListInDatabase(List<ItemDefinition> ours, List<string> notes)
        {
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            if (database != null && ours.All(item => database.Items.Contains(item))) return;
            Backup(DatabasePath, notes);
            var listed = InventorySetupMenu.EnsureAssets();
            notes.Add($"{AssetDatabase.GetAssetPath(listed)}: lists {listed.Items.Count} items, the new ones included.");
        }

        // ---- Helpers ----

        private static bool CopyFrom(string from, string to, List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(from) == null)
            {
                notes.Add($"WARNING: no {from}: {Path.GetFileNameWithoutExtension(to)} not made.");
                return false;
            }
            if (OpenInPrefabMode(from)) notes.Add($"{Path.GetFileNameWithoutExtension(from)} is open in Prefab Mode: copied from its saved file.");
            if (AssetDatabase.CopyAsset(from, to)) return true;
            notes.Add($"WARNING: couldn't copy {from} to {to}.");
            return false;
        }

        // Every action of the FSM with its state; a state whose actions don't all load is reported (and still listed).
        private static List<(FsmState state, FsmStateAction action)> Actions(PlayMakerFSM fsm, List<string> notes)
        {
            var found = new List<(FsmState, FsmStateAction)>();
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            foreach (var state in fsm.FsmStates)
            {
                state.LoadActions();
                if (state.ActionData.ActionCount != state.Actions.Length)
                    notes.Add($"WARNING: {fsm.gameObject.name} / {fsm.FsmName} / {state.Name}: some actions didn't load.");
                foreach (var action in state.Actions)
                {
                    if (action != null) found.Add((state, action));
                }
            }
            return found;
        }

        // Documents/429R_claude_backups/2026-11-10/shop/<the Assets/... path>, with its .meta. The first copy (from before
        // any run) is kept: later runs don't overwrite it.
        private static void Backup(string assetPath, List<string> notes)
        {
            string from = Path.GetFullPath(assetPath);
            if (!File.Exists(from)) return;
            string to = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-10", "shop", assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(to)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(from, to);
            if (File.Exists(from + ".meta")) File.Copy(from + ".meta", to + ".meta", true);
            notes.Add($"backed up {assetPath}");
        }

        private static bool OpenInPrefabMode(string path)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.assetPath == path;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name == childName) return t;
            }
            return null;
        }

        private static string Name(Object thing) => thing != null ? thing.name : "none";

        private static string Names(IEnumerable<Object> things) => string.Join(", ", things.Select(Name));
    }
}
