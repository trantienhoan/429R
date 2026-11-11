using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Game.VFX;
using HutongGames.PlayMaker;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Set Up PlayGround Slimes (user, 2026-11-07). Builds the PlayGround's ground slimes
    /// in Prefabs/Enemies/PLAYGROUND from the user's Giant_Slime model (Models/Enemies/Giant_Slime.fbx, kept a nested
    /// model, so a new export shows up by itself). Each is a Spider Brain with no Animator (its settings copied from
    /// Cockroach_Boss_PumpkinButt, then its own) and a Slime Body (the jelly look, the split, the candies), with a
    /// SlimeHealth FSM and Enemy Hit Damage like the bugs:
    /// - Slime_Small: only bites (Damage, 6-9), 3-4 health, 0-2 candies, never splits.
    /// - Slime_Big: what a wrong recipe makes, in the user's Giant_Slime_Mat. A mini boss (a bite and a body slam, both
    ///   25-50 damage); killed by the player it splits into 3 Slime_Small.
    /// - Slime_Big_Blue "Sticky" (round 1): sticky bites that slow, glob shots, Giant Web.
    /// - Slime_Big_Red "Bull" (round 2): bigger, Bulldozer, Bull Charge.
    /// - Slime_Big_Yellow "Bomb" (round 3): fast, Ticking Bomb (it splits out of its own blast).
    /// The coloured ones wear their own material (Materials/Flying_Slime/Slime_Blue_Mat, Slime_Red_Mat, Slime_Yellow_Mat:
    /// Giant_Slime_Mat's look on the Flying_Slime_Paint texture, in the Paint Bees colours). Only the user's slime and pop
    /// sounds (the specials keep the bugs' own charge and bomb sounds).
    /// Safe to run again: each prefab is rebuilt in place (its root is kept, so whatever points at it still does). Never
    /// touches the scene.
    /// </summary>
    public static class PlayGroundSlimesSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/PLAYGROUND/";
        private const string SmallPath = Folder + "Slime_Small.prefab";
        private const string BigPath = Folder + "Slime_Big.prefab";
        private const string BluePath = Folder + "Slime_Big_Blue.prefab";
        private const string RedPath = Folder + "Slime_Big_Red.prefab";
        private const string YellowPath = Folder + "Slime_Big_Yellow.prefab";
        private const string ModelPath = "Assets/Models/Enemies/Giant_Slime.fbx";
        private const string HeadName = "Giant_Slime_Below";
        private const string PlainMaterialPath = "Assets/Materials/Flying_Slime/Giant_Slime_Mat.mat";
        private const string PaintMapPath = "Assets/M_Textures/Flying_Slime/Flying_Slime_Paint.png";
        private const string MaterialPath = "Assets/Materials/Flying_Slime/Slime_{0}_Mat.mat";
        private const string Bugs = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string RoachBossPath = Bugs + "Cockroach_Boss_PumpkinButt.prefab";
        private const string RoachBulldozerPath = Bugs + "Cockroach_Boss.prefab";
        private const string SpiderBossPath = Bugs + "Spider_Boss_PumpkinButt.prefab";
        private const string BeeBossPath = Bugs + "Bee_Boss_PumpkinButt.prefab";
        private const string WebBallPath = Bugs + "Spider_Web_Ball.prefab";
        private const string CandyPath = "Assets/Prefabs/Items/Breakable/Candy_{0}.prefab";
        private const string HealthFsmName = "SlimeHealth";
        private static readonly string[] SlimeSounds =
        {
            "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/slime_000.ogg",
            "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/slime_001.ogg",
        };
        private static readonly string[] PopSounds =
        {
            "Assets/Audio/SFX/Balloon_Pop.wav",
            "Assets/Audio/SFX/bubble_pop_1.wav",
        };
        private static readonly string[] BurstEffects =
        {
            "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab",
            "Assets/Prefabs/Effects/Smoke/Smoke_Puff_Burst_Out.prefab",
        };

        /// <summary>One slime to build: its prefab and its numbers (slimes.md 3.5).</summary>
        private sealed class Recipe
        {
            public string path;
            public string name;
            public bool small;
            public string colour;
            public Color paint = Color.white;
            public Vector2Int health;
            public float toughness = 2.5f;
            public float stray = 4f;
            public int exp;
            public Vector2 scale;
            public float speed = 1f;
            public float run = 3f;
            public float knockBack = 0.35f;
            public float rest = 0.4f;
            public float pitch = 0.75f;
            public Vector2 launch = new(0.4f, 1.6f);
            public float stunGuard = 2f;
            public SpiderBrain.SpecialMove special = SpiderBrain.SpecialMove.None;
            public float specialBelow = 0.3f;
            public bool sticky;
            public bool bulldozer;
        }

        // What every slime is built from, loaded once.
        private sealed class Parts
        {
            public GameObject model;
            public Material plain;
            public SpiderBrain roachBrain;
            public EnemyHitDamage roachHits;
            public NavMeshAgent roachAgent;
            public SpiderBrain spiderBrain;
            public SpiderBrain beeBrain;
            public Bulldozer bulldozer;
            public GameObject webBall;
            public AudioClip[] slimeSounds;
            public AudioClip[] popSounds;
            public GameObject[] burstEffects;
            public GameObject[] candies;
            public GameObject small;
        }

        [MenuItem("Tools/429 Game/PlayGround/Set Up PlayGround Slimes")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[PlayGround Slimes] Stop play mode first. Nothing changed.");
                return;
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath.StartsWith(Folder + "Slime_"))
            {
                Debug.LogError($"[PlayGround Slimes] {stage.assetPath} is open in Prefab Mode: close it first. Nothing changed.");
                return;
            }
            Debug.Log("[PlayGround Slimes] " + string.Join("; ", SetUp()));
            // The candies' slime colours and drops win over the paint colours above (user, 2026-11-10).
            Game.Inventory.Editor.CandyColourSetupMenu.Run();
        }

        /// <summary>Builds (or rebuilds) the five slime prefabs and the three colour materials; returns what it did.</summary>
        public static List<string> SetUp()
        {
            var notes = new List<string>();
            var parts = LoadParts(notes);
            if (parts == null) return notes;

            var recipes = new[]
            {
                new Recipe
                {
                    path = SmallPath, name = "Slime_Small", small = true, health = new Vector2Int(3, 4), toughness = 1f,
                    stray = 1f, exp = 1, scale = new Vector2(0.45f, 0.55f), speed = 1.1f, run = 3f, knockBack = 1f,
                    rest = 1f, pitch = 1.4f, launch = Vector2.zero, stunGuard = 0f,
                },
                new Recipe
                {
                    path = BigPath, name = "Slime_Big", health = new Vector2Int(55, 65), exp = 5,
                    scale = new Vector2(1.4f, 1.6f), speed = 0.9f,
                },
                new Recipe
                {
                    path = BluePath, name = "Slime_Big_Blue", colour = "Blue", paint = new Color(0.2f, 0.45f, 1f),
                    health = new Vector2Int(85, 95), exp = 6, scale = new Vector2(1.4f, 1.6f), speed = 0.9f,
                    special = SpiderBrain.SpecialMove.GiantWeb, specialBelow = 0.6f, sticky = true,
                },
                new Recipe
                {
                    path = RedPath, name = "Slime_Big_Red", colour = "Red", paint = new Color(0.89f, 0.15f, 0.18f),
                    health = new Vector2Int(125, 140), exp = 8, scale = new Vector2(1.7f, 1.9f), speed = 0.95f, run = 3.2f,
                    knockBack = 0.2f, pitch = 0.65f, launch = new Vector2(0.6f, 2f), stunGuard = 2.5f,
                    special = SpiderBrain.SpecialMove.BullCharge, specialBelow = 0.5f, bulldozer = true,
                },
                new Recipe
                {
                    path = YellowPath, name = "Slime_Big_Yellow", colour = "Yellow", paint = new Color(1f, 0.85f, 0.12f),
                    health = new Vector2Int(130, 145), toughness = 2f, exp = 10, scale = new Vector2(1.4f, 1.6f),
                    speed = 1.15f, run = 3.2f, rest = 0.3f, special = SpiderBrain.SpecialMove.TickingBomb, specialBelow = 0.3f,
                },
            };

            // The small one first: the big ones split into it.
            foreach (var recipe in recipes)
            {
                var material = recipe.colour == null ? parts.plain : ColourMaterial(recipe, parts.plain, notes);
                if (!Build(recipe, material, parts, notes)) continue;
                if (recipe.small) parts.small = AssetDatabase.LoadAssetAtPath<GameObject>(recipe.path);
            }
            AssetDatabase.SaveAssets();
            return notes;
        }

        private static Parts LoadParts(List<string> notes)
        {
            var parts = new Parts { model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) };
            var roach = AssetDatabase.LoadAssetAtPath<GameObject>(RoachBossPath);
            if (parts.model == null || roach == null || !roach.TryGetComponent(out parts.roachBrain) ||
                !roach.TryGetComponent(out parts.roachHits) || !roach.TryGetComponent(out parts.roachAgent))
            {
                notes.Add($"WARNING: no {ModelPath} or no Cockroach_Boss_PumpkinButt (with its brain) at {RoachBossPath}: nothing made");
                return null;
            }
            parts.plain = Load<Material>(PlainMaterialPath, notes);
            parts.spiderBrain = Brain(SpiderBossPath, notes);
            parts.beeBrain = Brain(BeeBossPath, notes);
            var dozer = Load<GameObject>(RoachBulldozerPath, notes);
            if (dozer != null) dozer.TryGetComponent(out parts.bulldozer);
            parts.webBall = Load<GameObject>(WebBallPath, notes);
            parts.slimeSounds = LoadAll<AudioClip>(SlimeSounds, notes);
            parts.popSounds = LoadAll<AudioClip>(PopSounds, notes);
            parts.burstEffects = LoadAll<GameObject>(BurstEffects, notes);
            var candyPaths = System.Array.ConvertAll(Game.Inventory.CandyKinds.Numbers, n => string.Format(CandyPath, n));
            parts.candies = LoadAll<GameObject>(candyPaths, notes);
            // Rebuilt first below; until then (or if that fails) the one from before.
            parts.small = AssetDatabase.LoadAssetAtPath<GameObject>(SmallPath);
            return parts;
        }

        // Giant_Slime_Mat's look on the paint texture, in the slime's colour (a material, not a property block: the
        // Ticking Bomb's flash puts back the material's own colour). Made once, updated after.
        private static Material ColourMaterial(Recipe recipe, Material plain, List<string> notes)
        {
            string path = string.Format(MaterialPath, recipe.colour);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (plain == null)
                {
                    notes.Add($"WARNING: no {PlainMaterialPath}: {recipe.name} keeps the model's own material");
                    return null;
                }
                material = new Material(plain) { name = $"Slime_{recipe.colour}_Mat" };
                AssetDatabase.CreateAsset(material, path);
                notes.Add($"made {path}");
            }
            var paint = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintMapPath);
            if (paint != null) material.SetTexture("_BaseMap", paint);
            else notes.Add($"WARNING: no {PaintMapPath}: {path} keeps its texture");
            material.SetColor("_BaseColor", recipe.paint);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Rebuilds the prefab in place (its root kept), or makes it new in a preview scene.
        private static bool Build(Recipe recipe, Material material, Parts parts, List<string> notes)
        {
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(recipe.path) != null;
            var scene = default(UnityEngine.SceneManagement.Scene);
            GameObject root;
            if (existing)
            {
                root = PrefabUtility.LoadPrefabContents(recipe.path);
            }
            else
            {
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject(recipe.name);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            }
            try
            {
                Fill(root, recipe, material, parts, notes);
                PrefabUtility.SaveAsPrefabAsset(root, recipe.path, out bool saved);
                notes.Add(saved ? $"{(existing ? "rebuilt" : "made")} {recipe.name}" : $"WARNING: couldn't save {recipe.path}");
                return saved;
            }
            catch (System.Exception e)
            {
                notes.Add($"WARNING: {recipe.name} not built: {e.Message}");
                Debug.LogException(e);
                return false;
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else
                {
                    Object.DestroyImmediate(root);
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static void Fill(GameObject root, Recipe recipe, Material material, Parts parts, List<string> notes)
        {
            root.name = recipe.name;
            root.tag = "Enemy";
            root.layer = 0;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // The look, made fresh: Model (the brain's Models: its leap lift, the bomb's swell) > Jiggle (wobbles when
            // hurt) > Body (Slime Body's squash, stretch and hops) > Giant_Slime.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                if (child.name == "Model") Object.DestroyImmediate(child.gameObject);
            }
            var model = Child(root.transform, "Model");
            var jiggle = Child(model, "Jiggle");
            var body = Child(jiggle, "Body");
            var look = (GameObject)PrefabUtility.InstantiatePrefab(parts.model, body);
            look.name = "Giant_Slime";
            foreach (var c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in look.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var r in look.GetComponentsInChildren<Renderer>(true))
            {
                if (material != null)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = material;
                    r.sharedMaterials = mats;
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // Its bottom middle on the root (it squashes from the floor up). The root sits at the origin, unscaled.
            var all = Bounds(look);
            look.transform.localPosition -= new Vector3(all.center.x, all.min.y, all.center.z);
            float tall = all.size.y, wide = Mathf.Max(all.size.x, all.size.z);
            var head = look.transform.Find(HeadName);
            if (head == null) notes.Add($"WARNING: no {HeadName} in the model: {recipe.name}'s mouth won't open");

            var agent = GetOrAdd<NavMeshAgent>(root);
            agent.agentTypeID = parts.roachAgent.agentTypeID;
            agent.radius = wide * 0.43f;
            agent.height = tall;
            agent.baseOffset = 0f;
            agent.speed = 3f;
            agent.acceleration = 20f;
            agent.angularSpeed = 900f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            // The hit box: a sphere round its blob on a kinematic Rigidbody (moved by its agent), like the bugs.
            var rigid = GetOrAdd<Rigidbody>(root);
            rigid.isKinematic = true;
            rigid.useGravity = false;
            rigid.interpolation = RigidbodyInterpolation.None;
            var ball = GetOrAdd<SphereCollider>(root);
            ball.isTrigger = false;
            ball.center = new Vector3(0f, tall * 0.5f, 0f);
            ball.radius = (tall + wide) * 0.25f * 0.9f;

            var speaker = GetOrAdd<AudioSource>(root);
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1f;
            speaker.rolloffMode = AudioRolloffMode.Logarithmic;
            speaker.minDistance = 2f;
            speaker.maxDistance = 25f;
            speaker.dopplerLevel = 0f;

            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == HealthFsmName) Object.DestroyImmediate(fsm);
            }
            // The lowest it can have: the brain picks its health at Start, and a rise never shows a damage number.
            AddHealthFsm(root, recipe.health.x);

            var hits = GetOrAdd<EnemyHitDamage>(root);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(parts.roachHits), hits);
            var hso = new SerializedObject(hits);
            P(hso, "toughness").floatValue = recipe.toughness;
            P(hso, "strayToughness").floatValue = recipe.stray;
            P(hso, "exp").intValue = recipe.exp;
            P(hso, "expOrbs").boolValue = true;
            P(hso, "healthFsm").stringValue = HealthFsmName;
            P(hso, "healthVariable").stringValue = "health";
            P(hso, "hitEvent").stringValue = "Damage";
            P(hso, "weakHitEvent").stringValue = "ShakeSmall";
            P(hso, "oldDamageFsm").stringValue = "";
            P(hso, "knockBack").boolValue = true;
            P(hso, "knockBackDistance").floatValue = recipe.small ? 1.5f : 1.2f;
            P(hso, "furniture").boolValue = false;
            hso.ApplyModifiedPropertiesWithoutUndo();

            GetOrAdd<LandOnNavMesh>(root);
            var brain = GetOrAdd<SpiderBrain>(root);
            SetUpBrain(brain, model, recipe, parts);
            SetUpJiggle(GetOrAdd<Jiggle>(jiggle.gameObject));

            var slime = GetOrAdd<SlimeBody>(root);
            var so = new SerializedObject(slime);
            P(so, "body").objectReferenceValue = body;
            P(so, "head").objectReferenceValue = head;
            P(so, "splitInto").objectReferenceValue = recipe.small ? null : parts.small;
            P(so, "splitCount").intValue = 3;
            SetList(P(so, "candies"), recipe.small ? parts.candies : new Object[0]);
            P(so, "candyCount").vector2IntValue = new Vector2Int(0, 2);
            SetList(P(so, "burstEffects"), parts.burstEffects);
            P(so, "burstEffectSize").floatValue = 0.5f;
            SetList(P(so, "popSounds"), parts.popSounds);
            SetList(P(so, "squelchSounds"), parts.slimeSounds);
            P(so, "shieldTag").stringValue = "Fairy_Shield";
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!recipe.small && parts.small == null)
                notes.Add($"WARNING: no Slime_Small yet: {recipe.name} won't split (run again)");

            // The red one ploughs through loose things like the roach boss.
            var dozer = root.GetComponent<Bulldozer>();
            if (recipe.bulldozer)
            {
                if (dozer == null) dozer = root.AddComponent<Bulldozer>();
                if (parts.bulldozer != null) EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(parts.bulldozer), dozer);
            }
            else if (dozer != null) Object.DestroyImmediate(dozer);
        }

        private static void SetUpBrain(SpiderBrain brain, Transform model, Recipe recipe, Parts parts)
        {
            // The roach mini boss's settings (sounds, timings, the charge), then the slime's own.
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(parts.roachBrain), brain);
            var so = new SerializedObject(brain);
            bool small = recipe.small;
            P(so, "size").enumValueIndex = (int)(small ? SpiderBrain.SizeClass.Small : SpiderBrain.SizeClass.Boss);
            var kind = P(so, small ? "small" : "boss");
            R(kind, "weight").floatValue = 1f;
            R(kind, "scale").vector2Value = recipe.scale;
            R(kind, "health").vector2IntValue = recipe.health;
            R(kind, "speed").floatValue = recipe.speed;
            R(kind, "windUp").floatValue = small ? 1f : 1.3f;
            R(kind, "playerEvent").stringValue = small ? "" : "DamageHeavy";
            R(kind, "knockBack").floatValue = recipe.knockBack;
            R(kind, "pitch").floatValue = recipe.pitch;
            R(kind, "dodgeChance").floatValue = 0f;
            R(kind, "webs").boolValue = recipe.sticky;
            R(kind, "rest").floatValue = recipe.rest;
            R(kind, "launchHeight").floatValue = recipe.launch.x;
            R(kind, "launchDistance").floatValue = recipe.launch.y;

            P(so, "appearAnimation").stringValue = "";
            P(so, "target").enumValueIndex = (int)SpiderBrain.TargetKind.Player;
            P(so, "noticeOnSpawn").boolValue = true;
            P(so, "walkSpeed").floatValue = 0.8f;
            P(so, "runSpeed").floatValue = recipe.run;
            P(so, "skitter").boolValue = small;
            P(so, "attackRange").floatValue = small ? 1.3f : 2.6f;
            P(so, "maxAttackers").intValue = 3;
            SetAttacks(P(so, "attacks"), recipe, parts);
            P(so, "dodgeBackAnimation").stringValue = "";
            P(so, "dodgeLeftAnimation").stringValue = "";
            P(so, "dodgeRightAnimation").stringValue = "";
            P(so, "flies").boolValue = false;
            P(so, "bossRush").boolValue = false;
            P(so, "stunGuardSeconds").floatValue = recipe.stunGuard;
            P(so, "fleeBelowHealth").floatValue = 0f;
            // No rig: the brain moves the root and Models; Slime Body does the rest.
            P(so, "models").objectReferenceValue = model;
            P(so, "animator").objectReferenceValue = null;
            P(so, "deathAnimation").stringValue = "";
            P(so, "growFrom").floatValue = small ? 0.6f : 0.7f;
            P(so, "growSeconds").floatValue = small ? 1.5f : 2f;
            P(so, "hitEffect").objectReferenceValue = null;
            P(so, "setOnDeath").arraySize = 0;
            P(so, "aggressiveBoss").boolValue = true;
            P(so, "unstoppableAttacks").boolValue = true;
            P(so, "unstoppablePlayerEvent").stringValue = "DamageStrongZ";
            P(so, "bossHelmetSeconds").floatValue = 0f;
            // They split instead (and a boss's minions never count in a wave).
            P(so, "minion").objectReferenceValue = null;
            P(so, "callBelowHealth").floatValue = 0f;
            P(so, "minionsFillLimit").boolValue = false;
            P(so, "hideChance").floatValue = 0f;
            P(so, "anomalyChance").floatValue = 0f;
            P(so, "anomalySwap").objectReferenceValue = null;
            P(so, "anomalyBody").objectReferenceValue = null;
            SetList(P(so, "noticeSounds"), parts.slimeSounds);
            SetList(P(so, "hitSounds"), parts.slimeSounds);
            SetList(P(so, "squashSounds"), parts.slimeSounds);
            P(so, "squashExtras").arraySize = 0;
            P(so, "chargeRoar").objectReferenceValue = null;

            // Glob shots (the blue one): the spider's web ball, spat often, with a slime sound.
            P(so, "webBall").objectReferenceValue = recipe.sticky ? parts.webBall : null;
            P(so, "webAnimation").stringValue = "";
            P(so, "webSound").objectReferenceValue = parts.slimeSounds.Length > 1 ? parts.slimeSounds[1] : null;
            P(so, "webRange").vector2Value = new Vector2(2f, 7f);
            P(so, "webCooldown").vector2Value = new Vector2(3f, 5f);
            P(so, "maxWebShooters").intValue = 1;

            P(so, "special").enumValueIndex = (int)recipe.special;
            P(so, "specialBelowHealth").floatValue = recipe.specialBelow;
            P(so, "specialCooldown").vector2Value = new Vector2(10f, 15f);
            P(so, "specialPlayerEvent").stringValue = "PumpkinBossHit";
            switch (recipe.special)
            {
                case SpiderBrain.SpecialMove.GiantWeb:
                    Copy(so, parts.spiderBrain, "webSpeed", "webReleaseTime", "webDuration", "giantWebAnimationSpeed", "giantWebSpeed");
                    P(so, "specialDistance").floatValue = 5.5f;
                    P(so, "giantWebSize").floatValue = 2.5f;
                    break;
                case SpiderBrain.SpecialMove.BullCharge:
                    // The charge itself (speed, launch 3/6, dust, its whoosh and hit) is the roach boss's, copied above.
                    P(so, "specialDistance").floatValue = 7f;
                    break;
                case SpiderBrain.SpecialMove.TickingBomb:
                    Copy(so, parts.beeBrain, "bombSeconds", "bombKillsItself", "bombRadius", "bombLaunch", "bombPush", "bombEffects",
                        "bombEffectSize", "bombSound", "bombBeep", "bombFlash", "bombBlinks", "bombSwell", "bombDamageTaken");
                    break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Small: one bite. Big: a bite (blocked by a fist or a held weapon) and an unstoppable body slam from further off.
        // Their FSM events (Chomp, Slam) are Slime Body's hops.
        private static void SetAttacks(SerializedProperty list, Recipe recipe, Parts parts)
        {
            var slam = parts.slimeSounds.Length > 0 ? parts.slimeSounds[0] : null;
            if (recipe.small)
            {
                list.arraySize = 1;
                Attack(list.GetArrayElementAtIndex(0), "Bite", SpiderBrain.Sizes.Small | SpiderBrain.Sizes.Normal | SpiderBrain.Sizes.Boss,
                    0f, 1.5f, 0.8f, 0.3f, 0.55f, 0.9f, 0.35f, 0.25f, "Damage", null, "Chomp", false, 1f, 1f, 1f, 0f);
                return;
            }
            list.arraySize = 2;
            Attack(list.GetArrayElementAtIndex(0), "Bite", SpiderBrain.Sizes.Boss, 0f, 1.6f, 1f, 0.3f, 0.55f, 1f, 0.3f, 0.3f,
                "DamageHeavy", null, "Chomp", false, 1.3f, 1f, recipe.sticky ? 0.6f : 1f, recipe.sticky ? 1.5f : 0f);
            Attack(list.GetArrayElementAtIndex(1), "Body Slam", SpiderBrain.Sizes.Boss, 0.8f, 3f, 2.2f, 0.35f, 0.6f, 1.1f, 0.3f, 0.4f,
                "DamageHeavy", slam, "Slam", true, 2f, 1.5f, 1f, 0f);
        }

        private static void Attack(SerializedProperty a, string name, SpiderBrain.Sizes usedBy, float minDistance, float maxDistance,
            float lunge, float hitFrom, float hitUntil, float duration, float reach, float biteRadius, string playerEvent,
            AudioClip sound, string fsmEvent, bool unstoppable, float pushPower, float pushFarther, float slowTo, float slowSeconds)
        {
            R(a, "name").stringValue = name;
            R(a, "usedBy").intValue = (int)usedBy;
            R(a, "animation").stringValue = "";
            R(a, "weight").floatValue = 1f;
            R(a, "minDistance").floatValue = minDistance;
            R(a, "maxDistance").floatValue = maxDistance;
            R(a, "lunge").floatValue = lunge;
            R(a, "hitFrom").floatValue = hitFrom;
            R(a, "hitUntil").floatValue = hitUntil;
            R(a, "duration").floatValue = duration;
            R(a, "reach").floatValue = reach;
            R(a, "biteRadius").floatValue = biteRadius;
            R(a, "playerEvent").stringValue = playerEvent;
            R(a, "sound").objectReferenceValue = sound;
            R(a, "landSound").objectReferenceValue = null;
            R(a, "fsmEvent").stringValue = fsmEvent;
            R(a, "leapToFace").boolValue = false;
            R(a, "unstoppable").boolValue = unstoppable;
            R(a, "runAwayAfter").boolValue = false;
            R(a, "throughPlayer").boolValue = false;
            R(a, "clingToFace").boolValue = false;
            R(a, "slowPlayerTo").floatValue = slowTo;
            R(a, "slowSeconds").floatValue = slowSeconds;
            R(a, "pushPower").floatValue = pushPower;
            R(a, "pushFarther").floatValue = pushFarther;
            R(a, "usedWhen").enumValueIndex = (int)SpiderBrain.When.Always;
        }

        // Wobbles like jelly whenever its health drops (the bugs' Jiggle, a little softer).
        private static void SetUpJiggle(Jiggle jiggle)
        {
            var so = new SerializedObject(jiggle);
            P(so, "jiggleWhenHurt").boolValue = true;
            P(so, "hurtStrength").floatValue = 1f;
            P(so, "squash").floatValue = 0.3f;
            P(so, "tilt").floatValue = 10f;
            P(so, "wobblesPerSecond").floatValue = 4.5f;
            P(so, "settleTime").floatValue = 0.8f;
            P(so, "around").objectReferenceValue = null;
            P(so, "healthFsm").stringValue = HealthFsmName;
            P(so, "healthVariable").stringValue = "health";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A tiny Health FSM: just the "health" Enemy Hit Damage takes the hits off and Spider Brain watches (Slime Body
        // plays the death).
        private static void AddHealthFsm(GameObject root, float health)
        {
            var component = root.AddComponent<PlayMakerFSM>();
            component.FsmName = HealthFsmName;
            var fsm = component.Fsm;
            fsm.Variables.FloatVariables = new[] { new FsmFloat("health") { Value = health } };
            fsm.Events = new[] { FsmEvent.GetFsmEvent("Damage"), FsmEvent.GetFsmEvent("ShakeSmall") };
            var alive = new FsmState(fsm)
            {
                Name = "Alive",
                Position = new Rect(60f, 120f, 120f, 32f),
                Actions = new FsmStateAction[0],
                Transitions = new FsmTransition[0],
            };
            fsm.States = new[] { alive };
            fsm.StartState = alive.Name;
            alive.SaveActions();
        }

        // ---- Helpers ----

        private static SerializedProperty P(SerializedObject so, string name) =>
            so.FindProperty(name) ?? throw new System.InvalidOperationException($"no field '{name}' on {so.targetObject.GetType().Name}");

        private static SerializedProperty R(SerializedProperty parent, string name) =>
            parent.FindPropertyRelative(name) ?? throw new System.InvalidOperationException($"no field '{parent.propertyPath}.{name}'");

        // These fields' values from another brain (e.g. the bee boss's bomb). Nothing if it's missing.
        private static void Copy(SerializedObject to, SpiderBrain from, params string[] names)
        {
            if (from == null) return;
            var source = new SerializedObject(from);
            foreach (var name in names) to.CopyFromSerializedProperty(P(source, name));
        }

        private static void SetList(SerializedProperty list, Object[] items)
        {
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static T GetOrAdd<T>(GameObject on) where T : Component =>
            on.TryGetComponent(out T found) ? found : on.AddComponent<T>();

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static SpiderBrain Brain(string path, List<string> notes)
        {
            var prefab = Load<GameObject>(path, notes);
            if (prefab != null && prefab.TryGetComponent(out SpiderBrain brain)) return brain;
            if (prefab != null) notes.Add($"WARNING: no Spider Brain on {path}");
            return null;
        }

        private static T Load<T>(string path, List<string> notes) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) notes.Add($"WARNING: missing {path}");
            return asset;
        }

        private static T[] LoadAll<T>(string[] paths, List<string> notes) where T : Object
        {
            var found = new List<T>();
            foreach (var path in paths)
            {
                var asset = Load<T>(path, notes);
                if (asset != null) found.Add(asset);
            }
            return found.ToArray();
        }

        private static Bounds Bounds(GameObject thing)
        {
            var renderers = thing.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(thing.transform.position, Vector3.one * 0.5f);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
