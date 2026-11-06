using System.Collections;
using System.Collections.Generic;
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
            runner.Begin(noticeDelay, title, body, closeLine);
        }

        private class NoticeLater : MonoBehaviour
        {
            public void Begin(float delay, string title, string body, string closeLine) =>
                StartCoroutine(Run(delay, title, body, closeLine));

            private IEnumerator Run(float delay, string title, string body, string closeLine)
            {
                yield return new WaitForSeconds(delay);
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
