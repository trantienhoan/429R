using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Story.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Pumpkin Tree: The Seed's Green (re-runnable; "Pumpkin Tree: Back To The Old Look" undoes
    /// it). User 2026-11-27: "check the texture of the Pumpkin Tree, any way to make it less scary and more holy?", then
    /// picked look B, the Seed's green (Exports/Story/Tree_Holy_Options_2026-11-27/compare.jpg).
    /// - Green COPIES of its textures (the originals are kept): the bark with the carved jack-o'-lantern face, claw slashes
    ///   and drips painted out with bark from nearby, lighter warm wood, a soft green glow along its grain (the Seed's
    ///   glow colour), the carved-face patches a plain green glow; the maple leaves fresh green. Made by
    ///   make_options.py in that folder; the menu copies its green/*.png into the texture copies when they're there.
    /// - Green material copies for the bark, the leaves (they glow a little, in their own colour), the falling leaves and
    ///   the stone pumpkin in the trunk (its own copy of the rooftop wall's material, tinted green, a faint glow).
    /// - On Prefabs/Items/Breakable/Pumpkin_Tree: those materials, its black smoke (Door_smoke_black) switched off and a
    ///   pale green mist in its place (Green_Mist), and a soft green light in the canopy (Seed_Green_Light, on the stone
    ///   pumpkin's bone, so it rises as the tree grows). Its broken pieces (Pumpkin_Tree_Drop) get the green bark too.
    /// - Not touched: the PlayGround's House_Pumpkin_Tree (it shares the old materials; the user's call).
    /// </summary>
    internal static class PumpkinTreeGreenMenu
    {
        private const string TreePath = "Assets/Prefabs/Items/Breakable/Pumpkin_Tree.prefab";
        private const string DropPath = "Assets/Prefabs/Items/Broken_Stuffs/Pumpkin_Tree_Drop.prefab";
        private const string Source = "Exports/Story/Tree_Holy_Options_2026-11-27/green/";

        private const string BarkMat = "Assets/Materials/Pumpkin_Tree_Mat.mat";
        private const string LeavesMat = "Assets/Materials/Pumpkin_Leaves_Mat.mat";
        private const string LeafParticleMat = "Assets/Materials/Pumpkin_Leaf_Particle_Mat.mat";
        private const string StoneMat = "Assets/Materials/Rooftop_Wall_Mat.mat";
        private const string BarkAlbedo = "Assets/M_Textures/Pumpkin_Tree/Pumpkin_Tree_Pumpkin_Tree_AlbedoTransparency.png";
        private const string BarkEmission = "Assets/M_Textures/Pumpkin_Tree/Pumpkin_Tree_Pumpkin_Tree_Emission.png";
        private const string LeavesTex = "Assets/Images/pumpkin_leaves.png";
        private const string LightSmoke = "Assets/Data/Effects/Smoke Puff 3D.mat";

        private const string GreenAlbedo = "Assets/M_Textures/Pumpkin_Tree/Pumpkin_Tree_Green_Albedo.png";
        private const string GreenEmission = "Assets/M_Textures/Pumpkin_Tree/Pumpkin_Tree_Green_Emission.png";
        private const string GreenLeaves = "Assets/Images/pumpkin_leaves_green.png";
        private const string GreenBarkMat = "Assets/Materials/Pumpkin_Tree_Green_Mat.mat";
        private const string GreenLeavesMat = "Assets/Materials/Pumpkin_Leaves_Green_Mat.mat";
        private const string GreenLeafParticleMat = "Assets/Materials/Pumpkin_Leaf_Particle_Green_Mat.mat";
        private const string GreenStoneMat = "Assets/Materials/Stone_Pumpkin_Green_Mat.mat";

        private const string MistName = "Green_Mist";
        private const string LightName = "Seed_Green_Light";
        private const string SmokeName = "Door_smoke_black";
        private const string StoneBone = "Stone_Pumpkin_Bone";

        // The Seed's glow (Story Seed Settings) and the look's numbers, as in the picked mock-up.
        private static readonly Color LeafGlow = new(0.3f, 0.55f, 0.22f);
        private static readonly Color StoneTint = new(0.85f, 1f, 0.8f);
        private static readonly Color StoneGlow = new(0.12f, 0.3f, 0.1f);
        private static readonly Color LightColour = new(0.6f, 1f, 0.5f);
        // A soft green glow through the canopy (2.5 barely showed; 60 washed the whole tree green).
        private const float LightIntensity = 15f;
        private const float LightRange = 10f;
        private const float LightAboveStone = 1f;
        private static readonly Color MistA = new(0.78f, 1f, 0.72f, 0.45f);
        private static readonly Color MistB = new(0.6f, 1f, 0.55f, 0.3f);

        [MenuItem("Tools/429 Game/Story/Pumpkin Tree: The Seed's Green")]
        private static void Green()
        {
            var albedo = Texture(BarkAlbedo, GreenAlbedo, "Pumpkin_Tree_Green_Albedo.png");
            var emission = Texture(BarkEmission, GreenEmission, "Pumpkin_Tree_Green_Emission.png");
            var leaves = Texture(LeavesTex, GreenLeaves, "pumpkin_leaves_green.png");
            if (albedo == null || emission == null || leaves == null)
            {
                EditorUtility.DisplayDialog("Pumpkin Tree", "The green textures are missing: run make_options.py in " + Source.TrimEnd('/') +
                                            " (it writes green/*.png), then this menu again.", "OK");
                return;
            }

            var bark = Mat(BarkMat, GreenBarkMat, m =>
            {
                SetMain(m, albedo);
                m.SetTexture("_EmissionMap", emission);
                m.EnableKeyword("_EMISSION");
            });
            var leafMat = Mat(LeavesMat, GreenLeavesMat, m =>
            {
                SetMain(m, leaves);
                m.SetTexture("_EmissionMap", leaves);
                m.SetColor("_EmissionColor", LeafGlow);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });
            var falling = Mat(LeafParticleMat, GreenLeafParticleMat, m => SetMain(m, leaves));
            var stone = Mat(StoneMat, GreenStoneMat, m =>
            {
                m.SetColor("_BaseColor", StoneTint);
                m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap"));
                m.SetColor("_EmissionColor", StoneGlow);
                m.EnableKeyword("_EMISSION");
            });
            AssetDatabase.SaveAssets();

            var lightAt = LightLocal();
            var root = PrefabUtility.LoadPrefabContents(TreePath);
            string done;
            try
            {
                foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.name == "Pumpkin_Tree") r.sharedMaterials = new[] { bark, leafMat };
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name == "Stone_Pumpkin") r.sharedMaterial = stone;
                var fallingLeaves = root.transform.Find("Leaves");
                if (fallingLeaves != null && fallingLeaves.TryGetComponent(out ParticleSystemRenderer leafLook)) leafLook.sharedMaterial = falling;

                // The black smoke at its roots: off, a pale green mist instead (a copy of it).
                var smoke = root.transform.Find(SmokeName);
                var mist = root.transform.Find(MistName);
                if (smoke != null)
                {
                    if (mist == null)
                    {
                        mist = Object.Instantiate(smoke.gameObject, smoke.parent).transform;
                        mist.name = MistName;
                        mist.SetSiblingIndex(smoke.GetSiblingIndex() + 1);
                    }
                    mist.gameObject.SetActive(true);
                    smoke.gameObject.SetActive(false);
                    var ps = mist.GetComponent<ParticleSystem>();
                    var main = ps.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(MistA, MistB);
                    var look = mist.GetComponent<ParticleSystemRenderer>();
                    var light = AssetDatabase.LoadAssetAtPath<Material>(LightSmoke);
                    if (look != null && light != null) look.sharedMaterial = light;
                }

                // A soft green light in the canopy, on the stone pumpkin's bone (it rises as the tree grows).
                Transform bone = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == StoneBone) bone = t;
                if (bone != null)
                {
                    var glow = bone.Find(LightName);
                    if (glow == null)
                    {
                        glow = new GameObject(LightName).transform;
                        glow.SetParent(bone, false);
                    }
                    glow.localPosition = lightAt;
                    glow.localRotation = Quaternion.identity;
                    if (!glow.TryGetComponent(out Light l)) l = glow.gameObject.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = LightColour;
                    l.intensity = LightIntensity;
                    l.range = LightRange;
                    l.shadows = LightShadows.None;
                }
                PrefabUtility.SaveAsPrefabAsset(root, TreePath);
                done = "Pumpkin_Tree: green bark + leaves, green stone, green falling leaves, " + (smoke != null ? "black smoke off + Green_Mist" : "NO smoke found") +
                       ", " + (bone != null ? "Seed_Green_Light on " + StoneBone : "NO " + StoneBone);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            int pieces = SwapIn(DropPath, BarkMat, bark);
            Debug.Log($"[Pumpkin Tree] The Seed's green: {done}; Pumpkin_Tree_Drop: {pieces} pieces green. Originals kept " +
                      "(Back To The Old Look undoes it). House_Pumpkin_Tree (PlayGround) not touched.");
        }

        [MenuItem("Tools/429 Game/Story/Pumpkin Tree: Back To The Old Look")]
        private static void Back()
        {
            var bark = AssetDatabase.LoadAssetAtPath<Material>(BarkMat);
            var leaves = AssetDatabase.LoadAssetAtPath<Material>(LeavesMat);
            var root = PrefabUtility.LoadPrefabContents(TreePath);
            try
            {
                foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.name == "Pumpkin_Tree") r.sharedMaterials = new[] { bark, leaves };
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name == "Stone_Pumpkin") r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(StoneMat);
                var fallingLeaves = root.transform.Find("Leaves");
                if (fallingLeaves != null && fallingLeaves.TryGetComponent(out ParticleSystemRenderer leafLook))
                    leafLook.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(LeafParticleMat);
                var smoke = root.transform.Find(SmokeName);
                if (smoke != null) smoke.gameObject.SetActive(true);
                var mist = root.transform.Find(MistName);
                if (mist != null) Object.DestroyImmediate(mist.gameObject);
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && t.name == LightName) Object.DestroyImmediate(t.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, TreePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            int pieces = SwapIn(DropPath, GreenBarkMat, bark);
            Debug.Log($"[Pumpkin Tree] Back to the old look (Pumpkin_Tree, Pumpkin_Tree_Drop {pieces} pieces). The green copies are kept.");
        }

        // A copy of a texture (its import settings too), its pixels from the made green PNG when that's there.
        private static Texture2D Texture(string original, string copy, string madeName)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(copy) == null && !AssetDatabase.CopyAsset(original, copy)) return null;
            string made = Source + madeName;
            if (File.Exists(made))
            {
                File.Copy(made, copy, true);
                AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceUpdate);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(copy);
        }

        // A copy of a material (made once, so later hand tweaks stay), then the look's settings on it.
        private static Material Mat(string original, string copy, System.Action<Material> set)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(copy);
            if (mat == null)
            {
                mat = new Material(AssetDatabase.LoadAssetAtPath<Material>(original));
                AssetDatabase.CreateAsset(mat, copy);
            }
            set(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetMain(Material m, Texture texture)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
        }

        // Every renderer in a prefab using one material gets another.
        private static int SwapIn(string prefabPath, string fromPath, Material to)
        {
            var from = AssetDatabase.LoadAssetAtPath<Material>(fromPath);
            if (from == null || to == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return 0;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            int count = 0;
            try
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] == from) { mats[i] = to; changed = true; }
                    if (!changed) continue;
                    r.sharedMaterials = mats;
                    count++;
                }
                if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return count;
        }

        // Where the light goes on the stone pumpkin's bone: this far over it once the tree has grown (its grow clip's end).
        private static Vector3 LightLocal()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePath);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                foreach (var a in go.GetComponentsInChildren<Animator>(true))
                {
                    if (a.runtimeAnimatorController == null) continue;
                    foreach (var clip in a.runtimeAnimatorController.animationClips)
                        if (clip.name == "Pumpkin_Tree_Grow") clip.SampleAnimation(a.gameObject, clip.length);
                }
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name == StoneBone) return t.InverseTransformPoint(t.position + Vector3.up * LightAboveStone);
                return Vector3.up * LightAboveStone;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
