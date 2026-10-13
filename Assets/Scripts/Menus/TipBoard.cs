using TMPro;
using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// A board with a tip on it, above the knight's weapon in the middle of the path at the start. The first time it
    /// shows after the game is launched it says the First Tip, with the First Tip Hint under it in smaller letters (how to
    /// set the height in the pause menu); every time after that (the knight is back after the
    /// player died, or the game started over after the boss) it says a tip picked at random from Tips, never the same
    /// one twice in a row. Goes on the knight (Knight prefab). The board stays where it was when the knight appeared,
    /// even if the knight is knocked about, fades away once the player has walked past it, and goes away with the knight.
    /// Tools > 429 Game > Menu > Set Up Knight Tip Board builds it.
    /// </summary>
    public class TipBoard : MonoBehaviour
    {
        [Tooltip("The board (a world-space canvas, under this object until the game starts).")]
        [SerializeField] private RectTransform board;
        [SerializeField] private TMP_Text text;
        [Tooltip("The board fades away once the player's head is this far past it (in metres, beyond its face), and stays " +
                 "off until the knight is back. Below 0: it never fades.")]
        [SerializeField] private float passedBy = 0.3f;
        [Tooltip("Seconds the board takes to fade away.")]
        [Min(0.01f)]
        [SerializeField] private float fadeSeconds = 0.5f;

        private Transform head;
        private CanvasGroup fade;
        private bool passed;

        [Tooltip("Shown the first time, each time the game is launched.")]
        [TextArea(1, 3)]
        [SerializeField] private string firstTip = "You should be this High";
        [Tooltip("Shown under the First Tip, smaller: how to fix the height (the pause menu's Height option). Empty: none.")]
        [TextArea(1, 3)]
        [SerializeField] private string firstTipHint = "Not this high? You can change your height in the menu.";
        [Tooltip("How big the hint's letters are, as a share of the tip's.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float hintSize = 0.55f;
        [Tooltip("One is picked at random every time after the first.")]
        [TextArea(1, 3)]
        [SerializeField] private string[] tips =
        {
            "Eat as much candies as you want, this is a game",
            "Nothing to throw? Make a fist and start punching",
            "Clean your room and watch out for the bug bite",
            "Don't run, Don't Give Up, Don't Fear",
            "Remember to drink more water",
            "Bump your fists together twice to spend your level-up points",
            "Shake a bottle before you throw it. Not too long!",
            "Hold B to open the Candy Shop",
            "An uppercut to the head knocks them out",
        };

        // Once per launch: the game can start over in the same scene (after dying, or after the boss).
        private static bool firstShown;
        private static int lastTip = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            firstShown = false;
            lastTip = -1;
        }

        private void Start()
        {
            // Left where it is, so a knight knocked about doesn't swing it around; next to the knight, so it's cleared
            // away with everything else the game spawned.
            if (board != null) board.SetParent(transform.parent, true);
            if (text != null) text.text = PickTip();
        }

        // Once the player is past the board (a world-space canvas is read from behind its forward), it fades away.
        private void Update()
        {
            if (board == null || passedBy < 0f || !board.gameObject.activeSelf) return;
            if (!passed)
            {
                if (head == null && Camera.main != null) head = Camera.main.transform;
                if (head == null || Vector3.Dot(head.position - board.position, board.forward) < passedBy) return;
                passed = true;
                if (!board.TryGetComponent(out fade)) fade = board.gameObject.AddComponent<CanvasGroup>();
            }
            fade.alpha = Mathf.MoveTowards(fade.alpha, 0f, Time.deltaTime / fadeSeconds);
            if (fade.alpha <= 0f) board.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (board != null) Destroy(board.gameObject);
        }

        private string PickTip()
        {
            if (!firstShown || tips == null || tips.Length == 0)
            {
                firstShown = true;
                if (string.IsNullOrEmpty(firstTipHint)) return firstTip;
                // The tip kept on one line, the hint on its own lines under it in smaller letters.
                return $"<nobr>{firstTip}</nobr>\n<size={Mathf.RoundToInt(hintSize * 100f)}%>{firstTipHint}</size>";
            }

            int pick = Random.Range(0, tips.Length);
            // Never the same tip twice in a row.
            if (tips.Length > 1 && pick == lastTip) pick = (pick + 1 + Random.Range(0, tips.Length - 1)) % tips.Length;
            lastTip = pick;
            return tips[pick];
        }
    }
}
