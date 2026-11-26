using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Combat
{
    /// <summary>
    /// Hides the Knight's armor and the legendary weapons each run (user 2026-11-26: "very random, sometime in the boxes,
    /// sometime in some furnitures, sometime even in chest that drop piece of seed"). Made by itself when the game starts
    /// (if Resources/Knight Armor Settings exists); nothing to put in a scene.
    /// - Every second it looks for breakable furniture and boxes (Enemy Hit Damage, Furniture) big enough to hide
    ///   something. When a room's set has all come (none new for Room Settles seconds), 1-2 of the things not found yet
    ///   go into random ones of them; a seed piece's chest (Knight Armor Chest) holds one now and then.
    /// - A hiding place glints now and then. Broken: the thing pops out with a puff (an armor piece floats there, touch it
    ///   to put it on; a legendary weapon drops to be picked up). Gone without breaking (the room cleared away, the set
    ///   swapped): the thing hides again in the next set.
    /// - A new run (or the save read): everything not found hides again, in a new random order.
    /// Also plays the armor's hit and break sounds, and adds the armor to the health HUD.
    /// </summary>
    public class KnightArmorHider : MonoBehaviour
    {
        private const string Tag = "[Knight Armor]";
        private const string WeaponPrefix = "Weapon:";

        /// <summary>The one in the game, or null.</summary>
        public static KnightArmorHider Instance { get; private set; }

        private KnightArmorSettings settings;
        private readonly List<string> queue = new();
        // Her lost things (story idea 8): one goes into each room's set, besides the armor.
        private readonly List<string> keepsakeQueue = new();
        private readonly Dictionary<Object, string> hidden = new();
        private readonly Dictionary<Object, float> nextGlint = new();
        private readonly HashSet<EnemyHitDamage> known = new();
        private readonly List<EnemyHitDamage> batch = new();
        private readonly List<Object> lost = new();
        private float lastNewAt;
        private float nextScan;
        private AudioSource voice;
        private bool hudAdded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make()
        {
            if (Instance != null || KnightArmorSettings.Instance == null) return;
            var go = new GameObject("Knight Armor Hider");
            DontDestroyOnLoad(go);
            go.AddComponent<KnightArmorHider>();
        }

        private void Awake()
        {
            Instance = this;
            settings = KnightArmorSettings.Instance;
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            Refill();
            EnemyHitDamage.Died += OnDied;
            KnightArmor.RunSet += Refill;
            KnightArmor.TookHit += OnArmorHit;
            KnightArmor.Broke += OnArmorBroke;
            KnightArmor.KeepsakeFound += OnKeepsakeFound;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EnemyHitDamage.Died -= OnDied;
            KnightArmor.RunSet -= Refill;
            KnightArmor.TookHit -= OnArmorHit;
            KnightArmor.Broke -= OnArmorBroke;
            KnightArmor.KeepsakeFound -= OnKeepsakeFound;
        }

        /// <summary>What is still hidden somewhere and where (for tests): "Helmet in Bedroom_Box_3".</summary>
        public string Report()
        {
            var parts = new List<string>();
            foreach (var pair in hidden) parts.Add(pair.Value + " in " + (pair.Key != null ? pair.Key.name : "-"));
            return $"queue [{string.Join(", ", queue)}] keepsakes [{string.Join(", ", keepsakeQueue)}] hidden [{string.Join(", ", parts)}]";
        }

        // Everything not found yet goes back into the queues, in a new random order.
        private void Refill()
        {
            hidden.Clear();
            nextGlint.Clear();
            queue.Clear();
            keepsakeQueue.Clear();
            batch.Clear();
            if (settings == null) return;
            foreach (var piece in settings.Pieces)
                if (piece != null && !string.IsNullOrEmpty(piece.id) && piece.pickup != null && !KnightArmor.Has(piece.id))
                    queue.Add(piece.id);
            for (int i = 0; i < settings.Weapons.Length; i++)
                if (settings.Weapons[i] != null) queue.Add(WeaponPrefix + i);
            for (int i = 0; i < settings.Keepsakes.Length; i++)
            {
                string id = KnightArmorSettings.KeepsakePrefix + i;
                if (settings.Keepsakes[i] != null && !KnightArmor.Has(id)) keepsakeQueue.Add(id);
            }
            Shuffle(queue);
            Shuffle(keepsakeQueue);
        }

        private static void Shuffle(List<string> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private bool IsKeepsake(string item) => item.StartsWith(KnightArmorSettings.KeepsakePrefix);

        private void Update()
        {
            if (settings == null) return;
            if (!hudAdded) AddHud();
            float now = Time.time;
            if (now >= nextScan)
            {
                nextScan = now + 1f;
                Scan(now);
            }
            if (batch.Count > 0 && now - lastNewAt >= settings.RoomSettles) CloseBatch();
            Glints(now);
        }

        // New breakable things, and hiding places gone without breaking.
        private void Scan(float now)
        {
            lost.Clear();
            foreach (var pair in hidden)
            {
                var spot = pair.Key;
                if (spot == null || (spot is Behaviour b && !b.isActiveAndEnabled)) lost.Add(spot);
            }
            foreach (var spot in lost)
            {
                // Back to hiding, first in its queue: the next set gets it.
                string item = hidden[spot];
                (IsKeepsake(item) ? keepsakeQueue : queue).Insert(0, item);
                hidden.Remove(spot);
                nextGlint.Remove(spot);
            }

            known.RemoveWhere(t => t == null);
            foreach (var thing in FindObjectsByType<EnemyHitDamage>(FindObjectsSortMode.None))
            {
                if (thing == null || known.Contains(thing)) continue;
                known.Add(thing);
                if (!CanHide(thing)) continue;
                batch.Add(thing);
                lastNewAt = now;
            }
        }

        private bool CanHide(EnemyHitDamage thing)
        {
            if (!thing.IsFurniture || thing.IsDead || thing.HealthLeft >= 1000f) return false;
            if (thing.GetComponentInParent<WeaponPower>() != null || thing.GetComponentInParent<KnightArmorChest>() != null) return false;
            string name = thing.name;
            foreach (var skip in settings.SkipNames)
                if (!string.IsNullOrEmpty(skip) && name.IndexOf(skip, System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (!Bounds(thing.gameObject, out var bounds)) return false;
            return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) >= settings.SmallestSpot;
        }

        // A room's set has all come: 1-2 of the things still hidden go into it.
        private void CloseBatch()
        {
            batch.RemoveAll(t => t == null || t.IsDead || !t.isActiveAndEnabled || hidden.ContainsKey(t));
            if (batch.Count >= settings.SmallestRoom)
            {
                int wanted = Random.Range(settings.PerRoom.x, settings.PerRoom.y + 1);
                int count = Mathf.Min(wanted, queue.Count, batch.Count);
                for (int i = 0; i < count; i++) Hide(TakeSpot(), Take(queue), batch.Count + 1);
                // And one of her lost things.
                if (keepsakeQueue.Count > 0 && batch.Count > 0) Hide(TakeSpot(), Take(keepsakeQueue), batch.Count + 1);
            }
            batch.Clear();
        }

        private EnemyHitDamage TakeSpot()
        {
            int pick = Random.Range(0, batch.Count);
            var spot = batch[pick];
            batch.RemoveAt(pick);
            return spot;
        }

        private static string Take(List<string> list)
        {
            string item = list[0];
            list.RemoveAt(0);
            return item;
        }

        private void Hide(Object spot, string item, int among)
        {
            hidden[spot] = item;
            nextGlint[spot] = Time.time + Random.Range(1f, settings.GlintEvery.y);
            Debug.Log($"{Tag} {item} hidden in {spot.name} ({among} places to choose from).");
        }

        /// <summary>A seed piece's chest came: now and then it holds one of the things still hidden.</summary>
        public void ChestAppeared(KnightArmorChest chest)
        {
            if (settings == null || chest == null || hidden.ContainsKey(chest) || queue.Count == 0) return;
            if (Random.value >= settings.ChestChance) return;
            Hide(chest, Take(queue), 1);
        }

        /// <summary>A seed piece's chest opened: what it holds pops out a moment later.</summary>
        public void ChestOpened(KnightArmorChest chest)
        {
            if (chest == null || !hidden.TryGetValue(chest, out string item)) return;
            hidden.Remove(chest);
            nextGlint.Remove(chest);
            StartCoroutine(RevealLater(item, chest.transform.position + Vector3.up * 0.6f, 0.8f));
        }

        private void OnDied(EnemyHitDamage thing)
        {
            if (thing == null || !hidden.TryGetValue(thing, out string item)) return;
            hidden.Remove(thing);
            nextGlint.Remove(thing);
            var at = Bounds(thing.gameObject, out var bounds) ? bounds.center : thing.transform.position + Vector3.up * 0.3f;
            Reveal(item, at);
        }

        private IEnumerator RevealLater(string item, Vector3 at, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Reveal(item, at);
        }

        // It pops out: an armor piece floats in reach, a legendary weapon drops.
        private void Reveal(string item, Vector3 at)
        {
            if (settings.FoundPuff != null) Destroy(Instantiate(settings.FoundPuff, at, Quaternion.identity), 4f);
            PlayAt(settings.FoundSound, at, settings.FoundVolume);
            if (item.StartsWith(WeaponPrefix))
            {
                int.TryParse(item.Substring(WeaponPrefix.Length), out int weapon);
                var gear = weapon >= 0 && weapon < settings.Weapons.Length ? settings.Weapons[weapon] : null;
                if (gear != null) Instantiate(gear, at + Vector3.up * 0.3f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                Debug.Log($"{Tag} {(gear != null ? gear.name : item)} found at {at}.");
                return;
            }
            GameObject prefab = null;
            if (IsKeepsake(item))
            {
                int.TryParse(item.Substring(KnightArmorSettings.KeepsakePrefix.Length), out int index);
                prefab = index >= 0 && index < settings.Keepsakes.Length ? settings.Keepsakes[index] : null;
            }
            else
            {
                var piece = settings.Find(item);
                prefab = piece != null ? piece.pickup : null;
            }
            if (prefab == null) return;
            // Chest high over the floor the player stands on (falling pieces of the broken thing don't count).
            var player = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            float floor = player != null ? player.transform.position.y
                : Physics.Raycast(at + Vector3.up * 0.2f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                    ? hit.point.y : at.y - 0.5f;
            var spot = new Vector3(at.x, floor + settings.FloatHeight, at.z);
            var made = Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            if (made.TryGetComponent(out KnightArmorPickup pickup)) pickup.Set(item);
            Debug.Log($"{Tag} {item} found at {spot}.");
        }

        // One of her lost things found: her happy sound; the last one: her thanks (her green shower, health back to full).
        private void OnKeepsakeFound(int count, int total)
        {
            var cam = Camera.main;
            var at = cam != null ? cam.transform.position : transform.position;
            if (count < total)
            {
                PlayAt(settings.KeepsakeSound, at, settings.KeepsakeVolume);
                return;
            }
            PlayAt(settings.AllKeepsakesSound, at, settings.AllKeepsakesVolume);
            Game.Story.StoryShower.Play(Game.Story.StorySeedSettings.Instance);
            Game.Saving.PlayerBoosts.HealToFull();
            Debug.Log($"{Tag} All {total} of her lost things found: her thanks.");
        }

        // The hint: now and then a glint on top of each hiding place.
        private void Glints(float now)
        {
            if (settings.Glint == null || nextGlint.Count == 0) return;
            lost.Clear();
            foreach (var pair in nextGlint)
                if (now >= pair.Value) lost.Add(pair.Key);
            foreach (var spot in lost)
            {
                nextGlint[spot] = now + Random.Range(settings.GlintEvery.x, settings.GlintEvery.y);
                if (spot == null) continue;
                var go = spot is Component c ? c.gameObject : null;
                if (go == null || !go.activeInHierarchy) continue;
                var top = Bounds(go, out var bounds) ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) : go.transform.position;
                var glint = Instantiate(settings.Glint, top, Quaternion.identity);
                glint.transform.localScale *= settings.GlintScale;
                Destroy(glint, 3f);
            }
        }

        private void OnArmorHit(float taken)
        {
            var clips = settings != null ? settings.HitSounds : null;
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) voice.PlayOneShot(clip, settings.HitVolume);
        }

        private void OnArmorBroke()
        {
            if (settings != null && settings.BreakSound != null) voice.PlayOneShot(settings.BreakSound, settings.BreakVolume);
        }

        /// <summary>A one-shot where something is found, heard across a room.</summary>
        public static void PlayAt(AudioClip clip, Vector3 at, float volume)
        {
            if (clip == null) return;
            var go = new GameObject("Knight Armor Sound: " + clip.name);
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            source.dopplerLevel = 0f;
            source.Play();
            Destroy(go, clip.length + 0.1f);
        }

        // The armor next to the health number, once the HUD is there.
        private void AddHud()
        {
            var hud = FindAnyObjectByType<PlayerHealthHUD>(FindObjectsInactive.Include);
            if (hud == null) return;
            hudAdded = true;
            if (hud.GetComponent<KnightArmorHUD>() == null) hud.gameObject.AddComponent<KnightArmorHUD>();
        }

        private static bool Bounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var look in go.GetComponentsInChildren<Renderer>())
            {
                if (!look.enabled || look is ParticleSystemRenderer) continue;
                if (!any)
                {
                    bounds = look.bounds;
                    any = true;
                }
                else bounds.Encapsulate(look.bounds);
            }
            return any;
        }
    }
}
