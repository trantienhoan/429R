using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Inventory
{
    /// <summary>
    /// Small panel on the wrist listing what the player carries.
    /// By default it shows while Y on the left controller is held.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class WristInventoryUI : MonoBehaviour
    {
        public enum ShowMode
        {
            HoldButton,
            LookAtWrist,
            Always,
        }

        [SerializeField] private WristInventoryRow rowTemplate;
        [SerializeField] private Transform rowParent;
        [Tooltip("Shown while the inventory is empty (optional).")]
        [SerializeField] private GameObject emptyLabel;
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;

        [Header("When to show")]
        [Tooltip("Hold Button: while a button is held. Look At Wrist: when the panel faces your eyes. Always: never hidden.")]
        [SerializeField] private ShowMode showMode = ShowMode.HoldButton;
        [Tooltip("The button for Hold Button. Defaults to Y on the left controller.")]
        [SerializeField] private InputActionProperty showButton = new(new InputAction("Show Inventory", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton"));
        [Tooltip("Hold Button and Always: turn the panel towards your eyes so it's always readable.")]
        [SerializeField] private bool faceHead = true;
        [Tooltip("Look At Wrist: the panel shows when it faces your eyes within this many degrees.")]
        [Range(5f, 90f)]
        [SerializeField] private float showAngle = 35f;
        [SerializeField] private float fadeSpeed = 6f;

        private readonly List<WristInventoryRow> rows = new();
        private CanvasGroup group;
        private Canvas canvas;
        private PlayerInventory inventory;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            canvas = GetComponent<Canvas>();
            group.alpha = showMode == ShowMode.Always ? 1f : 0f;
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            showButton.action?.Enable();
        }

        private void OnDisable()
        {
            // Only switch off our own button; a referenced action may be used elsewhere.
            if (showButton.reference == null) showButton.action?.Disable();

            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            inventory = null;
        }

        private void LateUpdate()
        {
            if (inventory == null && PlayerInventory.Instance != null)
            {
                inventory = PlayerInventory.Instance;
                inventory.Changed += OnInventoryChanged;
                Rebuild();
            }

            // Unscaled time, so the panel still works while the game is paused.
            float target = ShouldShow() ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            if (canvas != null) canvas.enabled = group.alpha > 0.001f;

            if (faceHead && showMode != ShowMode.LookAtWrist && group.alpha > 0f && TryGetHead(out var eyes))
            {
                // A world-space canvas is readable when its forward points away from the viewer.
                var away = transform.position - eyes.position;
                if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        private bool ShouldShow()
        {
            switch (showMode)
            {
                case ShowMode.Always:
                    return true;
                case ShowMode.LookAtWrist:
                    return TryGetHead(out var eyes) && Vector3.Angle(transform.forward, transform.position - eyes.position) <= showAngle;
                default:
                    return showButton.action != null && showButton.action.IsPressed();
            }
        }

        private bool TryGetHead(out Transform eyes)
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            eyes = head;
            return eyes != null;
        }

        private void OnInventoryChanged(ItemDefinition item, int change, int newCount)
        {
            Rebuild();
        }

        private void Rebuild()
        {
            if (rowTemplate == null || rowParent == null || inventory == null) return;

            var items = new List<KeyValuePair<ItemDefinition, int>>(inventory.Counts);
            items.Sort((a, b) =>
            {
                int byOrder = a.Key.ListOrder.CompareTo(b.Key.ListOrder);
                return byOrder != 0 ? byOrder : string.Compare(a.Key.DisplayName, b.Key.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            while (rows.Count < items.Count)
                rows.Add(Instantiate(rowTemplate, rowParent));

            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < items.Count;
                rows[i].gameObject.SetActive(used);
                if (used) rows[i].Set(items[i].Key, items[i].Value);
            }

            if (emptyLabel != null) emptyLabel.SetActive(items.Count == 0);
        }
    }
}
