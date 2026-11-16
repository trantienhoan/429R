using UnityEngine;
using ValleyGame = Balls.BallValleyStage;   // 429R: the gate stage took BALLS' ValleyGame's place

namespace Balls
{
    /// <summary>
    /// Living food, about the size of a berry: it squeezes out of a slime rock, hops to the nearest grass tuft and eats
    /// it (getting a little bigger and worth more), and runs from any ball that comes close. A main ball that touches it
    /// eats it. Knocked into a cave, it's simply gone. Online, the host simulates it and clients show a copy (429R: no network copies).
    /// </summary>
    public class Slime : MonoBehaviour
    {
        public const string Model = "Giant_Slime";   // 429R's two-part slime, with its own painted texture

        // 429R: no network copies (BALLS' netId, proxy, netVelocity, animCode, ProxyAnim).
        public byte look;           // colour index (sent to clients)
        public Rigidbody rb;
        public SphereCollider col;
        public Transform vis;
        public int eaten;
        public float size = 1f;     // radius = tune.slimeRadius * size; grows with every tuft eaten
        public SlimeAnim anim;

        /// <summary>The Rainbow Slime's look number (sent to clients like any slime colour).</summary>
        public const byte RainbowLook = 255;
        /// <summary>A bit bigger than a slime fresh out of its rock, so it's easy to spot.</summary>
        public const float RainbowSize = 1.3f;
        /// <summary>
        /// The Rainbow Slime (ValleyGame.SpawnRainbowSlime): it never eats, bolts from every ball from far off and much
        /// faster than a slime, jumps aside from a ball charging straight at it, cycles through the rainbow with sparkles,
        /// and makes whoever catches it invincible for a while.
        /// </summary>
        public bool rainbow;
        float bornAt, nextSparkle;
        bool leaving, gone;
        Renderer[] paint;
        MaterialPropertyBlock paintBlock;

        GrassTuft target;
        Vector3 goal;
        float nextLook, nextGoal, nextHop, chewUntil, hitCooldown, laughAt;
        float shownSize, emerge;
        bool dead;
        // Went over a hole: drops out of sight (still sent to clients, so they see it fall), then it's gone.
        bool falling;
        float fallEnd;

        // Paint colours (one texture, like 429R): add a colour by adding it here. The 429R ones first.
        static readonly Color[] Colors =
        {
            new Color(0.89f, 0.15f, 0.18f),  // red (Slime_Red_Mat)
            new Color(0.20f, 0.45f, 1.00f),  // blue (Slime_Blue_Mat)
            new Color(1.00f, 0.85f, 0.12f),  // yellow (Slime_Yellow_Mat)
            new Color(0.94f, 0.63f, 0.19f),  // candy orange (Slime_Candy_1_Mat)
            new Color(0.38f, 0.84f, 0.28f),  // green
            new Color(0.95f, 0.45f, 0.75f),  // pink
            new Color(0.62f, 0.38f, 0.95f),  // purple
            new Color(0.20f, 0.85f, 0.85f),  // cyan
            new Color(0.95f, 0.95f, 0.95f),  // white
            new Color(0.30f, 0.30f, 0.34f),  // charcoal
        };
        static readonly int PaintColor = Shader.PropertyToID("_BaseColor");

        public static int LookCount => Colors.Length;
        public Color BodyColor => rainbow ? Rainbow(Time.time * 0.7f) : Colors[look % Colors.Length];
        Tuning T => ValleyGame.I.tune;
        public float Radius => T.slimeRadius * size;
        /// <summary>Food a ball gets for eating it: more for every tuft it ate.</summary>
        public int FoodValue => rainbow ? T.rainbowFood : T.slimeFood + eaten * T.slimeFoodPerGrass;

        /// <summary>A colour going round the rainbow as <paramref name="t"/> counts up (one lap per 1).</summary>
        public static Color Rainbow(float t) => Color.HSVToRGB(Mathf.Repeat(t, 1f), 0.72f, 1f);

        /// <summary>After its look is built (both on the host and on clients): the Rainbow Slime gets its colour cycle.</summary>
        public void SetupLook()
        {
            rainbow = look == RainbowLook;
            if (!rainbow || !vis) return;
            paint = vis.GetComponentsInChildren<Renderer>();
            paintBlock = new MaterialPropertyBlock();
            bornAt = Time.time;
        }
        public bool CanEat => !dead && !falling && emerge > 0.6f;   // not while still squeezing out of its rock
        public Vector3 Velocity => rb ? rb.linearVelocity : Vector3.zero;   // 429R: no proxy

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>
        /// The slime's look in "unit" space (radius 1 around the root, bottom at y = -1):
        /// Vis (size) > Jelly (squash, pivot at the bottom) > the two-part Giant_Slime model (lean, rock, hop), whose head
        /// (Giant_Slime_Below) opens the mouth. A squashed sphere stands in if the model is missing.
        /// </summary>
        public static Transform BuildVisual(Transform root, byte look, out SlimeAnim anim)
        {
            Color c = Colors[look % Colors.Length];
            var bodyMat = Gfx.Lit(c, 0.92f, c * 0.25f);
            var headMat = Gfx.Lit(Color.Lerp(c, Color.white, 0.12f), 0.92f, c * 0.25f);
            var vis = new GameObject("Vis").transform;
            vis.SetParent(root, false);
            var jelly = new GameObject("Jelly").transform;
            jelly.SetParent(vis, false);
            jelly.localPosition = new Vector3(0f, -1f, 0f);

            anim = root.gameObject.AddComponent<SlimeAnim>();
            anim.jelly = jelly;
            // One model, one material, one texture for every slime: each sets only its Paint Colour (the painted parts
            // take it; the stem, face and glowing eyes stay as painted).
            var skin = Gfx.SlimeMaterial();
            Transform model;
            if (skin)
            {
                model = ModelLib.Spawn(Model, jelly, 0f, 2f, r => skin);
                if (model)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor(PaintColor, c);
                    foreach (var r in model.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
                }
            }
            else model = ModelLib.Spawn(Model, jelly, 0f, 2f, r => r.name.Contains("Below") ? headMat : bodyMat);
            if (model)
            {
                anim.body = model;
                foreach (var t in model.GetComponentsInChildren<Transform>())
                    if (t.name.Contains("Below")) { anim.head = t; break; }
            }
            else
            {
                anim.body = Gfx.Visual("Body", Gfx.Sphere, bodyMat, jelly, new Vector3(0f, 0.85f, 0f), new Vector3(2f, 1.7f, 2f), Quaternion.identity);
            }
            return vis;
        }

        /// <summary>Start a clip (429R: no network copies to tell).</summary>
        public void Trigger(SlimeAnim.Clip c)
        {
            if (anim) anim.Play(c);
            Vector3 p = transform.position;
            if (c == SlimeAnim.Clip.Wake) Sfx.Play(Sfx.Sound.SlimeEmerge, p);
            else if (c == SlimeAnim.Clip.Hit) Sfx.Play(Sfx.Sound.SlimeHit, p);
            else if (c == SlimeAnim.Clip.Laugh) Sfx.Play(Sfx.Sound.SlimeLaugh, p);
        }

        /// <summary>Set the size (both sides); on the host also the collider and weight.</summary>
        public void SetSize(float s)
        {
            size = s;
            if (col) col.radius = Radius;
            if (rb) rb.mass = T.slimeMass * size * size;
        }

        void Update()
        {
            if (!vis) return;
            float dt = Time.deltaTime;
            shownSize = shownSize <= 0f ? size : Mathf.MoveTowards(shownSize, size, dt * 1.5f);
            emerge = Mathf.Min(1f, emerge + dt * 2.5f);
            float e = emerge - 1f;
            float pop = 1f + 2.7f * e * e * e + 1.7f * e * e;   // squeezes out of the rock with a little overshoot
            vis.localScale = Vector3.one * (T.slimeRadius * shownSize * Mathf.Max(0.01f, pop));   // squash and poses: SlimeAnim
            if (rainbow) UpdateRainbow();
        }

        /// <summary>Every computer: the colour cycle and a trail of rainbow sparkles (drawn here, not sent).</summary>
        void UpdateRainbow()
        {
            Color c = Rainbow(Time.time * 0.7f);
            if (paint != null)
                foreach (var r in paint)
                {
                    if (!r) continue;
                    r.GetPropertyBlock(paintBlock);
                    paintBlock.SetColor(PaintColor, c);
                    r.SetPropertyBlock(paintBlock);
                }
            if (Time.time < nextSparkle || falling) return;
            nextSparkle = Time.time + 0.06f;
            Vector3 at = transform.position + Random.insideUnitSphere * (Radius * 0.9f) + Vector3.up * (Radius * 0.4f);
            Gfx.Sparks(at, Rainbow(Time.time * 0.7f + Random.value * 0.6f), 2, 2.5f, false);
        }

        void FixedUpdate()
        {
            if (dead) return;   // 429R: no proxy
            var g = ValleyGame.I;
            float dt = Time.fixedDeltaTime;
            hitCooldown -= dt;
            Vector3 pos = rb.position;
            if (falling)
            {
                rb.AddForce(Physics.gravity * T.extraGravity, ForceMode.Acceleration);
                if (Time.time > fallEnd) Remove();
                return;
            }
            // Hopped or got knocked over a cave lip (real physics: it tips in once its centre is past the edge) and
            // dropped: it keeps falling out of sight, then it's gone.
            if (pos.y < -3f)
            {
                falling = true;
                fallEnd = Time.time + Falling.HoleSeconds;
                if (col) col.enabled = false;
                rb.linearDamping = 0f;
                if (rainbow) GotAway();
                return;
            }
            bool grounded = pos.y < Radius + 0.2f;
            if (rainbow)
            {
                RainbowMove(g, pos, dt, grounded);
                return;
            }
            if (laughAt > 0f && Time.time >= laughAt)
            {
                laughAt = 0f;
                Trigger(SlimeAnim.Clip.Laugh);   // happy after a good meal
            }

            if (Time.time > nextLook)
            {
                nextLook = Time.time + 0.6f;
                target = NearestGrass(g, pos);
            }

            // Run from every main ball that's close, harder the closer it is (and the faster it's coming).
            Vector3 away = Vector3.zero;
            foreach (var p in g.players)
            {
                if (!p.alive || !p.head) continue;
                Vector3 d = Flat(pos - p.Position);
                float dist = d.magnitude, r = T.slimeFleeRadius + p.radius;
                if (dist < r && dist > 0.01f) away += d / dist * (1f - dist / r) * (1f + Flat(p.Velocity).magnitude * 0.06f);
            }
            bool fleeing = away.sqrMagnitude > 0.0004f;

            Vector3 desired;
            if (fleeing)
            {
                desired = away.normalized;
                chewUntil = 0f;   // drop the grass and run
                laughAt = 0f;
            }
            else if (target)
            {
                Vector3 to = Flat(target.transform.position - pos);
                if (to.magnitude < Radius + 0.7f)
                {
                    Eat(g, target);
                    target = null;
                    desired = Vector3.zero;
                }
                else desired = to.normalized;
            }
            else
            {
                if (Time.time > nextGoal || Flat(goal - pos).sqrMagnitude < 9f)
                {
                    goal = g.RandomFloorPoint(g.FieldM(14f));   // 429R: scaled
                    nextGoal = Time.time + Random.Range(2f, 5f);
                }
                desired = Flat(goal - pos).normalized;   // busy little things: always on the move
            }
            desired += Avoid(g, pos) * 1.6f;   // keep away from caves and the rim
            if (Time.time < chewUntil) desired = Vector3.zero;

            if (grounded && Time.time > nextHop && desired.sqrMagnitude > 0.01f)
            {
                float speed = (fleeing ? T.slimeFleeHopSpeed : T.slimeHopSpeed) / Mathf.Sqrt(size);   // fat slimes are slower
                Vector3 dir = desired.normalized;
                rb.linearVelocity = new Vector3(dir.x * speed, T.slimeHopUp * (fleeing ? 0.8f : 1f), dir.z * speed);
                nextHop = Time.time + (fleeing ? Random.Range(0.22f, 0.35f) : Random.Range(T.slimeHopInterval.x, T.slimeHopInterval.y));   // quicker still when scared
            }
            rb.linearDamping = grounded ? 2f : 0.05f;
            rb.AddForce(Physics.gravity * T.extraGravity, ForceMode.Acceleration);

            Vector3 flat = Flat(rb.linearVelocity);
            if (flat.sqrMagnitude > 0.3f)
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up), 1f - Mathf.Exp(-8f * dt)));
        }

        /// <summary>
        /// Host: the Rainbow Slime never stops to eat. It bolts from every ball that comes near (from farther off and much
        /// faster than a slime), jumps sideways out of the path of a ball charging straight at it, keeps clear of the
        /// caves and the rim, and once its time is up heads back into the nearest cave.
        /// </summary>
        void RainbowMove(ValleyGame g, Vector3 pos, float dt, bool grounded)
        {
            float age = Time.time - bornAt;
            if (!leaving && age > T.rainbowSlimeLife) leaving = true;
            if (leaving && age > T.rainbowSlimeLife + 12f) { Vanish(); return; }   // couldn't find its way in: poof

            Vector3 desired;
            bool fleeing = false;
            if (leaving)
            {
                var cave = g.valley.NearestCave(pos);
                desired = Flat(cave.mouth + cave.openingOffset - pos).normalized;
            }
            else
            {
                Vector3 away = Vector3.zero;
                float reach = T.slimeFleeRadius * T.rainbowFleeRange;
                BallPlayer closest = null;
                float closestD = float.MaxValue;
                foreach (var p in g.players)
                {
                    if (!p.alive || !p.head) continue;
                    Vector3 d = Flat(pos - p.Position);
                    float dist = d.magnitude, r = reach + p.radius;
                    if (dist >= r || dist < 0.01f) continue;
                    away += d / dist * (1f - dist / r) * (1f + Flat(p.Velocity).magnitude * 0.08f);
                    if (dist < closestD) { closestD = dist; closest = p; }
                }
                // Dodge: a ball coming straight at it makes it jump aside, out of its path, not just run on ahead.
                if (closest)
                {
                    Vector3 v = Flat(closest.Velocity);
                    Vector3 toMe = Flat(pos - closest.Position).normalized;
                    if (v.magnitude > 4f && Vector3.Dot(v.normalized, toMe) > 0.75f)
                    {
                        Vector3 side = Vector3.Cross(Vector3.up, v.normalized);
                        if (Vector3.Dot(side, toMe) < 0f) side = -side;   // the side it's already on
                        away += side * 1.2f;
                    }
                }
                fleeing = away.sqrMagnitude > 0.0004f;
                if (fleeing) desired = away.normalized;
                else
                {
                    if (Time.time > nextGoal || Flat(goal - pos).sqrMagnitude < 9f)
                    {
                        goal = g.RandomFloorPoint(g.FieldM(14f));   // 429R: scaled
                        nextGoal = Time.time + Random.Range(1.5f, 3.5f);
                    }
                    desired = Flat(goal - pos).normalized;
                }
                desired += Avoid(g, pos) * 1.6f;   // keep away from caves and the rim
            }

            if (grounded && Time.time > nextHop && desired.sqrMagnitude > 0.01f)
            {
                float speed = T.slimeFleeHopSpeed * (fleeing || leaving ? T.rainbowSpeed : 1.1f);
                Vector3 dir = desired.normalized;
                rb.linearVelocity = new Vector3(dir.x * speed, T.slimeHopUp * 0.75f, dir.z * speed);
                nextHop = Time.time + (fleeing ? Random.Range(0.14f, 0.22f) : Random.Range(0.25f, 0.4f));
            }
            rb.linearDamping = grounded ? 1.2f : 0.05f;   // slides on a little between hops
            rb.AddForce(Physics.gravity * T.extraGravity, ForceMode.Acceleration);
            Vector3 flat = Flat(rb.linearVelocity);
            if (flat.sqrMagnitude > 0.3f)
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up), 1f - Mathf.Exp(-10f * dt)));
        }

        /// <summary>Host: the Rainbow Slime dropped back into a cave before anyone caught it.</summary>
        void GotAway()
        {
            if (gone) return;
            gone = true;
            ValleyGame.I.AddFeed("The Rainbow Slime got away!", new Color(1f, 0.6f, 0.92f));
        }

        /// <summary>Host: out of time and nowhere near a cave: it vanishes in a burst of sparkles.</summary>
        void Vanish()
        {
            Vector3 p = transform.position;
            for (int i = 0; i < 4; i++) Gfx.Sparks(p, Rainbow(i * 0.25f), 6, 6f);
            GotAway();
            Remove();
        }

        GrassTuft NearestGrass(ValleyGame g, Vector3 pos)
        {
            GrassTuft best = null;
            float bestD = T.slimeSightRadius * T.slimeSightRadius;
            foreach (var gt in g.grass)
            {
                if (!gt) continue;
                float d = Flat(gt.transform.position - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = gt; }
            }
            return best;
        }

        void Eat(ValleyGame g, GrassTuft gt)
        {
            g.EatGrass(gt);
            eaten++;
            SetSize(Mathf.Min(T.slimeMaxSize, size + T.slimeGrowPerGrass));
            chewUntil = Time.time + 0.55f;   // a quick gulp, then off again (the Eat clip finishes on the move)
            Trigger(SlimeAnim.Clip.Eat);
            laughAt = Random.value < 0.35f ? Time.time + 0.9f : 0f;
        }

        Vector3 Avoid(ValleyGame g, Vector3 pos)
        {
            Vector3 push = Vector3.zero;
            foreach (var c in g.valley.caves)
            {
                Vector3 to = Flat(c.mouth - pos);
                float d = to.magnitude, danger = c.DangerRadius + ValleyGame.I.FieldM(6f) + Radius;   // 429R: scaled
                if (d < danger && d > 0.01f) push -= to / d * (1f - d / danger) * 2f;
            }
            float edge = ValleyGame.I.FieldM(6f) + Radius;   // 429R: scaled
            float ex = g.valley.HalfX - Mathf.Abs(pos.x), ez = g.valley.HalfZ - Mathf.Abs(pos.z);
            if (ex < edge) push.x -= Mathf.Sign(pos.x) * (1f - ex / edge);
            if (ez < edge) push.z -= Mathf.Sign(pos.z) * (1f - ez / edge);
            return push;
        }

        /// <summary>Knocked by a tail ball or a loose ball (main balls eat it first): it wobbles and keeps going.</summary>
        void OnCollisionEnter(Collision c)
        {
            if (dead || hitCooldown > 0f) return;   // 429R: no proxy
            if (!c.collider.GetComponent<BallPart>() && !c.collider.GetComponent<LooseBall>()) return;
            if (c.relativeVelocity.magnitude < 2.5f) return;
            hitCooldown = 0.3f;
            Trigger(SlimeAnim.Clip.Hit);
            laughAt = 0f;
        }

        /// <summary>A main ball caught it.</summary>
        public void GetEaten()
        {
            if (dead) return;
            dead = true;
            gone = true;
            Vector3 p = transform.position;
            if (rainbow)
                for (int i = 0; i < 6; i++) Gfx.Sparks(p, Rainbow(i / 6f), 8, 8f);   // a burst in every colour
            else
            {
                Gfx.Puff(p, Color.Lerp(BodyColor, Color.white, 0.25f), 8 + eaten, 0.5f + Radius, 3f);
                Gfx.Sparks(p, Color.Lerp(BodyColor, Color.white, 0.5f), 8, 4f);
            }
            Remove();
        }

        void Remove()
        {
            dead = true;
            ValleyGame.I.slimes.Remove(this);
            Destroy(gameObject);
        }
    }
}
