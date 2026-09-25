using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Shopping
{
    /// <summary>One line of the shop panel: icon, name, price and a Buy button.</summary>
    public class ShopItemRow : MonoBehaviour
    {
        private static readonly Color CantAffordColor = new(1f, 0.45f, 0.45f);

        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image priceIcon;
        [SerializeField] private TMP_Text price;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyLabel;

        public void Set(ItemDefinition item, int cost, ItemDefinition currency, bool soldOut, bool canAfford, UnityAction onBuy)
        {
            if (icon != null)
            {
                icon.sprite = item.Icon;
                icon.enabled = item.Icon != null;
            }
            if (label != null) label.text = item.DisplayName;

            if (priceIcon != null)
            {
                priceIcon.sprite = currency != null ? currency.Icon : null;
                priceIcon.enabled = priceIcon.sprite != null && cost > 0;
            }
            if (price != null)
            {
                price.text = cost > 0 ? cost.ToString() : "Free";
                price.color = canAfford || cost <= 0 ? Color.white : CantAffordColor;
            }

            if (buyLabel != null) buyLabel.text = soldOut ? "Sold out" : "Buy";
            if (buyButton != null)
            {
                // Still clickable when too expensive, so pressing it can say how much more is needed.
                buyButton.interactable = !soldOut;
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(onBuy);
            }
        }
    }
}
