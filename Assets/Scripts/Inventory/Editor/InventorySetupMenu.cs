using System;
using System.Collections.Generic;
using Game.Inventory;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Menu commands that create the inventory assets and scene objects (Tools > 429 Game > Inventory).</summary>
    public static class InventorySetupMenu
    {
        private const string DataFolder = "Assets/Data/Inventory";
        private const string DatabasePath = DataFolder + "/ItemDatabase.asset";
        private const string CandyPath = SetupUtility.ItemsFolder + "/Candy.asset";

        [MenuItem("Tools/429 Game/Inventory/Set Up In Open Scene")]
        public static void SetUpInOpenScene()
        {
            var database = EnsureAssets();
            var notes = new List<string>();

            var inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (inventory == null)
            {
                var go = new GameObject("Inventory");
                Undo.RegisterCreatedObjectUndo(go, "Create Inventory");
                inventory = go.AddComponent<PlayerInventory>();
                notes.Add("created 'Inventory'");
            }
            SetupUtility.SetReference(inventory, "database", database);

            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            Transform head = null;
            if (origin != null && origin.Camera != null) head = origin.Camera.transform;
            else if (Camera.main != null) head = Camera.main.transform;
            if (head == null) notes.Add("WARNING: no XR camera found, set the Head field on Wrist Inventory yourself");

            // Items are collected when they touch the Player (e.g. an "Inventory Add Item" action in their FSM),
            // so a chest Stash Zone is optional and no longer created. An existing one is still wired up.
            var zone = Object.FindAnyObjectByType<StashZone>(FindObjectsInactive.Include);
            if (zone != null)
            {
                if (head != null) SetupUtility.SetReference(zone, "head", head);
                SetupUtility.SetReference(zone, "inventory", inventory);
            }

            var font = SetupUtility.ResolveFont();
            if (font == null) notes.Add("WARNING: no TextMeshPro font found, assign one to the Wrist Inventory texts");

            var wrist = Object.FindAnyObjectByType<WristInventoryUI>(FindObjectsInactive.Include);
            if (wrist == null)
            {
                var leftHand = XRRig.FindController(left: true);
                if (leftHand == null) notes.Add("WARNING: no 'Left Controller' found, move 'Wrist Inventory' onto your left hand");
                wrist = CreateWristUI(leftHand, font);
                notes.Add($"created 'Wrist Inventory' under '{(leftHand != null ? leftHand.name : "the scene root")}'");
            }
            if (head != null) SetupUtility.SetReference(wrist, "head", head);

            int fontsFixed = SetupUtility.AssignMissingFonts(wrist, font);
            if (fontsFixed > 0) notes.Add($"gave {fontsFixed} Wrist Inventory text(s) the font '{font.name}'");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = inventory.gameObject;
            Debug.Log("[Inventory Setup] Done: " + (notes.Count > 0 ? string.Join("; ", notes) : "everything was already set up") + ". Save the scene to keep it.");
        }

        [MenuItem("Tools/429 Game/Inventory/Make Selected Stashable")]
        public static void MakeSelectedStashable()
        {
            var items = SetupUtility.FindAllItems();
            var item = items.Count == 1 ? items[0] : items.Find(i => i.name == "Candy");
            int changed = 0;

            foreach (var go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    var path = AssetDatabase.GetAssetPath(go);
                    if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;

                    var contents = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if (AddStashable(contents, item, false))
                        {
                            PrefabUtility.SaveAsPrefabAsset(contents, path);
                            changed++;
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }
                else if (AddStashable(go, item, true))
                {
                    changed++;
                }
            }

            string itemNote = item != null ? $"Item set to '{item.name}'" : "Item left empty, set it on each one";
            Debug.Log($"[Inventory Setup] Made {changed} object(s) stashable. {itemNote}.");
        }

        [MenuItem("Tools/429 Game/Inventory/Make Selected Stashable", true)]
        private static bool CanMakeSelectedStashable()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem("Tools/429 Game/Inventory/Refresh Item Database")]
        public static void RefreshItemDatabase()
        {
            var database = EnsureAssets();
            Selection.activeObject = database;
            Debug.Log($"[Inventory Setup] The item database lists {database.Items.Count} item(s).");
        }

        [MenuItem("Tools/429 Game/Inventory/Make Icons For Selected Items")]
        public static void MakeIconsForSelectedItems()
        {
            int queued = 0;
            foreach (var selected in Selection.objects)
            {
                if (selected is ItemDefinition item && item.WorldPrefab != null)
                {
                    PrefabIconMaker.Queue(item.WorldPrefab, item);
                    queued++;
                }
            }
            Debug.Log(queued > 0
                ? $"[Item Icons] Making {queued} icon(s) from the items' World Prefabs. They appear in Assets/Data/Inventory/Icons in a moment."
                : "[Item Icons] Select item assets that have a World Prefab first.");
        }

        /// <summary>Makes sure the Candy item and the item database exist, and that the database lists every item.</summary>
        internal static ItemDatabase EnsureAssets()
        {
            SetupUtility.EnsureFolder(SetupUtility.ItemsFolder);

            if (SetupUtility.FindAllItems().Count == 0)
            {
                var candy = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(candy, CandyPath);

                // Candy is the money, so keep it at the top of lists.
                var serialized = new SerializedObject(candy);
                serialized.FindProperty("listOrder").intValue = -1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            if (database == null)
            {
                var existing = AssetDatabase.FindAssets("t:" + nameof(ItemDatabase));
                if (existing.Length > 0)
                    database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(existing[0]));
            }
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            var items = SetupUtility.FindAllItems();
            var ids = new Dictionary<string, ItemDefinition>();
            foreach (var item in items)
            {
                if (item.EditorFreezeId()) EditorUtility.SetDirty(item);

                if (ids.TryGetValue(item.Id, out var other))
                    Debug.LogError($"[Inventory Setup] '{item.name}' and '{other.name}' share the id '{item.Id}'. Give each item its own id.", item);
                else
                    ids.Add(item.Id, item);
            }

            database.EditorSetItems(items);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return database;
        }

        private static bool AddStashable(GameObject go, ItemDefinition item, bool recordUndo)
        {
            if (go.GetComponent<StashableItem>() != null) return false;

            var stashable = recordUndo ? Undo.AddComponent<StashableItem>(go) : go.AddComponent<StashableItem>();
            if (item != null) SetupUtility.SetReference(stashable, "item", item, recordUndo);
            return true;
        }

        private static WristInventoryUI CreateWristUI(Transform parent, TMP_FontAsset font)
        {
            var root = new GameObject("Wrist Inventory", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(root, "Create Wrist Inventory");
            root.layer = LayerMask.NameToLayer("UI");

            var rect = (RectTransform)root.transform;
            if (parent != null) rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(160f, 200f);
            rect.localPosition = new Vector3(0f, 0.06f, 0.02f);
            rect.localRotation = Quaternion.Euler(45f, 0f, 0f);
            rect.localScale = Vector3.one * 0.0008f;

            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

            var background = SetupUtility.CreateImage("Background", rect, new Color(0.08f, 0.06f, 0.12f, 0.85f));
            SetupUtility.Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

            var title = SetupUtility.CreateText("Title", rect, "Inventory", 22f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 32f);
            titleRect.anchoredPosition = new Vector2(0f, -6f);

            var rows = SetupUtility.CreateRect("Rows", rect);
            SetupUtility.Stretch(rows, new Vector2(10f, 10f), new Vector2(-10f, -42f));
            var list = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = 4f;
            list.childAlignment = TextAnchor.UpperLeft;
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;

            var empty = SetupUtility.CreateText("Empty", rows, "Nothing yet", 16f, TextAlignmentOptions.Center, FontStyles.Italic, font);
            SetupUtility.SetLayout(empty.gameObject, preferredHeight: 24f);

            var row = SetupUtility.CreateRect("Row Template", rows);
            SetupUtility.SetLayout(row.gameObject, preferredHeight: 28f);
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 6f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = true;

            var icon = SetupUtility.CreateImage("Icon", row, Color.white);
            icon.preserveAspect = true;
            SetupUtility.SetLayout(icon.gameObject, preferredWidth: 24f);

            var label = SetupUtility.CreateText("Name", row, "Item", 16f, TextAlignmentOptions.Left, FontStyles.Normal, font);
            SetupUtility.SetLayout(label.gameObject, flexibleWidth: 1f);
            var count = SetupUtility.CreateText("Count", row, "x0", 16f, TextAlignmentOptions.Right, FontStyles.Bold, font);
            SetupUtility.SetLayout(count.gameObject, preferredWidth: 44f);

            var rowView = row.gameObject.AddComponent<WristInventoryRow>();
            SetupUtility.SetReference(rowView, "icon", icon);
            SetupUtility.SetReference(rowView, "label", label);
            SetupUtility.SetReference(rowView, "count", count);
            row.gameObject.SetActive(false);

            var ui = root.AddComponent<WristInventoryUI>();
            SetupUtility.SetReference(ui, "rowTemplate", rowView);
            SetupUtility.SetReference(ui, "rowParent", rows);
            SetupUtility.SetReference(ui, "emptyLabel", empty.gameObject);
            return ui;
        }
    }
}
