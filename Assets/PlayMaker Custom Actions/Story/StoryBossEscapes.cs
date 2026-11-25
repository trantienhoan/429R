using Game.Story;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The room's mini boss escapes: it pops out of the laughing pumpkin that just broke, carrying its piece of " +
             "the Green Seed, hisses, runs off and digs into the floor, leaving a hole. When the room's EnemyManager " +
             "calls it during Fight State, it climbs out of that hole. Only a look: it can't hurt or be hurt. " +
             "Finishes once it has dug in. Settings: Resources/Story Boss Escape Settings.")]
    public class StoryBossEscapes : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The room's mini boss prefab (the one its EnemyManager calls at the end).")]
        public FsmGameObject boss;

        [Tooltip("The GAMESTAGES state of the room's fight (its wall drop): the boss comes back out of its hole while " +
                 "it runs.")]
        public FsmString fightState;

        [Tooltip("It finishes after this many seconds at most, whatever happens (paused time not counted).")]
        public FsmFloat mostSeconds;

        private StoryBossEscape show;
        private float waited;

        public override void Reset()
        {
            boss = null;
            fightState = "";
            mostSeconds = 7f;
        }

        public override void OnEnter()
        {
            waited = 0f;
            show = StoryBossEscape.Play(boss.Value, State.Name, fightState.Value);
            if (show == null) Finish();
        }

        public override void OnUpdate()
        {
            waited += Time.deltaTime;
            if (show == null || show.Done || waited >= mostSeconds.Value) Finish();
        }

        public override void OnExit()
        {
            if (show != null) show.Stop();
            show = null;
        }
    }
}
