using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The whack-a-pumpkin machine (Special Stage Type 4, see WhackRound): a half-ring table round the spot the player
    /// stands on (this object's origin, facing +Z), with 20 holes (a pumpkin head in each: Heads), two posts the mallets
    /// wait on (Hammer Spots) and the scoreboard behind the far row (its texts). It rises out of the floor when the chest
    /// opens and sinks back when the round is over. The setup menu (Tools > 429 Game > Bonus > Set Up Whack-a-Pumpkin)
    /// builds it: a stand-in model, or the user's model with empties Hole_00 ... Hole_19 at the hole tops.
    /// </summary>
    [DisallowMultipleComponent]
    public class WhackMachine : MonoBehaviour
    {
        [Tooltip("The part that rises and sinks (the table, heads, posts and board).")]
        [SerializeField] private Transform body;
        [Tooltip("The pumpkin heads, one per hole.")]
        [SerializeField] private WhackHead[] heads = System.Array.Empty<WhackHead>();
        [Tooltip("Where the two mallets wait (left, right).")]
        [SerializeField] private Transform[] hammerSpots = System.Array.Empty<Transform>();
        [Tooltip("Where the candies come out at the end (the top of the board).")]
        [SerializeField] private Transform candySpout;
        [Tooltip("The glowing ring on the spot to stand on.")]
        [SerializeField] private GameObject standRing;

        [Header("Scoreboard")]
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text candiesText;
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private TMP_Text bestText;
        [SerializeField] private TMP_Text messageText;

        [Header("Rise and sink")]
        [Tooltip("How far under the floor it waits, and goes back (metres).")]
        [Min(0f)]
        [SerializeField] private float depth = 1.3f;
        [Min(0.05f)]
        [SerializeField] private float riseSeconds = 0.7f;
        [Min(0.05f)]
        [SerializeField] private float sinkSeconds = 0.9f;

        public WhackHead[] Heads => heads;
        public Transform[] HammerSpots => hammerSpots;
        public Vector3 CandySpout => candySpout != null ? candySpout.position : transform.position + Vector3.up * 2f;

        /// <summary>Rising or sinking now.</summary>
        public bool Moving { get; private set; }

        private void Awake()
        {
            if (body == null) body = transform;
            Show(messageText, "");
        }

        /// <summary>Comes up out of the floor (a little past, then settles).</summary>
        public IEnumerator Rise()
        {
            Moving = true;
            for (float t = 0f; t < riseSeconds; t += Time.deltaTime)
            {
                float k = t / riseSeconds;
                float y = Mathf.Lerp(-depth, 0f, 1f - (1f - k) * (1f - k)) + Mathf.Sin(k * Mathf.PI) * 0.06f;
                SetHeight(y);
                yield return null;
            }
            SetHeight(0f);
            Moving = false;
        }

        /// <summary>Goes back down into the floor, shaking.</summary>
        public IEnumerator Sink()
        {
            Moving = true;
            if (standRing != null) standRing.SetActive(false);
            for (float t = 0f; t < sinkSeconds; t += Time.deltaTime)
            {
                float k = t / sinkSeconds;
                SetHeight(Mathf.Lerp(0f, -depth, k * k));
                if (body != transform) body.localPosition += new Vector3(Mathf.Sin(t * 70f) * 0.01f, 0f, 0f);
                yield return null;
            }
            SetHeight(-depth);
            Moving = false;
        }

        /// <summary>Hidden under the floor (before rising).</summary>
        public void Hide() => SetHeight(-depth);

        private void SetHeight(float y)
        {
            if (body == transform) return;
            body.localPosition = new Vector3(0f, y, 0f);
        }

        public void ShowRing(bool on)
        {
            if (standRing != null) standRing.SetActive(on);
        }

        // ---------- The scoreboard ----------

        public void ShowTime(float seconds, bool warn)
        {
            if (timeText == null) return;
            timeText.text = Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString();
            timeText.color = warn ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.3f);
        }

        public void ShowCandies(int candies) => Show(candiesText, "CANDIES  " + candies);

        public void ShowCombo(int combo) => Show(comboText, combo >= 2 ? "COMBO x" + combo : "");

        public void ShowBest(int best) => Show(bestText, best > 0 ? "BEST  " + best : "");

        public void ShowMessage(string message, Color colour)
        {
            if (messageText == null) return;
            messageText.text = message;
            messageText.color = colour;
        }

        private static void Show(TMP_Text text, string value)
        {
            if (text != null) text.text = value;
        }
    }
}
