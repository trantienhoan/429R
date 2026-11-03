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
    /// - after a 3-2-1, Bees bees of the four colours (and one rainbow bee, Rainbow At of the way through) fly in from the
    ///   front, a few at a time and a little faster each, hover in front of the face (the warning) and dive at it;
    /// - a swat with the hand of its colour bursts it into paint: Candies Per Bee candies and Exp Per Bee EXP (the
    ///   rainbow bee: any painted hand, Rainbow Candies and Rainbow Exp); the wrong colour (or a clean hand) knocks it
    ///   back with a boing and it comes again; one that reaches the face stings (Sting Share of a bite: about 1 health)
    ///   and splats its paint on the view;
    /// - the Candy Helmet is on the whole time (every candy flies to the head); at the end fireworks go up in front, a
    ///   round with no sting gives Perfect Candies more, the helmet eats what's left, the buckets sink, the hands are
    ///   clean again, and the round is over (Finished: Play Bonus Round goes on).
    /// Tools > 429 Game > Bonus > Set Up Paint Bees Bonus builds it and its parts.
    /// </summary>
    [DisallowMultipleComponent]
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
            new(1f, 0.2f, 0.18f), new(0.2f, 0.45f, 1f), new(1f, 0.85f, 0.12f), new(0.25f, 0.9f, 0.3f),
        };
        [Tooltip("How far from the player the buckets float, in metres...")]
        [Min(0.2f)]
        [SerializeField] private float bucketDistance = 0.62f;
        [Tooltip("...at these angles from where they face when they step on the ring (degrees; one per colour).")]
        [SerializeField] private float[] bucketAngles = { -105f, -35f, 35f, 105f };
        [Tooltip("How high over the floor their paint is, in metres (waist height).")]
        [Min(0.2f)]
        [SerializeField] private float paintHeight = 0.85f;
        [Tooltip("Seconds each takes to pop up, and between one and the next.")]
        [Min(0.05f)]
        [SerializeField] private float riseSeconds = 0.6f;
        [Min(0f)]
        [SerializeField] private float riseGap = 0.15f;
        [Tooltip("A hand dipped again in the same colour splashes again after this many seconds.")]
        [Min(0f)]
        [SerializeField] private float redipSeconds = 0.6f;

        [Header("Bees")]
        [SerializeField] private PaintBee bee;
        [Tooltip("How many bees of the four colours come (the rainbow bee on top).")]
        [Min(1)]
        [SerializeField] private int bees = 40;
        [Tooltip("The rainbow bee comes this share of the way through. 0 = none.")]
        [Range(0f, 1f)]
        [SerializeField] private float rainbowAt = 0.7f;
        [Tooltip("The rainbow bee is this many times bigger.")]
        [Min(0.5f)]
        [SerializeField] private float rainbowSize = 1.3f;
        [Tooltip("Seconds between one lot of bees and the next, at the start (x) and the end (y).")]
        [SerializeField] private Vector2 spawnEvery = new(1.9f, 1.0f);
        [Tooltip("Never more bees about than this.")]
        [Min(1)]
        [SerializeField] private int mostAtOnce = 5;
        [Tooltip("They come from this far away (x to y metres)...")]
        [SerializeField] private Vector2 spawnDistance = new(5.5f, 7f);
        [Tooltip("...within this many degrees either side of the front...")]
        [Range(0f, 120f)]
        [SerializeField] private float spawnArc = 60f;
        [Tooltip("...and this high over the head (x to y metres).")]
        [SerializeField] private Vector2 spawnHeight = new(-0.3f, 1.2f);
        [Tooltip("How fast they fly in, at the start (x) and the end (y), metres a second.")]
        [SerializeField] private Vector2 approachSpeed = new(1.8f, 3.2f);
        [Tooltip("How long they hover in front of the face before diving, at the start (x) and the end (y), seconds.")]
        [SerializeField] private Vector2 hoverSeconds = new(0.9f, 0.45f);
        [Tooltip("How fast they dive, at the start (x) and the end (y), metres a second.")]
        [SerializeField] private Vector2 diveSpeed = new(2.2f, 3.2f);
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
        [Min(0)]
        [SerializeField] private int expPerBee = 1;
        [Min(0)]
        [SerializeField] private int rainbowExp = 5;
        [Tooltip("At the end the helmet eats every candy left: for this many seconds at the most.")]
        [Min(0f)]
        [SerializeField] private float candyWaitSeconds = 20f;

        [Header("Stings")]
        [Tooltip("A sting does this share of a bee's bite (Damage: 1-3 health), so about 1 health.")]
        [Range(0f, 1f)]
        [SerializeField] private float stingShare = 0.5f;
        [Tooltip("The paint blobs splatted on the view (the round tints them its colour).")]
        [SerializeField] private Material splatMaterial;
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

        /// <summary>Bees swatted, and stings taken.</summary>
        public int Swatted { get; private set; }
        public int Stings { get; private set; }

        private PaintHands hands;
        private readonly List<PaintBucket> buckets = new();
        private readonly List<PaintBee> live = new();
        private readonly Dictionary<PaintBee, float> boinged = new();
        private Vector3 middle;
        private bool beesOut;
        private int combo;
        private int total;
        private AudioSource voice;
        private ParticleSystem blobs;
        private RectTransform sign;
        private TMP_Text signText;
        private int fountains;

        /// <summary>A paint colour (index into Colours).</summary>
        public Color ColourOf(int index) => index >= 0 && index < colours.Length ? colours[index] : Color.white;

        private void Start()
        {
            Head = Camera.main != null ? Camera.main.transform : null;
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
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
            middle = new Vector3(Head.position.x, floor.y, Head.position.z);
            if (mark != null)
            {
                Puff(floor + Vector3.up * 0.3f, 1f);
                Destroy(mark);
            }

            // The buckets pop up round them, in a random order of colours; the hands can be painted.
            hands = gameObject.AddComponent<PaintHands>();
            hands.Setup(colours, burstMaterial);
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

            // Dip a hand, then 3-2-1.
            Say("Dip your hands in the paint!");
            for (float t = 0f; t < 6f && !AnyPainted(); t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(1.2f);
            foreach (var n in new[] { "3", "2", "1" })
            {
                Say(n);
                Play(tickSound);
                yield return new WaitForSeconds(0.8f);
            }
            Say("GO!");
            Play(goSound);

            // The bees, a few at a time, each lot a little faster.
            total = bees + (rainbowAt > 0f ? 1 : 0);
            int rainbowIndex = rainbowAt > 0f ? Mathf.Clamp(Mathf.RoundToInt(bees * rainbowAt), 0, total - 1) : -1;
            PlayerBuffs.Add(Buff.CandyHelmet, 90f);
            beesOut = true;
            int spawned = 0;
            float next = Time.time + 0.6f;
            bool cleared = false;
            while (spawned < total || live.Count > 0)
            {
                if (!cleared && Time.time > next - 0.2f) { Say(""); cleared = true; }
                live.RemoveAll(b => b == null);
                if (spawned < total && Time.time >= next && live.Count < mostAtOnce)
                {
                    float p = total > 1 ? spawned / (float)(total - 1) : 1f;
                    int lot = 1;
                    if (p >= 0.25f && Random.value < (p < 0.6f ? 0.4f : 0.55f)) lot = p >= 0.6f && Random.value < 0.45f ? 3 : 2;
                    lot = Mathf.Min(lot, total - spawned, mostAtOnce - live.Count);
                    for (int i = 0; i < lot; i++)
                    {
                        bool rainbow = spawned == rainbowIndex;
                        SendBee(rainbow ? -1 : Random.Range(0, colours.Length), p, i, lot);
                        spawned++;
                    }
                    next = Time.time + Mathf.Lerp(spawnEvery.x, spawnEvery.y, p) + 0.35f * (lot - 1);
                }
                ShowCount();
                yield return null;
            }
            beesOut = false;

            // The end: fireworks, the perfect round's candies, the helmet eats the rest.
            bool perfect = Stings == 0;
            Say(perfect ? "PERFECT!" : $"{Swatted} / {total} bees!");
            if (perfect)
            {
                Play(perfectSound);
                StartCoroutine(Candies(middle + Front * 1.2f + Vector3.up * 1.4f, perfectCandies));
            }
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

        // One bee off: from far in front, to hover in front of the face on its side (a lot spread side by side, and
        // diving one after the other).
        private void SendBee(int colour, float progress, int slot, int lot)
        {
            if (bee == null || Head == null) return;
            float angle = Random.Range(-spawnArc, spawnArc);
            var way = Quaternion.Euler(0f, angle, 0f) * Front;
            var from = new Vector3(middle.x, Head.position.y, middle.z) + way * Random.Range(spawnDistance.x, spawnDistance.y) +
                       Vector3.up * Random.Range(spawnHeight.x, spawnHeight.y);
            float side = Mathf.Sin(angle * Mathf.Deg2Rad) * 0.25f + Random.Range(-0.1f, 0.1f) + (slot - (lot - 1) * 0.5f) * 0.32f;
            var hover = new Vector3(Mathf.Clamp(side, -0.55f, 0.55f), Random.Range(-0.15f, 0.08f), hoverDistance);
            var made = Instantiate(bee, from, Quaternion.LookRotation(Head.position - from), SpawnedStuff());
            if (colour < 0)
            {
                made.transform.localScale *= rainbowSize;
                Play(rainbowSound);
            }
            made.Launch(this, colour, Mathf.Lerp(approachSpeed.x, approachSpeed.y, progress),
                Mathf.Lerp(hoverSeconds.x, hoverSeconds.y, progress) + slot * 0.35f, Mathf.Lerp(diveSpeed.x, diveSpeed.y, progress), hover);
            live.Add(made);
            Puff(from, 0.4f);
        }

        // The hands: dipped in a bucket they take its colour; moving fast into a bee they swat it (its colour) or knock
        // it back (the wrong one).
        private void Update()
        {
            if (hands == null) return;
            foreach (var hand in hands.Hands)
            {
                if (hand.fist == null) continue;
                var point = PaintHands.PointOf(hand);
                foreach (var b in buckets)
                {
                    if (b == null || !b.Dips(point)) continue;
                    if (hand.colour == b.Colour && Time.time - hand.dippedAt < redipSeconds) continue;
                    bool fresh = hand.colour != b.Colour || Time.time - hand.dippedAt >= redipSeconds;
                    hands.Paint(hand, b.Colour);
                    if (!fresh) continue;
                    b.Splash();
                    Blobs(b.PaintMiddle, b.PaintColour, 10, 1.6f, true);
                    PlayAt(Pick(dipSounds), point, 1f);
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
            var colour = b.Rainbow ? hands.ColourOf(hand) : ColourOf(b.Colour);
            Blobs(at, colour, b.Rainbow ? 40 : 18, 3f, false);
            PlayAt(Pick(swatSounds), at, 1f + Mathf.Min(combo, 15) * 0.03f);
            StartCoroutine(Candies(at, b.Rainbow ? rainbowCandies : candiesPerBee));
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
            PlayerDamageShare.Next(stingShare);
            HurtPlayer();
            Play(stingSound);
            StartCoroutine(Splat(b.Rainbow ? Color.HSVToRGB(Random.value, 0.75f, 1f) : ColourOf(b.Colour)));
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

        // Paint blobs on the view: a few, in front of the eyes, that slide down and fade.
        private IEnumerator Splat(Color colour)
        {
            if (splatMaterial == null || Head == null) yield break;
            var made = new List<(Transform blob, Vector3 at, float size)>();
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
                made.Add((blob.transform, at, size));
            }
            for (float t = 0f; t < splatSeconds; t += Time.deltaTime)
            {
                float k = t / splatSeconds;
                foreach (var (blob, at, size) in made)
                {
                    if (blob == null) continue;
                    blob.localPosition = at + Vector3.down * (0.06f * k * k);
                    blob.localScale = new Vector3(size, size * (1f + 0.5f * k), size) * Mathf.Min(1f, t / 0.06f);
                    var c = colour;
                    c.a = 0.9f * (1f - k * k);
                    var part = blob.GetComponent<Renderer>();
                    part.GetPropertyBlock(block);
                    block.SetColor(BaseColor, c);
                    part.SetPropertyBlock(block);
                }
                yield return null;
            }
            foreach (var (blob, _, _) in made) if (blob != null) Destroy(blob.gameObject);
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
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

        private void ShowCount()
        {
            if (sign == null || (sign.gameObject.activeSelf && !signText.text.Contains("/") && signText.text.Length > 0)) return;
            Say($"{Swatted} / {total}");
        }

        private void LateUpdate()
        {
            if (sign == null || !sign.gameObject.activeSelf || Head == null) return;
            var at = middle == Vector3.zero && !beesOut ? transform.position : middle;
            sign.position = new Vector3(at.x, Head.position.y + SignOverHead, at.z) + Front * SignAhead;
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
