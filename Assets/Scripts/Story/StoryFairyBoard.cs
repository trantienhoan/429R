using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Game.Story
{
    /// <summary>
    /// The free Fairy's board (user 2026-11-25): once the player has beaten the final boss, she waits free on the start
    /// rooftop. Touching her (a hand, or the head close by) brings up "Choose your path" above her: one big picture card
    /// showing where the choice leads, its name on it, and arrows on its left and right to go through the choices (user:
    /// "less text, more image"; "arrows to move to gate 1 2 3 on left and right side of the picture The Path"). Point and
    /// click, or poke, the card to take it:
    /// - The Path (always, shown first; the storage room): the first chest's seed (Seed_Bracer) appears in front of her
    ///   and the start pumpkin counts as broken (GAMESTAGES Pumpkin_0), so taking the seed starts the game, no pumpkin.
    /// - Gate 1, 2, 3: only once the player has bought that stone path (Cube_N, by hitting the blocking stone and paying
    ///   it); locked gates are not in the choices. She is a teleporter: taking one starts the gate at once (its Gate_N
    ///   bool on), with no 3 s wait on the portal. Standing in the portal still works. The gates still being made show a
    ///   "???" picture (user: "just do some stupid image ??? ??? ???").
    /// - Only The Path (no gate bought): no arrows; under the card a stone's picture and one short line on how to unlock
    ///   a gate. With gates: small dots under the card show which choice of how many is up.
    /// The pictures are in Resources/FairyBoard (made by Exports/Story/make_fairy_board_pictures.py): Path, Gate_1-3,
    /// Stone, Arrow. Swap a PNG to change a card. Added by Story Start (Dress) when she is made free.
    /// </summary>
    public class StoryFairyBoard : MonoBehaviour
    {
        [Tooltip("The first chest's seed she gives (Seed_Bracer).")]
        public GameObject SeedPrefab;
        [Tooltip("A hand this close to her (metres) touches her.")]
        public float handReach = 0.45f;
        [Tooltip("The head this close to her along the floor (metres) also counts (she is small, on the floor).")]
        public float headReach = 0.9f;
        [Tooltip("The board goes away when the player is this far (metres).")]
        public float hideDistance = 4f;
        [Tooltip("How high above her feet (her root) the board's bottom floats (metres); she flies up to about 1 m.")]
        public float boardHeight = 1.08f;
        [Tooltip("Metres per board pixel.")]
        public float boardScale = 0.0022f;

        // Board layout in board pixels (BoardWidth x boardScale = 0.92 m wide).
        private const float BoardWidth = 420f;
        private const float Pad = 12f;
        private const float TitleHeight = 44f;
        private const float ArrowWidth = 44f;
        private const float ArrowHeight = 76f;
        private const float ArrowGap = 8f;
        private const float CardLeft = Pad + ArrowWidth + ArrowGap;
        private const float CardWidth = BoardWidth - 2f * CardLeft;
        private const float CardHeight = CardWidth * 9f / 16f;
        private const float CardTop = Pad + TitleHeight;
        private const float Gap = 10f;
        private const float DotSize = 10f;
        private const float DotSpacing = 20f;
        private const float Border = 3f;
        private const float HintHeight = 76f;

        private static readonly Color BoardColor = new Color(0.07f, 0.14f, 0.09f, 0.92f);
        private static readonly Color BandColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color HintColor = new Color(0.8f, 0.9f, 0.82f);
        // A card's frame and the arrows show these: dim, lit when pointed at, bright when pressed.
        private static readonly Color FrameColor = new Color(0.3f, 0.5f, 0.34f);
        private static readonly Color ArrowColor = new Color(0.62f, 0.82f, 0.64f);
        private static readonly Color Lit = new Color(0.75f, 1f, 0.6f);
        private static readonly Color Pressed = new Color(1f, 0.95f, 0.55f);
        private static readonly Color DotOff = new Color(0.3f, 0.45f, 0.33f);

        // The user's words: the title, the choices' names (The Path first), the hint.
        private const string Title = "Choose your path";
        private static readonly string[] Names = { "The Path", "Gate 1", "Gate 2", "Gate 3" };
        private static readonly string[] Pictures = { "FairyBoard/Path", "FairyBoard/Gate_1", "FairyBoard/Gate_2", "FairyBoard/Gate_3" };
        private const string StonePicture = "FairyBoard/Stone";
        private const string ArrowPicture = "FairyBoard/Arrow";
        private const string Hint = "Hit a stone\nto unlock a gate";

        private RectTransform board;
        private RawImage cardPicture;
        private TMP_Text cardName;
        private GameObject leftArrow;
        private GameObject rightArrow;
        private RectTransform dots;
        private readonly List<Image> dotImages = new List<Image>();
        private RectTransform hint;
        private readonly Texture2D[] textures = new Texture2D[4];
        private readonly List<int> choices = new List<int>();
        private int showing;
        private Transform head;
        private Transform[] hands;
        private bool seedGiven;
        private bool placed;
        private int shownMask = -1;

        private void Update()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;
            if (hands == null) hands = FindHands();
            var middle = Middle();
            if (Touching(middle)) Show();
            if (board != null && board.gameObject.activeSelf)
            {
                var flat = head.position - middle;
                flat.y = 0f;
                if (flat.magnitude > hideDistance) board.gameObject.SetActive(false);
                Refresh();
            }
        }

        private void LateUpdate()
        {
            if (board == null || !board.gameObject.activeSelf || head == null) return;
            // Over her, clear of her head while she flies up and down.
            var middle = Middle();
            var target = new Vector3(middle.x, transform.position.y + boardHeight, middle.z);
            board.position = placed ? Vector3.Lerp(board.position, target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)) : target;
            placed = true;
            // A world-space canvas reads right when its forward points away from the viewer.
            var away = board.TransformPoint(board.rect.center) - head.position;
            if (away.sqrMagnitude > 0.0001f) board.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        private void OnDestroy()
        {
            if (board != null) Destroy(board.gameObject);
        }

        private bool Touching(Vector3 middle)
        {
            var flat = head.position - middle;
            flat.y = 0f;
            if (flat.magnitude < headReach) return true;
            if (hands == null) return false;
            foreach (var hand in hands)
                if (hand != null && (hand.position - middle).magnitude < handReach) return true;
            return false;
        }

        // Where she really is: her skinned body's bounds, kept up to date (the stored bounds stay where the model was
        // made, about 1 m from where her animation flies her, so the board and hand touches missed her).
        private Vector3 Middle()
        {
            bool any = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Fairy_Girl")) continue;
                foreach (var look in child.GetComponentsInChildren<Renderer>())
                {
                    if (look is SkinnedMeshRenderer skin && !skin.updateWhenOffscreen) skin.updateWhenOffscreen = true;
                    if (!any) { bounds = look.bounds; any = true; }
                    else bounds.Encapsulate(look.bounds);
                }
            }
            return any ? bounds.center : transform.position + Vector3.up * 0.3f;
        }

        private static Transform[] FindHands()
        {
            var found = new List<Transform>();
            foreach (var name in new[] { "Left Controller", "Right Controller" })
            {
                var go = GameObject.Find(name);
                if (go != null) found.Add(go.transform);
            }
            return found.ToArray();
        }

        private void Show()
        {
            if (board == null) Build();
            if (board.gameObject.activeSelf) return;
            board.gameObject.SetActive(true);
            placed = false;
            shownMask = -1;
            showing = 0;
            Refresh();
        }

        // The choices are The Path and the bought gates; the arrows, dots or hint and the board's height follow.
        private void Refresh()
        {
            int mask = 1;
            for (int gate = 1; gate <= 3; gate++)
                if (GameStages.GetBool("Cube_" + gate)) mask |= 1 << gate;
            if (mask == shownMask) return;
            shownMask = mask;

            choices.Clear();
            for (int i = 0; i < Names.Length; i++)
                if ((mask & (1 << i)) != 0) choices.Add(i);
            if (!choices.Contains(showing)) showing = 0;

            bool more = choices.Count > 1;
            leftArrow.SetActive(more);
            rightArrow.SetActive(more);
            dots.gameObject.SetActive(more);
            hint.gameObject.SetActive(!more);

            float y = CardTop + CardHeight + Gap;
            if (more)
            {
                for (int i = 0; i < dotImages.Count; i++)
                {
                    bool on = i < choices.Count;
                    dotImages[i].gameObject.SetActive(on);
                    if (!on) continue;
                    var rect = dotImages[i].rectTransform;
                    rect.anchoredPosition = new Vector2((i - (choices.Count - 1) / 2f) * DotSpacing, 0f);
                }
                Place(dots, 0f, y, BoardWidth, DotSize * 1.5f);
                y += DotSize * 1.5f;
            }
            else
            {
                Place(hint, CardLeft, y, CardWidth, HintHeight);
                y += HintHeight;
            }
            board.sizeDelta = new Vector2(BoardWidth, y + Pad);
            ShowCard();
        }

        // Goes to the next choice (1) or the one before (-1), round and round.
        private void Step(int direction)
        {
            if (choices.Count < 2) return;
            int at = Mathf.Max(0, choices.IndexOf(showing));
            showing = choices[(at + direction + choices.Count) % choices.Count];
            ShowCard();
        }

        private void ShowCard()
        {
            cardPicture.texture = textures[showing];
            cardPicture.color = textures[showing] != null ? Color.white : new Color(0.12f, 0.16f, 0.13f);
            cardName.text = Names[showing];
            int at = choices.IndexOf(showing);
            for (int i = 0; i < dotImages.Count; i++)
                dotImages[i].color = i == at ? Lit : DotOff;
        }

        private void OnPick(int index)
        {
            if (index == 0)
            {
                GiveSeed();
                return;
            }
            // The teleport: the gate starts at once, as if the player had stood on its portal for its 3 seconds.
            if (GameStages.GetBool("Cube_" + index)) GameStages.SetBool("Gate_" + index, true, this);
        }

        private void GiveSeed()
        {
            if (seedGiven || SeedPrefab == null) return;
            seedGiven = true;
            // In front of her, toward the player, at hand height.
            var middle = Middle();
            var toward = head.position - middle;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;
            var at = new Vector3(middle.x, Mathf.Max(middle.y, transform.position.y) + 0.9f, middle.z) + toward * 0.5f;
            Instantiate(SeedPrefab, at, Quaternion.identity);
            // No start pumpkin this time: it counts as broken, so taking the seed starts the game.
            GameStages.SetBool("Pumpkin_0", true, this);
        }

        private void Build()
        {
            var root = new GameObject("Fairy Board", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) root.layer = uiLayer;
            board = (RectTransform)root.transform;
            board.sizeDelta = new Vector2(BoardWidth, CardTop + CardHeight + Pad);
            board.pivot = new Vector2(0.5f, 0f);
            board.localScale = Vector3.one * boardScale;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Drawn after other see-through things (the gates' portal glows far behind it showed through the cards).
            canvas.sortingOrder = 20;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            for (int i = 0; i < Pictures.Length; i++)
                textures[i] = Resources.Load<Texture2D>(Pictures[i]);

            Fill(MakeImage("Background", board, BoardColor).rectTransform, 0f);
            Place(MakeText("Title", board, Title, 28f, FontStyles.Bold, Color.white).rectTransform, Pad, 6f, BoardWidth - 2f * Pad, TitleHeight);

            // The card: a frame (the button; only its edge shows and lights when pointed at), the picture of where the
            // choice leads, and its name on a dark band along the picture's bottom.
            var frame = MakeImage("Card", board, Color.white);
            frame.raycastTarget = true;
            Place(frame.rectTransform, CardLeft, CardTop, CardWidth, CardHeight);
            Clickable(frame.gameObject, frame, FrameColor, () => OnPick(showing));
            cardPicture = MakePicture("Picture", frame.transform, null);
            Fill(cardPicture.rectTransform, Border);
            var band = MakeImage("Name Band", cardPicture.transform, BandColor);
            band.rectTransform.anchorMin = Vector2.zero;
            band.rectTransform.anchorMax = new Vector2(1f, 0.2f);
            band.rectTransform.offsetMin = Vector2.zero;
            band.rectTransform.offsetMax = Vector2.zero;
            cardName = MakeText("Name", band.transform, Names[0], 26f, FontStyles.Bold, Color.white);
            cardName.enableAutoSizing = true;
            cardName.fontSizeMin = 10f;
            cardName.fontSizeMax = 26f;
            Fill(cardName.rectTransform, 1f);

            // The arrows, one each side of the card, halfway down it.
            float arrowTop = CardTop + (CardHeight - ArrowHeight) / 2f;
            leftArrow = MakeArrow("Left Arrow", Pad, arrowTop, true, () => Step(-1));
            rightArrow = MakeArrow("Right Arrow", BoardWidth - Pad - ArrowWidth, arrowTop, false, () => Step(1));

            // Dots under the card: one per choice, the one showing lit.
            var dotsGo = new GameObject("Dots", typeof(RectTransform));
            dotsGo.layer = board.gameObject.layer;
            dotsGo.transform.SetParent(board, false);
            dots = (RectTransform)dotsGo.transform;
            for (int i = 0; i < Names.Length; i++)
            {
                var dot = MakeImage("Dot", dots, DotOff);
                var rect = dot.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(DotSize, DotSize);
                rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                dotImages.Add(dot);
            }

            // The hint: a blocking stone's picture and one short line beside it.
            var hintGo = new GameObject("Hint", typeof(RectTransform));
            hintGo.layer = board.gameObject.layer;
            hintGo.transform.SetParent(board, false);
            hint = (RectTransform)hintGo.transform;
            var stoneFrame = MakeImage("Stone", hint, FrameColor);
            Place(stoneFrame.rectTransform, 0f, 0f, HintHeight, HintHeight);
            Fill(MakePicture("Picture", stoneFrame.transform, Resources.Load<Texture2D>(StonePicture)).rectTransform, Border);
            var hintText = MakeText("Text", hint, Hint, 22f, FontStyles.Normal, HintColor);
            hintText.alignment = TextAlignmentOptions.MidlineLeft;
            hintText.enableWordWrapping = true;
            Place(hintText.rectTransform, HintHeight + 12f, 0f, CardWidth - HintHeight - 12f, HintHeight);

            root.SetActive(false);
        }

        private GameObject MakeArrow(string objectName, float left, float top, bool pointsLeft, UnityEngine.Events.UnityAction onClick)
        {
            var arrow = MakePicture(objectName, board, Resources.Load<Texture2D>(ArrowPicture));
            arrow.raycastTarget = true;
            // The picture points right; the left one is the same, flipped.
            if (pointsLeft) arrow.uvRect = new Rect(1f, 0f, -1f, 1f);
            Place(arrow.rectTransform, left, top, ArrowWidth, ArrowHeight);
            Clickable(arrow.gameObject, arrow, ArrowColor, onClick);
            return arrow.gameObject;
        }

        private static void Clickable(GameObject go, Graphic target, Color normal, UnityEngine.Events.UnityAction onClick)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = target;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.highlightedColor = Lit;
            colors.pressedColor = Pressed;
            colors.selectedColor = normal;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
        }

        private static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Fill(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Image MakeImage(string objectName, Transform parent, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // A picture from Resources; a missing one shows as a dark card, so only its name tells what it is.
        private static RawImage MakePicture(string objectName, Transform parent, Texture2D texture)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var picture = go.AddComponent<RawImage>();
            picture.texture = texture;
            picture.color = Color.white;
            picture.raycastTarget = false;
            return picture;
        }

        private static TMP_Text MakeText(string objectName, Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
    }
}
