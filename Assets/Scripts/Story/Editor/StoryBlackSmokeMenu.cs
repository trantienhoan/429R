using UnityEditor;
using UnityEngine;

namespace Game.Story.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Run Away In A Black Smoke Bomb (re-runnable; "Escape: Back To The White Smoke Bomb" undoes
    /// it). User 2026-11-27: "check the minibosses and their escape method. Make them run away with a black smoke bomb."
    /// - Prefabs/Story/Smoke_Bomb_Black: a copy of Smoke_Flash_Five_Puffs (the white smoke bomb, kept) whose five puffs use
    ///   the dark smoke (Smoke Puff 3D Dark), near-black with a lighter rim, and whose flash is dim.
    /// - Story Boss Escape Settings: Escape = Run And Smoke (it rears up, turns and runs off away from the player for up to
    ///   Run Smoke Seconds, then the smoke bomb), Smoke Bomb = the black one. The real boss comes in the black one too.
    /// Settings asset only + the new prefab: no scene change.
    /// </summary>
    internal static class StoryBlackSmokeMenu
    {
        private const string EscapePath = "Assets/Resources/Story Boss Escape Settings.asset";
        private const string WhiteSmoke = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Five_Puffs.prefab";
        private const string BlackSmoke = "Assets/Prefabs/Story/Smoke_Bomb_Black.prefab";
        private const string DarkPuff = "Assets/Data/Effects/Smoke Puff 3D Dark.mat";
        // Its own copy of the dark smoke: the dark one's white rim made the puffs read grey-blue, not black.
        private const string BlackPuff = "Assets/Materials/Story/Smoke_Bomb_Black_Mat.mat";
        private static readonly Color BlackRim = new(0.22f, 0.2f, 0.26f, 1f);
        private static readonly Color PuffDark = new(0.07f, 0.065f, 0.08f, 1f);
        private static readonly Color PuffLight = new(0.17f, 0.15f, 0.19f, 1f);
        private static readonly Color DimFlash = new(0.35f, 0.3f, 0.42f, 0.5f);

        [MenuItem("Tools/429 Game/Story/Run Away In A Black Smoke Bomb")]
        private static void Black()
        {
            var escape = AssetDatabase.LoadAssetAtPath<StoryBossEscapeSettings>(EscapePath);
            if (escape == null)
            {
                EditorUtility.DisplayDialog("Black smoke bomb", "No " + EscapePath + " (run Set Up Boss Escape first).", "OK");
                return;
            }
            var bomb = MakeBlackBomb();
            escape.SetEscape(StoryBossEscapeSettings.EscapeStyle.RunAndSmoke, bomb);
            EditorUtility.SetDirty(escape);
            AssetDatabase.SaveAssets();
            Debug.Log("[Story] Mini bosses: Alert, run off, then a black smoke bomb (" + (bomb != null ? bomb.name : "NO bomb") +
                      "); the real boss comes in one too. Story Boss Escape Settings saved.");
        }

        [MenuItem("Tools/429 Game/Story/Escape: Back To The White Smoke Bomb")]
        private static void White()
        {
            var escape = AssetDatabase.LoadAssetAtPath<StoryBossEscapeSettings>(EscapePath);
            if (escape == null) return;
            escape.SetEscape(StoryBossEscapeSettings.EscapeStyle.SmokeBomb, AssetDatabase.LoadAssetAtPath<GameObject>(WhiteSmoke));
            EditorUtility.SetDirty(escape);
            AssetDatabase.SaveAssets();
            Debug.Log("[Story] Mini bosses: Alert, then the white smoke bomb where they stand (as before 2026-11-27).");
        }

        // A copy of the dark smoke material (made once; these numbers set on every run): a faint dark rim, a little dimmer.
        private static Material BlackMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BlackPuff);
            if (mat == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(DarkPuff);
                if (source == null) return null;
                System.IO.Directory.CreateDirectory("Assets/Materials/Story");
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, BlackPuff);
            }
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", BlackRim);
            if (mat.HasProperty("_RimStrength")) mat.SetFloat("_RimStrength", 0.08f);
            if (mat.HasProperty("_Opacity")) mat.SetFloat("_Opacity", 0.9f);
            if (mat.HasProperty("_Brightness")) mat.SetFloat("_Brightness", 0.7f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // The white smoke bomb's copy, made black (made once, recoloured on every run).
        private static GameObject MakeBlackBomb()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BlackSmoke) == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Prefabs/Story");
                if (!AssetDatabase.CopyAsset(WhiteSmoke, BlackSmoke)) return null;
            }
            var dark = BlackMaterial();
            var root = PrefabUtility.LoadPrefabContents(BlackSmoke);
            try
            {
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var look = ps.GetComponent<ParticleSystemRenderer>();
                    var main = ps.main;
                    bool puff = look != null && look.sharedMaterial != null && look.sharedMaterial.name.StartsWith("Smoke Puff 3D");
                    if (!puff)
                    {
                        main.startColor = DimFlash;
                        continue;
                    }
                    if (dark != null) look.sharedMaterial = dark;
                    main.startColor = new ParticleSystem.MinMaxGradient(PuffDark, PuffLight);
                    // Over its life it only fades (its colour stays the start's).
                    var overLife = ps.colorOverLifetime;
                    var gradient = overLife.color.gradient;
                    if (!overLife.enabled || gradient == null) continue;
                    var white = new Gradient();
                    white.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, gradient.alphaKeys);
                    overLife.color = new ParticleSystem.MinMaxGradient(white);
                }
                PrefabUtility.SaveAsPrefabAsset(root, BlackSmoke);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(BlackSmoke);
        }
    }
}
