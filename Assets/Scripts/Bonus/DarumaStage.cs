using System.Collections;
using System.Collections.Generic;
using Game.Enemies;
using Game.Locomotion;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Game.Bonus
{
    /// <summary>
    /// Special Stage Type 3 (user, 2026-11-06), a "red light, green light" game: a narrow bridge with no way back, the
    /// Giant Slime at the far end. With its back to the player it sings a short song (its notes one by one, so it can sing
    /// at any speed: steady while they're far, changing speed in the middle, slow then suddenly very fast near it); when
    /// the song ends it whips round and looks. Moving while it looks (walking, or a big head or hand move; a short grace
    /// right after it turns) and it's caught: its mouth opens and a stream of bomber slimes flies out, and the first one
    /// to splat on the face throws the player off the bridge (if none does, it pushes them anyway). A fall puts them back
    /// at the start with the usual fall damage, and the clock keeps running (60 s). Touch the slime in time: a big
    /// firework, EXP in gold orbs and a fountain of candies, a few seconds to grab them. No teleport on the bridge. The
    /// PlayGround's own creatures and music wait meanwhile.
    /// The PlayGround's finale (user, 2026-11-07): made by Special Stage 3 Trigger (on the cauldron) when the mystery pot
    /// breaks. A win leaves the Green Seed (The_Seed) on the slime's platform; once it's eaten (or after 20 s) GAMESTAGES
    /// ends the PlayGround, and the player, in the black, is put on the rooftop near the Lid, facing it. Time running out
    /// is the end of the run: no lucky coins, a death and a new run. A pause (the menu, a notice, the coins) is like
    /// being knocked down: nothing counts, and a short grace after it.
    /// Tools > 429 Game > Bonus > Set Up Special Stage 3 builds it (Prefabs/Bonus/Special_Stage_3).
    /// </summary>
    public class DarumaStage : MonoBehaviour
    {
        [Header("Places")]
        [Tooltip("Where the player starts (and goes back to after a fall): the floor, facing the slime (+Z).")]
        [SerializeField] private Transform startSpot;
        [Tooltip("The Giant Slime (its Giant Slime component), standing at the far end.")]
        [SerializeField] private GiantSlime host;
        [Tooltip("Where the bridge begins and ends (for how close the player is: the song gets trickier).")]
        [SerializeField] private Transform pathStart;
        [SerializeField] private Transform pathEnd;

        [Header("Time")]
        [SerializeField] private float seconds = 60f;
        [Tooltip("Seconds after arriving before the first song (the sign is read).")]
        [SerializeField] private float introSeconds = 4f;

        [Header("Song")]
        [Tooltip("The song, note by note (Audio/SFX/Special_Stage_3/song_01..09).")]
        [SerializeField] private AudioClip[] notes = System.Array.Empty<AudioClip>();
        [Tooltip("Seconds a note lasts at normal speed (the last one 1.6 times).")]
        [SerializeField] private float noteSeconds = 0.42f;
        [Range(0f, 1f)]
        [SerializeField] private float songVolume = 1f;
        [Tooltip("The whole song is never longer than this, in seconds (user, 2026-11-06: \"a short song <10 seconds and " +
                 "can be fast and slow randomly while being played\").")]
        [Min(1f)]
        [SerializeField] private float maxSongSeconds = 9.5f;
        [Tooltip("Its speeds: slow stretches go this slow (x to y times normal), fast ones this fast (x to y; the top grows " +
                 "towards Fast Near as the player gets close).")]
        [SerializeField] private Vector2 slowTempo = new(0.45f, 0.8f);
        [SerializeField] private Vector2 fastTempo = new(1.4f, 2.6f);
        [Min(1f)]
        [SerializeField] private float fastNear = 3.5f;
        [Tooltip("How much a note's pitch follows its speed (0 = never, 1 = fully: twice as fast, an octave up).")]
        [Range(0f, 1f)]
        [SerializeField] private float pitchFollowsTempo = 0.25f;
        [Tooltip("Shown on it while it sings (the music notes).")]
        [SerializeField] private GameObject singEffect;

        [Header("Looking")]
        [SerializeField] private AudioClip turnSound;
        [SerializeField] private AudioClip lookSound;
        [Tooltip("How fast it turns round, degrees a second.")]
        [SerializeField] private float turnSpeed = 900f;
        [Tooltip("Seconds it looks (random between), and near the end.")]
        [SerializeField] private Vector2 lookSeconds = new(1.5f, 3f);
        [SerializeField] private Vector2 lookSecondsNear = new(2f, 3.5f);
        [Tooltip("Seconds after it has turned before moving counts (to stop).")]
        [SerializeField] private float grace = 0.35f;
        [Tooltip("Moving counts: walking further than this (metres), the head moving further than this, or a hand faster than this (m/s).")]
        [SerializeField] private float walkLimit = 0.08f;
        [SerializeField] private float headLimit = 0.25f;
        [SerializeField] private float handSpeedLimit = 1.6f;
        [Tooltip("On while it looks (red).")]
        [SerializeField] private Light lookLight;

        [Header("Caught")]
        [SerializeField] private AudioClip caughtSound;
        [Tooltip("The bomber slimes it lets out (Daruma_Bomber: fast).")]
        [SerializeField] private SlimeBomber bomber;
        [Min(1)]
        [SerializeField] private int bombers = 14;
        [Tooltip("Seconds between two of them.")]
        [SerializeField] private float bomberEvery = 0.12f;
        [SerializeField] private Color[] bomberColours = { new(0.5f, 1f, 0.85f), new(1f, 0.6f, 0.9f), new(0.8f, 0.65f, 1f), new(1f, 0.85f, 0.4f) };
        [Tooltip("How far and high a splat throws the player (over the bridge's edge).")]
        [SerializeField] private float pushDistance = 3f;
        [SerializeField] private float pushHeight = 0.9f;
        [Tooltip("Caught this long without falling: it pushes them itself (then every 1.5 s).")]
        [SerializeField] private float forceAfter = 3.5f;

        [Header("Win and lose")]
        [Tooltip("Touching it: a hand or the head this close to its skin (metres).")]
        [SerializeField] private float touchReach = 0.2f;
        [SerializeField] private int exp = 200;
        [SerializeField] private int candies = 30;
        [SerializeField] private GameObject[] candyPrefabs = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject winEffect;
        [SerializeField] private AudioClip winSound;
        [SerializeField] private AudioClip laughSound;
        [SerializeField] private AudioClip tickSound;
        [Tooltip("Seconds to grab the candies before the seed shows.")]
        [SerializeField] private float collectSeconds = 15f;

        [Header("After a win: the seed and the rooftop")]
        [Tooltip("Not eaten after this many seconds: it's eaten for them (as if touched).")]
        [Min(1f)]
        [SerializeField] private float seedSeconds = 20f;
        [Tooltip("The seed shows this far to the player's side, kept this close to the slime's centre (its platform is " +
                 "7 x 7 m), this high over the floor (metres).")]
        [SerializeField] private float seedBeside = 1f;
        [SerializeField] private float seedArea = 3f;
        [SerializeField] private float seedHeight = 0.5f;
        [Tooltip("Seconds in the black waiting for the rooftop before the view comes back (with a warning; it keeps " +
                 "waiting on the stage, never back to the PlayGround).")]
        [Min(1f)]
        [SerializeField] private float roofWaitSeconds = 20f;
        [Tooltip("The player is put this far from the Lid (metres), facing it.")]
        [Min(0.5f)]
        [SerializeField] private float lidStandOff = 4f;

        [Header("Pause")]
        [Tooltip("Seconds after a pause or getting up before moving counts again.")]
        [Min(0f)]
        [SerializeField] private float resumeGrace = 1f;

        [Header("Host colour (a clear cue)")]
        [Tooltip("The host's skin while the stage runs: the flying slime's paint texture (Flying_Slime_Paint: white where it " +
                 "takes the colour, the stem, eyes and mouth as they are), so the colours below show clean. Empty: its own.")]
        [SerializeField] private Texture hostPaint;
        [Tooltip("Its colour while it sings (walk!), while it looks (freeze!), and once it's over.")]
        [SerializeField] private Color singColour = new(0.25f, 0.95f, 0.35f);
        [SerializeField] private Color lookColour = new(1f, 0.15f, 0.12f);
        [SerializeField] private Color restColour = new(1f, 0.8f, 0.3f);

        [Header("Signs and look")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("While the stage runs, the fog starts and ends this far (so the slime shows at the far end).")]
        [SerializeField] private Vector2 fog = new(14f, 48f);

        /// <summary>The stage being played (only one at a time).</summary>
        public static DarumaStage Running { get; private set; }

        /// <summary>Raised when it's over: won or not.</summary>
        public static event System.Action<DarumaStage, bool> Ended;

        /// <summary>The seed a win leaves on the slime's platform (Items/Chest/The_Seed; Special Stage 3 Trigger hands it
        /// over). Empty: a win sets GAMESTAGES The_Seed itself (no item, no heal).</summary>
        public GameObject SeedPrefab { get; set; }

        /// <summary>Over: the player back where they were, on the rooftop, or dying (Play tests read it).</summary>
        public bool Finished { get; private set; }
        public bool Won { get; private set; }
        public bool Caught { get; private set; }
        public bool Looking { get; private set; }
        public bool Singing { get; private set; }
        public int Falls { get; private set; }
        public float TimeLeft => Mathf.Max(0f, endsAt - Time.time);

        private XROrigin origin;
        private Transform head, leftHand, rightHand;
        private AudioSource voice;
        private Transform away;
        private float endsAt = float.MaxValue;
        private bool over, timing;
        private Vector3 returnSpot;
        private float returnYaw;
        private readonly List<GameObject> frozen = new();
        private readonly List<SlimeBomber> sent = new();
        private TeleportationProvider teleport;
        private GameObject teleportRay;
        private bool teleportWas, rayWas;
        private float fogStartWas, fogEndWas;
        private bool fogSet;
        private float caughtAt, pushedAt;
        private int lastTick = -1;
        // Where the player came from is known (they were taken in), and the host's colour override.
        private bool tookPlayer;
        private MaterialPropertyBlock hostBlock;
        private Renderer[] hostLooks;
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // The player's health (PlayerHealth FSM), for "knocked down".
        private HutongGames.PlayMaker.FsmFloat playerHealth;
        // Moving doesn't count until then (just back from a pause or from being down).
        private float excusedUntil;
        // GAMESTAGES' bool the seed sets, and its seed FSM's state and event.
        private const string SeedBool = "The_Seed";
        private const string SeedWaitState = "Rotate and Wait";
        private const string SeedEatenEvent = "next";

        /// <summary>
        /// Takes the player in (fade, the PlayGround waits, onto the bridge) and starts the game. Running is set at once;
        /// the player is taken after Start Delay seconds, and never while down or paused.
        /// </summary>
        public void Begin(float startDelay = 0f)
        {
            if (Running != null && Running != this) { Destroy(gameObject); return; }
            Running = this;
            StartCoroutine(Play(startDelay));
        }

        private void Awake()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0.5f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 8f;
            voice.maxDistance = 80f;
            if (host != null)
            {
                away = new GameObject("Host Looks Away").transform;
                away.SetParent(transform, false);
                away.position = host.transform.position + (host.transform.position - (startSpot != null ? startSpot.position : transform.position)).normalized * 6f;
            }
            if (lookLight != null) lookLight.enabled = false;
            if (singEffect != null) singEffect.SetActive(false);
            if (statusText != null) statusText.text = "";
        }

        private void OnEnable() => SlimeBomber.FaceHit += OnFaceHit;

        private void OnDisable()
        {
            SlimeBomber.FaceHit -= OnFaceHit;
            FallOutOfWorld.Fell -= OnFell;
        }

        private void OnDestroy()
        {
            // Cleared away mid-game (e.g. Game Over): give everything back, and a player still out here (within 60 m of the
            // stage) goes back where they came from instead of standing in the empty void.
            Restore();
            if (tookPlayer && !Finished && origin != null && head != null &&
                (HeadFloor() - transform.position).sqrMagnitude < 60f * 60f)
                MovePlayer(returnSpot, returnYaw);
            if (Running == this) Running = null;
        }

        // ------------------------------------------------------------------ the game

        private IEnumerator Play(float startDelay)
        {
            origin = FindAnyObjectByType<XROrigin>();
            head = origin != null && origin.Camera != null ? origin.Camera.transform : (Camera.main != null ? Camera.main.transform : null);
            leftHand = Find(origin, "Left Controller");
            rightHand = Find(origin, "Right Controller");
            // The pot's smoke first; never mid coin flip, mid revive or while paused (the rig isn't moved then).
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
            while (PlayerDown() || Paused()) yield return null;
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(0.5f);
            yield return new WaitForSeconds(0.6f);

            // The PlayGround waits; the bridge takes the player in, no teleport here.
            returnSpot = HeadFloor();
            returnYaw = HeadYaw();
            tookPlayer = true;
            // Special Stage Type 3: level-0 stats until Restore (user, 2026-11-10).
            gameObject.AddComponent<Game.Leveling.SpecialStageStats>();
            Freeze(true);
            Teleporting(false);
            Fog(true);
            if (startSpot != null)
            {
                MovePlayer(startSpot.position, startSpot.eulerAngles.y);
                FallOutOfWorld.ReturnSpot = startSpot.position;
                FallOutOfWorld.ReturnYaw = startSpot.eulerAngles.y;
            }
            FallOutOfWorld.Fell += OnFell;
            if (host != null)
            {
                host.TurnSpeed = turnSpeed;
                host.FaceTarget = away;
            }
            HostColour(restColour);
            if (fade != null) fade.FadeIn(0.8f);
            Status("SPECIAL STAGE 3");
            yield return new WaitForSeconds(introSeconds);
            Status("");

            endsAt = Time.time + seconds;
            timing = true;
            while (!over)
            {
                // Sing with its back turned.
                yield return Sing();
                if (over) break;
                // Turn round and look.
                yield return Look();
                if (over) break;
                if (Caught)
                {
                    yield return Catch();
                    continue;
                }
                // Back to singing.
                if (host != null) host.FaceTarget = away;
                if (lookLight != null) lookLight.enabled = false;
                yield return new WaitForSeconds(0.35f);
            }
        }

        private IEnumerator Sing()
        {
            Singing = true;
            Looking = false;
            if (host != null) host.FaceTarget = away;
            HostColour(singColour);
            if (singEffect != null) singEffect.SetActive(true);
            var plan = PlanSong(Progress());
            for (int i = 0; i < plan.Count && !over; i++)
            {
                var note = plan[i];
                if (notes.Length > 0 && notes[note.index % notes.Length] != null)
                {
                    voice.Stop();
                    voice.clip = notes[note.index % notes.Length];
                    voice.volume = songVolume;
                    // Fast stretches sound a little higher, slow ones a little lower.
                    voice.pitch = Mathf.Clamp(Mathf.Pow(note.tempo, pitchFollowsTempo), 0.75f, 1.45f);
                    voice.Play();
                }
                if (host != null) host.Kick(0.12f);
                float until = Time.time + note.seconds;
                while (Time.time < until && !over) yield return null;
            }
            voice.pitch = 1f;
            if (singEffect != null) singEffect.SetActive(false);
            Singing = false;
        }

        private IEnumerator Look()
        {
            if (host != null) host.FaceTarget = head;
            HostColour(lookColour);
            PlayAt(turnSound, host != null ? host.transform.position : transform.position, 1f);
            if (lookLight != null) lookLight.enabled = true;
            // The turn, then a moment to stop.
            yield return new WaitForSeconds(Mathf.Max(0.05f, 180f / Mathf.Max(1f, turnSpeed)));
            PlayAt(lookSound, host != null ? host.Mouth : transform.position, 1f);
            yield return new WaitForSeconds(grace);
            Looking = true;
            float p = Progress();
            var range = p > 0.66f ? lookSecondsNear : lookSeconds;
            float until = Time.time + Random.Range(range.x, range.y);
            // Walking is measured where the head was in the play area: a snap turn swings the rig round the head, which
            // moved the rig itself by up to ~0.3 m and counted as walking whenever the player stood away from the play
            // area's middle (play test 2026-11-10); round the head it hardly moves. Leaning is the head check's job.
            var headAt = Local(head);
            var rigAt = RigSpot(headAt);
            var leftAt = Local(leftHand);
            var rightAt = Local(rightHand);
            while (Time.time < until && !over && !Caught)
            {
                // Knocked down (health out, the lucky coins, getting back up) or paused (the menu, a notice), or only just
                // back from either: nothing counts, and getting up (or the punch that closes a notice) isn't moving.
                if (Excused())
                {
                    headAt = Local(head);
                    rigAt = origin != null ? RigSpot(headAt) : rigAt;
                    leftAt = Local(leftHand);
                    rightAt = Local(rightHand);
                    yield return null;
                    continue;
                }
                var rigNow = origin != null ? RigSpot(headAt) : rigAt;
                var walk = rigNow - rigAt;
                walk.y = 0f;
                var headNow = Local(head);
                var leftNow = Local(leftHand);
                var rightNow = Local(rightHand);
                float dt = Mathf.Max(0.0001f, Time.deltaTime);
                bool moved = walk.magnitude > walkLimit
                    || (headNow - headAt).magnitude > headLimit
                    || (leftNow - leftAt).magnitude / dt > handSpeedLimit && leftHand != null
                    || (rightNow - rightAt).magnitude / dt > handSpeedLimit && rightHand != null;
                leftAt = leftNow;
                rightAt = rightNow;
                if (moved) Caught = true;
                yield return null;
            }
            Looking = false;
        }

        private IEnumerator Catch()
        {
            caughtAt = Time.time;
            pushedAt = -10f;
            Status("CAUGHT!");
            PlayAt(caughtSound, host != null ? host.Mouth : transform.position, 1f);
            int falls = Falls;
            // A stream of bomber slimes out of its mouth, straight at the player (it holds its fire while they're down or
            // the game is paused).
            for (int i = 0; i < bombers && Falls == falls && !over; i++)
            {
                while (Excused() && !over) yield return null;
                Spit(i);
                yield return new WaitForSeconds(bomberEvery);
            }
            // Until they fall: none splatted in time, it pushes them itself (never while they're down; the wait starts
            // again once they're up).
            while (Falls == falls && !over)
            {
                if (Excused()) caughtAt = Time.time;
                else if (Time.time - caughtAt > forceAfter && Time.time - pushedAt > 1.5f) Push();
                yield return null;
            }
            Caught = false;
            Status("");
            ClearBombers();
            if (host != null) host.FaceTarget = away;
            if (lookLight != null) lookLight.enabled = false;
            yield return new WaitForSeconds(1f);
        }

        private void Spit(int i)
        {
            if (bomber == null || host == null) return;
            host.Spit();
            host.OpenFor(0.5f);
            var from = host.Mouth;
            var to = head != null ? head.position : from + Vector3.back;
            var dir = (to - from).normalized;
            var made = Instantiate(bomber, from + dir * 0.4f, Quaternion.LookRotation(dir), transform);
            var colour = bomberColours.Length > 0 ? bomberColours[i % bomberColours.Length] : Color.white;
            made.Launch(colour, dir * Random.Range(5f, 8f) + Random.insideUnitSphere * 1.5f + Vector3.up * 1f);
            sent.Add(made);
        }

        private void OnFaceHit(SlimeBomber who)
        {
            if (!Caught || !sent.Contains(who) || Excused()) return;
            if (Time.time - pushedAt < 1f) return;
            Push();
        }

        // Thrown sideways off the bridge (to the nearer edge), whatever their Toughness.
        private void Push()
        {
            if (head == null) return;
            pushedAt = Time.time;
            var side = transform.right;
            float offset = Vector3.Dot(head.position - transform.position, side);
            float sign = Mathf.Abs(offset) < 0.1f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(offset);
            var from = head.position - side * sign;
            from.y = head.position.y;
            Game.Combat.PlayerLaunch.Launch(from, pushHeight, pushDistance, true, false, 1f);
        }

        private void OnFell()
        {
            Falls++;
            ClearBombers();
        }

        private void Update()
        {
            if (!timing || over) return;
            float left = TimeLeft;
            if (timerText != null)
            {
                int s = Mathf.CeilToInt(left);
                timerText.text = s.ToString();
                timerText.color = s <= 10 ? new Color(1f, 0.35f, 0.3f) : Color.white;
                if (s <= 10 && s != lastTick && s > 0)
                {
                    lastTick = s;
                    PlayAt(tickSound, head != null ? head.position : transform.position, 0.8f);
                }
            }
            // No win while down or paused (walking in real life during a pause doesn't count).
            if (!Caught && !Excused() && Touching())
            {
                StartCoroutine(WinGame());
                return;
            }
            if (left <= 0f) StartCoroutine(LoseGame());
        }

        // The host's body (its mesh renderers' bounds); false if there's none.
        private bool HostBody(out Bounds body)
        {
            body = new Bounds(host != null ? host.transform.position : transform.position, Vector3.zero);
            if (host == null) return false;
            bool any = false;
            foreach (var r in host.GetComponentsInChildren<MeshRenderer>())
            {
                if (!any) { body = r.bounds; any = true; } else body.Encapsulate(r.bounds);
            }
            return any;
        }

        private bool Touching()
        {
            if (!HostBody(out var body)) return false;
            float radius = Mathf.Max(body.extents.x, body.extents.z) + touchReach;
            var centre = body.center;
            foreach (var t in new[] { leftHand, rightHand, head })
            {
                if (t == null) continue;
                var d = t.position - centre;
                d.y *= 0.6f;
                if (d.magnitude < radius) return true;
            }
            return false;
        }

        private IEnumerator WinGame()
        {
            over = true;
            Won = true;
            StopSinging();
            Status("YOU WIN!");
            if (host != null) { host.Kick(0.6f); host.OpenFor(1.2f); host.FaceTarget = head; }
            var at = host != null ? host.Mouth : transform.position;
            if (winEffect != null) Destroy(Instantiate(winEffect, at + Vector3.up * 1.5f, Quaternion.identity), 8f);
            EndGameJackpot.Play(at, exp, 3f, 3, winSound, null);
            // The candies at the player's feet, in front of them.
            var feet = HeadFloor();
            var forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized : transform.forward;
            CandyFountain.Play(feet + forward * 0.8f + Vector3.up * 0.6f, candies, candyPrefabs, 2f, new Vector2(1.6f, 2.6f), new Vector2(0.4f, 1.1f));
            float until = Time.time + collectSeconds;
            while (Time.time < until)
            {
                Status("YOU WIN!\n<size=50%>Grab your candies! " + Mathf.CeilToInt(until - Time.time) + "</size>");
                yield return null;
            }
            yield return ToTheRooftop();
        }

        // After a win (user, 2026-11-07): the Green Seed on the slime's platform. Eating it (or Seed Seconds) sets
        // GAMESTAGES' The_Seed, the last bool the PlayGround waits for: GAMESTAGES sinks the PlayGround, clears what it
        // spawned and brings up the rooftop. This stage stands alone from then on (so it isn't cleared from under the
        // player), fades to black, waits for the rooftop, puts the player near the Lid facing it and fades back in.
        private IEnumerator ToTheRooftop()
        {
            if (GameStages.Fsm() == null)
            {
                // No GAMESTAGES (a test scene): back where they came from, as before.
                Debug.LogWarning("[Daruma Stage] No GAMESTAGES: back to where the player came from.", this);
                yield return Leave();
                yield break;
            }
            var seed = SpawnSeed();
            Status("YOU WIN!\n<size=50%>Eat the seed!</size>");
            float autoAt = Time.time + seedSeconds;
            bool given = false;
            while (!GameStages.GetBool(SeedBool))
            {
                if (seed == null && !given)
                {
                    // No seed (none handed over, or gone): the bool is set for them.
                    if (SeedPrefab == null) Debug.LogWarning("[Daruma Stage] No seed prefab: GAMESTAGES The_Seed set directly.", this);
                    GameStages.SetBool(SeedBool, true, this);
                    given = true;
                }
                else if (seed != null && !given && Time.time >= autoAt)
                {
                    // Not eaten in time: eaten for them (its Activate Stuffs: the item, the heal, The_Seed).
                    foreach (var seedFsm in seed.GetComponentsInChildren<PlayMakerFSM>())
                    {
                        if (seedFsm.ActiveStateName != SeedWaitState) continue;
                        seedFsm.SendEvent(SeedEatenEvent);
                        given = true;
                        break;
                    }
                }
                yield return null;
            }

            // GAMESTAGES' Clear Spawned Stuffs only takes what's under Currently Spawned Stuffs: the bridge stays.
            transform.SetParent(null, true);
            var oldRoof = Global("Rooftop_Base");
            Status("");
            // The seed's sound and shrink, then the black (not over a notice: it would hide it).
            yield return new WaitForSeconds(1f);
            while (Paused()) yield return null;
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(0.8f);
            yield return new WaitForSeconds(0.9f);

            // In the black until the new rooftop is there (Rooftop_Base changes, about 5 s after the seed).
            GameObject roof;
            float waited = 0f;
            bool warned = false;
            while (true)
            {
                if (GameStages.ActiveState() == "Game Over")
                {
                    // Died meanwhile: Game Over has the player; this stage just goes (no pull back).
                    Finished = true;
                    Destroy(gameObject);
                    yield break;
                }
                bool down = PlayerDown();
                roof = Global("Rooftop_Base");
                if (!down && roof != null && roof != oldRoof) break;
                if (!down && Paused())
                {
                    // A notice or the menu: shown, not hidden in the black; black again after.
                    if (fade != null) fade.FadeIn(0.3f);
                    while (Paused()) yield return null;
                    if (fade != null && !warned) fade.FadeOut(0.5f);
                }
                else if (!down && !warned && (waited += Time.deltaTime) > roofWaitSeconds)
                {
                    // GAMESTAGES didn't go on (a gear bool missing?): the view back, still waiting here. Never back into
                    // the PlayGround: its pot is gone, nothing is left to do there.
                    Debug.LogWarning("[Daruma Stage] The rooftop hasn't come " + roofWaitSeconds + " s after the seed (GAMESTAGES is in '" +
                                     GameStages.ActiveState() + "'): waiting on the stage.", this);
                    if (fade != null) fade.FadeIn(0.8f);
                    warned = true;
                }
                yield return null;
            }
            // A moment for the rooftop's animator to place its Lid.
            yield return new WaitForSeconds(0.3f);
            // Not while down, flying from a push, or stepping off the platform in the black: Fall Out Of World would
            // pull them back onto this stage after the move. A fall's put-back (or a notice) shows the view: black again.
            var launch = origin != null ? origin.GetComponent<Game.Combat.PlayerLaunch>() : null;
            for (float until = Time.time + 6f; Time.time < until;)
            {
                if (GameStages.ActiveState() == "Game Over")
                {
                    Finished = true;
                    Destroy(gameObject);
                    yield break;
                }
                if (!PlayerDown() && Paused())
                {
                    if (fade != null) fade.FadeIn(0.3f);
                    while (Paused()) yield return null;
                    continue;
                }
                if (PlayerDown() || (launch != null && launch.Flying) || !OverStage())
                {
                    yield return null;
                    continue;
                }
                if (fade == null || fade.Alpha >= 0.99f) break;
                fade.FadeOut(0.5f);
                yield return new WaitForSeconds(0.6f);
            }
            roof = Global("Rooftop_Base");
            var (spot, yaw) = RooftopSpot(roof);

            ClearCandies();
            Restore();
            MovePlayer(spot, yaw);
            Finished = true;
            Ended?.Invoke(this, true);
            if (Running == this) Running = null;
            if (fade != null) fade.FadeIn(0.8f);
            Destroy(gameObject, 0.2f);
        }

        // The seed on the slime's platform: beside the player, clear of the slime's body, a little over the floor. A child
        // of the stage, so it goes with it. Not on the player: it only notices them after its 1.5 s grow.
        private GameObject SpawnSeed()
        {
            if (SeedPrefab == null) return null;
            var feet = HeadFloor();
            var centre = host != null ? host.transform.position : transform.position;
            var away = feet - centre;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            away.Normalize();
            var side = head != null ? Vector3.ProjectOnPlane(head.right, Vector3.up) : Vector3.zero;
            if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(away, Vector3.up);
            var off = feet + side.normalized * seedBeside - centre;
            off.y = 0f;
            float clear = (HostBody(out var body) ? Mathf.Max(body.extents.x, body.extents.z) : 0f) + 0.6f;
            if (off.magnitude < clear) off = (off.sqrMagnitude > 0.0001f ? off.normalized : away) * clear;
            var local = transform.InverseTransformPoint(centre + off);
            var middle = transform.InverseTransformPoint(centre);
            local.x = Mathf.Clamp(local.x, middle.x - seedArea, middle.x + seedArea);
            local.z = Mathf.Clamp(local.z, middle.z - seedArea, middle.z + seedArea);
            local.y = seedHeight;
            return Instantiate(SeedPrefab, transform.TransformPoint(local), Quaternion.identity, transform);
        }

        // Lid Stand Off metres from the rooftop's Lid (or its middle), towards the south gate, on its floor, facing it.
        private (Vector3 spot, float yaw) RooftopSpot(GameObject roof)
        {
            var roofAt = roof != null ? roof.transform.position : Vector3.zero;
            var lid = Global("Lid");
            var centre = lid != null && roof != null && lid.transform.IsChildOf(roof.transform) ? lid.transform.position : roofAt;
            var back = roof != null ? -roof.transform.forward : Vector3.back;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f) back = Vector3.back;
            var spot = centre + back.normalized * lidStandOff;
            spot.y = roofAt.y + 0.03f;
            if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) &&
                Mathf.Abs(hit.point.y - roofAt.y) < 1.5f)
                spot.y = hit.point.y;
            var look = centre - spot;
            look.y = 0f;
            float yaw = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
            return (spot, yaw);
        }

        // Time's up (user, 2026-11-07): the end of the run. The slime laughs, then the player dies with no lucky coins
        // (Checkpoints: no revives left), so the Game Over starts a new run; it also clears this stage.
        private IEnumerator LoseGame()
        {
            over = true;
            StopSinging();
            HostColour(lookColour);
            Status("TIME'S UP!");
            if (timerText != null) timerText.text = "0";
            if (host != null) { host.FaceTarget = head; host.OpenFor(0.8f); host.Kick(0.4f); }
            PlayAt(laughSound, host != null ? host.Mouth : transform.position, 1f);
            yield return new WaitForSeconds(2.5f);
            // Not while down (the coins after a fall, getting up), paused, flying from a push or mid fall (its fade): then.
            var launch = origin != null ? origin.GetComponent<Game.Combat.PlayerLaunch>() : null;
            var fade = HeadsetFade.Find();
            float fadeWaitUntil = Time.unscaledTime + 6f;
            while (PlayerDown() || Paused() || (launch != null && launch.Flying) ||
                   (fade != null && (fade.IsFading || fade.Alpha > 0.01f) && Time.unscaledTime < fadeWaitUntil))
                yield return null;
            // Finished first: the Game Over clears this stage, and it must not pull the player back.
            Finished = true;
            if (!KillPlayer())
            {
                Debug.LogWarning("[Daruma Stage] No PlayerHealth FSM on the XR Origin: back to where the player came from instead.", this);
                yield return Leave();
                yield break;
            }
            // The PlayGround's creatures stay asleep: the Game Over takes them.
            frozen.Clear();
            Ended?.Invoke(this, false);
        }

        // Dead at once, the way the FIREKILL kill does it (PlayerHealth's global DamageToPlayer: DEAD), with no revive left.
        private bool KillPlayer()
        {
            if (origin == null) return false;
            PlayMakerFSM health = null;
            foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "PlayerHealth") continue;
                health = fsm;
                break;
            }
            if (health == null) return false;
            var launch = origin.GetComponent<Game.Combat.PlayerLaunch>();
            if (launch != null) launch.Stop();
            Game.Saving.Checkpoints.UseAllRevives();
            var hp = health.FsmVariables.FindFsmFloat("PlayerHealth");
            if (hp != null) hp.Value = 0f;
            health.SendEvent("DamageToPlayer");
            return true;
        }

        private void StopSinging()
        {
            voice.Stop();
            Singing = false;
            Looking = false;
            HostColour(restColour);
            if (singEffect != null) singEffect.SetActive(false);
            if (lookLight != null) lookLight.enabled = false;
            ClearBombers();
        }

        private IEnumerator Leave()
        {
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(0.5f);
            yield return new WaitForSeconds(0.6f);
            ClearCandies();
            Restore();
            MovePlayer(returnSpot, returnYaw);
            if (fade != null) fade.FadeIn(0.8f);
            Finished = true;
            Ended?.Invoke(this, Won);
            if (Running == this) Running = null;
            Destroy(gameObject, 0.2f);
        }

        // ------------------------------------------------------------------ the song

        // How close the player is: 0 at the start of the bridge, 1 at its end.
        private float Progress()
        {
            if (head == null || pathStart == null || pathEnd == null) return 0f;
            var path = pathEnd.position - pathStart.position;
            return Mathf.Clamp01(Vector3.Dot(head.position - pathStart.position, path) / Mathf.Max(0.01f, path.sqrMagnitude));
        }

        // One note of a song: which one, how long until the next (seconds), and the speed it's sung at.
        private struct Note
        {
            public int index;
            public float seconds;
            public float tempo;
        }

        // This time's song (user, 2026-11-06): always under Max Song Seconds, its speed changing at random while it's sung:
        // cut into 2 to 4 stretches, each slow or fast (mostly swapping), now and then a held note before a fast stretch.
        // The nearer the player (P: 0 at the start of the bridge, 1 at the slime), the faster its fast stretches can get,
        // the more stretches and the more held notes; now and then near it, only the song's end, very fast.
        private List<Note> PlanSong(float p)
        {
            int count = Mathf.Max(1, notes.Length > 0 ? notes.Length : 9);
            var plan = new List<Note>(count);
            int first = 0;
            if (p > 0.66f && Random.value < 0.15f) first = Mathf.Max(0, count - 4);
            int length = count - first;
            int stretches = Mathf.Clamp(Random.Range(2, p > 0.5f ? 5 : 4), 1, length);
            // Where each stretch starts (the first at 0), different notes, in order.
            var starts = new List<int> { 0 };
            while (starts.Count < stretches)
            {
                int at = Random.Range(1, length);
                if (!starts.Contains(at)) starts.Add(at);
            }
            starts.Sort();
            float fastTop = Mathf.Lerp(fastTempo.y, Mathf.Max(fastTempo.y, fastNear), p);
            bool fast = Random.value < 0.5f;
            float tempo = 1f;
            float holdChance = Mathf.Lerp(0.15f, 0.45f, p);
            int stretch = -1;
            for (int i = 0; i < length; i++)
            {
                if (stretch + 1 < starts.Count && starts[stretch + 1] == i)
                {
                    stretch++;
                    // Mostly the other kind than the last stretch, so the speed really changes.
                    if (i > 0 && Random.value < 0.75f) fast = !fast;
                    tempo = fast ? Random.Range(fastTempo.x, fastTop) : Random.Range(slowTempo.x, slowTempo.y);
                }
                float d = Beat(first + i, count) / tempo;
                // A fake-out: a held note right before a fast stretch starts.
                bool nextIsNewStretch = stretch + 1 < starts.Count && starts[stretch + 1] == i + 1;
                if (nextIsNewStretch && !fast && Random.value < holdChance) d += Random.Range(0.5f, 1.2f);
                plan.Add(new Note { index = first + i, seconds = d, tempo = tempo });
            }
            // Never longer than Max Song Seconds.
            float total = 0f;
            foreach (var n in plan) total += n.seconds;
            if (total > maxSongSeconds)
            {
                float k = maxSongSeconds / total;
                for (int i = 0; i < plan.Count; i++)
                {
                    var n = plan[i];
                    n.seconds *= k;
                    n.tempo /= k;
                    plan[i] = n;
                }
            }
            return plan;
        }

        private float Beat(int i, int count) => noteSeconds * (i == count - 1 ? 1.6f : 1f);

        // ------------------------------------------------------------------ the player and the world

        private Vector3 HeadFloor()
        {
            if (head == null) return transform.position;
            float y = origin != null ? origin.transform.position.y : 0f;
            return new Vector3(head.position.x, y, head.position.z);
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
            var body = origin.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            var launch = origin.GetComponent<Game.Combat.PlayerLaunch>();
            if (launch != null) launch.Stop();
            origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(HeadYaw(), yaw));
            var feet = HeadFloor();
            var to = origin.transform.position + new Vector3(spot.x - feet.x, 0f, spot.z - feet.z);
            to.y = spot.y;
            origin.transform.position = to;
            Physics.SyncTransforms();
            if (body != null) body.enabled = true;
        }

        private Vector3 Local(Transform t) => t == null || origin == null ? Vector3.zero : origin.transform.InverseTransformPoint(t.position);

        // Where a point of the play area (in the rig's own space) is in the world now.
        private Vector3 RigSpot(Vector3 local) => origin != null ? origin.transform.TransformPoint(local) : Vector3.zero;

        // The player is down: health out, the lucky coins under way, or getting back up from a revive (as Player Launch).
        private bool PlayerDown()
        {
            if (playerHealth == null && origin != null)
            {
                foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == "PlayerHealth") playerHealth = fsm.FsmVariables.FindFsmFloat("PlayerHealth");
                }
            }
            return Game.Saving.Checkpoints.IsDying ||
                   (Game.Combat.PlayerRevive.Instance != null && Game.Combat.PlayerRevive.Instance.IsReviving) ||
                   (playerHealth != null && playerHealth.Value <= 0f);
        }

        private static readonly RaycastHit[] floorHits = new RaycastHit[8];

        // The head is over this stage's floor (a platform or the bridge), not off its edge or mid fall.
        private bool OverStage()
        {
            if (head == null) return true;
            int count = Physics.RaycastNonAlloc(head.position, Vector3.down, floorHits, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (floorHits[i].collider != null && floorHits[i].collider.transform.IsChildOf(transform)) return true;
            }
            return false;
        }

        // The game is stopped: the pause menu, a level-up or other notice, the lucky coins (Pause Menu holds), lost focus.
        private static bool Paused() => Time.timeScale <= 0f || Game.Menus.PauseMenu.IsPaused || Game.Menus.PauseMenu.IsHeld;

        // Down or paused (user, 2026-11-07: a pause is like being knocked down), or only just back from either (Resume
        // Grace, real seconds): moving doesn't count, it holds its fire and there's no win.
        private bool Excused()
        {
            if (PlayerDown() || Paused())
            {
                excusedUntil = Time.unscaledTime + resumeGrace;
                return true;
            }
            return Time.unscaledTime < excusedUntil;
        }

        // A PlayMaker global GameObject's object (null if it's empty or gone).
        private static GameObject Global(string name)
        {
            var global = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject(name);
            return global != null ? global.Value : null;
        }

        // A spawned candy: "Candy_<number>(Clone)", never the shop's Candy Box (review, 2026-11-10: a bought Candy Box
        // lying near the stage was deleted with the candies).
        private static bool IsCandy(string name) =>
            name.StartsWith("Candy_") && name.Length > 6 && char.IsDigit(name[6]) && name.EndsWith("(Clone)");

        // Candies left lying on the stage go with it.
        private void ClearCandies()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && IsCandy(t.name) && (t.position - transform.position).sqrMagnitude < 60f * 60f) Destroy(t.gameObject);
            }
        }

        // The host's skin: the paint texture in this colour (green sings, red looks), on its renderers only.
        private void HostColour(Color colour)
        {
            if (host == null) return;
            hostLooks ??= host.GetComponentsInChildren<Renderer>(true);
            hostBlock ??= new MaterialPropertyBlock();
            foreach (var r in hostLooks)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(hostBlock);
                if (hostPaint != null) hostBlock.SetTexture(BaseMapId, hostPaint);
                hostBlock.SetColor(BaseColorId, colour);
                r.SetPropertyBlock(hostBlock);
            }
        }

        private static Transform Find(XROrigin rig, string name)
        {
            if (rig == null) return null;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // The PlayGround's creatures, spawners and music pause while the player is away.
        private void Freeze(bool on)
        {
            if (on)
            {
                frozen.Clear();
                var seen = new HashSet<GameObject>();
                // Only what the stages spawned (CurrentlySpawnedStuffs): never the scene's own managers.
                var holder = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
                var spawned = holder != null && holder.Value != null ? holder.Value.transform : null;
                if (spawned == null) return;
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb == null || mb.transform.IsChildOf(transform) || !mb.transform.IsChildOf(spawned)) continue;
                    if (!(mb is RainbowSlime || mb is SlimeBomber || mb is SpiderBrain || mb is FatPumpkin || mb is PlayGround.GhostTree ||
                          mb is PlayGround.GhostBush || mb is PlayGround.LaughingPumpkin || mb is Audio.PlaylistPlayer)) continue;
                    if (seen.Add(mb.gameObject) && mb.gameObject.activeSelf)
                    {
                        mb.gameObject.SetActive(false);
                        frozen.Add(mb.gameObject);
                    }
                }
                return;
            }
            foreach (var go in frozen) if (go != null) go.SetActive(true);
            frozen.Clear();
        }

        private void Teleporting(bool on)
        {
            if (origin == null) return;
            if (!on)
            {
                teleport = origin.GetComponentInChildren<TeleportationProvider>(true);
                var ray = Find(origin, "Teleport Interactor");
                teleportRay = ray != null ? ray.gameObject : null;
                teleportWas = teleport != null && teleport.enabled;
                rayWas = teleportRay != null && teleportRay.activeSelf;
                if (teleport != null) teleport.enabled = false;
                if (teleportRay != null) teleportRay.SetActive(false);
                return;
            }
            if (teleport != null) teleport.enabled = teleportWas;
            if (teleportRay != null) teleportRay.SetActive(rayWas);
            teleport = null;
            teleportRay = null;
        }

        private void Fog(bool on)
        {
            if (on)
            {
                fogStartWas = RenderSettings.fogStartDistance;
                fogEndWas = RenderSettings.fogEndDistance;
                RenderSettings.fogStartDistance = fog.x;
                RenderSettings.fogEndDistance = fog.y;
                fogSet = true;
                return;
            }
            if (!fogSet) return;
            RenderSettings.fogStartDistance = fogStartWas;
            RenderSettings.fogEndDistance = fogEndWas;
            fogSet = false;
        }

        private void Restore()
        {
            timing = false;
            FallOutOfWorld.Fell -= OnFell;
            if (startSpot != null && FallOutOfWorld.ReturnSpot.HasValue && (FallOutOfWorld.ReturnSpot.Value - startSpot.position).sqrMagnitude < 0.01f)
                FallOutOfWorld.ReturnSpot = null;
            Fog(false);
            Teleporting(true);
            Freeze(false);
            ClearBombers();
            // The player's own stats back (switched off now; it goes with the stage).
            if (TryGetComponent(out Game.Leveling.SpecialStageStats stats)) stats.enabled = false;
        }

        private void ClearBombers()
        {
            foreach (var b in sent) if (b != null) Destroy(b.gameObject);
            sent.Clear();
        }

        private void Status(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        private void PlayAt(AudioClip clip, Vector3 at, float volume)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
        }
    }
}
