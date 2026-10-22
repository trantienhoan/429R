using System.Collections.Generic;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Make Rooms Spooky: the Bedroom and Living Room get
    /// - spooky copies of their materials (Assets/Materials/*_Spooky.mat), so the originals and anything else using them
    ///   stay as they were:
    ///   - Bedroom: dark plum damask wallpaper (stains, peeling, mould at the floor) and Halloween bunting (orange, black
    ///     and purple flags);
    ///   - Living Room: a Victorian parlour wallpaper (green stripes over dark wood panelling, its own normal map; the
    ///     walls were re-unwrapped for it in Bedroom_Walls.blend), and lightly aged bricks on its breakable brick walls;
    /// - a failing ceiling lamp: Lamp Flicker on the room's Point Light, tinted a sickly yellow.
    /// The textures are painted by the scripts in K:/WORK_2026/08_04/_claude/spooky_rooms. Running it again updates the
    /// copies and changes nothing else. Undo: Make Rooms Normal Again.
    /// </summary>
    public static class SpookyRoomsSetupMenu
    {
        private const string BedroomPath = "Assets/Prefabs/Bases/Bedroom_Base_Z.prefab";
        private const string LivingRoomPath = "Assets/Prefabs/Bases/Livingroom_Base_Z.prefab";
        private const string Materials = "Assets/Materials/";
        private const string Walls = "Assets/M_Textures/Combos/Wall_Stylize_Set_1/";
        private const string BedroomWallpaper = Walls + "Bedroom_Wallpaper_Spooky.png";
        private const string LivingRoomWallpaper = Walls + "Livingroom_Wallpaper_Spooky.png";
        private const string LivingRoomWallpaperNormal = Walls + "Livingroom_Wallpaper_Spooky_Normal.png";
        private const string BrickTexture = "Assets/M_Textures/Combos/Wall_Brick_Set_1/Livingroom_Brick_Spooky.png";
        private const string BuntingTexture = "Assets/M_Textures/BedRoom_Set_1/NEW/Bedroom_Bunting_Halloween.png";
        // The Playroom's light (the Pumpkin_7 stage switches the room's own lamp off and spawns this one).
        private const string PlayroomLightPath = "Assets/Prefabs/Light/Point Light_LR_Sub.prefab";
        private const string JengaWoodTexture = "Assets/M_Textures/Combos/Wood_Floor_Stylize_Set_2/Jenga_Wood_Spooky.png";
        // The "?" boxes (weapon boxes and the plain wood boxes, the same crate) and their broken pieces get the same dark
        // mahogany as the Jenga blocks.
        private static readonly string[] WeaponBoxPrefabs =
        {
            "Assets/Prefabs/BedRoom/Wood_Box_Weapon_Main.prefab",
            "Assets/Prefabs/Items/Broken_Stuffs/Wood_Box_Weapon_Drop.prefab",
            "Assets/Prefabs/BedRoom/Wood_Box.prefab",
            "Assets/Prefabs/Items/Broken_Stuffs/Wood_Box_Drop.prefab",
        };
        private const string JengaBrassTexture = "Assets/M_Textures/Combos/Metal_Stylize_Set_2/Jenga_Brass_Spooky.png";
        // The Playroom's metal Jenga blocks: antique brass with verdigris instead of black iron (the doors and room trim
        // keep the iron).
        private static readonly string[] JengaMetalPrefabs =
        {
            "Assets/Prefabs/Items/Metal_Block/Jenga_Block.000.prefab",
            "Assets/Prefabs/Items/Metal_Block/Jenga_Block.001.prefab",
            "Assets/Prefabs/Items/Metal_Block/Jenga_Block.002.prefab",
            "Assets/Prefabs/Items/Metal_Block/Block_Gate_Pack.prefab",
            "Assets/Prefabs/Items/Metal_Block/Block_Gate_Pack_2.prefab",
            "Assets/Prefabs/BedRoom/Jenga_Block.prefab",
            "Assets/Prefabs/BedRoom/Jenga_Block Chaos.prefab",
        };
        // The Playroom's Jenga blocks (and their broken pieces): dark polished mahogany instead of the orange crate wood.
        // Inner prefabs first, so the packs pick them up.
        private static readonly string[] JengaPrefabs =
        {
            "Assets/Prefabs/Items/Metal_Block/Jenga_Block.003.prefab",
            "Assets/Prefabs/Items/Metal_Block/Jenga_Block.004.prefab",
            "Assets/Prefabs/Items/Metal_Block/Jenga_Block.005.prefab",
            "Assets/Prefabs/Items/Metal_Block/Block_Gate_Pack.prefab",
            "Assets/Prefabs/Items/Metal_Block/Block_Gate_Pack_2.prefab",
            "Assets/Prefabs/BedRoom/Jenga_Block.prefab",
            "Assets/Prefabs/BedRoom/Jenga_Block Chaos.prefab",
            "Assets/Prefabs/Items/Broken_Stuffs/Jenga_Block_Drop.prefab",
        };
        private static readonly Color LampColour = new(1f, 0.93f, 0.68f);

        [MenuItem("Tools/429 Game/Rooms/Make Rooms Spooky")]
        public static void Run()
        {
            Import(BedroomWallpaper, 2048, false);
            Import(LivingRoomWallpaper, 2048, false);
            Import(LivingRoomWallpaperNormal, 2048, true);
            Import(BrickTexture, 1024, false);
            Import(BuntingTexture, 512, false);
            var bedroomWall = Copy("Bedroom_Wall_Mat 1", "Bedroom_Wall_Spooky", BedroomWallpaper, null, 0.6f);
            var livingWall = Copy("Bedroom_Wall_Mat 1", "Livingroom_Wall_Spooky", LivingRoomWallpaper, LivingRoomWallpaperNormal, 1f);
            var brick = Copy("Brick_White_Mat", "Brick_Spooky_Clean", BrickTexture, null, -1f);
            var bunting = Copy("Bedroom_Set_1_Mat 1", "Bedroom_Bunting_Halloween", BuntingTexture, null, -1f);
            Import(JengaWoodTexture, 1024, false);
            var jengaWood = Copy("Wood_Floor_Mat", "Jenga_Wood_Spooky", JengaWoodTexture, null, -1f);
            jengaWood.SetFloat("_Smoothness", 0.42f);   // a little lacquer shine, like old furniture
            Import(JengaBrassTexture, 1024, false);
            var jengaBrass = Copy("Bedroom_Metal_Mat", "Jenga_Brass_Spooky", JengaBrassTexture, null, -1f);
            jengaBrass.SetFloat("_Metallic", 0.35f);      // a dull metal sheen, not a mirror
            jengaBrass.SetFloat("_Smoothness", 0.38f);

            var notes = new List<string>
            {
                Swap(BedroomPath, (renderer, name) =>
                    name == "Bedroom_Wall_Mat 1" ? bedroomWall : name == "Bedroom_Set_1_Mat 1" ? bunting : null, true),
                // The room's walls take the wallpaper; its separate brick walls (Brick_Wall_Breakable, the roof piece)
                // stay brick. Brick_Spooky: the first, too-dirty try.
                Swap(LivingRoomPath, (renderer, name) =>
                    name != "Brick_White_Mat 1" && name != "Brick_White_Mat" && name != "Brick_Spooky" ? null
                    : renderer is SkinnedMeshRenderer ? livingWall : brick, true),
            };
            int jengas = 0;
            foreach (var path in JengaPrefabs) jengas += SwapIn(path, "Wood_Floor_Mat", jengaWood);
            notes.Add("Jenga blocks: " + jengas + " wood slots");
            int boxes = 0;
            foreach (var path in WeaponBoxPrefabs) boxes += SwapIn(path, "Wood_Floor_Mat", jengaWood);
            notes.Add("weapon boxes: " + boxes + " wood slots");
            int brass = 0;
            foreach (var path in JengaMetalPrefabs) brass += SwapIn(path, "Bedroom_Metal_Mat", jengaBrass);
            notes.Add("metal Jenga blocks: " + brass + " brass slots");
            notes.Add(Lamp(PlayroomLightPath, true));
            AssetDatabase.SaveAssets();
            Debug.Log("[Spooky Rooms] " + string.Join("; ", notes));
        }

        [MenuItem("Tools/429 Game/Rooms/Make Rooms Normal Again")]
        public static void Undo()
        {
            var notes = new List<string>
            {
                Swap(BedroomPath, (renderer, name) =>
                    name == "Bedroom_Wall_Spooky" ? Load("Bedroom_Wall_Mat 1") : name == "Bedroom_Bunting_Halloween" ? Load("Bedroom_Set_1_Mat 1") : null, false),
                // (The Living Room's walls keep their new UVs, made for wallpaper: the old brick tiles differently on them.)
                Swap(LivingRoomPath, (renderer, name) =>
                    name != "Livingroom_Wall_Spooky" && name != "Brick_Spooky_Clean" && name != "Brick_Spooky" ? null
                    : Load(renderer is SkinnedMeshRenderer || renderer.name.Contains("Roof") ? "Brick_White_Mat 1" : "Brick_White_Mat"), false),
            };
            int jengas = 0;
            foreach (var path in JengaPrefabs) jengas += SwapIn(path, "Jenga_Wood_Spooky", Load("Wood_Floor_Mat"));
            notes.Add("Jenga blocks: " + jengas + " wood slots");
            int boxes = 0;
            foreach (var path in WeaponBoxPrefabs) boxes += SwapIn(path, "Jenga_Wood_Spooky", Load("Wood_Floor_Mat"));
            notes.Add("weapon boxes: " + boxes + " wood slots");
            int brass = 0;
            foreach (var path in JengaMetalPrefabs) brass += SwapIn(path, "Jenga_Brass_Spooky", Load("Bedroom_Metal_Mat"));
            notes.Add("metal Jenga blocks: " + brass + " iron slots");
            notes.Add(Lamp(PlayroomLightPath, false));
            AssetDatabase.SaveAssets();
            Debug.Log("[Spooky Rooms] back to normal: " + string.Join("; ", notes));
        }

        private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");

        private static void Import(string path, int maxSize, bool normalMap)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        // A copy of a material with a new base texture (and its emission map too, if it was the old base texture), and
        // optionally a new normal map and bump strength (below 0: unchanged).
        private static Material Copy(string from, string to, string texturePath, string normalPath, float bumpScale)
        {
            var source = Load(from);
            string path = Materials + to + ".mat";
            var copy = Load(to);
            if (copy == null)
            {
                AssetDatabase.CopyAsset(Materials + from + ".mat", path);
                copy = Load(to);
            }
            else
            {
                copy.CopyPropertiesFromMaterial(source);
            }
            var oldBase = source.GetTexture("_BaseMap");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            copy.SetTexture("_BaseMap", texture);
            copy.SetTexture("_MainTex", texture);
            copy.SetTextureScale("_BaseMap", Vector2.one);
            copy.SetTextureOffset("_BaseMap", Vector2.zero);
            copy.SetColor("_BaseColor", Color.white);
            copy.SetColor("_Color", Color.white);
            if (copy.HasProperty("_EmissionMap") && copy.GetTexture("_EmissionMap") == oldBase) copy.SetTexture("_EmissionMap", texture);
            if (normalPath != null)
            {
                copy.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                copy.EnableKeyword("_NORMALMAP");
            }
            if (bumpScale >= 0f && copy.HasProperty("_BumpScale")) copy.SetFloat("_BumpScale", bumpScale);
            EditorUtility.SetDirty(copy);
            return copy;
        }

        // A light prefab gets (or, undoing, loses) the lamp flicker and the sickly tint.
        private static string Lamp(string prefabPath, bool spooky)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            string name = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            if (stage != null && stage.assetPath == prefabPath) return name + " is open in Prefab Mode: skipped";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    var flicker = light.GetComponent<LampFlicker>();
                    if (spooky && flicker == null) light.gameObject.AddComponent<LampFlicker>();
                    if (!spooky && flicker != null) Object.DestroyImmediate(flicker);
                    light.color = spooky ? LampColour : Color.white;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return name + (spooky ? ": lamp flickers" : ": lamp steady");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // One material for another on every renderer of a prefab (not one open in Prefab Mode). Returns the slots changed.
        private static int SwapIn(string prefabPath, string fromName, Material to)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (to == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return 0;
            if (stage != null && stage.assetPath == prefabPath) return 0;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            int swapped = 0;
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null || materials[i].name != fromName) continue;
                        materials[i] = to;
                        changed = true;
                        swapped++;
                    }
                    if (changed) renderer.sharedMaterials = materials;
                }
                if (swapped > 0) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return swapped;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Swaps materials on every renderer of the room (pick: renderer and material name -> new material, or null to
        // keep), and adds (or, undoing, removes) the lamp flicker.
        private static string Swap(string prefabPath, System.Func<Renderer, string, Material> pick, bool spooky)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            string name = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            if (stage != null && stage.assetPath == prefabPath) return name + " is open in Prefab Mode: skipped";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            int swapped = 0;
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null) continue;
                        var to = pick(renderer, materials[i].name);
                        if (to == null || to == materials[i]) continue;
                        materials[i] = to;
                        changed = true;
                        swapped++;
                    }
                    if (changed) renderer.sharedMaterials = materials;
                }
                string lampNote = "";
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type != LightType.Point) continue;
                    var flicker = light.GetComponent<LampFlicker>();
                    if (spooky)
                    {
                        if (flicker == null) light.gameObject.AddComponent<LampFlicker>();
                        light.color = LampColour;
                        lampNote = ", lamp flickers";
                    }
                    else
                    {
                        if (flicker != null) Object.DestroyImmediate(flicker);
                        light.color = Color.white;
                        lampNote = ", lamp steady";
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return name + ": " + swapped + " material slots" + lampNote;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
