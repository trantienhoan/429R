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
    /// firework, EXP in gold orbs and a fountain of candies, a few seconds to grab them, then back where they were. No
    /// teleport on the bridge. The PlayGround's own creatures and music wait meanwhile.
    /// Made by Special Stage 3 Trigger (on the cauldron) when the 2nd pumpkin is caught with its whole wave beaten.
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
        [Tooltip("Seconds to grab the candies before going back.")]
        [SerializeField] private float collectSeconds = 15f;

        [Header("Signs and look")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("While the stage runs, the fog starts and ends this far (so the slime shows at the far end).")]
        [SerializeField] private Vector2 fog = new(14f, 48f);

        /// <summary>The stage being played (only one at a time).</summary>
        public static DarumaStage Running { get; private set; }

        /// <summary>Raised when it's over: won or not.</summary>
        public static event System.Action<DarumaStage, bool> Ended;

        /// <summary>Over, the player back where they were (Play tests read it).</summary>
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

        /// <summary>Takes the player in (fade, the PlayGround waits, onto the bridge) and starts the game.</summary>
        public void Begin()
        {
            if (Running != null && Running != this) { Destroy(gameObject); return; }
            Running = this;
            StartCoroutine(Play());
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
            // Cleared away mid-game (e.g. Game Over): give everything back.
            Restore();
            if (Running == this) Running = null;
        }

        // ------------------------------------------------------------------ the game

        private IEnumerator Play()
        {
            origin = FindAnyObjectByType<XROrigin>();
            head = origin != null && origin.Camera != null ? origin.Camera.transform : (Camera.main != null ? Camera.main.transform : null);
            leftHand = Find(origin, "Left Controller");
            rightHand = Find(origin, "Right Controller");
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(0.5f);
            yield return new WaitForSeconds(0.6f);

            // The PlayGround waits; the bridge takes the player in, no teleport here.
            returnSpot = HeadFloor();
            returnYaw = HeadYaw();
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
            if (singEffect != null) singEffect.SetActive(true);
            var plan = PlanSong(Progress());
            for (int i = 0; i < plan.Count && !over; i++)
            {
                if (notes.Length > 0 && notes[i % notes.Length] != null)
                {
                    voice.Stop();
                    voice.clip = notes[i % notes.Length];
                    voice.volume = songVolume;
                    voice.Play();
                }
                if (host != null) host.Kick(0.12f);
                float until = Time.time + plan[i];
                while (Time.time < until && !over) yield return null;
            }
            if (singEffect != null) singEffect.SetActive(false);
            Singing = false;
        }

        private IEnumerator Look()
        {
            if (host != null) host.FaceTarget = head;
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
            var rigAt = origin != null ? origin.transform.position : Vector3.zero;
            var headAt = Local(head);
            var leftAt = Local(leftHand);
            var rightAt = Local(rightHand);
            while (Time.time < until && !over && !Caught)
            {
                var rigNow = origin != null ? origin.transform.position : rigAt;
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
            // A stream of bomber slimes out of its mouth, straight at the player.
            for (int i = 0; i < bombers && Falls == falls && !over; i++)
            {
                Spit(i);
                yield return new WaitForSeconds(bomberEvery);
            }
            // Until they fall: none splatted in time, it pushes them itself.
            while (Falls == falls && !over)
            {
                if (Time.time - caughtAt > forceAfter && Time.time - pushedAt > 1.5f) Push();
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
            if (!Caught || !sent.Contains(who)) return;
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
            if (!Caught && Touching())
            {
                StartCoroutine(WinGame());
                return;
            }
            if (left <= 0f) StartCoroutine(LoseGame());
        }

        private bool Touching()
        {
            if (host == null) return false;
            var body = new Bounds(host.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in host.GetComponentsInChildren<MeshRenderer>())
            {
                if (!any) { body = r.bounds; any = true; } else body.Encapsulate(r.bounds);
            }
            if (!any) return false;
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
            yield return Leave();
        }

        private IEnumerator LoseGame()
        {
            over = true;
            StopSinging();
            Status("TIME'S UP!");
            if (timerText != null) timerText.text = "0";
            if (host != null) { host.FaceTarget = head; host.OpenFor(0.8f); host.Kick(0.4f); }
            PlayAt(laughSound, host != null ? host.Mouth : transform.position, 1f);
            yield return new WaitForSeconds(3.5f);
            yield return Leave();
        }

        private void StopSinging()
        {
            voice.Stop();
            Singing = false;
            Looking = false;
            if (singEffect != null) singEffect.SetActive(false);
            if (lookLight != null) lookLight.enabled = false;
            ClearBombers();
        }

        private IEnumerator Leave()
        {
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(0.5f);
            yield return new WaitForSeconds(0.6f);
            // Candies left lying on the stage go with it.
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith("Candy_") && t.name.EndsWith("(Clone)") && (t.position - transform.position).sqrMagnitude < 60f * 60f) Destroy(t.gameObject);
            }
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

        // Seconds each note lasts this time: steady while far; random speeds (sometimes changing mid-song) in the middle;
        // near the slime: slow, then suddenly very fast (or a long held note, then a rush), sometimes a short fast song.
        private List<float> PlanSong(float p)
        {
            int count = Mathf.Max(1, notes.Length > 0 ? notes.Length : 9);
            var plan = new List<float>(count);
            if (p < 0.3f)
            {
                float tempo = Random.Range(0.85f, 1.15f);
                for (int i = 0; i < count; i++) plan.Add(Beat(i, count) / tempo);
                return plan;
            }
            if (p < 0.66f)
            {
                float tempo = Random.Range(0.7f, 1.8f);
                int change = Random.value < 0.4f ? Random.Range(2, count - 1) : count;
                float tempo2 = Random.Range(0.7f, 2.2f);
                for (int i = 0; i < count; i++) plan.Add(Beat(i, count) / (i < change ? tempo : tempo2));
                return plan;
            }
            float roll = Random.value;
            if (roll < 0.15f)
            {
                // Short and fast: only the end of the song.
                for (int i = count - 4; i < count; i++) plan.Add(Beat(i, count) / Random.Range(2.5f, 3.5f));
                return plan;
            }
            int slow = Random.Range(3, 7);
            float slowTempo = Random.Range(0.45f, 0.7f), fastTempo = Random.Range(2.5f, 4f);
            for (int i = 0; i < count; i++)
            {
                float d = Beat(i, count) / (i < slow ? slowTempo : fastTempo);
                // A fake-out: one long held note right before the rush.
                if (roll < 0.4f && i == slow - 1) d += Random.Range(0.6f, 1.4f);
                plan.Add(d);
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
