using System;
using System.Collections.Generic;
using Game.Inventory;
using Game.Saving;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Game.Paths
{
    /// <summary>
    /// A path blocker the player buys open with seeds. Hit it with something (or a hand) and a price board appears above
    /// it: pay with ONE kind of seed, e.g. 50 Yellow, 5 Green or 1 Boss Seed, by pointing at a line and clicking (or
    /// poking it), like the shop. Once paid, the GAMESTAGES bool Cube Bool turns true, a Path Gate opens on the floor where
    /// it stood (its own GAMESTAGES bool, Gate Bool, is true while the player has stood in it a few seconds) and the
    /// block breaks the way it normally does, through its Health FSM. The unlock is saved under the Cube Bool's name: in
    /// later sessions the block removes itself as soon as it appears, its gate is already open and Cube Bool turns true.
    /// Goes on the block's root; Tools > 429 Game > Paths > Set Up Seed Locks puts it on Stone_Cube_Dice 1, 2 and 3.
    /// </summary>
    [DisallowMultipleComponent]
    public class SeedLock : MonoBehaviour
    {
        [Serializable]
        public class Price
        {
            public ItemDefinition item;
            [Min(1)] public int amount = 1;
        }

        [Header("Price (the player picks one)")]
        [SerializeField] private Price[] prices = Array.Empty<Price>();
        [SerializeField] private string title = "Open this path";
        [SerializeField] private string subtitle = "Pick a seed to pay with";

        [Header("When paid")]
        [Tooltip("GAMESTAGES bool that turns true when it's paid for, and stays true in later sessions, e.g. Cube_1. Also " +
                 "the name the unlock is saved under, so renaming it locks the path again. Add it to the GAMESTAGES FSM's Variables.")]
        [SerializeField] private string cubeBool = "Cube_1";
        [Tooltip("GAMESTAGES bool that's true while the player has stood in the gate for Gate Seconds, e.g. Gate_1; " +
                 "stepping out turns it false.")]
        [SerializeField] private string gateBool = "Gate_1";
        [Tooltip("The coloured circle effect that marks the gate on the floor.")]
        [SerializeField] private GameObject gateEffect;
        [Tooltip("Size of the circle effect. The Hyper Casual FX circles are about 10 m wide, so 0.18 makes them about 1.8 m.")]
        [Min(0.01f)]
        [SerializeField] private float gateEffectScale = 0.18f;
        [Tooltip("How close to the gate's middle (metres, along the floor) the player's head must be to count as standing in it.")]
        [Min(0.1f)]
        [SerializeField] private float gateRadius = 0.85f;
        [Tooltip("How long the player must stand in the gate, in seconds.")]
        [Min(0f)]
        [SerializeField] private float gateSeconds = 3f;
        [Tooltip("Optional sounds: the gate's countdown (a whole-countdown clip plays once, a short tick on every number; " +
                 "stepping out stops it) and when it's done.")]
        [SerializeField] private AudioClip gateTickSound;
        [SerializeField] private AudioClip gateDoneSound;
        [Tooltip("Moves the gate away from the spot under the block's middle, in metres.")]
        [SerializeField] private Vector3 gateOffset;
        [Tooltip("The FSM and float variable that break the block: set to 0, its Health FSM plays its pop and removes it. " +
                 "Without them the block just vanishes.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        [Header("Price board")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Hits slower than this, in metres per second, don't bring up the board (e.g. something just leaning on it).")]
        [Min(0f)]
        [SerializeField] private float showSpeed = 0.5f;
        [Tooltip("How far above the block's top the board floats, in metres. Negative brings it down in front of the block's face.")]
        [SerializeField] private float boardHeight = 0.2f;
        [Tooltip("Metres per board pixel: 0.0032 makes the board about 1.2 m wide.")]
        [Min(0.0001f)]
        [SerializeField] private float boardScale = 0.0032f;
        [Tooltip("The board goes away when the player is this far from the block, in metres.")]
        [Min(1f)]
        [SerializeField] private float hideDistance = 6f;
        [Tooltip("The board goes away after this many seconds without a hit or a click.")]
        [Min(1f)]
        [SerializeField] private float hideAfter = 30f;

        // Board layout, in board pixels.
        private const float BoardWidth = 380f;
        private const float RowsTop = 86f;
        private const float RowHeight = 56f;
        private const float RowSpacing = 6f;
        private const float StatusHeight = 36f;
        private const float StatusSeconds = 2.5f;

        private static readonly Color BoardColor = new(0.1f, 0.07f, 0.16f, 0.92f);
        private static readonly Color HintColor = new(0.75f, 0.75f, 0.82f);
        private static readonly Color EnoughColor = new(0.55f, 1f, 0.55f);
        private static readonly Color BadColor = new(1f, 0.5f, 0.5f);

        private class Row
        {
            public Button button;
            public Image icon;
            public TMP_Text label;
            public TMP_Text cost;
        }

        private readonly List<Row> rows = new();
        private RectTransform board;
        private TMP_Text status;
        private float statusHideTime;
        private float lastTouch;
        private bool boardPlaced;
        private bool opened;
        private Collider[] colliders;
        private Transform head;
        private PlayerInventory inventory;

        public bool IsOpen => opened;

        private void Awake()
        {
            colliders = GetComponentsInChildren<Collider>();
            // Already bought in an earlier session: not even shown for a frame.
            if (Unlocks.Has(cubeBool))
            {
                foreach (var shown in GetComponentsInChildren<Renderer>()) shown.enabled = false;
            }
        }

        // In Start, once whatever spawned it has put it in its place (the gate goes next to it).
        private void Start()
        {
            if (Unlocks.IsLoaded) OpenIfUnlocked();
            else Unlocks.Loaded += OpenIfUnlocked;
        }

        private void OnDisable()
        {
            HideBoard();
        }

        private void OnDestroy()
        {
            Unlocks.Loaded -= OpenIfUnlocked;
            if (board != null) Destroy(board.gameObject);
        }

        // Bought in an earlier session: the path is simply open, without the price, the pop or the wait.
        private void OpenIfUnlocked()
        {
            Unlocks.Loaded -= OpenIfUnlocked;
            if (opened || !Unlocks.Has(cubeBool)) return;

            opened = true;
            HideBoard();
            GameStages.SetBool(cubeBool, true, this);
            OpenGate();
            Destroy(gameObject);
        }

        // Hit by something the player holds or threw, or by the player's hand.
        private void OnCollisionEnter(Collision collision)
        {
            if (opened || prices.Length == 0 || collision.relativeVelocity.magnitude < showSpeed) return;

            bool byPlayer = collision.collider.GetComponentInParent<XROrigin>() != null;
            bool byItem = collision.rigidbody != null && collision.rigidbody.GetComponentInParent<XRGrabInteractable>() != null;
            if (byPlayer || byItem) ShowBoard();
        }

        /// <summary>Shows the price board above the block.</summary>
        public void ShowBoard()
        {
            if (opened) return;
            if (board == null) BuildBoard();

            lastTouch = Time.time;
            if (!board.gameObject.activeSelf)
            {
                board.gameObject.SetActive(true);
                boardPlaced = false;
                SetStatus("", Color.white, 0f);
                inventory = PlayerInventory.Instance;
                if (inventory != null) inventory.Changed += OnInventoryChanged;
            }
            Refresh();
        }

        public void HideBoard()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            inventory = null;
            if (board != null) board.gameObject.SetActive(false);
        }

        /// <summary>
        /// Opens the path for good without paying: saves the unlock, turns Cube Bool on, opens the gate and breaks the
        /// block.
        /// </summary>
        public void Open()
        {
            if (opened) return;
            opened = true;
            HideBoard();

            Unlocks.Add(cubeBool);
            GameStages.SetBool(cubeBool, true, this);
            OpenGate();
            Break();
        }

        private void Update()
        {
            if (board == null || !board.gameObject.activeSelf) return;

            if (status.text.Length > 0 && Time.unscaledTime >= statusHideTime) status.text = "";
            if (FindHead() == null) return;

            var middle = Bounds().center;
            bool tooFar = (head.position - middle).sqrMagnitude > hideDistance * hideDistance;
            if (tooFar || Time.time - lastTouch > hideAfter) HideBoard();
        }

        private void LateUpdate()
        {
            if (board == null || !board.gameObject.activeSelf || FindHead() == null) return;

            // Above the block, over the edge on the player's side, so its top doesn't hide the board from up close.
            var bounds = Bounds();
            var toHead = head.position - bounds.center;
            toHead.y = 0f;
            toHead = toHead.sqrMagnitude > 0.0001f ? toHead.normalized : Vector3.forward;
            float edge = Mathf.Max(bounds.extents.x, bounds.extents.z);
            var target = new Vector3(bounds.center.x, bounds.max.y + boardHeight, bounds.center.z) + toHead * (edge + 0.05f);

            board.position = boardPlaced ? Vector3.Lerp(board.position, target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)) : target;
            boardPlaced = true;

            // Like the shop: a world-space canvas reads right when its forward points away from the viewer.
            var away = board.TransformPoint(board.rect.center) - head.position;
            if (away.sqrMagnitude > 0.0001f) board.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        private void OnPick(int index)
        {
            lastTouch = Time.time;
            var price = prices[index];
            if (price == null || price.item == null) return;

            if (inventory == null) inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                SetStatus("Can't pay right now", BadColor, StatusSeconds);
                return;
            }
            if (!inventory.TryRemove(price.item, price.amount))
            {
                int missing = price.amount - inventory.GetCount(price.item);
                SetStatus($"You need {missing} more {price.item.DisplayName}", BadColor, StatusSeconds);
                return;
            }

            Open();
        }

        private void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var price = prices[i];
                var row = rows[i];
                bool used = price != null && price.item != null;
                row.button.gameObject.SetActive(used);
                if (!used) continue;

                int have = inventory != null ? inventory.GetCount(price.item) : 0;
                row.icon.sprite = price.item.Icon;
                row.icon.enabled = row.icon.sprite != null;
                // "Yellow Seed   12/50": what the player has out of what it costs.
                row.label.text = price.item.DisplayName;
                row.cost.text = $"{have}/{price.amount}";
                row.cost.color = have >= price.amount ? EnoughColor : BadColor;
            }
        }

        private void OnInventoryChanged(ItemDefinition item, int change, int newCount)
        {
            Refresh();
        }

        private void SetStatus(string message, Color color, float seconds)
        {
            if (status == null) return;
            status.text = message;
            status.color = color;
            statusHideTime = Time.unscaledTime + seconds;
        }

        // Breaks it the way it normally breaks, so its pop sound plays; otherwise it just vanishes.
        private void Break()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                var health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                if (health == null) break;
                health.Value = 0f;
                return;
            }
            Destroy(gameObject);
        }

        // On the floor below the block's middle; part of the same stage, so it moves and goes away with it.
        private void OpenGate()
        {
            var bounds = Bounds();
            var floor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            // From the block's middle, which a ray can't hit from inside, so it finds the floor under it even if the block
            // sank into it a little.
            if (Physics.Raycast(bounds.center, Vector3.down, out var hit, bounds.extents.y + 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                floor = hit.point;
            floor += gateOffset;

            // A gate left from an earlier run in the same spot is still there.
            foreach (var existing in FindObjectsByType<PathGate>(FindObjectsSortMode.None))
            {
                if (existing.GameStagesBool == gateBool && (existing.transform.position - floor).sqrMagnitude < 1f) return;
            }

            var gate = new GameObject($"Path Gate ({gateBool})");
            gate.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            gate.transform.SetParent(transform.parent, true);
            gate.AddComponent<PathGate>().Configure(gateBool, gateRadius, gateSeconds, font, gateTickSound, gateDoneSound);

            if (gateEffect == null) return;
            var effect = Instantiate(gateEffect, gate.transform);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.identity;
            effect.transform.localScale = Vector3.one * gateEffectScale;
        }

        private Bounds Bounds()
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var c in colliders)
            {
                if (c == null || c.isTrigger) continue;
                if (any) bounds.Encapsulate(c.bounds);
                else bounds = c.bounds;
                any = true;
            }
            return bounds;
        }

        private Transform FindHead()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }

        private void BuildBoard()
        {
            float height = RowsTop + prices.Length * (RowHeight + RowSpacing) + StatusHeight;
            var root = new GameObject($"{name} Price Board", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) root.layer = uiLayer;

            // Not under the block: it would go with it, and the block's scale would stretch it.
            board = (RectTransform)root.transform;
            board.sizeDelta = new Vector2(BoardWidth, height);
            board.pivot = new Vector2(0.5f, 0f);
            board.localScale = Vector3.one * boardScale;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

            var background = CreateImage("Background", board, BoardColor);
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;

            Place(CreateText("Title", board, title, 30f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white), 14f, 10f, BoardWidth - 28f, 40f);
            Place(CreateText("Subtitle", board, subtitle, 19f, FontStyles.Italic, TextAlignmentOptions.Center, HintColor), 14f, 50f, BoardWidth - 28f, 28f);

            float rowWidth = BoardWidth - 24f;
            for (int i = 0; i < prices.Length; i++)
            {
                var line = CreateImage($"Pay {i + 1}", board, Color.white);
                line.raycastTarget = true;
                Place(line, 12f, RowsTop + i * (RowHeight + RowSpacing), rowWidth, RowHeight);

                var button = line.gameObject.AddComponent<Button>();
                button.targetGraphic = line;
                button.colors = RowColors();
                int index = i;
                button.onClick.AddListener(() => OnPick(index));

                var icon = CreateImage("Icon", line.transform, Color.white);
                icon.preserveAspect = true;
                Place(icon, 8f, 4f, 48f, 48f);
                var label = CreateText("Name", line.transform, "", 24f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);
                Place(label, 66f, 2f, rowWidth - 66f - 124f, RowHeight - 4f);
                var cost = CreateText("Price", line.transform, "", 26f, FontStyles.Bold, TextAlignmentOptions.Right, Color.white);
                Place(cost, rowWidth - 122f, 2f, 112f, RowHeight - 4f);
                // Shrinks to fit big numbers like 1234/50.
                cost.fontSizeMax = 26f;
                cost.fontSizeMin = 14f;
                cost.enableAutoSizing = true;

                rows.Add(new Row { button = button, icon = icon, label = label, cost = cost });
            }

            status = CreateText("Status", board, "", 19f, FontStyles.Italic, TextAlignmentOptions.Center, Color.white);
            Place(status, 12f, height - StatusHeight, BoardWidth - 24f, StatusHeight - 6f);

            root.SetActive(false);
        }

        // The line is the button: faint until pointed at or poked, like the shop's rows.
        private static ColorBlock RowColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(1f, 1f, 1f, 0.08f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.2f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.32f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
            return colors;
        }

        // Puts a graphic 'left' and 'top' pixels in from its parent's top-left corner.
        private static void Place(Graphic graphic, float left, float top, float width, float height)
        {
            var rect = graphic.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateText(string objectName, Transform parent, string text, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.richText = true;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var cols = GetComponentsInChildren<Collider>();
            if (cols.Length == 0) return;
            var bounds = cols[0].bounds;
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
            DrawCircle(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + gateOffset, gateRadius);
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            const int segments = 32;
            var previous = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
