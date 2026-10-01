using System.Collections.Generic;
using System.IO;
using Game.Inventory;
using Game.Saving;
using Game.Shopping;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Menu commands for the save system (Tools > 429 Game > Save).</summary>
    public static class SaveSetupMenu
    {
        [MenuItem("Tools/429 Game/Save/Set Up Save Manager")]
        public static void SetUpSaveManager()
        {
            var notes = new List<string>();

            var manager = Object.FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                var go = new GameObject("Save Manager");
                Undo.RegisterCreatedObjectUndo(go, "Create Save Manager");
                manager = go.AddComponent<SaveManager>();
                notes.Add("created 'Save Manager' in the scene");
            }

            var inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (inventory != null) SetupUtility.SetReferenceIfEmpty(manager, "inventory", inventory);
            else notes.Add("WARNING: no PlayerInventory in the scene, run Tools > 429 Game > Inventory > Set Up In Open Scene");

            var shop = Object.FindAnyObjectByType<Shop>(FindObjectsInactive.Include);
            if (shop != null) SetupUtility.SetReferenceIfEmpty(manager, "shop", shop);

            // Saved item ids are turned back into items through the item database, so it must list every item.
            InventorySetupMenu.EnsureAssets();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = manager.gameObject;
            Debug.Log("[Save Setup] Done: " + (notes.Count > 0 ? string.Join("; ", notes) : "everything was already set up") +
                      $". Saves go to '{SaveManager.SaveFolder}'. Save the scene to keep it.");
        }

        [MenuItem("Tools/429 Game/Save/Open Save Folder")]
        public static void OpenSaveFolder()
        {
            Directory.CreateDirectory(SaveManager.SaveFolder);
            EditorUtility.RevealInFinder(File.Exists(SaveManager.SavePath) ? SaveManager.SavePath : SaveManager.SaveFolder);
        }

        // Deletes the Local save and every Steam ID's: deleting just one, the Local save would be copied back in.
        // Steam Cloud keeps its own copy, which the Steam version downloads again unless the save is deleted while the
        // game is running from Steam.
        [MenuItem("Tools/429 Game/Save/Delete Save")]
        public static void DeleteSave()
        {
            if (!EditorUtility.DisplayDialog("Delete Save",
                    $"Delete every save in\n{SaveManager.SavesRoot}\n(Local and Steam)?\n\nThe next play starts with no candies. " +
                    "Steam Cloud keeps its copy: to clear that too, delete the save while the game is running from Steam.",
                    "Delete", "Cancel"))
                return;

            int deleted = SaveManager.DeleteAllSaveFiles();
            Debug.Log(deleted > 0 ? $"[Save Setup] Deleted {deleted} save file(s) in '{SaveManager.SavesRoot}'." : "[Save Setup] There was no save to delete.");
        }

        // While playing, the running game would write the save straight back.
        [MenuItem("Tools/429 Game/Save/Delete Save", true)]
        private static bool CanDeleteSave()
        {
            return !EditorApplication.isPlaying;
        }
    }
}
