using System.Collections.Generic;
using UnityEngine;
using ValleyGame = Balls.BallValleyStage;   // 429R: the gate stage took BALLS' ValleyGame's place

namespace Balls
{
    /// <summary>One physical ball: the main ball (head) or one of the smaller balls following it (tail).</summary>
    public class BallPart : MonoBehaviour
    {
        public enum State { Following, Absorbing, Inside }

        public BallPlayer owner;
        public bool isHead;
        public Rigidbody rb;
        public SphereCollider col;
        public MeshRenderer mr;
        public Vector3 prevVel;     // velocity before this physics step (used to judge who hit whom)
        public Vector3 prevPos;     // position before this physics step (where a fast hit really met)
        public float freeUntil;     // tail ball flies free like a pinball until this time
        public bool ghost;          // tail ball too far away: passes through things while it catches up
        public float pop = 1f;      // pop-in animation 0..1
        public float targetR;       // radius this ball should have (from its place in the snake)
        public float curR;          // current (animated) radius
        public State state;         // special move: tail balls get pulled inside the main ball
        public int physId;

        void OnDestroy() => BallPhysics.Unregister(physId);

        public void SetGhost(bool on)
        {
            if (ghost == on) return;
            ghost = on;
            col.isTrigger = on;
            rb.useGravity = !on;
            Vector3 v = rb.linearVelocity;
            if (v.y > 0f) rb.linearVelocity = new Vector3(v.x, 0f, v.z);
        }

        void OnCollisionEnter(Collision c)
        {
            if (owner) owner.OnPartCollision(this, c);
        }

        // 429R: the VR body's trigger: balls coming into it, and staying pressed against the player.
        void OnTriggerEnter(Collider other)
        {
            if (owner && owner.vrBody && isHead) owner.VrContact(other, true);
        }

        void OnTriggerStay(Collider other)
        {
            if (owner && owner.vrBody && isHead) owner.VrContact(other, false);
        }
    }

    /// <summary>
    /// A player (human or bot): a snake of balls, biggest at the head.
    /// You start as ball B1 (size S1). Every new ball becomes the new head, one size step bigger
    /// (S2 = 1.2 S1, S3 = 1.4 S1 ...), and the old head joins the tail. Tail to head: B1 B2 ... Bn.
    /// The main ball only dies by falling into a cave.
    /// </summary>
    public class BallPlayer : MonoBehaviour
    {
        public string displayName;
        public Color color;
        public bool isHuman;         // a person plays it (no auto-respawn); may be on another computer
        public bool isLocal;         // the person at THIS computer (camera, HUD, cue-ball spots)
        public IBallBrain brain;

        // 429R: no networking (BALLS' netId, ownerClientId, proxy, netVelocity).
        /// <summary>429R: never grows a tail (the VR player's ball, a later step): food only scores points.</summary>
        public bool neverGrows;
        /// <summary>
        /// 429R (gate 3, 2026-11-18): the VR player's body. Set before it's spawned: its main ball is an invisible trigger
        /// that follows the player (<see cref="follow"/>), with no face, smoke or sounds, and none of a ball's own doings
        /// (no rolling, eating or dropping into a hole by itself). Bots see it and hunt it like any ball; what a ball touching
        /// it does (a shove, a hurt, being pushed along) is Ball Valley Player's, through <see cref="vrContact"/>.
        /// </summary>
        public bool vrBody;
        /// <summary>429R (VR body): where its main ball's centre goes each physics step.</summary>
        public System.Func<Vector3> follow;
        /// <summary>429R (VR body): a collider came into its trigger (true) or is still in it (false).</summary>
        public System.Action<Collider, bool> vrContact;
        /// <summary>429R: a fixed main-ball radius (0: by its number of balls, as in BALLS), e.g. the VR body.</summary>
        public float fixedRadius;
        /// <summary>429R gate 3 (2026-11-23): times this ball's weight (the Black Pumpkin Ball: 3, the same size, 3 times as heavy).</summary>
        [System.NonSerialized] public float massTimes = 1f;
        /// <summary>429R gate 3 (2026-11-23 evening): times this ball's size, its tail's too (the seed balls: a little bigger).</summary>
        [System.NonSerialized] public float sizeTimes = 1f;
        /// <summary>429R gate 3 (2026-11-23 evening): a pulsing glow on this ball with sparks flying off it (the seed balls: red; alpha 0 = none).</summary>
        [System.NonSerialized] public Color aura;
        float nextAuraSpark;
        /// <summary>429R gate 3 (2026-11-23 evening): an aura effect that follows this ball, upright and sized to it (the seed balls: the game's red Chaos_Aura).</summary>
        [System.NonSerialized] public GameObject auraLook;
        GameObject auraMade;
        // The aura's width for the ball's (the pumpkin boss wears it at 1.15: here its glow must show round a solid ball).
        const float AuraWidth = 1.6f;

        // The valley is bright where the boss room is dark: this ball's own copy of the aura gets its glow and wisps more
        // solid and its wisps thicker (the prefab stays as the boss wears it). Its big soft Glow is smaller and lifted: at
        // full size, on a ball sitting on the lawn, it cut a hard line where it met the ground (play test 2026-11-23).
        static void LouderAura(GameObject made)
        {
            foreach (var ps in made.GetComponentsInChildren<ParticleSystem>(true))
            {
                bool glow = ps.name == "Glow";
                if (glow)
                {
                    ps.transform.localPosition += Vector3.up * 0.25f;
                    var gm = ps.main;
                    gm.startSizeMultiplier *= 0.7f;
                }
                var col = ps.colorOverLifetime;
                if (col.enabled && col.color.mode == ParticleSystemGradientMode.Gradient && col.color.gradient != null)
                {
                    var src = col.color.gradient;
                    var alphas = src.alphaKeys;
                    for (int i = 0; i < alphas.Length; i++) alphas[i].alpha = Mathf.Clamp01(alphas[i].alpha * (glow ? 2f : 3f));
                    var louder = new Gradient { mode = src.mode };
                    louder.SetKeys(src.colorKeys, alphas);
                    col.color = new ParticleSystem.MinMaxGradient(louder);
                }
                if (glow) continue;
                var main = ps.main;
                main.maxParticles = Mathf.CeilToInt(main.maxParticles * 3f);
                var em = ps.emission;
                em.rateOverTimeMultiplier *= 2.5f;
            }
        }
        /// <summary>
        /// 429R gate 3 (2026-11-23): when the VR player last touched this ball (a punch, the dash, their body, or a ball they
        /// hit): it's the player's pocket if it goes down soon after (Ball Valley Gate's Credit Seconds).
        /// </summary>
        [System.NonSerialized] public float lastHumanTouch = -99f;
        /// <summary>429R gate 3 (2026-11-23): dresses the main ball as it spawns, instead of its face and spots (the Black Pumpkin Ball).</summary>
        [System.NonSerialized] public System.Action<BallPart> dressHead;

        public void VrContact(Collider other, bool entered) => vrContact?.Invoke(other, entered);

        public BallPart head;
        /// <summary>Tail, nearest the head first: babies[0] = B(n-1) ... last = B1 (smallest).</summary>
        public readonly List<BallPart> babies = new List<BallPart>();
        readonly List<Vector3> trail = new List<Vector3>();

        public bool alive;
        public float food;
        /// <summary>This match: players you potted, times you were potted (or fell), and pots you helped with (K / D / A).</summary>
        public int kills, deaths, assists;
        /// <summary>Drop-ins left this round after being pocketed (0 while dead = spectator).</summary>
        public int revives;
        /// <summary>Out when a new round started: the next drop-in is free (doesn't use a revive).</summary>
        public bool freeDrop;
        /// <summary>Rounds won (last ball standing) this session: the Glory Board.</summary>
        public int wins;
        /// <summary>Can drop (back) into the valley: has a revive left or a free drop.</summary>
        public bool CanDropIn => revives > 0 || freeDrop;
        /// <summary>Leaderboard score: food eaten, players pocketed, time as King. Kept through deaths (the whole match counts).</summary>
        public float points;
        // 429R: no replay vote, no chat (BALLS' votedReplay, chatText, chatUntil).
        public float radius;
        public float targetRadius;
        public Vector3 aimDir = Vector3.forward;
        public bool charging;
        public float charge01;
        public bool absorbed;        // tail is pulled inside the main ball (special move)
        public bool shotRolling;     // launched: no control until the ball has rolled out (snooker shot)
        public bool knocked;         // hit by a special move: no control, rolls out far like a struck snooker ball
        float knockTime, knockBurstUntil;
        public bool Locked => shotRolling || knocked;
        public float dashReadyAt;
        public float dashUntil;
        public float shieldUntil;
        public float deathTime;
        public BallPlayer lastAttacker;
        public float lastAttackTime;
        // Everyone who hit this ball lately, and when: if it goes down a hole, the last one gets the kill, the rest an assist.
        readonly List<BallPlayer> hitBy = new List<BallPlayer>();
        readonly List<float> hitAt = new List<float>();
        public BallPlayer killedBy;
        public Vector3 lastPosition;
        /// <summary>Went down a hole last time (rather than off the edge), decided when it happened: the holes move with the valley.</summary>
        public bool diedInHole;
        /// <summary>How many balls it had (main ball + tail) when it last went down: a pocketed winner's portrait shows them.</summary>
        public int ballsAtDeath = 1;

        float damageAccum;
        float flash;
        bool grounded;
        float airTime;
        float launchTime = -99f;
        Vector3 lastFloorPos;
        // The shot's spin (snooker): set at the launch from how wound up it was, wearing off with the distance rolled
        // (shotTravel), used up by the first ball it hits.
        float shotSpin0;
        float shotTravel;
        bool shotSpinLive;

        Material headMat, babyMat;
        Transform face;
        ParticleSystem smoke;

        static readonly Collider[] overlap = new Collider[64];
        const float TailGap = 0.15f;

        Tuning T => ValleyGame.I.tune;
        public int BabyCount => babies.Count;
        /// <summary>All balls including the main one (what the HUD shows).</summary>
        public int TotalBalls => babies.Count + 1;
        public bool Dashing => Time.time < dashUntil;
        public bool Shielded => Time.time < shieldUntil;
        /// <summary>
        /// Caught the Rainbow Slime: crashes cost no balls, no hit knocks it away, and it weighs Rainbow Weight times as
        /// much (pushes harder, hardly budges). Only a cave can still get it.
        /// </summary>
        public bool Invincible => Time.time < invincibleUntil;
        public float InvincibleLeft => Mathf.Max(0f, invincibleUntil - Time.time);
        [System.NonSerialized] public float invincibleUntil;
        /// <summary>The main ball's physics weight: its hitting weight, times Rainbow Weight while invincible.</summary>
        float BodyMass => Mass * (Invincible ? T.rainbowWeight : 1f);
        float nextRainbowSparkle;

        /// <summary>Host: invincible for <paramref name="seconds"/> (the Rainbow Slime's power).</summary>
        public void MakeInvincible(float seconds)
        {
            invincibleUntil = Time.time + seconds;
            knocked = false;   // back in control at once
            if (head) head.rb.mass = BodyMass;
        }
        public bool DashReady => Time.time >= dashReadyAt;
        public float DashCooldown01 => DashReady ? 1f : 1f - (dashReadyAt - Time.time) / T.dashCooldown;
        /// <summary>Hitting weight: grows with head size, keeps growing with ball count; the special move adds the swallowed tail.</summary>
        public float Mass => HeadMass + (absorbed ? TailMass() : 0f);
        float HeadMass => (T.baseMass * Sq(targetRadius / T.headRadius) + T.massPerBaby * babies.Count) * massTimes;   // 429R: Mass Times
        public float Size01 => Mathf.InverseLerp(T.headRadius, T.headMaxRadius, radius);
        public int NextCost => Mathf.RoundToInt(T.baseCost + babies.Count * T.costPerBaby);
        /// <summary>Top rolling speed: more balls and a bigger main ball both make you faster.</summary>
        public float TopSpeed => T.baseTopSpeed * (1f + T.topSpeedPerSqrtBaby * Mathf.Sqrt(babies.Count)) * (1f + T.topSpeedPerSize * Size01);
        public Vector3 Position => head ? head.transform.position : lastPosition;
        public Vector3 Velocity => head ? head.rb.linearVelocity : Vector3.zero;   // 429R: no network copies

        static float Sq(float x) => x * x;

        /// <summary>Radius of ball number k (1-based): S1 * (1 + step * (k - 1)), capped at the max size.</summary>
        public float SizeRadius(int k) => fixedRadius > 0f ? fixedRadius * sizeTimes
            : Mathf.Min(T.headMaxRadius, T.headRadius * (1f + T.sizeStep * (k - 1)) * sizeTimes);   // 429R: Size Times (never past the max: the pits still take it)

        float TailBallMass(float r) => T.babyMass * Sq(r / T.headRadius);

        float TailMass()
        {
            float m = 0f;
            foreach (var b in babies) m += b.rb.mass;
            return m;
        }

        public void Init(string n, Color c, bool human, bool local, IBallBrain b)
        {
            displayName = n;
            color = c;
            isHuman = human;
            isLocal = local;
            brain = b;
            headMat = Gfx.NewLit(c, 0.88f, Color.black);
            babyMat = Gfx.Lit(Color.Lerp(c, Color.white, 0.12f), 0.85f);
        }

        /// <summary>Recolour this player (the colour picked on the start screen), balls already out included.</summary>
        public void SetColor(Color c)
        {
            color = c;
            if (headMat) headMat.SetColor("_BaseColor", c);
            babyMat = Gfx.Lit(Color.Lerp(c, Color.white, 0.12f), 0.85f);
            foreach (var b in babies)
                if (b && b.mr) b.mr.sharedMaterial = babyMat;
        }

        // ---------- life cycle ----------

        public void Spawn(Vector3 pos)
        {
            CleanupParts();
            alive = true;
            freeDrop = false;
            food = 0f;
            kills = 0;
            damageAccum = 0f;
            flash = 0f;
            charging = false;
            charge01 = 0f;
            absorbed = false;
            shotRolling = false;
            shotSpinLive = false;
            knocked = false;
            dashUntil = 0f;
            dashReadyAt = Time.time + 1f;
            shieldUntil = Time.time + T.spawnShield;
            invincibleUntil = 0f;   // a new life starts without the Rainbow Slime's power
            lastAttacker = null;
            lastHumanTouch = -99f;   // 429R
            hitBy.Clear();
            hitAt.Clear();
            killedBy = null;
            radius = targetRadius = SizeRadius(1);
            Vector3 toCenter = -pos;
            toCenter.y = 0f;
            aimDir = toCenter.sqrMagnitude > 1f ? toCenter.normalized : Vector3.forward;

            head = CreatePart(true, pos, radius);
            if (!vrBody && dressHead != null) dressHead(head);   // 429R gate 3: its own look (no face)
            else if (!vrBody)   // 429R: the VR body has no face or smoke (nobody sees it)
            {
                if (!face) face = BuildFace();
                face.gameObject.SetActive(true);
                face.SetPositionAndRotation(pos, Quaternion.LookRotation(aimDir));
                if (!smoke) smoke = Gfx.MakeSmoke(transform);
            }

            trail.Clear();
            trail.Add(new Vector3(pos.x, 0f, pos.z));
            lastFloorPos = new Vector3(pos.x, 0f, pos.z);
            for (int i = 0; i < T.startBabies && !neverGrows && babies.Count < T.maxBabies; i++) GainBall(false);   // 429R: Never Grows, Max Babies
            RefreshSize();
            radius = targetRadius;
            foreach (var b in babies) b.curR = b.targetR;
            lastPosition = pos;
            if (isHuman && !vrBody) Sfx.Play(Sfx.Sound.Respawn, pos);
        }

        /// <summary>Removes the balls without dropping food (player left the game).</summary>
        public void Despawn()
        {
            alive = false;
            CleanupParts();
        }

        void CleanupParts()
        {
            if (head) Destroy(head.gameObject);
            head = null;
            foreach (var b in babies) if (b) Destroy(b.gameObject);
            babies.Clear();
        }

        /// <summary>Another player's ball hit this one: the last to do so gets the kill if it goes down soon, the others an assist.</summary>
        void NoteHit(BallPlayer enemy)
        {
            lastAttacker = enemy;
            lastAttackTime = Time.time;
            // 429R gate 3: the VR player's body bumped it, or a ball they sent off did (snooker: their shot). A chain keeps
            // the time of the player's own touch, so the credit still runs out (Ball Valley Gate's Credit Seconds).
            if (enemy) lastHumanTouch = Mathf.Max(lastHumanTouch, enemy.isHuman ? Time.time : enemy.lastHumanTouch);
            for (int i = hitBy.Count - 1; i >= 0; i--)   // forget old hits and the one being renewed
                if (!hitBy[i] || hitBy[i] == enemy || Time.time - hitAt[i] >= T.killCreditWindow)
                {
                    hitBy.RemoveAt(i);
                    hitAt.RemoveAt(i);
                }
            hitBy.Add(enemy);
            hitAt.Add(Time.time);
        }

        public void Die(Cave cave)
        {
            if (!alive) return;
            alive = false;
            deathTime = Time.time;
            ballsAtDeath = TotalBalls;
            if (head) lastPosition = head.transform.position;
            diedInHole = ValleyGame.I.DiedInHole(this);
            killedBy = lastAttacker && lastAttacker != this && lastAttacker.alive && Time.time - lastAttackTime < T.killCreditWindow
                ? lastAttacker : null;
            deaths++;
            if (killedBy)
            {
                killedBy.kills++;
                killedBy.points += T.pointsPerKill + T.pointsPerVictimBall * TotalBalls;   // bigger catch, bigger reward
                for (int i = 0; i < hitBy.Count; i++)   // everyone else who hit it in that time helped
                    if (hitBy[i] && hitBy[i] != killedBy && hitBy[i] != this && Time.time - hitAt[i] < T.killCreditWindow) hitBy[i].assists++;
            }
            hitBy.Clear();
            hitAt.Clear();

            // Every tail ball becomes food and scatters back into the valley.
            foreach (var b in babies)
            {
                if (!b) continue;
                float r = b.targetR;
                Vector3 bp = b.state == BallPart.State.Following
                    ? b.transform.position
                    : lastFloorPos + RandomFlat() * (radius + r);       // was inside the main ball
                bp.y = Mathf.Max(bp.y, r + 0.2f);
                Vector3 toCenter = -bp;
                toCenter.y = 0f;
                toCenter.Normalize();
                Vector3 v = (toCenter * 0.8f + RandomFlat()).normalized * Random.Range(5f, 12f) + Vector3.up * Random.Range(1f, 3f);
                b.gameObject.SetActive(false);
                Destroy(b.gameObject);
                ValleyGame.I.SpawnLooseBall(bp, v, r, this);   // (a white ball now: food for anyone)
            }
            babies.Clear();
            absorbed = false;
            if (head)
            {
                // It's out of the game now, but a look-alike keeps falling down the pit until it's out of sight.
                Falling.DropCopy(head.transform, head.rb.linearVelocity, Falling.HoleSeconds, head.rb.angularVelocity * Mathf.Rad2Deg);
                head.gameObject.SetActive(false);
                Destroy(head.gameObject);
            }
            head = null;
            if (face) face.gameObject.SetActive(false);
            SetSmoke(0f);
            Sfx.Play(Sfx.Sound.Pocket, lastPosition);
            ValleyGame.I.OnPlayerDied(this, killedBy, cave);
        }

        static Vector3 RandomFlat()
        {
            float a = Random.value * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        // ---------- building parts ----------

        BallPart CreatePart(bool isHead, Vector3 pos, float r)
        {
            var go = new GameObject(isHead ? displayName : displayName + " ball");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (r * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = isHead ? Gfx.BallSphere : Gfx.SmallSphere;   // a lighter tail: there can be lots
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = isHead ? headMat : babyMat;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            col.sharedMaterial = Gfx.BallPhys;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = isHead ? HeadMass : TailBallMass(r);
            var part = go.AddComponent<BallPart>();
            part.owner = this;
            part.isHead = isHead;
            part.rb = rb;
            part.col = col;
            part.mr = mr;
            part.targetR = part.curR = r;
            if (vrBody && isHead)
            {
                // 429R: the VR body: an invisible trigger carried along with the player (Ball Valley Player handles touches).
                mr.enabled = false;
                col.isTrigger = true;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.None;
                ValleyGame.SetupBody(rb);
                return part;
            }
            // 429R: no network copies (BALLS' proxy branch): always simulated here.
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = isHead ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Continuous;
            rb.maxAngularVelocity = 150f;
            rb.linearDamping = isHead ? 0.5f : 0.3f;
            rb.angularDamping = 0.4f;
            rb.maxDepenetrationVelocity = isHead ? 6f : 4f;   // overlaps ease apart instead of popping balls into the air
            part.physId = BallPhysics.Register(col);
            ValleyGame.SetupBody(rb);   // 429R: kept off 429R's Ground box and Teleport layer; BALLS' solver steps
            if (isHead)
            {
                // Two spots so you can see the ball roll and spin (your own ball is the cue ball with red spots).
                var spot = Gfx.Lit(isLocal ? new Color(0.85f, 0.1f, 0.1f) : Color.white, 0.8f);
                Gfx.Visual("Spot", Gfx.SmallSphere, spot, go.transform, new Vector3(0f, 0f, 0.47f), new Vector3(0.36f, 0.36f, 0.1f), Quaternion.identity);
                Gfx.Visual("Spot", Gfx.SmallSphere, spot, go.transform, new Vector3(0f, 0f, -0.47f), new Vector3(0.36f, 0.36f, 0.1f), Quaternion.identity);
            }
            return part;
        }

        Transform BuildFace()
        {
            var f = new GameObject("Face").transform;
            f.SetParent(transform, false);
            var white = Gfx.Lit(Color.white, 0.9f);
            var black = Gfx.Lit(new Color(0.03f, 0.03f, 0.03f), 0.9f);
            for (int s = -1; s <= 1; s += 2)
            {
                // Sit high on the front so the eyes read from the top-down camera.
                var eye = Gfx.Visual("Eye", Gfx.SmallSphere, white, f, new Vector3(0.16f * s, 0.33f, 0.27f), Vector3.one * 0.3f, Quaternion.identity);
                Gfx.Visual("Pupil", Gfx.SmallSphere, black, eye, new Vector3(0f, 0.12f, 0.3f), Vector3.one * 0.5f, Quaternion.identity);
            }
            return f;
        }

        /// <summary>
        /// A new ball is born: it becomes the head (one size step bigger) and the old head size drops in
        /// right behind it as the first tail ball.
        /// </summary>
        public void GainBall(bool fx = true)
        {
            if (!head) return;
            float r = SizeRadius(TotalBalls);   // the current head size becomes the newest tail ball
            Vector3 hp = head.transform.position;
            Vector3 p = hp - aimDir * (radius * 0.5f);
            p.y = Mathf.Max(hp.y, r);
            var b = CreatePart(false, p, r);
            Physics.IgnoreCollision(b.col, head.col);
            for (int i = 0; i < babies.Count; i++) Physics.IgnoreCollision(b.col, babies[i].col);
            b.rb.linearVelocity = head.rb.linearVelocity;
            babies.Insert(0, b);
            if (absorbed) SetInside(b, true);
            if (fx)
            {
                b.pop = 0f;
                b.transform.localScale = Vector3.one * 0.01f;
                Gfx.Sparks(hp, Color.Lerp(color, Color.white, 0.6f), 14, 5f);
                Sfx.Play(Sfx.Sound.GainBall, hp);
            }
            RefreshSize();
        }

        /// <summary>Sizes always follow the order B1..Bn (tail end to head); masses follow sizes.</summary>
        void RefreshSize()
        {
            int n = TotalBalls;
            targetRadius = SizeRadius(n);
            for (int i = 0; i < babies.Count; i++)
            {
                var b = babies[i];
                b.targetR = SizeRadius(n - 1 - i);
                b.rb.mass = TailBallMass(b.targetR);
            }
            if (head) head.rb.mass = BodyMass;
        }

        /// <summary>Lose balls from the tail end (smallest first); the rest step down one size.</summary>
        void Shed(int count)
        {
            for (int k = 0; k < count && babies.Count > 0; k++)
            {
                var b = babies[babies.Count - 1];
                babies.RemoveAt(babies.Count - 1);
                float r = b.targetR;
                Vector3 bp = b.state == BallPart.State.Following
                    ? b.transform.position
                    : head.transform.position + RandomFlat() * (radius + r);
                bp.y = Mathf.Max(bp.y, r + 0.1f);
                Vector3 v = (b.state == BallPart.State.Following ? b.rb.linearVelocity * 0.5f : head.rb.linearVelocity * 0.3f)
                            + RandomFlat() * Random.Range(4f, 8f) + Vector3.up * Random.Range(1f, 2.5f);
                b.gameObject.SetActive(false);
                Destroy(b.gameObject);
                ValleyGame.I.SpawnLooseBall(bp, v, r, this);   // (a white ball now: food for anyone)
            }
            if (babies.Count == 0) absorbed = false;
            RefreshSize();
        }

        // ---------- special move: swallow the tail, spit it out on impact ----------

        void SetInside(BallPart b, bool absorbing)
        {
            b.SetGhost(false);
            b.state = absorbing ? BallPart.State.Absorbing : BallPart.State.Inside;
            b.col.enabled = false;
            b.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            b.rb.isKinematic = true;
        }

        void StartAbsorb()
        {
            if (absorbed || babies.Count == 0) return;
            absorbed = true;
            foreach (var b in babies) SetInside(b, true);
            head.rb.mass = BodyMass;
        }

        /// <summary>Tail balls fly into the main ball and ride inside it.</summary>
        void AbsorbStep(float dt)
        {
            Vector3 hp = head.rb.position;
            foreach (var b in babies)
            {
                if (b.state == BallPart.State.Inside)
                {
                    b.rb.MovePosition(hp);
                    continue;
                }
                Vector3 p = b.rb.position;
                float dist = Vector3.Distance(p, hp);
                Vector3 np = Vector3.MoveTowards(p, hp, (25f + dist * 12f) * dt);
                b.rb.MovePosition(np);
                if (Vector3.Distance(np, hp) < radius * 0.6f)
                {
                    b.state = BallPart.State.Inside;
                    b.mr.enabled = false;
                }
            }
        }

        /// <summary>
        /// Spit the swallowed balls back out, sprayed forward past both sides of whatever was hit
        /// (<paramref name="toward"/> points at the impact). They fly like pinballs, then return to their places.
        /// </summary>
        void Burst(Vector3 toward, float speed)
        {
            if (!absorbed) return;
            absorbed = false;
            Vector3 hp = head.rb.position;
            Vector3 hv = head.rb.linearVelocity;
            hv.y = 0f;
            toward.y = 0f;
            if (toward.sqrMagnitude < 1e-4f) toward = aimDir;
            toward.Normalize();
            for (int i = 0; i < babies.Count; i++)
            {
                var b = babies[i];
                float side = (i & 1) == 0 ? 1f : -1f;
                float r = b.targetR;
                // Come out of the flanks of the main ball, fly forward-and-out past the impact.
                Vector3 outDir = Quaternion.Euler(0f, side * Random.Range(55f, 125f), 0f) * toward;
                Vector3 flyDir = Quaternion.Euler(0f, side * Random.Range(20f, 75f), 0f) * toward;
                Vector3 p = hp + outDir * (radius + r + 0.1f);
                p.y = r + 0.05f;
                b.state = BallPart.State.Following;
                b.mr.enabled = true;
                b.rb.isKinematic = false;
                b.rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                b.rb.position = p;
                b.transform.position = p;
                b.rb.linearVelocity = hv * 0.25f + flyDir * (speed * Random.Range(0.75f, 1.2f));
                b.col.enabled = true;
                b.freeUntil = Time.time + 0.6f;
                b.pop = Mathf.Min(b.pop, 0.3f);
                Physics.IgnoreCollision(b.col, head.col);
            }
            for (int i = 0; i < babies.Count; i++)
                for (int j = i + 1; j < babies.Count; j++)
                    Physics.IgnoreCollision(babies[i].col, babies[j].col);
            head.rb.mass = BodyMass;
            Gfx.Puff(hp, new Color(1f, 1f, 1f, 0.7f), 14, radius * 1.5f, 5f);
        }

        // ---------- simulation ----------

        void FixedUpdate()
        {
            if (!alive || !head) return;   // 429R: no proxy
            if (vrBody)
            {
                // 429R: the VR body only follows the player: no rolling, no pockets, no eating (Ball Valley Player).
                if (follow != null) head.rb.MovePosition(follow());
                head.prevVel = Vector3.zero;
                head.prevPos = head.rb.position;
                return;
            }
            float dt = Time.fixedDeltaTime;
            var rb = head.rb;
            float bodyMass = BodyMass;   // (the Rainbow Slime's weight comes and goes with its power)
            if (Mathf.Abs(rb.mass - bodyMass) > 0.0001f) rb.mass = bodyMass;
            Vector3 pos = rb.position;
            head.prevVel = rb.linearVelocity;
            head.prevPos = pos;
            for (int i = 0; i < babies.Count; i++)
            {
                babies[i].prevVel = babies[i].rb.linearVelocity;
                babies[i].prevPos = babies[i].rb.position;
            }

            // Dropped into a cave (or off the edge) -> pocketed. Real ball-and-lip physics decide it (the floor's
            // edge is a real edge, see BallPhysics): a ball with its centre still over the edge can teeter and roll back,
            // once the centre is past the edge gravity tips it in. Only once it has really dropped is it gone.
            if (pos.y < -2.5f)
            {
                Die(ValleyGame.I.valley.NearestCave(pos));
                return;
            }

            BallInput input = brain != null ? brain.Think(this) : default;
            Vector3 d = input.dir;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) aimDir = d.normalized;

            bool nowGrounded = Physics.Raycast(pos, Vector3.down, radius + 0.3f, ValleyGame.QueryMask, QueryTriggerInteraction.Ignore);   // 429R: the valley's layers only
            if (nowGrounded && !grounded && airTime > 0.35f)
                Gfx.Puff(pos - Vector3.up * radius, new Color(0.75f, 0.7f, 0.6f, 0.7f), 10, radius * 1.5f, 3f);
            airTime = nowGrounded ? 0f : airTime + dt;
            grounded = nowGrounded;
            if (grounded) lastFloorPos = new Vector3(pos.x, 0f, pos.z);

            float s01 = Size01;
            float rollDamping = Mathf.Lerp(T.dampingSmall, T.dampingBig, s01);
            float friction = 0f;   // constant rolling resistance, m/s^2
            if (knocked)
            {
                // Struck by a special move: it carries the shot on (for what was left of the striker's burst), then brakes
                // like a shot rolling out - the same cloth for every ball.
                if (Time.time < knockBurstUntil) rollDamping = Mathf.Min(rollDamping, T.dashDamping);
                else { rollDamping = Mathf.Max(rollDamping, T.shotDamping); friction = T.knockFriction >= 0f ? T.knockFriction : T.shotFriction; }   // 429R: Knock Friction
            }
            else if (Dashing) rollDamping = Mathf.Min(rollDamping, T.dashDamping);
            else if (shotRolling) { rollDamping = Mathf.Max(rollDamping, T.shotDamping); friction = T.shotFriction; }
            rb.linearDamping = grounded ? rollDamping : 0.02f;
            if (grounded && friction > 0f)
            {
                Vector3 fv = rb.linearVelocity;
                Vector3 fFlat = Vector3.MoveTowards(new Vector3(fv.x, 0f, fv.z), Vector3.zero, friction * dt);
                rb.linearVelocity = new Vector3(fFlat.x, fv.y, fFlat.z);
            }

            // Snooker rule: once the shot is played (or you've been knocked) the ball is out of your hands
            // until it rolls slower than shotRecoverSpeed.
            Vector3 sv = rb.linearVelocity;
            sv.y = 0f;
            float flatSpeed = sv.magnitude;
            if (shotRolling && ((!Dashing && flatSpeed < T.shotRecoverSpeed) || Time.time - launchTime > T.shotMaxLock)) shotRolling = false;
            if (shotSpinLive)
            {
                if (shotRolling) shotTravel += flatSpeed * dt;
                else shotSpinLive = false;
            }
            if (knocked && Time.time > knockTime + 0.25f && flatSpeed < (T.knockRecoverSpeed >= 0f ? T.knockRecoverSpeed : T.shotRecoverSpeed)) knocked = false;   // 429R: Knock Recover Speed

            if (Locked)
            {
                // no thrust, no braking, no new charge while the ball rolls out
            }
            else if (input.charge && (charging || (DashReady && grounded)))
            {
                // Burnout: spin in place, kicking up smoke, while the tail gets swallowed and the charge builds.
                // (Keeps charging through tiny hops; only releasing the buttons launches.)
                if (!charging) { charging = true; charge01 = 0f; StartAbsorb(); }
                charge01 = Mathf.Min(1f, charge01 + dt / T.chargeTime);
                if (grounded)
                {
                    Vector3 v = rb.linearVelocity;
                    Vector3 flat = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), Vector3.zero, 45f * dt);
                    rb.linearVelocity = new Vector3(flat.x, Mathf.Min(v.y, 0.5f), flat.z);
                }
                rb.angularVelocity = Vector3.Cross(Vector3.up, aimDir) * (12f + 50f * charge01);
            }
            else
            {
                if (charging)
                {
                    if (charge01 > 0.12f) Launch();
                    else Burst(aimDir, 4f);          // cancelled: just pop the tail back out
                    charging = false;
                    charge01 = 0f;
                }
                if (input.thrust && grounded) Drive(rb, s01, input.speed);
            }

            // Launched but never hit anything: let the tail out once the shot is spent.
            if (absorbed && !charging && Time.time > launchTime + 0.2f)
            {
                Vector3 hv = rb.linearVelocity;
                hv.y = 0f;
                if (Time.time > dashUntil + 0.5f || hv.magnitude < 5f) Burst(hv.sqrMagnitude > 1f ? hv : aimDir, 7f);
            }

            rb.AddForce(Physics.gravity * T.extraGravity, ForceMode.Acceleration);
            if (grounded)
            {
                // Snooker balls stay on the table: no hopping, and spin always matches the roll.
                Vector3 v = rb.linearVelocity;
                if (v.y > 1.5f) rb.linearVelocity = new Vector3(v.x, 1.5f, v.z);
                if (!charging) BallPhysics.RollWithVelocity(rb, radius, shotSpinLive ? ShotSpin : 1f);   // (a shot shows its spin: backspin, sliding, then rolling)
            }
            UpdateTail(dt);
            EatNearby();
        }

        /// <summary>Rolls toward aimDir, up to <paramref name="speed01"/> of top speed (the player's pointer or thumb distance).</summary>
        void Drive(Rigidbody rb, float s01, float speed01)
        {
            Vector3 v = rb.linearVelocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float along = Vector3.Dot(flat, aimDir);
            // Bigger pushes harder (up to its higher top speed) but still takes a little longer to get going.
            // Asking for less speed only stops the push: the ball rolls down to it (big balls stay hard to stop).
            float accel = T.baseAccel * (1f + T.topSpeedPerSize * s01) / (1f + 0.6f * s01);
            if (along < TopSpeed * Mathf.Clamp01(speed01)) rb.AddForce(aimDir * accel, ForceMode.Acceleration);
            Vector3 lateral = flat - aimDir * along;
            rb.AddForce(-lateral * Mathf.Lerp(T.gripSmall, T.gripBig, s01), ForceMode.Acceleration);
        }

        float BaseLaunchSpeed(float c01) => T.dashSpeed * (1f + T.dashPerSqrtBaby * Mathf.Sqrt(babies.Count)) * Mathf.Lerp(0.45f, 1f, c01);

        /// <summary>How fast a shot wound up to <paramref name="c01"/> leaves (the aim guide uses it too).</summary>
        public float LaunchSpeed(float c01) => BaseLaunchSpeed(c01) * LaunchBoost();

        // ---------- snooker: spin, and what a hit does to both balls (the aim guide predicts with these too) ----------

        /// <summary>
        /// The spin a shot wound up to <paramref name="c01"/> has after rolling <paramref name="travelled"/> metres:
        /// it starts with backspin (more the harder it was hit), which wears off as it slides, until it rolls.
        /// Below 0 = backspin (screws back after a full hit), 0 = stun (stops dead), 1 = natural roll (follows through).
        /// </summary>
        public float SpinAt(float c01, float travelled) => Mathf.Min(1f, -T.shotScrew * c01 + travelled / Mathf.Max(0.1f, T.spinRollDistance));

        /// <summary>This ball's shot spin right now (see <see cref="SpinAt"/>).</summary>
        public float ShotSpin => Mathf.Min(1f, shotSpin0 + shotTravel / Mathf.Max(0.1f, T.spinRollDistance));

        /// <summary>
        /// The speed a special-move hit adds to the struck ball along the line of centres. Real snooker balls: it takes
        /// the push the striker loses (into an equal ball on a full hit, nearly all of its speed), so a heavier striker
        /// (the swallowed tail adds its weight) sends it off faster and a heavier target goes off slower.
        /// </summary>
        public float PushSpeed(float closing, float strikerMass, float targetMass) =>
            Mathf.Min(T.maxHitSpeed, (1f + T.ballBounce) * strikerMass / (strikerMass + targetMass) * Mathf.Max(0f, closing));

        /// <summary>
        /// The striker after a hit (flat velocities): the line-of-centres part of its speed goes into the struck ball (on
        /// a full hit on an equal ball it stops; on a cut it peels off along the tangent, 90 degrees from the struck
        /// ball's path), then its <paramref name="spin"/> takes over along the way it was going: backspin draws it back,
        /// roll follows through, and on a cut either one bends its path back or forward.
        /// </summary>
        public Vector3 StrikerAfter(Vector3 u1, Vector3 n, float closing, float strikerMass, float targetMass, float spin)
        {
            float lost = (1f + T.ballBounce) * closing / (1f / strikerMass + 1f / targetMass) / strikerMass;
            Vector3 v1 = u1 - n * lost;
            float speed = u1.magnitude;
            if (speed > 0.01f) v1 += u1 / speed * (spin * T.spinTransfer * Mathf.Min(lost, speed));
            return v1;
        }

        /// <summary>
        /// How far a ball rolls from <paramref name="v0"/> after a hit if nobody steers it: <paramref name="burst"/> more
        /// seconds of a shot's burst (barely slowing), then braking like a shot until it's slow enough to steer again,
        /// then coasting. <paramref name="roll"/>: its rolling damping (see <see cref="RollDamping"/>).
        /// </summary>
        public static float RollOn(Tuning T, float v0, float burst, float roll)
        {
            roll = Mathf.Max(0.05f, roll);
            float x = 0f, v = Mathf.Max(0f, v0);
            if (burst > 0f)
            {
                float d1 = Mathf.Max(0.05f, Mathf.Min(roll, T.dashDamping)), e = Mathf.Exp(-d1 * burst);
                x = v / d1 * (1f - e);
                v *= e;
            }
            float d2 = Mathf.Max(roll, T.shotDamping), k = Mathf.Max(0.01f, T.shotFriction) / d2, vr = T.shotRecoverSpeed;
            if (v > vr)
            {
                float t = Mathf.Log((v + k) / (vr + k)) / d2;
                x += (v + k) * (1f - Mathf.Exp(-d2 * t)) / d2 - k * t;
                v = vr;
            }
            return x + v / roll;
        }

        /// <summary>This ball's rolling damping (bigger balls roll on further).</summary>
        public float RollDamping => Mathf.Lerp(T.dampingSmall, T.dampingBig, Size01);

        /// <summary>How far your own ball rolls on after hitting a main ball at <paramref name="v0"/> (the hit cuts its burst to 0.15 s).</summary>
        public float RollOutDistance(float v0) => RollOn(T, v0, 0.15f, RollDamping);

        /// <summary>How far a shot gets while it's still the launch burst (hits in it are snooker hits), and its speed then.</summary>
        public float DashReach(float v0, out float slidingDamping)
        {
            slidingDamping = Mathf.Max(0.05f, Mathf.Min(Mathf.Lerp(T.dampingSmall, T.dampingBig, Size01), T.dashDamping));
            return v0 / slidingDamping * (1f - Mathf.Exp(-slidingDamping * T.dashDuration));
        }

        /// <summary>
        /// Speed multiplier that makes a full shot roll <c>launchDistance</c> times as far as the base launch speed would
        /// (found by bisection on <see cref="ShotDistance"/>, since braking isn't linear in speed).
        /// </summary>
        float LaunchBoost()
        {
            if (T.launchDistance <= 1.001f) return 1f;
            float v = BaseLaunchSpeed(1f);
            float target = ShotDistance(v) * T.launchDistance;
            float lo = 1f, hi = 4f;
            for (int i = 0; i < 14; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (ShotDistance(v * mid) < target) lo = mid;
                else hi = mid;
            }
            return hi;
        }

        /// <summary>How far a full-power shot would roll on open ground before control comes back (bots use this to avoid suicide shots).</summary>
        public float EstimateShotDistance(float c01 = 1f) => ShotDistance(LaunchSpeed(c01));

        float ShotDistance(float v0)
        {
            float roll = Mathf.Lerp(T.dampingSmall, T.dampingBig, Size01);
            float d1 = Mathf.Max(0.05f, Mathf.Min(roll, T.dashDamping));
            float e = Mathf.Exp(-d1 * T.dashDuration);
            float v1 = v0 * e;
            float d2 = Mathf.Max(0.05f, Mathf.Max(roll, T.shotDamping));
            float f = Mathf.Max(0.01f, T.shotFriction);
            float tStop = Mathf.Log((v1 + f / d2) / (f / d2)) / d2;     // damping + constant friction
            return v0 / d1 * (1f - e) + v1 / d2 - f / d2 * tStop;
        }

        /// <summary>
        /// Hit by a special move (or a knocked ball): lose control and roll out like a struck snooker ball, carrying the
        /// shot's burst on until <paramref name="burstUntil"/> (the striker's, when a special move hit it).
        /// </summary>
        public void Knock(float burstUntil = 0f)
        {
            if (!alive || Invincible || vrBody) return;   // invincible: nothing knocks it out of your hands (429R: nor the VR body)
            knocked = true;
            knockTime = Time.time;
            knockBurstUntil = burstUntil;
            if (charging)
            {
                charging = false;
                charge01 = 0f;
                Burst(aimDir, 4f);
            }
        }

        void Launch()
        {
            var rb = head.rb;
            float speed = LaunchSpeed(charge01);
            Vector3 v = aimDir * speed;
            v.y = rb.linearVelocity.y;
            rb.linearVelocity = v;
            dashUntil = Time.time + T.dashDuration;
            dashReadyAt = Time.time + T.dashCooldown;
            shieldUntil = Mathf.Max(shieldUntil, Time.time + T.shotShield);   // invincible for a moment after the shot
            launchTime = Time.time;
            shotRolling = true;
            shotSpin0 = -T.shotScrew * charge01;   // struck low: backspin, more the harder (see SpinAt)
            shotTravel = 0f;
            shotSpinLive = true;
            rb.angularVelocity = Vector3.Cross(Vector3.up, aimDir) * (speed / radius * shotSpin0);
            Gfx.Puff(rb.position - Vector3.up * radius, new Color(0.88f, 0.88f, 0.88f, 0.8f), 16, radius * 2f, 5f);
            ValleyGame.I.Shake(this, 0.25f + 0.3f * charge01);
            Sfx.Play(Sfx.Sound.Launch, rb.position);
        }

        /// <summary>Snake-style tail: every ball springs toward its slot along the path the main ball rolled.</summary>
        void UpdateTail(float dt)
        {
            Vector3 hp = head.transform.position;
            hp.y = 0f;
            // Main ball backing up along its own path (e.g. recoil after a hit): forget the path it backed over,
            // so the tail gets pushed back behind it instead of ending up in front.
            while (trail.Count >= 2)
            {
                Vector3 last = trail[trail.Count - 1];
                Vector3 seg = last - trail[trail.Count - 2];
                float len = seg.magnitude;
                if (len < 1e-4f || Vector3.Dot(hp - last, seg / len) > -0.1f) break;
                trail.RemoveAt(trail.Count - 1);
            }
            if (trail.Count == 0 || (hp - trail[trail.Count - 1]).sqrMagnitude > 0.09f) trail.Add(hp);
            if (absorbed)
            {
                AbsorbStep(dt);
                return;
            }
            if (babies.Count == 0)
            {
                if (trail.Count > 40) trail.RemoveRange(0, trail.Count - 40);
                return;
            }

            Vector3 headVel = head.rb.linearVelocity;
            headVel.y = 0f;
            Vector3 a = hp;
            Vector3 backDir = -aimDir;
            int j = trail.Count - 1;
            float walked = 0f;
            float need = 0f;
            float prevR = radius;
            for (int i = 0; i < babies.Count; i++)
            {
                var ball = babies[i];
                need += prevR + ball.curR + TailGap;
                prevR = ball.curR;
                Vector3 target;
                while (true)
                {
                    if (j < 0) { target = a + backDir * (need - walked); break; }
                    Vector3 b = trail[j];
                    float seg = Vector3.Distance(a, b);
                    if (seg > 1e-4f && walked + seg >= need)
                    {
                        target = Vector3.Lerp(a, b, (need - walked) / seg);
                        break;
                    }
                    if (seg > 1e-4f) backDir = (b - a) / seg;
                    walked += seg;
                    a = b;
                    j--;
                }
                target.y = ball.curR;
                ApplySpring(ball, target, headVel);
            }
            int keepFrom = Mathf.Max(0, j - 2);
            if (keepFrom > 64) trail.RemoveRange(0, keepFrom);
        }

        void ApplySpring(BallPart b, Vector3 target, Vector3 headVel)
        {
            var rb = b.rb;
            Vector3 p = rb.position;
            float r = b.curR;
            // Tail balls are never lost to caves: they pop back out.
            if (p.y < -1.5f)
            {
                rb.position = target + Vector3.up * 1.5f;
                rb.linearVelocity = headVel;
                b.SetGhost(false);
                return;
            }
            Vector3 to = target - p;
            to.y = 0f;
            float dist = to.magnitude;
            if (!b.ghost && dist > 14f + 2f * r) b.SetGhost(true);
            else if (b.ghost && dist < 2.5f + r && !Physics.CheckSphere(p, r * 0.95f, ValleyGame.QueryMask, QueryTriggerInteraction.Ignore))   // 429R: mask
                b.SetGhost(false);   // only turn solid again once clear of rocks/mushrooms (no popping up on top of them)

            Vector3 v = rb.linearVelocity;
            Vector3 rel = headVel - new Vector3(v.x, 0f, v.z);
            bool free = Time.time < b.freeUntil && !b.ghost;
            float k = free ? T.tailSpring * 0.12f : T.tailSpring;
            float c = free ? 0f : T.tailDamping;
            Vector3 acc = Vector3.ClampMagnitude(to * k + rel * c, T.tailMaxAccel);
            if (b.ghost)
            {
                // Passing through things: glide at table height (no bobbing that could fling it up later).
                rb.linearVelocity = new Vector3(v.x, Mathf.Clamp((r - p.y) * 8f, -4f, 4f), v.z);
            }
            else
            {
                acc += Physics.gravity * T.extraGravity;
                if (v.y > 2.5f) rb.linearVelocity = new Vector3(v.x, 2.5f, v.z);   // tail balls never jump
            }
            rb.AddForce(acc, ForceMode.Acceleration);
        }

        void EatNearby()
        {
            Vector3 p = head.transform.position;
            int n = Physics.OverlapSphereNonAlloc(p, radius + 0.4f, overlap, ValleyGame.QueryMask, QueryTriggerInteraction.Collide);   // 429R: mask
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                if (c.TryGetComponent(out Pellet pellet))
                {
                    if (pellet.edible)
                    {
                        int v = pellet.value;
                        pellet.Eat();
                        Sfx.Play(Sfx.Sound.EatBerry, p, 0.6f);
                        AddFood(v);
                    }
                }
                else if (c.TryGetComponent(out Slime slime))
                {
                    if (slime.CanEat)
                    {
                        int v = slime.FoodValue;
                        bool rainbow = slime.rainbow;
                        slime.GetEaten();
                        Sfx.Play(Sfx.Sound.EatSlime, p);
                        AddFood(v);
                        if (rainbow) ValleyGame.I.CaughtRainbow(this);
                    }
                }
                else if (c.TryGetComponent(out LooseBall lb))
                {
                    if (lb.CanEat(this))
                    {
                        int v = lb.value;
                        lb.Eat();
                        Sfx.Play(Sfx.Sound.EatBerry, p, 0.8f);
                        AddFood(v);
                    }
                }
            }
        }

        public void AddFood(int v)
        {
            if (!alive) return;
            food += v;
            points += v * T.pointsPerFood;
            int guard = 0;
            bool grows = !neverGrows && babies.Count < T.maxBabies;   // 429R: Never Grows, Max Babies
            while (grows && food >= NextCost && guard++ < 50)
            {
                food -= NextCost;
                GainBall();
                grows = babies.Count < T.maxBabies;
            }
            if (!grows) food = Mathf.Min(food, NextCost);   // 429R: no pile of food waiting (it only scored points)
        }

        // ---------- collisions & damage ----------

        public void OnPartCollision(BallPart part, Collision c)
        {
            if (!alive) return;
            var other = c.collider;
            Vector3 cp = c.contactCount > 0 ? c.GetContact(0).point : part.transform.position;
            Vector3 nrm = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.up;
            float impact = Mathf.Abs(Vector3.Dot(c.relativeVelocity, nrm));
            if (!part.isHead && impact > 6f) part.freeUntil = Time.time + 0.35f;   // pinball!

            bool otherBall = other.TryGetComponent(out BallPart op) && op.owner != this && op.owner != null;
            bool looseBall = !otherBall && other.TryGetComponent(out LooseBall lb);
            bool solid = otherBall || looseBall || other.GetComponent<Destructible>() || other.GetComponent<Hazard>();

            // Special move hitting another ball: resolve it like a snooker shot (with the swallowed tail's weight).
            if (part.isHead && Dashing)
            {
                if (otherBall)
                {
                    bool clash = op.isHead && op.owner.Dashing;   // two launches meeting: resolve once
                    if (!clash || part.GetInstanceID() < op.GetInstanceID()) SnookerHit(part, op.rb, op.prevVel, op, null);
                }
                else if (looseBall)
                {
                    var loose = other.GetComponent<LooseBall>();
                    SnookerHit(part, loose.rb, loose.prevVel, null, loose);
                }
            }

            // Main ball carrying its tail hits something solid: the tail bursts back out.
            if (part.isHead && absorbed && !charging && solid)
            {
                Vector3 toward = cp - part.transform.position;
                Vector3 pre = part.prevVel;
                pre.y = 0f;
                Burst(toward, Mathf.Clamp(pre.magnitude * 0.7f, 8f, 20f));
            }

            if (otherBall)
            {
                // Balls never hurt each other: a hit only pushes (and credits the pusher if you end up in a cave).
                var enemy = op.owner;
                NoteHit(enemy);
                // Snooker chain reaction: a knocked ball slamming into me knocks me on too.
                if (part.isHead && op.isHead && enemy.knocked)
                {
                    Vector3 n2 = part.transform.position - op.transform.position;
                    n2.y = 0f;
                    if (n2.sqrMagnitude > 1e-6f && Vector3.Dot(op.prevVel, n2.normalized) > T.knockChainSpeed) Knock();
                }
                if (impact > 5f) Gfx.Sparks(cp, Color.Lerp(enemy.color, Color.white, 0.5f), Mathf.Clamp((int)impact, 3, 20), 7f);
                if (impact > 3f && part.GetInstanceID() < op.GetInstanceID())   // both balls report the hit: sound it once
                    Sfx.Play(Sfx.Sound.BallHit, cp, Mathf.Clamp01(impact / 20f));
                if (part.isHead && impact > 8f) ValleyGame.I.Shake(this, Mathf.Clamp01(impact / 40f));
                return;
            }
            if (other.TryGetComponent(out BallPart own) && own.owner == this) return;

            if (other.TryGetComponent(out Destructible dObj))
            {
                Vector3 toD = dObj.transform.position - part.transform.position;
                toD.y = 0f;
                toD.Normalize();
                float approach = Mathf.Max(Vector3.Dot(part.prevVel, toD), impact * 0.5f);
                float dmg = part.isHead
                    ? Mathf.Max(0f, approach - 2f) * Mathf.Sqrt(Mass) * (Dashing ? 1.6f : 1f)
                    : Mathf.Max(0f, approach - 3f) * 0.35f * Mathf.Sqrt(part.rb.mass / T.babyMass);
                if (dmg > 0f) dObj.Hit(dmg, cp, this);
                return;
            }

            if (other.TryGetComponent(out Hazard hz))
            {
                if (hz.prop)
                {
                    // Trees and boulders take damage too: enough crashes (or one big special move) break them into food.
                    Vector3 toP = hz.prop.transform.position - part.transform.position;
                    toP.y = 0f;
                    toP = toP.sqrMagnitude > 1e-4f ? toP.normalized : -nrm;
                    float approach = Mathf.Max(Vector3.Dot(part.prevVel, toP), impact * 0.5f);
                    float dmg = part.isHead
                        ? Mathf.Max(0f, approach - 3f) * Mathf.Sqrt(Mass / T.baseMass) * (Dashing ? 2f : 1f)
                        : Mathf.Max(0f, approach - 4f) * 0.3f * Mathf.Sqrt(part.rb.mass / T.babyMass);
                    if (dmg > 0f) hz.prop.Hit(dmg, cp, toP, this);
                }
                if (impact > 5f) Gfx.Sparks(cp, new Color(0.8f, 0.78f, 0.72f), Mathf.Clamp((int)impact, 3, 16), 6f);
                if (impact > 4f && (part.isHead || impact > 8f))
                    Sfx.Play(hz.wood ? Sfx.Sound.WoodHit : Sfx.Sound.RockHit, cp, Mathf.Clamp01(impact / 25f) * (part.isHead ? 1f : 0.5f));
                if (part.isHead && !Shielded && !Invincible)
                {
                    // Crashing into walls and boulders is what costs balls: the harder the crash (speed into the
                    // obstacle, weighted by how heavy you are), the more you lose.
                    float force = Mathf.Max(0f, impact - T.hazardSafeSpeed) * Mathf.Pow(Mass / T.baseMass, T.hazardMassWeight);
                    TakeDamage(force * T.hazardDamage * hz.damageScale);
                    if (impact > 8f) ValleyGame.I.Shake(this, Mathf.Clamp01(impact / 40f));
                }
            }
        }

        /// <summary>
        /// Flat, snooker-like collision for the special move. Uses the velocities from before the hit: the struck ball
        /// takes the push along the line between the two centres (PushSpeed: an equal ball hit full gets nearly all your
        /// speed) and carries the shot on; the striker loses that part of its speed and then its spin takes over
        /// (StrikerAfter): close and hard it screws back, at middle distance it stops dead, far or soft it follows
        /// through, and on a cut it peels off along the tangent - exactly where and how hard you hit the other ball
        /// decides where both go.
        /// </summary>
        void SnookerHit(BallPart striker, Rigidbody target, Vector3 u2, BallPart targetPart, LooseBall targetLoose)
        {
            bool targetIsHead = targetPart != null && targetPart.isHead;
            var rb = striker.rb;
            Vector3 u1 = striker.prevVel;
            u1.y = 0f;
            u2.y = 0f;
            // The line of centres as they met. A shot moves most of a metre per physics step, so by now the balls
            // overlap and their centres no longer line up the way they touched (a cut would come out far too thin):
            // work it out from where both were before this step.
            Vector3 p2 = targetPart ? targetPart.prevPos : targetLoose ? targetLoose.prevPos : target.position;
            float r2 = targetPart ? (targetIsHead ? targetPart.owner.radius : targetPart.curR)
                : targetLoose ? targetLoose.transform.localScale.x * 0.5f : 0f;
            Vector3 n = ContactNormal(striker.prevPos, u1, p2, u2, radius + r2, out float touch);
            bool timed = n != Vector3.zero;
            if (!timed)
            {
                n = target.position - rb.position;   // (couldn't tell: as they are now)
                n.y = 0f;
                if (n.sqrMagnitude < 1e-6f) return;
                n.Normalize();
            }
            float w = Vector3.Dot(u1 - u2, n);       // closing speed along the line of centres
            if (w <= 0.5f) return;

            float m1 = rb.mass, m2 = target.mass;

            // Struck ball: takes the push along the line of centres, and carries the shot on for what's left of its burst.
            // (An invincible striker hits harder through its extra weight; an invincible ball hardly budges.)
            Vector3 v2 = u2 + n * PushSpeed(w, m1, m2);
            if (targetIsHead && targetPart.owner.Invincible) v2 = u2 + (v2 - u2) * 0.3f;
            float burstUntil = dashUntil;

            // Striker: its spin decides what it does next (used up by this hit: from here on it just rolls).
            Vector3 v1 = StrikerAfter(u1, n, w, m1, m2, shotSpinLive ? ShotSpin : 1f);
            shotSpinLive = false;
            if (targetIsHead) dashUntil = Mathf.Min(dashUntil, Time.time + 0.15f);
            if (timed)
            {
                // Both back to where they touched, moved on along their new paths for the rest of this step: they no
                // longer overlap, so the physics won't shove them apart along the wrong line next step (bending a cut).
                float rest = Mathf.Max(0f, Time.fixedDeltaTime - touch);
                Vector3 q1 = striker.prevPos + u1 * touch + v1 * rest, q2 = p2 + u2 * touch + v2 * rest;
                rb.position = new Vector3(q1.x, rb.position.y, q1.z);
                target.position = new Vector3(q2.x, target.position.y, q2.z);
            }
            rb.linearVelocity = new Vector3(v1.x, Mathf.Min(rb.linearVelocity.y, 0f), v1.z);
            target.linearVelocity = new Vector3(v2.x, Mathf.Min(target.linearVelocity.y, 0f), v2.z);
            if (targetIsHead) targetPart.owner.Knock(burstUntil);
            else if (targetLoose) targetLoose.Knock(burstUntil);

            Vector3 cp = rb.position + n * radius;
            Gfx.Sparks(cp, Color.white, Mathf.Clamp(Mathf.RoundToInt(w), 6, 24), 9f);
            Sfx.Play(Sfx.Sound.SpecialHit, cp);
            Sfx.Play(Sfx.Sound.BallHit, cp, 0.6f);   // the bell rings under the punch
            float shake = Mathf.Clamp01(w / 30f) + 0.15f;
            ValleyGame.I.Shake(this, shake);
            if (targetIsHead) ValleyGame.I.Shake(targetPart.owner, shake);
        }

        /// <summary>
        /// The flat line of centres (unit, striker to target) at the moment two balls first touched, <paramref name="reach"/>
        /// apart: they were at <paramref name="p1"/> / <paramref name="p2"/> moving at <paramref name="u1"/> /
        /// <paramref name="u2"/> when this physics step began, and touched <paramref name="touch"/> seconds into it.
        /// That can be before the step began (below 0): a fast shot sometimes sinks into the other ball a step before the
        /// hit is reported, and the line of centres then would make the cut far too thin. Zero if their paths can't have
        /// met lately (then the caller goes by where they are now).
        /// </summary>
        static Vector3 ContactNormal(Vector3 p1, Vector3 u1, Vector3 p2, Vector3 u2, float reach, out float touch)
        {
            touch = 0f;
            Vector3 d = p2 - p1, v = u2 - u1;
            d.y = 0f;
            v.y = 0f;
            float a = v.sqrMagnitude, b = 2f * Vector3.Dot(d, v), c = d.sqrMagnitude - reach * reach;
            if (a < 1e-6f) return c <= 0f && d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;   // (barely moving: as they are)
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return Vector3.zero;
            float t = (-b - Mathf.Sqrt(disc)) / (2f * a);   // when they first came within reach
            float dt = Time.fixedDeltaTime;
            if (t > dt * 1.5f || t < -dt * 2.5f) return Vector3.zero;
            Vector3 at = d + v * t;
            if (at.sqrMagnitude < 1e-6f) return Vector3.zero;
            touch = t;
            return at.normalized;
        }

        void TakeDamage(float dmg)
        {
            if (dmg < 0.05f || !alive) return;
            flash = Mathf.Min(1f, flash + dmg / 8f);
            if (babies.Count == 0) { damageAccum = 0f; return; }
            damageAccum += dmg;
            int lose = Mathf.FloorToInt(damageAccum / T.hpPerBall);
            if (lose > 0)
            {
                damageAccum -= lose * T.hpPerBall;
                Shed(lose);
            }
            ValleyGame.I.Shake(this, Mathf.Clamp01(dmg / 20f));
        }

        // 429R: no network (BALLS' NetFlags, ProxySetAlive, ProxySetTail, ProxySetState).

        // ---------- visuals ----------

        AudioSource chargeSound;

        /// <summary>
        /// The special move's charge-up sound plays while this ball charges, wherever it is simulated or shown
        /// (host and clients each start it from the charging state, so it isn't sent over the network).
        /// </summary>
        void UpdateChargeSound()
        {
            if (charging && alive && head)
            {
                if (!chargeSound) chargeSound = Sfx.Begin(Sfx.Sound.Charge, Position);
                else Sfx.Follow(chargeSound, Sfx.Sound.Charge, Position);
            }
            else if (chargeSound)
            {
                Sfx.Stop(chargeSound);
                chargeSound = null;
            }
        }

        void OnDestroy()
        {
            Sfx.Stop(chargeSound);
            if (headMat) Destroy(headMat);   // 429R: its own material (Gfx.NewLit) goes with it
        }

        void Update()
        {
            UpdateChargeSound();
            if (!alive || !head || vrBody)   // 429R: nothing to show on the VR body
            {
                if (auraMade != null && auraMade.activeSelf) auraMade.SetActive(false);   // 429R: down a hole: its aura waits
                return;
            }
            float dt = Time.deltaTime;
            radius = Mathf.MoveTowards(radius, targetRadius, dt * 0.8f);
            head.transform.localScale = Vector3.one * (radius * 2f);
            Vector3 hp = head.transform.position;
            foreach (var b in babies)
            {
                b.curR = Mathf.MoveTowards(b.curR, b.targetR, dt * 0.8f);
                float s = b.curR * 2f;
                if (b.pop < 1f)
                {
                    b.pop = Mathf.Min(1f, b.pop + dt * 3.5f);
                    s *= EaseOutBack(b.pop);
                }
                if (b.state == BallPart.State.Absorbing)   // shrink as it disappears into the main ball
                    s *= Mathf.Clamp01((Vector3.Distance(b.transform.position, hp) - radius * 0.3f) / (radius * 1.5f));
                if (Mathf.Abs(b.transform.localScale.x - s) > 0.001f) b.transform.localScale = Vector3.one * Mathf.Max(0.01f, s);
            }
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
            Color glow = Color.white * (flash * 1.5f);
            if (charging) glow += new Color(1f, 0.45f, 0.1f) * (charge01 * 1.2f);
            else if (absorbed) glow += new Color(1f, 0.6f, 0.2f) * 0.6f;
            if (Shielded) glow += new Color(0.3f, 0.6f, 1f) * (0.35f + 0.25f * Mathf.Sin(Time.time * 12f));
            if (Invincible)
            {
                // The Rainbow Slime's power: the ball glows round the rainbow and throws off rainbow sparkles (every
                // computer draws its own), flickering in its last second.
                float on = InvincibleLeft > 1f || Mathf.Sin(Time.time * 30f) > 0f ? 1f : 0.3f;
                glow += Slime.Rainbow(Time.time * 0.8f) * (0.75f * on);
                if (Time.time >= nextRainbowSparkle)
                {
                    nextRainbowSparkle = Time.time + 0.07f;
                    Vector3 at = hp + Random.insideUnitSphere * (radius * 0.9f);
                    Gfx.Sparks(at, Slime.Rainbow(Time.time * 0.8f + Random.value * 0.5f), 2, 3f, false);
                }
            }
            if (aura.a > 0f)
            {
                // 429R gate 3: a seed ball's glow: a slow pulse of its colour, sparks off its skin now and then.
                glow += new Color(aura.r, aura.g, aura.b) * (0.9f + 0.4f * Mathf.Sin(Time.time * 5f));
                if (Time.time >= nextAuraSpark)
                {
                    nextAuraSpark = Time.time + 0.1f;
                    Gfx.Sparks(hp + Random.onUnitSphere * radius, aura, 1, 1.8f, false);
                }
            }
            if (auraLook != null)
            {
                // 429R gate 3: its aura round it, upright (the ball rolls, its wisps rise), as wide as the ball and a bit
                // (as the pumpkin boss wears its Chaos Aura).
                if (auraMade == null)
                {
                    auraMade = Instantiate(auraLook, transform);
                    auraMade.name = auraLook.name;
                    LouderAura(auraMade);
                }
                if (!auraMade.activeSelf) auraMade.SetActive(true);
                auraMade.transform.SetPositionAndRotation(hp, Quaternion.identity);
                auraMade.transform.localScale = Vector3.one * (radius * 2f * AuraWidth / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x)));
            }
            headMat.SetColor("_EmissionColor", glow);
        }

        StunStars stars;
        bool wasDizzy;

        /// <summary>
        /// Knocked by a special move (no control until the ball rolls out): dizzy stars circle over its head, with a dizzy
        /// twinkle when it starts. Every computer does this itself (the knock comes with the snapshots).
        /// </summary>
        void UpdateStars()
        {
            bool dizzy = alive && head && knocked;
            if (dizzy)
            {
                if (!stars) stars = StunStars.Create(transform);
                stars.Show(head.transform.position, radius);
                if (!wasDizzy) Sfx.PlayHere(Sfx.Sound.Dizzy, head.transform.position);
            }
            else if (stars) stars.Hide();
            wasDizzy = dizzy;
        }

        void LateUpdate()
        {
            UpdateStars();
            if (!alive || !head) return;
            Vector3 p = head.transform.position;
            lastPosition = p;
            if (vrBody || !face || !smoke) return;   // 429R: no face or smoke on the VR body (or a ball dressed its own way)
            Vector3 v = Velocity;
            v.y = 0f;
            Vector3 look = charging ? aimDir : (v.sqrMagnitude > 2f ? v.normalized : aimDir);
            face.position = p;
            face.rotation = Quaternion.Slerp(face.rotation, Quaternion.LookRotation(look, Vector3.up), 1f - Mathf.Exp(-12f * Time.deltaTime));
            face.localScale = Vector3.one * (radius * 2f);

            smoke.transform.position = p - Vector3.up * (radius * 0.85f) - aimDir * (radius * 0.3f);
            smoke.transform.localScale = Vector3.one * radius;
            var main = smoke.main;
            main.startSizeMultiplier = radius * 1.4f;
            SetSmoke((charging && grounded ? 40f + 120f * charge01 : (Dashing ? 60f : 0f)) * T.smokeAmount);
        }

        void SetSmoke(float rate)
        {
            if (!smoke) return;
            var em = smoke.emission;
            em.rateOverTimeMultiplier = rate;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
