using System.Collections.Generic;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Shopping
{
    /// <summary>
    /// The world-space shop panel. One lives in the scene, on the right hand like the inventory on the left;
    /// a Shop borrows it while open. It lists what's on sale; clicking a row buys it, with poke or ray.
    /// </summary>
    public class ShopPanelUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private Image currencyIcon;
        [SerializeField] private TMP_Text currencyCount;
        [SerializeField] private ShopItemRow rowTemplate;
        [SerializeField] private Transform rowParent;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button closeButton;
        [Tooltip("Counts down to the next new items.")]
        [SerializeField] private TMP_Text restockText;
        [Tooltip("The special offer: its title and line. Hidden when the catalog has no specials.")]
        [SerializeField] private GameObject specialSection;
        [SerializeField] private ShopItemRow specialRow;
        [Tooltip("How long messages like 'Not enough Candy' stay on screen.")]
        [SerializeField] private float statusSeconds = 2.5f;
        [Tooltip("Clicks this soon after new items come in are ignored, so you don't buy something you didn't aim at.")]
        [SerializeField] private float clickGuardSeconds = 0.4f;
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;
        [Tooltip("Turn the panel towards your eyes so it's always readable, like the inventory.")]
        [SerializeField] private bool faceHead = true;

        /// <summary>
        /// Where the panel sits on a hand until you move it: its bottom edge a little above the controller and to
        /// the left, so the Buy buttons are over the hand and your other hand can reach them.
        /// </summary>
        public static readonly Vector3 HandPosition = new(-0.06f, 0.04f, 0f);
        // About 25 x 20 cm, so its text is as big as the inventory's.
        private const float HandScale = 0.0006f;

        private static readonly Color GoodColor = new(0.55f, 1f, 0.55f);
        private static readonly Color BadColor = new(1f, 0.5f, 0.5f);
        private static readonly Color NewsColor = new(1f, 0.85f, 0.4f);
        private const string SpecialTitle = "Special Offer";
        private const string LegendaryTitle = "Legendary Offer!";

        private readonly List<ShopItemRow> rows = new();
        private PlayerInventory inventory;
        private float statusHideTime;
        private float clicksFrom;
        private int shownSeconds = -1;
        private TMP_Text specialTitle;

        public Shop Owner { get; private set; }

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (statusText != null && statusText.text.Length > 0 && Time.unscaledTime >= statusHideTime) statusText.text = "";
            ShowCountdown();
        }

        /// <summary>New items came in while the panel is open.</summary>
        public void Restocked()
        {
            clicksFrom = Time.unscaledTime + clickGuardSeconds;
            Refresh();
            ShowStatus("New items!", NewsColor);
        }

        private void ShowCountdown()
        {
            if (restockText == null || Owner == null) return;

            int seconds = Mathf.CeilToInt(Owner.SecondsToRestock);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            restockText.text = $"New items in {seconds / 60}:{seconds % 60:00}";
        }

        private void LateUpdate()
        {
            if (faceHead) FaceHead();
        }

        /// <summary>
        /// Puts the panel on a hand at the default spot. It stands on its bottom edge, so it stays clear of the hand
        /// and barely swings when you turn your wrist.
        /// </summary>
        public void PutOnHand(Transform hand)
        {
            transform.SetParent(hand, false);
            if (transform is RectTransform rect) rect.pivot = new Vector2(0.5f, 0f);
            transform.localPosition = HandPosition;
            transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            transform.localScale = Vector3.one * HandScale;
        }

        public void Show(Shop shop, string shopName)
        {
            Owner = shop;
            gameObject.SetActive(true);
            if (faceHead) FaceHead();
            if (title != null) title.text = shopName;
            if (statusText != null) statusText.text = "";

            Unsubscribe();
            inventory = PlayerInventory.Instance;
            if (inventory != null) inventory.Changed += OnInventoryChanged;
            shownSeconds = -1;
            ShowCountdown();
            Refresh();
        }

        public void Hide()
        {
            Owner = null;
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (Owner == null || Owner.Catalog == null || rowTemplate == null || rowParent == null) return;

            var catalog = Owner.Catalog;
            var currency = catalog.Currency;
            int money = inventory != null && currency != null ? inventory.GetCount(currency) : 0;

            if (currencyIcon != null)
            {
                currencyIcon.sprite = currency != null ? currency.Icon : null;
                currencyIcon.enabled = currencyIcon.sprite != null;
            }
            if (currencyCount != null) currencyCount.text = currency != null ? $"You have {money} {currency.DisplayName}" : "";

            // Only what's on sale right now; the shop picks new items every few minutes.
            var entries = catalog.Entries;
            var onSale = Owner.OnSale;
            while (rows.Count < onSale.Count)
                rows.Add(Instantiate(rowTemplate, rowParent));

            for (int i = 0; i < rows.Count; i++)
            {
                int index = i < onSale.Count ? onSale[i] : -1;
                var entry = index >= 0 && index < entries.Count ? entries[index] : null;
                bool used = entry != null && entry.item != null;
                rows[i].gameObject.SetActive(used);
                if (used) rows[i].Set(entry.item, entry.price, currency, Owner.IsSoldOut(entry), money >= entry.price, () => OnBuyClicked(index));
            }

            RefreshSpecial(money);
        }

        private void RefreshSpecial(int money)
        {
            var special = Owner.CurrentSpecial;
            if (specialSection != null) specialSection.SetActive(special != null);
            if (special == null || specialRow == null) return;

            // Its title says when the offer is a legendary one.
            if (specialTitle == null && specialSection != null)
            {
                var title = specialSection.transform.Find("Title");
                if (title != null) specialTitle = title.GetComponent<TMP_Text>();
            }
            if (specialTitle != null) specialTitle.text = Owner.IsLegendaryOffer ? LegendaryTitle : SpecialTitle;

            var catalog = Owner.Catalog;
            if (Owner.SpecialUnlocked)
            {
                specialRow.Set(special.item, special.price, catalog.Currency, Owner.IsSpecialSoldOut, money >= special.price, OnSpecialClicked);
                return;
            }

            // Locked: a dark shape of today's special and what opening the line costs.
            var key = catalog.SpecialUnlockCurrency;
            int keys = inventory != null ? inventory.GetCount(key) : 0;
            specialRow.Show(special.item.Icon, Color.black, "Unlock", catalog.SpecialUnlockPrice, key, false, keys >= catalog.SpecialUnlockPrice, OnSpecialClicked);
        }

        private void OnSpecialClicked()
        {
            if (Owner == null || Time.unscaledTime < clicksFrom) return;

            if (Owner.SpecialUnlocked)
            {
                var special = Owner.CurrentSpecial;
                if (special != null) ShowResult(Owner.TryBuySpecial(), special, Owner.Catalog.Currency);
            }
            else
            {
                var catalog = Owner.Catalog;
                var key = catalog.SpecialUnlockCurrency;
                switch (Owner.TryUnlockSpecial())
                {
                    case PurchaseResult.Bought:
                        ShowStatus("Special offers unlocked!", GoodColor);
                        break;
                    case PurchaseResult.NotEnoughCurrency:
                        int keys = inventory != null ? inventory.GetCount(key) : 0;
                        ShowStatus($"You need {catalog.SpecialUnlockPrice - keys} more {(key != null ? key.DisplayName : "")}", BadColor);
                        break;
                    default:
                        ShowStatus("Can't unlock that right now", BadColor);
                        break;
                }
            }
            Refresh();
        }

        private void OnBuyClicked(int index)
        {
            if (Owner == null || Time.unscaledTime < clicksFrom) return;

            var catalog = Owner.Catalog;
            ShowResult(Owner.TryBuy(index), catalog.Entries[index], catalog.Currency);
            Refresh();
        }

        private void ShowResult(PurchaseResult result, ShopEntry entry, ItemDefinition currency)
        {
            string currencyName = currency != null ? currency.DisplayName : "";
            switch (result)
            {
                case PurchaseResult.Bought:
                    ShowStatus($"Enjoy your {entry.item.DisplayName}!", GoodColor);
                    break;
                case PurchaseResult.NotEnoughCurrency:
                    int money = inventory != null && currency != null ? inventory.GetCount(currency) : 0;
                    ShowStatus($"You need {entry.price - money} more {currencyName}", BadColor);
                    break;
                case PurchaseResult.SoldOut:
                    ShowStatus("Sold out", BadColor);
                    break;
                default:
                    ShowStatus("Can't buy that right now", BadColor);
                    break;
            }
        }

        private void ShowStatus(string message, Color color)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = color;
            statusHideTime = Time.unscaledTime + statusSeconds;
        }

        private void FaceHead()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;

            // Aim at the middle of the panel, not its bottom edge. A world-space canvas is readable when its
            // forward points away from the viewer.
            var middle = transform is RectTransform rect ? rect.TransformPoint(rect.rect.center) : transform.position;
            var away = middle - head.position;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        private void OnCloseClicked()
        {
            if (Owner != null) Owner.Close();
            else Hide();
        }

        private void OnInventoryChanged(ItemDefinition item, int change, int newCount)
        {
            Refresh();
        }

        private void Unsubscribe()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            inventory = null;
        }
    }
}
