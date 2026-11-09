using System.Collections;
using System.Collections.Generic;
using Game.Bonus;
using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// The cooking game's first-clear prize (user, 2026-11-06): the first time a save slot breaks the mystery pot, the
    /// player gets Stat Points for good (spent in the Level Up menu like a level's; saved), with a big firework, a
    /// miracle sound and a notice. Later clears give only the pot's usual candies. "First time" is kept per save slot
    /// (ShowOnce, like the tutorials). The round chests' own candy + EXP bursts are in their Open_Chest states (Play End
    /// Game Jackpot + Spill Candies). Goes on the cauldron (Fire_Woods_n_Cauldron); Tools > 429 Game > PlayGround >
    /// Set Up Cooking Rewards sets it all up.
    /// The broken pot starts the Daruma game (Special Stage 3 Trigger), so the notice (user, 2026-11-07) waits for it and
    /// shows only after a win, on the rooftop; after a loss (a death and a new run) there's none. The point is kept.
    /// </summary>
    public class CookingRewards : MonoBehaviour
    {
        [Tooltip("Kept in the save slot once given (a new save slot can win it again).")]
        [SerializeField] private string firstClearKey = "CookingGameFirstClear";
        [Min(1)]
        [SerializeField] private int statPoints = 1;
        [SerializeField] private GameObject effect;
        [SerializeField] private AudioClip sound;
        [Tooltip("Seconds after the pot bursts before the notice shows (the burst and candies first).")]
        [Min(0f)]
        [SerializeField] private float noticeDelay = 2.5f;
        [Tooltip("Seconds after a won Daruma game (the player on the rooftop) before the notice shows (the lid bursts first).")]
        [Min(0f)]
        [SerializeField] private float afterDaruma = 4f;
        [SerializeField] private string title = "COOKING MASTER!";
        [TextArea]
        [SerializeField] private string body = "+1 stat point, yours to keep.";
        [SerializeField] private string closeLine = "Bump your fists to spend it";

        /// <summary>Raised when the first-clear prize is given (Play tests).</summary>
        public static event System.Action Given;

        private void OnEnable() => Cauldron.PotBroken += OnPotBroken;
        private void OnDisable() => Cauldron.PotBroken -= OnPotBroken;

        private void OnPotBroken(Cauldron cauldron)
        {
            if (Game.Menus.ShowOnce.WasShown(firstClearKey)) return;
            Game.Menus.ShowOnce.Remember(firstClearKey);
            Game.Leveling.PlayerLevel.GiveStatPoints(statPoints);
            var at = cauldron != null ? cauldron.transform.position : transform.position;
            if (effect != null) Destroy(Instantiate(effect, at + Vector3.up * 1.5f, Quaternion.identity), 8f);
            if (sound != null) AudioSource.PlayClipAtPoint(sound, at, 1f);
            Given?.Invoke();
            // The cauldron is destroyed once its pot bursts: the notice waits on something that stays.
            var runner = new GameObject("Cooking Rewards Notice").AddComponent<NoticeLater>();
            runner.Begin(noticeDelay, afterDaruma, title, body, closeLine);
        }

        private class NoticeLater : MonoBehaviour
        {
            // The Daruma game's end, as soon as it's known (won or not).
            private bool ended, won;

            public void Begin(float delay, float afterDaruma, string title, string body, string closeLine)
            {
                DarumaStage.Ended += OnDarumaEnded;
                StartCoroutine(Run(delay, afterDaruma, title, body, closeLine));
            }

            private void OnDestroy() => DarumaStage.Ended -= OnDarumaEnded;

            private void OnDarumaEnded(DarumaStage stage, bool win)
            {
                ended = true;
                won = win;
            }

            private IEnumerator Run(float delay, float afterDaruma, string title, string body, string closeLine)
            {
                // The pot's break starts the Daruma game in the same frame (whichever hears Pot Broken first): known by now.
                yield return null;
                bool daruma = DarumaStage.Running != null;
                yield return new WaitForSeconds(delay);
                // The Daruma game the pot started: only a win shows the notice, once the player stands on the rooftop. A
                // loss (death, a new run) or the stage cleared by a Game Over (gone without an end, even before now): none.
                if (daruma || ended || DarumaStage.Running != null)
                {
                    while (!ended && DarumaStage.Running != null) yield return null;
                    if (!won)
                    {
                        Destroy(gameObject);
                        yield break;
                    }
                    yield return new WaitForSeconds(afterDaruma);
                }
                var notice = Game.Menus.TutorialNotice.Find();
                // After any other notice that's up (a level up from the pot's EXP, say).
                for (float t = 0f; t < 30f && notice != null; t += 0.25f)
                {
                    var levelUp = FindAnyObjectByType<Game.Leveling.LevelUpNotice>();
                    if (!notice.IsShowing && (levelUp == null || !levelUp.IsShowing)) break;
                    yield return new WaitForSeconds(0.25f);
                }
                if (notice != null)
                    notice.Show(new List<Game.Menus.TutorialNotice.Page> { new Game.Menus.TutorialNotice.Page(title, body, null) }, closeLine);
                Destroy(gameObject);
            }
        }
    }
}
