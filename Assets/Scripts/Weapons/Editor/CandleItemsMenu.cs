using System.Collections.Generic;
using Game.Combat;
using Game.Inventory;
using Game.Rooms;

using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.EditorTools
{
    /// <summary>
    /// User 2026-11-26: "check light hang, I want them to be grabable when dropped to the ground and light up like normal
    /// light. Add the Candle_Land to the shop." Two re-runnable menus (prefabs and assets only, no scene change):
    /// - Tools > 429 Game > Items > Hanging Candles Grabbable When Dropped: Candle_Hang (the wall candles of the living
    ///   room and the playroom) gets Candle_Land's grab (its XR Grab Interactable settings, with its own grip point at the
    ///   candle), switched OFF while it hangs (Wall Hung Prop switches it on when the walls drop it), Candle_Land's throw
    ///   damage (Weapon Power) and the Lighter of the other candles: once it has fallen you can pick it up and it is a
    ///   candle like Candle_Land (lit; it goes out when picked up, a shake lights it again, its flame lights boongs). On
    ///   the floor it weighs 5 like Candle_Land (Mass When Dropped). Its wall light (range 8) keeps shining where it lies.
    /// - Tools > 429 Game > Shop > Add Candle To The Shop: an item "Candle" (Data/Inventory/Items/Candle_Land.asset, its
    ///   world prefab Candle_Land, an icon drawn from it, in the item database) and a row in the Candy Shop: 8 candies.
    /// </summary>
    public static class CandleItemsMenu
    {
        private const string Tag = "[Candles]";
        private const string HangPath = "Assets/Prefabs/Light/Candle_Hang.prefab";
        private const string LandPath = "Assets/Prefabs/Light/Candle_Land.prefab";
        private const string TemplateItemPath = "Assets/Data/Inventory/Items/Hammer_Broom.asset";
        private const string ItemPath = "Assets/Data/Inventory/Items/Candle_Land.asset";
        private const string ShopPath = "Assets/Data/Shop/Candy Shop.asset";
        private const string ItemName = "Candle";
        private const int Price = 8;
        private const float DroppedMass = 5f;
        internal const string GreenHangName = "Lair_Candle_Hang_Green";

        [MenuItem("Tools/429 Game/Items/Hanging Candles Grabbable When Dropped")]
        public static void HangingCandles()
        {
            var land = AssetDatabase.LoadAssetAtPath<GameObject>(LandPath);
            var landGrab = land != null ? land.GetComponent<XRGrabInteractable>() : null;
            if (landGrab == null || AssetDatabase.LoadAssetAtPath<GameObject>(HangPath) == null)
            {
                Debug.LogError($"{Tag} {LandPath} (with its grab) or {HangPath} is missing; nothing changed.");
                return;
            }
            var notes = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(HangPath);
            try
            {
                // Its grip: the candle's middle (the bounds of what it draws, not its flame or light).
                var attach = root.transform.Find("attach");
                if (attach == null)
                {
                    attach = new GameObject("attach").transform;
                    attach.SetParent(root.transform, false);
                    notes.Add("grip point added");
                }
                var look = root.GetComponent<MeshRenderer>();
                attach.position = look != null ? look.bounds.center : root.transform.position;
                attach.rotation = root.transform.rotation;

                var grab = root.GetComponent<XRGrabInteractable>();
                if (grab == null)
                {
                    grab = root.AddComponent<XRGrabInteractable>();
                    notes.Add("grab added");
                }
                EditorUtility.CopySerialized(landGrab, grab);
                var so = new SerializedObject(grab);
                so.FindProperty("m_AttachTransform").objectReferenceValue = attach;
                var colliders = so.FindProperty("m_Colliders");
                if (colliders != null) colliders.arraySize = 0;   // its own colliders, found at start
                so.ApplyModifiedPropertiesWithoutUndo();
                grab.enabled = false;   // on the wall it can't be taken; Wall Hung Prop switches it on when it drops

                var landPower = land.GetComponent<WeaponPower>();
                if (landPower != null)
                {
                    var power = root.GetComponent<WeaponPower>();
                    if (power == null) { power = root.AddComponent<WeaponPower>(); notes.Add("throw damage added"); }
                    EditorUtility.CopySerialized(landPower, power);
                }

                var hung = root.GetComponent<WallHungProp>();
                if (hung != null)
                {
                    var hs = new SerializedObject(hung);
                    hs.FindProperty("massWhenDropped").floatValue = DroppedMass;
                    hs.FindProperty("grabbableWhenDropped").boolValue = true;
                    hs.ApplyModifiedPropertiesWithoutUndo();
                }
                else notes.Add("WARNING: no Wall Hung Prop, so nothing switches the grab on");

                // The other candles' Lighter (goes out when picked up, shake to relight, lights boongs). Done here, in
                // the loaded prefab, so a prefab open in Prefab Mode elsewhere doesn't stop it.
                string lit = LighterSetupMenu.MakeLighter(root);
                if (lit != null) notes.Add("Lighter: " + lit);
                PrefabUtility.SaveAsPrefabAsset(root, HangPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            // The lair rooms' hanging candles burn green like the lair's green candle, on the wall and after they fall
            // (user: "the light now is green, but when they fall to the ground, they back to yellow. Fix them also"):
            // a green copy, swapped in by Set Up Living Room Lair / Set Up Playroom Lair.
            var made = new List<string>();
            LairStoryPropsMenu.GreenCandle(HangPath, GreenHangName, made);
            notes.Add(GreenHangName + " made");
            Debug.Log($"{Tag} Candle_Hang: {(notes.Count > 0 ? string.Join(", ", notes) : "grab settings refreshed")}; grab off on the wall, on when dropped; mass {DroppedMass} when dropped.");
        }

        [MenuItem("Tools/429 Game/Shop/Add Candle To The Shop")]
        public static void AddCandleToShop()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LandPath);
            if (prefab == null || AssetDatabase.LoadAssetAtPath<ScriptableObject>(ShopPath) == null)
            {
                Debug.LogError($"{Tag} {LandPath} or {ShopPath} is missing; nothing changed.");
                return;
            }
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPath);
            if (item == null)
            {
                if (!AssetDatabase.CopyAsset(TemplateItemPath, ItemPath))
                {
                    item = ScriptableObject.CreateInstance<ItemDefinition>();
                    AssetDatabase.CreateAsset(item, ItemPath);
                }
                item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPath);
            }
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = "Candle_Land";
            so.FindProperty("displayName").stringValue = ItemName;
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            bool needsIcon = so.FindProperty("icon").objectReferenceValue == null || item.Icon == null || !item.Icon.name.Contains("Candle");
            if (needsIcon) so.FindProperty("icon").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            string icon = "kept";
            if (needsIcon)
            {
                bool drawn = PrefabIconMaker.MakeNow(prefab, item);
                if (!drawn) PrefabIconMaker.Queue(prefab, item);
                icon = drawn ? "drawn" : "queued";
            }
            InventorySetupMenu.EnsureAssets();   // the item database lists every item

            var shop = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ShopPath);
            var shopSo = new SerializedObject(shop);
            var list = shopSo.FindProperty("entries");
            int at = -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item) at = i;
            if (at < 0) { at = list.arraySize; list.arraySize++; }
            var row = list.GetArrayElementAtIndex(at);
            row.FindPropertyRelative("item").objectReferenceValue = item;
            row.FindPropertyRelative("price").intValue = Price;
            row.FindPropertyRelative("stock").intValue = 0;
            shopSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Shop: '{ItemName}' (Candle_Land) {Price} candies, row {at + 1} of {list.arraySize}; icon {icon}.");
        }

        [MenuItem("Tools/429 Game/Items/Hanging Candles Grabbable When Dropped", true)]
        [MenuItem("Tools/429 Game/Shop/Add Candle To The Shop", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
