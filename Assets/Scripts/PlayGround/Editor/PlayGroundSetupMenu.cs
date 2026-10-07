using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Game.Inventory;
using Game.PlayGround;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.EditorTools
{
    /// <summary>Menu commands that set up the PlayGround's cooking game (Tools > 429 Game > PlayGround).</summary>
    public static class PlayGroundSetupMenu
    {
        private const string PlayGroundFolder = "Assets/Prefabs/PlayGround/";
        private const string CauldronPath = PlayGroundFolder + "Fire_Woods_n_Cauldron.prefab";
        private const string LollipopPath = PlayGroundFolder + "Lolipop_{0}.prefab";
        private const string FuelPath = PlayGroundFolder + "Dropped_Wood_Fuel.prefab";
        private const string TreePath = PlayGroundFolder + "Big_Tree_01_04.prefab";
        private const string BushPath = PlayGroundFolder + "bushes_01.prefab";
        private const string HatPath = PlayGroundFolder + "Recipe_Hat.prefab";
        private const string CardPath = PlayGroundFolder + "Recipe_Card.prefab";
        private const string BlackPumpkinPath = PlayGroundFolder + "pumpkin_black.prefab";
        private const string SkullPath = PlayGroundFolder + "Giant_Skull.prefab";
        private const string SkullBushPath = PlayGroundFolder + "bushes_01_Skull.prefab";
        private const string PumpkinOnTreePath = "Assets/Prefabs/Items/Breakable/Pumpkin_On_Tree.prefab";
        private const string ResultPath = "Assets/Prefabs/Items/Breakable/Pumpkin_{0}.prefab";
        private const string GhostPath = "Assets/Prefabs/Enemies/PLAYGROUND/Ghost_Balloon_{0}.prefab";
        private const string IconPath = "Assets/Data/Cooking/Icons/{0}.png";
        private const string CandyPath = "Assets/Data/Inventory/Items/Candy.asset";
        private const string SmokePath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        private static readonly string[] BossPaths =
        {
            "Assets/Prefabs/Enemies/TheSpider.prefab", "Assets/Prefabs/Enemies/TheSpider 1.prefab",
            "Assets/Prefabs/Enemies/TheCockroach.prefab", "Assets/Prefabs/Enemies/TheCockroach 1.prefab",
            "Assets/Prefabs/Enemies/TheBee.prefab", "Assets/Prefabs/Enemies/TheBee 1.prefab",
        };

        /// <summary>
        /// Sets up the cauldron, the seven lollipops, Pumpkin_On_Tree, the firewood, the ghost tree and bush, the giant
        /// skull, the black pumpkin, the recipe hat (Recipe_Hat, with its Recipe_Card), and gives the ghost balloons
        /// their lollipop prizes. Run it again any time: settings already made are kept, only missing parts are added.
        /// Save and close those prefabs first.
        /// </summary>
        [MenuItem("Tools/429 Game/PlayGround/Set Up Cooking Game")]
        public static void SetUpCookingGame()
        {
            var lollipops = new List<GameObject>();
            for (int n = 1; n <= 7; n++)
            {
                var lollipop = Load<GameObject>(string.Format(LollipopPath, n));
                if (lollipop != null) lollipops.Add(lollipop);
            }
            var ghosts = LoadGhosts();
            var smoke = Load<GameObject>(SmokePath);
            var notes = new List<string>();

            // Ingredients first, so the cauldron's recipe can use their ids and pictures.
            for (int n = 1; n <= 7; n++)
            {
                int number = n;
                Edit(string.Format(LollipopPath, n), notes, root =>
                {
                    var ingredient = GetOrAdd<CookingIngredient>(root, out bool addedIngredient);
                    if (addedIngredient) SetIngredient(ingredient, $"Lolipop_{number}", $"Lolipop_{number}");
                    var suck = GetOrAdd<SuckToEat>(root, out bool addedSuck);
                    if (addedSuck) Set(suck, "reward", Load<ItemDefinition>(CandyPath));
                });
            }
            Edit(PumpkinOnTreePath, notes, root =>
            {
                var ingredient = GetOrAdd<CookingIngredient>(root, out bool added);
                if (added) SetIngredient(ingredient, "Pumpkin_On_Tree", "Pumpkin_On_Tree");
            });

            Edit(FuelPath, notes, root =>
            {
                if (!root.TryGetComponent<Rigidbody>(out _)) root.AddComponent<Rigidbody>();
                if (root.GetComponentInChildren<Collider>() == null) FitBox(root);
                if (!root.TryGetComponent<XRGrabInteractable>(out var grab))
                {
                    grab = root.AddComponent<XRGrabInteractable>();
                    // Like the lollipops and weapons: follows the hand by velocity, so it can be thrown.
                    grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                    grab.useDynamicAttach = true;
                    grab.throwVelocityScale = 1.5f;
                }
                GetOrAdd<FireFuel>(root, out _);
            });

            var bushPrefab = Load<GameObject>(BushPath);
            var treePrefab = Load<GameObject>(TreePath);
            Edit(TreePath, notes, root =>
            {
                var tree = GetOrAdd<GhostTree>(root, out bool added);
                if (added)
                {
                    SetArray(tree, "ghosts", ghosts);
                    Set(tree, "fuel", Load<GameObject>(FuelPath));
                    Set(tree, "leftBehind", bushPrefab);
                    SetArray(tree, "hitSounds", new List<Object> { Load<AudioClip>(Sfx + "wood_get_hit_1.wav"), Load<AudioClip>(Sfx + "wood_get_hit_2.wav") });
                    Set(tree, "fallSound", Load<AudioClip>(Sfx + "wood_furniture_break_complete.wav"));
                    Set(tree, "fallEffect", smoke);
                }
                ScriptJiggle(root);
            });
            Edit(BushPath, notes, root =>
            {
                var bush = GetOrAdd<GhostBush>(root, out bool added);
                if (added)
                {
                    SetArray(bush, "ghosts", ghosts);
                    Set(bush, "tree", treePrefab);
                    Set(bush, "hitSound", Load<AudioClip>(Sfx + "wood_get_hit_2.wav"));
                    Set(bush, "growSound", Load<AudioClip>(Sfx + "bubble_pop_1.wav"));
                    Set(bush, "growEffect", smoke);
                }
                ScriptJiggle(root);
            });
            SetUpGiantSkull(notes, ghosts, smoke);

            for (int n = 1; n <= 3; n++)
            {
                Edit(string.Format(GhostPath, n), notes, root =>
                {
                    if (!root.TryGetComponent<GhostBalloon>(out var balloon)) return;
                    var serialized = new SerializedObject(balloon);
                    if (serialized.FindProperty("lollipops").arraySize > 0) return;
                    SetArray(balloon, "lollipops", lollipops);
                });
            }

            Edit(BlackPumpkinPath, notes, root =>
            {
                var pumpkin = GetOrAdd<BlackPumpkin>(root, out bool added);
                if (added)
                {
                    var bosses = new List<Object>();
                    foreach (var path in BossPaths) bosses.Add(Load<GameObject>(path));
                    SetArray(pumpkin, "bosses", bosses);
                    var cackles = new List<Object>();
                    for (int n = 1; n <= 4; n++) cackles.Add(Load<AudioClip>($"{Sfx}pumpkin_laugh_{n}.wav"));
                    SetArray(pumpkin, "cackles", cackles);
                    SetArray(pumpkin, "burstSounds", new List<Object> { Load<AudioClip>(Sfx + "pumpkin_explode_1.wav"), Load<AudioClip>(Sfx + "pumpkin_explode_2.wav") });
                    Set(pumpkin, "burstEffect", smoke);
                }
                // It falls and bounces, which needs a body and a convex collider.
                if (!root.TryGetComponent<Rigidbody>(out _)) root.AddComponent<Rigidbody>();
                foreach (var shape in root.GetComponentsInChildren<MeshCollider>(true)) shape.convex = true;
                if (root.GetComponentInChildren<Collider>() == null) FitBox(root);
            });

            Edit(HatPath, notes, SetUpHat);

            Edit(CauldronPath, notes, root =>
            {
                var cauldron = GetOrAdd<Cauldron>(root, out bool added);
                SetIfEmpty(cauldron, "badPumpkin", Load<GameObject>(BlackPumpkinPath));
                SetIfEmpty(cauldron, "addEffect", smoke);
                SetRoundResults(cauldron);
                if (!added) return;

                Set(cauldron, "pot", Find(root, "Magical_Pot"));
                var surface = Find(root, "Cauldron_Surface");
                Set(cauldron, "surface", surface != null ? surface.GetComponent<ParticleSystem>() : null);
                var fire = Find(root, "Fire");
                Set(cauldron, "fire", fire != null ? fire.GetComponent<ParticleSystem>() : null);
                SetArray(cauldron, "lollipops", lollipops);
                Set(cauldron, "pumpkin", Load<GameObject>(PumpkinOnTreePath));
                var laughs = new List<Object>();
                for (int n = 1; n <= 5; n++) laughs.Add(Load<AudioClip>($"{Sfx}laughing_pumpkin_{n}.wav"));
                SetArray(cauldron, "laughs", laughs);
                Set(cauldron, "addSound", Load<AudioClip>(Sfx + "Bug_Pop.wav"));
                Set(cauldron, "fuelSound", Load<AudioClip>("Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/thrusterFire_000.ogg"));
                Set(cauldron, "successSound", Load<AudioClip>(Sfx + "shop_buy_success.wav"));
                Set(cauldron, "failSound", Load<AudioClip>(Sfx + "shop_buy_fail.wav"));
                Set(cauldron, "doneEffect", smoke);
                Set(cauldron, "font", SetupUtility.ResolveFont());
            });

            Debug.Log("[PlayGround] " + string.Join("; ", notes) + ". Put Recipe_Hat on the table near the cauldron, and give " +
                      "the PlayGround a Ghost Roam Area (Add Component > Ghost Roam Area) so the ghost balloons roam it.");
        }

        /// <summary>
        /// Makes Giant_Skull act like the ghost tree: ghost balloons float out of it every so often, it jiggles when hit,
        /// and when its Health FSM breaks it, it leaves a bush behind (bushes_01_Skull, made here as a variant of
        /// bushes_01) that pops back up into the skull when nobody's watching. Save and close Giant_Skull first.
        /// </summary>
        [MenuItem("Tools/429 Game/PlayGround/Set Up Giant Skull")]
        public static void SetUpGiantSkullMenu()
        {
            var notes = new List<string>();
            SetUpGiantSkull(notes, LoadGhosts(), Load<GameObject>(SmokePath));
            Debug.Log("[PlayGround] " + string.Join("; ", notes) + ".");
        }

        private static void SetUpGiantSkull(List<string> notes, List<GameObject> ghosts, GameObject smoke)
        {
            var skullPrefab = Load<GameObject>(SkullPath);
            var bushPrefab = Load<GameObject>(BushPath);
            if (skullPrefab == null || bushPrefab == null || !bushPrefab.TryGetComponent<GhostBush>(out _))
            {
                notes.Add(skullPrefab == null ? $"no '{SkullPath}'" : "set up bushes_01 first (Set Up Cooking Game)");
                return;
            }

            // The bush it leaves: bushes_01, growing back into the skull instead of the tree.
            if (Load<GameObject>(SkullBushPath) == null)
            {
                MakeVariant(bushPrefab, SkullBushPath, notes, root => Set(root.GetComponent<GhostBush>(), "tree", skullPrefab));
            }

            Edit(SkullPath, notes, root =>
            {
                var skull = GetOrAdd<GhostTree>(root, out bool added);
                if (added)
                {
                    SetArray(skull, "ghosts", ghosts);
                    // Its Damage and Health FSMs make its hit and break sounds, and its pieces drop the pumpkins, so it
                    // gets no firewood or break sound here; these hit sounds only play if those FSMs are removed.
                    SetArray(skull, "hitSounds", new List<Object> { Load<AudioClip>(Sfx + "bone_bonk_1.wav"), Load<AudioClip>(Sfx + "bone_bonk_2.wav") });
                    Set(skull, "fallEffect", smoke);
                }
                SetIfEmpty(skull, "leftBehind", Load<GameObject>(SkullBushPath));
                ScriptJiggle(root);
            });
        }

        /// <summary>Makes the selected object or prefab a magic hat that spits out the recipe card.</summary>
        [MenuItem("Tools/429 Game/PlayGround/Make Selected Magic Hat")]
        public static void MakeSelectedMagicHat()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            if (EditorUtility.IsPersistent(go))
            {
                var notes = new List<string>();
                Edit(AssetDatabase.GetAssetPath(go), notes, SetUpHat);
                Debug.Log("[PlayGround] " + string.Join("; ", notes) + ".");
            }
            else
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Make Magic Hat");
                SetUpHat(go);
                Debug.Log($"[PlayGround] '{go.name}' is now a magic hat. Save the scene or apply it to its prefab.");
            }
        }

        [MenuItem("Tools/429 Game/PlayGround/Make Selected Magic Hat", true)]
        private static bool CanMakeSelectedMagicHat()
        {
            return Selection.activeGameObject != null;
        }

        private static void SetUpHat(GameObject root)
        {
            var hat = GetOrAdd<MagicHat>(root, out bool added);
            if (root.GetComponentInChildren<Collider>() == null) FitBox(root);
            SetIfEmpty(hat, "card", Load<GameObject>(CardPath));
            if (!added) return;
            Set(hat, "font", SetupUtility.ResolveFont());
            Set(hat, "spitSound", Load<AudioClip>(Sfx + "A_gentle_miracle_sound_1.wav"));
            Set(hat, "backSound", Load<AudioClip>(Sfx + "bubble_pop_1.wav"));
            Set(hat, "shakeSound", Load<AudioClip>(Sfx + "creepy_laugh_1.wav"));
        }

        // The tree and bush jiggle when hit, told by their own script (not a Health FSM).
        private static void ScriptJiggle(GameObject root)
        {
            var jiggle = GetOrAdd<DamageJiggle>(root, out bool added);
            if (!added) return;
            var serialized = new SerializedObject(jiggle);
            serialized.FindProperty("healthFsm").stringValue = "";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIngredient(CookingIngredient ingredient, string id, string iconName)
        {
            var serialized = new SerializedObject(ingredient);
            serialized.FindProperty("ingredientId").stringValue = id;
            serialized.FindProperty("icon").objectReferenceValue = Load<Sprite>(string.Format(IconPath, iconName));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // A box around everything it shows.
        private static void FitBox(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                root.AddComponent<BoxCollider>();
                return;
            }
            var bounds = new Bounds(root.transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (var r in renderers)
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    bounds.Encapsulate(root.transform.InverseTransformPoint(corner));
                }
            }
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        private static List<GameObject> LoadGhosts()
        {
            var ghosts = new List<GameObject>();
            for (int n = 1; n <= 3; n++)
            {
                var ghost = Load<GameObject>(string.Format(GhostPath, n));
                if (ghost != null) ghosts.Add(ghost);
            }
            return ghosts;
        }

        // Saves a Prefab Variant of 'source' at 'path', changed by 'change'. Built in a preview scene, so the open scene
        // isn't touched.
        private static void MakeVariant(GameObject source, string path, List<string> notes, System.Action<GameObject> change)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                change(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                notes.Add(System.IO.Path.GetFileNameWithoutExtension(path) + " (new)");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Edit(string path, List<string> notes, System.Action<GameObject> change)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add($"no '{path}'");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                change(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.Add(root.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T GetOrAdd<T>(GameObject root, out bool added) where T : Component
        {
            added = !root.TryGetComponent<T>(out var component);
            if (added) component = root.AddComponent<T>();
            return component;
        }

        private static Transform Find(GameObject root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName) return t;
            }
            return null;
        }

        private static T Load<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void Set(Object target, string field, Object value)
        {
            if (value == null) return;
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Rounds with no result yet make Pumpkin_8, the last one Pumpkin_9.
        private static void SetRoundResults(Cauldron cauldron)
        {
            var serialized = new SerializedObject(cauldron);
            var rounds = serialized.FindProperty("rounds");
            if (rounds == null) return;
            for (int i = 0; i < rounds.arraySize; i++)
            {
                var result = rounds.GetArrayElementAtIndex(i).FindPropertyRelative("result");
                if (result.objectReferenceValue != null) continue;
                bool last = i == rounds.arraySize - 1;
                result.objectReferenceValue = Load<GameObject>(string.Format(ResultPath, last ? 9 : 8));
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Fills in a field only if it's empty, so a choice made in the Inspector stays.
        private static void SetIfEmpty(Object target, string field, Object value)
        {
            if (value == null) return;
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null || property.objectReferenceValue != null) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(Object target, string field, List<T> values) where T : Object
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) return;
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
