using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Bonus")]
    [Tooltip("The end of the game's jackpot (user, 2026-11-06): a big firework over the Game Object, a miracle sound, and a " +
             "stream of gold EXP orbs out of it into the player while EXP is paid (saved, so the levels stay for the next " +
             "run). In Treasure_Chest_GAMEOVER's Open_Chest state (Tools > 429 Game > Bonus > Set Up Thank You Reward).")]
    public class PlayEndGameJackpot : FsmStateAction
    {
        [Tooltip("Where it pours out of (the chest).")]
        public FsmOwnerDefault gameObject;

        [Tooltip("The EXP it pays, in all (a level is 100 to 400).")]
        public FsmInt exp;

        [Tooltip("How long the orbs pour out, in seconds.")]
        public FsmFloat seconds;

        [Tooltip("Orbs in each burst (8 a second).")]
        public FsmInt orbsPerBurst;

        [Tooltip("Played as it starts.")]
        [ObjectType(typeof(AudioClip))]
        public FsmObject sound;

        [Tooltip("Made over it as it starts (e.g. Firework_Big).")]
        public FsmGameObject[] effects;

        public override void Reset()
        {
            gameObject = null;
            exp = 500;
            seconds = 3f;
            orbsPerBurst = 4;
            sound = null;
            effects = new FsmGameObject[0];
        }

        public override void OnEnter()
        {
            var go = Fsm.GetOwnerDefaultTarget(gameObject);
            var at = go != null ? go.transform.position + Vector3.up * 0.4f : Owner.transform.position;
            var made = new GameObject[effects != null ? effects.Length : 0];
            for (int i = 0; i < made.Length; i++) made[i] = effects[i] != null ? effects[i].Value : null;
            Game.Bonus.EndGameJackpot.Play(at, exp.Value, seconds.Value, orbsPerBurst.Value, sound.Value as AudioClip, made);
            Finish();
        }
    }
}
