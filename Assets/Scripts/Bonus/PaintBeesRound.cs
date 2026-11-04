using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Inventory;
using Game.Leveling;
using TMPro;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The Paint Bees bonus round (Paint_Bees_Round), the Playroom's bonus before the rooftop's bees. Play Bonus Round
    /// makes it when the player earned it, on the bonus stage (--Playroom--MiniGame--: the field):
    /// - a glowing ring with an arrow over it marks the middle; standing on it, four floating paint buckets (red, blue,
    ///   yellow, green, in a random order) pop up round the player at waist height, and a sign says to dip a hand;
    /// - a hand dipped in a bucket takes its colour (see Paint Hands), and keeps it until dipped in another;
    /// - it all goes to a song (user, 2026-11-03: "Player punches flying colored bee that drops candies follow fight
    ///   song beat", like Vibe Punch; Pumpkin_Pillow_Party, 100 BPM): the song starts as the buckets come up, the 3-2-1
    ///   ticks on its beats, and bees (two colours at first; all of them and one rainbow bee once All Colours After are swatted) appear on
    ///   a beat far in front, a few at a time and more often as it goes, reach the face's front on a beat, hover there
    ///   (the warning) and dive on a beat, at the face on the next; they throb to every beat. They stop coming at Last
    ///   Bee At seconds into the song, and it fades out once the last one is done;
    /// - a swat with the hand of its colour bursts it into paint: Candies Per Bee candies and Exp Per Bee EXP (the
    ///   rainbow bee: any painted hand, Rainbow Candies and Rainbow Exp); a swat on a beat (within On Beat Window) gives
    ///   On Beat Candies more and a gold flash, no words; the wrong colour (or a clean hand) knocks it back with a boing
    ///   and it comes again; one that reaches the face stings (Sting Share of a bite: about 1 health) and splats its
    ///   paint on the view;
    /// - other music playing (a room's radio, the stage's song) is paused while the round's song plays;
    /// - the bees' road lights up from the 3-2-1 (Road: lanes of runway lights throbbing to the beat, sparkling lights
    ///   marching in to the player on every half beat, embers, a red danger ring round the feet that flares while a bee
    ///   dives), and paint that falls to the floor (swatted bees, dripping hands, the buckets' drips) splats there;
    /// - the Candy Helmet is on the whole time (every candy flies to the head); at the end fireworks go up in front, a
    ///   round with no sting gives Perfect Candies more, the helmet eats what's left, the buckets sink, the hands are
    ///   clean again, and the round is over (Finished: Play Bonus Round goes on).
    /// Tools > 429 Game > Bonus > Set Up Paint Bees Bonus builds it and its parts.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class PaintBeesRound : MonoBehaviour, IBonusGame
    {
        [Header("Stage")]
        [Tooltip("The sky over the field while the round is on (Bonus_Stage_Sky). Empty: none.")]
        [SerializeField] private GameObject stageSky;

        [Header("Start")]
        [Tooltip("What marks the middle (Paint_Bees_Start: a ring on the floor, an arrow over it, sparkles). Its child " +
                 "\"Arrow\" bobs and turns to the player.")]
        [SerializeField] private GameObject marker;
        [Tooltip("The player counts as on it within this many metres of its middle...")]
        [Min(0.1f)]
        [SerializeField] private float standRadius = 0.5f;
        [Tooltip("...for this long, in seconds.")]
        [Min(0f)]
        [SerializeField] private float standSeconds = 0.4f;

        [Header("Buckets")]
        [SerializeField] private PaintBucket bucket;
        [Tooltip("The paint colours, one bucket each.")]
        [SerializeField] private Color[] colours =
        {
            new(1f, 0.2f, 0.18f), new(0.2f, 0.45f, 1f), new(1f, 0.85f, 0.12f),
        };
        [Tooltip("How far from the player the buckets float, in metres...")]
        [Min(0.2f)]
        [SerializeField] private float bucketDistance = 0.7f;
        [Tooltip("...at these angles from where they face when they step on the ring (degrees; one per colour).")]
        [SerializeField] private float[] bucketAngles = { -60f, 0f, 60f };
        [Tooltip("How high over the floor their paint is, in metres (waist height).")]
        [Min(0.2f)]
        [SerializeField] private float paintHeight = 0.175f;
        [Tooltip("Seconds each takes to pop up, and between one and the next.")]
        [Min(0.05f)]
        [SerializeField] private float riseSeconds = 0.6f;
        [Min(0f)]
        [SerializeField] private float riseGap = 0.15f;

        [Header("Song")]
        [Tooltip("The song the bees fly to (Pumpkin_Pillow_Party). It starts as the buckets come up. Empty: no song, the " +
                 "bees keep the beat anyway.")]
        [SerializeField] private AudioClip song;
        [Range(0f, 1f)]
        [SerializeField] private float songVolume = 0.8f;
        [Tooltip("Its beats a minute, and where its first beat is, in seconds (measured: 100 and 0.593 for " +
                 "Pumpkin_Pillow_Party).")]
        [Min(30f)]
        [SerializeField] private float bpm = 100f;
        [Min(0f)]
        [SerializeField] private float firstBeat = 0.593f;
        [Tooltip("No more bees after this many seconds into the song (Pumpkin_Pillow_Party's loud part ends at about 97 s).")]
        [Min(5f)]
        [SerializeField] private float lastBeeAt = 90f;
        [Tooltip("Seconds the song fades out over at the end.")]
        [Min(0f)]
        [SerializeField] private float fadeSeconds = 2.5f;

        [Header("Road")]
        [Tooltip("The lit road the bees fly in on (Paint_Bees_Road), laid in front of the player from the 3-2-1. Empty: none.")]
        [SerializeField] private PaintBeesRoad road;

        [Header("Bees")]
        [SerializeField] private PaintBee bee;
        [Tooltip("The bees fly out of the stage's fire columns (user, 2026-11-04: \"Spawn bee where 4 column of Fire is " +
                 "burning. Bee flys out of fire\"): the objects with this name (Boss_Area's Danger_Flames) within Fire Reach of " +
                 "the round. None found: they come from in front of the player, as before.")]
        [SerializeField] private string fireName = "Danger_Flame";
        [Min(1f)]
        [SerializeField] private float fireReach = 25f;
        [Tooltip("How high in the fire they come out, in metres over its foot (x to y), times its size.")]
        [SerializeField] private Vector2 fireHeight = new(0.8f, 1.5f);
        [Tooltip("The bees come in only two of the paint colours (two picked at random each round) until the player has " +
                 "swatted more than this many; then in all of them, and the next one is the rainbow bee (user, 2026-11-04). " +
                 "0 = all colours and the rainbow bee from the start.")]
        [Min(0)]
        [SerializeField] private int allColoursAfter = 30;
        [Tooltip("The rainbow bee is this many times bigger.")]
        [Min(0.5f)]
        [SerializeField] private float rainbowSize = 1.3f;
        [Tooltip("Beats between one lot of bees and the next, at the start (x) and the end (y); a lot of two or three " +
                 "takes a beat more for each bee after the first.")]
        [SerializeField] private Vector2 everyBeats = new(4f, 2f);
        [Tooltip("Beats a bee takes to fly in, at the start (x) and the end (y).")]
        [SerializeField] private Vector2 flyBeats = new(4f, 3f);
        [Tooltip("Beats it hovers in front of the face before it dives, at the start (x) and the end (y); the bees of a " +
                 "lot dive one beat after another.")]
        [SerializeField] private Vector2 hoverBeats = new(2f, 1f);
        [Tooltip("Beats its dive takes: it's at the face on the beat after it dives.")]
        [Min(0.25f)]
        [SerializeField] private float diveBeats = 1f;
        [Tooltip("Never more bees about than this (a lot that would go over it comes a beat later).")]
        [Min(1)]
        [SerializeField] private int mostAtOnce = 6;
        [Tooltip("They come from this far away (x to y metres)...")]
        [SerializeField] private Vector2 spawnDistance = new(5.5f, 7f);
        [Tooltip("...within this many degrees either side of the front...")]
        [Range(0f, 120f)]
        [SerializeField] private float spawnArc = 35f;
        [Tooltip("...and this high over the head (x to y metres).")]
        [SerializeField] private Vector2 spawnHeight = new(-0.3f, 1.2f);
        [Tooltip("How far in front of the face they hover, in metres (about arm's reach).")]
        [Min(0.3f)]
        [SerializeField] private float hoverDistance = 0.7f;
        [Tooltip("A hand moving at least this fast (metres a second) swats.")]
        [Min(0f)]
        [SerializeField] private float swatSpeed = 0.9f;
        [Tooltip("The wrong colour knocks a bee back this fast (metres a second).")]
        [Min(0f)]
        [SerializeField] private float bounceSpeed = 3f;

        [Header("Rewards")]
        [Tooltip("One of these, picked at random, per candy.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Min(0)]
        [SerializeField] private int candiesPerBee = 1;
        [Min(0)]
        [SerializeField] private int rainbowCandies = 10;
        [Tooltip("More candies for a round with no sting.")]
        [Min(0)]
        [SerializeField] private int perfectCandies = 10;
        [Tooltip("A swat this close to a beat (seconds either side) is on the beat...")]
        [Range(0f, 0.3f)]
        [SerializeField] private float onBeatWindow = 0.12f;
        [Tooltip("...and gives this many candies more...")]
        [Min(0)]
        [SerializeField] private int onBeatCandies = 1;
        [Tooltip("...and this flash where it burst (Flash_round_yellow), this big. Empty: none.")]
        [SerializeField] private GameObject onBeatFlash;
        [Min(0.01f)]
        [SerializeField] private float onBeatFlashSize = 0.35f;
        [Min(0)]
        [SerializeField] private int expPerBee = 1;
        [Min(0)]
        [SerializeField] private int rainbowExp = 5;
        [Tooltip("At the end the helmet eats every candy left: for this many seconds at the most.")]
        [Min(0f)]
        [SerializeField] private float candyWaitSeconds = 20f;

        [Header("Stings")]
        [Tooltip("A sting does this share of a bee's bite (Damage: about 6-9 health), so about 1 health.")]
        [Range(0f, 1f)]
        [SerializeField] private float stingShare = 0.15f;
        [Tooltip("The paint blobs splatted on the view (the round tints them its colour).")]
        [SerializeField] private Material splatMaterial;
        [Tooltip("The paint splats on the floor (swatted bees, dripping hands). Empty: the view's splat above.")]
        [SerializeField] private Material floorSplatMaterial;
        [Min(0.1f)]
        [SerializeField] private float splatSeconds = 1.4f;

        [Header("Sounds")]
        [SerializeField] private AudioClip[] dipSounds = System.Array.Empty<AudioClip>();
        [Tooltip("A bucket popping up.")]
        [SerializeField] private AudioClip bucketSound;
        [Tooltip("A bee swatted (one at random), a little higher with each swat in a row.")]
        [SerializeField] private AudioClip[] swatSounds = System.Array.Empty<AudioClip>();
        [Tooltip("The wrong colour.")]
        [SerializeField] private AudioClip boingSound;
        [SerializeField] private AudioClip stingSound;
        [Tooltip("Each of the 3-2-1.")]
        [SerializeField] private AudioClip tickSound;
        [Tooltip("GO!")]
        [SerializeField] private AudioClip goSound;
        [Tooltip("The rainbow bee comes.")]
        [SerializeField] private AudioClip rainbowSound;
        [Tooltip("No sting the whole round.")]
        [SerializeField] private AudioClip perfectSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;

        [Header("Effects")]
        [Tooltip("A puff where things appear and go, e.g. Smoke_Flash_Small_Puff.")]
        [SerializeField] private GameObject puffEffect;
        [Tooltip("The paint blobs a bee bursts into, and a dip splashes (the round tints them).")]
        [SerializeField] private Material burstMaterial;
        [Tooltip("Fireworks at the end (Firework_Big).")]
        [SerializeField] private GameObject winFireworks;
        [Min(0.1f)]
        [SerializeField] private float fireworkScale = 1f;

        [Header("Sign")]
        [SerializeField] private TMP_FontAsset font;
        [Min(0.0001f)]
        [SerializeField] private float signScale = 0.006f;

        private static readonly Color SignColor = new(1f, 0.82f, 0.25f, 1f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private const float CandyReach = 15f;
        private const float BoingWait = 0.4f;
        private const float SignAhead = 2.2f;
        private const float SignOverHead = 0.45f;

        /// <summary>The round is over and the candies eaten: Play Bonus Round goes on.</summary>
        public bool Finished { get; private set; }

        /// <summary>The player's head (the camera).</summary>
        public Transform Head { get; private set; }

        /// <summary>The flat way the player faced when they stepped on the ring: the bees come from there.</summary>
        public Vector3 Front { get; private set; } = Vector3.forward;

        /// <summary>The flat way the player looks now (smoothed): the bees come from there and hover in front of it.</summary>
        public Vector3 Looking { get; private set; } = Vector3.forward;

        /// <summary>Bees swatted, stings taken, and swats on a beat.</summary>
        public int Swatted { get; private set; }

        // The fire columns the bees fly out of, and the one used last.
        private readonly List<Transform> fires = new();
        private int lastFire = -1;

        // The two colours the bees come in at first, whether all of them come now, and the rainbow bee's turn.
        private int[] startColours;
        private bool allColours;
        private bool rainbowDue;
        public int Stings { get; private set; }
        public int OnBeat { get; private set; }

        /// <summary>The song's clock: seconds into the song (it stops while the game is paused, as the song does).</summary>
        public float SongTime { get; private set; }

        /// <summary>1 on each beat, falling off fast before the next (for things that throb to it).</summary>
        public float BeatPulse { get; private set; }

        // Music this long or longer that's playing when the song starts (a radio, the stage's song) is hushed meanwhile.
        private const float OtherMusicLength = 30f;

        // One bee to come: its song time (on a beat), colour (-1: the rainbow bee), place in its lot, the lot's size, and
        // how far through the round it comes (0-1).
        private struct Planned
        {
            public float at;
            public int colour;
            public int slot;
            public int lot;
            public float progress;
        }

        private AudioSource music;
        private bool clockRunning;
        private Transform paintFloor;
        private PaintBeesRoad roadMade;
        private int lastHalfBeat = int.MinValue;
        private float nextHush;
        private readonly List<AudioSource> hushed = new();

        private PaintHands hands;
        private readonly List<PaintBucket> buckets = new();
        private readonly List<PaintBee> live = new();
        private readonly Dictionary<PaintBee, float> boinged = new();
        // The bucket each hand was in last frame (a dip splashes as it goes in).
        private readonly Dictionary<PaintHands.Hand, PaintBucket> dippedIn = new();
        private int shownCount = -1;
        private Vector3 middle;
        private bool beesOut;
        private int combo;
        private int total;
        private AudioSource voice;
        private ParticleSystem blobs;
        private RectTransform sign;
        private TMP_Text signText;
        private int fountains;

        private Material FloorSplat => floorSplatMaterial != null ? floorSplatMaterial : splatMaterial != null ? splatMaterial : burstMaterial;

        /// <summary>A paint colour (index into Colours).</summary>
        public Color ColourOf(int index) => index >= 0 && index < colours.Length ? colours[index] : Color.white;

        private void Start()
        {
            Head = Camera.main != null ? Camera.main.transform : null;
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            // The floor paint splats on (the round stands on it).
            paintFloor = new GameObject("Paint Floor").transform;
            paintFloor.SetParent(transform, false);
            blobs = MakeBlobs();
            MakeSign();
            if (stageSky != null) Instantiate(stageSky, transform.position, Quaternion.identity, transform);
            if (puffEffect != null) EffectPool.Warm(puffEffect, 3);
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            foreach (var b in live) if (b != null) Destroy(b.gameObject);
            if (sign != null) Destroy(sign.gameObject);
            Unhush();
        }

        // ---------- The song and its beat ----------

        private float BeatSeconds => 60f / Mathf.Max(1f, bpm);

        /// <summary>One beat of the song, in seconds.</summary>
        public float BeatLength => BeatSeconds;

        // The song time of a beat (0 = the first).
        private float BeatTime(int beat) => firstBeat + beat * BeatSeconds;

        // The first beat at or after this song time.
        private int BeatAtOrAfter(float time) => Mathf.CeilToInt((time - firstBeat) / BeatSeconds - 0.001f);

        // The song from the top, the clock with it; other music hushed meanwhile.
        private void StartSong()
        {
            SongTime = 0f;
            clockRunning = true;
            if (song == null) return;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.priority = 16;
            music.clip = song;
            music.volume = songVolume;
            Hush();
            music.Play();
        }

        // Other music playing is muted (not paused: a radio's FSM would take a paused song for finished and play the next).
        private void Hush()
        {
            nextHush = Time.unscaledTime + 1f;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (source == null || source == music || source.mute || !source.isPlaying || source.clip == null ||
                    source.clip.length < OtherMusicLength) continue;
                source.mute = true;
                hushed.Add(source);
            }
        }

        private void Unhush()
        {
            foreach (var source in hushed) if (source != null) source.mute = false;
            hushed.Clear();
        }

        // The song's clock: frame time, kept in step with where the song is heard (paused with the game: both stop).
        private void Tick()
        {
            // The way the player looks, flat and smoothed (a glance doesn't swing the bees about).
            if (Head != null)
            {
                var look = Head.forward;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f) Looking = Vector3.Slerp(Looking, look.normalized, 1f - Mathf.Exp(-3f * Time.deltaTime));
            }
            if (!clockRunning) return;
            SongTime += Time.deltaTime;
            if (music != null && music.isPlaying && music.clip != null)
            {
                float heard = music.timeSamples / (float)music.clip.frequency;
                SongTime = Mathf.Abs(heard - SongTime) > 0.08f ? heard : Mathf.Lerp(SongTime, heard, 0.15f);
                // Music started meanwhile (a radio's next song) is hushed too.
                if (Time.unscaledTime >= nextHush) Hush();
            }
            float sinceBeat = Mathf.Repeat(SongTime - firstBeat, BeatSeconds);
            BeatPulse = SongTime >= firstBeat - 0.01f ? Mathf.Exp(-sinceBeat * 9f) : 0f;
            // The road's lights set off on every half beat.
            int half = Mathf.FloorToInt((SongTime - firstBeat) / (BeatSeconds * 0.5f));
            if (roadMade != null && half != lastHalfBeat && SongTime >= firstBeat)
            {
                lastHalfBeat = half;
                roadMade.HalfBeat(half % 2 == 0, BeatSeconds);
            }
        }

        // The song fades out and stops; the other music comes back.
        private IEnumerator FadeSong()
        {
            if (music != null)
            {
                float from = music.volume;
                for (float t = 0f; t < fadeSeconds && music != null; t += Time.deltaTime)
                {
                    music.volume = from * (1f - t / fadeSeconds);
                    yield return null;
                }
                if (music != null) music.Stop();
            }
            Unhush();
        }

        /// <summary>
        /// A bee knocked back comes again: the song times it's back in front of the face, dives and reaches it, on beats.
        /// </summary>
        public void ComeAgain(out float arrive, out float dive, out float hit)
        {
            int beat = BeatAtOrAfter(SongTime + BeatSeconds * 0.5f) + 1;
            arrive = BeatTime(beat);
            dive = BeatTime(beat + 1);
            hit = dive + diveBeats * BeatSeconds;
        }

        // The bees from this beat to Last Bee At: a lot every few beats (more often as it goes), lots of two or three
        // from a quarter of the way through. Their colours are picked as they're sent (see Colour For Next Bee).
        private List<Planned> Plan(int startBeat)
        {
            var plan = new List<Planned>();
            float start = BeatTime(startBeat);
            float end = Mathf.Max(start + BeatSeconds, lastBeeAt);
            for (int beat = startBeat; BeatTime(beat) <= end;)
            {
                float p = Mathf.InverseLerp(start, end, BeatTime(beat));
                int lot = 1;
                if (p >= 0.25f && Random.value < (p < 0.6f ? 0.4f : 0.55f)) lot = p >= 0.6f && Random.value < 0.45f ? 3 : 2;
                lot = Mathf.Min(lot, mostAtOnce);
                for (int i = 0; i < lot; i++)
                    plan.Add(new Planned { at = BeatTime(beat), slot = i, lot = lot, progress = p });
                beat += Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(everyBeats.x, everyBeats.y, p))) + (lot - 1);
            }
            return plan;
        }

        // Two colours at first; once more than All Colours After bees are swatted, every colour, and the next bee is the
        // rainbow bee (only that one).
        private int ColourForNextBee()
        {
            if (startColours == null)
            {
                var order = new List<int>();
                for (int i = 0; i < colours.Length; i++) order.Add(i);
                for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
                startColours = order.GetRange(0, Mathf.Min(2, order.Count)).ToArray();
                allColours = allColoursAfter <= 0;
                rainbowDue = allColours;
            }
            if (!allColours && Swatted > allColoursAfter)
            {
                allColours = true;
                rainbowDue = true;
            }
            if (rainbowDue)
            {
                rainbowDue = false;
                return -1;
            }
            return allColours || startColours.Length == 0 ? Random.Range(0, colours.Length) : startColours[Random.Range(0, startColours.Length)];
        }

        private IEnumerator Run()
        {
            // The ring in the middle: the round starts when the player stands on it.
            var floor = transform.position;
            var mark = marker != null ? Instantiate(marker, floor, Quaternion.identity, transform) : null;
            var arrow = mark != null ? mark.transform.Find("Arrow") : null;
            float arrowHeight = arrow != null ? arrow.localPosition.y : 0f;
            float stood = 0f;
            while (stood < standSeconds)
            {
                if (Head == null && Camera.main != null) Head = Camera.main.transform;
                if (Head != null && Flat(Head.position - floor).magnitude <= standRadius) stood += Time.deltaTime;
                else stood = 0f;
                if (arrow != null && Head != null)
                {
                    arrow.localPosition = new Vector3(0f, arrowHeight + 0.08f * Mathf.Sin(Time.time * 3f), 0f);
                    var look = Flat(Head.position - arrow.position);
                    if (look.sqrMagnitude > 0.001f) arrow.rotation = Quaternion.LookRotation(look);
                }
                yield return null;
            }
            var facing = Flat(Head.forward);
            Front = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward;
            Looking = Front;
            middle = new Vector3(Head.position.x, floor.y, Head.position.z);
            if (mark != null)
            {
                Puff(floor + Vector3.up * 0.3f, 1f);
                Destroy(mark);
            }

            // The song starts; the buckets pop up round them, in a random order of colours; the hands can be painted.
            StartSong();
            hands = gameObject.AddComponent<PaintHands>();
            hands.Setup(colours, burstMaterial, paintFloor, FloorSplat);
            var order = new List<int>();
            for (int i = 0; i < colours.Length; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
            for (int i = 0; i < order.Count && i < bucketAngles.Length && bucket != null; i++)
            {
                var way = Quaternion.Euler(0f, bucketAngles[i], 0f) * Front;
                var at = middle + way * bucketDistance + Vector3.up * paintHeight;
                var made = Instantiate(bucket, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                made.Setup(order[i], colours[order[i]], at, riseSeconds);
                buckets.Add(made);
                Puff(new Vector3(at.x, floor.y + 0.1f, at.z), 0.7f);
                Play(bucketSound);
                yield return new WaitForSeconds(riseGap);
            }
            yield return new WaitForSeconds(riseSeconds);

            // Dip a hand, then 3-2-1 on the song's beats, from the next bar.
            Say("Dip your hands in the paint!");
            for (float t = 0f; t < 6f && !AnyPainted(); t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(1.2f);
            int beat = BeatAtOrAfter(SongTime + 0.3f);
            beat += (4 - beat % 4) % 4;
            // The road lights up, its lights marching in to the 3-2-1.
            if (road != null)
            {
                roadMade = Instantiate(road, middle, Quaternion.LookRotation(Front), transform);
                // Its lanes lead from the fires the bees fly out of.
                FindFires();
                if (fires.Count > 0) roadMade.LeadFrom(fires);
                roadMade.Setup(this);
            }
            foreach (var n in new[] { "3", "2", "1" })
            {
                while (SongTime < BeatTime(beat)) yield return null;
                Say(n);
                Play(tickSound);
                beat++;
            }
            while (SongTime < BeatTime(beat)) yield return null;
            Say("GO!");
            Play(goSound);

            // The bees, each lot on a beat, more often as it goes; a lot that would make too many waits a beat.
            var plan = Plan(beat + 1);
            total = plan.Count;
            PlayerBuffs.Add(Buff.CandyHelmet, lastBeeAt - SongTime + 15f);
            beesOut = true;
            int next = 0;
            bool cleared = false;
            while (next < plan.Count || live.Count > 0)
            {
                if (!cleared && SongTime > BeatTime(beat) + 0.4f) { Say(""); cleared = true; }
                live.RemoveAll(b => b == null);
                if (next < plan.Count && SongTime >= plan[next].at)
                {
                    int lot = plan[next].lot - plan[next].slot;
                    if (live.Count + lot <= mostAtOnce)
                    {
                        for (int i = 0; i < lot && next < plan.Count; i++, next++) SendBee(plan[next]);
                    }
                    else
                    {
                        // The rest move on so this lot comes on the next beat.
                        float later = BeatTime(BeatAtOrAfter(SongTime) + 1) - plan[next].at;
                        for (int i = next; i < plan.Count; i++)
                        {
                            var e = plan[i];
                            e.at += later;
                            plan[i] = e;
                        }
                    }
                }
                ShowCount();
                yield return null;
            }
            beesOut = false;
            if (roadMade != null) roadMade.Stop();

            // The end: the song fades, fireworks, the perfect round's candies (no words), the helmet eats the rest.
            bool perfect = Stings == 0;
            Say($"{Swatted} / {total}");
            if (perfect)
            {
                Play(perfectSound);
                StartCoroutine(Candies(middle + Front * 1.2f + Vector3.up * 1.4f, perfectCandies));
            }
            StartCoroutine(FadeSong());
            StartCoroutine(Fireworks());
            yield return new WaitForSeconds(1.5f);
            for (float waited = 0f; waited < candyWaitSeconds; waited += Time.deltaTime)
            {
                if (fountains == 0 && CandyHelmet.Flying == 0 && CandyHelmet.LyingAround(middle, CandyReach) == 0) break;
                if (!PlayerBuffs.CandyHelmet) PlayerBuffs.Give(Buff.CandyHelmet, candyWaitSeconds - waited);
                yield return null;
            }
            foreach (var b in buckets) if (b != null) b.Sink(0.5f);
            yield return new WaitForSeconds(0.6f);
            Say("");
            if (hands != null) Destroy(hands);
            Finished = true;
        }

        private bool AnyPainted()
        {
            if (hands == null) return false;
            foreach (var hand in hands.Hands) if (hand.colour != PaintHands.None) return true;
            return false;
        }

        // The stage's fire columns near the round (Boss_Area's Danger_Flames).
        private void FindFires()
        {
            fires.Clear();
            if (string.IsNullOrEmpty(fireName)) return;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.gameObject.activeInHierarchy || !t.name.StartsWith(fireName)) continue;
                // Its own top object, not the parts inside it.
                if (t.parent != null && t.parent.name.StartsWith(fireName)) continue;
                var gap = t.position - middle;
                gap.y = 0f;
                if (gap.magnitude <= fireReach) fires.Add(t);
            }
        }

        // One bee off, on its beat: from far in front, to hover in front of the face on its side, on a beat Fly Beats on
        // (a lot spread side by side, and diving one beat after another), diving Hover Beats later, at the face Dive
        // Beats after that.
        private void SendBee(Planned e)
        {
            if (bee == null || Head == null) return;
            e.colour = ColourForNextBee();
            float angle = Random.Range(-spawnArc, spawnArc);
            // Out of one of the fire columns (not the one used last); with none, around where the player looks now.
            fires.RemoveAll(f => f == null || !f.gameObject.activeInHierarchy);
            Vector3 from;
            if (fires.Count > 0)
            {
                int pick = Random.Range(0, fires.Count);
                if (pick == lastFire && fires.Count > 1) pick = (pick + 1 + Random.Range(0, fires.Count - 1)) % fires.Count;
                lastFire = pick;
                var fire = fires[pick];
                from = fire.position + Vector3.up * (Random.Range(fireHeight.x, fireHeight.y) * Mathf.Abs(fire.lossyScale.y));
                // Which side of the face it hovers on: the side its fire is on.
                var flat = from - Head.position;
                flat.y = 0f;
                angle = Vector3.SignedAngle(Looking, flat, Vector3.up);
                if (puffEffect != null) EffectPool.Play(puffEffect, from, 0.8f);
            }
            else
            {
                var way = Quaternion.Euler(0f, angle, 0f) * Looking;
                from = new Vector3(middle.x, Head.position.y, middle.z) + way * Random.Range(spawnDistance.x, spawnDistance.y) +
                       Vector3.up * Random.Range(spawnHeight.x, spawnHeight.y);
            }
            float side = Mathf.Sin(Mathf.Clamp(angle, -90f, 90f) * Mathf.Deg2Rad) * 0.25f + Random.Range(-0.1f, 0.1f) + (e.slot - (e.lot - 1) * 0.5f) * 0.32f;
            var hover = new Vector3(Mathf.Clamp(side, -0.55f, 0.55f), Random.Range(-0.15f, 0.08f), hoverDistance);
            var made = Instantiate(bee, from, Quaternion.LookRotation(Head.position - from), SpawnedStuff());
            if (e.colour < 0)
            {
                made.transform.localScale *= rainbowSize;
                Play(rainbowSound);
            }
            float arrive = e.at + Mathf.Round(Mathf.Lerp(flyBeats.x, flyBeats.y, e.progress)) * BeatSeconds;
            float dive = arrive + (Mathf.Round(Mathf.Lerp(hoverBeats.x, hoverBeats.y, e.progress)) + e.slot) * BeatSeconds;
            made.Launch(this, e.colour, hover, arrive, dive, dive + diveBeats * BeatSeconds);
            live.Add(made);
            Puff(from, 0.4f);
        }

        // The hands: dipped in a bucket they take its colour; moving fast into a bee they swat it (its colour) or knock
        // it back (the wrong one).
        private void Update()
        {
            Tick();
            if (roadMade != null) roadMade.Diving = live.Exists(b => b != null && b.Diving);
            if (hands == null) return;
            foreach (var hand in hands.Hands)
            {
                if (hand.fist == null) continue;
                var point = PaintHands.PointOf(hand);
                PaintBucket inside = null;
                foreach (var b in buckets)
                {
                    if (b != null && b.Dips(point)) { inside = b; break; }
                }
                dippedIn.TryGetValue(hand, out var was);
                dippedIn[hand] = inside;
                if (inside != null)
                {
                    // Going in (or into another colour): it takes the colour and splashes; held in, it stays fresh.
                    if (inside != was || hand.colour != inside.Colour)
                    {
                        hands.Paint(hand, inside.Colour);
                        inside.Splash(point);
                        Blobs(inside.PaintMiddle, inside.PaintColour, 10, 1.6f, true);
                        PlayAt(Pick(dipSounds), point, 1f);
                    }
                    else
                    {
                        hand.dippedAt = Time.time;
                        inside.Stir(point);
                    }
                }
                if (!beesOut || PaintHands.SpeedOf(hand) < swatSpeed) continue;
                for (int i = live.Count - 1; i >= 0; i--)
                {
                    var b = live[i];
                    if (b == null || b.Leaving) continue;
                    float reach = b.Radius + hand.fist.BallSize + 0.03f;
                    if ((b.transform.position - point).sqrMagnitude > reach * reach) continue;
                    if (hand.colour != PaintHands.None && (b.Rainbow || b.Colour == hand.colour)) Swat(b, hand);
                    else Boing(b, hand, point);
                }
            }
        }

        private void Swat(PaintBee b, PaintHands.Hand hand)
        {
            Swatted++;
            combo++;
            var at = b.transform.position;
            // On a beat: a gold flash and more candies (no words).
            float sinceBeat = Mathf.Repeat(SongTime - firstBeat, BeatSeconds);
            bool onBeat = Mathf.Min(sinceBeat, BeatSeconds - sinceBeat) <= onBeatWindow;
            if (onBeat)
            {
                OnBeat++;
                if (onBeatFlash != null) EffectPool.Play(onBeatFlash, at, onBeatFlashSize);
            }
            // The rainbow bee bursts into every colour.
            if (b.Rainbow)
            {
                for (int k = 0; k < 6; k++) Blobs(at, Color.HSVToRGB(k / 6f, 0.75f, 1f), 7, 3f, false);
            }
            else Blobs(at, ColourOf(b.Colour), 18, 3f, false);
            PlayAt(Pick(swatSounds), at, 1f + Mathf.Min(combo, 15) * 0.03f + (onBeat ? 0.08f : 0f));
            StartCoroutine(Candies(at, (b.Rainbow ? rainbowCandies : candiesPerBee) + (onBeat ? onBeatCandies : 0)));
            PlayerLevel.AddExp(b.Rainbow ? rainbowExp : expPerBee);
            live.Remove(b);
            boinged.Remove(b);
            Destroy(b.gameObject);
        }

        private void Boing(PaintBee b, PaintHands.Hand hand, Vector3 point)
        {
            if (boinged.TryGetValue(b, out float last) && Time.time - last < BoingWait) return;
            boinged[b] = Time.time;
            var away = b.transform.position - point;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : -Front;
            b.Bounce(away * bounceSpeed + hand.fist.HandVelocity * 0.4f);
            PlayAt(boingSound, b.transform.position, Random.Range(0.95f, 1.1f));
        }

        /// <summary>A bee reached the face: it stings (about 1 health), splats its paint on the view and flies off.</summary>
        public void Stung(PaintBee b)
        {
            if (b == null || b.Leaving) return;
            Stings++;
            combo = 0;
            boinged.Remove(b);
            PlayerDamageShare.Next(stingShare);
            HurtPlayer();
            Play(stingSound);
            StartCoroutine(Splat(ColourOf(b.Colour), b.Rainbow));
            live.Remove(b);
            b.FlyOff();
        }

        // The player's Damage FSM takes the hit as a bee's bite (Damage), with the share above.
        private static void HurtPlayer()
        {
            var player = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            if (player == null || player.Value == null) return;
            foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Damage") fsm.SendEvent("Damage");
            }
        }

        // Paint blobs on the view: a few, in front of the eyes, that slide down and fade (the rainbow bee's: each its own colour).
        private IEnumerator Splat(Color colour, bool rainbow)
        {
            if (splatMaterial == null || Head == null) yield break;
            var made = new List<(Transform blob, Vector3 at, float size, Color colour)>();
            var block = new MaterialPropertyBlock();
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var blob = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(blob.GetComponent<Collider>());
                blob.name = "Paint Splat";
                blob.transform.SetParent(Head, false);
                var at = new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.08f, 0.1f), 0.3f);
                float size = Random.Range(0.06f, 0.14f) * (i == 0 ? 1.6f : 1f);
                blob.transform.localPosition = at;
                blob.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                blob.GetComponent<Renderer>().sharedMaterial = splatMaterial;
                made.Add((blob.transform, at, size, rainbow ? Color.HSVToRGB((i / (float)count + Random.value * 0.1f) % 1f, 0.75f, 1f) : colour));
            }
            for (float t = 0f; t < splatSeconds; t += Time.deltaTime)
            {
                float k = t / splatSeconds;
                foreach (var (blob, at, size, tint) in made)
                {
                    if (blob == null) continue;
                    blob.localPosition = at + Vector3.down * (0.06f * k * k);
                    blob.localScale = new Vector3(size, size * (1f + 0.5f * k), size) * Mathf.Min(1f, t / 0.06f);
                    var c = tint;
                    c.a = 0.9f * (1f - k * k);
                    var part = blob.GetComponent<Renderer>();
                    part.GetPropertyBlock(block);
                    block.SetColor(BaseColor, c);
                    part.SetPropertyBlock(block);
                }
                yield return null;
            }
            foreach (var (blob, _, _, _) in made) if (blob != null) Destroy(blob.gameObject);
        }

        // Candies popping out of where a bee burst, one by one (they break if they start inside each other).
        private IEnumerator Candies(Vector3 at, int amount)
        {
            if (candies.Length == 0 || amount <= 0) yield break;
            fountains++;
            try
            {
                for (int i = 0; i < amount; i++)
                {
                    var prefab = candies[Random.Range(0, candies.Length)];
                    if (prefab == null) continue;
                    var out_ = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    var candy = Instantiate(prefab, at + out_ * 0.12f, Random.rotation, SpawnedStuff());
                    if (candy.TryGetComponent<Rigidbody>(out var body)) body.linearVelocity = out_ * 0.6f + Vector3.up * 1.6f;
                    yield return new WaitForSeconds(0.07f);
                }
            }
            finally { fountains--; }
        }

        // Two fireworks in front, one to each side, then one in the middle.
        private IEnumerator Fireworks()
        {
            if (winFireworks == null) yield break;
            var side = Vector3.Cross(Vector3.up, Front);
            var at = middle + Front * 4.5f;
            Firework(at + side * 2.2f);
            Firework(at - side * 2.2f);
            yield return new WaitForSeconds(1.4f);
            Firework(at + Front * 1f);
        }

        private void Firework(Vector3 at)
        {
            var show = Instantiate(winFireworks, at, Quaternion.identity, SpawnedStuff());
            show.transform.localScale = Vector3.one * fireworkScale;
            Destroy(show, 5f);
        }

        // ---------- Paint blobs ----------

        // One system for every burst and splash: each lot of blobs is tinted its own colour as it's let out.
        private ParticleSystem MakeBlobs()
        {
            var go = new GameObject("Paint Blobs");
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            // Long enough to reach the floor and splat there.
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f)));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = burstMaterial;
            var floorSplat = FloorSplat;
            if (floorSplat != null) PaintSplats.Add(system, paintFloor, floorSplat, 0.7f);
            return system;
        }

        private void Blobs(Vector3 at, Color colour, int count, float speed, bool upward)
        {
            if (blobs == null || burstMaterial == null) return;
            var emit = new ParticleSystem.EmitParams { startColor = colour, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                if (upward) dir = new Vector3(dir.x * 0.6f, Mathf.Abs(dir.y) + 0.6f, dir.z * 0.6f).normalized;
                emit.position = at;
                emit.velocity = dir * speed * Random.Range(0.4f, 1f);
                blobs.Emit(emit, 1);
            }
        }

        // ---------- The sign ----------

        private void MakeSign()
        {
            sign = WorldUI.CreatePanel("Paint Bees Sign", 520f, 120f, signScale, new Color(0.08f, 0.05f, 0.12f, 0.7f));
            var face = font != null ? font : TMP_Settings.defaultFontAsset;
            signText = WorldUI.CreateText("Text", sign, "", 64f, FontStyles.Bold, TextAlignmentOptions.Center, SignColor, face);
            WorldUI.Stretch(signText.rectTransform);
            signText.textWrappingMode = TextWrappingModes.NoWrap;
            signText.enableAutoSizing = true;
            signText.fontSizeMin = 24f;
            signText.fontSizeMax = 64f;
            signText.margin = new Vector4(12f, 0f, 12f, 0f);
            sign.gameObject.SetActive(false);
        }

        private void Say(string text)
        {
            if (sign == null) return;
            signText.text = text;
            sign.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        // How many are swatted, once "GO!" is gone (only when it changes).
        private void ShowCount()
        {
            if (sign == null || (sign.gameObject.activeSelf && shownCount < 0) || Swatted == shownCount) return;
            shownCount = Swatted;
            Say($"{Swatted} / {total}");
        }

        private void LateUpdate()
        {
            if (sign == null || !sign.gameObject.activeSelf || Head == null) return;
            sign.position = new Vector3(middle.x, Head.position.y + SignOverHead, middle.z) + Front * SignAhead;
            WorldUI.Face(sign, Head);
        }

        // ---------- Helpers ----------

        private void Play(AudioClip clip)
        {
            if (clip != null) voice.PlayOneShot(clip, volume);
        }

        private void PlayAt(AudioClip clip, Vector3 at, float pitch)
        {
            if (clip == null) return;
            var go = new GameObject("Paint Bees Sound");
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        private static AudioClip Pick(AudioClip[] clips) => clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

        private void Puff(Vector3 at, float size)
        {
            if (puffEffect != null) EffectPool.Play(puffEffect, at, size);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static Transform SpawnedStuff()
        {
            var stuff = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            return stuff != null && stuff.Value != null ? stuff.Value.transform : null;
        }
    }
}
