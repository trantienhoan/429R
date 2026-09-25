using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Inventory
{
    /// <summary>One line of the wrist inventory: icon, name and count.</summary>
    public class WristInventoryRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text count;

        public void Set(ItemDefinition item, int amount)
        {
            if (icon != null)
            {
                icon.sprite = item.Icon;
                icon.enabled = item.Icon != null;
            }
            if (label != null) label.text = item.DisplayName;
            if (count != null) count.text = "x" + amount;
        }
    }
}
