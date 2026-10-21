using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// One of the bonus round's Russian-doll pumpkins (Nesting_Pumpkin_1 = the giant, Nesting_Pumpkin_2 = each smaller one;
    /// copies of the start pumpkin): its own size, health, candies and laugh. Bonus Round sets those for each one (Configure),
    /// so the values here are only what it has when used on its own. Its Health FSM still does the laughing, the
    /// crack and the break (pieces and smoke), its Jiggle wobbles it at every hit that hurts, and Enemy Hit Damage gives
    /// its EXP (with the gold orbs). The giant comes out of the bonus stage's chest (the chest makes it; the round running
    /// takes it as its first pumpkin and grows it out of the chest); the others pop out where the last one broke.
    /// Its FSMs wait until it's set up, so whatever made it can't start them with the prefab's values.
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
        [Tooltip("Drops in from the sky. Off: it pops out where it is (the bonus round's pumpkins).")]
        [SerializeField] private bool dropFromTheSky;
        [Tooltip("How high it drops from, in metres (its drop is set in its own size, so a giant would come from far " +
                 "above what the camera shows).")]
        [Min(0f)]
        [SerializeField] private float dropHeight = 18f;
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
        [Tooltip("The state after it that only laughs (its health isn't checked there): one that pops out leaves it at once.")]
        [SerializeField] private string laughState = "End 1";

        /// <summary>Candies it drops when it breaks.</summary>
        public int Candies => candies;

        /// <summary>Its pumpkin (the child with the FSMs), where it stands.</summary>
        public Transform Body { get; private set; }

        /// <summary>Broken (its health ran out) or gone.</summary>
        public bool IsBroken => this == null || hits == null || hits.IsDead;

        /// <summary>It has come in (landed from the sky, or popped out) and can be fought.</summary>
        public bool Ready => healthMachine == null || healthMachine.ActiveStateName != entranceState;

        /// <summary>The top of its pumpkin, in the world (where it is if it has no mesh).</summary>
        public float Top
        {
            get
            {
                float top = transform.position.y;
                foreach (var shape in GetComponentsInChildren<Renderer>())
                {
                    if (shape is MeshRenderer || shape is SkinnedMeshRenderer) top = Mathf.Max(top, shape.bounds.max.y);
                }
                return top;
            }
        }

        private Game.Combat.EnemyHitDamage hits;
        private PlayMakerFSM healthMachine;
        private int exp = -1;
        private float jiggle = 1f;
        private bool setUp;
        // Its FSMs, held off until it's set up.
        private readonly List<PlayMakerFSM> held = new();

        private void Awake()
        {
            Body = transform.Find(bodyName);
            if (Body == null) Body = transform;
            // Whatever made it (a chest's Create Object) can't start them with the prefab's size and drop: Start lets them go.
            foreach (var fsm in Body.GetComponents<PlayMakerFSM>())
            {
                if (!fsm.enabled) continue;
                fsm.enabled = false;
                held.Add(fsm);
            }
        }

        private void Start()
        {
            // Not made by the round (the bonus stage's chest made it): the round running takes it as its giant. On its
            // own it keeps its own values.
            if (!setUp)
            {
                var round = BonusRound.Running;
                if (round == null || !round.TakeGiant(this)) Setup();
            }
            foreach (var fsm in held)
            {
                if (fsm != null) fsm.enabled = true;
            }
            held.Clear();
        }

        /// <summary>
        /// Called by Bonus Round before Setup: its size, health, candies, laugh pitch, EXP (-1: as on its Enemy Hit
        /// Damage) and how much it jiggles when hit (1 = as its Jiggle is set; the smaller, tougher ones less).
        /// </summary>
        public void Configure(float newSize, float newHealth, int newCandies, float newPitch, int newExp, float newJiggle = 1f)
        {
            size = Mathf.Max(0.05f, newSize);
            health = Mathf.Max(1f, newHealth);
            candies = Mathf.Max(0, newCandies);
            voicePitch = newPitch;
            exp = newExp;
            jiggle = Mathf.Max(0f, newJiggle);
        }

        /// <summary>Called by Bonus Round before Setup: it grows out of where it is in 'seconds' (no drop from the sky).</summary>
        public void GrowHere(float seconds)
        {
            dropFromTheSky = false;
            popSeconds = Mathf.Max(0.05f, seconds);
        }

        /// <summary>
        /// Called by Bonus Round right after it's made (before its FSMs start; it does it itself on its own): its size,
        /// health and voice, and a pop where it is instead of the drop from the sky.
        /// </summary>
        public void Setup()
        {
            setUp = true;
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
            if (hits != null && exp >= 0) hits.Exp = exp;
            if (Body.TryGetComponent<AudioSource>(out var voice)) voice.pitch = voicePitch;
            foreach (var wobble in GetComponentsInChildren<Game.VFX.Jiggle>(true)) wobble.Amount = jiggle;
            if (!dropFromTheSky) PopOutInstead();
            else DropFrom(dropHeight);
        }

        /// <summary>No more hits: it stays as it is, laughing (time's up).</summary>
        public void StopTakingHits()
        {
            if (hits != null) hits.enabled = false;
            foreach (var fsm in Body.GetComponents<PlayMakerFSM>())
            {
                if (fsm != healthMachine) fsm.enabled = false;
            }
            // Still held (time ran out the moment it came): only the laugh starts.
            held.RemoveAll(fsm => fsm != healthMachine);
            foreach (var part in GetComponentsInChildren<Collider>()) part.enabled = false;
        }

        // The entrance state's tweens: no fall (it starts where it is) and a quick grow from small, so the FSM goes on to
        // its laughing the usual way. Its laugh after landing is cut short: health isn't checked there, so one broken
        // right after popping out would stay whole and in the way until it ended.
        private void PopOutInstead()
        {
            if (healthMachine == null) return;
            var laugh = healthMachine.Fsm.GetState(laughState);
            if (laugh != null)
            {
                foreach (var action in laugh.Actions)
                {
                    if (action.GetType().Name == "Wait") SetFloat(action, "time", 0.05f);
                }
            }
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

        // The entrance's fall starts 'height' metres up (its offset is in the pumpkin's own scale).
        private void DropFrom(float height)
        {
            if (healthMachine == null || height <= 0f) return;
            var state = healthMachine.Fsm.GetState(entranceState);
            if (state == null) return;
            float scale = Mathf.Max(0.01f, Body.lossyScale.y);
            foreach (var action in state.Actions)
            {
                if (action.GetType().Name == "TweenPosition") SetVector(action, "fromPosition", Vector3.up * (height / scale));
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
