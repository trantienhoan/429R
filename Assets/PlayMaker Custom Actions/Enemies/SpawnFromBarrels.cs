using System.Collections.Generic;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Spawns an enemy out of each barrel: one random barrel gets the Boss, every other one a Regular enemy (so " +
             "5 barrels = 4 regular + 1 boss). Barrels that are gone or empty are skipped. Each one comes out First Delay + " +
             "n x Delay Between seconds after the state starts, to match the barrels breaking (Send Event Dead with delays). " +
             "Leaving the state early spawns the rest at once. With Boss Last, the boss waits for the swarm instead: it comes " +
             "out of its barrel once no more than Boss When Left regular ones are alive, after a warning (it keeps waiting " +
             "even when the state is left). Out, the boss can hang there facing the player for Boss Hold Off seconds first.")]
    public class SpawnFromBarrels : FsmStateAction
    {
        [Tooltip("The barrels, in the order they break.")]
        public FsmGameObject[] barrels;

        [RequiredField]
        [Tooltip("Comes out of every barrel but one.")]
        public FsmGameObject regular;

        [Tooltip("Comes out of one random barrel. Empty = a regular one there too.")]
        public FsmGameObject boss;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: what the enemies are put under, e.g. CurrentlySpawnedStuffs.")]
        public FsmGameObject parent;

        [Tooltip("Seconds before the first one comes out.")]
        public FsmFloat firstDelay;

        [Tooltip("Seconds between one barrel and the next.")]
        public FsmFloat delayBetween;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store the boss.")]
        public FsmGameObject storeBoss;

        [Tooltip("Event to send when they're all out. Left empty, the action just finishes.")]
        public FsmEvent finishEvent;

        [ActionSection("Boss last")]
        [Tooltip("The boss doesn't come out with the others: it waits until the swarm is nearly cleared (Boss When Left), " +
                 "then comes out of its barrel after a warning. Off: it comes out with the others.")]
        public FsmBool bossLast;

        [Tooltip("The boss comes once this many regular ones (or fewer) are still alive...")]
        public FsmInt bossWhenLeft;

        [Tooltip("...but no sooner than this many seconds after the last regular one came out...")]
        public FsmFloat bossMinWait;

        [Tooltip("...and no later than this many (even if the swarm is still there).")]
        public FsmFloat bossMaxWait;

        [Tooltip("Seconds of warning before it comes out: a puff at its barrel, the Warning Sound and a camera shake.")]
        public FsmFloat bossWarning;

        [Tooltip("The puff at its barrel, e.g. Smoke_Blast.")]
        public FsmGameObject warningEffect;

        [ObjectType(typeof(AudioClip))]
        [Tooltip("Played at its barrel as the warning starts.")]
        public FsmObject warningSound;

        [Tooltip("The camera shake with the warning (ShakeSmall, ShakeMed, ShakeBig). Empty: none.")]
        public FsmString warningShake;

        [Tooltip("Seconds the boss hangs where it came out, facing the player, before it comes for them (hitting it " +
                 "ends the wait). 0 = it comes at once. With Boss Last off too.")]
        public FsmFloat bossHoldOff;

        private readonly List<(Vector3 position, Quaternion rotation, bool isBoss)> waiting = new();
        private readonly List<GameObject> regulars = new();
        private float elapsed;
        private int spawned;
        private bool bossHandedOver;

        public override void Reset()
        {
            barrels = new FsmGameObject[0];
            regular = null;
            boss = null;
            parent = new FsmGameObject { UseVariable = true };
            firstDelay = 0.1f;
            delayBetween = 0.1f;
            storeBoss = new FsmGameObject { UseVariable = true };
            finishEvent = null;
            bossLast = false;
            bossWhenLeft = 2;
            bossMinWait = 3f;
            bossMaxWait = 15f;
            bossWarning = 1.5f;
            warningEffect = null;
            warningSound = null;
            warningShake = "ShakeMed";
            bossHoldOff = 2.5f;
        }

        // FSMs saved before Boss Last existed have it unset: off, as before.
        private bool BossLast => bossLast != null && !bossLast.IsNone && bossLast.Value;

        // FSMs saved before Boss Hold Off existed have it unset: it comes at once, as before.
        private float HoldOffSeconds => bossHoldOff != null && !bossHoldOff.IsNone ? Mathf.Max(0f, bossHoldOff.Value) : 0f;

        public override void OnEnter()
        {
            // Where the barrels stand now: they're destroyed a moment after they break.
            waiting.Clear();
            foreach (var barrel in barrels)
            {
                var go = barrel != null ? barrel.Value : null;
                if (go != null && go.activeInHierarchy) waiting.Add((go.transform.position, go.transform.rotation, false));
            }
            if (waiting.Count == 0) waiting.Add((Owner.transform.position, Quaternion.identity, false));
            int bossAt = Random.Range(0, waiting.Count);
            waiting[bossAt] = (waiting[bossAt].position, waiting[bossAt].rotation, true);
            elapsed = 0f;
            spawned = 0;
            regulars.Clear();
            bossHandedOver = false;
            SpawnDue();
        }

        public override void OnUpdate()
        {
            elapsed += Time.deltaTime;
            SpawnDue();
        }

        public override void OnExit()
        {
            while (spawned < waiting.Count) Spawn(waiting[spawned++]);
            if (BossLast) HandOverBoss();
        }

        private void SpawnDue()
        {
            while (spawned < waiting.Count && elapsed >= firstDelay.Value + spawned * delayBetween.Value) Spawn(waiting[spawned++]);
            if (spawned < waiting.Count) return;
            if (BossLast) HandOverBoss();
            Finish();
            if (finishEvent != null) Fsm.Event(finishEvent);
        }

        private void Spawn((Vector3 position, Quaternion rotation, bool isBoss) at)
        {
            // Boss Last: its barrel breaks empty; the boss comes out there later (Hand Over Boss).
            if (at.isBoss && BossLast && boss.Value != null) return;
            var prefab = at.isBoss && boss.Value != null ? boss.Value : regular.Value;
            if (prefab == null) return;
            var enemy = Object.Instantiate(prefab, at.position, Quaternion.Euler(0f, at.rotation.eulerAngles.y, 0f));
            if (!parent.IsNone && parent.Value != null) enemy.transform.SetParent(parent.Value.transform, true);
            if (at.isBoss && boss.Value != null && HoldOffSeconds > 0f && enemy.TryGetComponent(out Game.Enemies.SpiderBrain brain))
                brain.HoldOff(HoldOffSeconds);
            if (at.isBoss && !storeBoss.IsNone) storeBoss.Value = enemy;
            else if (!at.isBoss) regulars.Add(enemy);
        }

        // Once the regular ones are out, the boss waits for the swarm on its own (Barrel Boss Caller), so the FSM can move
        // on meanwhile.
        private void HandOverBoss()
        {
            if (bossHandedOver || boss.Value == null) return;
            bossHandedOver = true;
            var spot = waiting.Find(w => w.isBoss);
            var caller = Owner.GetComponent<Game.Enemies.BarrelBossCaller>();
            if (caller == null) caller = Owner.AddComponent<Game.Enemies.BarrelBossCaller>();
            var store = storeBoss;
            caller.Begin(new Game.Enemies.BarrelBossCaller.Call
            {
                boss = boss.Value,
                at = spot.position,
                rotation = Quaternion.Euler(0f, spot.rotation.eulerAngles.y, 0f),
                parent = !parent.IsNone && parent.Value != null ? parent.Value.transform : null,
                whenLeft = bossWhenLeft != null ? bossWhenLeft.Value : 2,
                minWait = bossMinWait != null ? bossMinWait.Value : 3f,
                maxWait = bossMaxWait != null ? bossMaxWait.Value : 15f,
                warningSeconds = bossWarning != null ? bossWarning.Value : 1.5f,
                warningEffect = warningEffect != null ? warningEffect.Value : null,
                warningSound = warningSound != null ? warningSound.Value as AudioClip : null,
                warningShake = warningShake != null ? warningShake.Value : null,
                holdSeconds = HoldOffSeconds,
                spawned = enemy => { if (store != null && !store.IsNone) store.Value = enemy; },
            }, regulars);
        }
    }
}
