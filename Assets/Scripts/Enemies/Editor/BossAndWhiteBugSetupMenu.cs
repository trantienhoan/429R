using System.Collections.Generic;
using Game.Enemies;
using UnityEditor;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Aggressive Bosses And White Bugs:
    /// - the cockroaches get their hiding in tree pumpkins (normal 1 in 5, small 1 in 17: Spider Brain > Hiding in
    ///   pumpkins), and spiders and bees turn into a small white cockroach when they come out white (Anomaly Body);
    /// - every bug gets the rare white anomaly's look: the white mini boss's own painted material for its kind (Spider_,
    ///   Cockroach_ or Bee_Smol_White_Painted_Mat, same models and UVs), its sparkles (White_Bug_Glow), the burst where
    ///   it's killed (White_Bug_Pop) and a reward sound (Spider Brain > White anomaly; 1 in 100 by default);
    /// - the six mini bosses call only the small bug of their own kind, as many as the Enemy Limit has room for, as soon
    ///   as they run away (or get low, for the bees);
    /// - the spider and cockroach mini bosses get more health and toughness, a Stun Guard, and leap at the player from
    ///   further away (the spiders get a Pounce; the roaches' Face Leap reaches further). The bees are left as they are.
    /// Running it again sets the same numbers (it doesn't add up).
    /// </summary>
    public static class BossAndWhiteBugSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        // The white mini bosses' painted materials, by the start of the bug's name.
        private static readonly string[,] WhiteMaterials =
        {
            { "Spider", "Spider_Smol_White_Painted_Mat" },
            { "Cockroach", "Cockroach_Smol_White_Painted_Mat" },
            { "Bee", "Bee_Smol_White_Painted_Mat" },
        };
        private const string GlowPath = "Assets/Prefabs/Enemies/White_Bug_Glow.prefab";
        private const string PopPath = "Assets/Prefabs/Enemies/White_Bug_Pop.prefab";
        private const string RewardSound = "Assets/Audio/SFX/A_gentle_miracle_sound_1.wav";
        private const string SparkleTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string StarTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Flare02.png";
        // The white bug is always a small white cockroach; and what the roaches hide in and burst into.
        private const string WhiteRoach = "Assets/Prefabs/Enemies/Enemies_Smarter/Cockroach_Small.prefab";
        private const string RotPieces = "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_2_Drop.prefab";
        private const string Lana = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/";
        private const string Sfx = "Assets/Audio/SFX/";
        // Chance a cockroach hides in a tree pumpkin as it runs away low on health.
        private static readonly (string prefab, float chance)[] HideChances =
        {
            ("Cockroach_Smarter", 0f), ("Cockroach_Normal", 0.2f), ("Cockroach_Small", 1f / 17f),
            ("Cockroach_Boss", 0f), ("Cockroach_Boss_PumpkinButt", 0f),
        };

        private class Boss
        {
            public string name;
            public string minion;
            public bool walks;
            // Spider and cockroach bosses only: health (min, max), toughness and stun guard; 0,0 = leave as it is.
            public Vector2Int health;
            public float toughness;
            public float stunGuard;
        }

        private static readonly Boss[] Bosses =
        {
            new() { name = "Spider_Boss", minion = "Spider_Small", walks = true, health = new Vector2Int(50, 64), toughness = 3f, stunGuard = 2f },
            new() { name = "Spider_Boss_PumpkinButt", minion = "Spider_Small", walks = true, health = new Vector2Int(115, 126), toughness = 2.5f, stunGuard = 4f },
            new() { name = "Cockroach_Boss", minion = "Cockroach_Small", walks = true, health = new Vector2Int(50, 56), toughness = 2.5f, stunGuard = 2f },
            new() { name = "Cockroach_Boss_PumpkinButt", minion = "Cockroach_Small", walks = true, health = new Vector2Int(176, 195), toughness = 2.5f, stunGuard = 4f },
            new() { name = "Bee_Boss", minion = "Bee_Small" },
            new() { name = "Bee_Boss_PumpkinButt", minion = "Bee_Small" },
        };

        // Mini bosses wind up from this far (metres), so their leaps reach the player from further away.
        private const float BossAttackRange = 2.6f;

        [MenuItem("Tools/429 Game/Enemies/Aggressive Bosses And White Bugs")]
        public static void Run()
        {
            var glow = BuildGlow();
            var whiteRoach = AssetDatabase.LoadAssetAtPath<GameObject>(WhiteRoach);
            var pop = BuildPop();
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(RewardSound);
            var log = new List<string>();

            // Base prefabs first, so their variants just inherit the white bug's parts.
            var brains = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponent<SpiderBrain>() != null) brains.Add(path);
            }
            brains.Sort((a, b) => IsVariant(a).CompareTo(IsVariant(b)));
            int white = 0;
            foreach (var path in brains)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                var so = new SerializedObject(root.GetComponent<SpiderBrain>());
                var material = WhiteMaterialFor(System.IO.Path.GetFileNameWithoutExtension(path));
                string bug = System.IO.Path.GetFileNameWithoutExtension(path);
                bool roach = bug.StartsWith("Cockroach");
                bool changed = SetObject(so, "anomalyMaterial", material) | SetObject(so, "anomalyGlow", glow) |
                               SetObject(so, "anomalyPop", pop) | SetObject(so, "anomalyRewardSound", sound) |
                               SetObject(so, "anomalyBody", roach ? null : whiteRoach);
                if (roach) changed |= SetHiding(so, bug);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    white++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            log.Add($"white bug parts on {white} of {brains.Count} bug prefabs");

            foreach (var boss in Bosses) log.Add(SetUpBoss(boss));
            AssetDatabase.SaveAssets();
            Debug.Log("[Bosses And White Bugs] " + string.Join("; ", log));
        }

        private static bool IsVariant(string path) =>
            PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(path)) == PrefabAssetType.Variant;

        private static string SetUpBoss(Boss boss)
        {
            string path = Folder + boss.name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return boss.name + " missing";
            var brain = new SerializedObject(root.GetComponent<SpiderBrain>());

            brain.FindProperty("minion").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + boss.minion + ".prefab");
            brain.FindProperty("minionsFillLimit").boolValue = true;
            brain.FindProperty("callWhenFleeing").boolValue = true;
            brain.FindProperty("minionGap").vector2Value = new Vector2(0.15f, 0.35f);
            brain.FindProperty("aggressiveBoss").boolValue = true;
            string extra = "";
            if (boss.walks)
            {
                // They call as they run away (both at the same share of health).
                brain.FindProperty("callBelowHealth").floatValue = brain.FindProperty("fleeBelowHealth").floatValue;
                brain.FindProperty("boss").FindPropertyRelative("health").vector2IntValue = boss.health;
                brain.FindProperty("stunGuardSeconds").floatValue = boss.stunGuard;
                brain.FindProperty("attackRange").floatValue = BossAttackRange;
                extra = boss.name.StartsWith("Spider") ? Pounce(brain.FindProperty("attacks")) : LongerFaceLeap(brain.FindProperty("attacks"));

                var hits = new SerializedObject(root.GetComponent<Game.Combat.EnemyHitDamage>());
                hits.FindProperty("toughness").floatValue = boss.toughness;
                hits.ApplyModifiedPropertiesWithoutUndo();
                extra = $", health {boss.health}, toughness {boss.toughness}, stun guard {boss.stunGuard}, {extra}";
            }
            brain.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return $"{boss.name}: calls {boss.minion} (fills the Enemy Limit){extra}";
        }

        // The spider bosses spring onto the player from up to 3 m away (their bite animation, lifted to the face).
        private static string Pounce(SerializedProperty attacks)
        {
            var move = Find(attacks, "Pounce");
            if (move == null)
            {
                // A copy of the Bite (its sounds and bite size), changed below.
                int bite = IndexOf(attacks, "Bite");
                if (bite < 0) bite = 0;
                attacks.InsertArrayElementAtIndex(bite);
                move = attacks.GetArrayElementAtIndex(bite + 1);
            }
            move.FindPropertyRelative("name").stringValue = "Pounce";
            move.FindPropertyRelative("animation").stringValue = "attack";
            move.FindPropertyRelative("weight").floatValue = 3f;
            move.FindPropertyRelative("minDistance").floatValue = 0.8f;
            move.FindPropertyRelative("maxDistance").floatValue = 3f;
            move.FindPropertyRelative("lunge").floatValue = 2.8f;
            move.FindPropertyRelative("hitFrom").floatValue = 0.45f;
            move.FindPropertyRelative("hitUntil").floatValue = 0.75f;
            move.FindPropertyRelative("duration").floatValue = 1.2f;
            move.FindPropertyRelative("leapToFace").boolValue = true;
            move.FindPropertyRelative("runAwayAfter").boolValue = false;
            move.FindPropertyRelative("pushPower").floatValue = 2f;
            return "Pounce up to 3 m";
        }

        // The cockroach bosses' Face Leap starts from further away.
        private static string LongerFaceLeap(SerializedProperty attacks)
        {
            var leap = Find(attacks, "Face Leap");
            if (leap == null) return "no Face Leap found";
            leap.FindPropertyRelative("maxDistance").floatValue = 2.8f;
            leap.FindPropertyRelative("lunge").floatValue = 2.6f;
            return "Face Leap up to 2.8 m";
        }

        private static SerializedProperty Find(SerializedProperty attacks, string name)
        {
            int i = IndexOf(attacks, name);
            return i >= 0 ? attacks.GetArrayElementAtIndex(i) : null;
        }

        private static int IndexOf(SerializedProperty attacks, string name)
        {
            for (int i = 0; i < attacks.arraySize; i++)
            {
                if (attacks.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return i;
            }
            return -1;
        }

        // A cockroach: what it hides in a tree pumpkin with, and how often (Hiding in pumpkins).
        private static bool SetHiding(SerializedObject so, string bug)
        {
            bool changed = SetObject(so, "hideBurst", SmallRoachLink()) |
                           SetObject(so, "rotPieces", AssetDatabase.LoadAssetAtPath<GameObject>(RotPieces)) |
                           SetObject(so, "rotBurstSound", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "pumpkin_explode_2.wav")) |
                           SetObject(so, "rotSquashSound", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "Bug_Squish_Bedroom.wav"));
            changed |= SetList(so.FindProperty("rotPuffs"), Lana + "Hit_Blast_BLack.prefab");
            changed |= SetList(so.FindProperty("rotBurstEffects"), Lana + "Hit_Blast_BLack.prefab", Lana + "Smoke_Blast.prefab");
            changed |= SetList(so.FindProperty("rotHiss"), Sfx + "cockroach_screech.wav");
            foreach (var (prefab, chance) in HideChances)
            {
                if (prefab != bug) continue;
                var p = so.FindProperty("hideChance");
                if (!Mathf.Approximately(p.floatValue, chance))
                {
                    p.floatValue = chance;
                    changed = true;
                }
            }
            return changed;
        }

        // The Prefab Link to Cockroach_Small (made the first time): what the rotten pumpkins burst into.
        private static PrefabLink SmallRoachLink()
        {
            const string path = "Assets/Data/Enemies/Cockroach_Small Link.asset";
            var link = AssetDatabase.LoadAssetAtPath<PrefabLink>(path);
            if (link == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data/Enemies")) AssetDatabase.CreateFolder("Assets/Data", "Enemies");
                link = ScriptableObject.CreateInstance<PrefabLink>();
                AssetDatabase.CreateAsset(link, path);
            }
            link.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WhiteRoach);
            EditorUtility.SetDirty(link);
            return link;
        }

        private static bool SetList(SerializedProperty list, params string[] paths)
        {
            bool changed = list.arraySize != paths.Length;
            list.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(paths[i]);
                var element = list.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == asset) continue;
                element.objectReferenceValue = asset;
                changed = true;
            }
            return changed;
        }

        private static bool SetObject(SerializedObject so, string field, Object value)
        {
            var property = so.FindProperty(field);
            if (property == null || property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            return true;
        }

        // ---- the white bug's looks ----

        // The white mini boss's painted material for this kind of bug (it uses the same models), or none.
        private static Material WhiteMaterialFor(string bug)
        {
            for (int i = 0; i < WhiteMaterials.GetLength(0); i++)
            {
                if (!bug.StartsWith(WhiteMaterials[i, 0])) continue;
                foreach (var guid in AssetDatabase.FindAssets(WhiteMaterials[i, 1] + " t:Material"))
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (material != null && material.name == WhiteMaterials[i, 1]) return material;
                }
            }
            return null;
        }

        // Shiny white sparkles round the white bug: little motes drifting off it, bright four-point star twinkles
        // popping on and off around it, and a soft white halo. Sized in metres (a small bug doesn't shrink them).
        private static GameObject BuildGlow()
        {
            var sparkle = DangerFlameSetupMenu.MakeMaterial("White Bug Sparkle", SparkleTexture, true);
            sparkle.SetColor("_BaseColor", new Color(0.85f, 0.9f, 1f));
            var star = DangerFlameSetupMenu.MakeMaterial("White Bug Twinkle", StarTexture, true);
            star.SetColor("_BaseColor", Color.white);
            var halo = DangerFlameSetupMenu.MakeMaterial("White Bug Halo", SparkleTexture, true);
            halo.SetColor("_BaseColor", new Color(0.75f, 0.85f, 1f, 0.35f));
            var root = new GameObject("White_Bug_Glow");

            var ps = DangerFlameSetupMenu.NewSystem(root, sparkle, ParticleSystemRenderMode.Billboard, 40);
            var main = ps.main;
            main.prewarm = false;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new MinMaxCurve(0.5f, 0.9f);
            main.startSize = new MinMaxCurve(0.025f, 0.055f);
            main.startSpeed = new MinMaxCurve(0.05f, 0.25f);
            main.gravityModifier = -0.05f;
            main.startColor = new MinMaxGradient(Color.white, new Color(0.7f, 0.85f, 1f));
            var emission = ps.emission;
            emission.rateOverTime = 22f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            DangerFlameSetupMenu.Shrink(ps, 0.4f, 1f, 0f);

            var twinkles = DangerFlameSetupMenu.NewSystem(DangerFlameSetupMenu.Child(root, "Twinkles"), star, ParticleSystemRenderMode.Billboard, 16);
            main = twinkles.main;
            main.prewarm = false;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new MinMaxCurve(0.3f, 0.55f);
            main.startSize = new MinMaxCurve(0.06f, 0.12f);
            main.startSpeed = 0f;
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new MinMaxGradient(Color.white, new Color(0.85f, 0.92f, 1f));
            emission = twinkles.emission;
            emission.rateOverTime = 7f;
            shape = twinkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var spin = twinkles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new MinMaxCurve(-2f, 2f);
            // Each twinkle pops up and is gone again: a quick flash.
            var size = twinkles.sizeOverLifetime;
            size.enabled = true;
            size.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));

            var glow = DangerFlameSetupMenu.NewSystem(DangerFlameSetupMenu.Child(root, "Halo"), halo, ParticleSystemRenderMode.Billboard, 2);
            main = glow.main;
            main.prewarm = true;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 1.2f;
            main.startSize = 0.42f;
            main.startSpeed = 0f;
            emission = glow.emission;
            emission.rateOverTime = 1.6f;
            shape = glow.shape;
            shape.enabled = false;
            DangerFlameSetupMenu.Fade(glow, new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });

            var saved = PrefabUtility.SaveAsPrefabAsset(root, GlowPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildPop()
        {
            var sparkle = DangerFlameSetupMenu.MakeMaterial("White Bug Sparkle", SparkleTexture, true);
            var root = new GameObject("White_Bug_Pop");
            var flash = DangerFlameSetupMenu.NewSystem(DangerFlameSetupMenu.Child(root, "Flash"), sparkle, ParticleSystemRenderMode.Billboard, 2);
            OneShot(flash, 0.25f);
            var main = flash.main;
            main.startSize = 0.8f;
            main.startColor = Color.white;
            flash.emission.SetBursts(new[] { new Burst(0f, 1) });
            DangerFlameSetupMenu.Shrink(flash, 0.5f, 1f, 0f);

            var sparks = DangerFlameSetupMenu.NewSystem(DangerFlameSetupMenu.Child(root, "Sparks"), sparkle, ParticleSystemRenderMode.Billboard, 50);
            OneShot(sparks, 0.9f);
            main = sparks.main;
            main.startLifetime = new MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new MinMaxCurve(1f, 3f);
            main.startSize = new MinMaxCurve(0.04f, 0.09f);
            main.gravityModifier = -0.1f;
            main.startColor = new MinMaxGradient(Color.white, new Color(1f, 0.6f, 0.9f));
            sparks.emission.SetBursts(new[] { new Burst(0f, 36) });
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            DangerFlameSetupMenu.Shrink(sparks, 1f, 1f, 0f);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PopPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void OneShot(ParticleSystem ps, float duration)
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.duration = duration;
            main.startLifetime = duration;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
        }
    }
}


