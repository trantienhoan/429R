using UnityEngine;
using ValleyGame = Balls.BallValleyStage;   // 429R: the gate stage took BALLS' ValleyGame's place

namespace Balls
{
    /// <summary>What a ball wants to do this physics step. Kept tiny so it can be sent over the network later.</summary>
    public struct BallInput
    {
        public Vector3 dir;    // flat direction to roll / aim
        public bool thrust;    // roll toward dir
        public float speed;    // with thrust: how fast, 0..1 of top speed (the pointer's or the thumb's distance)
        public bool charge;    // hold to charge the special move, release to launch
    }

    public interface IBallBrain
    {
        BallInput Think(BallPlayer me);
    }

    // 429R: BALLS' MouseBrain (mouse and touch) and RemoteBrain (online players) aren't brought over. The VR player's
    // brain comes in a later step (BallValleyStage.AddExternalBall).

    /// <summary>Simple bot: hunts players near caves, eats food, smashes stuff, and (mostly) avoids caves and rocks.</summary>
    public class BotBrain : IBallBrain
    {
        enum Mode { Wander, Food, Smash, Hunt }

        Mode mode = Mode.Wander;
        readonly float temper, caution;
        float nextPlan, huntUntil, noHuntUntil, chargeStart, chargeHold;
        Vector3 goal;
        BallPlayer prey;
        Destructible smash;
        Component foodTarget;

        // Stuck (wedged on a rock or a wall, boxed in) for StuckSeconds: it blasts its way out with the special move (or,
        // with that not ready, rolls hard the most open way for EscapeSeconds).
        const float StuckSeconds = 2.5f, StuckRadius = 2f, EscapeSeconds = 1.2f, EscapeLook = 14f;
        Vector3 stuckAt, escapeDir;
        float stuckSince, escapeUntil;
        bool escapeCharge;   // the charge going on is an escape: aim it at escapeDir
        static readonly RaycastHit[] castHits = new RaycastHit[24];

        public BotBrain()
        {
            temper = Random.value;
            caution = Random.Range(0.5f, 1.1f);
        }

        /// <summary>0..1: how much this bot fights (its temper inside the Tuning range, so it can be tweaked live).</summary>
        float Aggression
        {
            get
            {
                var g = ValleyGame.I;
                var t = g.tune;
                float a = Mathf.Lerp(t.botAggressionMin, t.botAggressionMax, temper);
                // 429R: in the Moment of Truth every bot gets angrier until it fights all out (play test 2026-11-16: the
                // round never ended).
                if (g.momentOfTruth) a = Mathf.Lerp(a, 1f, Mathf.Clamp01((Time.time - g.momentOfTruthAt) / Mathf.Max(0.1f, g.momentOfTruthRampSeconds)));
                return a;
            }
        }

        /// <summary>429R: hunting the VR player's body right now (gate 3 lets only a few at a time: Ball Valley Gate's Max Hunters).</summary>
        public bool HuntsHuman => mode == Mode.Hunt && prey && prey.isHuman;

        // 429R: it leaves the VR player alone until then (it just hit them).
        float humanRestUntil;

        /// <summary>429R: it just hit the VR player: it stops hunting them and leaves them alone for <paramref name="seconds"/>.</summary>
        public void ForgetHuman(float seconds)
        {
            humanRestUntil = Time.time + seconds;
            if (mode == Mode.Hunt && prey && prey.isHuman)
            {
                mode = Mode.Wander;
                nextPlan = 0f;
            }
        }

        void EndHunt(float aggression)
        {
            mode = Mode.Wander;
            nextPlan = 0f;
            noHuntUntil = Time.time + Random.Range(0.5f, 4f) * (1.1f - aggression);   // the fiercest go again almost at once
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public BallInput Think(BallPlayer me)
        {
            var input = new BallInput { dir = me.aimDir };
            Vector3 pos = Flat(me.Position);
            // Not getting anywhere? The clock starts over whenever it has got StuckRadius away, and while it isn't driving
            // itself (rolling out a shot, knocked, winding up).
            if (me.Locked || me.charging || (pos - stuckAt).sqrMagnitude > StuckRadius * StuckRadius)
            {
                stuckAt = pos;
                stuckSince = Time.time;
            }
            if (me.Locked) return input;   // shot played / knocked: nothing to do until it slows down
            Vector3 vel = Flat(me.Velocity);
            float speed = vel.magnitude;

            if (Time.time >= nextPlan)
            {
                Plan(me, pos);
                nextPlan = Time.time + Random.Range(0.2f, 0.4f);
            }

            float aggression = Aggression;
            Vector3 desired = Vector3.zero;
            bool wantCharge = false;
            float commit = 0f;
            switch (mode)
            {
                case Mode.Hunt:
                    if (!prey || !prey.alive || Time.time > huntUntil) { EndHunt(aggression); break; }
                    desired = HuntDir(me, pos, aggression, out wantCharge, out commit);
                    break;
                case Mode.Food:
                    if (!foodTarget) { nextPlan = 0f; break; }
                    desired = Flat(foodTarget.transform.position - pos).normalized;
                    break;
                case Mode.Smash:
                    if (!smash) { nextPlan = 0f; break; }
                    Vector3 toS = Flat(smash.transform.position - pos);
                    float ds = toS.magnitude;
                    desired = toS / Mathf.Max(ds, 0.01f);
                    wantCharge = ds > 7f && ds < 16f && aggression > 0.5f;
                    break;
                default:
                    if (goal == Vector3.zero || Vector3.Distance(goal, pos) < 6f) goal = ValleyGame.I.RandomFloorPoint(ValleyGame.I.FieldM(15f));   // 429R: scaled
                    desired = Flat(goal - pos).normalized;
                    break;
            }

            // Charging: stand still spinning, keep aiming, release after the hold time.
            if (me.charging)
            {
                input.charge = Time.time - chargeStart < chargeHold;
                Vector3 aim = escapeCharge ? escapeDir : desired;
                if (aim.sqrMagnitude > 0.001f) input.dir = aim;
                return input;
            }
            escapeCharge = false;

            // Stuck: find the most open way out and blast off that way with the special move, at the most power that
            // rolls out safely; without it (not ready, no safe shot), roll hard that way for a moment.
            if (Time.time - stuckSince > StuckSeconds && Time.time > escapeUntil)
            {
                stuckSince = Time.time;   // (give this way out a chance before looking for another)
                escapeUntil = Time.time + EscapeSeconds;
                escapeDir = EscapeDir(me, pos, out float power);
                // Whatever it was after is behind whatever stopped it: carry on out that way, and only look for
                // something new once clear (else it rolls straight back in).
                var v = ValleyGame.I.valley;
                Vector3 onward = pos + escapeDir * 25f;
                mode = Mode.Wander;
                goal = new Vector3(Mathf.Clamp(onward.x, -v.SafeHalfX * 0.8f, v.SafeHalfX * 0.8f), 0f,
                    Mathf.Clamp(onward.z, -v.SafeHalfZ * 0.8f, v.SafeHalfZ * 0.8f));
                nextPlan = Time.time + 3f;   // (out of the shot's roll and a few metres on by then)
                if (power > 0f && me.DashReady)
                {
                    escapeCharge = true;
                    chargeStart = Time.time;
                    chargeHold = Mathf.Max(0.25f, power * ValleyGame.I.tune.chargeTime);
                    input.charge = true;
                    input.dir = escapeDir;
                    return input;
                }
            }
            if (Time.time < escapeUntil)
            {
                desired = escapeDir;
                wantCharge = false;
                commit = 0.5f;   // (still steers clear of holes, a little)
            }
            // Once launched there's no steering, so check where the shot rolls. A hunting shot stops on the prey, so only
            // the run-up to it counts (the bolder the bot, the closer to the hole it dares); the wildest don't check at all.
            if (wantCharge)
            {
                bool safe;
                if (mode == Mode.Hunt && prey)
                {
                    float toContact = Mathf.Max(0f, Vector3.Distance(Flat(prey.Position), pos) - prey.radius - me.radius);
                    safe = aggression > 0.9f || ShotIsSafe(me, pos, desired, toContact, 3f - 6f * aggression);
                }
                else safe = ShotIsSafe(me, pos, desired, float.MaxValue, 3f);
                if (!safe) wantCharge = false;
            }
            if (wantCharge && me.DashReady && speed < 25f)
            {
                chargeStart = Time.time;
                chargeHold = Random.Range(0.35f, 1f) + 0.4f * aggression;   // bold bots wind up to full power
                input.charge = true;
                if (desired.sqrMagnitude > 0.001f) input.dir = desired;
                return input;
            }

            Vector3 avoid = Avoid(me, pos, vel, speed) * (1f - commit);
            if (avoid.sqrMagnitude > 0.0025f) desired = desired * 0.5f + avoid;
            if (desired.sqrMagnitude < 0.0001f) return input;
            input.dir = desired.normalized;
            input.thrust = true;
            input.speed = 1f;   // bots always roll flat out
            return input;
        }

        void Plan(BallPlayer me, Vector3 pos)
        {
            var g = ValleyGame.I;
            // The Rainbow Slime beats everything: every bot in range drops what it's doing and races for it.
            var rainbow = g.RainbowSlime;
            if (rainbow && Vector3.Distance(Flat(rainbow.transform.position), pos) < 70f)
            {
                foodTarget = rainbow;
                mode = Mode.Food;
                return;
            }
            if (mode == Mode.Hunt && prey && prey.alive && Time.time < huntUntil) return;

            float aggression = Aggression;
            if (Time.time > noHuntUntil && Random.value < aggression)
            {
                // Bold bots look further for prey and go after it wherever it is, not only right next to a hole.
                float sight = 40f + 35f * aggression, caveReach = 45f + 40f * aggression;
                BallPlayer best = null;
                float bestScore = float.MaxValue;
                foreach (var p in g.players)
                {
                    if (p == me || !p.alive || p.Shielded || p.Invincible) continue;   // (no use hunting a ball that can't be knocked)
                    if (p.isHuman && (Time.time < humanRestUntil || !g.HumanHuntable())) continue;   // 429R: gate 3: only a few at a time go for the player
                    if (!p.isHuman && !g.botsHuntBots) continue;   // 429R: gate 3: bots don't pocket each other (that's the player's job)
                    float d = Vector3.Distance(Flat(p.Position), pos);
                    if (d > sight) continue;
                    if (p.BabyCount > me.BabyCount * 1.5f + 4 && aggression < 0.75f) continue;
                    var c = g.valley.NearestCave(p.Position);
                    float dc = Vector3.Distance(Flat(p.Position), c.mouth);
                    if (dc > caveReach) continue;
                    float score = d + dc * (1.2f - aggression);   // bold bots care less how far the prey is from a hole
                    if (score < bestScore) { bestScore = score; best = p; }
                }
                if (best)
                {
                    prey = best;
                    mode = Mode.Hunt;
                    huntUntil = Time.time + Random.Range(5f, 9f) + 5f * aggression;
                    return;
                }
            }

            foodTarget = g.NearestFood(me, pos, 40f);
            if (foodTarget) { mode = Mode.Food; return; }
            smash = g.NearestDestructible(pos, 80f);
            if (smash) { mode = Mode.Smash; return; }
            mode = Mode.Wander;
        }

        /// <summary>
        /// Does a shot in this direction (<paramref name="c01"/>: how wound up, 1 = full power) stay clear of every cave
        /// mouth for its first <paramref name="maxDist"/> metres (or its whole roll, if shorter), and, once the border is
        /// gone, stop short of the edge? <paramref name="margin"/>: extra room wanted beyond the cave's danger zone.
        /// </summary>
        static bool ShotIsSafe(BallPlayer me, Vector3 pos, Vector3 dir, float maxDist, float margin, float c01 = 1f)
        {
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            var valley = ValleyGame.I.valley;
            margin = ValleyGame.I.FieldM(margin);   // 429R: the extra room (BALLS 3 m / 1.5 m) scales with the valley
            Vector3 end = pos + dir * Mathf.Min(maxDist, me.EstimateShotDistance(c01));
            foreach (var c in valley.caves)
            {
                Vector3 m = Flat(c.mouth);
                Vector3 seg = end - pos;
                float t = Mathf.Clamp01(Vector3.Dot(m - pos, seg) / Mathf.Max(seg.sqrMagnitude, 0.0001f));
                if (Vector3.Distance(pos + seg * t, m) < c.DangerRadius + me.radius + margin) return false;
            }
            if (!valley.BordersOn)
            {
                float room = me.radius + Mathf.Max(0f, margin);
                if (Mathf.Abs(end.x) > valley.SafeHalfX - room || Mathf.Abs(end.z) > valley.SafeHalfZ - room) return false;
            }
            return true;
        }

        /// <summary>
        /// The way out for a stuck bot: of 24 directions, the one with the longest clear run (its own tail doesn't count,
        /// and a ball or anything else that rolls aside isn't in the way), toward the middle rather than the edge, and
        /// best of all at another player's ball to knock. <paramref name="power"/>: the strongest special move (0..1)
        /// that rolls out safely that way (it stops at whatever's in the way), 0 if none does.
        /// </summary>
        Vector3 EscapeDir(BallPlayer me, Vector3 pos, out float power)
        {
            Vector3 best = -me.aimDir;   // (back the way it was trying to go, if nothing's better)
            float bestScore = float.MinValue;
            power = 0f;
            Vector3 toMiddle = pos.sqrMagnitude > 1f ? -pos.normalized : Vector3.zero;
            float start = Random.value * 360f;
            for (int k = 0; k < 24; k++)
            {
                float a = (start + k * 15f) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float free = Clear(me, d, out float targetAt);
                bool target = targetAt < free;
                // Where the shot's run ends: on the first ball it hits (it stops there, like a hunting shot), else at
                // whatever's in the way; with nothing in the way its whole roll counts.
                float reach = target ? targetAt : free < EscapeLook ? free : float.MaxValue;
                float safePower = 0f;
                foreach (float c01 in EscapePowers)
                    if (ShotIsSafe(me, pos, d, reach, 1.5f, c01)) { safePower = c01; break; }
                // The clear run counts most (a wall a few metres off only bounces it back in), then a ball to knock,
                // a shot that's safe, and the middle.
                float score = free / EscapeLook * 6f + (target ? 2f : 0f) + (safePower > 0f ? 1f : 0f)
                              + Vector3.Dot(d, toMiddle) * 0.75f + (free < 3f && !target ? -4f : 0f) + Random.value * 0.3f;
                if (score > bestScore) { bestScore = score; best = d; power = safePower; }
            }
            return best;
        }

        static readonly float[] EscapePowers = { 1f, 0.7f, 0.45f, 0.25f };

        /// <summary>
        /// How far this ball can roll in <paramref name="dir"/> before something solid stops it (up to EscapeLook m).
        /// Its own balls don't count, nor anything that gets knocked aside; <paramref name="targetAt"/>: how far the
        /// first other player's ball that way is (MaxValue if none).
        /// </summary>
        static float Clear(BallPlayer me, Vector3 dir, out float targetAt)
        {
            float clear = EscapeLook;
            targetAt = float.MaxValue;
            Vector3 from = me.Position;
            int n = Physics.SphereCastNonAlloc(from, me.radius * 0.7f, dir, castHits, EscapeLook, ValleyGame.QueryMask, QueryTriggerInteraction.Ignore);   // 429R: mask
            for (int i = 0; i < n; i++)
            {
                var h = castHits[i];
                if (h.collider.TryGetComponent(out BallPart part))
                {
                    if (part.owner != me && part.owner.alive && h.distance < targetAt) targetAt = h.distance;
                    continue;
                }
                var rb = h.collider.attachedRigidbody;
                if (rb && !rb.isKinematic) continue;   // loose balls, slimes: pushed aside
                if (h.collider.GetComponent<Pellet>()) continue;   // 429R: a candy on the field: rolled over (eaten)
                float at = h.distance;
                if (at <= 0f)   // already touching it (wedged): only the way into it is blocked
                {
                    if (h.collider is MeshCollider mc && !mc.convex) continue;   // (the ground)
                    if (Vector3.Dot(Flat(h.collider.bounds.center - from), dir) <= 0f) continue;
                }
                clear = Mathf.Min(clear, Mathf.Max(0f, at));
            }
            return clear;
        }

        /// <summary>
        /// Get on the far side of the prey from its nearest cave, then ram it toward the cave. Bold bots also take the shot
        /// whenever ANY hole within knocking range is lined up behind the prey, not just its nearest one.
        /// </summary>
        Vector3 HuntDir(BallPlayer me, Vector3 pos, float aggression, out bool charge, out float commit)
        {
            charge = false;
            commit = 0f;
            Vector3 pp = Flat(prey.Position);
            Vector3 toPrey = pp - pos;
            float dPrey = toPrey.magnitude;
            Vector3 dirPrey = toPrey / Mathf.Max(dPrey, 0.01f);

            Cave cave = ValleyGame.I.valley.NearestCave(pp);
            Vector3 toCave = Flat(cave.mouth - pp);
            toCave /= Mathf.Max(toCave.magnitude, 0.01f);
            float align = Vector3.Dot(dirPrey, toCave);
            float reach = 20f + 45f * aggression;   // how far behind the prey a hole may be and still be worth the shot
            foreach (var c in ValleyGame.I.valley.caves)
            {
                Vector3 to = Flat(c.mouth - pp);
                float dc = to.magnitude;
                if (c == cave || dc > reach || dc < 0.01f) continue;
                float a = Vector3.Dot(dirPrey, to / dc);
                if (a > align && a > 0.8f) { align = a; toCave = to / dc; }   // lined up already: no need to circle round
            }
            if (align > 0.8f)
            {
                commit = Mathf.Clamp01((align - 0.8f) * 5f) * aggression;
                // Fire once lined up: bold bots accept a rougher line and shoot from further away.
                if (align > 0.9f - 0.07f * aggression && dPrey < 28f + 14f * aggression && me.DashReady) charge = true;
                return dirPrey;
            }
            Vector3 behind = pp - toCave * (prey.radius + me.radius + 4f);
            Vector3 toBehind = behind - pos;
            Vector3 d = toBehind.normalized;
            if (Vector3.Dot(d, dirPrey) > 0.3f && dPrey < toBehind.magnitude + 2f)
            {
                Vector3 side = Vector3.Cross(Vector3.up, dirPrey);
                float s = Vector3.Dot(side, toBehind) >= 0f ? 1f : -1f;
                d = (d + side * s * 1.3f).normalized;
            }
            return d;
        }

        /// <summary>The push (along one axis) away from the edges at +-<paramref name="half"/> for a bot at <paramref name="p"/> moving at <paramref name="v"/>.</summary>
        static float EdgePush(float p, float v, float half, float danger, float weight, bool bordered)
        {
            float push = 0f, toPlus = half - p, toMinus = half + p;
            if (toPlus < danger) push -= (1f - toPlus / danger) * weight * (bordered || v <= 0f ? 1f : 1f + v * 0.15f);
            if (toMinus < danger) push += (1f - toMinus / danger) * weight * (bordered || v >= 0f ? 1f : 1f - v * 0.15f);
            return push;
        }

        Vector3 Avoid(BallPlayer me, Vector3 pos, Vector3 vel, float speed)
        {
            var g = ValleyGame.I;
            Vector3 avoid = Vector3.zero;
            foreach (var c in g.valley.caves)
            {
                Vector3 toC = Flat(c.mouth - pos);
                float d = toC.magnitude;
                if (d < 0.01f) continue;
                float danger = c.DangerRadius + g.FieldM(5f) + speed * 0.7f * caution + me.radius * 2f;   // 429R: the 5 m scaled with the valley
                if (d < danger)
                {
                    Vector3 dir = toC / d;
                    float closing = Vector3.Dot(vel, dir);
                    float w = (1f - d / danger) * (closing > 0f ? 2.5f + closing * 0.15f : 1f);
                    avoid -= dir * w;
                }
            }
            // Keep inside the size it's shrinking to. With no border (the Moment of Truth) the edge is a drop: twice the push,
            // more the faster it's heading there. (Both sides count: the last valley is only 10 m across.)
            float edgeDanger = g.FieldM(5f) + speed * 0.4f + me.radius;   // 429R: the 5 m scaled with the valley
            float edgeWeight = g.valley.BordersOn ? 1.5f : 3f;
            avoid.x += EdgePush(pos.x, vel.x, g.valley.SafeHalfX, edgeDanger, edgeWeight, g.valley.BordersOn);
            avoid.z += EdgePush(pos.z, vel.z, g.valley.SafeHalfZ, edgeDanger, edgeWeight, g.valley.BordersOn);

            if (speed > 2f)
            {
                Vector3 dir = vel / speed;
                if (Physics.SphereCast(me.Position, me.radius * 0.9f, dir, out RaycastHit hit, 3f + speed * 0.5f, ValleyGame.QueryMask, QueryTriggerInteraction.Ignore)   // 429R: mask
                    && hit.collider.GetComponent<Hazard>() != null)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    float s = Vector3.Dot(side, Flat(hit.collider.bounds.center - pos)) > 0f ? -1f : 1f;
                    avoid += side * (s * 1.6f);
                }
            }
            return avoid;
        }
    }
}
