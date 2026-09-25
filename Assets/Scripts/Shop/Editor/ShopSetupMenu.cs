using System.Collections.Generic;
using System.IO;
using Game.Inventory;
using Game.Shopping;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Menu commands that set up the candy shop (Tools > 429 Game > Shop).</summary>
    public static class ShopSetupMenu
    {
        private const string DefaultCatalogPath = "Assets/Data/Shop/Candy Shop.asset";
        private const string FirstItemPrefabPath = "Assets/Prefabs/Weapon/Hammer_Tool.prefab";
        private const int DefaultPrice = 5;

        [MenuItem("Tools/429 Game/Shop/Set Up Candy Shop")]
        public static void SetUpCandyShop()
        {
            var notes = new List<string>();
            InventorySetupMenu.EnsureAssets();
            var catalog = EnsureCatalog(notes);
            var font = SetupUtility.ResolveFont();

            var shop = Object.FindAnyObjectByType<Shop>(FindObjectsInactive.Include);
            if (shop == null)
            {
                var go = new GameObject("Candy Shop");
                Undo.RegisterCreatedObjectUndo(go, "Create Candy Shop");
                shop = go.AddComponent<Shop>();
                notes.Add("created 'Candy Shop' in the scene");
            }
            SetupUtility.SetReferenceIfEmpty(shop, "catalog", catalog);

            var rightHand = XRRig.FindController(left: false);
            if (rightHand != null) SetupUtility.SetReferenceIfEmpty(shop, "hand", rightHand);
            else notes.Add("WARNING: no 'Right Controller' found, set the Hand field on Candy Shop yourself");

            var panel = Object.FindAnyObjectByType<ShopPanelUI>(FindObjectsInactive.Include);
            if (panel == null)
            {
                panel = CreatePanel(shop.transform, font);
                notes.Add("created its panel");
            }
            else if (SetupUtility.AssignMissingFonts(panel, font) > 0)
            {
                notes.Add("fixed the shop panel's fonts");
            }
            SetupUtility.SetReferenceIfEmpty(shop, "panel", panel);

            // Like the Wrist Inventory on the left hand, the panel lives on the right hand, where you can move it.
            // One that's already there is left where you put it.
            var hand = shop.Hand;
            if (hand != null && !panel.transform.IsChildOf(hand))
            {
                Undo.SetTransformParent(panel.transform, hand, "Put Shop Panel On Hand");
                Undo.RecordObject(panel.transform, "Put Shop Panel On Hand");
                panel.PutOnHand(hand);
                notes.Add($"put the shop panel on '{hand.name}'");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            // Lists any new items in the item database and saves assets.
            InventorySetupMenu.EnsureAssets();
            Selection.activeObject = catalog;
            Debug.Log("[Shop Setup] Done: " + (notes.Count > 0 ? string.Join("; ", notes) : "everything was already set up") +
                      $". Edit what it sells in '{AssetDatabase.GetAssetPath(catalog)}', then save the scene.");
        }

        [MenuItem("Tools/429 Game/Shop/Add Selected Prefabs To Shop")]
        public static void AddSelectedPrefabsToShop()
        {
            var catalog = FindCatalog();
            if (catalog == null)
            {
                Debug.LogError("[Shop Setup] Run Tools > 429 Game > Shop > Set Up Candy Shop first.");
                return;
            }

            int added = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (!IsPrefabAsset(go)) continue;

                var item = SetupUtility.GetOrCreateItemForPrefab(go);
                if (catalog.EditorAddEntry(item, DefaultPrice)) added++;
            }

            EditorUtility.SetDirty(catalog);
            InventorySetupMenu.EnsureAssets();
            Selection.activeObject = catalog;

            string currency = catalog.Currency != null ? catalog.Currency.DisplayName : "currency";
            Debug.Log($"[Shop Setup] Added {added} item(s) at {DefaultPrice} {currency} each. Change prices in '{AssetDatabase.GetAssetPath(catalog)}'. Icons are being made in the background.");
        }

        [MenuItem("Tools/429 Game/Shop/Add Selected Prefabs To Shop", true)]
        private static bool CanAddSelectedPrefabs()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (IsPrefabAsset(go)) return true;
            }
            return false;
        }

        private static bool IsPrefabAsset(GameObject go)
        {
            if (go == null || !EditorUtility.IsPersistent(go)) return false;
            var type = PrefabUtility.GetPrefabAssetType(go);
            return type == PrefabAssetType.Regular || type == PrefabAssetType.Variant;
        }

        private static ShopCatalog FindCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ShopCatalog));
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<ShopCatalog>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        private static ShopCatalog EnsureCatalog(List<string> notes)
        {
            var catalog = FindCatalog();
            if (catalog == null)
            {
                SetupUtility.EnsureFolder(Path.GetDirectoryName(DefaultCatalogPath)?.Replace('\\', '/'));
                catalog = ScriptableObject.CreateInstance<ShopCatalog>();
                AssetDatabase.CreateAsset(catalog, DefaultCatalogPath);
                notes.Add($"created the catalog '{DefaultCatalogPath}'");
            }

            if (catalog.Currency == null)
            {
                var currency = FindCurrency();
                if (currency != null)
                {
                    catalog.EditorSetCurrency(currency);
                    notes.Add($"the shop takes '{currency.DisplayName}'");
                }
            }

            if (catalog.Entries.Count == 0)
            {
                var firstPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirstItemPrefabPath);
                if (firstPrefab != null)
                {
                    var item = SetupUtility.GetOrCreateItemForPrefab(firstPrefab);
                    catalog.EditorAddEntry(item, DefaultPrice);
                    notes.Add($"added '{item.DisplayName}' for {DefaultPrice} as a first item");
                }
            }

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static ItemDefinition FindCurrency()
        {
            ItemDefinition lowest = null;
            foreach (var item in SetupUtility.FindAllItems())
            {
                if (item.name == "Candy") return item;
                if (lowest == null || item.ListOrder < lowest.ListOrder) lowest = item;
            }
            return lowest;
        }

        private static ShopPanelUI CreatePanel(Transform parent, TMP_FontAsset font)
        {
            // Resized when it's put on the hand (ShopPanelUI.PutOnHand).
            var rect = SetupUtility.CreateWorldCanvas("Shop Panel", parent, new Vector2(420f, 330f), new Color(0.1f, 0.07f, 0.16f, 0.92f));

            var title = SetupUtility.CreateText("Title", rect, "Candy Shop", 30f, TextAlignmentOptions.Left, FontStyles.Bold, font);
            SetupUtility.TopBand(title.rectTransform, 10f, 44f, 16f, 70f);

            var close = SetupUtility.CreateButton("Close", rect, "X", 26f, font, new Color(0.6f, 0.22f, 0.28f, 1f));
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(48f, 48f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);

            var money = SetupUtility.CreateRect("Money", rect);
            SetupUtility.TopBand(money, 58f, 32f, 16f, 16f);
            var moneyLine = money.gameObject.AddComponent<HorizontalLayoutGroup>();
            moneyLine.spacing = 8f;
            moneyLine.childAlignment = TextAnchor.MiddleLeft;
            moneyLine.childControlWidth = true;
            moneyLine.childControlHeight = true;
            moneyLine.childForceExpandWidth = false;
            moneyLine.childForceExpandHeight = true;
            var moneyIcon = SetupUtility.CreateImage("Icon", money, Color.white);
            moneyIcon.preserveAspect = true;
            SetupUtility.SetLayout(moneyIcon.gameObject, preferredWidth: 30f);
            var moneyText = SetupUtility.CreateText("Amount", money, "You have 0", 22f, TextAlignmentOptions.Left, FontStyles.Normal, font);
            SetupUtility.SetLayout(moneyText.gameObject, flexibleWidth: 1f);

            var rows = SetupUtility.CreateRect("Rows", rect);
            SetupUtility.Stretch(rows, new Vector2(12f, 50f), new Vector2(-12f, -98f));
            var list = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = 6f;
            list.childAlignment = TextAnchor.UpperLeft;
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;

            var row = SetupUtility.CreateImage("Row Template", rows, new Color(1f, 1f, 1f, 0.06f));
            SetupUtility.SetLayout(row.gameObject, preferredHeight: 52f);
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.padding = new RectOffset(8, 8, 4, 4);
            line.spacing = 10f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = true;

            var icon = SetupUtility.CreateImage("Icon", row.transform, Color.white);
            icon.preserveAspect = true;
            SetupUtility.SetLayout(icon.gameObject, preferredWidth: 44f);
            var label = SetupUtility.CreateText("Name", row.transform, "Item", 22f, TextAlignmentOptions.Left, FontStyles.Normal, font);
            SetupUtility.SetLayout(label.gameObject, flexibleWidth: 1f);
            var priceIcon = SetupUtility.CreateImage("Price Icon", row.transform, Color.white);
            priceIcon.preserveAspect = true;
            SetupUtility.SetLayout(priceIcon.gameObject, preferredWidth: 26f);
            var price = SetupUtility.CreateText("Price", row.transform, "0", 22f, TextAlignmentOptions.Right, FontStyles.Bold, font);
            SetupUtility.SetLayout(price.gameObject, preferredWidth: 56f);
            var buy = SetupUtility.CreateButton("Buy", row.transform, "Buy", 22f, font, new Color(0.3f, 0.58f, 0.32f, 1f));
            SetupUtility.SetLayout(buy.gameObject, preferredWidth: 110f);

            var rowView = row.gameObject.AddComponent<ShopItemRow>();
            SetupUtility.SetReference(rowView, "icon", icon);
            SetupUtility.SetReference(rowView, "label", label);
            SetupUtility.SetReference(rowView, "priceIcon", priceIcon);
            SetupUtility.SetReference(rowView, "price", price);
            SetupUtility.SetReference(rowView, "buyButton", buy);
            SetupUtility.SetReference(rowView, "buyLabel", buy.GetComponentInChildren<TextMeshProUGUI>());
            row.gameObject.SetActive(false);

            var status = SetupUtility.CreateText("Status", rect, "", 20f, TextAlignmentOptions.Center, FontStyles.Italic, font);
            SetupUtility.BottomBand(status.rectTransform, 8f, 36f, 12f, 12f);

            var ui = rect.gameObject.AddComponent<ShopPanelUI>();
            SetupUtility.SetReference(ui, "title", title);
            SetupUtility.SetReference(ui, "currencyIcon", moneyIcon);
            SetupUtility.SetReference(ui, "currencyCount", moneyText);
            SetupUtility.SetReference(ui, "rowTemplate", rowView);
            SetupUtility.SetReference(ui, "rowParent", rows);
            SetupUtility.SetReference(ui, "statusText", status);
            SetupUtility.SetReference(ui, "closeButton", close);
            rect.gameObject.SetActive(false);
            return ui;
        }
    }
}
