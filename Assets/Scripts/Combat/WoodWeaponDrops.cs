using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Wooden furniture drops wooden weapons (user 2026-11-27: "check furnitures in the game, make them wood furniture have
    /// high chance of dropping wooden weapon like bat, broom..."). When a breakable piece of furniture (Enemy Hit Damage,
    /// Furniture) made of wood breaks (its main material is a wood one: the beds, stools, coffers, shelves, tables, book
    /// shelves, the clock tower, wardrobes, benches and their lair copies; never the metal versions, the sofas, doors,
    /// chests, barrels, webs or lights), Chance of it drops a wooden weapon (the bat, the broom, the tool hammer, by
    /// weight) where it stood, with a small puff, unless Most About of the weapons it dropped are still about (lying,
    /// held, not yet broken or eaten by the purple hole). Made by itself when the game starts (if Resources/Wood Weapon
    /// Drop Settings exists); nothing to put in a scene.
    /// </summary>
    public class WoodWeaponDrops : MonoBehaviour
    {
        private const string Tag = "[Wood Weapon Drops]";

        /// <summary>The one in the game, or null.</summary>
        public static WoodWeaponDrops Instance { get; private set; }

        /// <summary>How many weapons it has dropped since the game started (for tests).</summary>
        public static int Dropped { get; private set; }

        /// <summary>For tests: a chance to use instead of the settings' (below 0: the settings').</summary>
        public static float TestChance { get; set; } = -1f;

        /// <summary>Its weapons still about (for tests).</summary>
        public int About
        {
            get
            {
                about.RemoveAll(w => w == null);
                return about.Count;
            }
        }

        private WoodWeaponDropSettings settings;
        private readonly List<GameObject> about = new List<GameObject>();
        // Which furniture is wooden, by name (without "(Clone)").
        private readonly Dictionary<string, bool> wooden = new Dictionary<string, bool>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Dropped = 0;
            TestChance = -1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make()
        {
            if (Instance != null || WoodWeaponDropSettings.Instance == null) return;
            var go = new GameObject("Wood Weapon Drops");
            DontDestroyOnLoad(go);
            go.AddComponent<WoodWeaponDrops>();
        }

        private void Awake()
        {
            Instance = this;
            settings = WoodWeaponDropSettings.Instance;
        }

        private void OnEnable() => EnemyHitDamage.Died += OnDied;

        private void OnDisable() => EnemyHitDamage.Died -= OnDied;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Whether this furniture is wooden (its main material is a wood one, its name not a skipped one).</summary>
        public bool IsWooden(EnemyHitDamage thing)
        {
            if (thing == null || settings == null || !thing.IsFurniture) return false;
            string name = thing.name.Replace("(Clone)", "").Trim();
            if (wooden.TryGetValue(name, out bool known)) return known;
            bool result = Judge(thing.gameObject, name);
            wooden[name] = result;
            return result;
        }

        private bool Judge(GameObject thing, string name)
        {
            foreach (var skip in settings.SkipNames)
                if (!string.IsNullOrEmpty(skip) && name.IndexOf(skip, System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            foreach (var r in thing.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var m = r.sharedMaterial;
                if (m == null) continue;
                foreach (var word in settings.WoodWords)
                    if (!string.IsNullOrEmpty(word) && m.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            }
            return false;
        }

        private void OnDied(EnemyHitDamage thing)
        {
            if (settings == null || !IsWooden(thing)) return;
            if (About >= settings.MostAbout) return;
            if (Random.value >= (TestChance >= 0f ? TestChance : settings.Chance)) return;
            var prefab = settings.Pick();
            if (prefab == null) return;
            var at = Middle(thing.gameObject);
            // Breaking over the drop (a Fall Edge took it): nothing to drop there.
            if (Game.Locomotion.FallEdge.Covers(at)) return;
            var weapon = Instantiate(prefab, at + Vector3.up * 0.25f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            weapon.name = prefab.name;
            about.Add(weapon);
            Dropped++;
            if (settings.Puff != null)
            {
                var puff = Instantiate(settings.Puff, at, Quaternion.identity);
                puff.transform.localScale *= settings.PuffScale;
                Destroy(puff, 4f);
            }
            Debug.Log($"{Tag} A {prefab.name} fell out of the broken {thing.name.Replace("(Clone)", "").Trim()} ({About} about).");
        }

        // The middle of what it looked like (falling pieces of the broken thing are elsewhere by now, or not yet made).
        private static Vector3 Middle(GameObject thing)
        {
            bool any = false;
            var box = new Bounds();
            foreach (var r in thing.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any)
                {
                    box = r.bounds;
                    any = true;
                }
                else box.Encapsulate(r.bounds);
            }
            return any ? box.center : thing.transform.position + Vector3.up * 0.4f;
        }
    }
}
