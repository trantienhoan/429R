using System;
using System.Collections.Generic;
using Game.Inventory;
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
    /// The candy shop. By default its panel shows on your right hand while B on the right controller is held,
    /// like the inventory on the left hand, and you buy with your left hand. Bought items float next to the panel
    /// until grabbed. FSMs use the actions in PlayMaker's "Shop" category.
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

        [Header("Opening")]
        [Tooltip("Hold Button: open while held. Toggle Button: press to open, press again to close. Only From FSM: use the Shop Open / Shop Close actions.")]
        [SerializeField] private OpenMode openMode = OpenMode.HoldButton;
        [Tooltip("Defaults to B on the right controller.")]
        [SerializeField] private InputActionProperty openButton = new(new InputAction("Open Shop", InputActionType.Button, "<XRController>{RightHand}/secondaryButton"));

        [Header("Placement")]
        [Tooltip("The hand the panel rides on. Left empty, the Right Controller is found automatically. " +
                 "To change where the panel sits, move 'Shop Panel' under that hand, like the Wrist Inventory.")]
        [SerializeField] private Transform hand;
        [Tooltip("Where bought items appear, in metres from the panel's left edge (x right, y up, z away from you).")]
        [SerializeField] private Vector3 itemSpawnOffset = new(-0.12f, -0.04f, 0f);

        [Header("Sounds")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip buySound;
        [SerializeField] private AudioClip failSound;

        // Used only if no hand can be found: in front of the head.
        private static readonly Vector3 NoHandOffset = new(0f, -0.15f, 0.5f);

        private static readonly List<Shop> enabledShops = new();
        private readonly Dictionary<string, int> purchases = new();
        private Transform head;
        private bool available = true;
        private bool openedByHold;
        private XRPokeInteractor blockedPoke;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            enabledShops.Clear();
            AllAvailable = true;
            AnyPurchased = null;
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
        }

        private void OnDisable()
        {
            Close();
            enabledShops.Remove(this);
            // Only switch off our own button; a referenced action may be used elsewhere.
            if (openButton.reference == null) openButton.action?.Disable();
        }

        private void Update()
        {
            // No shopping while the game is paused.
            if (Time.timeScale == 0f)
            {
                Close();
                return;
            }
            if (openMode == OpenMode.OnlyFromFsm || openButton.action == null) return;

            bool canOpen = available && AllAvailable;
            if (openMode == OpenMode.HoldButton)
            {
                bool held = openButton.action.IsPressed();
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
            else if (openButton.action.WasPressedThisFrame())
            {
                if (IsOpen) Close();
                else if (canOpen) Open();
            }
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

            var entry = catalog.Entries[index];
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
            if (spawned.TryGetComponent<Rigidbody>(out _)) spawned.AddComponent<FloatUntilGrabbed>();
            return spawned;
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
