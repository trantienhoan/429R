using System.Linq;
using Game.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Game.EditorTools.DangerFlameSetupMenu;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Effects > Purple Hole (user, 2026-11-19: the purple hole that sucks in everything, instead of
    /// the Fairy Shield; candy stays). Makes Assets/Prefabs/Effects/Purple_Hole.prefab, the hole's looks, from the wall
    /// portal's materials (a dark void, a purple haze, turning rings and runes, sparks and wisps spiralling in, a purple
    /// light), and puts a Purple Hole on the Fairy Shield (Shield_Sphere) in the open scene, with the user's whoosh and
    /// om nom nom. Run again to rebuild the looks (the component's numbers are kept). The scene is marked changed, not saved.
    /// </summary>
    public static class PurpleHoleSetupMenu
    {
        private const string PrefabPath = "Assets/Prefabs/Effects/Purple_Hole.prefab";
        private const string Materials = "Assets/Data/Effects/";
        private const string OpenSound = "Assets/Audio/SFX/whoosh.wav";
        private const string CloseSound = "Assets/Audio/SFX/om_nom_nom.wav";
        private const float Diameter = 3.6f;

        [MenuItem("Tools/429 Game/Effects/Purple Hole")]
        public static void Run()
        {
            var prefab = BuildPrefab();
            if (prefab == null) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var shield = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == "Shield_Sphere" && g.scene == scene && g.hideFlags == HideFlags.None);
            if (shield == null)
            {
                Debug.LogError($"[Purple Hole] Made {PrefabPath}, but there's no Shield_Sphere (the Fairy Shield) in {scene.name}.");
                return;
            }
            bool made = !shield.TryGetComponent(out PurpleHole hole);
            if (made) hole = Undo.AddComponent<PurpleHole>(shield);
            var so = new SerializedObject(hole);
            so.FindProperty("holePrefab").objectReferenceValue = prefab;
            if (so.FindProperty("openSound").objectReferenceValue == null)
                so.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(OpenSound);
            if (so.FindProperty("closeSound").objectReferenceValue == null)
                so.FindProperty("closeSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(CloseSound);
            // The old clean-up's smoke blasts fill the room and hide the show: off while the hole eats.
            var hide = so.FindProperty("hideWhileEating");
            if (hide.arraySize == 0)
            {
                var smokes = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Where(g => (g.name == "Smoke_Blast_Brown" || g.name == "Smoke_Blast_Green") && g.scene == scene && g.hideFlags == HideFlags.None)
                    .ToArray();
                hide.arraySize = smokes.Length;
                for (int i = 0; i < smokes.Length; i++) hide.GetArrayElementAtIndex(i).objectReferenceValue = smokes[i];
            }
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Purple Hole] {PrefabPath} made; Purple Hole {(made ? "added to" : "kept on")} the Fairy Shield " +
                      $"(Shield_Sphere). Every clean-up and the player's death now open the hole. Save the scene to keep it.");
        }

        [MenuItem("Tools/429 Game/Effects/Purple Hole", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static GameObject BuildPrefab()
        {
            var voidMat = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Portal Void.mat");
            var ringMat = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Portal Ring.mat");
            var runeMat = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Portal Runes.mat");
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Portal Glow.mat");
            var smokeMat = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Portal Smoke.mat");
            if (voidMat == null || ringMat == null || runeMat == null || glowMat == null || smokeMat == null)
            {
                Debug.LogError("[Purple Hole] The portal materials (Assets/Data/Effects/Portal *.mat) are missing: run " +
                               "Tools > 429 Game > Enemies > Wall Portals In The Living Room once, then this again.");
                return null;
            }

            var root = new GameObject("Purple_Hole");
            try
            {
                // Seen from below: the rings nearest (lowest), then the runes, the void, the haze furthest up.
                var haze = Disc(Child(root, "Haze"), glowMat, 1.75f, new Color(0.5f, 0.12f, 0.8f, 0.55f), 0f, 0.03f);
                haze.GetComponent<ParticleSystemRenderer>().sortingFudge = 40f;
                var hole = Disc(Child(root, "Void"), voidMat, 0.95f, new Color(0.03f, 0f, 0.06f, 0.97f), 0f, 0.02f);
                hole.GetComponent<ParticleSystemRenderer>().sortingFudge = 30f;
                Disc(Child(root, "Runes"), runeMat, 1.1f, new Color(0.75f, 0.35f, 1f, 0.8f), 0.5f, 0.01f);
                Disc(Child(root, "Ring Purple"), ringMat, 1.18f, new Color(0.7f, 0.3f, 1f, 0.9f), -1f, 0.005f);
                Disc(Child(root, "Ring Pink"), ringMat, 0.75f, new Color(1f, 0.45f, 0.95f, 0.85f), 1.6f, 0f);

                // Sparks drawn round and in from the edge.
                var pull = NewSystem(Child(root, "Pull"), glowMat, ParticleSystemRenderMode.Billboard, 90);
                pull.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                var pullMain = pull.main;
                pullMain.startLifetime = 1.2f;
                pullMain.startSize = new MinMaxCurve(0.06f, 0.14f);
                var pullRate = pull.emission;
                pullRate.rateOverTime = 45f;
                var pullShape = pull.shape;
                pullShape.enabled = true;
                pullShape.shapeType = ParticleSystemShapeType.Circle;
                pullShape.radius = Diameter * 0.7f;
                pullShape.radiusThickness = 0.2f;
                pullShape.rotation = new Vector3(90f, 0f, 0f);
                var inward = pull.velocityOverLifetime;
                inward.enabled = true;
                inward.space = ParticleSystemSimulationSpace.Local;
                inward.x = new MinMaxCurve(0f);
                inward.y = new MinMaxCurve(0f);
                inward.z = new MinMaxCurve(0f);
                inward.radial = new MinMaxCurve(-1.6f);
                inward.orbitalY = new MinMaxCurve(2.2f);
                Fade(pull, new[] { Key(1f, 0.85f, 1f, 0f), Key(0.75f, 0.4f, 1f, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });

                // Purple wisps rising from below and swirling up into it.
                var wisps = NewSystem(Child(root, "Wisps"), smokeMat, ParticleSystemRenderMode.Billboard, 40);
                var wispMain = wisps.main;
                wispMain.startLifetime = new MinMaxCurve(1.8f, 2.4f);
                wispMain.startSize = new MinMaxCurve(0.6f, 1.1f);
                wispMain.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                var wispRate = wisps.emission;
                wispRate.rateOverTime = 12f;
                var wispShape = wisps.shape;
                wispShape.enabled = true;
                wispShape.shapeType = ParticleSystemShapeType.Circle;
                wispShape.radius = Diameter * 1.1f;
                wispShape.rotation = new Vector3(90f, 0f, 0f);
                wispShape.position = new Vector3(0f, -2.2f, 0f);
                var rise = wisps.velocityOverLifetime;
                rise.enabled = true;
                rise.space = ParticleSystemSimulationSpace.Local;
                // x, y and z in the same mode (two constants), or Unity complains.
                rise.x = new MinMaxCurve(0f, 0f);
                rise.y = new MinMaxCurve(0.9f, 1.3f);
                rise.z = new MinMaxCurve(0f, 0f);
                rise.radial = new MinMaxCurve(-1.4f);
                rise.orbitalY = new MinMaxCurve(1.2f);
                Shrink(wisps, 1f, 0.9f, 0.3f);
                var sheet = wisps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = 2;
                sheet.numTilesY = 2;
                sheet.startFrame = new MinMaxCurve(0f, 3.99f);
                sheet.frameOverTime = new MinMaxCurve(0f);
                Fade(wisps, new[] { Key(0.75f, 0.4f, 1f, 0f), Key(0.35f, 0.1f, 0.6f, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.3f), new GradientAlphaKey(0f, 1f) });

                // A purple glow over the room while it eats.
                var lightObject = Child(root, "Light");
                lightObject.transform.localPosition = new Vector3(0f, -0.6f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.62f, 0.3f, 1f);
                light.range = 11f;
                light.intensity = 3f;
                light.shadows = LightShadows.None;

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // One particle that stays, flat (Share of the diameter), turning Spin rad/s, Lift above the hole's plane.
        private static ParticleSystem Disc(GameObject go, Material material, float share, Color colour, float spin, float lift)
        {
            go.transform.localPosition = new Vector3(0f, lift, 0f);
            var ps = NewSystem(go, material, ParticleSystemRenderMode.HorizontalBillboard, 1);
            go.GetComponent<ParticleSystemRenderer>().maxParticleSize = 50f;
            var main = ps.main;
            main.prewarm = false;
            main.startLifetime = 100000f;
            main.startSize = Diameter * share;
            main.startColor = colour;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, 1) });
            var shape = ps.shape;
            shape.enabled = false;
            if (spin != 0f)
            {
                var turn = ps.rotationOverLifetime;
                turn.enabled = true;
                turn.z = spin;
            }
            return ps;
        }
    }
}
