using System.Reflection;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// One of the bonus round's Russian-doll pumpkins (Nesting_Pumpkin_1 = the giant ... Nesting_Pumpkin_5 = the last,
    /// copies of the start pumpkin): its own size, health, candies and laugh. Its Health FSM still does the laughing, the
    /// crack and the break (pieces and smoke), its Jiggle wobbles it at every hit that hurts, and Enemy Hit Damage gives
    /// its EXP (with the gold orbs). The giant drops in from the sky like the start pumpkin; the others pop out where the
    /// last one broke. Bonus Round makes them one after another.
    /// </summary>
    [DisallowMultipleComponent]
    public class NestingPumpkin : MonoBehaviour
    {
        [Tooltip("Its size (this object's scale).")]
        [Min(0.1f)]
        [SerializeField] private float size = 2f;
        [Tooltip("Its health.")]
        [Min(1f)]
        [SerializeField] private float health = 120f;
        [Tooltip("Candies it drops when it breaks. (Its EXP is on pumpkin_Start > Enemy Hit Damage > Exp.)")]
        [Min(0)]
        [SerializeField] private int candies = 5;
        [Tooltip("Its laugh's pitch: higher for the smaller ones.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float voicePitch = 1f;
        [Tooltip("Drops in from the sky (the giant). Off: it pops out where the last one broke.")]
        [SerializeField] private bool dropFromTheSky;
        [Tooltip("Seconds it takes to pop out.")]
        [Min(0.05f)]
        [SerializeField] private float popSeconds = 0.35f;

        [Header("PlayMaker")]
        [Tooltip("The child with the pumpkin's FSMs, Enemy Hit Damage and voice.")]
        [SerializeField] private string bodyName = "pumpkin_Start";
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("The Health FSM's first state: the drop from the sky.")]
        [SerializeField] private string entranceState = "Compare Health";

        /// <summary>Candies it drops when it breaks.</summary>
        public int Candies => candies;

        /// <summary>Its pumpkin (the child with the FSMs), where it stands.</summary>
        public Transform Body { get; private set; }

        /// <summary>Broken (its health ran out) or gone.</summary>
        public bool IsBroken => this == null || hits == null || hits.IsDead;

        private Game.Combat.EnemyHitDamage hits;
        private PlayMakerFSM healthMachine;

        /// <summary>
        /// Called by Bonus Round right after it's made (before its FSMs start): its size, health and voice, and a quick
        /// pop instead of the drop from the sky unless it's the giant.
        /// </summary>
        public void Setup()
        {
            Body = transform.Find(bodyName);
            if (Body == null) Body = transform;
            hits = Body.GetComponent<Game.Combat.EnemyHitDamage>();
            foreach (var fsm in Body.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == healthFsm) healthMachine = fsm;
            }

            transform.localScale = Vector3.one * size;
            if (healthMachine != null)
            {
                var value = healthMachine.FsmVariables.FindFsmFloat(healthVariable);
                if (value != null) value.Value = health;
            }
            if (Body.TryGetComponent<AudioSource>(out var voice)) voice.pitch = voicePitch;
            if (!dropFromTheSky) PopOutInstead();
        }

        /// <summary>No more hits: it stays as it is, laughing (time's up).</summary>
        public void StopTakingHits()
        {
            if (hits != null) hits.enabled = false;
            foreach (var fsm in Body.GetComponents<PlayMakerFSM>())
            {
                if (fsm != healthMachine) fsm.enabled = false;
            }
            foreach (var part in GetComponentsInChildren<Collider>()) part.enabled = false;
        }

        // The entrance state's tweens: no fall (it starts where it is) and a quick grow from small, so the FSM goes on to
        // its laughing the usual way.
        private void PopOutInstead()
        {
            if (healthMachine == null) return;
            var state = healthMachine.Fsm.GetState(entranceState);
            if (state == null) return;
            foreach (var action in state.Actions)
            {
                string type = action.GetType().Name;
                if (type == "TweenPosition")
                {
                    SetVector(action, "fromPosition", Vector3.zero);
                    SetFloat(action, "startDelay", 0f);
                    SetFloat(action, "time", popSeconds);
                }
                else if (type == "TweenScale")
                {
                    SetVector(action, "fromScale", Body.localScale * 0.2f);
                    SetFloat(action, "startDelay", 0f);
                    SetFloat(action, "time", popSeconds);
                }
            }
        }

        private static void SetFloat(object action, string field, float value)
        {
            if (action.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public)?.GetValue(action) is HutongGames.PlayMaker.FsmFloat f)
                f.Value = value;
        }

        private static void SetVector(object action, string field, Vector3 value)
        {
            if (action.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public)?.GetValue(action) is HutongGames.PlayMaker.FsmVector3 v)
                v.Value = value;
        }
    }
}
