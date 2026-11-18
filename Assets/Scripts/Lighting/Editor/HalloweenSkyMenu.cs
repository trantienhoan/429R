using Game.Lighting;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Look > Sky: Halloween (user, 2026-11-16: "use this sky for main game"): a painted 360 sky becomes
    /// the scene's skybox, and the player's head camera clears to it instead of the flat fog colour. It was the gate 3
    /// valley's Halloween dusk (BallValley_Sky.png); since 2026-11-18 it's a midnight sky (Midnight_Sky.png: dark navy, a
    /// silver moon in the same spot, moonlit clouds, stars).
    /// A skybox is drawn at infinity after everything else, so the head's 40 m far plane and the house fog (6-22 m) don't
    /// cut it, and it only costs the pixels nothing else covers. Nothing else changes: the ambient light stays Flat and the
    /// reflections stay Custom (none), so shiny things don't pick it up. Before, the old Skydome showed nothing (black
    /// material, 75 m away past the 40 m far plane), so the "sky" was the fog colour.
    /// It also puts "Sky Flyers" in the scene: flying slimes looping round the sky's moon (SkyFlyers; user, 2026-11-16:
    /// real flying bats instead of painted ones, "reuse the slime"); running it again keeps their tuning.
    /// The open scene is marked changed, not saved: save it to keep it. "Sky: Fog Colour" puts the old background back
    /// and takes the flyers out.
    /// </summary>
    public static class HalloweenSkyMenu
    {
        private const string Log = "[Sky]";
        // The main game's sky picture: since 2026-11-18 a midnight one (user: "change the sky in game to mid night sky, dark
        // blue color"; Documents/429R_claude_backups/2026-11-18/midnight_sky/midnight_sky.py, the moon where the valley's
        // is, so the Sky Flyers still loop round it). The gate 3 valley keeps BallValley_Sky.png (its own material).
        private const string PicturePath = "Assets/BallValley/Sky/Midnight_Sky.png";
        private const string MaterialPath = "Assets/BallValley/Sky/Halloween_Skybox_Mat.mat";
        private const string PanoramicShader = "Skybox/Panoramic";
        private const string FlyersName = "Sky Flyers";
        private const string SlimeModelPath = "Assets/Models/Enemies/Flying_Slime.fbx";
        private const string SlimeMaterialPath = "Assets/Materials/Flying_Slime/Flying_Slime_Mat.mat";

        // The picture's left edge looks along +Z (BallValleySky's layout); Skybox/Panoramic's looks along -X. Turned this
        // much, the moon is where it is in the valley: toward -Z, a little -X.
        private const float Rotation = 270f;

        [MenuItem("Tools/429 Game/Look/Sky: Halloween")]
        public static void UseHalloween()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var picture = AssetDatabase.LoadAssetAtPath<Texture2D>(PicturePath);
            var shader = Shader.Find(PanoramicShader);
            if (picture == null || shader == null)
            {
                Debug.LogError($"{Log} Need {PicturePath} and the {PanoramicShader} shader; nothing changed.");
                return;
            }
            // Mipmaps off: no seam line where the picture wraps round (Balls' Set Up Ball Valley sets the same).
            if (AssetImporter.GetAtPath(PicturePath) is TextureImporter importer && importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool made = mat == null;
            if (made)
            {
                mat = new Material(shader) { name = "Halloween_Skybox_Mat" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.shader = shader;
            mat.SetTexture("_MainTex", picture);
            mat.SetFloat("_Mapping", 1f);       // Latitude Longitude Layout
            mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            mat.SetFloat("_ImageType", 0f);     // 360 degrees
            mat.SetFloat("_MirrorOnBack", 0f);
            mat.SetFloat("_Layout", 0f);        // not 3D
            mat.SetFloat("_Rotation", Rotation);
            mat.SetFloat("_Exposure", 1f);
            mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));   // grey 0.5 = the picture as painted
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            RenderSettings.skybox = mat;
            int cameras = SetHeadClear(CameraClearFlags.Skybox);
            string flyers = PutFlyers();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"{Log} Sky: Halloween ({(made ? "made " : "")}{MaterialPath}, rotation {Rotation}); the head camera" +
                      $"{(cameras == 1 ? "" : "s")} ({cameras}) now clear to the skybox; {flyers}. Save the scene to keep it.", mat);
        }

        [MenuItem("Tools/429 Game/Look/Sky: Halloween", true)]
        private static bool CanUse() => !EditorApplication.isPlaying;

        /// <summary>The old background: no skybox, the head camera cleared to its flat colour (the fog's, 0.05 / 0.06 / 0.07).</summary>
        [MenuItem("Tools/429 Game/Look/Sky: Fog Colour")]
        public static void UseFogColour()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            RenderSettings.skybox = null;
            int cameras = SetHeadClear(CameraClearFlags.SolidColor);
            var old = FindFlyers();
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"{Log} Sky: Fog Colour (no skybox; {cameras} head camera(s) clear to their flat colour). Save the scene to keep it.");
        }

        [MenuItem("Tools/429 Game/Look/Sky: Fog Colour", true)]
        private static bool CanUseFog() => !EditorApplication.isPlaying;

        private static SkyFlyers FindFlyers()
        {
            foreach (var f in Object.FindObjectsByType<SkyFlyers>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.gameObject.scene.IsValid()) return f;
            return null;
        }

        // The scene's Sky Flyers: made with the slime model and material if missing; an existing one keeps its tuning
        // (only a missing model or material is filled in).
        private static string PutFlyers()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SlimeModelPath);
            var slimeMat = AssetDatabase.LoadAssetAtPath<Material>(SlimeMaterialPath);
            if (model == null) return $"NO flyers ({SlimeModelPath} missing)";
            var flyers = FindFlyers();
            bool made = flyers == null;
            if (made)
            {
                var go = new GameObject(FlyersName);
                Undo.RegisterCreatedObjectUndo(go, "Sky Flyers");
                flyers = go.AddComponent<SkyFlyers>();
            }
            var so = new SerializedObject(flyers);
            var m = so.FindProperty("model");
            var mat = so.FindProperty("material");
            if (m.objectReferenceValue == null) m.objectReferenceValue = model;
            if (mat.objectReferenceValue == null) mat.objectReferenceValue = slimeMat;
            so.ApplyModifiedProperties();
            return made ? "Sky Flyers made" : "Sky Flyers kept";
        }

        // The XR Origin's head camera(s) in the open scene: how they clear the screen. How many were set.
        private static int SetHeadClear(CameraClearFlags flags)
        {
            int n = 0;
            foreach (var origin in Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var cam = origin.Camera;
                if (cam == null || !cam.gameObject.scene.IsValid()) continue;
                Undo.RecordObject(cam, "Head camera background");
                cam.clearFlags = flags;
                PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
                n++;
            }
            if (n == 0) Debug.LogWarning($"{Log} No XR Origin head camera in the open scene (open 2 Game Scene).");
            return n;
        }
    }
}
