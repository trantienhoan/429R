using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Inventory
{
    /// <summary>
    /// Small panel on the wrist listing what the player carries.
    /// By default it shows while Y on the left controller is held. With Float Above Hand it sits just above the hand as
    /// you see it (however the hand is turned), so it never covers the hand.
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

        [Header("Where")]
        [Tooltip("Hold Button and Always: the panel floats just above the hand as you see it, however the hand is turned, " +
                 "so it never covers the hand. Off: it stays where it's placed on the controller.")]
        [SerializeField] private bool floatAboveHand = true;
        [Tooltip("The hand it floats above. Left empty: the biggest hand model under the same controller.")]
        [SerializeField] private Renderer hand;
        [Tooltip("Gap between the top of the hand and the panel's bottom edge, as you see it (metres).")]
        [Min(0f)]
        [SerializeField] private float gap = 0.03f;

        // With no hand to measure (hidden, or none): the controller, and a hand this big round it (metres).
        private const float HandRadius = 0.1f;
        private readonly List<WristInventoryRow> rows = new();
        private CanvasGroup group;
        private Canvas canvas;
        private PlayerInventory inventory;
        private bool lookedForHand;
        private Mesh handMesh;
        private readonly List<Vector3> handPoints = new();
        private Vector3 handCenter;

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

        private void OnDestroy()
        {
            if (handMesh != null) Destroy(handMesh);
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

            if (showMode != ShowMode.LookAtWrist && group.alpha > 0f && TryGetHead(out var eyes))
            {
                if (floatAboveHand) FloatAboveHand(eyes);
                // A world-space canvas is readable when its forward points away from the viewer.
                var away = transform.position - eyes.position;
                if (faceHead && away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        // Just above the hand as the eyes see it: "up" is world up as it looks from the eyes (straight up across the
        // line of sight), and the panel's bottom edge sits Gap above the hand's highest point as seen from the eyes
        // (measured on the hand's real shape, so its pose and how near it is both count).
        private void FloatAboveHand(Transform eyes)
        {
            var parent = transform.parent;
            bool shaped = HandShape();
            var center = shaped ? handCenter : parent != null ? parent.position : transform.position;
            var sight = center - eyes.position;
            float distance = sight.magnitude;
            if (distance < 0.01f) return;
            var forward = sight / distance;
            var up = Vector3.ProjectOnPlane(Vector3.up, forward);
            up = up.sqrMagnitude > 0.0001f ? up.normalized : eyes.up;

            // How high the hand reaches above the line of sight, as a slope from the eyes (its highest point on screen).
            float slope = HandRadius / distance;
            if (shaped)
            {
                slope = 0f;
                foreach (var point in handPoints)
                {
                    var toPoint = point - eyes.position;
                    float ahead = Mathf.Max(Vector3.Dot(toPoint, forward), distance * 0.25f);
                    slope = Mathf.Max(slope, Vector3.Dot(toPoint, up) / ahead);
                }
            }
            var rect = transform as RectTransform;
            float halfHeight = rect != null ? rect.rect.height * Mathf.Abs(transform.lossyScale.y) * 0.5f : 0.08f;
            transform.position = eyes.position + forward * distance + up * (slope * distance + gap + halfHeight);
        }

        // The hand's real shape now (it moves and bends), in world space; false if it's hidden or there's none.
        private bool HandShape()
        {
            if (hand == null && !lookedForHand)
            {
                lookedForHand = true;
                float biggest = 0f;
                if (transform.parent != null)
                {
                    foreach (var part in transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (part.transform.IsChildOf(transform) || part.sharedMesh == null) continue;
                        float size = part.sharedMesh.bounds.size.sqrMagnitude;
                        if (size > biggest) { biggest = size; hand = part; }
                    }
                }
            }
            if (hand == null || !hand.enabled || !hand.gameObject.activeInHierarchy) return false;

            handPoints.Clear();
            if (hand is SkinnedMeshRenderer skin)
            {
                if (handMesh == null) handMesh = new Mesh();
                skin.BakeMesh(handMesh, false);
                handMesh.GetVertices(handPoints);
                var toWorld = hand.transform.localToWorldMatrix;
                for (int i = 0; i < handPoints.Count; i++) handPoints[i] = toWorld.MultiplyPoint3x4(handPoints[i]);
            }
            else
            {
                // Not a skinned hand: its box's corners.
                var box = hand.bounds;
                for (int i = 0; i < 8; i++)
                    handPoints.Add(box.center + Vector3.Scale(box.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
            }
            if (handPoints.Count == 0) return false;
            var sum = Vector3.zero;
            foreach (var point in handPoints) sum += point;
            handCenter = sum / handPoints.Count;
            return true;
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
