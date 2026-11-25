using Game.Story;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The Fairy says her lines where she is (the user's recordings in Resources/Story Fairy Settings): Thank You " +
             "(one at random) or Talk (all of them, one after another). Finishes once she has said them and At Least " +
             "seconds have gone (game time), then sends Finish Event if set. No lines: finishes after At Least.")]
    public class StoryFairySay : FsmStateAction
    {
        public enum Lines
        {
            ThankYou,
            Talk,
        }

        [RequiredField]
        [Tooltip("The Fairy.")]
        public FsmOwnerDefault gameObject;

        [Tooltip("Which of her lines (Story Fairy Settings: Thank You Lines or Talk Lines).")]
        public Lines lines;

        [Tooltip("Seconds before she starts.")]
        public FsmFloat delay;

        [Tooltip("The state stays at least this long, even with short lines or none (seconds).")]
        public FsmFloat atLeast;

        [Tooltip("Sent when she is done (e.g. FINISHED). Empty: the action only finishes.")]
        public FsmEvent finishEvent;

        private float left;

        public override void Reset()
        {
            gameObject = null;
            lines = Lines.Talk;
            delay = 0f;
            atLeast = 0f;
            finishEvent = null;
        }

        public override void OnEnter()
        {
            float said = 0f;
            var go = gameObject != null ? Fsm.GetOwnerDefaultTarget(gameObject) : null;
            if (go == null) go = Owner;
            if (go != null)
            {
                var acts = go.GetComponent<StoryFairyActs>();
                if (acts == null) acts = go.AddComponent<StoryFairyActs>();
                var settings = StoryFairySettings.Instance;
                said = lines == Lines.Talk
                    ? acts.Say(settings.TalkLines, delay.Value, settings.TalkGap)
                    : acts.Say(new[] { StoryFairySettings.Pick(settings.ThankYouLines) }, delay.Value, 0f);
            }
            left = Mathf.Max(said, atLeast.Value);
            if (left <= 0f) Done();
        }

        public override void OnUpdate()
        {
            left -= Time.deltaTime;
            if (left <= 0f) Done();
        }

        private void Done()
        {
            Finish();
            if (finishEvent != null) Fsm.Event(finishEvent);
        }
    }
}
