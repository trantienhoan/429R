using System.Collections.Generic;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Shopping
{
    /// <summary>
    /// The world-space shop panel. One lives in the scene, on the right hand like the inventory on the left;
    /// a Shop borrows it while open. Buttons work with poke and ray.
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
        [Tooltip("How long messages like 'Not enough Candy' stay on screen.")]
        [SerializeField] private float statusSeconds = 2.5f;
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

        private readonly List<ShopItemRow> rows = new();
        private PlayerInventory inventory;
        private float statusHideTime;

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

            var entries = catalog.Entries;
            while (rows.Count < entries.Count)
                rows.Add(Instantiate(rowTemplate, rowParent));

            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < entries.Count && entries[i].item != null;
                rows[i].gameObject.SetActive(used);
                if (!used) continue;

                int index = i;
                var entry = entries[i];
                rows[i].Set(entry.item, entry.price, currency, Owner.IsSoldOut(entry), money >= entry.price, () => OnBuyClicked(index));
            }
        }

        private void OnBuyClicked(int index)
        {
            if (Owner == null) return;

            var catalog = Owner.Catalog;
            var entry = catalog.Entries[index];
            var currency = catalog.Currency;
            string currencyName = currency != null ? currency.DisplayName : "";

            switch (Owner.TryBuy(index))
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
            Refresh();
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
