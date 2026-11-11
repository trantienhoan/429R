using System.Collections.Generic;
using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// The Pumpkin boss (pumpkin_boss_Z). It laughs non-stop from the moment it appears, and breaks the room it comes
    /// up in (the bedroom or the living room). It starts as just its head sitting on the floor; every hit the player
    /// lands raises it one pumpkin out of the ground, and after the third one its arms and body come out, it slams
    /// both arms down and then fights; hurt a lot very fast, it stands there dizzy for a few seconds. Left alone it
    /// wakes itself up once, after Wake Up Seconds. Below True Form Below of its health it climbs out on two legs,
    /// grows and runs faster. How it fights (both forms):
    /// - the player too close for its punches (Too Close): it spins its cyclone to push them away (then it's dizzy);
    /// - in reach: one of three punches, left, right or both arms;
    /// - out of reach after a few punches (Punches Before Throw), or far away (further than it runs in Far Seconds,
    ///   after a clap): a combo of 1-5 pumpkins full of bugs, left hand then right. A hit slows the player down and it
    ///   chases them; dodging them all makes it vomit bugs to back it up, or clap for the player (Vomit After Dodge);
    /// - true form: the jump with a body slam is its main move (see True Form): never more than a couple of punches in
    ///   a row, and it leaps at a player a little out of its reach. For its upper-body moves (punches, the two-arm slam,
    ///   the clap, the cyclone) it drops back into the floor to its upper body's depth first, so it doesn't hang in the
    ///   air with its legs folded up; it climbs back out to run, throw, vomit or jump.
    /// Its pumpkin butt (user, 2026-11-11: "no more walking, only sliding for now", Slide On Pumpkin): the true form has a
    /// giant pumpkin for a butt instead of legs. It slides on it with momentum (slow to speed up and to stop, it overshoots
    /// a player who steps aside), leaning into the slide, with a dust trail and a grinding sound; its jump is a butt bounce
    /// (the whole boss hops and lands butt first, same timing as the old leg jump); and it has a slide charge at a player
    /// out of its reach (wind-up, then straight at where they stood, a big throw if it touches them, dizzy if it bonks into
    /// a wall). Its pumpkin is kept out of walls. Hits on the pumpkin hurt it a fifth (its Hit Zone) but crack it (Butt
    /// Break Share of its health in full hits); cracked through, it bursts, the boss drops into the floor dizzy for a long
    /// time, and fights from the floor like its upper body for the rest of the fight. Off: the old two-legged true form.
    /// A far player gets hopped at: its pumpkin squashes and springs it along in hops (Butt Hops). While it gets about it
    /// holds one pose (slide_pose: arms up, roaring), standing it idles; and what its pumpkin runs into or lands on is
    /// crushed (Crushing).
    /// Every dizzy spell (too much damage, its own cyclone, a wall bonk) goes through one Stun Guard: none within Stun Guard
    /// Seconds of the last one, and each one soon after the last is shorter (see Try Stun).
    /// Hits never stop it. Its hits throw the player (Player Launch, Push Power) and only hurt when they really land.
    /// Every health it loses drops a pumpkin to throw back at it. One mouth, one sound: a yelp when it's hurt, a roar,
    /// or munching candy (Candy Vacuum) cuts its laugh short, and the laugh comes back right after.
    /// When it dies it climbs out of the floor and screams at the sky with its whole body showing, shaking harder and
    /// harder; then every pumpkin of its body bursts into a tree pumpkin's break (Pumpkin Break), the fists first and
    /// the hips last, its shards flying everywhere, and it sets GAMESTAGES' BOSS flag.
    /// Hits on it come from Enemy Hit Damage, which lowers the "health" of its (switched off) Health FSM.
    /// Every one that appears may be a chaos boss (Chaos Chance): more health, harder blows, more EXP and a red aura.
    /// Tools > 429 Game > Enemies > Set Up Pumpkin Boss builds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class PumpkinBossBrain : MonoBehaviour
    {
        private enum Phase { Head, Rising, Upper, Transforming, True, Dying }

        /// <summary>One pumpkin of its body, for bursting it when the boss dies.</summary>
        [System.Serializable]
        public struct BodyPumpkin
        {
            public string bone;
            [Tooltip("Its middle, in the bone's own space.")]
            public Vector3 offset;
            [Tooltip("How big it is, in model units (the boss at size 1).")]
            [Min(0.01f)]
            public float size;
            [Tooltip("Seconds after the first pumpkin bursts.")]
            [Min(0f)]
            public float at;

            public BodyPumpkin(string bone, Vector3 offset, float size, float at)
            {
                this.bone = bone;
                this.offset = offset;
                this.size = size;
                this.at = at;
            }
        }

        // JumpSlam is the leg jump, or with Slide On Pumpkin the butt bounce; Charge the slide charge; Stagger a short reel
        // without stars (a wall bonk while its Stun Guard is up).
        private enum Act { None, Taunt, PunchL, PunchR, Slam, Throw, JumpSlam, Vomit, Cyclone, Dizzy, Clap, Charge, Stagger }

        // Where a dizzy spell comes from (Try Stun): too much damage too fast, its own cyclone, a wall bonk, or the pumpkin
        // butt breaking.
        private enum Stun { Damage, Cyclone, Bonk, ButtBreak }

        // The slide charge's steps: winding up, sliding, braking after it, skidding at the floor's edge, catching its breath.
        private enum ChargeStep { WindUp, Slide, Brake, Skid, Recover }

        [Header("Parts (set by the setup menu)")]
        [SerializeField] private Animator animator;
        [Tooltip("The child that holds the model: moved up and down for how far the boss is out of the ground, and scaled.")]
        [SerializeField] private Transform model;
        [SerializeField] private Transform head;
        [SerializeField] private Transform jaw;
        [SerializeField] private Transform handL;
        [SerializeField] private Transform handR;

        [Header("Health")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("It goes true form below this share of its health.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float trueFormBelow = 0.4f;

        [Header("Chaos boss (user, 2026-11-09)")]
        [Tooltip("The chance it comes out as a chaos boss, rolled as it appears: every Pumpkin Boss (the rooftop's, a runner " +
                 "pumpkin's, a laughing pumpkin's; user, 2026-11-10). 0 = never.")]
        [Range(0f, 1f)]
        [SerializeField] private float chaosChance = 0.33f;
        [Tooltip("A chaos boss has this many times the health (729 -> 1094)...")]
        [Min(1f)]
        [SerializeField] private float chaosHealth = 1.5f;
        [Tooltip("...its blows do this many times the damage (before the player's Toughness)...")]
        [Min(1f)]
        [SerializeField] private float chaosDamage = 1.3f;
        [Tooltip("...and it gives this many times the EXP.")]
        [Min(1f)]
        [SerializeField] private float chaosExp = 2f;
        [Tooltip("The red aura round a chaos boss's head pumpkin (Prefabs/Effects/Chaos_Aura; Tools > 429 Game > Balance " +
                 "2026-11-10 > Enemies sets it). It grows with the boss. Empty: no aura.")]
        [SerializeField] private GameObject chaosAura;
        [Tooltip("How wide the aura is, in shares of the head pumpkin's width.")]
        [Min(0.1f)]
        [SerializeField] private float chaosAuraWidth = 1.15f;

        [Header("Waking up")]
        [Tooltip("Hits it takes as a head before its arms and body come out.")]
        [Min(1)]
        [SerializeField] private int revealHits = 3;
        [Tooltip("With no hit for this long as a head, it wakes itself up (once).")]
        [Min(1f)]
        [SerializeField] private float wakeUpSeconds = 33f;
        [Tooltip("How far the model sits under the floor (model units, before its scale): head only, then one more " +
                 "pumpkin out per hit; the last one is the upper body, with the vines under its bottom pumpkin kept " +
                 "under the floor.")]
        [SerializeField] private float[] buried = { 1.72f, 1.41f, 1.22f, 1.06f };

        [Header("Size and speed")]
        [SerializeField] private float headScale = 1.7f;
        [SerializeField] private float upperScale = 1.7f;
        [SerializeField] private float trueScale = 2.1f;
        [Tooltip("Running speed in true form, in m/s. The upper body moves at Upper Speed of it.")]
        [Min(0.1f)]
        [SerializeField] private float trueSpeed = 5f;
        [Range(0.1f, 1f)]
        [SerializeField] private float upperSpeed = 0.6f;

        [Header("Getting dizzy (upper body)")]
        [Tooltip("It's stunned once it loses this share of its health in quick succession (0.06 of 729 = about 44 " +
                 "health; was 0.0294, raised 2026-11-05 so punch spam gets its answer, see Punch Spam, before it reels). " +
                 "What it has lost fades away by Dizzy Fade Per Second, so slow hits never add up.")]
        [Range(0.02f, 1f)]
        [SerializeField] private float dizzyAfterDamage = 0.06f;
        [Range(0f, 0.5f)]
        [SerializeField] private float dizzyFadePerSecond = 0.0029f;
        [Tooltip("How long it stands there dizzy (being_hit), in seconds.")]
        [Min(0.5f)]
        [SerializeField] private float dizzySeconds = 3f;
        [Tooltip("The dizzy stars (Hit Settings' Stun Stars) circle this far above its head bone, in metres (x), on a " +
                 "ring this wide from the middle (y): just over the top of the big pumpkin head.")]
        [SerializeField] private Vector2 dizzyStars = new(1f, 0.5f);

        [Header("Stun guard (user, 2026-11-11: \"don't let the player stun it forever\")")]
        [Tooltip("After any dizzy spell ends (too much damage, its own cyclone, a wall bonk, its pumpkin butt breaking) it " +
                 "can't be made dizzy again for this many seconds: a stun meanwhile is shaken off (after its cyclone it " +
                 "roars and fights on, a wall bonk only staggers it, damage doesn't add up towards one). Was Dizzy " +
                 "Cooldown, which only held back the damage dizzy; 17 since 2026-11-11, user).")]
        [FormerlySerializedAs("dizzyCooldown")]
        [Min(0f)] [SerializeField] private float stunGuardSeconds = 17f;
        [Tooltip("A dizzy spell that starts within this many seconds of the last one's end is shorter...")]
        [Min(0f)] [SerializeField] private float stunRepeatWindow = 45f;
        [Tooltip("...this share of its usual length for each spell in a row (0.6: 3 s, then 1.8 s, then 1.08 s)...")]
        [Range(0.1f, 1f)] [SerializeField] private float stunRepeatShare = 0.6f;
        [Tooltip("...but never shorter than this, in seconds.")]
        [Min(0.2f)] [SerializeField] private float stunShortest = 1.5f;
        [Tooltip("A wall bonk while the guard is up: it only reels this long (no stars), then roars and fights on.")]
        [Min(0.1f)] [SerializeField] private float staggerSeconds = 0.5f;

        [Header("Punch spam (user, 2026-11-05: \"when he gets spammed by Player's punches, he should start spinning or body slamming Player\")")]
        [Tooltip("Hit this many times within Spam Seconds, it answers at once: the upper body spins its cyclone, the true " +
                 "form spins or leaps into its body slam (Spam Slam Chance). It cuts short a punch, clap, throw or vomit, " +
                 "never a body slam, a cyclone or a dizzy spell (it answers as soon as that's over if the hits kept coming).")]
        [Min(2)] [SerializeField] private int spamHits = 4;
        [Min(0.2f)] [SerializeField] private float spamSeconds = 2f;
        [Tooltip("True form: the chance its answer is the body slam instead of the cyclone.")]
        [Range(0f, 1f)] [SerializeField] private float spamSlamChance = 0.5f;
        [Tooltip("After its answer is over, it doesn't answer again for this many seconds.")]
        [Min(0f)] [SerializeField] private float spamCooldown = 6f;
        [Tooltip("Hits landing within this many seconds of the last one count as one (a punch and its own blast with " +
                 "Explosive Fist, user's play test 2026-11-11: it answered at the second punch).")]
        [Min(0f)] [SerializeField] private float spamMergeSeconds = 0.15f;

        [Header("Hitting the player")]
        [Tooltip("Sent to the player's PlayerHealth FSM when an upper-body hit or the true form's body slam lands " +
                 "(95-122 damage, before the player's Toughness) / any other true-form hit (149-189). Raised 35% on " +
                 "2026-11-05; the numbers live in the PlayerHealth states Subtract Health Boss (Heavy) in 2 Game Scene.")]
        [SerializeField] private string upperHitEvent = "PumpkinBossHit";
        [SerializeField] private string trueHitEvent = "PumpkinBossHitHeavy";
        [SerializeField] private string playerHealthFsm = "PlayerHealth";
        [Tooltip("Throws: x = how high, y = how far, in metres (before Push Power).")]
        [SerializeField] private Vector2 slamLaunch = new(3.2f, 2.5f);
        [SerializeField] private Vector2 punchLaunch = new(1.0f, 3.0f);
        [SerializeField] private Vector2 jumpSlamLaunch = new(3.6f, 3.0f);
        [Tooltip("The cyclone pushes a player who came too close away: it throws them highest and furthest of all.")]
        [SerializeField] private Vector2 cycloneLaunch = new(4.5f, 7f);
        [Tooltip("How much further its punches, slams and body slam throw the player: times the distances above (the " +
                 "heights stay, so the arc is as high but flies further and faster). 1 (or 0) = as set above. 7.5 on " +
                 "pumpkin_boss_Z since 2026-11-10 (user: x2.5, was 3; the cyclone's 1.5 -> 3.75).")]
        [Min(0f)] [SerializeField] private float pushPower = 1f;
        [Tooltip("The same for its cyclone (its throw is already the furthest). 1 (or 0) = as set above.")]
        [Min(0f)] [SerializeField] private float cyclonePushPower = 1f;
        [Tooltip("The player's Toughness (level-up points and candy) cuts its throws, but never below this share: 0.6 = a " +
                 "fully tough player still flies at least 60% as high and far. (Toughness alone would cut them to a tenth.)")]
        [Range(0f, 1f)] [SerializeField] private float minPushShare = 0.6f;
        [Tooltip("How long the whole cyclone lasts, in seconds: the length of its clip (wind-up, spin, hard stop).")]
        [SerializeField] private float cycloneSeconds = 3f;
        [Tooltip("After a cyclone it can't spin another for this many seconds (nor in the first half of that after it " +
                 "turns into its true form); too close meanwhile, it punches.")]
        [Min(0f)]
        [SerializeField] private float cycloneCooldown = 8f;
        [Tooltip("After its cyclone it's spun itself dizzy (dizzy_true) for this many seconds: the player's chance " +
                 "to hit back. 0 = never dizzy.")]
        [Min(0f)]
        [SerializeField] private float cycloneDizzySeconds = 3f;

        [Header("Fighting")]
        [Tooltip("Closer than this (times its size) its punches can't land: it spins its cyclone to push the player away.")]
        [Min(0f)] [SerializeField] private float tooClose = 1.1f;
        [Tooltip("Further away than it can run in this many seconds, it stands clapping; still that far when the clap " +
                 "is nearly over, it throws pumpkins.")]
        [Min(0.5f)] [SerializeField] private float farSeconds = 2f;
        [Tooltip("Pumpkins it throws one after another, left hand then right: a random number from x to y.")]
        [SerializeField] private Vector2Int throwCombo = new(1, 5);
        [Tooltip("Bugs in each pumpkin of a throw combo.")]
        [Min(0)] [SerializeField] private int bugsPerThrownPumpkin = 1;
        [Tooltip("Seconds from one pumpkin leaving its hand to the next throw starting.")]
        [Min(0.05f)] [SerializeField] private float throwGap = 0.3f;
        [Tooltip("How far ahead of a moving player it throws: 0 = at where they stand, 1 = where they'll be if they keep going. " +
                 "They dodge it by changing direction.")]
        [Range(0f, 1f)] [SerializeField] private float throwLead = 0.85f;
        [Tooltip("It never aims further ahead of them than this, in metres.")]
        [Min(0f)] [SerializeField] private float throwLeadMost = 4f;
        [Tooltip("The Animator state for a left-hand throw. Without such a state its left punch throws instead.")]
        [SerializeField] private string leftThrowState = "throw_L";
        [Tooltip("After this many punches, a player who got out of its reach gets pumpkins thrown at them.")]
        [Min(1)] [SerializeField] private int punchesBeforeThrow = 2;
        [Tooltip("True form: after this many punches in a row miss, it jumps and body-slams (once its body slam is ready, " +
                 "see True Form).")]
        [Min(1)] [SerializeField] private int missesBeforeBodySlam = 3;
        [Tooltip("How much a punching arm tilts to point at the player's chest (0 = the clip as it is, 1 = straight at " +
                 "them). The punch clips were made for the upper body, so in the true form, standing on its legs, they " +
                 "fly over the player's head without it. Aimed while it winds up; once the fist flies the player can " +
                 "still step aside.")]
        [Range(0f, 1f)] [SerializeField] private float punchAim = 1f;
        [Tooltip("After a throw combo it chases the player at least this long before it claps and throws again.")]
        [Min(0f)] [SerializeField] private float chaseSeconds = 4f;
        [Tooltip("The player dodged every pumpkin: the chance it vomits bugs to back it up; otherwise it claps for them.")]
        [Range(0f, 1f)] [SerializeField] private float vomitAfterDodge = 0.5f;

        [Header("True form: body slam")]
        [Tooltip("After each move, the chance that its next one is the jump with a body slam (once it's ready and the " +
                 "player is in range). 0 = only after Punches Before Body Slam or missed punches.")]
        [Range(0f, 1f)] [SerializeField] private float bodySlamChance = 0.6f;
        [Tooltip("Seconds from one body slam ending until it can do the next.")]
        [Min(0f)] [SerializeField] private float bodySlamCooldown = 3f;
        [Tooltip("It never throws more punches than this in a row: then it body-slams (once ready). 0 = no limit.")]
        [Min(0)] [SerializeField] private int punchesBeforeBodySlam = 2;
        [Tooltip("It leaps at a player up to this far away (times its size: 4.5 = about 9.5 m in its true form), also " +
                 "to close the gap. 0 = any distance.")]
        [Min(0f)] [SerializeField] private float bodySlamRange = 4.5f;

        [Header("True form: sliding on its pumpkin (user, 2026-11-11: \"no more walking, only sliding for now\")")]
        [Tooltip("On: the true form has a giant pumpkin for a butt (the remodel): it slides on it (the clips below, momentum, " +
                 "leaning, a dust trail, a grinding sound), its jump is the butt bounce and it has the slide charge. Off: " +
                 "the old true form, running on two legs (run / idle_true, the leg jump, no charge), exactly as before.")]
        [SerializeField] private bool slideOnPumpkin = true;
        [Tooltip("The Animator state it holds while it gets about (sliding, hopping, the charge): slide_pose, one still pose " +
                 "the user picked (2026-11-11): the transform clip at 1.40 s, arms up in a V and roaring, with its normal " +
                 "face (idle_true's). Pumpkin Boss Slide Setup makes the clip and the state. Missing: move.")]
        [SerializeField] private string slideMoveState = "slide_pose";
        [Tooltip("The Animator state it plays standing still: its idle (the pumpkin stays planted, Hold Hips Still).")]
        [SerializeField] private string slideStandState = "idle_true";
        [Tooltip("Seconds its clips blend into each other between getting about and standing (slide pose <-> idle).")]
        [Min(0f)] [SerializeField] private float slideBlend = 0.25f;
        [Tooltip("How fast it speeds up and slows down, in m/s each second (it runs at True Speed): low, so it takes a while " +
                 "to get going and to stop, and a player who steps aside makes it slide past. Its legs had 12.")]
        [Min(0.5f)] [SerializeField] private float slideAcceleration = 4f;
        [Tooltip("How fast it turns to where it's sliding, in degrees a second (its body; where it goes lags behind).")]
        [Min(10f)] [SerializeField] private float slideTurnSpeed = 200f;
        [Tooltip("It leans forward this many degrees at full speed (more when it charges)...")]
        [Range(0f, 20f)] [SerializeField] private float slideLeanForward = 5f;
        [Tooltip("...and into its turns and stops: this many degrees towards where it's being pushed (speeding up, braking, " +
                 "turning) at Slide Acceleration...")]
        [Range(0f, 20f)] [SerializeField] private float slideLeanTurn = 6f;
        [Tooltip("...never more than this, in degrees...")]
        [Range(0f, 30f)] [SerializeField] private float slideLeanMost = 12f;
        [Tooltip("...eased in and out over about this many seconds...")]
        [Min(0.01f)] [SerializeField] private float slideLeanSmooth = 0.15f;
        [Tooltip("...plus a wobble of up to this many degrees, the faster it goes the more.")]
        [Range(0f, 5f)] [SerializeField] private float slideWobble = 1.2f;
        [Tooltip("A little dust behind its base while it slides (Pumpkin Boss Fx's Scrape): one puff every this many seconds " +
                 "at full speed (less often slower, none standing). Light on purpose.")]
        [Min(0.05f)] [SerializeField] private float slideDustEvery = 0.35f;
        [Tooltip("How big each puff is, times the scrape dust's usual size for the boss.")]
        [Min(0.1f)] [SerializeField] private float slideDustSize = 0.6f;
        [Tooltip("The grinding sound it makes sliding, looped; louder and higher the faster it goes, quiet when it stands, is " +
                 "dizzy, in the air or dying (Tools > 429 Game > Enemies > Pumpkin Boss Slide Setup sets giant_ball_rolling).")]
        [SerializeField] private AudioClip slideGrind;
        [Range(0f, 1f)] [SerializeField] private float slideGrindVolume = 0.45f;
        [Tooltip("Its pitch slow (x) and at a charge's top speed (y).")]
        [SerializeField] private Vector2 slideGrindPitch = new(0.85f, 1.25f);
        [Tooltip("Seconds of silence at the end of the grinding clip, skipped when it loops (giant_ball_rolling ends in 0.26 s).")]
        [Min(0f)] [SerializeField] private float slideGrindTail = 0.3f;
        [Tooltip("The giant pumpkin's radius, in model units (the boss at size 1; measured 0.78 on the remodel). While the " +
                 "pumpkin is out (true form, not sunk, not broken) it keeps it from hanging far over the floor's edge (its " +
                 "NavMesh only keeps its middle Spider's 0.5 m in), sets the NavMesh Agent's radius for the other enemies to " +
                 "keep clear, keeps pumpkin drops and candy reach outside it, and its charge skids to a stop when the " +
                 "pumpkin's edge nears the floor's. 0 = no pumpkin.")]
        [Min(0f)] [SerializeField] private float pumpkinRadius = 0.78f;
        [Tooltip("True form: for its upper-body moves (punches, the two-arm slam, the clap, the cyclone, reeling) it sinks to " +
                 "its upper body's depth first and climbs out again for the rest, as before. Turn it off once the remodel's " +
                 "upper-body clips stand right on the pumpkin (with the old legs it would hang in the air).")]
        [SerializeField] private bool sinkForUpperMoves = true;
        [Tooltip("With Slide On Pumpkin: after the Animator, the Hips bone (the pumpkin is skinned to it alone) goes back to its " +
                 "rest pose in the model's space, so clips that swing, dip or turn the hips (move, throw and attack turn them " +
                 "up to 28 degrees; run, throw, transform dip them up to 0.33 model units) leave the pumpkin upright and " +
                 "planted. What the clip did to the hips is passed on to the Waist, so the upper body still twists and leans " +
                 "as animated. Every phase (the pumpkin is under the floor before the true form anyway).")]
        [SerializeField] private bool holdHipsStill = true;
        [SerializeField] private string hipsBone = "Hips";
        [SerializeField] private string waistBone = "Waist";
        [Tooltip("The Hips' rest pose in the model's space, from Pumpkin_Boss_Z.fbx itself (the Slide Setup menu writes it: the " +
                 "prefab's own bones may be from an older export). Not set: the prefab's Hips as it is when the boss appears.")]
        [SerializeField] private bool hipsRestSet;
        [SerializeField] private Vector3 hipsRestPosition;
        [SerializeField] private Quaternion hipsRestRotation = Quaternion.identity;

        [Header("True form: butt bounce (Slide On Pumpkin; replaces the leg jump, same choice and timing)")]
        [Tooltip("The Animator state it plays during the bounce (the two-arm slam: its arms come down as it lands).")]
        [SerializeField] private string bounceState = "attack";
        [Tooltip("Seconds into that clip it starts, so the clip's slam (1.58 s into attack) comes as it lands (1.32 s).")]
        [Min(0f)] [SerializeField] private float bounceClipFrom = 0.28f;
        [Tooltip("How high the whole boss hops, times its size (1.1 = about 2.5 m in its true form).")]
        [Min(0f)] [SerializeField] private float bounceHeight = 1.1f;
        [Tooltip("How flat it squashes before take-off and on landing (0.3 = 30% lower, and wider).")]
        [Range(0f, 0.6f)] [SerializeField] private float bounceSquash = 0.3f;
        [Tooltip("How much it stretches in the air (0.2 = 20% taller), most leaving and nearing the ground.")]
        [Range(0f, 0.6f)] [SerializeField] private float bounceStretch = 0.2f;

        [Header("True form: slide charge (Slide On Pumpkin)")]
        [Tooltip("After each move, the chance its next one is the slide charge, once it's ready: the player out of its " +
                 "punches' reach but within Charge Range, the cooldown over (a body slam that's due comes first). 0 = never.")]
        [Range(0f, 1f)] [SerializeField] private float chargeChance = 0.5f;
        [Tooltip("Seconds from one charge ending until the next may start.")]
        [Min(0f)] [SerializeField] private float chargeCooldown = 7f;
        [Tooltip("It charges at a player up to this far away, times its size (6 = about 14 m in its true form).")]
        [Min(0f)] [SerializeField] private float chargeRange = 6f;
        [Tooltip("The wind-up, in seconds: it stops, faces the player, rocks back, grinds louder and roars. Then its way is " +
                 "fixed on where the player stands at that moment (no aiming ahead): stepping aside dodges it.")]
        [Min(0.2f)] [SerializeField] private float chargeWindUp = 0.9f;
        [Tooltip("How fast it slides, in m/s. It can't turn.")]
        [Min(1f)] [SerializeField] private float chargeSpeed = 9f;
        [Tooltip("It goes on until it has passed the player by this much, times its size...")]
        [Min(0f)] [SerializeField] private float chargePastPlayer = 1.6f;
        [Tooltip("...or slid this far in metres, or this many seconds; then it brakes (Charge Brake).")]
        [Min(1f)] [SerializeField] private float chargeMaxDistance = 20f;
        [Min(0.2f)] [SerializeField] private float chargeMaxSeconds = 2.6f;
        [Tooltip("How fast it slows down after the charge, in m/s each second.")]
        [Min(0.5f)] [SerializeField] private float chargeBrake = 8f;
        [Tooltip("The floor ends ahead (there are no walls in its fights: user, 2026-11-11): it skids to a stop with dust, " +
                 "this hard (m/s each second), its pumpkin's edge about at the floor's. No dizzy.")]
        [Min(1f)] [SerializeField] private float chargeSkid = 18f;
        [Tooltip("Every charge ends with it catching its breath this long, in seconds (wobbling, in Charge Recover State): a " +
                 "window to hit it, not a stun (no stars, its Stun Guard doesn't count it).")]
        [Min(0f)] [SerializeField] private float chargeRecovery = 1f;
        [SerializeField] private string chargeRecoverState = "being_hit";
        [Tooltip("It hits a player this close to its middle, times its size (at least its pumpkin's radius while that's out).")]
        [Min(0.1f)] [SerializeField] private float chargeHitRadius = 0.55f;
        [Tooltip("Its throw: x = how high, y = how far, in metres (before Push Power), once per charge.")]
        [SerializeField] private Vector2 chargeLaunch = new(3.2f, 3.4f);
        [Tooltip("On: its hit is a true-form heavy one (Pumpkin Boss Hit Heavy), like its other true-form blows. Off: like " +
                 "its body slam (Pumpkin Boss Hit).")]
        [SerializeField] private bool chargeHitsHeavy = true;
        [Tooltip("How far it rocks back while it winds up, in degrees.")]
        [Range(0f, 20f)] [SerializeField] private float chargeCrouch = 8f;
        [Tooltip("On: sliding into a solid prop (something you can see that doesn't move, standing in its way at chest " +
                 "height: the PlayGround's cauldron, a tree, the giant skull) at Bonk Min Speed or faster, it bonks: a hit " +
                 "blast, a shake and dizzy for Bonk Dizzy Seconds, or only a stagger within its Stun Guard. The floor's edge " +
                 "never bonks it (it skids).")]
        [SerializeField] private bool bonkOnProps = true;
        [Min(0f)] [SerializeField] private float bonkMinSpeed = 3f;
        [Min(0.5f)] [SerializeField] private float bonkDizzySeconds = 2.5f;

        [Header("Pumpkin butt (user, 2026-11-11: crack it to bring it down)")]
        [Tooltip("The collider on the giant pumpkin (the Slide Setup menu makes one from the mesh). The player's hits on it " +
                 "(full damage, before its Hit Zone's fifth) crack it, true form only. Empty: no cracking.")]
        [SerializeField] private Collider buttCollider;
        [Tooltip("The model's material slot that is the giant pumpkin (its own material, last in the list, is best: the cracks " +
                 "then cost one more draw of that part only). -1 = no cracks drawn and nothing hidden when it breaks.")]
        [SerializeField] private int buttMaterialSlot = -1;
        [Tooltip("The crack overlay (Materials/Boss/Boss_Butt_Cracks, shader 429/Boss/Crack Overlay), drawn over the pumpkin " +
                 "and grown with the damage. It also hides the pumpkin once it breaks.")]
        [SerializeField] private Material buttCrackMaterial;
        [Tooltip("The share of its health (full hits, before the Hit Zone) the player must land on the pumpkin to break it " +
                 "(0.2 of 729 = 146; user, 2026-11-11).")]
        [Range(0.01f, 1f)] [SerializeField] private float buttBreakShare = 0.2f;
        [Tooltip("Broken, it drops into the floor dizzy this long (the one stun its Stun Guard doesn't stop), then fights from " +
                 "the floor for the rest of the fight.")]
        [Min(0.5f)] [SerializeField] private float breakDizzySeconds = 5f;

        [Header("True form: butt hops (user, 2026-11-11: \"when the player is too far from the boss, the boss can jump by scaling its butt to move around\")")]
        [Tooltip("With its pumpkin whole and out of the floor, a player further away than this (times its size: 5.2 = twice " +
                 "its punches' reach, about 12 m in its true form) is hopped at instead of slid at: hop after hop while they " +
                 "stay that far; closer it slides. Its moves (charge, bounce, throws, claps) come between hops as usual. 0 = " +
                 "it never hops.")]
        [Min(0f)] [SerializeField] private float hopFrom = 5.2f;
        [Tooltip("How far each hop goes, times its size (2.3 = about 5.3 m), never past the player. With the timing below it " +
                 "gets about 5.3 m each second, as fast as sliding at True Speed, so staying far doesn't shake it off.")]
        [Min(0.1f)] [SerializeField] private float hopLength = 2.3f;
        [Tooltip("How high each hop goes, times its size (0.75 = about 1.7 m in its true form).")]
        [Min(0f)] [SerializeField] private float hopHeight = 0.75f;
        [Tooltip("Seconds its pumpkin squashes down before each hop...")]
        [Min(0.01f)] [SerializeField] private float hopCrouch = 0.18f;
        [Tooltip("...in the air...")]
        [Min(0.1f)] [SerializeField] private float hopAir = 0.6f;
        [Tooltip("...and landing, springing back, before the next.")]
        [Min(0f)] [SerializeField] private float hopRest = 0.22f;
        [Tooltip("Its pumpkin's squash before take-off and on landing (0.75 = 25% lower and wider), and its stretch in the air " +
                 "(1.2 = 20% taller). Only the pumpkin: the upper body keeps its size.")]
        [Range(0.4f, 1f)] [SerializeField] private float hopSquash = 0.75f;
        [Range(1f, 1.6f)] [SerializeField] private float hopStretch = 1.2f;
        [Tooltip("Landing on or next to the player (within its pumpkin's radius and this many metres) hurts, once per hop. " +
                 "This event goes to their PlayerHealth FSM (DamageHeavy: 40-50 before Toughness, what it does this far out; " +
                 "Pumpkin Boss Hit is its slam's)...")]
        [Min(0f)] [SerializeField] private float hopHitReach = 2f;
        [SerializeField] private string hopHitEvent = "DamageHeavy";
        [Tooltip("...times this much right next to its pumpkin or under it, less the further out, x1 at Hop Hit Reach (user, " +
                 "2026-11-11: the closer the player, the harder; x2.5 the most: DamageHeavy does 100-125)...")]
        [Range(1f, 3f)] [SerializeField] private float hopNearShare = 2.5f;
        [Tooltip("...and throws them: x = how high, y = how far, in metres (before Push Power).")]
        [SerializeField] private Vector2 hopLaunch = new(1.2f, 1.0f);
        [Tooltip("Sliding at the player but getting nowhere this many seconds (pushing into a gap too narrow for its pumpkin, " +
                 "like the rooftop's gates), it hops instead: over the low wall or through the gap if the arc clears, else " +
                 "along its path.")]
        [Min(0.1f)] [SerializeField] private float stuckHopAfter = 0.5f;

        [Header("Crushing (user, 2026-11-11: \"crush stuffs on hit path\")")]
        [Tooltip("With its pumpkin whole and out of the floor: what the pumpkin runs into sliding or charging (Crush Min Speed " +
                 "or faster) or lands on (a hop, the bounce) is crushed. Breakables and furniture break their own way (as " +
                 "from a big knock: shards, sounds, what's inside), small and normal bugs are squashed flat. Never candy (it " +
                 "eats it), a Crush Keep Names thing, anything in the player's hands, the player, a boss or giant, anything " +
                 "floating or held still (kinematic), the heaviest blocks (Push Weight). Loose things that can't break are " +
                 "pushed aside (Bulldozer). Not the player's doing: no EXP.")]
        [SerializeField] private bool crushOnPath = true;
        [Min(0f)] [SerializeField] private float crushMinSpeed = 1f;
        [Tooltip("How far past its pumpkin's edge it crushes, in metres.")]
        [Min(0f)] [SerializeField] private float crushReach = 0.25f;
        [Tooltip("Off: the pumpkins it drops when it's hurt (the player's to throw back at it) are only pushed. Crushed, they " +
                 "would spill their candy for it to eat.")]
        [SerializeField] private bool crushOwnDrops = false;
        [Tooltip("Never crushed: anything with one of these in its name (any case). Seed and Coin are what Fall Edge keeps; " +
                 "Chest keeps the reward chests whole.")]
        [SerializeField] private string[] crushKeepNames = { "Seed", "Coin", "Chest" };

        [Header("Bugs")]
        [Tooltip("Small bugs it vomits and that come out of its thrown pumpkin; one at random each time.")]
        [SerializeField] private GameObject[] bugs = System.Array.Empty<GameObject>();
        [Tooltip("Bugs per vomit (when the player dodged all its pumpkins).")]
        [Min(0)] [SerializeField] private int vomitBugs = 6;
        [Tooltip("As it turns into its true form it calls its bugs: a vomit of this many, right away (if the game isn't " +
                 "already full of enemies). 0 = none.")]
        [Min(0)] [SerializeField] private int trueFormVomitBugs = 6;
        [Tooltip("After each retch its bugs come out of the black smoke this many seconds later, once the smoke is down.")]
        [Min(0f)] [SerializeField] private float vomitBugDelay = 0.5f;
        [Tooltip("The pumpkin full of bugs it throws (Bug Pumpkin).")]
        [SerializeField] private GameObject bugPumpkin;

        [Header("Pumpkins to throw back")]
        [Tooltip("Dropped for every health it loses.")]
        [SerializeField] private GameObject pumpkinDrop;
        [Min(1)] [SerializeField] private int maxDropsAround = 10;

        [Header("Smoke and dust")]
        [Tooltip("Its blasts where it slams the ground or a blow lands, and its black vomit smoke (Pumpkin Boss Fx; " +
                 "Tools > 429 Game > Enemies > Build Pumpkin Boss Smoke FX sets it up).")]
        [SerializeField] private PumpkinBossFx fx;
        [Tooltip("The little puff when it's hit as a head, gets dizzy, or bursts at the end.")]
        [SerializeField] private GameObject smoke;

        [Header("Dying")]
        [Tooltip("What each pumpkin of its body bursts into when it dies: a tree pumpkin's break (pumpkin_2_Drop: its " +
                 "shards, its crunch and its candies). Empty: its head bursts into Burst Into instead, the old way.")]
        [SerializeField] private GameObject pumpkinBreak;
        [Tooltip("Its body's pumpkins: the bone each sits on, its middle in that bone's space, its size (model units) and " +
                 "when it bursts (seconds after the first): the fists first, the hips last.")]
        [SerializeField] private BodyPumpkin[] bodyPumpkins = DefaultBodyPumpkins();
        [Tooltip("How fast the shards fly off, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float shardSpeed = 7f;
        [Tooltip("How hard it shakes in its death pose before it bursts (metres at full size).")]
        [Min(0f)]
        [SerializeField] private float deathTremble = 0.05f;
        [Tooltip("What its big head bursts into (the pieces of a broken pumpkin), when there's no Pumpkin Break.")]
        [SerializeField] private GameObject burstInto;
        [SerializeField] private string gameStagesBool = "BOSS";
        [SerializeField] private string gameStagesEvent = "Win";

        [Header("Breaking the room")]
        [Tooltip("GAMESTAGES' global variables holding the rooms. When it appears it breaks whichever is standing: the " +
                 "walls fall away and the room is removed (there's ground under them).")]
        [SerializeField] private string[] roomVariables = { "Bedroom_Base", "Livingroom_Base" };
        [Tooltip("Seconds from the walls starting to fall until the room is removed (the fall-away clips last 3.3 s).")]
        [Min(0f)]
        [SerializeField] private float roomGoneAfter = 3.5f;

        [Header("Sounds")]
        [Tooltip("It laughs non-stop until it dies: one of these after another, never the same twice in a row.")]
        [SerializeField] private AudioClip[] laughs = System.Array.Empty<AudioClip>();
        [Tooltip("Seconds of quiet between two laughs.")]
        [Min(0f)]
        [SerializeField] private float laughGap = 0.1f;
        [Range(0f, 1f)]
        [SerializeField] private float laughVolume = 1f;
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip hurtSound;
        [SerializeField] private AudioClip roarSound;
        [Tooltip("Its scream as it dies (one of these, at random): arms up, shaking, until its pumpkins burst (about 2.5 s). " +
                 "Empty: its hurt yelp.")]
        [SerializeField] private AudioClip[] deathScreams = System.Array.Empty<AudioClip>();

        // Animator states and their timing (seconds): when the hit lands, when the move is over.
        private const string Laugh = "laugh", GetHit = "gethit", Come = "come", Move = "move", Slam = "attack",
            Clap = "clap", BeingHit = "being_hit", PunchRState = "punch_R", PunchLState = "punch_L",
            TransformState = "transform", Run = "run", IdleTrue = "idle_true", ThrowState = "throw", JumpSlamState = "jump_slam",
            VomitState = "vomit", CycloneState = "cyclone", DizzyTrue = "dizzy_true", Death = "death";

        // When the vomit clip retches (seconds in: frames 38, 55, 76 and 84 at 60 fps); the bugs come up then.
        private static readonly float[] VomitRetches = { 0.62f, 0.90f, 1.25f, 1.38f };

        // The cyclone clip winds up until 0.5 s, spins seven times until 2.5 s, then stops dead.
        private const float CycloneSpinFrom = 0.5f, CycloneSpinTo = 2.5f;

        private readonly List<GameObject> drops = new();
        // When the player's recent hits landed, and when it may answer punch spam again.
        private readonly List<float> recentHits = new();
        private float nextSpamAnswer;
        private readonly Dictionary<Transform, Vector3> fistTracked = new();
        private readonly List<PlayMakerFSM> fsmBuffer = new();
        private NavMeshAgent agent;
        private AudioSource speaker;
        private AudioSource laughSpeaker;
        private int lastLaugh = -1;
        private float nextLaugh;
        private bool actImpact;
        private FsmFloat health;
        private float maxHealth;
        private float lastHealth;
        private Phase phase = Phase.Head;
        private Act act = Act.None;
        private float actStarted;
        private float actUntil;
        private bool actHit;
        private float nextAct;
        private float nextFlinch;
        private float daze;
        // Stun Guard: no dizzy spell before this time; when the last one ended (-1: none yet); spells in a row so far.
        private float stunGuardUntil;
        private float lastStunEnded = -1f;
        private int stunChain;
        private int hitsTaken;
        private float lastHitAt;
        private bool wokeItself;
        private float phaseUntil;
        // Dying: the bones of its body's pumpkins, which have burst, and when the bursting started (-1: not yet).
        private Transform[] pumpkinBones;
        private bool[] pumpkinBurst;
        private float burstingFrom = -1f;
        private Vector3 modelHome;
        private float targetBuried;
        private float targetScale;
        private Vector3 jumpFrom, jumpTo;
        private float nextCyclone;
        private int bugsLeft;
        private int retchesDone;
        // A throw combo: throws still to come, the hand of the next one, the one under way, the pumpkins in the air.
        private int throwsLeft;
        private bool throwLeftNext;
        private Transform throwHand;
        private float throwRelease;
        // How the player is moving on the ground, to throw ahead of them.
        private Vector3 playerMove;
        private Vector3 playerWasAt;
        private bool playerSeen;
        private int pumpkinsOut;
        private bool comboHit;
        private bool waitingForPumpkins;
        private float pumpkinsWaitUntil;
        // Punches since its last throws, punches missed in a row, and how long it keeps chasing whatever happens.
        private int spreePunches;
        private int spreeMisses;
        private float chaseUntil;
        // The clap is the far-away one (still far when it's nearly over: it throws).
        private bool clapToThrow;
        // When its last move ended: chasing a player it can't reach for long, it throws.
        private float idleSince;
        // Sinking into (or climbing out of) the ground fast: the true form dropping to its upper body's depth and back.
        private bool quickBury;
        // True form: when its next body slam may come, whether its next move should be one, punches since the last.
        private float nextBodySlam;
        private bool bodySlamNext;
        private int punchesSinceBodySlam;
        // Moments of the move under way that already had their blast (Once); cleared when a move starts.
        private int marks;
        private const int MarkTakeOff = 1, MarkFistR = 2, MarkFistL = 4, MarkHardStop = 8, MarkEmerge = 16;
        private float risingStarted, transformStarted, nextScrape;
        // Vomited bugs on their way out of the smoke: when each comes out, and where (the middle of the black cloud).
        private readonly List<float> bugsDue = new();
        private Vector3 vomitSpot;
        private const float VomitAhead = 2.5f;
        // The upper arms (tilted to aim a punch) and the tilt of the punch under way, in degrees down.
        private Transform upperArmL, upperArmR;
        private float aimPitch;
        // The fist flies out at this many seconds into a punch clip; it's out until about Punch Hit Until.
        private const float PunchRelease = 0.76f, PunchHitUntil = 1.4f;
        // Faster than this, in metres a second, the player teleported. Their movement is smoothed over this many seconds.
        private const float TeleportSpeed = 15f;
        private const float PlayerMoveSmooth = 0.2f;
        private string playing = "";
        private XROrigin player;
        // A chaos boss's aura (on its head bone), stopped as it dies.
        private GameObject auraMade;

        // The model's pose, kept here and never read back from its transform (the lean, hop and squash change that): its
        // size, how deep it's buried (model units), how high it's hopping (metres), its squash (1 = none, under 1 flatter,
        // over 1 taller) and its lean; and where the model rests under the boss.
        private float scaleNow = 1f, buriedNow, hopLift, squash = 1f;
        private float leanPitch, leanRoll, leanJitter;
        private Quaternion lean = Quaternion.identity;
        private Vector3 modelRestPosition;
        private Quaternion modelRestRotation = Quaternion.identity;
        // Sliding: how it really moves (smoothed, flat), how that changes, when the next puff of dust is due, its grinding.
        private Vector3 slideWasAt, slideVelocity, slideAccel;
        private bool slideMeasured;
        private float nextSlideDust;
        private AudioSource grindSpeaker;
        private float grindVolume;
        // Its legs' NavMesh Agent values (acceleration, radius) and the baked NavMesh's radius for its agent type.
        private float legAcceleration = 12f, agentRadiusAsBuilt = 0.6f, bakedRadius = 0.5f;
        // The Animator states it slides and stands in (Slide Move / Slide Stand if the controller has them, else move).
        private string slideMove = Move, slideStand = Move;
        // Hips held still: the bones, and their rest pose in the model's space.
        private Transform hips, waist;
        private Vector3 hipsRestAt;
        private Quaternion hipsRestTurn = Quaternion.identity;
        // The slide charge: its step, its fixed way, where it set off and when, how fast it goes now; when the next may
        // start; whether the dice said its next move is one.
        private ChargeStep chargeStep;
        private Vector3 chargeDir, chargeFrom;
        private float chargeSlideStarted, chargeSpeedNow, nextCharge, recoverStarted;
        private bool chargeNext;
        private static readonly RaycastHit[] propHits = new RaycastHit[8];
        // The pumpkin butt: the full damage it has taken, whether it broke (the boss fights from the floor then), its
        // renderer, the crack overlay and the material hiding it (runtime copies), and a second renderer drawing the cracks
        // when the pumpkin's slot isn't the last one.
        private float buttDamage;
        private bool buttBroken;
        private SkinnedMeshRenderer buttSkin;
        private Material crackMade, hiddenMade, buttOwnMaterial;
        private SkinnedMeshRenderer crackCopy;
        private int crackSlot = -1;
        private bool buttLookReady, buttLookFailed, buttShownNow = true, buttShowSet;
        // The pumpkin hides once it's this share of its upper body's depth into the floor.
        private const float ButtHideShare = 0.6f;
        // Butt hops: the step, when the hop started, where from and to, whether its landing has hit the player yet.
        private enum HopStep { None, Crouch, Air, Land }
        private HopStep hopStep;
        private float hopStarted;
        private Vector3 hopFromAt, hopToAt;
        private bool hopHit;
        private NavMeshPath hopPath;
        private float stuckSince = -1f, nextHopTry;
        private static readonly RaycastHit[] arcHits = new RaycastHit[16];
        // The butt's own squash (Squash Butt): the Hips' and the Waist's scales at rest, and whether they're changed now.
        private Vector3 hipsRestScale = Vector3.one, waistRestScale = Vector3.one;
        private bool buttScaled;
        // Crushing: what each collider met is (found out once), and when each thing was last crushed.
        private enum Crushable { No, Bug, Breakable, HealthFsm }
        private readonly Dictionary<Collider, Crushable> crushKinds = new();
        private readonly Dictionary<int, float> crushedAt = new();
        private static readonly Collider[] crushHits = new Collider[64];
        private const float CrushDamage = 100000f;
        private CandyVacuum vacuum;
        private static readonly int CrackAmountId = Shader.PropertyToID("_CrackAmount");
        private static readonly int CollapseId = Shader.PropertyToID("_Collapse");

        /// <summary>Whether this one came out as a chaos boss (Chaos Chance).</summary>
        public bool IsChaos { get; private set; }

        /// <summary>For tests: the next Pumpkin Boss to appear is a chaos boss, whatever the roll.</summary>
        public static bool ForceNextChaos;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ForceNextChaos = false;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            speaker = GetComponent<AudioSource>();
            if (fx == null) fx = GetComponent<PumpkinBossFx>();
            vacuum = GetComponent<CandyVacuum>();
            // Its laughing has its own speaker, so yelps and roars play over it instead of cutting it off.
            if (speaker != null)
            {
                laughSpeaker = gameObject.AddComponent<AudioSource>();
                laughSpeaker.playOnAwake = false;
                laughSpeaker.outputAudioMixerGroup = speaker.outputAudioMixerGroup;
                laughSpeaker.spatialBlend = speaker.spatialBlend;
                laughSpeaker.rolloffMode = speaker.rolloffMode;
                laughSpeaker.minDistance = speaker.minDistance;
                laughSpeaker.maxDistance = speaker.maxDistance;
                laughSpeaker.priority = speaker.priority;
                laughSpeaker.dopplerLevel = speaker.dopplerLevel;
                laughSpeaker.spread = speaker.spread;
            }
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
            }
            foreach (var bone in GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "UpperArm.L") upperArmL = bone;
                else if (bone.name == "UpperArm.R") upperArmR = bone;
                else if (bone.name == hipsBone && hips == null) hips = bone;
                else if (bone.name == waistBone && waist == null) waist = bone;
            }
            if (model != null)
            {
                modelRestPosition = model.localPosition;
                modelRestRotation = model.localRotation;
                scaleNow = model.localScale.x;
                // The hips' rest pose in the model's space: from the FBX (the menu), else as the prefab has them now,
                // before the Animator has posed anything.
                if (hips != null)
                {
                    hipsRestAt = hipsRestSet ? hipsRestPosition : model.InverseTransformPoint(hips.position);
                    hipsRestTurn = hipsRestSet ? hipsRestRotation : Quaternion.Inverse(model.rotation) * hips.rotation;
                    hipsRestScale = hips.localScale;
                }
                if (waist != null) waistRestScale = waist.localScale;
            }
            if (agent != null)
            {
                legAcceleration = agent.acceleration;
                agentRadiusAsBuilt = agent.radius;
                bakedRadius = NavMesh.GetSettingsByID(agent.agentTypeID).agentRadius;
            }
            // Its grinding has its own looping speaker, set up like its voice.
            if (speaker != null && slideGrind != null)
            {
                grindSpeaker = gameObject.AddComponent<AudioSource>();
                grindSpeaker.playOnAwake = false;
                grindSpeaker.loop = true;
                grindSpeaker.clip = slideGrind;
                grindSpeaker.volume = 0f;
                grindSpeaker.outputAudioMixerGroup = speaker.outputAudioMixerGroup;
                grindSpeaker.spatialBlend = speaker.spatialBlend;
                grindSpeaker.rolloffMode = speaker.rolloffMode;
                grindSpeaker.minDistance = speaker.minDistance;
                grindSpeaker.maxDistance = speaker.maxDistance;
                grindSpeaker.priority = speaker.priority;
                grindSpeaker.dopplerLevel = 0f;
                grindSpeaker.spread = speaker.spread;
            }
            // In Awake, so every way it appears gets the roll (the rooftop's GAMESTAGES, a runner pumpkin's or a laughing
            // pumpkin's Create BOSS) with no FSM change, and before Start reads its health as its max.
            RollChaos();
        }

        // ---------- chaos boss ----------

        // One in three or so (user, 2026-11-09): more health, harder blows, more EXP, and a red aura round its head.
        private void RollChaos()
        {
            IsChaos = ForceNextChaos || (chaosChance > 0f && Random.value < chaosChance);
            ForceNextChaos = false;
            if (!IsChaos) return;
            if (health != null) health.Value = Mathf.Round(health.Value * chaosHealth);
            if (TryGetComponent(out EnemyHitDamage hits)) hits.Exp = Mathf.RoundToInt(hits.Exp * chaosExp);
            name += " (Chaos)";
            MakeChaosAura();
        }

        // The aura on the head bone at the head pumpkin's middle, as wide as Chaos Aura Width of it. It's made for a
        // pumpkin 1 m across and scales with what it's on, so it grows with the model from the little head to the true form.
        private void MakeChaosAura()
        {
            if (chaosAura == null || head == null) return;
            var offset = Vector3.zero;
            float across = 0.6f;
            if (bodyPumpkins != null)
            {
                foreach (var part in bodyPumpkins)
                {
                    if (part.bone != head.name) continue;
                    offset = part.offset;
                    across = part.size;
                    break;
                }
            }
            auraMade = Instantiate(chaosAura, head);
            auraMade.name = "Chaos_Aura";
            var t = auraMade.transform;
            t.localPosition = offset;
            // Upright, whichever way the bone points, so its wisps rise.
            t.rotation = transform.rotation;
            // The head pumpkin is Across model units wide (the model at size 1); the bone's own scale is the rest.
            float modelScale = model != null ? Mathf.Abs(model.lossyScale.x) : Mathf.Abs(transform.lossyScale.x);
            float boneScale = Mathf.Abs(head.lossyScale.x) / Mathf.Max(0.0001f, modelScale);
            t.localScale = Vector3.one * (across * chaosAuraWidth / Mathf.Max(0.0001f, boneScale));
        }

        // Its aura fades out as it dies (no new wisps; the head pumpkin bursts a moment later).
        private void StopChaosAura()
        {
            if (auraMade == null) return;
            foreach (var system in auraMade.GetComponentsInChildren<ParticleSystem>())
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // A punch's arm tilts to point at the player's chest (after the Animator has posed it this frame). Dying, the
        // pumpkins that have burst are gone from its body. The hips are held still first (Hold Hips Still).
        private void LateUpdate()
        {
            if (phase == Phase.Dying) HideBurstPumpkins();
            HoldHips();
            SquashButt();
            if (punchAim <= 0f || player == null || (act != Act.PunchL && act != Act.PunchR)) return;
            var arm = act == Act.PunchL ? upperArmL : upperArmR;
            if (arm == null) return;
            float t = Time.time - actStarted;
            if (t < PunchRelease) aimPitch = PitchTo(arm);
            // In while it winds up, out as the arm comes back.
            float w = punchAim * Mathf.Clamp01(Mathf.Min((t - 0.35f) / 0.3f, (1.6f - t) / 0.3f));
            if (w > 0f) arm.rotation = Quaternion.AngleAxis(aimPitch * w, transform.right) * arm.rotation;
        }

        // The pumpkin stays upright and planted whatever the clip does with the hips (Hold Hips Still): the Hips go back to
        // their rest pose in the model's space (cancelling the Root bone's moves too), and the Waist keeps the turn the
        // clip gave it through the hips, so the upper body still twists and leans as animated. Only with the pumpkin (Slide
        // On Pumpkin): the old two-legged boss needs its hips to move (its leg jump lifts them 1.5 model units).
        private void HoldHips()
        {
            if (!holdHipsStill || !slideOnPumpkin || hips == null || model == null) return;
            var waistTurn = waist != null ? waist.rotation : Quaternion.identity;
            hips.SetPositionAndRotation(model.TransformPoint(hipsRestAt), model.rotation * hipsRestTurn);
            if (waist != null) waist.rotation = waistTurn;
        }

        // The pumpkin butt squashes and stretches on its own (its hops and its bounce; user, 2026-11-11: "jump by scaling
        // its butt"): the Hips bone (the pumpkin is skinned to it alone) is scaled wide and short or narrow and tall, about
        // the pumpkin's base on the floor (the hips come down with the squash), and the Waist the other way, so the upper
        // body rides on the pumpkin's top at its own size. With the hips upright (Hold Hips Still) the only error is a
        // slight shear where the waist leans: a 0.75 squash with the waist leaning 10 degrees sets its axes 8% off (20
        // degrees: 16%), a 1.2 stretch 5% (10%).
        private void SquashButt()
        {
            if (hips == null || model == null) return;
            bool squashed = Mathf.Abs(squash - 1f) > 0.001f;
            if (!squashed && !buttScaled) return;
            buttScaled = squashed;
            float tall = squashed ? squash : 1f, wide = 1f / Mathf.Sqrt(tall);
            hips.localScale = Vector3.Scale(hipsRestScale, new Vector3(wide, tall, wide));
            if (waist != null) waist.localScale = Vector3.Scale(waistRestScale, new Vector3(1f / wide, 1f / tall, 1f / wide));
            if (!squashed) return;
            var at = model.InverseTransformPoint(hips.position);
            at.y *= tall;
            hips.position = model.TransformPoint(at);
        }

        // Degrees down from level for a punch from this shoulder to reach the player's chest.
        private float PitchTo(Transform shoulder)
        {
            float chest = player.Camera.transform.position.y - 0.35f;
            float across = Flat(PlayerFloor() - shoulder.position);
            return Mathf.Atan2(shoulder.position.y - chest, Mathf.Max(0.5f, across)) * Mathf.Rad2Deg;
        }

        private void Start()
        {
            maxHealth = lastHealth = health != null ? Mathf.Max(1f, health.Value) : 429f;
            targetBuried = buried.Length > 0 ? buried[0] : 0f;
            targetScale = headScale;
            if (model != null)
            {
                scaleNow = headScale * 0.1f;
                buriedNow = targetBuried + 1f;
                PoseModel();
            }
            slideMove = HasState(slideMoveState) ? slideMoveState : Move;
            slideStand = HasState(slideStandState) ? slideStandState : Move;
            slideWasAt = transform.position;
            lastHitAt = Time.time;
            Stop();
            PlayState(Laugh);
            LaughNow();
            if (smoke != null) EffectPool.Warm(smoke);
            // It bursts up out of the floor (still tiny: the blast is the size of its head).
            GroundBlast(transform.position, PumpkinBossFx.Blast.Big, headScale);
            BreakRooms();
            EnemyHitDamage.AnyHit += OnAnyHit;
            EnemyHitDamage.PartHit += OnPartHit;
        }

        private void OnDestroy()
        {
            EnemyHitDamage.AnyHit -= OnAnyHit;
            EnemyHitDamage.PartHit -= OnPartHit;
            if (crackMade != null) Destroy(crackMade);
            if (hiddenMade != null) Destroy(hiddenMade);
        }

        private void OnAnyHit(EnemyHitDamage enemy, HitResult result)
        {
            if (enemy == null || enemy.gameObject != gameObject || phase == Phase.Dying) return;
            lastHitAt = Time.time;
            if (phase == Phase.Head)
            {
                hitsTaken++;
                PlayState(GetHit, true);
                Say(hurtSound);
                Puff();
                int step = Mathf.Min(hitsTaken, buried.Length - 1);
                targetBuried = buried[step];
                if (hitsTaken >= revealHits) StartRising();
                return;
            }
            // (Counted for punch spam in On Part Hit: every hit that would hurt, a fifth on its pumpkin or not.)
            // Hits never stop it: between bigger moves it only yelps now and then. It reels only when dizzy (the upper
            // body from too much damage, see TrackHealth; the true form after its own cyclone).
            if (act != Act.None && act != Act.Taunt && act != Act.Dizzy) return;
            if (Time.time < nextFlinch || phase == Phase.Transforming || phase == Phase.Rising) return;
            nextFlinch = Time.time + 1.8f;
            Say(hurtSound);
        }

        private void Update()
        {
            KeepLaughing();
            if (!FindPlayer()) return;
            TrackPlayerMove();
            TrackHealth();
            if (phase == Phase.Upper || phase == Phase.True) TryAnswerSpam();
            UpdateSlide();
            UpdateModel();
            UpdateButtShown();
            switch (phase)
            {
                case Phase.Head: UpdateHead(); break;
                case Phase.Rising: UpdateRising(); break;
                case Phase.Upper: UpdateFighting(false); break;
                case Phase.Transforming: UpdateTransforming(); break;
                case Phase.True: UpdateFighting(true); break;
                case Phase.Dying: UpdateDying(); break;
            }
            UpdatePumpkinBody();
        }

        // ---------- health ----------

        private void TrackHealth()
        {
            if (health == null || phase == Phase.Dying) return;
            float now = health.Value;
            daze = Mathf.Max(0f, daze - dizzyFadePerSecond * Time.deltaTime);
            if (now < lastHealth)
            {
                int lost = Mathf.Max(1, Mathf.RoundToInt(lastHealth - now));
                for (int i = 0; i < lost; i++) DropPumpkin();
                // Damage only adds up towards a dizzy spell outside its Stun Guard.
                if (act != Act.Dizzy && Time.time >= stunGuardUntil) daze += (lastHealth - now) / maxHealth;
            }
            lastHealth = now;
            if (now <= 0f) { StartDying(); return; }
            if (phase != Phase.Upper) return;
            if (now <= maxHealth * trueFormBelow && act == Act.None) StartTransform();
            // Too much damage too fast makes it dizzy, but only between moves: a punch or slam under way always finishes.
            // (Low on health it may shrug that off too; with True Form Below at 0.4 it never gets here that low.)
            else if (daze >= dizzyAfterDamage && act == Act.None) TryStun(Stun.Damage, BeingHit, dizzySeconds);
        }

        // ---------- the one stun rule ----------
        //
        // Every dizzy spell comes through here (user, 2026-11-11: "don't let the player stun it forever"):
        // 1. Stun Guard: after a dizzy spell ends, none for Stun Guard Seconds. A stun that comes meanwhile is shaken off:
        //    after its cyclone it roars and fights on, a wall bonk only staggers it (Stagger Seconds, no stars) and it roars,
        //    and damage doesn't even add up towards one (Track Health).
        // 2. Low on health, Hit Settings' Low Health Stun Chance may shrug off the damage dizzy and the cyclone's (as before;
        //    a wall bonk is the player's doing, so it always counts).
        // 3. A dizzy spell that starts within Stun Repeat Window of the last one's end is shorter: Stun Repeat Share of its
        //    length per spell in a row (3 s, 1.8 s, then Stun Shortest, 1.5 s).
        // 4. The pumpkin butt breaking is the one exception: Break Dizzy Seconds in full, guard or not. The guard (and the
        //    count for rule 3) starts after it like after any other.
        // Returns true if it's dizzy now.
        private bool TryStun(Stun from, string state, float seconds)
        {
            bool earned = from == Stun.ButtBreak;
            bool shrug = !earned && (Time.time < stunGuardUntil || ((from == Stun.Damage || from == Stun.Cyclone) && ResistsStun()));
            if (shrug)
            {
                if (from == Stun.Damage) daze = 0f;
                else if (from == Stun.Cyclone) Say(roarSound);
                else if (from == Stun.Bonk)
                {
                    Begin(Act.Stagger, BeingHit, staggerSeconds, SinkDown(PumpkinBossFx.Blast.Small));
                    Say(roarSound);
                }
                return false;
            }
            if (!earned)
            {
                stunChain = lastStunEnded >= 0f && Time.time - lastStunEnded <= stunRepeatWindow ? stunChain + 1 : 0;
                if (stunChain > 0) seconds = Mathf.Max(Mathf.Min(seconds, stunShortest), seconds * Mathf.Pow(stunRepeatShare, stunChain));
            }
            StartDizzy(state, seconds);
            return true;
        }

        // Low on health it's dizzy only now and then (user, 2026-11-10; Hit Settings > Stunned: Low Health Below and Low
        // Health Stun Chance, shared with the mini bosses). One roll each time it would get dizzy.
        private bool ResistsStun() => health != null && HitSettings.Shared.ResistsStun(health.Value / maxHealth);

        // Spam Hits of the player's hits within Spam Seconds: it answers at once with its cyclone, or in its true form
        // maybe its body slam, cutting short whatever smaller move it was making.
        private void TryAnswerSpam()
        {
            recentHits.RemoveAll(t => Time.time - t > spamSeconds);
            if (recentHits.Count < spamHits || Time.time < nextSpamAnswer) return;
            if (act == Act.JumpSlam || act == Act.Cyclone || act == Act.Dizzy || act == Act.Charge) return;
            // Not in the middle of a hop: it answers once it has landed.
            if (hopStep != HopStep.None) return;
            recentHits.Clear();
            if (act == Act.Vomit) EndVomit(true);
            throwsLeft = 0;
            waitingForPumpkins = false;
            // Its pumpkin broken, it has no bounce left: the cyclone.
            if (phase == Phase.True && !buttBroken && Random.value < spamSlamChance) StartJumpSlam();
            else StartCyclone();
            nextSpamAnswer = actUntil + spamCooldown;
        }

        // Whatever it was doing, it stands there swaying, arms hanging, for a few seconds (being_hit, an upper-body clip:
        // the true form sinks to its upper body's depth for it, see Sink Down). Only through Try Stun.
        private void StartDizzy(string state, float seconds)
        {
            daze = 0f;
            Begin(Act.Dizzy, state, seconds, SinkDown(PumpkinBossFx.Blast.Small));
            Say(hurtSound);
            Puff();
            // Stars circling over its head while it reels.
            if (TryGetComponent(out Game.Combat.EnemyHitDamage hits)) hits.ShowStunned(seconds, head, dizzyStars.x, dizzyStars.y);
        }

        private void DropPumpkin()
        {
            drops.RemoveAll(d => d == null);
            if (pumpkinDrop == null || drops.Count >= maxDropsAround) return;
            // Round it, outside its giant pumpkin while that's out (1.8 m wide on each side in the true form).
            float inner = Mathf.Max(1f, BodyRadius + 0.4f);
            var around = Random.insideUnitCircle.normalized * Random.Range(inner, inner + 1.2f);
            var at = transform.position + new Vector3(around.x, 1.6f, around.y);
            var drop = Instantiate(pumpkinDrop, at, Random.rotation, transform.parent);
            if (drop.TryGetComponent(out Rigidbody body))
                body.linearVelocity = new Vector3(around.x, 2.5f, around.y) * 1.2f;
            drops.Add(drop);
        }

        // ---------- head phase ----------

        private void UpdateHead()
        {
            Face(player.Camera.transform.position, 90f);
            if (!wokeItself && Time.time - lastHitAt >= wakeUpSeconds)
            {
                // Bored of waiting: it punches itself awake, once.
                wokeItself = true;
                PlayState(GetHit, true);
                LaughNow();
                Puff();
                StartRising();
                return;
            }
            if (playing == GetHit && Time.time >= actUntil) PlayState(Laugh);
        }

        private void StartRising()
        {
            phase = Phase.Rising;
            targetBuried = buried[buried.Length - 1];
            targetScale = upperScale;
            PlayState(Come, true);
            LaughNow();
            phaseUntil = Time.time + 2.3f;
            risingStarted = Time.time;
            marks = 0;
            // Its body heaves up out of the floor.
            GroundBlast(transform.position, PumpkinBossFx.Blast.Medium);
        }

        // Coming out ("come"): its arms rise out of the ground and its fists slap the floor either side (right at
        // 1.92 s, left at 2.05 s), then it fights.
        private void UpdateRising()
        {
            float t = Time.time - risingStarted;
            if (t >= 1.92f && Once(MarkFistR)) { GroundBlast(FistPoint(handR), PumpkinBossFx.Blast.Medium); SendShake("ShakeSmall"); }
            if (t >= 2.05f && Once(MarkFistL)) GroundBlast(FistPoint(handL), PumpkinBossFx.Blast.Medium);
            if (Time.time >= phaseUntil) EnterUpper();
        }

        // Out of the ground it comes out swinging: a two-arm slam on the spot first, then the chase.
        private void EnterUpper()
        {
            phase = Phase.Upper;
            // Hits it took as a head or while rising don't count towards its first dizzy spell.
            daze = 0f;
            agent.speed = trueSpeed * upperSpeed;
            Begin(Act.Slam, Slam, 2.75f);
            PlaySound(attackSound);
        }

        // ---------- fighting ----------

        private void UpdateFighting(bool trueForm)
        {
            if (act != Act.None) { UpdateAct(trueForm); return; }
            // A hop under way goes on (nothing starts in the air).
            if (hopStep != HopStep.None) { UpdateHop(); return; }
            var target = PlayerFloor();
            float distance = Flat(target - transform.position);
            // Its pumpkins are still in the air: it watches where they land.
            if (waitingForPumpkins)
            {
                Stop();
                Face(target, 240f);
                PlayState(StandingIdle(trueForm), false, MoveBlend);
                if (comboHit || pumpkinsOut <= 0 || Time.time >= pumpkinsWaitUntil) AfterThrows();
                return;
            }
            // The upper body's rubber-arm punch reaches far, so it stops well away and punches.
            float reach = Reach(trueForm);
            if (Time.time >= nextAct && ChooseAttack(trueForm, distance, reach)) return;
            // Sliding, it stops slowly: it lets go this much sooner, so it glides to a halt about where it would have
            // stopped (a player who steps aside meanwhile still has it slide past).
            float glide = Sliding ? GlideDistance : 0f;
            // Sunk in the floor (true form), it only climbs out once the player is really out of its reach.
            if (Sunk ? distance > reach + glide : distance > reach * 0.85f + glide)
            {
                // It needs its legs (or its pumpkin) to get about: up out of the floor first, then off it goes. With its
                // pumpkin broken it stays in the floor and slides through it like its upper body.
                if (trueForm && !buttBroken && (Sunk || BuriedNow > 0.3f))
                {
                    // Between moves it stays down until it has picked its next one: a far player gets a clap, from the
                    // floor, so climbing out first would only drop it straight back in (throws and body slams climb out
                    // by themselves).
                    if (Sunk && Time.time < nextAct)
                    {
                        Stop();
                        Face(target, 240f);
                        PlayState(StandingIdle(trueForm), false, MoveBlend);
                        return;
                    }
                    StandUp();
                    Stop();
                    Face(target, 240f);
                    PlayState(TrueStandState, false, MoveBlend);
                    return;
                }
                // Far away on its pumpkin: it hops there instead of sliding (Butt Hops).
                if (trueForm && CanHop && distance > hopFrom * Size && StartHop(target, distance)) return;
                agent.isStopped = false;
                agent.stoppingDistance = reach * 0.6f;
                agent.SetDestination(target);
                // Sliding but getting nowhere (pushing into a gap too narrow for its pumpkin): it hops instead.
                if (trueForm && CanHop && SlideStuck() && StartHop(target, distance)) return;
                // Sliding, it turns its body to where it's heading (where it really goes lags behind: momentum).
                if (Sliding) Face(agent.desiredVelocity.sqrMagnitude > 0.04f ? transform.position + agent.desiredVelocity : target, slideTurnSpeed);
                PlayState(trueForm ? TrueMoveState : Move, false, MoveBlend);
            }
            else
            {
                Stop();
                Face(target, 240f);
                PlayState(StandingIdle(trueForm), false, MoveBlend);
            }
        }

        // Blending between getting about and standing: Slide Blend on its pumpkin, the usual 0.15 s otherwise.
        private float MoveBlend => Sliding ? slideBlend : 0.15f;

        // Waiting between moves: the upper body (or the true form sunk to it) sways in "move", the true form on its legs
        // stands in "idle_true" (on its pumpkin: Slide Stand State).
        private string StandingIdle(bool trueForm) => trueForm && !Sunk ? (GlidingStill() ? TrueMoveState : TrueStandState) : Move;

        // Letting go to glide to a halt it keeps its slide pose until it has nearly stopped (play test 2026-11-11: it
        // dropped into idle_true still gliding at 4-5 m/s). A little hysteresis so it doesn't flick between the two.
        private const float GlidePoseOn = 1.2f, GlidePoseOff = 0.6f;
        private bool glidePose;

        private bool GlidingStill()
        {
            if (!Sliding || agent == null || !agent.enabled)
            {
                glidePose = false;
                return false;
            }
            var v = agent.velocity;
            v.y = 0f;
            float speed = v.magnitude;
            if (speed > GlidePoseOn) glidePose = true;
            else if (speed < GlidePoseOff) glidePose = false;
            return glidePose;
        }

        // The true form's clips for getting about and for standing: on its legs run / idle_true, on its pumpkin the
        // upper-body ones (Slide Move / Slide Stand State).
        private string TrueMoveState => slideOnPumpkin ? slideMove : Run;
        private string TrueStandState => slideOnPumpkin ? slideStand : IdleTrue;

        // Sliding on its pumpkin: its true form with Slide On Pumpkin (momentum, leaning, dust, grinding).
        private bool Sliding => slideOnPumpkin && phase == Phase.True;

        // How far it glides before it stops, from the speed it's going now (v² / 2a).
        private float GlideDistance
        {
            get
            {
                if (agent == null) return 0f;
                var v = agent.velocity;
                v.y = 0f;
                return v.sqrMagnitude / (2f * Mathf.Max(0.5f, slideAcceleration));
            }
        }

        // What it does next; false: it just chases the player (or stands facing them, in reach).
        private bool ChooseAttack(bool trueForm, float distance, float reach)
        {
            // Too close for its punches to land: its cyclone pushes the player away.
            if (distance <= tooClose * Size && Time.time >= nextCyclone)
            {
                StartCyclone();
                return true;
            }
            // True form: the body slam is its main move. Once it's ready and the player is in range: when the dice said
            // so after its last move, after a couple of punches, or when its punches keep missing.
            if (trueForm && BodySlamReady(distance) &&
                (bodySlamNext || spreeMisses >= missesBeforeBodySlam ||
                 (punchesBeforeBodySlam > 0 && punchesSinceBodySlam >= punchesBeforeBodySlam)))
            {
                StartJumpSlam();
                return true;
            }
            if (distance <= reach)
            {
                StartPunch(distance);
                return true;
            }
            // True form on its pumpkin: the slide charge at a player out of its punches' reach, when the dice said so after
            // its last move (Charge Chance), the cooldown is over and they're within Charge Range. It comes after a body
            // slam that's due (its main move) and before the throws and claps, which it replaces that time.
            if (trueForm && chargeNext && ChargeReady(distance, reach))
            {
                StartCharge();
                return true;
            }
            // The player got out of reach after a few punches: pumpkins, at once.
            if (spreePunches >= punchesBeforeThrow)
            {
                StartThrows();
                return true;
            }
            // Far away (further than it runs in Far Seconds): it claps, and throws if they're still that far.
            if (Time.time >= chaseUntil && distance > FormSpeed * farSeconds)
            {
                StartClap(true);
                return true;
            }
            // Chasing a player it can't catch for that long: pumpkins.
            if (Time.time >= chaseUntil && Time.time - idleSince > chaseSeconds)
            {
                StartThrows();
                return true;
            }
            return false;
        }

        // Its size (the model's scale, without the bounce's squash).
        private float Size => model != null ? scaleNow : 1f;

        // Its rubber-arm punches reach far in both forms (the fist flies out to about 3.2 times its size).
        private float Reach(bool trueForm) => 2.6f * Size;

        // Where its two-arm slam comes down, in front of it (times its size): only there does it slam.
        private const float SlamFrom = 1.7f, SlamTo = 2.5f;

        // Its pumpkin broken, it slides through the floor as fast as its upper body.
        private float FormSpeed => phase == Phase.True && !buttBroken ? trueSpeed : trueSpeed * upperSpeed;

        // True form: its body slam has cooled down and the player is close enough to leap at. Never with its pumpkin
        // broken (it fights from the floor).
        private bool BodySlamReady(float distance)
        {
            return !buttBroken && Time.time >= nextBodySlam && (bodySlamRange <= 0f || distance <= bodySlamRange * Size);
        }

        // The slide charge can start: on its pumpkin (or, broken, through the floor), cooled down, the player out of its
        // punches' reach and within Charge Range.
        private bool ChargeReady(float distance, float reach)
        {
            return slideOnPumpkin && chargeChance > 0f && Time.time >= nextCharge && distance > reach &&
                   distance <= chargeRange * Size;
        }

        // One of its three punches: left, right, or both arms (the slam, only when the player stands where it lands).
        private void StartPunch(float distance)
        {
            bool canSlam = distance >= SlamFrom * Size && distance <= SlamTo * Size;
            float r = Random.value * (canSlam ? 3f : 2f);
            // Upper-body moves: the true form drops into the floor for them (the legs fold up as it goes down).
            float fade = SinkDown(PumpkinBossFx.Blast.Small);
            if (r < 1f) Begin(Act.PunchL, PunchLState, 1.6f, fade);
            else if (r < 2f) Begin(Act.PunchR, PunchRState, 1.6f, fade);
            else Begin(Act.Slam, Slam, 2.75f, fade);
            spreePunches++;
            punchesSinceBodySlam++;
            PlaySound(attackSound);
        }

        // ---------- throwing pumpkins ----------

        // A combo of Throw Combo pumpkins, left hand then right.
        private void StartThrows()
        {
            throwsLeft = Random.Range(Mathf.Max(1, throwCombo.x), Mathf.Max(throwCombo.x, throwCombo.y) + 1);
            throwLeftNext = true;
            pumpkinsOut = 0;
            comboHit = false;
            spreePunches = 0;
            spreeMisses = 0;
            NextThrow();
        }

        private void NextThrow()
        {
            // Throwing takes its legs (true form): it climbs out of the floor first (the pumpkin leaves at 0.65 s).
            StandUp();
            throwsLeft--;
            bool left = throwLeftNext;
            throwLeftNext = !left;
            throwHand = left ? handL : handR;
            if (left && !HasState(leftThrowState))
            {
                // No left-hand throw clip: its left punch swings and lets the pumpkin go.
                Begin(Act.Throw, PunchLState, 1.6f);
                throwRelease = 0.7f;
            }
            else
            {
                Begin(Act.Throw, left ? leftThrowState : ThrowState, 1.5f);
                throwRelease = 0.65f;
            }
            PlaySound(attackSound);
        }

        // All thrown: a hit slowed the player, so it chases them; dodged them all, it vomits bugs to back it up or
        // claps for the player, then chases.
        private void AfterThrows()
        {
            waitingForPumpkins = false;
            if (!comboHit)
            {
                if (Random.value < vomitAfterDodge && CanVomit()) StartVomit(vomitBugs);
                // The true form leaps at a player who dodged everything instead of clapping, when it can.
                else if (phase == Phase.True && BodySlamReady(Flat(PlayerFloor() - transform.position))) StartJumpSlam();
                else StartClap(false);
            }
            chaseUntil = Mathf.Max(Time.time, actUntil) + chaseSeconds;
        }

        private void StartClap(bool thenThrow)
        {
            // An upper-body move: the true form drops into the floor for it.
            Begin(Act.Clap, Clap, 1.5f, SinkDown(PumpkinBossFx.Blast.Small));
            clapToThrow = thenThrow;
        }

        private bool HasState(string state)
        {
            return animator != null && !string.IsNullOrEmpty(state) && animator.HasState(0, Animator.StringToHash(state));
        }

        // A move starts: its clip (blended in over Fade seconds, from Clip From seconds into it) and how long it lasts.
        private void Begin(Act what, string state, float seconds, float fade = 0.15f, float clipFrom = 0f)
        {
            // A charge cut short (a wall bonk, its pumpkin breaking) still waits out its cooldown.
            if (act == Act.Charge && what != Act.Charge) nextCharge = Time.time + chargeCooldown;
            // A move ends any hop (its lift and squash ease back).
            hopStep = HopStep.None;
            fistTracked.Clear();
            marks = 0;
            act = what;
            actStarted = Time.time;
            actUntil = Time.time + seconds;
            actHit = false;
            actImpact = false;
            Stop();
            PlayState(state, true, fade, clipFrom);
        }

        private void UpdateAct(bool trueForm)
        {
            float t = Time.time - actStarted;
            var target = PlayerFloor();
            switch (act)
            {
                case Act.PunchL:
                case Act.PunchR:
                    // It charges (aiming) and lets fly at about 0.8 s; the stretched fist flies out to its full reach
                    // (it passes a player 3.5 m away at about 1.1 s).
                    if (t < 0.7f) Face(target, 300f);
                    if (t >= PunchRelease && t <= PunchHitUntil) TryHit(act == Act.PunchR ? handR : handL, 0.32f, punchLaunch, false);
                    break;
                case Act.Slam:
                    if (t < 1.3f) Face(target, 200f);
                    if (t >= 1.5f && t <= 1.8f) { TryHit(handL, 0.42f, slamLaunch, false); TryHit(handR, 0.42f, slamLaunch, false); }
                    // The fists smash into the floor: a blast under each and a shake. Standing on its legs (true form)
                    // the clip's fists come down short of the floor: the blast goes off at the fist, dust below it.
                    if (!actImpact && t >= 1.58f)
                    {
                        actImpact = true;
                        foreach (var fist in new[] { handL, handR })
                        {
                            if (fist == null) continue;
                            var at = FistPoint(fist);
                            if (at.y - transform.position.y < 1f) { GroundBlast(at, PumpkinBossFx.Blast.Big); continue; }
                            if (fx != null) fx.Hit(at, at + Vector3.up, Size);
                            GroundBlast(at, PumpkinBossFx.Blast.Small);
                        }
                        SendShake("ShakeMed");
                    }
                    break;
                case Act.Throw:
                    Face(target, 260f);
                    // A pumpkin already hit: the combo's over, this one isn't thrown.
                    if (comboHit && !actHit) { throwsLeft = 0; actUntil = Time.time; break; }
                    if (!actHit && t >= throwRelease) { actHit = true; ThrowBugPumpkin(throwHand); }
                    // The next throw follows at once; a pumpkin that hit ends the combo (it chases the slowed player).
                    if (actHit && t >= throwRelease + throwGap)
                    {
                        if (comboHit) throwsLeft = 0;
                        if (throwsLeft > 0) { NextThrow(); return; }
                    }
                    break;
                case Act.Clap:
                    Face(target, 240f);
                    // Nearly over: still far away, the player gets pumpkins; otherwise it chases them.
                    if (clapToThrow && !actHit && t >= 1.2f)
                    {
                        actHit = true;
                        if (Flat(target - transform.position) > FormSpeed * farSeconds) { StartThrows(); return; }
                        chaseUntil = Time.time + chaseSeconds;
                    }
                    break;
                case Act.JumpSlam:
                    if (slideOnPumpkin) UpdateButtBounce(t);
                    else UpdateJumpSlam(t);
                    break;
                case Act.Charge:
                    // It sets the act's end itself (once it has braked, or it bonked).
                    if (!UpdateCharge(t)) return;
                    break;
                case Act.Vomit:
                    // It turns to where it's going to throw up (away from a player standing close).
                    if (t < 0.6f) Face(vomitSpot, 200f);
                    if (fx != null) fx.VomitMouth(MouthPoint(), MouthDirection(), transform.position.y);
                    // The bugs come up with its retches, shared out between them, and crawl out of the black smoke
                    // a moment later.
                    if (retchesDone < VomitRetches.Length && t >= VomitRetches[retchesDone])
                    {
                        int retchesLeft = VomitRetches.Length - retchesDone++;
                        int n = Mathf.Min(bugsLeft, Mathf.CeilToInt(bugsLeft / (float)retchesLeft));
                        bugsLeft -= n;
                        for (int i = 0; i < n; i++) bugsDue.Add(Time.time + vomitBugDelay + i * 0.08f);
                        Retch();
                    }
                    for (int i = bugsDue.Count - 1; i >= 0; i--)
                    {
                        if (Time.time < bugsDue[i]) continue;
                        bugsDue.RemoveAt(i);
                        SpitBug();
                    }
                    break;
                case Act.Cyclone:
                    // Winds up on the spot, spins after the player, then stops dead.
                    bool spinning = t >= CycloneSpinFrom && t <= CycloneSpinTo;
                    if (spinning) { agent.isStopped = false; agent.SetDestination(target); }
                    else { Stop(); if (t < CycloneSpinFrom) Face(target, 200f); }
                    // One hit per cyclone, from the flying rubber fists or the spinning body: it throws the player
                    // clear instead of grinding them down.
                    if (spinning && !actHit)
                    {
                        TryHit(handL, 0.3f, cycloneLaunch, trueForm);
                        TryHit(handR, 0.3f, cycloneLaunch, trueForm);
                        float span = 1.15f * Size;
                        if (!actHit && PlayerNear(transform.position + Vector3.up * 1.6f, span))
                        {
                            actHit = true;
                            if (fx != null) fx.Hit(PlayerChest(), transform.position + Vector3.up * 1.6f, Size);
                            HitPlayer(cycloneLaunch, trueForm);
                        }
                    }
                    // Spinning half sunk in the floor, it churns up dust all round it.
                    if (spinning && Time.time >= nextScrape && fx != null)
                    {
                        nextScrape = Time.time + 0.3f;
                        var round = Random.insideUnitCircle.normalized * (0.9f * Size);
                        fx.Scrape(transform.position + new Vector3(round.x, 0f, round.y), Size);
                    }
                    // The hard stop: both fists drive into the floor.
                    if (t >= 2.62f && Once(MarkHardStop))
                    {
                        GroundBlast(FistPoint(handL), PumpkinBossFx.Blast.Big);
                        GroundBlast(FistPoint(handR), PumpkinBossFx.Blast.Big);
                        SendShake("ShakeMed");
                    }
                    break;
            }
            if (Time.time < actUntil) return;
            if (act == Act.Vomit) EndVomit(true);
            if (act == Act.Cyclone)
            {
                agent.speed = FormSpeed;
                // It spun itself dizzy, still sunk into its upper-body form (it reels like one). Low on health it often
                // shakes it off with a roar and fights straight on, and always within its Stun Guard (Try Stun).
                if (cycloneDizzySeconds > 0f && TryStun(Stun.Cyclone, BeingHit, cycloneDizzySeconds)) return;
            }
            if (act == Act.Charge) nextCharge = Time.time + chargeCooldown;
            // Sunk in the floor (true form, after a punch, clap or its cyclone), it stays down while it fights the player
            // in its reach, and climbs out only when it needs its legs (to run, throw, vomit or jump).
            // A punch that didn't land counts towards the body slam (Start Jump Slam starts the count again).
            if (act == Act.PunchL || act == Act.PunchR || act == Act.Slam) spreeMisses = actHit ? 0 : spreeMisses + 1;
            // The last pumpkin is away: it watches them land (UpdateFighting), then decides.
            if (act == Act.Throw) { waitingForPumpkins = true; pumpkinsWaitUntil = Time.time + 2.5f; }
            // A dizzy spell is over: its Stun Guard starts (Try Stun).
            if (act == Act.Dizzy)
            {
                stunGuardUntil = Time.time + stunGuardSeconds;
                lastStunEnded = Time.time;
            }
            // True form: roll once per move whether the next one is a body slam (it waits for the cooldown and range).
            if (trueForm && !bodySlamNext) bodySlamNext = Random.value < bodySlamChance;
            // ...and, on its pumpkin, whether the next one is the slide charge (rolled afresh after every move).
            chargeNext = trueForm && slideOnPumpkin && Random.value < chargeChance;
            act = Act.None;
            idleSince = Time.time;
            nextAct = Time.time + (trueForm ? Random.Range(0.6f, 1.2f) : Random.Range(0.8f, 1.6f));
        }

        private void StartJumpSlam()
        {
            // Out of the floor first if it's sunk (it takes off at 0.6 s; the take-off blast covers the climb).
            StandUp(false);
            spreeMisses = 0;
            punchesSinceBodySlam = 0;
            bodySlamNext = false;
            nextBodySlam = Time.time + 2.2f + bodySlamCooldown;
            // On its pumpkin: the butt bounce (code hops the whole boss; its upper body plays the two-arm slam, timed so
            // the arms come down as it lands). It stops dead for it, no gliding.
            if (slideOnPumpkin)
            {
                Begin(Act.JumpSlam, HasState(bounceState) ? bounceState : Move, 2.2f, 0.15f, bounceClipFrom);
                HardStop();
            }
            else Begin(Act.JumpSlam, JumpSlamState, 2.2f);
            Say(roarSound != null ? roarSound : attackSound);
            jumpFrom = transform.position;
            var to = PlayerFloor();
            var leap = to - jumpFrom;
            leap.y = 0f;
            if (slideOnPumpkin && BodyRadius > 0f)
            {
                // On its pumpkin: the pumpkin's front edge comes down on the player (its middle short of them by most of its
                // radius), so they're hit and thrown clear rather than landed on (play test 2026-11-11).
                float reachTo = Mathf.Min(8f, Mathf.Max(0f, leap.magnitude - BodyRadius * 0.9f));
                jumpTo = jumpFrom + (leap.sqrMagnitude > 0.0001f ? leap.normalized : Vector3.zero) * reachTo;
            }
            else
            {
                if (leap.magnitude > 8f) leap = leap.normalized * 8f;
                // Lands just short of where the player stood, so the shockwave reaches them.
                jumpTo = jumpFrom + leap * 0.85f;
            }
            if (NavMesh.SamplePosition(jumpTo, out var hit, 3f, NavMesh.AllAreas)) jumpTo = hit.position;
        }

        private void UpdateJumpSlam(float t)
        {
            if (t < 0.55f) Face(jumpTo, 240f);
            // It kicks off: dust where it stood.
            if (t >= 0.6f && Once(MarkTakeOff)) GroundBlast(jumpFrom, PumpkinBossFx.Blast.Medium);
            if (t >= 0.6f && t <= 1.32f)
            {
                float k = Mathf.InverseLerp(0.6f, 1.32f, t);
                var p = Vector3.Lerp(jumpFrom, jumpTo, k);
                agent.Warp(p);
            }
            if (!actHit && t >= 1.32f)
            {
                actHit = true;
                JumpSlamLands();
            }
        }

        // The landing of its jump or butt bounce: it lands with both fists slammed into the floor (or butt first): its
        // biggest blast (its dust rolls out past the fists; more blasts on top would be a lot of smoke for the headset,
        // right where the player stands).
        private void JumpSlamLands()
        {
            GroundBlast(transform.position, PumpkinBossFx.Blast.Huge);
            // Butt first onto whatever is there (Crushing).
            if (slideOnPumpkin) CrushAround(transform.position + Vector3.up * (0.6f * BodyRadius), BodyRadius + crushReach);
            float radius = 1.7f * Size;
            var flat = PlayerFloor() - transform.position;
            flat.y = 0f;
            // It body-slams a lot, so its body slam hurts like an upper-body hit (Upper Hit Event, 70-90), not a
            // heavy one: a few in a row are no one-shot loop.
            if (flat.magnitude <= radius && !PlayerFlying()) HitPlayer(jumpSlamLaunch, false);
            // After the hit: the player's own hit shakes (sent by HitPlayer) would cut this big one short.
            SendShake("ShakeBig");
        }

        // The butt bounce (Slide On Pumpkin), the same moments as the leg jump: it squashes down (0.3-0.6 s), the whole boss
        // hops off at 0.6 s on an arc to where the player stood (Bounce Height high, stretched in the air), and lands butt
        // first at 1.32 s: squashed flat, then springing back with a wobble. The hop lifts the model on top of how deep
        // it's buried; the agent moves it along the floor.
        private const float BounceSquashFrom = 0.3f, BounceTakeOff = 0.6f, BounceLands = 1.32f;

        private void UpdateButtBounce(float t)
        {
            if (t < BounceTakeOff - 0.05f) Face(jumpTo, 240f);
            if (t < BounceTakeOff)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BounceSquashFrom, BounceTakeOff, t));
                squash = 1f - bounceSquash * 0.8f * k;
                hopLift = 0f;
                return;
            }
            if (t < BounceLands)
            {
                // Off it goes: dust where it sat.
                if (Once(MarkTakeOff)) GroundBlast(jumpFrom, PumpkinBossFx.Blast.Medium);
                float k = Mathf.InverseLerp(BounceTakeOff, BounceLands, t);
                agent.Warp(Vector3.Lerp(jumpFrom, jumpTo, k));
                hopLift = 4f * bounceHeight * Size * k * (1f - k);
                // Stretched most leaving and nearing the ground (fastest), round at the top.
                squash = 1f + bounceStretch * Mathf.Abs(1f - 2f * k);
                return;
            }
            hopLift = 0f;
            if (!actHit)
            {
                actHit = true;
                agent.Warp(jumpTo);
                JumpSlamLands();
            }
            // Squashed flat on landing, springing back with a wobble that dies away.
            float u = t - BounceLands;
            squash = 1f - bounceSquash * Mathf.Exp(-u * 7f) * Mathf.Cos(u * 18f);
        }

        private void StartCyclone()
        {
            // Standing on its legs its arms would spin over the player's head: it sinks back into its upper-body form
            // for the spin (and the dizzy spell after it), the ground bursting round it, and stays down until it needs
            // its legs again.
            Begin(Act.Cyclone, CycloneState, cycloneSeconds, SinkDown(PumpkinBossFx.Blast.Medium));
            agent.speed = FormSpeed * 0.7f;
            nextCyclone = Time.time + cycloneSeconds + cycloneCooldown;
            nextScrape = Time.time + CycloneSpinFrom;
            Say(roarSound != null ? roarSound : attackSound);
        }

        private void StartVomit(int count)
        {
            // It bends over on its legs to throw up (true form): out of the floor first.
            StandUp();
            Begin(Act.Vomit, VomitState, 2.0f);
            // No gliding onto its own cloud of bugs.
            HardStop();
            bugsLeft = count;
            retchesDone = 0;
            bugsDue.Clear();
            vomitSpot = PickVomitSpot();
            if (fx != null) fx.BeginVomit(Size, vomitSpot);
        }

        // Where its bugs come out, under the black cloud: Vomit Ahead in front of it, towards the player, but turned
        // aside from a player standing close, so the cloud doesn't swallow them and the bugs don't land at their feet.
        private Vector3 PickVomitSpot()
        {
            var toPlayer = PlayerFloor() - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            var ahead = distance > 0.01f ? toPlayer / distance : transform.forward;
            if (distance < VomitAhead + 2.5f)
                ahead = Quaternion.Euler(0f, Random.value < 0.5f ? -70f : 70f, 0f) * ahead;
            var spot = transform.position + ahead * VomitAhead;
            return NavMesh.SamplePosition(spot, out var hit, 2f, NavMesh.AllAreas) ? hit.position : spot;
        }

        // The vomit is over: bugs still on their way come out now (or, cut short, never).
        private void EndVomit(bool letBugsOut)
        {
            if (letBugsOut) foreach (float _ in bugsDue) SpitBug();
            bugsDue.Clear();
            if (fx != null) fx.EndVomit();
        }

        private bool CanVomit() => bugs.Length > 0 && EnemyLimit.AliveCount < EnemyLimit.Max;

        // A gush of black smoke out of its mouth on every retch, bugs or not.
        private void Retch()
        {
            if (fx != null) fx.Retch(MouthPoint());
        }

        // Its lips: the jaw bone runs from its hinge out to the chin (about 0.3 of its size long).
        private Vector3 MouthPoint()
        {
            if (jaw != null) return jaw.position + jaw.up * 0.3f * Size;
            return head != null ? head.position + transform.forward * 0.5f * Size : transform.position + Vector3.up * Size;
        }

        // Out of its mouth (down while it's bent over to retch) and a little ahead, never upwards.
        private Vector3 MouthDirection()
        {
            var outOfMouth = jaw != null ? jaw.up : -transform.up;
            var dir = outOfMouth + transform.forward * 0.35f;
            if (dir.y > -0.3f) dir.y = -0.3f;
            return dir.normalized;
        }

        private void SpitBug()
        {
            if (bugs.Length == 0 || !EnemyLimit.TryTakeSlot()) return;
            var prefab = bugs[Random.Range(0, bugs.Length)];
            if (prefab == null) return;
            var forward = transform.forward;
            // Out of the middle of the black cloud.
            var around = Random.insideUnitCircle * 1.3f;
            var spot = vomitSpot + new Vector3(around.x, 0f, around.y);
            if (!NavMesh.SamplePosition(spot, out var hit, 2.5f, NavMesh.AllAreas)) return;
            var bug = Instantiate(prefab, hit.position, Quaternion.LookRotation(forward), transform.parent);
            // Vomited up to fight: it goes for the player at once instead of looking around first.
            if (bug.TryGetComponent(out SpiderBrain brain)) brain.ComeAtOnce();
        }

        private void TrackPlayerMove()
        {
            var floor = PlayerFloor();
            if (playerSeen && Time.deltaTime > 0f)
            {
                var moved = floor - playerWasAt;
                moved.y = 0f;
                moved /= Time.deltaTime;
                // A teleport isn't walking: it starts again from standing still.
                playerMove = moved.sqrMagnitude > TeleportSpeed * TeleportSpeed
                    ? Vector3.zero
                    : Vector3.Lerp(playerMove, moved, 1f - Mathf.Exp(-Time.deltaTime / PlayerMoveSmooth));
            }
            playerWasAt = floor;
            playerSeen = true;
        }

        // Thrown from the hand at where the player will be if they keep moving as they are: they dodge it by changing direction.
        private void ThrowBugPumpkin(Transform hand)
        {
            if (bugPumpkin == null) return;
            var from = hand != null ? hand.position : transform.position + Vector3.up * 3f;
            const float flight = 1.0f;
            var to = PlayerFloor() + Vector3.ClampMagnitude(playerMove * (flight * throwLead), throwLeadMost) + Vector3.up * 0.3f;
            var velocity = (to - from) / flight - 0.5f * Physics.gravity * flight;
            var thrown = Instantiate(bugPumpkin, from, Random.rotation, transform.parent);
            if (!thrown.TryGetComponent(out BugPumpkin pumpkin)) return;
            pumpkinsOut++;
            pumpkin.Done += OnPumpkinDone;
            pumpkin.Launch(velocity, gameObject, bugs, bugsPerThrownPumpkin);
        }

        private void OnPumpkinDone(bool hitPlayer)
        {
            pumpkinsOut = Mathf.Max(0, pumpkinsOut - 1);
            if (hitPlayer) comboHit = true;
        }

        // ---------- hitting the player ----------

        private void TryHit(Transform hand, float radius, Vector2 launch, bool trueForm)
        {
            if (actHit || hand == null) return;
            // The fist's own hit sphere when it has one (it grows and flies out with the fist); otherwise round the hand.
            Vector3 point;
            float r;
            if (hand.TryGetComponent(out SphereCollider fist))
            {
                var b = fist.bounds;
                point = b.center;
                r = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
            }
            else
            {
                point = hand.position;
                r = radius * Size;
            }
            // The rubber fist covers metres in a frame or two: everything along its path since last frame counts.
            bool reached = PlayerNear(point, r);
            if (!reached && fistTracked.TryGetValue(hand, out var last))
            {
                for (int i = 1; i < 8 && !reached; i++) reached = PlayerNear(Vector3.Lerp(last, point, i / 8f), r);
            }
            fistTracked[hand] = point;
            if (!reached) return;
            actHit = true;
            // The blow lands: a blast where the fist meets them (on the boss's side of it).
            if (fx != null) fx.Hit(point, new Vector3(transform.position.x, point.y, transform.position.z), Size);
            HitPlayer(launch, trueForm || phase == Phase.True);
        }

        private void HitPlayer(Vector2 launch, bool heavy) => HitPlayer(launch, heavy, transform.position);

        // The player is thrown away from From (its middle; the charge throws them on along its way).
        private void HitPlayer(Vector2 launch, bool heavy, Vector3 from) => HitPlayer(heavy ? trueHitEvent : upperHitEvent, launch, from);

        // A blow with this event to the player's PlayerHealth FSM, doing Share times its damage (a hop's: more the closer).
        private void HitPlayer(string healthEvent, Vector2 launch, Vector3 from, float share = 1f)
        {
            var target = player.gameObject;
            target.GetComponents(fsmBuffer);
            // A chaos boss hits harder: the PlayerHealth FSM's damage (Player Toughness ticked) uses this share up.
            if (IsChaos) share *= chaosDamage;
            bool shared = !Mathf.Approximately(share, 1f);
            if (shared) PlayerDamageShare.Next(share);
            foreach (var fsm in fsmBuffer)
            {
                if (fsm.FsmName == playerHealthFsm) fsm.SendEvent(healthEvent);
            }
            // Not used up (the player is down or getting up): thrown away, or the next spider bite would hurt more.
            if (shared) PlayerDamageShare.Take();
            // Push Power throws them further (as high: the same arc, faster); its cyclone has its own.
            float push = act == Act.Cyclone ? cyclonePushPower : pushPower;
            // Its own body (over or against the player as it hits) doesn't cut the throw short (user's play test 2026-11-11:
            // the throws ended after 0.03 s).
            PlayerLaunch.Launch(from, launch.x, launch.y * (push > 0f ? push : 1f), true, true, minPushShare, gameObject);
        }

        // A sphere touching the player's body: a standing cylinder from their feet to their head.
        private bool PlayerNear(Vector3 point, float radius)
        {
            var headPos = player.Camera.transform.position;
            float feet = player.transform.position.y;
            var closest = new Vector3(headPos.x, Mathf.Clamp(point.y, feet, headPos.y), headPos.z);
            return (closest - point).magnitude <= radius + 0.25f;
        }

        private bool PlayerFlying()
        {
            return player.TryGetComponent(out PlayerLaunch launch) && launch.Flying;
        }

        // ---------- transforming and dying ----------

        private void StartTransform()
        {
            phase = Phase.Transforming;
            Stop();
            act = Act.None;
            // A combo or punches under way don't carry over into its true form.
            waitingForPumpkins = false;
            spreePunches = 0;
            spreeMisses = 0;
            punchesSinceBodySlam = 0;
            targetBuried = 0f;
            targetScale = trueScale;
            PlayState(TransformState, true);
            Say(roarSound != null ? roarSound : laughs.Length > 0 ? laughs[0] : null);
            SendShake("ShakeMed");
            phaseUntil = Time.time + 2.0f;
            transformStarted = Time.time;
            marks = 0;
            // The ground cracks open round it...
            GroundBlast(transform.position, PumpkinBossFx.Blast.Big);
        }

        // ...and at 1.2 s its legs are out of the floor: it bursts up out of the ground.
        private void UpdateTransforming()
        {
            if (Time.time - transformStarted >= 1.2f && Once(MarkEmerge))
            {
                GroundBlast(transform.position, PumpkinBossFx.Blast.Huge);
                SendShake("ShakeMed");
            }
            if (Time.time >= phaseUntil) EnterTrue();
        }

        private void EnterTrue()
        {
            phase = Phase.True;
            agent.speed = trueSpeed;
            nextAct = Time.time + 0.4f;
            nextCyclone = Time.time + cycloneCooldown * 0.5f;
            // It shows off its new legs: its first move after the opening vomit is a body slam (in range).
            nextBodySlam = Time.time + 2f;
            bodySlamNext = true;
            punchesSinceBodySlam = 0;
            // No slide charge in its first few seconds (the opening vomit and body slam come first).
            nextCharge = Time.time + chargeCooldown * 0.5f;
            chargeNext = false;
            // Its true form opens by calling its bugs: a big vomit, right away (unless the game is already full).
            if (trueFormVomitBugs > 0 && CanVomit()) StartVomit(trueFormVomitBugs);
            else PlayState(TrueMoveState);
        }

        private void StartDying()
        {
            if (act == Act.Vomit) EndVomit(false);
            if (act == Act.Charge) nextCharge = Time.time + chargeCooldown;
            StopChaosAura();
            phase = Phase.Dying;
            act = Act.None;
            HardStop();
            // No more sliding: no lean, no hop, no grinding.
            leanPitch = leanRoll = leanJitter = 0f;
            hopStep = HopStep.None;
            lean = Quaternion.identity;
            hopLift = 0f;
            squash = 1f;
            grindVolume = 0f;
            if (grindSpeaker != null) grindSpeaker.Stop();
            // It dies with its whole body showing: killed in the ground (as a head, its upper body, or sunk for a
            // punch), it climbs right out. With its pumpkin broken it has no legs or butt to show: it dies where it is,
            // in the floor at its upper body's depth.
            if (!buttBroken) targetBuried = 0f;
            quickBury = true;
            PlayState(Death, true);
            var scream = deathScreams.Length > 0 ? deathScreams[Random.Range(0, deathScreams.Length)] : null;
            Say(scream != null ? scream : hurtSound);
            phaseUntil = Time.time + DeathPoseSeconds;
            if (model != null) modelHome = new Vector3(modelRestPosition.x, model.localPosition.y, modelRestPosition.z);
        }

        // Its death pose (arms up, screaming at the sky) shakes harder and harder; then every pumpkin of its body bursts,
        // one after another, the fists first and the hips last, and the stage goes on.
        private void UpdateDying()
        {
            if (model == null || !model.gameObject.activeSelf) return;
            if (Time.time < phaseUntil)
            {
                Tremble(1f - (phaseUntil - Time.time) / (DeathPoseSeconds - TrembleFrom));
                return;
            }
            if (pumpkinBreak == null || bodyPumpkins == null || bodyPumpkins.Length == 0)
            {
                BurstHead();
                Gone();
                return;
            }
            if (burstingFrom < 0f) StartBursting();
            float t = Time.time - burstingFrom;
            bool allBurst = true;
            for (int i = 0; i < bodyPumpkins.Length; i++)
            {
                if (pumpkinBurst[i]) continue;
                if (t >= bodyPumpkins[i].at) BurstPumpkin(i);
                else allBurst = false;
            }
            if (allBurst) Gone();
        }

        // Shaking in place (0 = still, 1 = hardest), sideways only: its height is how deep it's buried.
        private void Tremble(float amount)
        {
            if (amount <= 0f) return;
            float r = deathTremble * Size * Mathf.Clamp01(amount);
            var p = model.localPosition;
            p.x = modelHome.x + (Mathf.PerlinNoise(Time.time * 31f, 0.3f) - 0.5f) * 2f * r;
            p.z = modelHome.z + (Mathf.PerlinNoise(0.7f, Time.time * 29f) - 0.5f) * 2f * r;
            model.localPosition = p;
        }

        // Seconds of the death clip (arms up, screaming at the sky) before its pumpkins burst, and when the shaking starts.
        private const float DeathPoseSeconds = 2.5f;
        private const float TrembleFrom = 0.8f;
        // How big a tree pumpkin's break (pumpkin_2_Drop) is at scale 1, in metres.
        private const float BreakSize = 0.62f;

        // Its pumpkins, measured on Pumpkin_Boss_Z.fbx (the orange of the atlas, per bone): fists, elbows, shoulders,
        // head, chest, waist, and since the remodel (2026-11-11) the giant pumpkin butt on the hips instead of the knees and
        // the little hips pumpkin (the Slide Setup menu measures it on the mesh and writes the prefab's). The offsets are
        // in the bones' own space (the armature is scaled 100: 0.01 = one model unit).
        private static BodyPumpkin[] DefaultBodyPumpkins() => new[]
        {
            new BodyPumpkin("Hand.L", new Vector3(0.00002f, -0.00032f, -0.0001f), 0.52f, 0f),
            new BodyPumpkin("Hand.R", new Vector3(-0.0001f, 0.00006f, 0.00001f), 0.43f, 0f),
            new BodyPumpkin("LowerArm.L", new Vector3(-0.00018f, 0.00099f, 0.00012f), 0.26f, 0.1f),
            new BodyPumpkin("LowerArm.R", new Vector3(-0.00016f, 0.00019f, -0.00013f), 0.21f, 0.1f),
            new BodyPumpkin("UpperArm.L", new Vector3(-0.00023f, 0.00079f, -0.00014f), 0.29f, 0.2f),
            new BodyPumpkin("UpperArm.R", new Vector3(-0.00021f, 0.00053f, -0.00001f), 0.28f, 0.2f),
            new BodyPumpkin("Head", new Vector3(-0.00009f, 0.00324f, 0.00006f), 0.6f, 0.32f),
            new BodyPumpkin("Chest", new Vector3(-0.00001f, 0.00005f, -0.00009f), 0.33f, 0.42f),
            new BodyPumpkin("Waist", new Vector3(-0.00003f, 0.00243f, 0.00007f), 0.19f, 0.5f),
            // The giant pumpkin: its middle 0.62 model units up, the hips at 0.868; 1.56 wide. Last and biggest.
            new BodyPumpkin("Hips", new Vector3(0f, -0.00248f, 0f), 1.56f, 0.66f),
        };

        private void StartBursting()
        {
            burstingFrom = Time.time;
            var p = model.localPosition;
            p.x = modelHome.x;
            p.z = modelHome.z;
            model.localPosition = p;
            // Its shards fly free (not off its own hit spheres).
            foreach (var part in GetComponentsInChildren<Collider>()) part.enabled = false;
            var named = new Dictionary<string, Transform>();
            foreach (var bone in model.GetComponentsInChildren<Transform>(true)) named[bone.name] = bone;
            pumpkinBones = new Transform[bodyPumpkins.Length];
            pumpkinBurst = new bool[bodyPumpkins.Length];
            var buttAt = buttCollider != null ? buttCollider.transform : null;
            for (int i = 0; i < bodyPumpkins.Length; i++)
            {
                named.TryGetValue(bodyPumpkins[i].bone ?? "", out pumpkinBones[i]);
                // Its pumpkin butt already burst, and (dying in the floor) pumpkins under the floor: nothing to burst there
                // (left on the body, not hidden: the hips hold the rest of it).
                var bone = pumpkinBones[i];
                if (buttBroken && bone != null &&
                    (buttAt == bone || (buttAt != null && buttAt.parent == bone) ||
                     bone.TransformPoint(bodyPumpkins[i].offset).y < transform.position.y))
                    pumpkinBones[i] = null;
                // A bone it hasn't got: nothing to burst there.
                if (pumpkinBones[i] == null) pumpkinBurst[i] = true;
            }
        }

        // One pumpkin of its body bursts into a tree pumpkin's break, as big as it is, with a puff; its shards fly off
        // away from the boss's middle.
        private void BurstPumpkin(int i)
        {
            pumpkinBurst[i] = true;
            var bone = pumpkinBones[i];
            BurstAt(bone.TransformPoint(bodyPumpkins[i].offset), bodyPumpkins[i].size * Size);
        }

        // A tree pumpkin's break this big (metres) here, its shards flying away from the boss's middle, with a puff.
        private void BurstAt(Vector3 at, float size)
        {
            if (pumpkinBreak == null) return;
            var bits = Instantiate(pumpkinBreak, at, Random.rotation, transform.parent);
            bits.transform.localScale *= size / BreakSize;
            var middle = head != null ? Vector3.Lerp(transform.position, head.position, 0.5f) : transform.position + Vector3.up * size;
            foreach (var shard in bits.GetComponentsInChildren<Rigidbody>())
            {
                if (shard.isKinematic) continue;
                var away = shard.worldCenterOfMass - middle;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.onUnitSphere;
                shard.linearVelocity = (away + Vector3.up * 0.5f).normalized * shardSpeed * Random.Range(0.6f, 1.2f);
                shard.angularVelocity = Random.insideUnitSphere * 12f;
            }
            if (smoke != null) EffectPool.Play(smoke, at, Mathf.Max(1f, size * 1.5f));
        }

        // Each pumpkin that has burst is gone from its body (its bone squashed to nothing after the Animator posed it).
        private void HideBurstPumpkins()
        {
            if (pumpkinBurst == null) return;
            for (int i = 0; i < pumpkinBurst.Length; i++)
            {
                if (pumpkinBurst[i] && pumpkinBones[i] != null) pumpkinBones[i].localScale = Vector3.one * 0.0001f;
            }
        }

        // No Pumpkin Break: its head bursts into Burst Into (the old way).
        private void BurstHead()
        {
            var at = head != null ? head.position : transform.position;
            // Killed while half in the ground, its head is partly under the floor: it bursts just above it.
            at.y = Mathf.Max(at.y, transform.position.y + 0.8f);
            if (burstInto != null)
            {
                var bits = Instantiate(burstInto, at, transform.rotation, transform.parent);
                bits.transform.localScale *= Mathf.Max(1f, Size * 1.2f);
            }
            if (smoke != null) EffectPool.Play(smoke, at);
        }

        // It's gone: a last blast, a laugh, and GAMESTAGES goes on (its BOSS flag, Win).
        private void Gone()
        {
            GroundBlast(transform.position, PumpkinBossFx.Blast.Huge);
            PlaySound(laughs);
            SendShake("ShakeBig");
            model.gameObject.SetActive(false);
            var stages = FsmVariables.GlobalVariables.FindFsmGameObject("GAMESTAGES");
            if (stages != null && stages.Value != null)
            {
                foreach (var fsm in stages.Value.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "GAMESTAGES") continue;
                    var flag = fsm.FsmVariables.FindFsmBool(gameStagesBool);
                    if (flag != null) flag.Value = true;
                    if (!string.IsNullOrEmpty(gameStagesEvent)) fsm.SendEvent(gameStagesEvent);
                }
            }
            Destroy(gameObject, 1.6f);
        }

        // ---------- laughing and breaking the room ----------

        // Non-stop: the next laugh starts Laugh Gap after the last one ends, until it dies.
        private void KeepLaughing()
        {
            if (phase == Phase.Dying || laughSpeaker == null) return;
            if (!laughSpeaker.isPlaying && Time.time >= nextLaugh) LaughNow();
        }

        // A new laugh right now (its big moments: appearing, waking, coming out of the ground).
        private void LaughNow()
        {
            if (laughSpeaker == null || laughs.Length == 0) return;
            int i = Random.Range(0, laughs.Length);
            if (laughs.Length > 1 && i == lastLaugh) i = (i + 1 + Random.Range(0, laughs.Length - 1)) % laughs.Length;
            lastLaugh = i;
            var clip = laughs[i];
            if (clip == null) return;
            laughSpeaker.clip = clip;
            laughSpeaker.volume = laughVolume;
            laughSpeaker.Play();
            nextLaugh = Time.time + clip.length + laughGap;
        }

        // A yelp or a roar: the laugh stops for it and comes back Laugh Gap after it ends (one mouth, one sound). Even
        // dying (its last yelp; nothing laughs after that until it bursts).
        private void Say(AudioClip clip)
        {
            if (clip == null) return;
            if (laughSpeaker == null)
            {
                PlaySound(clip);
                return;
            }
            laughSpeaker.Stop();
            laughSpeaker.clip = clip;
            laughSpeaker.volume = speaker != null ? speaker.volume : 1f;
            laughSpeaker.Play();
            nextLaugh = Time.time + clip.length + laughGap;
        }

        /// <summary>
        /// Stunned: standing there dizzy (too much damage too fast, or spun dizzy by its own cyclone). Candy Vacuum doesn't
        /// eat while it is (user, 2026-10-29).
        /// </summary>
        public bool IsDizzy => act == Act.Dizzy;

        /// <summary>
        /// Its mouth is busy (munching candy: Candy Vacuum calls this): the laugh stops for this sound and comes back
        /// Laugh Gap after it ends, so the two never play on top of each other. Still munching, a second candy adds no
        /// second sound on top. False when it has no laugh speaker (then the caller plays the sound itself).
        /// </summary>
        public bool SayInsteadOfLaugh(AudioClip clip, float volume)
        {
            if (laughSpeaker == null || clip == null) return false;
            // Dying: nothing over its last laugh.
            if (phase == Phase.Dying) return true;
            if (laughSpeaker.isPlaying && laughSpeaker.clip == clip) return true;
            laughSpeaker.Stop();
            laughSpeaker.clip = clip;
            laughSpeaker.volume = volume;
            laughSpeaker.Play();
            nextLaugh = Time.time + clip.length + laughGap;
            return true;
        }

        // The walls of whichever room is standing fall away (its own fall-away clip), then the room is removed.
        private void BreakRooms()
        {
            bool broke = false;
            foreach (var name in roomVariables)
            {
                var room = FsmVariables.GlobalVariables.FindFsmGameObject(name)?.Value;
                if (room == null || !room.activeInHierarchy) continue;
                var walls = room.GetComponentInChildren<Animator>();
                var fallAway = walls != null ? FallAwayState(walls) : null;
                if (fallAway != null) walls.CrossFadeInFixedTime(fallAway, 0.1f, 0, 0f);
                Destroy(room, fallAway != null ? roomGoneAfter : 0.5f);
                broke = true;
            }
            if (broke) SendShake("ShakeBig");
        }

        // Which clip takes the room's walls down from where they are now; none when they're already down.
        private static string FallAwayState(Animator walls)
        {
            var now = walls.GetCurrentAnimatorStateInfo(0);
            foreach (var (from, to) in FallAway)
            {
                if (!now.IsName(from)) continue;
                return to != null && walls.HasState(0, Animator.StringToHash(to)) ? to : null;
            }
            return null;
        }

        // Room animator states (Bedroom_Base_Z, Livingroom_Base_Z) and the clip that takes those walls down.
        private static readonly (string from, string to)[] FallAway =
        {
            ("Bedroom_Mini_COME", "Bedroom_Mini_GO"), ("Bedroom_Mini_GO2", "Bedroom_Mini_GO"),
            ("Bedroom_COME", "Bedroom_GO"), ("Bedroom_GO2", "Bedroom_GO"), ("Bedroom_Base_Z_Setup", "Bedroom_GO"),
            ("COME", "LivingRoom_GO"), ("LivingRoom_GO2", "LivingRoom_GO"), ("Livingroom_Setup", "LivingRoom_GO"),
            ("COME2", "GO4"),
        };

        // ---------- sliding on its pumpkin ----------

        /// <summary>Where its mouth is now (its lips, at the jaw): Candy Vacuum flies candy there.</summary>
        internal Vector3 MouthPosition => MouthPoint();

        /// <summary>Its size: the model's scale (as a head and upper body, then grown in its true form).</summary>
        internal float ModelSize => Size;

        /// <summary>How far its giant pumpkin reaches out from its middle while that's out (true form), in metres; else 0.</summary>
        internal float BodyRadius => PumpkinOut ? pumpkinRadius * Size : 0f;

        // Its giant pumpkin is out of the floor: the true form on it, not broken, not sunk for an upper-body move.
        private bool PumpkinOut => slideOnPumpkin && pumpkinRadius > 0f && !buttBroken && phase == Phase.True &&
                                   buriedNow < UpperDepth * 0.5f;

        // How it really moves (from where it was last frame: the agent, the charge and the bounce all count), its lean, its
        // dust trail and its grinding. Every frame; outside the true form on its pumpkin they all ease back to nothing.
        private void UpdateSlide()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var at = transform.position;
            var moved = at - slideWasAt;
            moved.y = 0f;
            slideWasAt = at;
            var was = slideVelocity;
            // More than 2 m in a frame is a warp, not a slide.
            var now = slideMeasured && moved.sqrMagnitude < 4f ? moved / dt : slideVelocity;
            slideMeasured = true;
            slideVelocity = Vector3.Lerp(slideVelocity, now, 1f - Mathf.Exp(-dt / 0.06f));
            slideAccel = Vector3.Lerp(slideAccel, (slideVelocity - was) / dt, 1f - Mathf.Exp(-dt / 0.12f));

            // On the floor and sliding: not in the air (the bounce), not reeling dizzy, not dying.
            bool onFloor = Sliding && act != Act.JumpSlam && act != Act.Dizzy && hopStep == HopStep.None && hopLift < 0.05f;
            float speed = onFloor ? slideVelocity.magnitude : 0f;
            float share = speed / Mathf.Max(0.1f, trueSpeed);
            bool windingUp = act == Act.Charge && chargeStep == ChargeStep.WindUp;
            bool recovering = act == Act.Charge && chargeStep == ChargeStep.Recover;
            bool skidding = act == Act.Charge && chargeStep == ChargeStep.Skid;

            // It leans forward with its speed and towards whatever pushes it (speeding up, braking, turning: it carves round
            // a player who stepped aside), plus a wobble; rocks back and shivers winding up a charge; rocks side to side
            // catching its breath after one.
            float pitch = 0f, roll = 0f, jitter = 0f;
            if (onFloor)
            {
                var localV = transform.InverseTransformDirection(slideVelocity);
                var localA = transform.InverseTransformDirection(slideAccel);
                float push = Mathf.Max(0.5f, slideAcceleration);
                pitch = slideLeanForward * Mathf.Clamp(localV.z / Mathf.Max(0.1f, trueSpeed), -1f, 2f) +
                        slideLeanTurn * Mathf.Clamp(localA.z / push, -1.5f, 1.5f);
                roll = slideLeanTurn * Mathf.Clamp(localA.x / push, -1.5f, 1.5f);
                float wobble = slideWobble * Mathf.Clamp01(share);
                pitch += (Mathf.PerlinNoise(Time.time * 2.7f, 0.31f) - 0.5f) * 2f * wobble;
                roll += (Mathf.PerlinNoise(0.57f, Time.time * 2.3f) - 0.5f) * 2f * wobble;
                if (windingUp)
                {
                    float k = Mathf.Clamp01((Time.time - actStarted) / chargeWindUp);
                    pitch -= chargeCrouch * k;
                    jitter = Mathf.Sin(Time.time * 70f) * 1.2f * k;
                }
                if (recovering)
                {
                    float k = Mathf.Clamp01((Time.time - recoverStarted) / Mathf.Max(0.1f, chargeRecovery));
                    roll += Mathf.Sin((Time.time - recoverStarted) * 9f) * 5f * (1f - k);
                }
            }
            pitch = Mathf.Clamp(pitch, -slideLeanMost, slideLeanMost);
            roll = Mathf.Clamp(roll, -slideLeanMost, slideLeanMost);
            float follow = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, slideLeanSmooth));
            leanPitch = Mathf.Lerp(leanPitch, pitch, follow);
            leanRoll = Mathf.Lerp(leanRoll, roll, follow);
            leanJitter = jitter;
            // Pitch about its own right (forward lean), roll about its own forward (the top towards +x for a push to +x).
            lean = Quaternion.Euler(leanPitch + leanJitter, 0f, -leanRoll);

            // A light dust trail behind its base, more often the faster it goes (none standing, nor in the cyclone, which
            // churns its own); skidding at the floor's edge, more of it at its front.
            if (onFloor && fx != null && act != Act.Cyclone && speed > 1f && Time.time >= nextSlideDust)
            {
                nextSlideDust = Time.time + (skidding ? 0.1f : slideDustEvery / Mathf.Clamp(share, 0.3f, 1.5f));
                var back = -slideVelocity / Mathf.Max(0.01f, slideVelocity.magnitude);
                if (skidding) back = -back;
                var side = Vector3.Cross(Vector3.up, back) * Random.Range(-0.4f, 0.4f);
                float edge = (PumpkinOut ? pumpkinRadius * 0.85f : 0.35f) * Size;
                fx.Scrape(at + (back + side) * edge, Size * slideDustSize);
            }
            UpdateGrind(onFloor ? share : 0f, windingUp);
        }

        // Its grinding: louder and higher the faster it slides; revving up through a charge's wind-up; quiet standing,
        // dizzy, in the air or dying. The clip's silent tail is skipped as it loops.
        private void UpdateGrind(float share, bool windingUp)
        {
            if (grindSpeaker == null) return;
            float want = slideGrindVolume * Mathf.Clamp01(share);
            float high = Mathf.Clamp01(share / 1.8f);
            if (windingUp)
            {
                float k = Mathf.Clamp01((Time.time - actStarted) / chargeWindUp);
                want = slideGrindVolume * Mathf.Lerp(0.5f, 1f, k);
                high = k;
            }
            if (phase == Phase.Dying || act == Act.Dizzy) want = 0f;
            grindVolume = Mathf.MoveTowards(grindVolume, want, Time.deltaTime * 2f);
            if (grindVolume <= 0.001f)
            {
                if (grindSpeaker.isPlaying) grindSpeaker.Stop();
                return;
            }
            if (!grindSpeaker.isPlaying) grindSpeaker.Play();
            grindSpeaker.volume = grindVolume;
            grindSpeaker.pitch = Mathf.Lerp(slideGrindPitch.x, slideGrindPitch.y, high);
            var clip = grindSpeaker.clip;
            if (clip != null && slideGrindTail > 0f && clip.length > slideGrindTail + 0.2f &&
                grindSpeaker.time >= clip.length - slideGrindTail)
                grindSpeaker.time = 0.03f;
        }

        // The true form's body, every frame: on its pumpkin the agent speeds up and slows down slowly and doesn't turn it
        // (Update Fighting turns it), its radius is the pumpkin's (other enemies keep clear of it), and the pumpkin is kept
        // from hanging far over the floor's edge. On legs (or in its cyclone) the agent as it was built.
        private void UpdatePumpkinBody()
        {
            if (agent == null || !agent.enabled || phase != Phase.True) return;
            agent.acceleration = Sliding && act != Act.Cyclone ? slideAcceleration : legAcceleration;
            agent.updateRotation = !Sliding;
            float radius = PumpkinOut ? pumpkinRadius * Size : agentRadiusAsBuilt;
            if (!Mathf.Approximately(agent.radius, radius)) agent.radius = radius;
            KeepOffEdge();
            KeepOffBlocks();
            // Crushing what its pumpkin runs into (hops and the bounce crush where they land).
            if (crushOnPath && PumpkinOut && !Airborne && hopStep == HopStep.None)
            {
                float speed = act == Act.Charge ? Mathf.Max(chargeSpeedNow, slideVelocity.magnitude) : slideVelocity.magnitude;
                if (speed >= crushMinSpeed)
                {
                    var ahead = slideVelocity.sqrMagnitude > 0.01f ? slideVelocity.normalized : transform.forward;
                    CrushAround(transform.position + Vector3.up * (0.6f * BodyRadius) + ahead * (0.25f * BodyRadius), BodyRadius + crushReach);
                }
            }
        }

        /// <summary>In the air (a hop or the bounce): Bulldozer pushes nothing then.</summary>
        internal bool Airborne => hopLift > 0.05f;

        // Its fights have no walls (user, 2026-11-11): the floor just ends, where its NavMesh does (and round the Fall Edge
        // cut-outs). The NavMesh keeps only its middle Spider's 0.5 m in, so its pumpkin (1.8 m round its middle in the true
        // form) would hang far out over the edge: it's eased away from the nearest edge, at most Edge Ease Speed, until the
        // pumpkin's edge is about at the floor's. Where the floor is narrower than the pumpkin it can't (it's eased off one
        // edge towards the other). Not in the air (the bounce's flight).
        private const float EdgeEaseSpeed = 6f;

        private void KeepOffEdge()
        {
            if (!PumpkinOut || !agent.isOnNavMesh || (act == Act.JumpSlam && !actHit) || hopStep == HopStep.Air) return;
            float clear = pumpkinRadius * Size - bakedRadius;
            if (clear <= 0.05f) return;
            var at = agent.nextPosition;
            if (!NavMesh.FindClosestEdge(at, out var edge, agent.areaMask) || edge.distance >= clear) return;
            var away = at - edge.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = edge.normal;
                away.y = 0f;
            }
            if (away.sqrMagnitude < 0.0001f) return;
            var way = away.normalized;
            float push = Mathf.Min(clear - edge.distance, EdgeEaseSpeed * Time.deltaTime);
            // The floor narrower than the pumpkin (a gate's gap, a narrow arm): only to its middle, no shuffling edge to edge.
            if (FloorEndsAhead(at, way, 2f * clear, out float other)) push = Mathf.Min(push, Mathf.Max(0f, (other - edge.distance) * 0.5f));
            if (push > 0.0005f) agent.Move(way * push);
        }

        // The heaviest loose things (the rooftop's carved stone blocks, Push Weight's immovable) don't cut the NavMesh (Live
        // NavMesh skips loose bodies) and Bulldozer can't shove them, so its pumpkin would slide right through them (play
        // test 2026-11-11): it's eased off them like off the floor's edge, at most Edge Ease Speed. Not in the air.
        private void KeepOffBlocks()
        {
            if (!PumpkinOut || !agent.isOnNavMesh || hopStep == HopStep.Air || (act == Act.JumpSlam && !actHit)) return;
            float reach = BodyRadius;
            var middle = transform.position + Vector3.up * (0.6f * reach);
            int n = Physics.OverlapSphereNonAlloc(middle, reach, blockHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var push = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var c = blockHits[i];
                if (c == null || c.transform.IsChildOf(transform)) continue;
                var body = c.attachedRigidbody;
                if (body == null || !PushWeight.IsImmovable(body)) continue;
                // Closest Point needs a convex shape; a concave mesh is taken by its box.
                bool exact = !(c is MeshCollider mesh) || mesh.convex;
                var near = exact ? c.ClosestPoint(middle) : c.bounds.ClosestPoint(middle);
                var away = middle - near;
                away.y = 0f;
                float gap = away.magnitude;
                if (gap < 0.0001f)
                {
                    away = middle - c.bounds.center;
                    away.y = 0f;
                    gap = 0f;
                    if (away.sqrMagnitude < 0.0001f) continue;
                }
                float into = reach - gap;
                if (into > 0f) push += away.normalized * into;
            }
            if (push.sqrMagnitude < 0.000001f) return;
            float most = EdgeEaseSpeed * Time.deltaTime;
            agent.Move(Vector3.ClampMagnitude(push, most));
        }

        private static readonly Collider[] blockHits = new Collider[16];

        // Whether the floor (the NavMesh) ends within Length of From along Way, and how far. A NavMesh raycast, but a "hit"
        // the floor carries on just past (a vertex right on the line: a charge through 0,0,0 skidded 2.5 m in, play test
        // 2026-11-11) is stepped over and the ray goes on from there.
        private bool FloorEndsAhead(Vector3 from, Vector3 way, float length, out float distance)
        {
            distance = 0f;
            float gone = 0f;
            for (int i = 0; i < 4 && gone < length; i++)
            {
                if (!NavMesh.Raycast(from, from + way * (length - gone), out var hit, agent.areaMask)) return false;
                var past = hit.position + way * 0.3f;
                if (NavMesh.SamplePosition(past, out var on, 0.25f, agent.areaMask) && Flat(on.position - past) < 0.12f)
                {
                    gone += Flat(on.position - from);
                    from = on.position;
                    continue;
                }
                distance = gone + hit.distance;
                return true;
            }
            return false;
        }

        // ---------- butt hops ----------

        // It hops (rather than slides) at a far player: on its pumpkin, whole and out of the floor, between moves.
        private bool CanHop => Sliding && !buttBroken && hopFrom > 0f && hopLength > 0f && pumpkinRadius > 0f && !Sunk &&
                               BuriedNow <= 0.3f;

        // A hop at the player (user, 2026-11-11): its pumpkin squashes down (Hop Crouch), springs it up stretched on an arc
        // Hop Height high and up to Hop Length far, lands squashed with a little dust (a light shake if the player is near),
        // crushing what's there and hurting a player on or next to it once, and springs back (Hop Rest). Then Update
        // Fighting decides again: still far, the next hop; closer, it slides; or one of its moves. It holds its slide pose
        // throughout. It goes straight at the player, its pumpkin's front edge coming down on them (not its middle), over
        // whatever is in between (a low wall, a gap between gate pillars) when the landing is on the floor and the arc clears
        // everything solid (play test 2026-11-11: it couldn't follow a player out through the rooftop's gates); else as far
        // as a hop goes along its NavMesh path round what's in the way. False: no hop there (it slides).
        private bool StartHop(Vector3 target, float distance)
        {
            // A try that found no hop waits half a second before the next (the arc checks and path aren't free).
            if (Time.time < nextHopTry) return false;
            nextHopTry = Time.time + 0.5f;
            var at = transform.position;
            var way = target - at;
            way.y = 0f;
            if (way.sqrMagnitude < 0.0001f) return false;
            way.Normalize();
            float most = hopLength * Size;
            float straight = Mathf.Min(most, distance - BodyRadius * 0.9f);
            foreach (float share in HopTries)
            {
                if (straight * share >= 1f && HopLanding(at, at + way * (straight * share), out var to))
                {
                    BeginHop(at, to);
                    return true;
                }
            }
            if (!agent.isOnNavMesh) return false;
            hopPath ??= new NavMeshPath();
            if (!NavMesh.CalculatePath(agent.nextPosition, target, agent.areaMask, hopPath) || hopPath.corners.Length < 2) return false;
            var spot = AlongPath(hopPath.corners, Mathf.Min(most, PathLength(hopPath.corners) - BodyRadius * 0.9f));
            if (!HopLanding(at, spot, out var along)) return false;
            BeginHop(at, along);
            return true;
        }

        // The straight hop tried at its full length, then shorter.
        private static readonly float[] HopTries = { 1f, 0.7f };

        private void BeginHop(Vector3 from, Vector3 to)
        {
            nextHopTry = 0f;
            HardStop();
            hopFromAt = from;
            hopToAt = to;
            hopStep = HopStep.Crouch;
            hopStarted = Time.time;
            hopHit = false;
            stuckSince = -1f;
            PlayState(slideMove, false, slideBlend);
        }

        // A spot to land a hop from At aiming at Want: on the floor there (the NavMesh within a metre, about level with where
        // it sets off: never off the roof or down a floor), a metre or more away, and an arc there that clears everything
        // solid in between. Too near the floor's edge for its pumpkin is fine: it eases off the edge after landing.
        private bool HopLanding(Vector3 at, Vector3 want, out Vector3 to)
        {
            to = want;
            if (!NavMesh.SamplePosition(want, out var onMesh, 1f, agent.areaMask)) return false;
            if (Mathf.Abs(onMesh.position.y - at.y) > 1f || Flat(onMesh.position - at) < 1f) return false;
            to = onMesh.position;
            return ArcClear(at, to);
        }

        // The hop's arc from At to To clears everything solid: a thick line through the pumpkin's lower middle along the arc
        // (eight steps) touches nothing standing still (walls, pillars, props); loose things, enemies, the player and itself
        // don't count (it crushes or pushes them).
        private bool ArcClear(Vector3 at, Vector3 to)
        {
            // Most of the pumpkin's width (a thin line let its sides pass through blocks beside the arc).
            float r = Mathf.Clamp(0.75f * BodyRadius, 0.3f, 1.4f);
            float lift = r + 0.15f;
            var last = at + Vector3.up * lift;
            const int Steps = 8;
            for (int i = 1; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                var next = Vector3.Lerp(at, to, k) + Vector3.up * (lift + 4f * hopHeight * Size * k * (1f - k));
                var d = next - last;
                float len = d.magnitude;
                if (len > 0.001f)
                {
                    int n = Physics.SphereCastNonAlloc(last, r, d / len, arcHits, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < n; j++)
                    {
                        if (BlocksHop(arcHits[j].collider)) return false;
                    }
                }
                last = next;
            }
            return true;
        }

        private bool BlocksHop(Collider part)
        {
            if (part == null || part.transform.IsChildOf(transform) || Game.Weapons.Blast.IsPlayer(part)) return false;
            var body = part.attachedRigidbody;
            if (body == null) return true;
            // The heaviest loose things (the rooftop's carved stone blocks) never move: as solid as a wall (play test
            // 2026-11-11: a hop went straight through one).
            if (PushWeight.IsImmovable(body)) return true;
            return body.isKinematic && body.GetComponentInParent<EnemyHitDamage>() == null;
        }

        private static float PathLength(Vector3[] corners)
        {
            float length = 0f;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        // The point Length along a path.
        private static Vector3 AlongPath(Vector3[] corners, float length)
        {
            for (int i = 1; i < corners.Length; i++)
            {
                float piece = Vector3.Distance(corners[i - 1], corners[i]);
                if (piece >= length) return Vector3.Lerp(corners[i - 1], corners[i], length / Mathf.Max(0.001f, piece));
                length -= piece;
            }
            return corners[corners.Length - 1];
        }

        // Sliding but getting nowhere: its agent going (1.5 m/s or more) but it really moves less than a third of that, for
        // Stuck Hop After seconds.
        private bool SlideStuck()
        {
            float going = Flat(agent.velocity);
            if (going < 1.5f || slideVelocity.magnitude > going * 0.35f)
            {
                stuckSince = -1f;
                return false;
            }
            if (stuckSince < 0f) stuckSince = Time.time;
            return Time.time - stuckSince >= stuckHopAfter;
        }

        private void UpdateHop()
        {
            float t = Time.time - hopStarted;
            if (hopStep == HopStep.Crouch)
            {
                Face(hopToAt, 400f);
                squash = Mathf.Lerp(1f, hopSquash, Mathf.SmoothStep(0f, 1f, t / hopCrouch));
                if (t < hopCrouch) return;
                hopStep = HopStep.Air;
                // Springing off: a puff of dust where it sat.
                if (fx != null) fx.Scrape(hopFromAt, Size * slideDustSize);
            }
            if (hopStep == HopStep.Air)
            {
                float k = Mathf.Clamp01((t - hopCrouch) / hopAir);
                agent.Warp(Vector3.Lerp(hopFromAt, hopToAt, k));
                hopLift = 4f * hopHeight * Size * k * (1f - k);
                // Stretched most leaving and nearing the ground (fastest), round at the top.
                squash = 1f + (hopStretch - 1f) * Mathf.Abs(1f - 2f * k);
                if (k < 1f) return;
                hopLift = 0f;
                hopStep = HopStep.Land;
                HopLands();
            }
            // Landed: squashed flat, springing back with a wobble, a short pause.
            float u = t - hopCrouch - hopAir;
            squash = 1f - (1f - hopSquash) * Mathf.Exp(-u * 9f) * Mathf.Cos(u * 20f);
            if (u >= hopRest) hopStep = HopStep.None;
        }

        // Down it comes: dust (a little boom and a light shake only near the player), what's under it crushed, and a player
        // on or next to it hit once, the harder the closer (Hop Near Share right next to it, x1 at Hop Hit Reach).
        private void HopLands()
        {
            agent.Warp(hopToAt);
            float near = Flat(PlayerFloor() - transform.position);
            bool close = near < BodyRadius + 6f;
            GroundBlast(transform.position, close ? PumpkinBossFx.Blast.Medium : PumpkinBossFx.Blast.Small);
            if (close) SendShake("ShakeSmall");
            CrushAround(transform.position + Vector3.up * (0.6f * BodyRadius), BodyRadius + crushReach);
            if (!hopHit && near <= BodyRadius + hopHitReach && !PlayerFlying())
            {
                hopHit = true;
                float outside = hopHitReach > 0f ? Mathf.Clamp01((near - BodyRadius) / hopHitReach) : 0f;
                HitPlayer(hopHitEvent, hopLaunch, transform.position, Mathf.Lerp(hopNearShare, 1f, outside));
            }
        }

        // ---------- crushing ----------

        /// <summary>Whether it would crush this (Bulldozer leaves it be, for the crush).</summary>
        internal bool Crushes(Collider part) => crushOnPath && PumpkinOut && part != null && CrushKindOf(part) != Crushable.No;

        // What its pumpkin meets in this sphere is crushed (see Crush On Path), each thing at most once a second. Per call:
        // one overlap test (up to 64 colliders), and per collider a cached look-up of what it is plus a check it isn't in
        // the player's hands right now.
        private void CrushAround(Vector3 middle, float radius)
        {
            if (!crushOnPath || !PumpkinOut) return;
            int count = Physics.OverlapSphereNonAlloc(middle, radius, crushHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var part = crushHits[i];
                var kind = CrushKindOf(part);
                if (kind == Crushable.No) continue;
                var body = part.attachedRigidbody;
                if (body != null && body.TryGetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(out var grab) && grab.isSelected) continue;
                var enemy = kind == Crushable.HealthFsm ? null : part.GetComponentInParent<EnemyHitDamage>();
                Object thing = enemy != null ? (Object)enemy : body != null ? (Object)body : part;
                int id = thing.GetInstanceID();
                if (crushedAt.TryGetValue(id, out float when) && Time.time - when < 1f) continue;
                crushedAt[id] = Time.time;
                if (enemy != null)
                {
                    // Its own break or death, at once, from above (a bug flattened), no EXP.
                    enemy.Crush();
                    continue;
                }
                // A thing with a Health FSM of its own: a blast's worth on it, in full, not the player's.
                var go = body != null ? body.gameObject : part.gameObject;
                Game.Leveling.PlayerLevel.NoRewardFor(go);
                Game.Weapons.Blast.Damage(part, part.bounds.ClosestPoint(middle), CrushDamage, false);
            }
            if (crushedAt.Count > 256) crushedAt.Clear();
        }

        private Crushable CrushKindOf(Collider part)
        {
            if (crushKinds.TryGetValue(part, out var kind)) return kind;
            kind = JudgeCrush(part);
            if (crushKinds.Count > 512) crushKinds.Clear();
            crushKinds[part] = kind;
            return kind;
        }

        // What a collider is, for crushing (see Crush On Path).
        private Crushable JudgeCrush(Collider part)
        {
            if (part == null || part.transform.IsChildOf(transform) || Game.Weapons.Blast.IsPlayer(part)) return Crushable.No;
            var body = part.attachedRigidbody;
            var thing = body != null ? body.gameObject : part.gameObject;
            if (thing.name.StartsWith("Candy") || (vacuum != null && body != null && vacuum.IsCandy(body))) return Crushable.No;
            foreach (var word in crushKeepNames)
            {
                if (!string.IsNullOrEmpty(word) && thing.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return Crushable.No;
            }
            if (body != null && PushWeight.IsImmovable(body)) return Crushable.No;
            if (!crushOwnDrops && drops.Contains(thing)) return Crushable.No;
            var enemy = part.GetComponentInParent<EnemyHitDamage>();
            if (enemy != null)
            {
                // Bosses and giants are too big; other enemies than bugs (slimes, fat pumpkins) aren't squashed either.
                if (enemy.GetComponentInParent<PumpkinBossBrain>() != null || enemy.GetComponentInParent<GiantPumpkin>() != null) return Crushable.No;
                if (!enemy.IsFurniture)
                {
                    var bug = enemy.GetComponent<SpiderBrain>();
                    return bug != null && !bug.IsBoss && !SpiderBrain.IsAnomalyBug(enemy.gameObject) ? Crushable.Bug : Crushable.No;
                }
                // Furniture and props, unless floating or held still.
                return body != null && body.isKinematic ? Crushable.No : Crushable.Breakable;
            }
            if (body != null && body.isKinematic) return Crushable.No;
            for (var t = part.transform; t != null; t = t.parent)
            {
                if (Game.Weapons.Blast.HasHealth(t.gameObject, out var health, out _) && health != null) return Crushable.HealthFsm;
            }
            return Crushable.No;
        }

        // ---------- the slide charge ----------

        // It stops, faces the player and winds up (rocking back, shivering, grinding louder, dust round its base, a roar),
        // then slides straight at where they stood at the end of the wind-up (no aiming ahead, no turning) at Charge Speed,
        // until it has passed them by Charge Past Player (or Charge Max Distance / Seconds), brakes, and catches its breath
        // (Charge Recovery). Touching the player once on the way throws them (Charge Launch). The floor ending ahead makes it
        // skid to a stop; a solid prop in its way makes it bonk (Bonk On Props).
        private void StartCharge()
        {
            StandUp();
            chargeNext = false;
            // Until it's over (the end of the act, or whatever cuts it short, sets the cooldown).
            nextCharge = float.MaxValue;
            Begin(Act.Charge, slideStand, 60f, slideBlend);
            HardStop();
            chargeStep = ChargeStep.WindUp;
            chargeSpeedNow = 0f;
            Say(roarSound != null ? roarSound : attackSound);
        }

        // Returns false when the charge started another move (it bonked): Update Act stops there.
        private bool UpdateCharge(float t)
        {
            var target = PlayerFloor();
            var at = transform.position;
            if (chargeStep == ChargeStep.WindUp)
            {
                HardStop();
                Face(target, 300f);
                // Dust kicked up round its base as it revs.
                if (fx != null && Time.time >= nextSlideDust)
                {
                    nextSlideDust = Time.time + 0.2f;
                    var round = Random.insideUnitCircle.normalized * Mathf.Max(BodyRadius, 0.6f * Size);
                    fx.Scrape(at + new Vector3(round.x, 0f, round.y), Size * slideDustSize);
                }
                if (t < chargeWindUp) return true;
                // Its way is fixed now: straight at where the player stands.
                var way = target - at;
                way.y = 0f;
                if (way.sqrMagnitude < 0.01f) way = transform.forward;
                way.y = 0f;
                chargeDir = way.sqrMagnitude > 0.0001f ? way.normalized : Vector3.forward;
                transform.rotation = Quaternion.LookRotation(chargeDir);
                chargeFrom = at;
                chargeSlideStarted = Time.time;
                chargeStep = ChargeStep.Slide;
                PlayState(slideMove, false, slideBlend);
                PlaySound(attackSound);
                return true;
            }
            if (chargeStep == ChargeStep.Recover)
            {
                Face(target, 90f);
                if (Time.time >= recoverStarted + chargeRecovery) actUntil = Time.time;
                return true;
            }
            // Sliding, braking or skidding: it can't turn.
            transform.rotation = Quaternion.LookRotation(chargeDir);
            float dt = Time.deltaTime;
            if (chargeStep == ChargeStep.Slide)
            {
                chargeSpeedNow = Mathf.MoveTowards(chargeSpeedNow, chargeSpeed, chargeSpeed / 0.2f * dt);
                bool past = Vector3.Dot(target - at, chargeDir) < -chargePastPlayer * Size;
                if (past || Flat(at - chargeFrom) >= chargeMaxDistance || Time.time - chargeSlideStarted >= chargeMaxSeconds)
                    chargeStep = ChargeStep.Brake;
            }
            else chargeSpeedNow = Mathf.MoveTowards(chargeSpeedNow, 0f, (chargeStep == ChargeStep.Skid ? chargeSkid : chargeBrake) * dt);
            float step = chargeSpeedNow * dt;
            // A solid prop in its way: it bonks into it.
            if (bonkOnProps && chargeSpeedNow >= bonkMinSpeed && PropAhead(step))
            {
                Bonk();
                return false;
            }
            // The floor ends ahead, within what it needs to skid to a stop (plus its pumpkin): it skids, and never past the
            // point where its pumpkin's edge is at the floor's edge.
            if (step > 0f && agent.isOnNavMesh)
            {
                float clear = PumpkinOut ? Mathf.Max(0f, pumpkinRadius * Size - bakedRadius) : 0f;
                float skidRoom = chargeSpeedNow * chargeSpeedNow / (2f * chargeSkid);
                var from = agent.nextPosition;
                if (FloorEndsAhead(from, chargeDir, skidRoom + clear + step + 0.05f, out float edgeAt))
                {
                    chargeStep = ChargeStep.Skid;
                    float room = Mathf.Max(0f, edgeAt - clear);
                    if (room < step)
                    {
                        step = room;
                        // Right at it: stopped.
                        if (room <= 0.001f) chargeSpeedNow = 0f;
                    }
                }
            }
            if (step > 0f) agent.Move(chargeDir * step);
            at = transform.position;
            // Touching the player once on the way: a big throw, on along its way.
            if (!actHit && chargeSpeedNow >= bonkMinSpeed && PlayerNear(at + Vector3.up * Size, Mathf.Max(chargeHitRadius * Size, BodyRadius)))
            {
                actHit = true;
                if (fx != null) fx.Hit(PlayerChest(), at + Vector3.up * (0.8f * Size), Size);
                HitPlayer(chargeLaunch, chargeHitsHeavy, at - chargeDir * (2f * Size));
            }
            // Stopped: it catches its breath (no stars, no stun).
            if (chargeStep != ChargeStep.Slide && chargeSpeedNow <= 0.05f)
            {
                chargeSpeedNow = 0f;
                chargeStep = ChargeStep.Recover;
                recoverStarted = Time.time;
                PlayState(HasState(chargeRecoverState) ? chargeRecoverState : slideStand, true, 0.25f);
                if (chargeRecovery <= 0f) actUntil = Time.time;
            }
            return true;
        }

        // A solid thing right in front of it at about its chest's height that doesn't move and can be seen (a prop: the
        // PlayGround's cauldron, a tree, the giant skull), within its body's reach plus this step. Not a trigger, nothing
        // loose (a dropped pumpkin, candy), not an enemy, not the player or itself, not a slope.
        private bool PropAhead(float step)
        {
            float body = Mathf.Max(BodyRadius, 0.5f * Size);
            var from = transform.position + Vector3.up * (0.5f * Size);
            int n = Physics.SphereCastNonAlloc(from, 0.3f, chargeDir, propHits, body + step, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hit = propHits[i];
                var c = hit.collider;
                if (c == null || hit.distance <= 0f || c.transform.IsChildOf(transform)) continue;
                if (Mathf.Abs(hit.normal.y) > 0.5f) continue;
                var body2 = c.attachedRigidbody;
                // Loose things don't stop it, except the heaviest, which never move (the rooftop's stone blocks).
                if (body2 != null && !body2.isKinematic && !PushWeight.IsImmovable(body2)) continue;
                if (c.GetComponentInParent<XROrigin>() != null || c.GetComponentInParent<EnemyHitDamage>() != null) continue;
                var look = c.GetComponentInParent<Renderer>();
                if (look == null) look = c.GetComponentInChildren<Renderer>();
                if (look != null && look.enabled && look.bounds.Intersects(c.bounds)) return true;
            }
            return false;
        }

        // It slid hard into a prop: a blow's blast at its front, a shake, it stops dead and reels: dizzy, or within its Stun
        // Guard only a stagger (Try Stun).
        private void Bonk()
        {
            HardStop();
            chargeSpeedNow = 0f;
            var middle = transform.position + Vector3.up * (0.6f * Size);
            if (fx != null) fx.Hit(middle + chargeDir * Mathf.Max(BodyRadius, 0.5f * Size), middle, Size);
            SendShake("ShakeMed");
            TryStun(Stun.Bonk, BeingHit, bonkDizzySeconds);
        }

        // ---------- the pumpkin butt ----------

        // Every hit on it that would hurt (before its Hit Zone takes its share) counts for punch spam (Try Answer Spam): punches
        // on its pumpkin too, though most of them take nothing off its health yet. And the player's hits on its pumpkin
        // (true form) crack it by their full damage (its Hit Zone takes only a fifth off its health; that doesn't count
        // here). Butt Break Share of its health in such hits and it breaks.
        private void OnPartHit(EnemyHitDamage enemy, Collider part, HitResult full)
        {
            if (enemy == null || enemy.gameObject != gameObject || phase == Phase.Dying) return;
            // A punch and its own blast (Explosive Fist) land together: one hit.
            if (phase != Phase.Head && (recentHits.Count == 0 || Time.time - recentHits[recentHits.Count - 1] > spamMergeSeconds))
                recentHits.Add(Time.time);
            if (buttCollider == null || part != buttCollider) return;
            if (buttBroken || phase != Phase.True || !enemy.LastHitByPlayer || full.Damage <= 0f) return;
            buttDamage += full.Damage;
            float needed = Mathf.Max(1f, buttBreakShare * maxHealth);
            ShowCracks(Mathf.Clamp01(buttDamage / needed));
            if (buttDamage >= needed) BreakButt();
        }

        /// <summary>How cracked its pumpkin butt is: 0 whole, 1 broken (for tests and the HUD).</summary>
        internal float ButtCracked => buttBroken ? 1f : Mathf.Clamp01(buttDamage / Mathf.Max(1f, buttBreakShare * maxHealth));

        private void ShowCracks(float amount)
        {
            if (!PrepareButtLook()) return;
            crackMade.SetFloat(CrackAmountId, amount);
        }

        // The pumpkin's look, set up once: the renderer, the pumpkin slot's own material, and the crack overlay drawn over it:
        // an extra material past the renderer's last one draws its last part (submesh) again, so with the pumpkin last it
        // goes there. Pumpkin Boss Slide Setup puts the overlay on the prefab already (a build then keeps the mesh's rest
        // positions the cracks are drawn from); either way this boss gets its own copy. With the pumpkin not last, a second
        // skinned renderer on the same mesh and bones draws only its part (one more skinning). One part only: nothing.
        private bool PrepareButtLook()
        {
            if (buttLookReady) return true;
            if (buttLookFailed || buttCrackMaterial == null || buttMaterialSlot < 0) return false;
            buttLookFailed = true;
            buttSkin = FindButtSkin();
            var mesh = buttSkin != null ? buttSkin.sharedMesh : null;
            int parts = mesh != null ? mesh.subMeshCount : 0;
            if (parts < 2 || buttMaterialSlot >= parts)
            {
                Debug.LogWarning($"[Pumpkin Boss] Butt Material Slot is {buttMaterialSlot} but the model has {parts} material " +
                                 "slot(s): no cracks drawn, nothing hidden. Give the giant pumpkin its own material, last in the list.", this);
                return false;
            }
            var mats = buttSkin.sharedMaterials;
            if (buttMaterialSlot >= mats.Length) return false;
            buttOwnMaterial = mats[buttMaterialSlot];
            var overlay = mats.Length > parts && mats[parts] != null ? mats[parts] : buttCrackMaterial;
            crackMade = new Material(overlay) { name = overlay.name + " (boss)" };
            crackMade.SetFloat(CrackAmountId, 0f);
            if (buttMaterialSlot == parts - 1)
            {
                var more = new Material[parts + 1];
                for (int i = 0; i < parts; i++) more[i] = mats[i];
                more[parts] = crackMade;
                buttSkin.sharedMaterials = more;
                crackSlot = parts;
            }
            else
            {
                var copy = new GameObject("Butt_Cracks").transform;
                copy.SetParent(buttSkin.transform.parent, false);
                copy.SetLocalPositionAndRotation(buttSkin.transform.localPosition, buttSkin.transform.localRotation);
                copy.localScale = buttSkin.transform.localScale;
                crackCopy = copy.gameObject.AddComponent<SkinnedMeshRenderer>();
                crackCopy.sharedMesh = mesh;
                crackCopy.bones = buttSkin.bones;
                crackCopy.rootBone = buttSkin.rootBone;
                crackCopy.localBounds = buttSkin.localBounds;
                crackCopy.updateWhenOffscreen = buttSkin.updateWhenOffscreen;
                crackCopy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                crackCopy.receiveShadows = false;
                var only = new Material[parts];
                for (int i = 0; i < parts; i++) only[i] = i == buttMaterialSlot ? crackMade : Hidden;
                crackCopy.sharedMaterials = only;
            }
            buttLookFailed = false;
            buttLookReady = true;
            return true;
        }

        // The model's skinned mesh (not the crack copy).
        private SkinnedMeshRenderer FindButtSkin()
        {
            if (model == null) return null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin != crackCopy && skin.sharedMesh != null) return skin;
            }
            return null;
        }

        // The crack shader with Collapse on: draws nothing (a part of the mesh hidden without touching its bones).
        private Material Hidden
        {
            get
            {
                if (hiddenMade == null && buttCrackMaterial != null)
                {
                    hiddenMade = new Material(buttCrackMaterial) { name = "Boss_Butt_Hidden (boss)" };
                    hiddenMade.SetFloat(CollapseId, 1f);
                    hiddenMade.SetFloat(CrackAmountId, 0f);
                }
                return hiddenMade;
            }
        }

        // The giant pumpkin shows only while it's out of the floor (user, 2026-11-11: "hide the pumpkin in phase 2", and
        // "there too": the true form sunk to its upper body's depth). As a head, rising and as its upper body it's hidden; it
        // bursts out of the ground with its true form (the Transforming eruption at 1.2 s); sunk for an upper-body move it
        // goes once it's Butt Hide Share of the way down and comes back as it climbs past that again, inside the ground burst
        // and dust of the sinking and the climbing. Broken, it's gone for good. The cracks keep their amount meanwhile, and its
        // collider is off while it's hidden.
        private void UpdateButtShown()
        {
            if (!slideOnPumpkin || buttBroken) return;
            bool show = phase switch
            {
                Phase.Head or Phase.Rising or Phase.Upper => false,
                Phase.Transforming => (marks & MarkEmerge) != 0,
                _ => buriedNow < UpperDepth * ButtHideShare,
            };
            if (buttShowSet && show == buttShownNow) return;
            buttShowSet = true;
            ShowButt(show);
        }

        // The pumpkin shown (its own material, the cracks over it) or hidden (the hiding material on both), its collider
        // on or off with it.
        private void ShowButt(bool show)
        {
            buttShownNow = show;
            if (buttCollider != null) buttCollider.enabled = show && !buttBroken;
            if (!PrepareButtLook()) return;
            var mats = buttSkin.sharedMaterials;
            if (buttMaterialSlot < mats.Length) mats[buttMaterialSlot] = show ? buttOwnMaterial : Hidden;
            if (crackSlot >= 0 && crackSlot < mats.Length) mats[crackSlot] = show ? crackMade : Hidden;
            buttSkin.sharedMaterials = mats;
            if (crackCopy != null) crackCopy.enabled = show;
        }

        // Cracked through: the pumpkin bursts into a big tree-pumpkin break, the ground blasts, the screen shakes, the
        // pumpkin is gone (its slot hidden, its collider off), and the boss drops into the floor to its upper body's depth,
        // dizzy for Break Dizzy Seconds (Try Stun's one exception; its Stun Guard starts after). From then on it fights from
        // the floor for good: no climbing out, no bounce, its upper body's speed; it never transforms again (it stays in its
        // true form) and dies where it is.
        private void BreakButt()
        {
            buttBroken = true;
            hopStep = HopStep.None;
            var bounds = buttCollider.bounds;
            BurstAt(bounds.center, Mathf.Max(bounds.size.x, bounds.size.z));
            GroundBlast(transform.position, PumpkinBossFx.Blast.Huge);
            SendShake("ShakeBig");
            ShowButt(false);
            // Whatever it was doing stops (a bounce in the air drops, a charge stops dead, a combo is off).
            if (act == Act.Vomit) EndVomit(true);
            throwsLeft = 0;
            waitingForPumpkins = false;
            HardStop();
            agent.speed = FormSpeed;
            targetBuried = UpperDepth;
            quickBury = true;
            // The killing blow broke it: it dies (Track Health), no dizzy spell first.
            if (health != null && health.Value <= 0f) return;
            TryStun(Stun.ButtBreak, BeingHit, breakDizzySeconds);
        }

        // ---------- helpers ----------

        private void UpdateModel()
        {
            if (model == null) return;
            scaleNow = Mathf.MoveTowards(scaleNow, targetScale, Time.deltaTime * 2.5f);
            buriedNow = Mathf.MoveTowards(buriedNow, targetBuried, Time.deltaTime * (phase == Phase.Head ? 2.5f : quickBury ? 3f : 0.9f));
            if (quickBury && Mathf.Approximately(buriedNow, targetBuried) && targetBuried <= 0f) quickBury = false;
            // Out of the bounce or a hop (it landed, or something cut it short): back on the floor, its shape back.
            if (act != Act.JumpSlam && hopStep == HopStep.None)
            {
                hopLift = Mathf.MoveTowards(hopLift, 0f, Time.deltaTime * 12f);
                squash = Mathf.MoveTowards(squash, 1f, Time.deltaTime * 4f);
            }
            PoseModel();
        }

        // The model's pose from its parts, each on top of the others: its size, how deep it's buried (model units), how high
        // it hops (metres), and its lean, which tilts it round the middle of its base on the floor (round its bottom while
        // it hops), wherever it's buried to. (The bounce's and the hops' squash is the pumpkin's own: Squash Butt.)
        private void PoseModel()
        {
            model.localScale = Vector3.one * scaleNow;
            var down = new Vector3(0f, -buriedNow * scaleNow, 0f);
            model.localPosition = new Vector3(modelRestPosition.x, hopLift, modelRestPosition.z) + lean * down;
            model.localRotation = lean * modelRestRotation;
        }

        // The upper body's depth in the floor (model units): the true form drops to it for its upper-body moves.
        private float UpperDepth => buried.Length > 0 ? buried[buried.Length - 1] : 0f;

        // How deep in the floor the model is right now (model units).
        private float BuriedNow => model != null ? buriedNow : 0f;

        // True form, dropped (or dropping) into the floor.
        private bool Sunk => phase == Phase.True && targetBuried > 0f;

        // True form: its upper-body clips (punches, the two-arm slam, the clap, the cyclone, reeling) fold its legs up
        // into its body. Standing, it would hang in the air, so first it drops back into the floor to its upper body's
        // depth, the ground bursting round it. Returns the blend for the move's clip: slower while it drops (so the legs
        // fold up as it goes down), the usual one otherwise.
        private float SinkDown(PumpkinBossFx.Blast blast)
        {
            if (phase != Phase.True || targetBuried >= UpperDepth || !sinkForUpperMoves) return 0.15f;
            targetBuried = UpperDepth;
            quickBury = true;
            // (No second blast when it was only half way out.)
            if (BuriedNow < UpperDepth * 0.5f) GroundBlast(transform.position, blast);
            return 0.3f;
        }

        // Running, throwing, vomiting and jumping take its legs (or its pumpkin): it climbs back out of the floor (a puff of
        // dust). Not once its pumpkin is broken: it fights from the floor for good.
        private void StandUp(bool dust = true)
        {
            if (phase != Phase.True || targetBuried <= 0f || buttBroken) return;
            targetBuried = 0f;
            quickBury = true;
            if (dust && BuriedNow > UpperDepth * 0.5f) GroundBlast(transform.position, PumpkinBossFx.Blast.Small);
        }

        private void PlayState(string state, bool restart = false, float fade = 0.15f, float clipFrom = 0f)
        {
            if (animator == null || (!restart && playing == state)) return;
            playing = state;
            animator.CrossFadeInFixedTime(state, fade, 0, clipFrom);
            if (phase == Phase.Head && state == GetHit) actUntil = Time.time + 0.65f;
        }

        // It stops where it is. Sliding on its pumpkin it doesn't stop dead: the agent brakes it at Slide Acceleration, so
        // it glides to a halt (momentum).
        private void Stop()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            if (!Sliding) agent.velocity = Vector3.zero;
        }

        // It stops dead, sliding or not (a bounce, a charge, a bonk, a vomit, dying).
        private void HardStop()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
        }

        private void Face(Vector3 point, float degreesPerSecond)
        {
            var flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            var want = Quaternion.LookRotation(flat);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degreesPerSecond * Time.deltaTime);
        }

        private Vector3 PlayerFloor()
        {
            var headPos = player.Camera.transform.position;
            return new Vector3(headPos.x, player.transform.position.y, headPos.z);
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        private bool FindPlayer()
        {
            if (player != null && player.Camera != null) return true;
            player = FindAnyObjectByType<XROrigin>();
            return player != null && player.Camera != null;
        }

        // The little puff at its head (never under the floor).
        private void Puff()
        {
            if (smoke == null) return;
            var at = head != null ? head.position : transform.position;
            at.y = Mathf.Max(at.y, transform.position.y + 0.3f);
            EffectPool.Play(smoke, at);
        }

        // A blast on the floor right under this point, as hard as the move and as big as the boss (or this size).
        private void GroundBlast(Vector3 at, PumpkinBossFx.Blast strength, float size = 0f)
        {
            if (fx == null) return;
            at.y = transform.position.y;
            fx.Ground(at, strength, size > 0f ? size : Size);
        }

        // The middle of a fist: its hit sphere (it grows and flies out with the fist), else the hand.
        private Vector3 FistPoint(Transform hand)
        {
            if (hand == null) return transform.position;
            return hand.TryGetComponent(out SphereCollider fist) ? fist.bounds.center : hand.position;
        }

        private Vector3 PlayerChest() => player.Camera.transform.position + Vector3.down * 0.35f;

        // True the first time it's asked for this moment of the move (or rising, or transform).
        private bool Once(int mark)
        {
            if ((marks & mark) != 0) return false;
            marks |= mark;
            return true;
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null || speaker == null) return;
            speaker.PlayOneShot(clip);
        }

        private void PlaySound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            PlaySound(clips[Random.Range(0, clips.Length)]);
        }

        private static void SendShake(string eventName)
        {
            var cam = FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }
    }
}
