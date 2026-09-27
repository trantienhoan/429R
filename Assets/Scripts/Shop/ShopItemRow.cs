using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Shopping
{
    /// <summary>One line of the shop panel: icon, name and price. Clicking the line buys the item.</summary>
    public class ShopItemRow : MonoBehaviour
    {
        private static readonly Color CantAffordColor = new(1f, 0.45f, 0.45f);

        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image priceIcon;
        [SerializeField] private TMP_Text price;
        [Tooltip("Buys the item when clicked, normally the row itself. Left empty, the first button in the row is used.")]
        [SerializeField] private Button button;

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
                priceIcon.enabled = priceIcon.sprite != null && cost > 0 && !soldOut;
            }
            if (price != null)
            {
                if (soldOut) price.text = "Sold out";
                else price.text = cost > 0 ? cost.ToString() : "Free";
                price.color = soldOut || canAfford || cost <= 0 ? Color.white : CantAffordColor;
            }

            if (button == null) button = GetComponentInChildren<Button>(true);
            if (button != null)
            {
                // Still clickable when too expensive, so clicking can say how much more is needed.
                button.interactable = !soldOut;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onBuy);
            }
        }
    }
}
