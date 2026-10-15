using System.Collections.Generic;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Menus")]
    [Tooltip("Shows a tutorial in pages (the scene's Tutorial Notice): each page has a title, a picture (or none) and some " +
             "text; a trigger turns the page, and on the last page closes it. The game pauses while it's up. The first " +
             "Pages Above pages float above Above (e.g. the enemy), with an arrow pointing down at it; the rest show in " +
             "front of the player. Show After waits first (game time), e.g. for a laugh to end. Then Finish Event is sent " +
             "once it has gone (also when it isn't shown: Once Key already used, or no Tutorial Notice in the scene).")]
    public class ShowNotice : FsmStateAction
    {
        [Tooltip("Each page's title (the big line at the top). One entry per page.")]
        public FsmString[] titles;

        [UIHint(UIHint.TextArea)]
        [Tooltip("Each page's text. \\n starts a new line.")]
        public FsmString[] bodies;

        [ObjectType(typeof(Texture))]
        [Tooltip("Each page's picture (16:9). Empty = a short page without one.")]
        public FsmTexture[] pictures;

        [Tooltip("What the first pages float above, an arrow pointing down at it (e.g. the enemy).")]
        public FsmOwnerDefault above;

        [Tooltip("How many pages, from the first, float above Above. 0 = all in front of the player.")]
        public FsmInt pagesAbove;

        [Tooltip("Seconds of game time to wait before it shows.")]
        public FsmFloat showAfter;

        [Tooltip("Shown only once for each key (e.g. \"StartPumpkinTutorial\"). Empty: every time.")]
        public FsmString onceKey;

        [Tooltip("Once Key is remembered for this install of the game, not just until the game is closed: a new install " +
                 "shows it again (on PC a small file in the game's folder; on Quest the app's settings, wiped when it's " +
                 "uninstalled). In the editor: Tools > 429 Game > Menu > Show Tutorials Again forgets it.")]
        public FsmBool oncePerInstall;

        [Tooltip("Sent once the tutorial has gone.")]
        public FsmEvent finishEvent;


        private static readonly HashSet<string> shown = new();

        private float waited;
        private Game.Menus.TutorialNotice notice;
        private bool showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => shown.Clear();

        public override void Reset()
        {
            titles = new FsmString[0];
            bodies = new FsmString[0];
            pictures = new FsmTexture[0];
            above = null;
            pagesAbove = 0;
            showAfter = 0f;
            onceKey = "";
            oncePerInstall = false;
            finishEvent = null;
        }

        public override void OnEnter()
        {
            waited = 0f;
            showing = false;
            notice = null;
        }

        public override void OnUpdate()
        {
            if (showing)
            {
                if (notice != null && notice.IsShowing) return;
                // Seen to the end: remembered for good (Once Per Install). Quitting halfway shows it again next time.
                if (oncePerInstall.Value) RememberForGood(onceKey.Value);
                Done();
                return;
            }

            // Game time: it waits while the game is paused.
            waited += Time.deltaTime;
            if (waited < showAfter.Value) return;

            string key = onceKey.Value;
            if (AlreadyShown(key))
            {
                Done();
                return;
            }
            if (notice == null) notice = Game.Menus.TutorialNotice.Find();
            if (notice == null)
            {
                Debug.LogWarning("[Show Notice] The scene has no Tutorial Notice (Tools > 429 Game > Menu > Set Up Tutorial Notice), so the tutorial isn't shown.");
                Done();
                return;
            }
            // Another notice is up (e.g. a level up): this one comes after it.
            var levelUp = Object.FindAnyObjectByType<Game.Leveling.LevelUpNotice>();
            if (notice.IsShowing || (levelUp != null && levelUp.IsShowing)) return;

            var target = pagesAbove.Value > 0 ? Fsm.GetOwnerDefaultTarget(above) : null;
            int count = Mathf.Max(titles.Length, Mathf.Max(bodies.Length, pictures.Length));
            var pages = new List<Game.Menus.TutorialNotice.Page>(count);
            for (int i = 0; i < count; i++)
            {
                pages.Add(new Game.Menus.TutorialNotice.Page(
                    i < titles.Length ? titles[i].Value : "",
                    i < bodies.Length ? bodies[i].Value : "",
                    i < pictures.Length ? pictures[i].Value as Texture : null,
                    target != null && i < pagesAbove.Value ? target.transform : null));
            }
            if (pages.Count == 0)
            {
                Done();
                return;
            }

            if (!string.IsNullOrEmpty(key)) shown.Add(key);
            notice.Show(pages);
            showing = true;
        }

        private bool AlreadyShown(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (shown.Contains(key)) return true;
            return oncePerInstall.Value && Game.Menus.ShowOnce.WasShown(key);
        }

        private static void RememberForGood(string key) => Game.Menus.ShowOnce.Remember(key);

        private void Done()
        {
            if (finishEvent != null) Fsm.Event(finishEvent);
            Finish();
        }
    }
}
