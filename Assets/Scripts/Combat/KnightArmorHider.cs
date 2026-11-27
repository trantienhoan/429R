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
    ///   something. When a room's set has all come (none new for Room Settles seconds), it may hide one armor piece not
    ///   found yet (Room Chance) and, rarer, one legendary weapon (Weapon Room Chance) in random ones of them; a seed
    ///   piece's chest (Knight Armor Chest) holds an armor piece now and then (Chest Chance). User 2026-11-26: "make them
    ///   really rare" (it was 1-2 every room: all four in the first rooms).
    /// - A secret room's giant pumpkin holds one far more often (Secret Room Chance; user 2026-11-27: "make them rarely
    ///   dropped, increase chance of obtains them when break stuffs in secret rooms"). Broken: it pops out with the rest.
    /// - One armor piece per room at most, whatever hides it (its sets, its seed chest, one lost earlier); a secret room is
    ///   a room of its own (user 2026-11-27: "1 armour piece can find per room. secret room counts as another room"). A
    ///   room starts at the GAMESTAGES states in Room Starts. The legendary weapons keep their own chance.
    /// - A hiding place glints now and then. Broken: the thing pops out with a puff (an armor piece floats there, grab it
    ///   and eat it like a candy; a legendary weapon drops to be picked up). Gone without breaking (the room cleared away,
    ///   the set swapped), or a found armor piece gone uneaten: it hides again in the next set, no chance roll.
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
        // The armor pieces and the legendary weapons not found yet (each set may take one of each, by chance).
        private readonly List<string> queue = new();
        private readonly List<string> weaponQueue = new();
        // Things that were hidden (or found) and lost again: the next set takes them all, no chance roll.
        private readonly List<string> carryOver = new();
        // Her lost things (story idea 8): one goes into each room's set, besides the armor.
        private readonly List<string> keepsakeQueue = new();
        private readonly Dictionary<Object, string> hidden = new();
        private readonly Dictionary<Object, float> nextGlint = new();
        private readonly HashSet<EnemyHitDamage> known = new();
        private readonly List<EnemyHitDamage> batch = new();
        private readonly List<Object> lost = new();
        // Hiding places whose glint shows on something else (a secret room's giant pumpkin).
        private readonly Dictionary<Object, Transform> glintOn = new();
        // One armor piece per room (Pieces Per Room): how many this room has had, and where (a piece whose hiding place
        // goes unbroken in the same room, a set swapped, may hide again in it). A new room starts at a GAMESTAGES state
        // named in Room Starts.
        private int roomPieces;
        private readonly HashSet<Object> roomSpots = new();
        private PlayMakerFSM stages;
        private string lastState;
        private float nextStagesLook;
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
            return $"queue [{string.Join(", ", queue)}] weapons [{string.Join(", ", weaponQueue)}] carry [{string.Join(", ", carryOver)}] " +
                   $"keepsakes [{string.Join(", ", keepsakeQueue)}] hidden [{string.Join(", ", parts)}] room pieces {roomPieces} ({lastState})";
        }

        // Everything not found yet goes back into the queues, in a new random order.
        private void Refill()
        {
            hidden.Clear();
            nextGlint.Clear();
            glintOn.Clear();
            queue.Clear();
            weaponQueue.Clear();
            carryOver.Clear();
            keepsakeQueue.Clear();
            batch.Clear();
            roomPieces = 0;
            roomSpots.Clear();
            if (settings == null) return;
            foreach (var piece in settings.Pieces)
                if (piece != null && !string.IsNullOrEmpty(piece.id) && piece.pickup != null && !KnightArmor.Has(piece.id))
                    queue.Add(piece.id);
            for (int i = 0; i < settings.Weapons.Length; i++)
                if (settings.Weapons[i] != null) weaponQueue.Add(WeaponPrefix + i);
            for (int i = 0; i < settings.Keepsakes.Length; i++)
            {
                string id = KnightArmorSettings.KeepsakePrefix + i;
                if (settings.Keepsakes[i] != null && !KnightArmor.Has(id)) keepsakeQueue.Add(id);
            }
            Shuffle(queue);
            Shuffle(weaponQueue);
            Shuffle(keepsakeQueue);
        }

        /// <summary>
        /// A found armor piece went without being eaten (the room cleared away, the purple hole): it hides again in the
        /// next set. Called by the Knight Armor Pickup.
        /// </summary>
        public void Lost(string item)
        {
            if (settings == null || string.IsNullOrEmpty(item) || KnightArmor.Has(item)) return;
            if (queue.Contains(item) || carryOver.Contains(item) || keepsakeQueue.Contains(item) || hidden.ContainsValue(item)) return;
            if (IsKeepsake(item)) keepsakeQueue.Insert(0, item);
            else carryOver.Add(item);
            Debug.Log($"{Tag} {item} went uneaten: it hides again in the next room.");
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

        // An armor piece (not a legendary weapon, not one of her keepsakes): one per room.
        private bool IsArmor(string item) => !item.StartsWith(WeaponPrefix) && !IsKeepsake(item);

        private bool RoomHasSpace => roomPieces < settings.PiecesPerRoom;

        // Hides it; an armor piece takes one of the room's places.
        private void HideInRoom(Object spot, string item, int among)
        {
            Hide(spot, item, among);
            if (!IsArmor(item)) return;
            roomPieces++;
            roomSpots.Add(spot);
        }

        // A new room (user 2026-11-27: "1 armour piece can find per room"): the GAMESTAGES state just entered starts with
        // one of Room Starts (the clean-ups between rooms, the rooftop coming, a checkpoint, a new run).
        private void WatchRoom()
        {
            if (stages == null)
            {
                if (Time.unscaledTime < nextStagesLook) return;
                nextStagesLook = Time.unscaledTime + 1f;
                stages = Game.GameStages.Fsm();
                if (stages == null) return;
            }
            string state = stages.ActiveStateName;
            if (state == lastState) return;
            lastState = state;
            if (string.IsNullOrEmpty(state) || settings.RoomStarts == null) return;
            foreach (var start in settings.RoomStarts)
            {
                if (string.IsNullOrEmpty(start) || !state.StartsWith(start, System.StringComparison.Ordinal)) continue;
                roomPieces = 0;
                roomSpots.Clear();
                return;
            }
        }

        private void Update()
        {
            if (settings == null) return;
            if (!hudAdded) AddHud();
            WatchRoom();
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
                // Back to hiding: the next set gets it for sure (it won its chance already).
                string item = hidden[spot];
                if (IsKeepsake(item)) keepsakeQueue.Insert(0, item);
                else carryOver.Add(item);
                // Gone in the room it hid in (a set swapped): it may hide again in this room, still its one piece.
                if (roomSpots.Remove(spot) && IsArmor(item)) roomPieces = Mathf.Max(0, roomPieces - 1);
                hidden.Remove(spot);
                nextGlint.Remove(spot);
                glintOn.Remove(spot);
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

        // A room's set has all come: what was lost hides in it again, then by chance one armor piece and, rarer, one
        // legendary weapon.
        private void CloseBatch()
        {
            batch.RemoveAll(t => t == null || t.IsDead || !t.isActiveAndEnabled || hidden.ContainsKey(t));
            // Pieces already worn (eaten some other way) don't hide again.
            queue.RemoveAll(KnightArmor.Has);
            carryOver.RemoveAll(KnightArmor.Has);
            if (batch.Count >= settings.SmallestRoom)
            {
                // What was lost hides first, no roll (a weapon always; an armor piece while the room has space), then by
                // chance a new armor piece, if the room hasn't had its one yet.
                for (int i = 0; i < carryOver.Count && batch.Count > 0;)
                {
                    string item = carryOver[i];
                    if (IsArmor(item) && !RoomHasSpace)
                    {
                        i++;
                        continue;
                    }
                    carryOver.RemoveAt(i);
                    HideInRoom(TakeSpot(), item, batch.Count + 1);
                }
                if (queue.Count > 0 && batch.Count > 0 && RoomHasSpace && Random.value < settings.RoomChance)
                    HideInRoom(TakeSpot(), Take(queue), batch.Count + 1);
                if (weaponQueue.Count > 0 && batch.Count > 0 && Random.value < settings.WeaponRoomChance)
                    Hide(TakeSpot(), Take(weaponQueue), batch.Count + 1);
                // And one of her lost things (none while the list is empty).
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

        /// <summary>
        /// A secret room's giant pumpkin came (user 2026-11-27: "make them rarely dropped, increase chance of obtains them
        /// when break stuffs in secret rooms"): it holds an armor piece not found yet, by Secret Room Chance (a room's
        /// furniture only by Room Chance). It glints on the pumpkin. Called by the Secret Room.
        /// </summary>
        public void SecretPumpkinUp(Object room, Transform pumpkin)
        {
            if (settings == null || room == null || pumpkin == null || hidden.ContainsKey(room)) return;
            queue.RemoveAll(KnightArmor.Has);
            carryOver.RemoveAll(KnightArmor.Has);
            // A room of its own (user 2026-11-27: "secret room counts as another room"): its one piece, not the host
            // room's. One lost earlier hides here with no roll, like in any room; else by Secret Room Chance.
            int lostOne = carryOver.FindIndex(IsArmor);
            string item;
            if (lostOne >= 0)
            {
                item = carryOver[lostOne];
                carryOver.RemoveAt(lostOne);
            }
            else if (queue.Count > 0 && Random.value < settings.SecretRoomChance) item = Take(queue);
            else return;
            Hide(room, item, 1);
            glintOn[room] = pumpkin;
            Debug.Log($"{Tag} ... in the secret room's giant pumpkin ({pumpkin.name}).");
        }

        /// <summary>The secret room's giant pumpkin was broken: what it held pops out.</summary>
        public void SecretPumpkinBroke(Object room, Vector3 at)
        {
            if (room == null || !hidden.TryGetValue(room, out string item)) return;
            hidden.Remove(room);
            nextGlint.Remove(room);
            glintOn.Remove(room);
            Reveal(item, at);
        }

        /// <summary>The secret room's giant pumpkin went unbroken: what it held hides again in the next set.</summary>
        public void SecretPumpkinGone(Object room)
        {
            if (room == null || !hidden.TryGetValue(room, out string item)) return;
            hidden.Remove(room);
            nextGlint.Remove(room);
            glintOn.Remove(room);
            carryOver.Add(item);
            Debug.Log($"{Tag} {item} went with the unbroken giant pumpkin: it hides again in the next room.");
        }

        /// <summary>A seed piece's chest came: now and then it holds one of the things still hidden.</summary>
        public void ChestAppeared(KnightArmorChest chest)
        {
            if (settings == null || chest == null || hidden.ContainsKey(chest) || queue.Count == 0) return;
            // Its room's one piece (it comes at the end of the room's fight), if the room hasn't had it.
            if (!RoomHasSpace || Random.value >= settings.ChestChance) return;
            HideInRoom(chest, Take(queue), 1);
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
                var go = glintOn.TryGetValue(spot, out var on) ? (on != null ? on.gameObject : null)
                    : spot is Component c ? c.gameObject : null;
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
