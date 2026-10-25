using Game.Saving;
using TMPro;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The board shown when a bonus round is over: how many punches the pumpkins took and how many were perfect jabs,
    /// over the player's leaderboard (BonusScores, saved), this round's row lit up. It stands where the player looks,
    /// then fades away by itself (the stages go on meanwhile). Bonus Round makes it (Show).
    /// </summary>
    public class BonusBoard : MonoBehaviour
    {
        private const float Width = 560f;
        private const float Height = 440f;
        // Metres per panel pixel: about a metre wide.
        private const float Scale = 0.0022f;
        // How far in front of the player it stands, in metres.
        private const float Distance = 1.6f;
        // ... at the nearest (a wall in the way), and how far clear of the wall.
        private const float MinDistance = 0.6f;
        private const float WallGap = 0.15f;
        private const float FadeSeconds = 0.6f;
        private const float RowTop = 196f;
        private const float RowHeight = 44f;
        // The columns: left edge and width, in panel pixels.
        private static readonly Vector2[] Columns = { new(20f, 60f), new(80f, 150f), new(230f, 150f), new(380f, 160f) };
        private static readonly Color Background = new(0.08f, 0.05f, 0.12f, 0.85f);
        private static readonly Color Gold = new(1f, 0.82f, 0.25f, 1f);
        private static readonly Color Pale = new(1f, 1f, 1f, 0.55f);
        private static readonly Color Lit = new(1f, 0.82f, 0.25f, 0.18f);

        private RectTransform panel;
        private CanvasGroup group;
        private Transform head;
        private float shownAt;
        private float showFor;

        /// <summary>
        /// Shows the board for this many seconds: this round's punches and perfect jabs, and the leaderboard with its
        /// place on it lit up (0 = the best; -1: it didn't make it).
        /// </summary>
        public static BonusBoard Show(int punches, int perfectJabs, int place, float seconds, TMP_FontAsset font)
        {
            var panel = WorldUI.CreatePanel("Bonus Board", Width, Height, Scale, Background);
            var board = panel.gameObject.AddComponent<BonusBoard>();
            board.panel = panel;
            board.group = panel.gameObject.AddComponent<CanvasGroup>();
            board.group.alpha = 0f;
            board.shownAt = Time.time;
            board.showFor = Mathf.Max(seconds, FadeSeconds * 2f);
            board.head = Camera.main != null ? Camera.main.transform : null;
            board.Build(punches, perfectJabs, place, font != null ? font : TMP_Settings.defaultFontAsset);
            board.PlaceInFront();
            // A punch shatters it: gone at once, the shards take its place.
            Game.Menus.PunchToShatter.Add(panel, () => Destroy(board.gameObject));
            return board;
        }

        private void Build(int punches, int perfectJabs, int place, TMP_FontAsset font)
        {
            bool newBest = place == 0 && BonusScores.Best.Count > 1;
            Label(newBest ? "NEW BEST!" : "PUMPKIN BONUS", 0f, 12f, Width, 60f, 50f, Gold, FontStyles.Bold, font);
            Label($"Punches {punches}    Perfect Jabs {perfectJabs}", 30f, 80f, Width - 60f, 50f, 36f, Color.white, FontStyles.Bold, font);
            WorldUI.Place(WorldUI.CreateImage("Line", panel, Pale), 30f, 145f, Width - 60f, 3f);

            string[] titles = { "#", "Pumpkins", "Punches", "Perfect Jabs" };
            for (int c = 0; c < Columns.Length; c++)
                Label(titles[c], Columns[c].x, 158f, Columns[c].y, 34f, 24f, Pale, FontStyles.Normal, font);

            var best = BonusScores.Best;
            for (int i = 0; i < BonusScores.Kept; i++)
            {
                float top = RowTop + i * RowHeight;
                if (i == place) WorldUI.Place(WorldUI.CreateImage("Lit", panel, Lit), 20f, top - 2f, Width - 40f, RowHeight - 2f);
                var color = i == place ? Gold : Color.white;
                bool has = i < best.Count;
                var e = has ? best[i] : null;
                string[] cells =
                {
                    $"{i + 1}",
                    has ? $"{e.pumpkins}/{e.pumpkinCount}" : "-",
                    has ? $"{e.punches}" : "-",
                    has ? $"{e.perfectJabs}" : "-",
                };
                for (int c = 0; c < Columns.Length; c++)
                    Label(cells[c], Columns[c].x, top, Columns[c].y, RowHeight - 4f, 32f, has ? color : Pale, FontStyles.Bold, font);
            }
        }

        private void Label(string text, float left, float top, float width, float height, float size, Color color, FontStyles style, TMP_FontAsset font)
        {
            var label = WorldUI.CreateText("Text", panel, text, size, style, TextAlignmentOptions.Center, color, font);
            WorldUI.Place(label, left, top, width, height);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = size * 0.5f;
            label.fontSizeMax = size;
        }

        // Where the player looks (level), its middle at their eye height. A wall or furniture nearer than that: just in
        // front of it, smaller, so it looks the same.
        private void PlaceInFront()
        {
            if (head == null) return;
            var ahead = head.forward;
            ahead.y = 0f;
            ahead = ahead.sqrMagnitude < 0.0001f ? Vector3.forward : ahead.normalized;
            float distance = Distance;
            if (Physics.Raycast(head.position, ahead, out var hit, Distance, LayerMask.GetMask("Wall", "Furniture"), QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(MinDistance, hit.distance - WallGap);
            float scale = Scale * distance / Distance;
            panel.localScale = Vector3.one * scale;
            panel.position = head.position + ahead * distance - Vector3.up * (Height * scale * 0.5f);
            WorldUI.Face(panel, head);
        }

        private void LateUpdate()
        {
            float age = Time.time - shownAt;
            if (age >= showFor)
            {
                Destroy(gameObject);
                return;
            }
            group.alpha = Mathf.Clamp01(Mathf.Min(age, showFor - age) / FadeSeconds);
            if (head == null && Camera.main != null) head = Camera.main.transform;
            WorldUI.Face(panel, head);
        }
    }
}
