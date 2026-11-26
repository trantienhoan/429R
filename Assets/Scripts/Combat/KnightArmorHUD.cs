using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat
{
    /// <summary>
    /// The Knight's armor on the health HUD (user 2026-11-26): the Knight's helmet icon and the armor left, just above the
    /// health number, once a piece is found this run (dimmed when the armor is broken). It fades with the HUD, and a
    /// change in the armor shows the HUD for a moment. Added to the Player Health HUD by the Knight Armor Hider.
    /// </summary>
    [RequireComponent(typeof(PlayerHealthHUD))]
    public class KnightArmorHUD : MonoBehaviour
    {
        private PlayerHealthHUD hud;
        private RectTransform group;
        private Image icon;
        private TMP_Text amount;
        private bool built;

        private void OnEnable()
        {
            hud = GetComponent<PlayerHealthHUD>();
            KnightArmor.Changed += OnChanged;
            Build();
            Show();
        }

        private void OnDisable()
        {
            KnightArmor.Changed -= OnChanged;
        }

        private void OnChanged()
        {
            Show();
            if (hud != null && isActiveAndEnabled) hud.Poke();
        }

        private void Build()
        {
            if (built || hud == null || hud.NumberText == null) return;
            built = true;
            var settings = KnightArmorSettings.Instance;
            var number = hud.NumberText.rectTransform;
            float height = Mathf.Max(20f, number.rect.height);

            group = new GameObject("Knight Armor", typeof(RectTransform)).GetComponent<RectTransform>();
            group.SetParent(number.parent, false);
            group.anchorMin = number.anchorMin;
            group.anchorMax = number.anchorMax;
            group.pivot = new Vector2(0.5f, 0.5f);
            group.sizeDelta = new Vector2(height * 2.2f, height * 0.6f);
            group.anchoredPosition = number.anchoredPosition + new Vector2(0f, height * 0.85f);

            icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.rectTransform.SetParent(group, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(height * 0.6f, height * 0.6f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.sprite = settings != null ? settings.Icon : null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = icon.sprite != null;

            var style = hud.ChangeText != null ? hud.ChangeText : hud.NumberText;
            amount = Instantiate(style, group);
            amount.name = "Amount";
            amount.gameObject.SetActive(true);
            var rect = amount.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(height * 0.68f, 0f);
            rect.offsetMax = Vector2.zero;
            amount.alignment = TextAlignmentOptions.Left;
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 14f;
            amount.fontSizeMax = height * 0.55f;
            amount.color = settings != null ? settings.NumberColor : Color.white;
            amount.raycastTarget = false;
        }

        private void Show()
        {
            if (!built) Build();
            if (group == null) return;
            bool any = KnightArmor.MaxArmor > 0f;
            group.gameObject.SetActive(any);
            if (!any) return;
            amount.SetText("{0}", Mathf.CeilToInt(KnightArmor.Armor));
            // Broken: the icon and number dim (the pieces' stats stay for the run).
            float alpha = KnightArmor.Armor > 0f ? 1f : 0.35f;
            if (icon != null) icon.color = new Color(1f, 1f, 1f, alpha);
            var colour = amount.color;
            colour.a = alpha;
            amount.color = colour;
        }
    }
}
