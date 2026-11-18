using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Gates;
using Game.Locomotion;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Balls
{
    /// <summary>
    /// 429R gate 3 (user, 2026-11-18): the VR player in Ball Valley, on foot among the balls.
    /// - Body: an invisible ball round the player (Body Radius) that every bot sees and hunts like any ball: they try to
    ///   ram the player into a hole. A ball crashing in throws the player (further the faster and heavier it was; a
    ///   special move most of all) and bounces off; a hard one (Hurt Speed) or a special move hurts (Hurt Event).
    /// - Pushing balls: walking into a ball pushes it along; a punch sends it off (the fist's own push) and knocks it
    ///   (no steering until it has rolled out); the special move blasts everything in its way.
    /// - Special move: both fists made (grip + trigger) and held together for Charge Seconds (a glow grows between them),
    ///   then pulled apart: a dash where the player looks (Dash Speed for Dash Seconds; it stops at a hole's rim), knocking
    ///   every ball on its way and hitting the giant pumpkin hard (Dash Boss Damage). Dash Cooldown between.
    /// - Slimes: a punch pops one into candy (the field's food is candy, see Ball Valley Stage's Candy Prefabs).
    /// - Holes: the head over a pit (past its rim by Pit Margin) is a normal fall (user, 2026-11-18): the view drops and
    ///   blacks out, the player is put back where they stood a moment before (on the lawn, clear of the holes) and it costs
    ///   the fall's health (FallDamage); at 0 health the death goes on as usual.
    /// - Teleport lands only on the lawn (the holes' pits are cut out of it).
    /// On the Ball_Valley prefab next to the stage and its Ball Valley Gate.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BallValleyStage))]
    public class BallValleyPlayer : MonoBehaviour
    {
        [Header("Start")]
        [Tooltip("Where the player starts (on the lawn, world) and which way they face (degrees).")]
        public Vector3 startSpot = new Vector3(0f, 0f, -4f);
        public float startYaw = 0f;
        [Tooltip("Teleporting lands only on the lawn (its holes cut out).")]
        public bool teleportLawn = true;

        [Header("Body")]
        [Tooltip("The player's ball (metres): what the bots hunt and crash into.")]
        [Min(0.2f)] public float bodyRadius = 0.45f;

        [Header("Hit by balls")]
        [Tooltip("A ball closing in slower than this (m/s) only bumps: the player isn't thrown.")]
        [Min(0f)] public float minShoveSpeed = 2.5f;
        [Tooltip("Metres the player is thrown per m/s of the ball's closing speed, times the square root of its weight.")]
        [Min(0f)] public float shovePerSpeed = 0.22f;
        [Min(0f)] public float maxShove = 3.5f;
        [Min(0f)] public float shoveHeight = 0.3f;
        [Tooltip("A ball's special move hurts, and so does a plain crash at least this fast (play test 2026-11-18: hurting " +
                 "from 7 m/s, the bots took 106 health in a minute).")]
        [Min(0f)] public float hurtSpeed = 12f;
        [Tooltip("PlayerHealth FSM event for a hurting hit.")]
        public string hurtEvent = "Damage";
        [Tooltip("The player is hurt at most this often by the balls (seconds).")]
        [Min(0f)] public float hurtEvery = 1.5f;
        [Tooltip("A ball that has thrown the player leaves them alone this long (seconds) before it may hunt them again.")]
        [Min(0f)] public float restAfterHit = 10f;
        [Tooltip("How bouncy a ball comes off the player (0 = dead stop, 1 = full bounce).")]
        [Range(0f, 1f)] public float bounce = 0.35f;
        [Tooltip("One ball throws the player at most this often (seconds).")]
        [Min(0.1f)] public float sameBallEvery = 0.8f;

        [Header("Pushing balls")]
        [Tooltip("Walking into a ball pushes it on at least this much faster than the player walks (m/s).")]
        [Min(0f)] public float walkPush = 1.2f;
        [Tooltip("A punched ball rolls on freely (the special move's slide) for this long before it brakes (seconds).")]
        [Min(0f)] public float punchSlide = 0.35f;

        [Header("Holes")]
        [Tooltip("The point under the head this far past a pit's rim (metres) is a fall.")]
        [Min(0f)] public float pitMargin = 0.25f;
        [Tooltip("A fall (user, 2026-11-18: \"fall into hole = normal fall = minus health, 0 health = death\"): the view " +
                 "drops this far (metres) while it blacks out, as over a Fall Edge...")]
        [Min(0f)] public float fallDrop = 1f;
        [Min(0.1f)] public float fallSeconds = 0.45f;
        [Tooltip("...then the player is back where they stood this many seconds before (on the lawn, clear of the holes) and " +
                 "this goes to their PlayerHealth FSM (FallDamage = 55, as any fall; at 0 health the death goes on as usual).")]
        [Min(0f)] public float secondsBack = 2f;
        public string fallDamageEvent = "FallDamage";
        [Tooltip("Spots this close to a hole's rim (metres) aren't kept as places to put the player back.")]
        [Min(0f)] public float keepFromRim = 1.5f;
        [Tooltip("The falling whistles (Cartoon_fall_1/2, as Fall Out Of World's); one plays as the view drops.")]
        public AudioClip[] fallSounds = new AudioClip[0];
        [Range(0f, 1f)] public float fallVolume = 0.9f;

        [Header("Special move")]
        [Tooltip("Both fists made and held this close together (metres, knuckles) charge it.")]
        [Min(0.05f)] public float fistsTogether = 0.3f;
        [Min(0.1f)] public float chargeSeconds = 1f;
        [Min(1f)] public float dashSpeed = 12f;
        [Min(0.05f)] public float dashSeconds = 0.35f;
        [Min(0f)] public float dashCooldown = 3f;
        [Tooltip("A ball the dash runs into flies off this fast (m/s), knocked.")]
        [Min(1f)] public float dashKnockSpeed = 16f;
        [Tooltip("How far past the body the dash reaches (metres).")]
        [Min(0f)] public float dashReach = 0.8f;
        [Tooltip("Damage to the giant pumpkin (twice when it's dizzy: its Dizzy Damage Times).")]
        [Min(0f)] public float dashBossDamage = 10f;

        /// <summary>The player's ball in the valley (the bots' prey).</summary>
        public BallPlayer Body => vr;
        /// <summary>The special move's charge (0..1) and whether it's ready.</summary>
        public float Charge01 => charge01;
        public bool DashReady => Time.time >= dashReadyAt;

        private BallValleyStage stage;
        private BallValleyGate gate;
        private BallPlayer vr;
        private XROrigin origin;
        private Transform head;
        private CharacterController body;
        private XRBodyTransformer mover;
        private PlayerLaunch launch;
        private PlayMakerFSM healthFsm;
        private HutongGames.PlayMaker.FsmFloat playerHealth;
        private readonly List<Fist> fists = new();
        private readonly Dictionary<Rigidbody, float> nextShoveBy = new();
        private readonly HashSet<Rigidbody> dashHits = new();
        private Vector3 lastCentre;
        private Vector3 bodyVel;
        private float nextHurt;
        private bool falling;
        private bool dashing;
        private float dashReadyAt;
        private float charge01;
        private bool charged;
        private Transform glow;
        private AudioSource chargeSound;
        private bool holding;
        private bool bodyWasOn;
        private bool moverWasOn;
        private bool placed;

        private sealed class StillBrain : IBallBrain
        {
            public BallInput Think(BallPlayer me) => default;
        }

        private void Awake()
        {
            stage = GetComponent<BallValleyStage>();
            gate = GetComponent<BallValleyGate>();
        }

        private void OnEnable() => Fist.Landed += OnPunch;

        private void OnDisable() => Fist.Landed -= OnPunch;

        private void OnDestroy()
        {
            Hold(false);
            if (chargeSound != null) Sfx.Stop(chargeSound);
        }

        private void Start()
        {
            origin = FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogWarning("[BallValley] No XR Origin: no player in the valley.", this);
                enabled = false;
                return;
            }
            head = origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;
            body = origin.GetComponent<CharacterController>();
            mover = origin.GetComponent<XRBodyTransformer>();
            launch = origin.GetComponent<PlayerLaunch>();
            foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "PlayerHealth") continue;
                healthFsm = fsm;
                playerHealth = fsm.FsmVariables.FindFsmFloat("PlayerHealth");
            }
            fists.AddRange(FindObjectsByType<Fist>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            // On the lawn (clear of trees and boulders: play test 2026-11-18 started under a pine), then the body the bots see.
            MovePlayer(ClearSpot(startSpot), startYaw);
            vr = stage.AddExternalBall("You", new Color(1f, 0.85f, 0.3f), new StillBrain(), true);
            vr.vrBody = true;
            vr.fixedRadius = bodyRadius;
            vr.follow = BodyCentre;
            vr.vrContact = OnContact;
            lastCentre = BodyCentre();
            stage.SpawnAt(vr, lastCentre);
            placed = true;

            if (teleportLawn) MakeTeleportLawn();
        }

        private void MakeTeleportLawn()
        {
            int layer = LayerMask.NameToLayer("Teleport");
            if (layer < 0 || stage.valley == null) return;
            var lawn = stage.valley.MakeTeleportFloor(layer);
            if (lawn == null) return;
            var area = lawn.GetComponent<TeleportationArea>();
            if (area == null) area = lawn.gameObject.AddComponent<TeleportationArea>();
            // Bit 31: the scene's Teleport Interactor and every other teleport area use it (as the bowling arena's dirt).
            area.interactionLayers = unchecked((int)0x80000000);
            area.matchOrientation = MatchOrientation.None;
        }

        // ---------------------------------------------------------------- every frame

        private void FixedUpdate()
        {
            if (!placed) return;
            var c = BodyCentre();
            bodyVel = BallValleyStage.Flat(c - lastCentre) / Time.fixedDeltaTime;
            if (bodyVel.sqrMagnitude > 400f) bodyVel = Vector3.zero;   // a teleport or a put-back, not walking
            lastCentre = c;
        }

        private void Update()
        {
            if (!placed || vr == null) return;
            bool down = PlayerDown();
            // Down (the coins, getting up): the bots leave the player alone; up again: the body is back.
            if (down || falling) vr.shieldUntil = Time.time + 0.5f;
            if (!vr.alive && !down && !falling) stage.SpawnAt(vr, BodyCentre());

            KeepStood(down);
            KeepInside(down);
            if (!down && !falling && !GateExit.Running && stage.valley != null)
            {
                var feet = HeadFloor();
                bool flying = launch != null && launch.Flying;
                if (stage.valley.DistanceToNearestPit(feet) < -pitMargin && (!flying || origin.transform.position.y < -0.3f))
                    StartCoroutine(FallIn());
            }
            UpdateSpecial(down);
        }

        // ---------------------------------------------------------------- balls meeting the player

        private void OnContact(Collider other, bool entered)
        {
            if (falling || PlayerDown() || other == null || GateExit.Running) return;   // (on the way out: left alone)
            Rigidbody rb;
            BallPlayer owner = null;
            var part = other.GetComponent<BallPart>();
            if (part != null && part.owner != null && part.owner != vr) { rb = part.rb; owner = part.owner; }
            else if (other.TryGetComponent(out LooseBall loose)) rb = loose.rb;
            else return;
            if (rb == null || rb.isKinematic) return;

            var c = BodyCentre();
            var n = BallValleyStage.Flat(rb.position - c);
            float d = n.magnitude;
            n = d > 0.001f ? n / d : -BallValleyStage.Flat(rb.linearVelocity).normalized;
            if (n.sqrMagnitude < 0.5f) return;
            var v = rb.linearVelocity;
            var flat = BallValleyStage.Flat(v);
            float closing = -Vector3.Dot(flat, n);   // the ball's speed into the player
            float walkIn = Vector3.Dot(bodyVel, n);   // the player's walking into the ball

            bool special = owner != null && part.isHead && owner.Dashing;
            if (entered && !dashing && closing > minShoveSpeed && (!nextShoveBy.TryGetValue(rb, out float next) || Time.time >= next))
            {
                // A ball crashing into the player: thrown off (more the heavier and faster), and it bounces back.
                nextShoveBy[rb] = Time.time + sameBallEvery;
                float weight = Mathf.Sqrt(Mathf.Max(0.25f, rb.mass));
                float throwBy = Mathf.Min(maxShove, closing * shovePerSpeed * weight * (special ? 1.6f : 1f));
                if (throwBy > 0.3f) PlayerLaunch.Launch(c + n, shoveHeight, throwBy, false);
                if ((closing >= hurtSpeed || special) && Time.time >= nextHurt)
                {
                    nextHurt = Time.time + hurtEvery;
                    SendPlayer(hurtEvent);
                }
                // It got the player: it goes off after something else for a while (play test 2026-11-18).
                if (owner != null && owner.brain is BotBrain hunter) hunter.ForgetHuman(restAfterHit);
                flat += n * (closing * (1f + bounce));
                rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
                Sfx.Play(Sfx.Sound.BallHit, c + n * bodyRadius, Mathf.Clamp01(closing / 15f));
                return;
            }
            // Pressed against the player, or walked into: pushed along, a little faster than the player walks.
            float want = Mathf.Max(0f, walkIn) + walkPush;
            float along = Vector3.Dot(flat, n);
            if (along < want && (walkIn > 0.2f || d < bodyRadius + 0.2f))
            {
                flat += n * (want - along);
                rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
                if (owner != null) Credit(owner);
            }
        }

        // ---------------------------------------------------------------- punches

        private void OnPunch(Fist.PunchLanded landed)
        {
            var hit = landed.Hit;
            if (hit == null || stage == null) return;
            var part = hit.GetComponent<BallPart>();
            if (part != null && part.owner != null && part.owner != vr)
            {
                // The fist has sent it off along the punch: knocked, it rolls out without steering, free for a moment.
                if (part.isHead) part.owner.Knock(Time.time + punchSlide);
                else part.freeUntil = Time.time + 0.6f;
                Credit(part.owner);
                Sfx.Play(Sfx.Sound.BallHit, hit.transform.position, 0.8f);
                return;
            }
            if (hit.TryGetComponent(out LooseBall loose))
            {
                loose.Knock(Time.time + punchSlide);
                return;
            }
            if (hit.TryGetComponent(out Slime slime)) PopSlime(slime);
        }

        // A punched (or dashed) slime pops into candy.
        private void PopSlime(Slime slime)
        {
            if (slime == null || !slime.CanEat) return;
            var at = slime.transform.position;
            int candies = slime.rainbow ? 3 : slime.size > 1.4f ? 2 : 1;
            slime.GetEaten();
            Sfx.Play(Sfx.Sound.SlimeHit, at);
            for (int i = 0; i < candies; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var fly = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(2f, 4f) + Vector3.up * Random.Range(5f, 7f);
                stage.SpawnPellet(at + Vector3.up * 0.6f, fly, stage.RandomBerryColor());
            }
        }

        // If this ball goes down a hole soon, the player pushed it (the kill feed).
        private void Credit(BallPlayer owner)
        {
            if (owner == null || vr == null) return;
            owner.lastAttacker = vr;
            owner.lastAttackTime = Time.time;
        }

        // ---------------------------------------------------------------- the special move

        private void UpdateSpecial(bool down)
        {
            Fist a = null, b = null;
            foreach (var f in fists)
            {
                if (f == null || !f.isActiveAndEnabled) continue;
                if (a == null) a = f;
                else if (b == null) b = f;
            }
            bool together = a != null && b != null && a.IsMadeFist && b.IsMadeFist &&
                            Vector3.Distance(a.Knuckles, b.Knuckles) < fistsTogether;
            bool can = !down && !falling && !dashing && DashReady && !(launch != null && launch.Flying) && !GateExit.Running;
            if (together && can)
            {
                bool was = charged;
                charge01 = Mathf.Min(1f, charge01 + Time.deltaTime / chargeSeconds);
                charged = charge01 >= 1f;
                var mid = (a.Knuckles + b.Knuckles) * 0.5f;
                ShowGlow(mid, charge01);
                if (chargeSound == null) chargeSound = Sfx.Begin(Sfx.Sound.Charge, mid);
                else Sfx.Follow(chargeSound, Sfx.Sound.Charge, mid);
                if (charged && !was) Sfx.Play(Sfx.Sound.GainBall, mid);
                return;
            }
            // Let go (pulled apart or opened): a full charge dashes; a part one just fades.
            if (charged && can) StartCoroutine(Dash());
            charge01 = 0f;
            charged = false;
            ShowGlow(Vector3.zero, 0f);
            if (chargeSound != null)
            {
                Sfx.Stop(chargeSound);
                chargeSound = null;
            }
        }

        private void ShowGlow(Vector3 at, float amount)
        {
            if (amount <= 0f)
            {
                if (glow != null) glow.gameObject.SetActive(false);
                return;
            }
            if (glow == null)
            {
                var orange = new Color(1f, 0.55f, 0.15f);
                glow = Gfx.Visual("Charge Glow", Gfx.SmallSphere, Gfx.Lit(orange, 0.6f, orange * 2.5f), transform,
                    Vector3.zero, Vector3.one, Quaternion.identity);
                Gfx.NoShadows(glow);
            }
            glow.gameObject.SetActive(true);
            glow.position = at;
            float pulse = charged ? 1f + 0.12f * Mathf.Sin(Time.time * 20f) : 1f;
            glow.localScale = Vector3.one * (Mathf.Lerp(0.04f, 0.2f, amount) * pulse);
        }

        private IEnumerator Dash()
        {
            dashing = true;
            dashReadyAt = Time.time + dashCooldown;
            var dir = head != null ? BallValleyStage.Flat(head.forward) : Vector3.zero;
            if (dir.sqrMagnitude < 0.0001f) dir = BallValleyStage.Flat(origin.transform.forward);
            dir.Normalize();
            var start = HeadFloor();
            Sfx.Play(Sfx.Sound.Launch, start, 0.8f);
            Gfx.Puff(start + Vector3.up * 0.2f, new Color(0.9f, 0.85f, 0.75f, 0.7f), 12, 1.2f, 4f);
            dashHits.Clear();
            var boss = gate != null ? gate.Boss : null;
            bool bossHit = false;
            for (float t = 0f; t < dashSeconds; t += Time.deltaTime)
            {
                if (PlayerDown() || falling || origin == null) break;
                var step = dir * (dashSpeed * Time.deltaTime);
                if (stage.valley != null && stage.valley.DistanceToNearestPit(HeadFloor() + step) < 0.4f) break;   // stops at a hole's rim
                if (body != null && body.enabled) body.Move(step);
                else origin.transform.position += step;
                var c = BodyCentre();
                foreach (var p in stage.players)
                {
                    if (p == null || p == vr || !p.alive || p.head == null) continue;
                    DashInto(p.head.rb, p.radius, c, dir, p);
                    foreach (var tail in p.babies) if (tail != null && tail.col.enabled) DashInto(tail.rb, tail.curR, c, dir, null);
                }
                foreach (var l in stage.looseBalls) if (l != null) DashInto(l.rb, l.transform.localScale.x * 0.5f, c, dir, null);
                for (int i = stage.slimes.Count - 1; i >= 0; i--)
                {
                    var s = i < stage.slimes.Count ? stage.slimes[i] : null;
                    if (s != null && BallValleyStage.Flat(s.transform.position - c).magnitude < s.Radius + bodyRadius + dashReach) PopSlime(s);
                }
                if (!bossHit && boss != null && !boss.IsBroken && boss.Now != PumpkinBallBoss.State.Dropping &&
                    BallValleyStage.Flat(boss.transform.position - c).magnitude < boss.radius + bodyRadius + 0.3f)
                {
                    bossHit = true;
                    boss.SpecialHit(dashBossDamage);
                    Sfx.Play(Sfx.Sound.SpecialHit, c + dir * bodyRadius);
                    Gfx.Sparks(c + dir * bodyRadius, Color.white, 24, 9f);
                    break;   // it stops the dash
                }
                yield return null;
            }
            dashing = false;
        }

        private void DashInto(Rigidbody rb, float r, Vector3 c, Vector3 dir, BallPlayer owner)
        {
            if (rb == null || rb.isKinematic || dashHits.Contains(rb)) return;
            var to = BallValleyStage.Flat(rb.position - c);
            float d = to.magnitude;
            if (d > r + bodyRadius + dashReach || Vector3.Dot(to, dir) < -0.3f * d) return;
            dashHits.Add(rb);
            var away = d > 0.01f ? to / d : dir;
            var v = (away * 0.35f + dir * 0.65f).normalized * dashKnockSpeed;
            rb.linearVelocity = new Vector3(v.x, Mathf.Min(rb.linearVelocity.y, 0f), v.z);
            if (owner != null)
            {
                owner.Knock(Time.time + 0.4f);
                Credit(owner);
            }
            else if (rb.TryGetComponent(out LooseBall loose)) loose.Knock(Time.time + 0.4f);
            var at = rb.position - away * r;
            Sfx.Play(Sfx.Sound.SpecialHit, at, 0.8f);
            Gfx.Sparks(at, Color.white, 14, 8f);
        }

        // ---------------------------------------------------------------- holes

        // Where they stood lately (the floor under the head, and when): on the lawn, clear of the holes.
        private readonly List<(Vector3 spot, float time)> stood = new();
        private float nextStood;
        private int lastFallSound = -1;
        private AudioSource fallVoice;

        // The border moving in as the valley shrinks sweeps the player in with it (play test 2026-11-18: it passed over a
        // player standing at the edge and left them outside the field). Not in a hole's gap: walking that way is theirs.
        private void KeepInside(bool down)
        {
            var v = stage.valley;
            if (v == null || down || falling || GateExit.Running || (launch != null && launch.Flying)) return;
            var feet = HeadFloor();
            float lx = Mathf.Max(0.5f, v.HalfX - 0.5f), lz = Mathf.Max(0.5f, v.HalfZ - 0.5f);
            if (Mathf.Abs(feet.x) <= lx + 0.05f && Mathf.Abs(feet.z) <= lz + 0.05f) return;
            var nearest = v.NearestCave(feet);
            if (nearest != null && BallValleyStage.Flat(feet - nearest.mouth).magnitude < nearest.width * 0.75f) return;
            var inside = new Vector3(Mathf.Clamp(feet.x, -lx, lx), feet.y, Mathf.Clamp(feet.z, -lz, lz));
            bool bodyOn = body != null && body.enabled;
            if (bodyOn) body.enabled = false;
            origin.transform.position += new Vector3(inside.x - feet.x, 0f, inside.z - feet.z);
            Physics.SyncTransforms();
            if (bodyOn) body.enabled = true;
        }

        // Keeps a spot every 0.2 s while they stand on the lawn, clear of the holes, not flying.
        private void KeepStood(bool down)
        {
            if (Time.time < nextStood || down || falling || stage.valley == null) return;
            nextStood = Time.time + 0.2f;
            if (launch != null && launch.Flying) return;
            var feet = HeadFloor();
            var v = stage.valley;
            if (Mathf.Abs(feet.x) > v.HalfX - 0.5f || Mathf.Abs(feet.z) > v.HalfZ - 0.5f) return;
            if (v.DistanceToNearestPit(feet) < keepFromRim) return;
            stood.Add((feet, Time.time));
            while (stood.Count > 0 && stood[0].time < Time.time - 10f) stood.RemoveAt(0);
        }

        // Over a pit: a normal fall (user, 2026-11-18): the view drops and blacks out, they're put back where they stood a
        // moment before, and it costs the fall's health (FallDamage); at 0 health the death goes on as usual.
        private IEnumerator FallIn()
        {
            falling = true;
            if (launch != null) launch.Stop();
            Hold(true);
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(fallSeconds);
            PlayFallSound();
            var from = origin.transform.position;
            for (float t = 0f; t < fallSeconds; t += Time.deltaTime)
            {
                float k = t / fallSeconds;
                origin.transform.position = from + Vector3.down * (fallDrop * k * k);
                yield return null;
            }
            stage.AddFeed("You fell into a hole!", new Color(1f, 0.5f, 0.4f));
            var back = SpotBeforeFall();
            stood.Clear();
            Hold(false);
            MovePlayer(back, HeadYaw());
            if (fade != null) fade.FadeIn(0.6f);
            SendPlayer(fallDamageEvent);
            yield return new WaitForSeconds(0.6f);
            falling = false;
        }

        // The newest kept spot from at least Seconds Back before the fall that's still on the lawn and clear of the holes
        // (the valley may have shrunk since); else the middle of the lawn, clear of the giant pumpkin.
        private Vector3 SpotBeforeFall()
        {
            var v = stage.valley;
            float before = Time.time - secondsBack;
            for (int i = stood.Count - 1; i >= 0; i--)
            {
                var s = stood[i].spot;
                if (stood[i].time > before || v == null) continue;
                if (Mathf.Abs(s.x) > v.HalfX - 0.5f || Mathf.Abs(s.z) > v.HalfZ - 0.5f) continue;
                if (v.DistanceToNearestPit(s) < keepFromRim) continue;
                if (NearBoss(s)) continue;
                return s;
            }
            return SafeSpot();
        }

        private bool NearBoss(Vector3 at)
        {
            var boss = gate != null ? gate.Boss : null;
            return boss != null && !boss.IsBroken && BallValleyStage.Flat(boss.transform.position - at).magnitude < boss.radius + 1.5f;
        }

        // Spot, or the nearest place round it with room to stand (no tree, boulder or ball there), on the lawn.
        private Vector3 ClearSpot(Vector3 spot)
        {
            for (float r = 0f; r <= 8f; r += 1f)
            {
                int steps = r == 0f ? 1 : 10;
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    var p = spot + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (stage.valley != null && stage.valley.DistanceToNearestPit(p) < 3f) continue;
                    if (Blocked(p)) continue;
                    return p;
                }
            }
            return spot;
        }

        private static readonly Collider[] blockers = new Collider[32];

        // A tree, a boulder or a ball where the player would stand (the field's candies don't count: they hover there).
        private static bool Blocked(Vector3 p)
        {
            int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 1f, p + Vector3.up * 4f, 0.8f, blockers, BallValleyStage.QueryMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = blockers[i];
                if (c == null || c.GetComponentInParent<Pellet>() != null) continue;
                return true;
            }
            return false;
        }

        // The middle of the lawn (the holes are round its edge), or near it if the boss is there.
        private Vector3 SafeSpot()
        {
            var boss = gate != null ? gate.Boss : null;
            var spot = Vector3.zero;
            if (boss != null && !boss.IsBroken && BallValleyStage.Flat(boss.transform.position).magnitude < boss.radius + 2f)
            {
                var off = BallValleyStage.Flat(-boss.transform.position);
                spot = (off.sqrMagnitude > 0.01f ? off.normalized : Vector3.right) * (boss.radius + 2f);
            }
            return spot;
        }

        // The falling whistle, in the player's ears (never the same twice in a row).
        private void PlayFallSound()
        {
            if (fallSounds == null || fallSounds.Length == 0)
            {
                Sfx.Play(Sfx.Sound.Pocket, HeadFloor());
                return;
            }
            int pick = Random.Range(0, fallSounds.Length);
            if (pick == lastFallSound && fallSounds.Length > 1) pick = (pick + 1) % fallSounds.Length;
            lastFallSound = pick;
            var clip = fallSounds[pick];
            if (clip == null) return;
            if (fallVoice == null)
            {
                fallVoice = gameObject.AddComponent<AudioSource>();
                fallVoice.playOnAwake = false;
                fallVoice.spatialBlend = 0f;
            }
            fallVoice.pitch = Random.Range(0.95f, 1.08f);
            fallVoice.PlayOneShot(clip, fallVolume);
        }

        private void SendPlayer(string ev)
        {
            if (healthFsm != null && !string.IsNullOrEmpty(ev)) healthFsm.SendEvent(ev);
        }

        // ---------------------------------------------------------------- the player

        /// <summary>The middle of the player's ball: over the floor under the head.</summary>
        private Vector3 BodyCentre() => HeadFloor() + Vector3.up * bodyRadius;

        private Vector3 HeadFloor()
        {
            float y = origin != null ? origin.transform.position.y : 0f;
            return head != null ? new Vector3(head.position.x, y, head.position.z) : new Vector3(0f, y, 0f);
        }

        private float HeadYaw()
        {
            if (head == null) return 0f;
            var f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            return f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f).eulerAngles.y : 0f;
        }

        // The head over Spot, the feet on its floor, facing Yaw.
        private void MovePlayer(Vector3 spot, float yaw)
        {
            if (origin == null) return;
            bool bodyOn = body != null && body.enabled;
            if (bodyOn) body.enabled = false;
            if (launch != null) launch.Stop();
            origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(HeadYaw(), yaw));
            var feet = HeadFloor();
            var to = origin.transform.position + new Vector3(spot.x - feet.x, 0f, spot.z - feet.z);
            to.y = spot.y;
            origin.transform.position = to;
            Physics.SyncTransforms();
            if (bodyOn) body.enabled = true;
        }

        // The body and its mover off while the rig is moved by hand; back on as they were.
        private void Hold(bool on)
        {
            if (on == holding) return;
            holding = on;
            if (on)
            {
                bodyWasOn = body != null && body.enabled;
                moverWasOn = mover != null && mover.enabled;
                if (bodyWasOn) body.enabled = false;
                if (moverWasOn) mover.enabled = false;
                return;
            }
            if (bodyWasOn && body != null) body.enabled = true;
            if (moverWasOn && mover != null) mover.enabled = true;
            Physics.SyncTransforms();
        }

        private bool PlayerDown()
        {
            return Game.Saving.Checkpoints.IsDying ||
                   (PlayerRevive.Instance != null && PlayerRevive.Instance.IsReviving) ||
                   (playerHealth != null && playerHealth.Value <= 0f);
        }
    }
}
