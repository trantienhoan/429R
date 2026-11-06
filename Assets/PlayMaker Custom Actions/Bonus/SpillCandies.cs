using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Bonus")]
    [Tooltip("A fountain of candies out of the top of the Game Object (e.g. a cooking-round chest as it opens, user " +
             "2026-11-06): Count candies, each one of Candies at random, tossed gently up and out over Seconds, into " +
             "CurrentlySpawnedStuffs (cleared with the stage). It goes on by itself after this state ends (Candy Fountain).")]
    public class SpillCandies : FsmStateAction
    {
        [Tooltip("Where they come out of (just above its solid colliders).")]
        public FsmOwnerDefault gameObject;

        [Tooltip("How many candies.")]
        public FsmInt count;

        [Tooltip("The candies to pick from (e.g. Candy_1 ... Candy_8).")]
        public FsmGameObject[] candies;

        [Tooltip("How long they keep coming out, in seconds (0: all at once).")]
        public FsmFloat seconds;

        [Tooltip("Up speed (x = least, y = most), in metres a second. Keep it gentle: a candy breaks on a knock over 7.")]
        public FsmVector2 upSpeed;

        [Tooltip("Sideways speed (x = least, y = most), in metres a second.")]
        public FsmVector2 outSpeed;

        public override void Reset()
        {
            gameObject = null;
            count = 10;
            candies = new FsmGameObject[0];
            seconds = 1f;
            upSpeed = new Vector2(1.8f, 3f);
            outSpeed = new Vector2(0.6f, 1.4f);
        }

        public override void OnEnter()
        {
            var go = Fsm.GetOwnerDefaultTarget(gameObject);
            var picks = new GameObject[candies != null ? candies.Length : 0];
            for (int i = 0; i < picks.Length; i++) picks[i] = candies[i] != null ? candies[i].Value : null;
            Game.Bonus.CandyFountain.Play(Game.Bonus.CandyFountain.TopOf(go != null ? go : Owner), count.Value, picks,
                seconds.Value, upSpeed.Value, outSpeed.Value);
            Finish();
        }
    }
}
