using System;
using System.Collections.Generic;
using Game.Inventory;
using Game.Saving;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Shopping
{
    public enum PurchaseResult
    {
        Bought,
        NotEnoughCurrency,
        SoldOut,
        Unavailable,
    }

    /// <summary>
    /// The candy shop. By default its panel shows on your right hand while B on the right controller is held
    /// (the right menu button on controllers without B), like the inventory on the left hand. It sells a few random
    /// items from its catalog at a time and picks new ones every 42 seconds (the panel counts down); you buy one by
    /// clicking its row with your left hand. Below them is the special line, an extra slot picked again at every restock:
    /// sometimes a legendary special (rare, less so while the pumpkin boss is fought), otherwise another normal item or
    /// special; each offer can be bought once. That line is locked until it's paid for once (15 Yellow Seed by
    /// default); once it's open, a legendary offer buzzes the shop hand twice. Bought items pop out of the panel and
    /// fall to the floor. FSMs use the actions in PlayMaker's "Shop" category.
    /// </summary>
    [DisallowMultipleComponent]
    public class Shop : MonoBehaviour
    {
        public enum OpenMode
        {
            HoldButton,
            ToggleButton,
            OnlyFromFsm,
        }

        /// <summary>How many of one item were bought, as written to a save file.</summary>
        [Serializable]
        public class SavedPurchase
        {
            public string id;
            public int count;
        }

        [SerializeField] private ShopCatalog catalog;
        [SerializeField] private string shopName = "Candy Shop";
        [Tooltip("Left empty, the first shop panel in the scene is used.")]
        [SerializeField] private ShopPanelUI panel;

        [Header("Stock")]
        [Tooltip("How many items from the catalog are on sale at once, picked at random. 0 = everything, always. " +
                 "The panel is made to fit this many; after changing it, run Tools > 429 Game > Shop > Rebuild Shop Panel.")]
        [Min(0)]
        [SerializeField] private int itemsOnSale = 4;
        [Tooltip("New items go on sale every this many seconds of play (the clock stops while paused). The panel " +
                 "counts down to it, and changes while open; clicks just after a change are ignored.")]
        [Min(1f)]
        [SerializeField] private float restockSeconds = 42f;

        [Header("Opening")]
        [Tooltip("Hold Button: open while held. Toggle Button: press to open, press again to close. Only From FSM: use the Shop Open / Shop Close actions.")]
        [SerializeField] private OpenMode openMode = OpenMode.HoldButton;
        [Tooltip("Defaults to B on the right controller.")]
        [SerializeField] private InputActionProperty openButton = new(new InputAction("Open Shop", InputActionType.Button, "<XRController>{RightHand}/secondaryButton"));
        [Tooltip("Also opens the shop, for controllers without B (HTC Vive wands, Windows Mixed Reality, basic controllers). " +
                 "Defaults to the menu button on the right controller; the left one pauses.")]
        [SerializeField] private InputActionProperty alternateOpenButton = new(new InputAction("Open Shop (No B Button)", InputActionType.Button, "<XRController>{RightHand}/menuButton"));

        [Header("Placement")]
        [Tooltip("The hand the panel rides on. Left empty, the Right Controller is found automatically. " +
                 "To change where the panel sits, move 'Shop Panel' under that hand, like the Wrist Inventory.")]
        [SerializeField] private Transform hand;
        [Tooltip("Where bought items appear, in metres from the panel's left edge (x right, y up, z away from you).")]
        [SerializeField] private Vector3 itemSpawnOffset = new(-0.12f, -0.04f, 0f);
        [Tooltip("How fast bought items fly out of the shop, in metres per second (x right, y up, z away from you); " +
                 "then they fall to the floor. All zero = they just drop.")]
        [SerializeField] private Vector3 flyOutVelocity = new(0f, 2f, 0.8f);

        [Header("Sounds")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip buySound;
        [SerializeField] private AudioClip failSound;

        [Header("Legendary offer buzz")]
        [Tooltip("When a legendary special comes up (and the special line is unlocked), the shop hand buzzes this many times.")]
        [Min(0)]
        [SerializeField] private int legendaryBuzzes = 2;
        [Range(0f, 1f)]
        [SerializeField] private float buzzStrength = 0.8f;
        [Tooltip("How long each buzz lasts, and the pause between them, in seconds.")]
        [SerializeField] private Vector2 buzzTiming = new(0.15f, 0.15f);

        /// <summary>The name the special line's unlock is saved under (see Unlocks).</summary>
        public const string SpecialUnlockId = "Shop.SpecialOfTheDay";

        // Used only if no hand can be found: in front of the head.
        private static readonly Vector3 NoHandOffset = new(0f, -0.15f, 0.5f);

        private static readonly List<Shop> enabledShops = new();
        private readonly Dictionary<string, int> purchases = new();
        private readonly List<int> onSale = new();
        private readonly List<int> lastOnSale = new();
        private Transform head;
        private bool available = true;
        private bool openedByHold;
        private bool stocked;
        private float restockAt;
        private XRPokeInteractor blockedPoke;
        private ShopEntry special;
        private bool specialLegendary;
        private bool specialBought;
        private readonly List<(ShopEntry entry, float chance)> specialChoices = new();

        /// <summary>Master switch for every shop, e.g. turned off by an FSM during a boss fight.</summary>
        public static bool AllAvailable { get; private set; } = true;

        /// <summary>The most recently enabled shop.</summary>
        public static Shop Main => enabledShops.Count > 0 ? enabledShops[enabledShops.Count - 1] : null;

        /// <summary>Raised after any purchase: the shop, the item, the price paid and the object that appeared (or null).</summary>
        public static event Action<Shop, ItemDefinition, int, GameObject> AnyPurchased;

        public event Action Opened;
        public event Action Closed;

        public ShopCatalog Catalog => catalog;
        public Transform Hand => hand;
        public bool IsOpen { get; private set; }
        public bool Available => available;
        public int ItemsOnSale => itemsOnSale;

        /// <summary>Where in the catalog the items on sale now are.</summary>
        public IReadOnlyList<int> OnSale => onSale;

        /// <summary>Seconds of play until new items go on sale.</summary>
        public float SecondsToRestock => stocked ? Mathf.Max(0f, restockAt - Time.time) : restockSeconds;

        /// <summary>Whether there's a special offer.</summary>
        public bool HasSpecial => CurrentSpecial != null;

        /// <summary>
        /// What's on the special line until the next restock: a legendary special, a normal item or another special;
        /// null if there's nothing to offer.
        /// </summary>
        public ShopEntry CurrentSpecial => special;

        /// <summary>Whether the special line's offer is a legendary one.</summary>
        public bool IsLegendaryOffer => special != null && specialLegendary;

        /// <summary>Whether this offer of the special was bought already (each offer can be bought once).</summary>
        public bool IsSpecialSoldOut => specialBought || (special != null && IsSoldOut(special));

        /// <summary>Raised when a legendary special comes up while the special line is unlocked: the shop, the offer.</summary>
        public static event Action<Shop, SpecialEntry> LegendaryOffered;

        /// <summary>Whether the special line has been paid for (it's saved), or costs nothing to open.</summary>
        public bool SpecialUnlocked => catalog != null &&
                                       (catalog.SpecialUnlockCurrency == null || catalog.SpecialUnlockPrice <= 0 || Unlocks.Has(SpecialUnlockId));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            enabledShops.Clear();
            AllAvailable = true;
            AnyPurchased = null;
            LegendaryOffered = null;
        }

        /// <summary>The shop on target (or its parents); with no target, the main shop.</summary>
        public static Shop Resolve(GameObject target)
        {
            return target != null ? target.GetComponentInParent<Shop>() : Main;
        }

        public static void SetAllAvailable(bool value)
        {
            AllAvailable = value;
            if (value) return;

            foreach (var shop in enabledShops.ToArray()) shop.Close();
        }

        private void OnEnable()
        {
            enabledShops.Add(this);
            openButton.action?.Enable();
            alternateOpenButton.action?.Enable();
        }

        private void OnDisable()
        {
            Close();
            enabledShops.Remove(this);
            // Only switch off our own buttons; a referenced action may be used elsewhere.
            if (openButton.reference == null) openButton.action?.Disable();
            if (alternateOpenButton.reference == null) alternateOpenButton.action?.Disable();
        }

        private void Update()
        {
            // Game time, so the clock stops while paused. Also while open: the panel counts down to it.
            if (!stocked || Time.time >= restockAt) Restock();

            // No shopping while the game is paused.
            if (Time.timeScale == 0f)
            {
                Close();
                return;
            }
            if (openMode == OpenMode.OnlyFromFsm) return;

            bool canOpen = available && AllAvailable;
            if (openMode == OpenMode.HoldButton)
            {
                bool held = IsPressed(openButton) || IsPressed(alternateOpenButton);
                if (held && !IsOpen && canOpen)
                {
                    Open();
                    openedByHold = IsOpen;
                }
                else if (!held && IsOpen && openedByHold)
                {
                    Close();
                }
            }
            else if (WasPressedThisFrame(openButton) || WasPressedThisFrame(alternateOpenButton))
            {
                if (IsOpen) Close();
                else if (canOpen) Open();
            }
        }

        private static bool IsPressed(InputActionProperty button)
        {
            return button.action != null && button.action.IsPressed();
        }

        private static bool WasPressedThisFrame(InputActionProperty button)
        {
            return button.action != null && button.action.WasPressedThisFrame();
        }

        public void SetAvailable(bool value)
        {
            available = value;
            if (!value) Close();
        }

        public void Open()
        {
            if (IsOpen || catalog == null || Time.timeScale == 0f) return;

            var target = panel != null ? panel : FindAnyObjectByType<ShopPanelUI>(FindObjectsInactive.Include);
            if (target == null)
            {
                Debug.LogWarning("[Shop] There is no shop panel in the scene. Run Tools > 429 Game > Shop > Set Up Candy Shop.", this);
                return;
            }
            bool hasHand = TryGetHand(out var anchor);
            // No shop while that hand is switched off, e.g. by an FSM.
            if (hasHand && !anchor.gameObject.activeInHierarchy) return;
            if (target.Owner != null && target.Owner != this) target.Owner.Close();

            panel = target;
            if (!stocked) Restock();
            if (hasHand)
            {
                // The panel rides on the hand like the inventory. Until the setup menu has put it there, use the default spot.
                if (!panel.transform.IsChildOf(anchor)) panel.PutOnHand(anchor);
                BlockPoke(anchor);
            }
            else if (TryGetHead(out var eyes))
            {
                panel.transform.position = eyes.position + HeadYaw(eyes) * NoHandOffset;
            }

            IsOpen = true;
            openedByHold = false;
            panel.Show(this, shopName);
            PlaySound(openSound, panel.transform.position);
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            openedByHold = false;
            RestorePoke();
            if (panel != null && panel.Owner == this) panel.Hide();
            Closed?.Invoke();
        }

        public PurchaseResult TryBuy(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.Entries.Count) return Fail(PurchaseResult.Unavailable);
            return Buy(catalog.Entries[index]);
        }

        /// <summary>Buys the special on offer, once per offer; the special line must be unlocked first.</summary>
        public PurchaseResult TryBuySpecial()
        {
            var entry = special;
            if (entry == null || entry.item == null || !SpecialUnlocked) return Fail(PurchaseResult.Unavailable);
            if (specialBought) return Fail(PurchaseResult.SoldOut);

            var result = Buy(entry);
            if (result == PurchaseResult.Bought) specialBought = true;
            return result;
        }

        /// <summary>Pays to unlock the special line for good. Bought = unlocked now (or already was).</summary>
        public PurchaseResult TryUnlockSpecial()
        {
            if (catalog == null) return Fail(PurchaseResult.Unavailable);
            if (SpecialUnlocked) return PurchaseResult.Bought;

            var inventory = PlayerInventory.Instance;
            if (inventory == null) return Fail(PurchaseResult.Unavailable);
            if (!inventory.TryRemove(catalog.SpecialUnlockCurrency, catalog.SpecialUnlockPrice)) return Fail(PurchaseResult.NotEnoughCurrency);

            Unlocks.Add(SpecialUnlockId);
            PlaySound(buySound, panel != null ? panel.transform.position : transform.position);
            return PurchaseResult.Bought;
        }

        private PurchaseResult Buy(ShopEntry entry)
        {
            var inventory = PlayerInventory.Instance;
            if (entry.item == null || inventory == null) return Fail(PurchaseResult.Unavailable);
            if (IsSoldOut(entry)) return Fail(PurchaseResult.SoldOut);
            if (entry.price > 0 && !inventory.TryRemove(catalog.Currency, entry.price)) return Fail(PurchaseResult.NotEnoughCurrency);

            purchases.TryGetValue(entry.item.Id, out var bought);
            purchases[entry.item.Id] = bought + 1;

            GameObject spawned = null;
            if (entry.item.WorldPrefab != null) spawned = Deliver(entry.item.WorldPrefab);
            else inventory.Add(entry.item);

            PlaySound(buySound, spawned != null ? spawned.transform.position : transform.position);
            AnyPurchased?.Invoke(this, entry.item, entry.price, spawned);
            return PurchaseResult.Bought;
        }

        /// <summary>Puts a new random pick from the catalog on sale. Happens by itself every 42 seconds.</summary>
        public void Restock()
        {
            stocked = true;
            restockAt = Time.time + restockSeconds;

            lastOnSale.Clear();
            lastOnSale.AddRange(onSale);
            // A few tries for a pick that isn't the same as the last one, when the catalog has more to choose from.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                PickOnSale();
                if (!SameAsLast()) break;
            }
            PickSpecial();

            if (IsOpen && panel != null) panel.Restocked();
        }

        // A new offer for the special line, the extra slot. First a roll for a legendary offer (more likely while the
        // pumpkin boss is being fought), which buzzes the shop hand once the line is open; otherwise one of the normal
        // items not already on sale, or a special that isn't legendary.
        private void PickSpecial()
        {
            special = null;
            specialLegendary = false;
            specialBought = false;
            if (catalog == null) return;

            bool boss = IsBossFight();
            if (UnityEngine.Random.value < (boss ? catalog.BossLegendaryChance : catalog.LegendaryChance))
            {
                specialChoices.Clear();
                foreach (var entry in catalog.Specials)
                {
                    if (entry != null && entry.item != null && entry.legendary && !IsSoldOut(entry))
                        specialChoices.Add((entry, boss ? entry.bossChance : entry.chance));
                }
                special = PickByChance(specialChoices);
                specialLegendary = special != null;
            }

            if (special == null)
            {
                // Not one that's on a normal row right now; if they all are, any.
                special = PickByChance(OrdinaryChoices(boss, true));
                if (special == null) special = PickByChance(OrdinaryChoices(boss, false));
            }

            if (specialLegendary && SpecialUnlocked)
            {
                if (legendaryBuzzes > 0) StartCoroutine(Buzz());
                LegendaryOffered?.Invoke(this, (SpecialEntry)special);
            }
        }

        // The normal items (chance 1 each) and the specials that aren't legendary (their chances). An item that's also a
        // special counts once, as the special (its price and chance).
        private List<(ShopEntry entry, float chance)> OrdinaryChoices(bool boss, bool skipOnSale)
        {
            specialChoices.Clear();
            if (catalog.OfferNormalItems)
            {
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    var entry = catalog.Entries[i];
                    if (entry.item == null || IsSoldOut(entry) || ListedBefore(i) || IsSpecialItem(entry.item)) continue;
                    if (skipOnSale && onSale.Contains(i)) continue;
                    specialChoices.Add((entry, 1f));
                }
            }
            foreach (var entry in catalog.Specials)
            {
                if (entry == null || entry.item == null || entry.legendary || IsSoldOut(entry)) continue;
                if (skipOnSale && IsOnSale(entry.item)) continue;
                specialChoices.Add((entry, boss ? entry.bossChance : entry.chance));
            }
            return specialChoices;
        }

        private static ShopEntry PickByChance(List<(ShopEntry entry, float chance)> choices)
        {
            float total = 0f;
            foreach (var (_, chance) in choices) total += Mathf.Max(0f, chance);
            if (total <= 0f) return null;

            float pick = UnityEngine.Random.value * total;
            ShopEntry picked = null;
            foreach (var (entry, chance) in choices)
            {
                if (chance <= 0f) continue;
                picked = entry;
                pick -= chance;
                if (pick <= 0f) break;
            }
            return picked;
        }

        private bool IsSpecialItem(ItemDefinition item)
        {
            foreach (var entry in catalog.Specials)
            {
                if (entry != null && entry.item == item && !entry.legendary) return true;
            }
            return false;
        }

        private bool IsOnSale(ItemDefinition item)
        {
            foreach (int index in onSale)
            {
                if (catalog.Entries[index].item == item) return true;
            }
            return false;
        }

        // The pumpkin boss is on the field and still alive.
        private static bool IsBossFight()
        {
            foreach (var boss in FindObjectsByType<Game.Enemies.PumpkinBossBrain>(FindObjectsSortMode.None))
            {
                if (!boss.isActiveAndEnabled) continue;
                if (!boss.TryGetComponent(out Game.Combat.EnemyHitDamage health) || !health.IsDead) return true;
            }
            return false;
        }

        // Buzzes the hand the shop rides on (the right one), a few times in a row. Game time, so a pause holds it.
        private System.Collections.IEnumerator Buzz()
        {
            if (!TryGetHand(out var anchor)) yield break;
            var buzzer = anchor.GetComponentInChildren<XRBaseInputInteractor>();
            if (buzzer == null) yield break;
            for (int i = 0; i < legendaryBuzzes; i++)
            {
                Game.Combat.HitBuzz.Other(buzzer, buzzStrength, buzzTiming.x);
                yield return new WaitForSeconds(buzzTiming.x + buzzTiming.y);
            }
        }

        private void PickOnSale()
        {
            onSale.Clear();
            if (catalog == null) return;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry.item != null && !IsSoldOut(entry) && !ListedBefore(i)) onSale.Add(i);
            }
            // Dropping random ones keeps a random pick, still in catalog order.
            while (itemsOnSale > 0 && onSale.Count > itemsOnSale)
                onSale.RemoveAt(UnityEngine.Random.Range(0, onSale.Count));
        }

        // The same item listed twice in the catalog is only on sale once (the first listing's price).
        private bool ListedBefore(int index)
        {
            var item = catalog.Entries[index].item;
            for (int i = 0; i < index; i++)
            {
                if (catalog.Entries[i].item == item) return true;
            }
            return false;
        }

        private bool SameAsLast()
        {
            if (onSale.Count != lastOnSale.Count) return false;
            for (int i = 0; i < onSale.Count; i++)
            {
                if (onSale[i] != lastOnSale[i]) return false;
            }
            return true;
        }

        public int GetPurchaseCount(ItemDefinition item)
        {
            return item != null && purchases.TryGetValue(item.Id, out var count) ? count : 0;
        }

        public bool IsSoldOut(ShopEntry entry)
        {
            return entry != null && entry.item != null && entry.stock > 0 && GetPurchaseCount(entry.item) >= entry.stock;
        }

        public List<SavedPurchase> ToSaveData()
        {
            var saved = new List<SavedPurchase>(purchases.Count);
            foreach (var pair in purchases)
                saved.Add(new SavedPurchase { id = pair.Key, count = pair.Value });
            return saved;
        }

        public void LoadSaveData(IEnumerable<SavedPurchase> saved)
        {
            purchases.Clear();
            if (saved != null)
            {
                foreach (var entry in saved)
                {
                    if (!string.IsNullOrEmpty(entry.id) && entry.count > 0) purchases[entry.id] = entry.count;
                }
            }
            if (IsOpen && panel != null) panel.Refresh();
        }

        // The hand holding the panel shouldn't press its buttons by accident: you buy with the other hand.
        private void BlockPoke(Transform holder)
        {
            var poke = holder.GetComponentInChildren<XRPokeInteractor>(true);
            if (poke == null || !poke.enableUIInteraction) return;

            poke.enableUIInteraction = false;
            blockedPoke = poke;
        }

        private void RestorePoke()
        {
            if (blockedPoke != null) blockedPoke.enableUIInteraction = true;
            blockedPoke = null;
        }

        // The way the head looks, ignoring up/down tilt (looking straight down, the head's up points forward).
        private static Quaternion HeadYaw(Transform eyes)
        {
            var forward = Vector3.ProjectOnPlane(eyes.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(eyes.up, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private bool TryGetHand(out Transform anchor)
        {
            if (hand == null) hand = XRRig.FindController(left: false);
            anchor = hand;
            return anchor != null;
        }

        private GameObject Deliver(GameObject prefab)
        {
            var position = transform.position;
            var facing = Quaternion.identity;
            if (panel != null)
            {
                // Beside the panel's left edge, where your other hand is.
                var anchor = panel.transform;
                var edge = anchor.position;
                if (anchor is RectTransform rect) edge = rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.center.y, 0f));
                position = edge + anchor.rotation * itemSpawnOffset;
                facing = Quaternion.Euler(0f, anchor.eulerAngles.y + 180f, 0f);
            }

            var spawned = Instantiate(prefab, position, facing * prefab.transform.rotation);
            if (spawned.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
            {
                // "Away from you" is the way the head looks; without a head, away from the panel's face.
                var away = TryGetHead(out var eyes) ? HeadYaw(eyes) : Quaternion.Euler(0f, facing.eulerAngles.y + 180f, 0f);
                FlyOut(body, away);
            }
            return spawned;
        }

        // Pops the item up and out with a tumble; gravity then drops it to the floor. A little random sideways, so a
        // few bought in a row don't land in a pile.
        private void FlyOut(Rigidbody body, Quaternion away)
        {
            var velocity = flyOutVelocity;
            if (velocity.sqrMagnitude > 0f) velocity.x += UnityEngine.Random.Range(-0.3f, 0.3f);
            body.linearVelocity = away * velocity;
            body.angularVelocity = UnityEngine.Random.insideUnitSphere * 6f;
        }

        private bool TryGetHead(out Transform eyes)
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            eyes = head;
            return eyes != null;
        }

        private PurchaseResult Fail(PurchaseResult result)
        {
            PlaySound(failSound, panel != null ? panel.transform.position : transform.position);
            return result;
        }

        private static void PlaySound(AudioClip clip, Vector3 position)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position);
        }
    }
}
