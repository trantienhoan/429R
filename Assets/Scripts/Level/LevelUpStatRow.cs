using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Leveling
{
    /// <summary>One stat's line on the Level Up menu: its name, what it gives now, its points, and the + button.</summary>
    public class LevelUpStatRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text effect;
        [SerializeField] private TMP_Text points;
        [SerializeField] private Button plusButton;
        [Tooltip("The row's background colour for a bonus stat.")]
        [SerializeField] private Color highlightColor = new(1f, 0.78f, 0.25f, 0.22f);

        private Action onPlus;
        private Image background;
        private Color normalColor;

        private void Awake()
        {
            if (plusButton != null) plusButton.onClick.AddListener(() => onPlus?.Invoke());
            if (TryGetComponent(out background)) normalColor = background.color;
        }

        public void Set(string statName, string gives, int have, int most, bool canRaise, Action plus)
        {
            if (label != null) label.text = statName;
            if (effect != null) effect.text = gives;
            if (points != null) points.text = have >= most ? "MAX" : $"{have} / {most}";
            if (plusButton != null) plusButton.interactable = canRaise;
            onPlus = plus;
        }

        /// <summary>Gold background for a bonus stat, the normal one otherwise.</summary>
        public void Highlight(bool on)
        {
            if (background != null) background.color = on ? highlightColor : normalColor;
        }
    }
}
