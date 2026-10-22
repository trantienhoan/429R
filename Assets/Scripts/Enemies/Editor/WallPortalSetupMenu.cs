using System.Collections.Generic;
using Game.Enemies;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static Game.EditorTools.DangerFlameSetupMenu;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Wall Portals In The Living Room: the Living Room's breakable brick walls
    /// (Brick_Wall_Breakable) take weapon damage like the secret door (150 health, Enemy Hit Damage copied from the door),
    /// and each one hides a Wall_Portal (Assets/Prefabs/Enemies/Wall_Portal.prefab) under its drop point: when the wall
    /// breaks, a summoning circle opens on the dark ground behind it (a dark hole ringed with turning runes and rings,
    /// sparks drawn in, wisps rising; stepping on it, the player falls). Each gate rolls its portal: one in four a loot
    /// portal (gold, gives the Legendary Sword and Shield, loses its colour once they are picked up), otherwise an enemy
    /// portal of a random kind (Spider purple, Cockroach red, Bee orange, Mixed green) letting its enemies in at the
    /// wall's line, faster and faster. The Fairy Shield clears it like most things. See Wall Portal.
    /// Running it again rebuilds the portal prefab and leaves the walls as they are.
    /// </summary>
    public static class WallPortalSetupMenu
    {
        private const string BrickWallPath = "Assets/Prefabs/BedRoom/Brick_Wall_Breakable.prefab";
        private const string LivingRoomPath = "Assets/Prefabs/Bases/Livingroom_Base_Z.prefab";
        private const string PortalPath = "Assets/Prefabs/Enemies/Wall_Portal.prefab";
        private const string Enemies = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        // Each gate rolls one of these (or, one in four, a loot portal: gold, the Legendary Sword and Shield).
        private static readonly (string name, Color colour, string[] enemies)[] Kinds =
        {
            ("Spider", new Color(0.75f, 0.35f, 1f), new[] { "Spider_Normal", "Spider_Small" }),
            ("Cockroach", new Color(1f, 0.22f, 0.12f), new[] { "Cockroach_Normal", "Cockroach_Small" }),
            ("Bee", new Color(1f, 0.55f, 0.1f), new[] { "Bee_Normal", "Bee_Small" }),
            ("Mixed", new Color(0.4f, 1f, 0.5f), new[] { "Spider_Normal", "Spider_Small", "Cockroach_Normal", "Cockroach_Small", "Bee_Normal", "Bee_Small" }),
        };
        private static readonly string[] LegendaryPaths = { "Assets/Prefabs/Items/Pie_Knight_Sword_Legendary.prefab", "Assets/Prefabs/Items/Pie_Knight_Shield_Legendary.prefab" };
        private const string SpawnSound = "Assets/Audio/SFX/whoosh.wav";
        private const string GlowTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string SmokeTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Dust01.png";
        private const string RingTexture = "Assets/Data/Effects/Textures/Seal_Ring.png";
        private const string SealTexture = "Assets/Data/Effects/Textures/Seal_Circle.png";
        private const float WallHealth = 150f;
        // The brick wall sits at the end of a passage 1.6 m wide (room units; x1.5 in the Playroom): the circle on the
        // floor in front of it fills the passage.
        private const float Diameter = 1.5f;

        [MenuItem("Tools/429 Game/Enemies/Wall Portals In The Living Room")]
        public static void Run()
        {
            var portal = BuildPortal();
            var notes = new List<string> { "built " + PortalPath, SetUpWall(portal) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Wall Portal] " + string.Join("; ", notes));
        }

        private static Material Portal(string name, string texture, bool additive)
        {
            var material = MakeMaterial(name, texture, additive);
            material.SetFloat("_Cull", (float)CullMode.Off);   // seen from both sides
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildPortal()
        {
            var voidMat = Portal("Portal Void", GlowTexture, false);
            var ringMat = Portal("Portal Ring", RingTexture, true);
            var runeMat = Portal("Portal Runes", SealTexture, true);
            var glowMat = Portal("Portal Glow", GlowTexture, true);
            var smokeMat = Portal("Portal Smoke", SmokeTexture, true);

            var root = new GameObject("Wall_Portal");
            try
            {
                // The hole in the floor, a purple haze round it, the turning rings and runes: flat on the floor.
                var hole = Floor(Child(root, "Void"), voidMat, 0.95f, new Color(0.03f, 0f, 0.06f, 0.97f), 0f, 0.01f);
                hole.GetComponent<ParticleSystemRenderer>().sortingFudge = 30f;
                var haze = Floor(Child(root, "Haze"), glowMat, 1.6f, new Color(0.45f, 0.12f, 0.7f, 0.5f), 0f, 0.005f);
                haze.GetComponent<ParticleSystemRenderer>().sortingFudge = 40f;
                Floor(Child(root, "Runes"), runeMat, 1.05f, new Color(0.7f, 0.32f, 1f, 0.75f), 0.4f, 0.02f);
                Floor(Child(root, "Ring Green"), ringMat, 1.12f, new Color(0.45f, 1f, 0.5f, 0.85f), -0.9f, 0.025f);
                Floor(Child(root, "Ring Purple"), ringMat, 0.7f, new Color(0.75f, 0.35f, 1f, 0.8f), 1.3f, 0.03f);

                // Sparks drawn in from the edge to the middle, sinking into the hole.
                var pull = NewSystem(Child(root, "Pull"), glowMat, ParticleSystemRenderMode.Billboard, 60);
                pull.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                var pullMain = pull.main;
                pullMain.startLifetime = 1.1f;
                pullMain.startSize = new MinMaxCurve(0.03f, 0.07f);
                var pullRate = pull.emission;
                pullRate.rateOverTime = 26f;
                var pullShape = pull.shape;
                pullShape.enabled = true;
                pullShape.shapeType = ParticleSystemShapeType.Circle;
                pullShape.radius = Diameter * 0.55f;
                pullShape.radiusThickness = 0f;
                pullShape.rotation = new Vector3(90f, 0f, 0f);
                var inward = pull.velocityOverLifetime;
                inward.enabled = true;
                inward.space = ParticleSystemSimulationSpace.Local;
                inward.x = new MinMaxCurve(0f);
                inward.y = new MinMaxCurve(0f);
                inward.z = new MinMaxCurve(0f);
                inward.radial = new MinMaxCurve(-0.7f);
                inward.orbitalZ = new MinMaxCurve(1.6f);
                Fade(pull, new[] { Key(1f, 1f, 1f, 0f), Key(0.8f, 0.8f, 0.8f, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });

                // Purple wisps rising out of the hole.
                var wisps = NewSystem(Child(root, "Wisps"), smokeMat, ParticleSystemRenderMode.Billboard, 20);
                var wispMain = wisps.main;
                wispMain.startLifetime = new MinMaxCurve(1.6f, 2.4f);
                wispMain.startSize = new MinMaxCurve(0.3f, 0.5f);
                wispMain.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
                var wispRate = wisps.emission;
                wispRate.rateOverTime = 6f;
                var wispShape = wisps.shape;
                wispShape.enabled = true;
                wispShape.shapeType = ParticleSystemShapeType.Circle;
                wispShape.radius = Diameter * 0.3f;
                wispShape.rotation = new Vector3(90f, 0f, 0f);
                var rise = wisps.velocityOverLifetime;
                rise.enabled = true;
                rise.space = ParticleSystemSimulationSpace.Local;
                rise.x = new MinMaxCurve(-0.05f, 0.05f);
                rise.y = new MinMaxCurve(0.4f, 0.8f);
                rise.z = new MinMaxCurve(-0.05f, 0.05f);
                Shrink(wisps, 0.6f, 1f, 1.6f);
                var sheet = wisps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = 2;
                sheet.numTilesY = 2;
                sheet.startFrame = new MinMaxCurve(0f, 3.99f);
                sheet.frameOverTime = new MinMaxCurve(0f);
                Fade(wisps, new[] { Key(0.8f, 0.8f, 0.8f, 0f), Key(0.3f, 0.3f, 0.3f, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.25f), new GradientAlphaKey(0f, 1f) });

                // The burst when something climbs out (played by Wall Portal): sparks shooting up.
                var burst = NewSystem(Child(root, "Spawn Burst"), glowMat, ParticleSystemRenderMode.Billboard, 40);
                var burstMain = burst.main;
                burstMain.loop = false;
                burstMain.prewarm = false;
                burstMain.playOnAwake = false;
                burstMain.duration = 1f;
                burstMain.startLifetime = new MinMaxCurve(0.4f, 0.9f);
                burstMain.startSpeed = new MinMaxCurve(2f, 4.5f);
                burstMain.startSize = new MinMaxCurve(0.06f, 0.14f);
                burstMain.gravityModifier = 0.6f;
                var burstRate = burst.emission;
                burstRate.rateOverTime = 0f;
                burstRate.SetBursts(new[] { new Burst(0f, 30) });
                var burstShape = burst.shape;
                burstShape.enabled = true;
                burstShape.shapeType = ParticleSystemShapeType.Cone;
                burstShape.angle = 25f;
                burstShape.radius = Diameter * 0.3f;
                burstShape.rotation = new Vector3(-90f, 0f, 0f);   // up
                Fade(burst, new[] { Key(1f, 1f, 1f, 0f), Key(0.8f, 0.8f, 0.8f, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

                var lightObject = Child(root, "Portal Light");
                lightObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 5f;
                light.intensity = 2.5f;
                light.color = new Color(0.6f, 0.35f, 1f);
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                lightObject.AddComponent<FlameFlicker>();

                // What the Fairy Shield touches to clear it: a trigger round the circle, on a kinematic body.
                var touch = root.AddComponent<SphereCollider>();
                touch.isTrigger = true;
                touch.radius = Diameter * 0.6f;
                touch.center = new Vector3(0f, 0.5f, 0f);
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                var voice = root.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.minDistance = 2f;
                voice.maxDistance = 20f;

                var portal = root.AddComponent<WallPortal>();
                var so = new SerializedObject(portal);
                // The enemy kinds a gate rolls from, each with its colour.
                var kinds = so.FindProperty("kinds");
                kinds.arraySize = Kinds.Length;
                for (int i = 0; i < Kinds.Length; i++)
                {
                    var kind = kinds.GetArrayElementAtIndex(i);
                    kind.FindPropertyRelative("name").stringValue = Kinds[i].name;
                    kind.FindPropertyRelative("colour").colorValue = Kinds[i].colour;
                    var list = kind.FindPropertyRelative("enemies");
                    list.arraySize = Kinds[i].enemies.Length;
                    for (int e = 0; e < Kinds[i].enemies.Length; e++)
                        list.GetArrayElementAtIndex(e).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + Kinds[i].enemies[e] + ".prefab");
                }
                SetList(so.FindProperty("tinted"), root, "Haze", "Runes", "Ring Green", "Ring Purple", "Pull", "Wisps", "Spawn Burst");
                SetList(so.FindProperty("stopWhenSpent"), root, "Pull", "Wisps");
                so.FindProperty("portalLight").objectReferenceValue = light;
                var legendary = so.FindProperty("legendary");
                legendary.arraySize = LegendaryPaths.Length;
                for (int i = 0; i < LegendaryPaths.Length; i++)
                    legendary.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(LegendaryPaths[i]);
                so.FindProperty("spawnBurst").objectReferenceValue = burst;
                so.FindProperty("spawnSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SpawnSound);
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, PortalPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetList(SerializedProperty list, GameObject root, params string[] names)
        {
            list.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var child = root.transform.Find(names[i]);
                list.GetArrayElementAtIndex(i).objectReferenceValue = child != null ? child.GetComponent<ParticleSystem>() : null;
            }
        }

        // One particle that stays, flat on the floor (Share of the circle's size), turning Spin rad/s, Lift above the floor
        // (so the layers don't flicker into each other).
        private static ParticleSystem Floor(GameObject go, Material material, float share, Color colour, float spin, float lift)
        {
            go.transform.localPosition = new Vector3(0f, lift, 0f);
            var ps = NewSystem(go, material, ParticleSystemRenderMode.HorizontalBillboard, 1);
            go.GetComponent<ParticleSystemRenderer>().maxParticleSize = 20f;
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

        // The brick wall: door-like toughness, and the portal under its drop point (switched on when it breaks).
        private static string SetUpWall(GameObject portalPrefab)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == BrickWallPath) return "Brick_Wall_Breakable is open in Prefab Mode: skipped";
            var door = AssetDatabase.LoadAssetAtPath<GameObject>(LivingRoomPath)
                .transform.Find("Livingroom_Base_Z_Armature/outholes/Bedroom_Base_Z_Door");
            var doorHits = door != null ? door.GetComponent<Game.Combat.EnemyHitDamage>() : null;
            var root = PrefabUtility.LoadPrefabContents(BrickWallPath);
            var notes = new List<string>();
            try
            {
                // (PlayMaker variables are set through the serialized FSM: FsmVariables changes don't stick here.)
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "Health") continue;
                    var so = new SerializedObject(fsm);
                    var floats = so.FindProperty("fsm.variables.floatVariables");
                    for (int i = 0; i < floats.arraySize; i++)
                    {
                        var variable = floats.GetArrayElementAtIndex(i);
                        if (variable.FindPropertyRelative("name").stringValue != "health") continue;
                        var value = variable.FindPropertyRelative("value");
                        if (value.floatValue != WallHealth) { notes.Add("health " + value.floatValue + " -> " + WallHealth); value.floatValue = WallHealth; }
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (doorHits != null && root.GetComponent<Game.Combat.EnemyHitDamage>() == null)
                {
                    var hits = root.AddComponent<Game.Combat.EnemyHitDamage>();
                    EditorUtility.CopySerialized(doorHits, hits);
                    notes.Add("takes weapon damage like the secret door");
                }
                var drop = root.transform.Find("Brick_Wall_Drop_Pos");
                if (drop != null && drop.Find(portalPrefab.name) == null)
                {
                    var portal = (GameObject)PrefabUtility.InstantiatePrefab(portalPrefab, drop);
                    portal.transform.position = root.transform.position;   // the middle of the wall; it moves to the floor when it opens
                    portal.transform.rotation = root.transform.rotation;
                    notes.Add("portal under Brick_Wall_Drop_Pos");
                }
                PrefabUtility.SaveAsPrefabAsset(root, BrickWallPath);
                return "Brick_Wall_Breakable: " + (notes.Count > 0 ? string.Join(", ", notes) : "already set up");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
