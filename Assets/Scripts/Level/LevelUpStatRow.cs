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

        private Action onPlus;

        private void Awake()
        {
            if (plusButton != null) plusButton.onClick.AddListener(() => onPlus?.Invoke());
        }

        public void Set(string statName, string gives, int have, int most, bool canRaise, Action plus)
        {
            if (label != null) label.text = statName;
            if (effect != null) effect.text = gives;
            if (points != null) points.text = have >= most ? "MAX" : $"{have} / {most}";
            if (plusButton != null) plusButton.interactable = canRaise;
            onPlus = plus;
        }
    }
}
