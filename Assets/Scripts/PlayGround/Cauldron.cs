using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Game.Combat;
using Game.Enemies;
using Game.Inventory;
using Unity.XR.CoreUtils;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.PlayGround
{
    /// <summary>
    /// The PlayGround's witch cauldron. It cooks a few dishes in a row (Rounds), each with its own recipe of pumpkins
    /// (Pumpkin_On_Tree) and random candies (the 6 in Candies); the magic hat shows the current one. Ingredients dropped
    /// or thrown into the pot are taken in with a puff of green smoke. Above the pot, while ingredients go in (and for a
    /// few seconds after a hit), the recipe shows: only "??? x ?" for each ingredient until the player has read it off
    /// the hat, then what's in the pot and what's still missing. Its surface always bubbles. The moment the pot is
    /// full it checks the recipe. Right: it cooks (Cooking Started, the round's wave starts): the surface boils, a tower
    /// of bubbles rises out of it and a meter fills; done, it spits the round's laughing pumpkin out to the middle of
    /// the PlayGround, and a small bubble plume stays until the pumpkin is caught. Wrong: it boils a moment and spits
    /// out a big slime (Slime_Big) next to it, in the colour of a candy the recipe still needs (Candy Colours), so it
    /// drops that candy; the magic hat shakes, and the same recipe can be tried again at once.
    /// Firewood thrown into the fire under it makes the fire roar and the cooking go faster for a while. A punch or a
    /// hit with a weapon rings its bell, shows the recipe above it for a few seconds and lets a wild slime out of the
    /// pot (candy, no EXP); a head over the pot is pushed back. With a PlayGround Rounds director next to it, the next
    /// round waits until the pumpkin is caught and the chest's seed taken (Continue). After the last round comes the
    /// candy pot: it shakes and rings by itself, letting slimes out, until enough candies are in; then it cooks, swells
    /// and bursts (Pot Broken). It stands fixed on the floor, upright, wherever it appears. Goes on
    /// Fire_Woods_n_Cauldron's root; it makes its own zones, panels and pot collider when the game runs.
    /// </summary>
    [DisallowMultipleComponent]
    public class Cauldron : MonoBehaviour
    {
        /// <summary>One line of a recipe: how many of which ingredient.</summary>
        [Serializable]
        public class Line
        {
            public string id;
            public int count;
            public Sprite icon;
        }

        /// <summary>One dish: what its recipe asks for, what it makes when cooked right, and how long it cooks.</summary>
        [Serializable]
        public class Round
        {
            [Tooltip("How many pumpkins (Pumpkin_On_Tree) the recipe asks for.")]
            [Min(0)] public int pumpkins = 1;
            [Tooltip("How many candies the recipe asks for; each is picked at random.")]
            [FormerlySerializedAs("lollipops")]
            [Min(0)] public int candies = 3;
            [Tooltip("What it makes when cooked right, e.g. Pumpkin_8.")]
            public GameObject result;
            [Tooltip("How many of them jump out.")]
            [Min(1)] public int results = 1;
            [Tooltip("Seconds this dish cooks with no extra firewood. 0 = the cauldron's Cook Seconds.")]
            [Min(0f)] public float cookSeconds;
        }

        public enum Zone
        {
            Mouth,
            Fire,
        }

        [Header("Parts")]
        [Tooltip("The pot. Without a collider of its own, it gets one shaped like it.")]
        [SerializeField] private Transform pot;
        [Tooltip("The bubbling on the surface; its child effects (coloured bubbles, skulls, glow) go along.")]
        [SerializeField] private ParticleSystem surface;
        [Tooltip("The fire under the pot.")]
        [SerializeField] private ParticleSystem fire;
        [Tooltip("Where ingredients go in: the pot's opening, in the cauldron's own space.")]
        [SerializeField] private Vector3 mouthCenter = new(0f, 1.2f, 0f);
        [SerializeField] private Vector3 mouthSize = new(1.1f, 0.4f, 1.1f);
        [Tooltip("Where firewood burns: the fire under the pot, in the cauldron's own space.")]
        [SerializeField] private Vector3 fireCenter = new(0.08f, 0.3f, -0.05f);
        [SerializeField] private Vector3 fireSize = new(1.9f, 0.8f, 1.9f);
        [Tooltip("When it appears it turns upright and drops onto the floor below, then stays fixed there: nothing can " +
                 "push, tip or knock it off (its Rigidbody is made kinematic).")]
        [SerializeField] private bool stickToGround = true;
        [Tooltip("Played when it lands on the floor.")]
        [SerializeField] private AudioClip landSound;
        [Tooltip("How much faster the surface bubbles while cooking (it boils): 2 = twice as fast. More with a hot fire.")]
        [Min(1f)]
        [SerializeField] private float cookingBubbles = 1.6f;

        [Header("Rounds (cooked one after another; new random candies each time the PlayGround opens)")]
        [Tooltip("The dishes it cooks, in order. A wrong recipe doesn't count: that round's recipe stays until it's " +
                 "cooked right. After the last one it takes nothing more (or opens the candy pot).")]
        [SerializeField] private Round[] rounds =
        {
            new() { pumpkins = 1, candies = 3, cookSeconds = 30f },
            new() { pumpkins = 1, candies = 4, cookSeconds = 45f },
            new() { pumpkins = 1, candies = 5, cookSeconds = 60f },
        };
        [Tooltip("The candies a recipe can ask for (the 6 in the game: Candy_1, 2, 4, 5, 7, 8); each needs a Cooking " +
                 "Ingredient.")]
        [FormerlySerializedAs("lollipops")]
        [SerializeField] private GameObject[] candies = Array.Empty<GameObject>();
        [Tooltip("Most different candies one recipe asks for, so it fits on the card.")]
        [Min(1)] [SerializeField] private int maxKinds = 3;
        [Tooltip("The pumpkin recipes ask for (Pumpkin_On_Tree).")]
        [SerializeField] private GameObject pumpkin;
        [Tooltip("Each new round's recipe has to be read off the magic hat again before it shows above the cauldron " +
                 "(until then it shows as '??? x ?').")]
        [SerializeField] private bool hatEachRound = true;

        [Header("Taking in")]
        [Tooltip("Puff at the pot each time an ingredient goes in, e.g. smoke; it's tinted Add Effect Color.")]
        [SerializeField] private GameObject addEffect;
        [SerializeField] private Color addEffectColor = new(0.35f, 1f, 0.3f, 1f);
        [Tooltip("Size of the puff. The Hyper Casual FX smoke is about 13 m wide, so 0.07 makes it about 0.9 m.")]
        [Min(0.01f)] [SerializeField] private float addEffectScale = 0.07f;

        [Header("Cooking")]
        [Tooltip("Seconds a dish cooks with no extra firewood, when its round has no Cook Seconds of its own.")]
        [Min(1f)] [SerializeField] private float cookSeconds = 30f;
        [Tooltip("Most heat the fire can hold; each log adds its Heat. 1 heat = cooking twice as fast.")]
        [Min(0f)] [SerializeField] private float maxHeat = 3f;
        [Tooltip("Heat the fire loses each second.")]
        [Min(0f)] [SerializeField] private float heatFade = 0.05f;
        [SerializeField] private AudioClip addSound;
        [SerializeField] private AudioClip fuelSound;

        [Header("Done")]
        [Tooltip("Played when a right recipe is done; one at random.")]
        [SerializeField] private AudioClip[] laughs = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip successSound;
        [Tooltip("Optional: a GAMESTAGES bool that turns true once it has cooked its last round, for the PlayGround's " +
                 "FSMs. Add it to the GAMESTAGES FSM's Variables.")]
        [SerializeField] private string cookedBool;
        [Tooltip("What a wrong recipe spits out (Slime_Big), and how many. It lands Spit Distance away on the NavMesh, " +
                 "not on the player, with a puff (one with a loose Rigidbody flies there in an arc instead).")]
        [FormerlySerializedAs("badPumpkin")]
        [SerializeField] private GameObject wrongResult;
        [Tooltip("The candy colour table (Candy_Colours.asset; the Candy Colours menu sets it): a wrong recipe's slime " +
                 "comes in the colour of a candy the recipe still needs, so it drops that candy (user, 2026-11-10). " +
                 "Empty: it keeps its own colour.")]
        [SerializeField] private CandyColours candyColours;
        [Min(0)] [SerializeField] private int minBadPumpkins = 1;
        [Min(0)] [SerializeField] private int maxBadPumpkins = 1;
        [Tooltip("Seconds a wrong recipe boils before it's spat out (no meter, no wave).")]
        [Min(0f)] [SerializeField] private float wrongBoilSeconds = 2.5f;
        [Tooltip("How far from the cauldron it lands, in metres.")]
        [Min(0.5f)] [SerializeField] private float spitDistance = 2.5f;
        [SerializeField] private AudioClip failSound;
        [Tooltip("Burst at the pot when it finishes cooking, e.g. smoke (also where a wrong recipe's slime lands).")]
        [SerializeField] private GameObject doneEffect;
        [Tooltip("Its scale (replaces the prefab's own: Smoke_Flash_Small_Puff's root is 3).")]
        [Min(0.01f)] [SerializeField] private float doneEffectScale = 2.5f;

        [Header("Pumpkin out")]
        [Tooltip("A right recipe's pumpkin is spat out in an arc to the middle of the PlayGround (its Ghost Roam " +
                 "Area); without one it lands Spit Distance away.")]
        [SerializeField] private bool spitToMiddle = true;

        [Header("Bubble tower")]
        [Tooltip("A column of bubbles out of the pot (Magical_Pot/Bubble_Tower, made by Set Up Cauldron): it rises " +
                 "while a dish cooks, stays small from the spit until the pumpkin is caught, then stops.")]
        [SerializeField] private ParticleSystem bubbleTower;
        [Tooltip("How big it is (a share of its rate and speed) when cooking starts, and from the spit until the catch.")]
        [Range(0.05f, 1f)] [SerializeField] private float towerSmall = 0.4f;

        [Header("Push back")]
        [Tooltip("A head closer than this to the pot's middle, along the floor, is pushed back out, in metres. Never " +
                 "while the player holds an ingredient, a candy or firewood, or is down.")]
        [Min(0f)] [SerializeField] private float pushRadius = 0.55f;
        [Tooltip("How far the push moves the player along the floor, in metres (no shake, no buzz).")]
        [Min(0f)] [SerializeField] private float pushDistance = 0.6f;
        [Tooltip("Seconds before it can push again.")]
        [Min(0f)] [SerializeField] private float pushCooldown = 2f;
        [Tooltip("The player's Toughness cuts the push, but never below this share of it (1 = always the full push).")]
        [Range(0f, 1f)] [SerializeField] private float pushMinShare = 1f;

        [Header("Bell")]
        [Tooltip("Rung when it's punched, or hit with a held or thrown weapon (never an ingredient, candy, pumpkin or " +
                 "log); one at random, played as it is.")]
        [SerializeField] private AudioClip[] bellSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)] [SerializeField] private float bellVolume = 1f;
        [Tooltip("What a ring lets out of the pot, e.g. PG_White_Slime. They give candy but no EXP. None while it cooks.")]
        [SerializeField] private GameObject[] wildSlimes = Array.Empty<GameObject>();
        [Tooltip("Most of its slimes about at once.")]
        [Min(0)] [SerializeField] private int maxWildSlimes = 3;
        [Tooltip("Seconds after a slime before a ring can let another one out.")]
        [Min(0f)] [SerializeField] private float slimeCooldown = 3f;
        [Tooltip("How fast a held or thrown weapon has to hit it to ring it, in m/s (a fist rings on any real punch).")]
        [Min(0f)] [SerializeField] private float bellHitSpeed = 1.5f;
        [Tooltip("Seconds the recipe shows above the pot after a punch or a weapon hit: '??? x ?' for each ingredient " +
                 "until the magic hat has been read, then the real recipe (even with nothing in yet). 0 = hits show nothing.")]
        [Min(0f)] [SerializeField] private float hitShowSeconds = 6f;

        [Header("Last: the candy pot")]
        [Tooltip("After the last round it asks for this many candies (any of Candies; nothing else counts), shaking and " +
                 "ringing by itself. Then it cooks, swells up and bursts (Pot Broken). 0 = done after the last round.")]
        [Min(0)] [SerializeField] private int mysteryCount = 8;
        [Tooltip("Seconds the candy pot cooks (fire still speeds it up); it shakes harder meanwhile.")]
        [Min(1f)] [SerializeField] private float mysteryCookSeconds = 3f;
        [Tooltip("Seconds between its rings while it waits for candies (random between); each can let a slime out.")]
        [SerializeField] private Vector2 finaleBellEvery = new(4f, 6f);
        [Tooltip("Optional: left where it stood when it bursts. None in the PlayGround (the Daruma game gives the Green Seed).")]
        [SerializeField] private GameObject finalSeed;
        [Tooltip("Candies that fly out when it bursts; each picked at random from Candies.")]
        [Min(0)] [SerializeField] private int finalCandies;
        [Tooltip("Burst when it breaks, e.g. smoke (Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject breakEffect;
        [Tooltip("Its scale (replaces the prefab's own: Smoke_Flash_Small_Puff's root is 3).")]
        [Min(0.01f)] [SerializeField] private float breakEffectScale = 6f;
        [SerializeField] private AudioClip swellSound;
        [SerializeField] private AudioClip breakSound;

        [Header("Meter")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("How far above the pot's rim the meter floats, in metres.")]
        [SerializeField] private float meterHeight = 0.5f;

        /// <summary>Raised when a wrong recipe has boiled and its slime is spat out (the magic hat shakes).</summary>
        public static event Action<Cauldron> CookFailed;
        /// <summary>Raised when a right recipe finishes cooking, once for each thing it made.</summary>
        public static event Action<Cauldron, GameObject> Cooked;
        /// <summary>Raised when it moves on to the next round's recipe, or has cooked them all.</summary>
        public static event Action<Cauldron> RecipeChanged;
        /// <summary>Raised when a round's right recipe starts cooking (never a wrong one, never the candy pot).</summary>
        public static event Action<Cauldron> CookingStarted;
        /// <summary>Raised when the candy pot bursts (the cauldron is gone 3 s later).</summary>
        public static event Action<Cauldron> PotBroken;

        // Things it spawns go here, so the stage clean-up removes them.
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        private const float MeterWidth = 320f;
        private const float MeterPixelsHigh = 86f;
        private const float MeterScale = 0.0018f;
        // Seconds something with a loose Rigidbody is in the air when a wrong recipe spits it out.
        private const float SpitSeconds = 1.1f;
        // A wrong recipe's slime lands at least this far from the player's head, along the floor, when it can.
        private const float KeepFromPlayer = 1.8f;
        // Shortest gap between two bell sounds, in seconds.
        private const float BellGap = 0.25f;
        // The candy pot's first ring, this many seconds after it opens.
        private const float FirstFinaleBell = 3f;
        // Seconds the player's body passes through the pot while pushed out of it.
        private const float PushPassSeconds = 0.6f;
        // How far the candy pot's pot shakes (in its own space) and tips (degrees); harder while it cooks.
        private const float ShakeDistance = 0.04f;
        private const float ShakeTilt = 6f;
        private const float ShakeCooking = 2.2f;
        private static readonly Color MeterBack = new(0.1f, 0.07f, 0.16f, 0.85f);
        private static readonly Color MeterFill = new(0.55f, 1f, 0.35f, 1f);
        private static readonly Color MeterHot = new(1f, 0.55f, 0.15f, 1f);
        private static readonly Color TooMany = new(1f, 0.4f, 0.35f, 1f);
        // The recipe above the pot: one column per ingredient, its picture over "have/need".
        private const float ColumnWidth = 96f;
        private const float ColumnIcon = 64f;
        private const float ColumnCount = 38f;
        private const float PanelPad = 12f;
        private static readonly List<Cauldron> cauldrons = new();

        private readonly List<Line> recipe = new();
        private readonly Dictionary<string, int> contents = new();
        private readonly HashSet<GameObject> taken = new();
        private readonly HashSet<string> candyIds = new();
        private readonly List<GameObject> wild = new();
        private readonly List<XRBaseInputInteractor> hands = new();
        private ParticleSystem[] surfaces = Array.Empty<ParticleSystem>();
        private float[] surfaceRates = Array.Empty<float>();
        private bool falling;
        private float fallSpeed;
        private float groundHeight;
        private int count;
        private bool cooking;
        private bool boiling;
        private float progress;
        private float heat;
        private float fireRate;
        private float fireSizeBase;
        private RectTransform meter;
        private RectTransform meterBar;
        private Image meterFillImage;
        private TMP_Text meterText;
        private RectTransform recipePanel;
        private Transform head;
        private int round;
        private bool recipeKnown;
        private bool waitingForNext;
        private bool potBroken;
        private bool pumpkinCaught;
        private Sprite candyIcon;
        private float towerRate;
        private ParticleSystem.MinMaxCurve towerSpeed;
        private float towerLevel = -1f;
        private Vector3 potHome;
        private Quaternion potRest = Quaternion.identity;
        private bool potShaken;
        private XROrigin player;
        private CharacterController playerBody;
        private float nextPush;
        private float nextSlime;
        private float nextBellSound;
        private float nextFinaleBell;
        // A hit keeps the recipe panel up until then (0 = no hit showing it).
        private float hitShowUntil;

        public IReadOnlyList<Line> Recipe => recipe;
        public bool IsCooking => cooking;
        /// <summary>A wrong recipe is boiling, about to be spat out as a slime.</summary>
        public bool IsBoiling => boiling;
        public float Heat => heat;
        /// <summary>Which round it's on, from 0; equals RoundCount once it has cooked them all.</summary>
        public int RoundIndex => round;
        public int RoundCount => rounds.Length;
        /// <summary>It has cooked every round (the candy pot may still be to come).</summary>
        public bool AllCooked => round >= rounds.Length;
        /// <summary>After the last round, the candy pot: asking for candies (only candies count), until it bursts.</summary>
        public bool InMystery => AllCooked && mysteryCount > 0 && !potBroken;
        public int MysteryCount => mysteryCount;
        public bool IsBroken => potBroken;
        /// <summary>
        /// Set by a PlayGround Rounds director: after a right recipe the next round waits until it calls Continue()
        /// (the player has caught the pumpkin and taken the chest's seed). Takes nothing in meanwhile.
        /// </summary>
        public bool HoldBetweenRounds { get; set; }
        public bool WaitingForNextRound => waitingForNext;
        /// <summary>The player has read this round's recipe off the magic hat, so it shows above the pot (not "???").</summary>
        public bool RecipeKnown => recipeKnown;
        /// <summary>The middle of the pot's opening.</summary>
        public Vector3 Mouth => MouthPoint();

        /// <summary>The cauldron nearest to 'point', within 'reach' metres; null if there's none.</summary>
        public static Cauldron Nearest(Vector3 point, float reach)
        {
            Cauldron nearest = null;
            float best = reach * reach;
            foreach (var cauldron in cauldrons)
            {
                float distance = (cauldron.transform.position - point).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = cauldron;
            }
            return nearest;
        }

        /// <summary>How many ingredients the recipe asks for, in all.</summary>
        public int RecipeTotal
        {
            get
            {
                int total = 0;
                foreach (var line in recipe) total += line.count;
                return total;
            }
        }

        // This round's cook time: its own, or Cook Seconds.
        private float RoundCookSeconds =>
            round < rounds.Length && rounds[round] != null && rounds[round].cookSeconds > 0f ? Mathf.Max(1f, rounds[round].cookSeconds) : cookSeconds;

        private void Awake()
        {
            MakeZone(Zone.Mouth, "Cauldron Mouth", mouthCenter, mouthSize);
            MakeZone(Zone.Fire, "Cauldron Fire", fireCenter, fireSize);
            if (pot != null && pot.GetComponentInChildren<Collider>() == null && pot.TryGetComponent<MeshFilter>(out var shape))
                pot.gameObject.AddComponent<MeshCollider>().sharedMesh = shape.sharedMesh;
            if (pot != null)
            {
                potHome = pot.localPosition;
                potRest = pot.localRotation;
            }
            // What counts as a candy for the candy pot, and the picture on its card.
            foreach (var candy in candies)
            {
                if (candy == null) continue;
                var ingredient = candy.GetComponent<CookingIngredient>();
                candyIds.Add(ingredient != null ? ingredient.Id : CookingIngredient.BaseName(candy.name));
                if (candyIcon == null && ingredient != null) candyIcon = ingredient.Icon;
            }
            MakeRecipe();
        }

        private void Start()
        {
            if (stickToGround) Land();
            if (surface != null)
            {
                // Always bubbling; busier while it cooks (see Update).
                surfaces = surface.GetComponentsInChildren<ParticleSystem>(true);
                surfaceRates = new float[surfaces.Length];
                for (int i = 0; i < surfaces.Length; i++) surfaceRates[i] = surfaces[i].emission.rateOverTimeMultiplier;
                surface.Play(true);
            }
            if (fire != null)
            {
                fireRate = fire.emission.rateOverTimeMultiplier;
                var main = fire.main;
                fireSizeBase = main.startSizeMultiplier;
                main.maxParticles = Mathf.Max(main.maxParticles, 80);
            }
            if (bubbleTower != null)
            {
                // Its full size as set up; off until a dish cooks.
                towerRate = bubbleTower.emission.rateOverTimeMultiplier;
                towerSpeed = bubbleTower.main.startSpeed;
                SetTower(0f);
            }
        }

        private void OnEnable()
        {
            cauldrons.Add(this);
            Fist.Landed += OnPunchLanded;
        }

        private void OnDisable()
        {
            cauldrons.Remove(this);
            Fist.Landed -= OnPunchLanded;
        }

        private void OnDestroy()
        {
            if (meter != null) Destroy(meter.gameObject);
            if (recipePanel != null) Destroy(recipePanel.gameObject);
        }

        /// <summary>The magic hat showed the recipe: from now on the panel above the pot shows it, not "???".</summary>
        public void RevealRecipe()
        {
            if (recipeKnown) return;
            recipeKnown = true;
            RefreshRecipePanel();
        }

        /// <summary>
        /// The PlayGround Rounds director: this round's pumpkin is caught (or turned into the boss), so the bubble plume
        /// stops until the next round's dish cooks.
        /// </summary>
        public void PumpkinCaught()
        {
            pumpkinCaught = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            heat = Mathf.Max(0f, heat - heatFade * dt);
            UpdateFire();

            if (falling) Fall(dt);
            UpdateSurface();
            if (cooking)
            {
                progress += dt / (InMystery ? mysteryCookSeconds : RoundCookSeconds) * (1f + heat);
                if (progress >= 1f) Finish();
            }
            UpdateTower();
            ShakePot();
            // The candy pot rings by itself every few seconds, letting slimes out (their candy can feed it).
            if (InMystery && !cooking && Time.time >= nextFinaleBell) RingFinaleBell();
            PushBack();
            // A hit's showing is over: the panel goes (or stays, if ingredients are in).
            if (hitShowUntil > 0f && Time.time >= hitShowUntil)
            {
                hitShowUntil = 0f;
                if (recipePanel != null) RefreshRecipePanel();
            }
        }

        // The surface bubbles gently all the time, and boils (faster, more so with a hot fire) while cooking or while a
        // wrong recipe boils. Its effects mostly emit in bursts, so speeding them up is what makes them busier.
        private void UpdateSurface()
        {
            float busy = cooking || boiling ? cookingBubbles * (1f + heat * 0.5f) : 1f;
            for (int i = 0; i < surfaces.Length; i++)
            {
                var system = surfaces[i];
                if (system == null) continue;
                var emission = system.emission;
                emission.rateOverTimeMultiplier = surfaceRates[i] * busy;
                var main = system.main;
                main.simulationSpeed = busy;
            }
        }

        // The bubble tower rises while a round's dish cooks (small at first, full when done), stays small from the spit
        // until the pumpkin is caught, and is off otherwise (bubbles already out finish their flight).
        private void UpdateTower()
        {
            if (bubbleTower == null || potBroken) return;
            float level = 0f;
            if (cooking && !InMystery) level = Mathf.Lerp(towerSmall, 1f, Mathf.Clamp01(progress));
            else if (waitingForNext && !pumpkinCaught) level = towerSmall;
            SetTower(level);
        }

        private void SetTower(float level)
        {
            if (bubbleTower == null || Mathf.Abs(level - towerLevel) < 0.01f) return;
            towerLevel = level;
            var emission = bubbleTower.emission;
            emission.enabled = level > 0f;
            if (level <= 0f) return;
            emission.rateOverTimeMultiplier = towerRate * level;
            var main = bubbleTower.main;
            main.startSpeed = Scaled(towerSpeed, level);
            if (!bubbleTower.isPlaying) bubbleTower.Play(false);
        }

        // A curve times 'k' (a Two Constants curve's multiplier would scale only its max).
        private static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve curve, float k)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(curve.constant * k);
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(curve.constantMin * k, curve.constantMax * k);
                default:
                    curve.curveMultiplier *= k;
                    return curve;
            }
        }

        // The candy pot shakes non-stop (harder while it cooks): only the pot itself, so its zones stay put.
        private void ShakePot()
        {
            if (pot == null) return;
            if (!InMystery)
            {
                StopShaking();
                return;
            }
            potShaken = true;
            float strength = cooking ? ShakeCooking : 1f;
            float t = Time.time * 22f;
            var wobble = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 0.4f, Mathf.PerlinNoise(t, 0.7f) - 0.5f);
            pot.localPosition = potHome + wobble * (ShakeDistance * strength);
            pot.localRotation = potRest * Quaternion.Euler(wobble.z * ShakeTilt * strength, 0f, -wobble.x * ShakeTilt * strength);
        }

        private void StopShaking()
        {
            if (!potShaken || pot == null) return;
            potShaken = false;
            pot.localPosition = potHome;
            pot.localRotation = potRest;
        }

        // It always stands upright on the floor, fixed in place: wherever it's made (the seed lets it out in the air,
        // maybe tilted), it turns upright and drops straight down onto the ground below.
        private void Land()
        {
            if (TryGetComponent<Rigidbody>(out var body))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (!FindGround(out groundHeight)) return;

            if (transform.position.y > groundHeight + 0.01f)
            {
                falling = true;
                fallSpeed = 0f;
            }
            else
            {
                var at = transform.position;
                transform.position = new Vector3(at.x, groundHeight, at.z);
            }
        }

        private void Fall(float dt)
        {
            fallSpeed += -Physics.gravity.y * dt;
            var at = transform.position;
            float y = at.y - fallSpeed * dt;
            if (y <= groundHeight)
            {
                y = groundHeight;
                falling = false;
                Play(landSound, at);
            }
            transform.position = new Vector3(at.x, y, at.z);
        }

        // The floor under it: the highest solid thing below that isn't part of it, a loose thing or the chest it came
        // out of (anything with a Rigidbody counts only if it's wide, like the ground).
        private bool FindGround(out float height)
        {
            height = 0f;
            bool found = false;
            var from = transform.position + Vector3.up * 1.5f;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var thing = hit.collider;
                if (thing.transform.IsChildOf(transform)) continue;
                if (thing.attachedRigidbody != null && Mathf.Max(thing.bounds.size.x, thing.bounds.size.z) < 2f) continue;
                if (found && hit.point.y <= height) continue;
                height = hit.point.y;
                found = true;
            }
            return found;
        }

        private void LateUpdate()
        {
            UpdateMeter();
        }

        // ---- Push back ----

        // A head over the pot is pushed gently back out (no shake, no buzz): never while the player holds something to
        // put in it, while they're down (Player Launch won't throw them then), while it's falling, paused or gone, or
        // during the Daruma game.
        private void PushBack()
        {
            if (potBroken || falling || pushDistance <= 0f || Time.time < nextPush || Time.timeScale <= 0f) return;
            var eye = Head();
            if (eye == null || Game.Bonus.DarumaStage.Running != null) return;
            var flat = Flat(eye.position - transform.position);
            if (flat.sqrMagnitude > pushRadius * pushRadius) return;
            if (HoldingFood()) return;
            if (!PlayerLaunch.Launch(transform.position, 0f, pushDistance, shake: false, toughness: true, minShare: pushMinShare)) return;
            nextPush = Time.time + pushCooldown;
            StartCoroutine(PassThroughPot(PushPassSeconds));
        }

        // The player's body passes through the pot for a moment, so bumping into it can't stop the push out of it.
        private System.Collections.IEnumerator PassThroughPot(float seconds)
        {
            var body = playerBody;
            if (body == null) yield break;
            var mine = GetComponentsInChildren<Collider>();
            foreach (var part in mine)
            {
                if (part != null && !part.isTrigger) Physics.IgnoreCollision(body, part, true);
            }
            yield return new WaitForSeconds(seconds);
            foreach (var part in mine)
            {
                if (part != null && body != null && !part.isTrigger) Physics.IgnoreCollision(body, part, false);
            }
        }

        // Whether one of the player's hands holds an ingredient, a candy or firewood (they're about to put it in).
        private bool HoldingFood()
        {
            FindPlayer();
            foreach (var hand in hands)
            {
                if (hand == null || !hand.isActiveAndEnabled || !hand.hasSelection) continue;
                foreach (var held in hand.interactablesSelected)
                {
                    if (held is Component thing && thing != null && IsFood(thing.transform)) return true;
                }
            }
            return false;
        }

        private void FindPlayer()
        {
            if (player != null) return;
            player = FindAnyObjectByType<XROrigin>();
            hands.Clear();
            playerBody = null;
            if (player == null) return;
            player.GetComponentsInChildren(true, hands);
            playerBody = player.GetComponent<CharacterController>();
        }

        // A candy by its name ("Candy_4", "Candy_4(Clone)"), never the shop's Candy Box.
        private static bool IsCandyName(string name) => name.StartsWith("Candy_") && name.Length > 6 && char.IsDigit(name[6]);

        // Something it cooks with: an ingredient (a candy, a tree pumpkin) or firewood.
        private static bool IsFood(Transform thing)
        {
            if (thing == null) return false;
            return thing.GetComponentInParent<CookingIngredient>() != null || thing.GetComponentInParent<FireFuel>() != null ||
                   IsCandyName(thing.name) || thing.name.StartsWith("Pumpkin_On_Tree");
        }

        private Transform Head()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }

        // ---- Bell and wild slimes ----

        // A punch on it (the fist itself or a flying one) rings the bell and shows the recipe for a while.
        private void OnPunchLanded(Fist.PunchLanded punch)
        {
            if (potBroken || punch.Hit == null || !punch.Hit.transform.IsChildOf(transform)) return;
            Ring(true);
            ShowRecipeForHit();
        }

        // A held or thrown weapon (or a shot) hitting it hard enough rings the bell. Fists count in OnPunchLanded;
        // ingredients, candies, pumpkins and logs never ring it.
        private void OnCollisionEnter(Collision collision)
        {
            if (potBroken) return;
            var body = collision.rigidbody;
            if (body == null || Fist.IsFistBall(body) || !HeldThings.ByPlayer(collision)) return;
            if (IsFood(body.transform) || body.GetComponentInParent<LaughingPumpkin>() != null ||
                body.GetComponentInParent<RunAwayPumpkin>() != null) return;
            if (collision.relativeVelocity.magnitude < bellHitSpeed) return;
            Ring(true);
            ShowRecipeForHit();
        }

        // A hit on the pot shows the recipe above it for Hit Show Seconds (user, 2026-11-10: hitting the cauldron shows
        // "??? x" until the black hat is found and hit): '??? x ?' until the hat has been read, then the real one. More
        // hits only keep it up longer (it isn't rebuilt each time). The candy pot's own rings never come here.
        private void ShowRecipeForHit()
        {
            if (potBroken || hitShowSeconds <= 0f) return;
            bool wasUp = recipePanel != null;
            hitShowUntil = Time.time + hitShowSeconds;
            if (!wasUp) RefreshRecipePanel();
        }

        // The bell rings (one of Bell Sounds, as it is); 'callSlime' also lets a wild slime out if it may.
        private void Ring(bool callSlime)
        {
            if (potBroken) return;
            if (bellSounds.Length > 0 && Time.time >= nextBellSound)
            {
                nextBellSound = Time.time + BellGap;
                var clip = bellSounds[Random.Range(0, bellSounds.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, MouthPoint(), bellVolume);
            }
            if (callSlime) LetSlimeOut();
        }

        // One wild slime rises out of the pot, unless it's cooking or boiling, one came out a moment ago, or enough of
        // them are about. It gives candy but no EXP.
        private void LetSlimeOut()
        {
            if (cooking || boiling || potBroken || wildSlimes.Length == 0 || Time.time < nextSlime) return;
            if (WildAlive() >= maxWildSlimes) return;
            var prefab = wildSlimes[Random.Range(0, wildSlimes.Length)];
            if (prefab == null) return;
            nextSlime = Time.time + slimeCooldown;
            var from = MouthPoint() + Vector3.up * 0.25f;
            var slime = GhostTree.LetOut(prefab, from, from + Vector3.up * 1.5f, SpawnedStuff());
            if (slime == null) return;
            IgnorePot(slime);
            foreach (var hits in slime.GetComponentsInChildren<EnemyHitDamage>(true)) hits.Exp = 0;
            wild.Add(slime);
            Puff();
        }

        private int WildAlive()
        {
            wild.RemoveAll(slime => !EnemyWave.IsAlive(slime));
            return wild.Count;
        }

        private void RingFinaleBell()
        {
            nextFinaleBell = Time.time + Mathf.Max(0.5f, Random.Range(finaleBellEvery.x, Mathf.Max(finaleBellEvery.x, finaleBellEvery.y)));
            Ring(true);
        }

        // Its slimes still about pop when it bursts (no candy, no EXP).
        private void PopWildSlimes()
        {
            foreach (var slime in wild)
            {
                if (!EnemyWave.IsAlive(slime)) continue;
                Game.Leveling.PlayerLevel.NoRewardFor(slime);
                EnemyWave.Kill(slime);
            }
            wild.Clear();
        }

        // ---- Taking in and cooking ----

        /// <summary>Something entered one of its zones: ingredients go in the pot, firewood burns.</summary>
        public void OnZoneEnter(Zone zone, Collider other)
        {
            if (zone == Zone.Mouth)
            {
                // Cooking (the recipe was checked when the pot filled up), boiling a wrong recipe, between rounds, or
                // done: what goes in just lies there.
                if (potBroken || waitingForNext || boiling || cooking) return;
                if (InMystery)
                {
                    var thing = MysteryThing(other);
                    if (thing != null && taken.Add(thing)) AddMystery(thing);
                    return;
                }
                if (AllCooked) return;
                var ingredient = other.GetComponentInParent<CookingIngredient>();
                if (ingredient != null && taken.Add(ingredient.gameObject)) AddIngredient(ingredient);
            }
            else
            {
                var fuel = other.GetComponentInParent<FireFuel>();
                if (fuel != null && taken.Add(fuel.gameObject)) AddFuel(fuel);
            }
        }

        private void AddIngredient(CookingIngredient ingredient)
        {
            contents.TryGetValue(ingredient.Id, out int had);
            contents[ingredient.Id] = had + 1;
            count++;

            Play(addSound, MouthPoint());
            if (surface != null) surface.Emit(4);
            Puff();
            Destroy(ingredient.gameObject);

            // Checked the moment the pot is full: a right recipe cooks, a wrong one boils a moment and is spat out.
            if (!cooking && !boiling && count >= RecipeTotal)
            {
                if (RightRecipe()) StartCooking();
                else StartCoroutine(WrongBoil());
            }
            RefreshRecipePanel();
        }

        // A puff of green smoke out of the pot.
        private void Puff()
        {
            if (addEffect == null) return;
            var effect = Instantiate(addEffect, MouthPoint() + Vector3.up * 0.1f, Quaternion.identity);
            effect.transform.localScale = Vector3.one * addEffectScale;
            Tint(effect, addEffectColor);
            Destroy(effect, 4f);
        }

        // Colours every particle system in 'effect' (it keeps its see-through-ness), then starts it over in that colour.
        private static void Tint(GameObject effect, Color color)
        {
            var systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var system in systems)
            {
                var main = system.main;
                var start = main.startColor;
                float had = start.mode switch
                {
                    ParticleSystemGradientMode.Color => start.color.a,
                    ParticleSystemGradientMode.TwoColors => start.colorMax.a,
                    _ => 1f,
                };
                float alpha = had * color.a;
                var light = Color.Lerp(color, Color.white, 0.35f);
                var dark = color * 0.7f;
                light.a = alpha;
                dark.a = alpha;
                main.startColor = new ParticleSystem.MinMaxGradient(dark, light);

                // Colour over lifetime would colour it again: it keeps only its fading.
                var life = system.colorOverLifetime;
                if (life.enabled) life.color = Whiten(life.color);
            }
            foreach (var system in systems)
            {
                system.Clear(false);
                system.Play(false);
            }
        }

        private static ParticleSystem.MinMaxGradient Whiten(ParticleSystem.MinMaxGradient colors)
        {
            switch (colors.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, colors.color.a));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, colors.colorMin.a), new Color(1f, 1f, 1f, colors.colorMax.a));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Whiten(colors.gradient));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Whiten(colors.gradientMin), Whiten(colors.gradientMax));
                default:
                    return colors;
            }
        }

        private static Gradient Whiten(Gradient gradient)
        {
            var white = new Gradient { mode = gradient.mode };
            white.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, gradient.alphaKeys);
            return white;
        }

        private void AddFuel(FireFuel fuel)
        {
            heat = Mathf.Min(maxHeat, heat + fuel.Heat);
            Play(fuelSound, FirePoint());
            if (fire != null) fire.Emit(10);
            Destroy(fuel.gameObject);
        }

        // A right recipe (or the full candy pot) starts cooking. Only a round's dish raises Cooking Started (its wave).
        private void StartCooking()
        {
            cooking = true;
            progress = 0f;
            pumpkinCaught = false;
            RefreshRecipePanel();
            if (!InMystery) CookingStarted?.Invoke(this);
        }

        // A wrong recipe: it boils a moment (no meter, no wave), then spits out its slime. The same recipe stays, and
        // the pot takes ingredients again at once (anything that fell in meanwhile is taken now).
        private System.Collections.IEnumerator WrongBoil()
        {
            boiling = true;
            RefreshRecipePanel();
            if (wrongBoilSeconds > 0f) yield return new WaitForSeconds(wrongBoilSeconds);
            boiling = false;
            if (potBroken) yield break;
            var at = MouthPoint();
            DoneEffect(at);
            Fail(at);
            contents.Clear();
            count = 0;
            RefreshRecipePanel();
            TakeWhatsInside();
        }

        // Ingredients lying in the pot's opening (they fell in while it was busy) go in now.
        private void TakeWhatsInside()
        {
            var half = Vector3.Scale(mouthSize, transform.lossyScale) * 0.5f;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            foreach (var other in Physics.OverlapBox(MouthPoint(), half, transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (boiling || cooking) return;
                OnZoneEnter(Zone.Mouth, other);
            }
        }

        private void Finish()
        {
            cooking = false;
            progress = 0f;

            if (InMystery)
            {
                StartCoroutine(BreakPot());
                return;
            }

            var at = MouthPoint();
            DoneEffect(at);

            // Only a right recipe cooks (a wrong one is spat out after its boil), but just in case: a wrong one fails.
            bool right = RightRecipe();
            if (right) MakeResult(at);
            else Fail(at);

            contents.Clear();
            count = 0;
            if (right)
            {
                if (HoldBetweenRounds) waitingForNext = true;
                else NextRound();
            }
            RefreshRecipePanel();
        }

        private void DoneEffect(Vector3 at)
        {
            if (doneEffect == null) return;
            var effect = Instantiate(doneEffect, at, Quaternion.identity);
            effect.transform.localScale = Vector3.one * doneEffectScale;
            Destroy(effect, 4f);
        }

        /// <summary>
        /// The PlayGround Rounds director's go-ahead after a right recipe (pumpkin caught, seed taken): on to the next
        /// round's recipe (or the candy pot).
        /// </summary>
        public void Continue()
        {
            if (!waitingForNext) return;
            waitingForNext = false;
            NextRound();
            RefreshRecipePanel();
            // Candies left lying in the pot count for the candy pot. A new round's recipe never takes what's lying
            // there: it would cook (or fail) before the player has read it, while the walls come back up.
            if (InMystery) TakeWhatsInside();
        }

        // On to the next dish, with a new recipe; after the last one it's done (or opens the candy pot).
        private void NextRound()
        {
            round++;
            pumpkinCaught = false;
            if (hatEachRound) recipeKnown = false;
            if (AllCooked)
            {
                recipe.Clear();
                contents.Clear();
                count = 0;
                taken.Clear();
                nextFinaleBell = Time.time + FirstFinaleBell;
                if (mysteryCount <= 0 && !string.IsNullOrEmpty(cookedBool)) GameStages.SetBool(cookedBool, true, this);
            }
            else
            {
                MakeRecipe();
            }
            RecipeChanged?.Invoke(this);
        }

        // Only candies (any of Candies) count for the candy pot.
        private GameObject MysteryThing(Collider other)
        {
            var ingredient = other.GetComponentInParent<CookingIngredient>();
            return ingredient != null && candyIds.Contains(ingredient.Id) ? ingredient.gameObject : null;
        }

        private void AddMystery(GameObject thing)
        {
            count++;
            Play(addSound, MouthPoint());
            if (surface != null) surface.Emit(4);
            Puff();
            Destroy(thing);
            if (count >= mysteryCount) StartCooking();
            else RefreshRecipePanel();
        }

        // The candy pot: it swells, then bursts into smoke and is gone (its slimes pop). Pot Broken starts what comes
        // next (the Daruma game); the cauldron itself is destroyed 3 s later, so nothing else may wait on it.
        private System.Collections.IEnumerator BreakPot()
        {
            potBroken = true;
            PopWildSlimes();
            StopShaking();
            RefreshRecipePanel();
            var at = MouthPoint();
            Play(swellSound, at);
            var size = transform.localScale;
            var home = transform.position;
            const float swellSeconds = 1.8f;
            for (float t = 0f; t < swellSeconds; t += Time.deltaTime)
            {
                float k = t / swellSeconds;
                transform.localScale = size * (1f + 0.3f * k * k);
                transform.position = home + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (0.06f * k);
                if (surface != null && Random.value < 0.3f) surface.Emit(2);
                yield return null;
            }
            transform.position = home;
            transform.localScale = size;

            Play(breakSound, at);
            if (breakEffect != null)
            {
                var effect = Instantiate(breakEffect, at, Quaternion.identity);
                effect.transform.localScale = Vector3.one * breakEffectScale;
                Destroy(effect, 5f);
            }

            var parent = SpawnedStuff();
            for (int i = 0; i < finalCandies && candies.Length > 0; i++)
            {
                var prefab = candies[Random.Range(0, candies.Length)];
                if (prefab == null) continue;
                var landing = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(1.5f, 4.5f);
                var candy = Instantiate(prefab, at + Vector3.up * 0.3f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
                if (candy.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic) body.linearVelocity = Throw(candy.transform.position, landing, Random.Range(0.9f, 1.4f));
            }
            // Optional: something left right where the cauldron stood (none in the PlayGround).
            if (finalSeed != null) Instantiate(finalSeed, transform.position + Vector3.up * 0.5f, Quaternion.identity, parent);

            // The cauldron itself is gone: hide it, stop its effects, drop its colliders.
            foreach (var shape in GetComponentsInChildren<Renderer>(true)) shape.enabled = false;
            foreach (var effect in GetComponentsInChildren<ParticleSystem>(true)) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var part in GetComponentsInChildren<Collider>(true)) part.enabled = false;
            if (meter != null) meter.gameObject.SetActive(false);

            if (!string.IsNullOrEmpty(cookedBool)) GameStages.SetBool(cookedBool, true, this);
            PotBroken?.Invoke(this);
            Destroy(gameObject, 3f);
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private bool RightRecipe()
        {
            if (count != RecipeTotal) return false;
            foreach (var line in recipe)
            {
                contents.TryGetValue(line.id, out int had);
                if (had != line.count) return false;
            }
            return true;
        }

        // The round's pumpkins jump out of the pot, each off to a different side.
        private void MakeResult(Vector3 at)
        {
            Play(successSound, at);
            if (laughs.Length > 0) Play(laughs[Random.Range(0, laughs.Length)], at);

            var dish = rounds[round];
            if (dish.result == null) return;

            int amount = Mathf.Max(1, dish.results);
            float start = Random.Range(0f, 360f);
            var middle = spitToMiddle ? PlayGroundMiddle() : (Vector3?)null;
            for (int i = 0; i < amount; i++)
            {
                float angle = (start + i * 360f / amount) * Mathf.Deg2Rad;
                var away = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // Side by side, not inside each other, and clear of the pot.
                var from = at + Vector3.up * 0.9f + (amount > 1 ? away * 0.35f : Vector3.zero);
                var made = Instantiate(dish.result, from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
                IgnorePot(made);
                if (made.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
                {
                    if (middle.HasValue)
                    {
                        // An arc out of the pot down to the middle of the PlayGround.
                        var landing = middle.Value + (amount > 1 ? away * 1.2f : Vector3.zero);
                        body.linearVelocity = Throw(from, landing, 1.4f);
                    }
                    else
                    {
                        body.linearVelocity = away * 1.2f + Vector3.up * 4.5f;
                    }
                }
                Cooked?.Invoke(this, made);
            }
        }

        // A wrong recipe's slime (Slime_Big) is spat out: it appears on the NavMesh Spit Distance from the pot, away from
        // the player when it can, facing them, with a puff (its root is kinematic, so it can't fly). One with a loose
        // Rigidbody flies there in an arc instead. Always made (no Enemy Limit), so the punishment can't be skipped.
        private void Fail(Vector3 at)
        {
            Play(failSound, at);
            int wanted = wrongResult != null ? Random.Range(Mathf.Min(minBadPumpkins, maxBadPumpkins), maxBadPumpkins + 1) : 0;
            if (wanted > 0)
            {
                bool flies = wrongResult.TryGetComponent<Rigidbody>(out var loose) && !loose.isKinematic;
                var filter = new NavMeshQueryFilter { areaMask = NavMesh.AllAreas };
                var agent = wrongResult.GetComponentInChildren<NavMeshAgent>(true);
                if (agent != null) filter.agentTypeID = agent.agentTypeID;
                float start = Random.Range(0f, 360f);
                var from = at + Vector3.up * 0.4f;
                for (int i = 0; i < wanted; i++)
                {
                    var landing = LandingSpot(start + i * 360f / wanted, filter);
                    if (flies)
                    {
                        var made = Instantiate(wrongResult, from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
                        ColourForRecipe(made);
                        IgnorePot(made);
                        if (made.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
                        {
                            body.linearVelocity = Throw(from, landing, SpitSeconds);
                            body.angularVelocity = Random.insideUnitSphere * 3f;
                        }
                    }
                    else
                    {
                        ColourForRecipe(Instantiate(wrongResult, landing, FacingPlayer(landing), SpawnedStuff()));
                        DoneEffect(landing + Vector3.up * 0.3f);
                    }
                }
            }
            CookFailed?.Invoke(this);
        }

        // A wrong recipe's slime comes in the colour of a candy the recipe still needs (user, 2026-11-10: so it drops
        // that candy): one the pot is short of, more likely the more it's short; if none is short (too many of
        // something), any of the recipe's candies. Called before the pot is emptied. Without Candy Colours, or a slime
        // with no Slime Body, it keeps its own colour.
        private void ColourForRecipe(GameObject made)
        {
            if (made == null || candyColours == null || !made.TryGetComponent(out SlimeBody slime)) return;
            var needed = new List<GameObject>();
            var any = new List<GameObject>();
            foreach (var line in recipe)
            {
                var candy = CandyFor(line.id);
                if (candy == null) continue;
                any.Add(candy);
                contents.TryGetValue(line.id, out int had);
                for (int i = had; i < line.count; i++) needed.Add(candy);
            }
            var pool = needed.Count > 0 ? needed : any;
            if (pool.Count == 0) return;
            var pick = pool[Random.Range(0, pool.Count)];
            if (!candyColours.TryGetRow(pick, out var row)) return;
            slime.Recolour(row.slimeSkin, row.candy);
        }

        // The candy prefab (of Candies) whose ingredient id this is; null for a pumpkin or anything else.
        private GameObject CandyFor(string id)
        {
            foreach (var candy in candies)
            {
                if (candy == null) continue;
                var ingredient = candy.GetComponent<CookingIngredient>();
                if ((ingredient != null ? ingredient.Id : CookingIngredient.BaseName(candy.name)) == id) return candy;
            }
            return null;
        }

        // A spot on the NavMesh Spit Distance from the pot, trying round from 'startAngle': the first one at least Keep
        // From Player from the player's head, else the one farthest from them.
        private Vector3 LandingSpot(float startAngle, NavMeshQueryFilter filter)
        {
            var eye = Head();
            var best = transform.position + Quaternion.Euler(0f, startAngle, 0f) * Vector3.forward * spitDistance;
            float bestRoom = -1f;
            for (int i = 0; i < 8; i++)
            {
                var spot = transform.position + Quaternion.Euler(0f, startAngle + i * 45f, 0f) * Vector3.forward * spitDistance;
                if (!NavMesh.SamplePosition(spot, out var hit, 3f, filter) && !NavMesh.SamplePosition(spot, out hit, 3f, NavMesh.AllAreas)) continue;
                spot = hit.position;
                float room = eye != null ? Flat(spot - eye.position).magnitude : float.MaxValue;
                if (room >= KeepFromPlayer) return spot;
                if (room <= bestRoom) continue;
                bestRoom = room;
                best = spot;
            }
            return best;
        }

        private Quaternion FacingPlayer(Vector3 from)
        {
            var eye = Head();
            var look = eye != null ? Flat(eye.position - from) : Flat(from - transform.position);
            return look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look) : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        // What it spits out passes through the cauldron, so it isn't shoved sideways out of the pot.
        private void IgnorePot(GameObject made)
        {
            var mine = GetComponentsInChildren<Collider>(true);
            foreach (var theirs in made.GetComponentsInChildren<Collider>(true))
            {
                foreach (var own in mine)
                {
                    if (own != null && theirs != null) Physics.IgnoreCollision(theirs, own);
                }
            }
        }

        // The floor in the middle of the PlayGround (its Ghost Roam Area), on the NavMesh if there is one; null without
        // a PlayGround around.
        private Vector3? PlayGroundMiddle()
        {
            var area = GhostRoamArea.Around(transform.position);
            if (area == null) return null;
            var middle = area.Middle;
            if (Physics.Raycast(middle + Vector3.up * 2f, Vector3.down, out var hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                middle = hit.point;
            if (NavMesh.SamplePosition(middle, out var onMesh, 3f, NavMesh.AllAreas)) middle = onMesh.position;
            return middle + Vector3.up * 0.3f;
        }

        // How fast to throw something from 'from' so it comes down on 'to' after 'seconds'.
        private static Vector3 Throw(Vector3 from, Vector3 to, float seconds)
        {
            return (to - from - 0.5f * seconds * seconds * Physics.gravity) / seconds;
        }

        // This round's candies, each picked at random (at most Max Kinds different ones), and its pumpkins.
        private void MakeRecipe()
        {
            recipe.Clear();
            if (AllCooked) return;
            var dish = rounds[round];

            var pool = new List<GameObject>();
            foreach (var candy in candies)
            {
                if (candy != null) pool.Add(candy);
            }

            var kinds = new List<GameObject>();
            var counts = new List<int>();
            for (int i = 0; i < dish.candies && pool.Count > 0; i++)
            {
                var pick = kinds.Count >= maxKinds ? kinds[Random.Range(0, kinds.Count)] : pool[Random.Range(0, pool.Count)];
                int had = kinds.IndexOf(pick);
                if (had >= 0)
                {
                    counts[had]++;
                    continue;
                }
                kinds.Add(pick);
                counts.Add(1);
            }
            for (int i = 0; i < kinds.Count; i++) recipe.Add(LineFor(kinds[i], counts[i]));
            if (pumpkin != null && dish.pumpkins > 0) recipe.Add(LineFor(pumpkin, dish.pumpkins));
        }

        private static Line LineFor(GameObject prefab, int amount)
        {
            var ingredient = prefab.GetComponent<CookingIngredient>();
            return new Line
            {
                id = ingredient != null ? ingredient.Id : CookingIngredient.BaseName(prefab.name),
                count = amount,
                icon = ingredient != null ? ingredient.Icon : null,
            };
        }

        private void UpdateFire()
        {
            if (fire == null) return;
            var emission = fire.emission;
            emission.rateOverTimeMultiplier = fireRate * (1f + heat * 1.5f);
            var main = fire.main;
            main.startSizeMultiplier = fireSizeBase * (1f + heat * 0.25f);
            main.simulationSpeed = 1f + heat * 0.3f;
        }

        private void MakeZone(Zone zone, string zoneName, Vector3 center, Vector3 size)
        {
            var go = new GameObject(zoneName);
            go.transform.SetParent(transform, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = center;
            box.size = size;
            go.AddComponent<CauldronZone>().Setup(this, zone);
        }

        // A filling bar while it cooks, orange when the fire is hot. Before that, while something is in or after a hit,
        // the recipe with what's in and what's missing, or "???" before the hat (see RefreshRecipePanel).
        private void UpdateMeter()
        {
            Head();
            if (recipePanel != null && recipePanel.gameObject.activeSelf) PlacePanel(recipePanel);

            if (!cooking)
            {
                if (meter != null && meter.gameObject.activeSelf) meter.gameObject.SetActive(false);
                return;
            }
            if (meter == null) BuildMeter();
            if (!meter.gameObject.activeSelf) meter.gameObject.SetActive(true);

            meterText.text = heat > 0.5f ? "Cooking hot!" : "Cooking...";
            var fill = meterFillImage.rectTransform;
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            meterFillImage.color = Color.Lerp(MeterFill, MeterHot, Mathf.Clamp01(heat / Mathf.Max(0.01f, maxHeat)));
            PlacePanel(meter);
        }

        // Floats a panel above the pot, turned to the player.
        private void PlacePanel(RectTransform panel)
        {
            var top = pot != null && pot.TryGetComponent<Renderer>(out var shape) ? shape.bounds.max.y : MouthPoint().y;
            panel.position = new Vector3(transform.position.x, top + meterHeight, transform.position.z);
            WorldUI.Face(panel, head);
        }

        // The recipe above the pot: each ingredient's picture over how many are in / how many it needs (green when
        // there are enough, red when too many), and a red "?" for anything that isn't in the recipe. Until the magic hat
        // has shown the recipe it's only "??? x ?" for each ingredient (BuildHiddenPanel). Shown while ingredients are
        // going in, and for Hit Show Seconds after a hit on the pot (then even with nothing in). Rebuilt whenever that
        // changes.
        private void RefreshRecipePanel()
        {
            if (recipePanel != null)
            {
                Destroy(recipePanel.gameObject);
                recipePanel = null;
            }
            if (InMystery && !cooking && !waitingForNext)
            {
                BuildMysteryPanel();
                return;
            }
            bool hitShown = Time.time < hitShowUntil;
            if (cooking || AllCooked || recipe.Count == 0 || waitingForNext || (count == 0 && !hitShown)) return;
            if (!recipeKnown)
            {
                BuildHiddenPanel();
                return;
            }

            int wrong = count;
            foreach (var line in recipe)
            {
                contents.TryGetValue(line.id, out int had);
                wrong -= Mathf.Min(had, line.count);
            }
            int columns = recipe.Count + (wrong > 0 ? 1 : 0);
            float width = Mathf.Max(ColumnWidth * 2f, columns * ColumnWidth) + PanelPad * 2f;
            float height = PanelPad + ColumnIcon + ColumnCount + PanelPad;
            recipePanel = WorldUI.CreatePanel("Cauldron Recipe", width, height, MeterScale, MeterBack);

            float left = (width - columns * ColumnWidth) * 0.5f;
            for (int i = 0; i < recipe.Count; i++, left += ColumnWidth)
            {
                var line = recipe[i];
                contents.TryGetValue(line.id, out int had);
                var color = had == line.count ? MeterFill : had > line.count ? TooMany : Color.white;
                if (line.icon != null)
                {
                    var icon = WorldUI.CreateImage($"Icon {i + 1}", recipePanel, Color.white);
                    icon.sprite = line.icon;
                    icon.preserveAspect = true;
                    WorldUI.Place(icon, left + (ColumnWidth - ColumnIcon) * 0.5f, PanelPad, ColumnIcon, ColumnIcon);
                }
                else
                {
                    var words = WorldUI.CreateText($"Name {i + 1}", recipePanel, line.id.Replace('_', ' '), 18f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
                    WorldUI.Place((Graphic)words, left, PanelPad, ColumnWidth, ColumnIcon);
                }
                var amount = WorldUI.CreateText($"Count {i + 1}", recipePanel, $"{had}/{line.count}", 30f, FontStyles.Bold, TextAlignmentOptions.Center, color, font);
                WorldUI.Place((Graphic)amount, left, PanelPad + ColumnIcon, ColumnWidth, ColumnCount);
            }
            if (wrong > 0)
            {
                var what = WorldUI.CreateText("Wrong", recipePanel, "?", 54f, FontStyles.Bold, TextAlignmentOptions.Center, TooMany, font);
                WorldUI.Place((Graphic)what, left, PanelPad, ColumnWidth, ColumnIcon);
                var amount = WorldUI.CreateText("Wrong Count", recipePanel, $"x {wrong}", 30f, FontStyles.Bold, TextAlignmentOptions.Center, TooMany, font);
                WorldUI.Place((Graphic)amount, left, PanelPad + ColumnIcon, ColumnWidth, ColumnCount);
            }
            PlacePanel(recipePanel);
        }

        // Before the magic hat has been read: one column per recipe line, "???" over "x ?", so the player sees there is a
        // recipe but not what it asks for, nor how many (user, 2026-11-10). No red "?" for wrong things either: that
        // would give the recipe away.
        private void BuildHiddenPanel()
        {
            int columns = recipe.Count;
            float width = Mathf.Max(ColumnWidth * 2f, columns * ColumnWidth) + PanelPad * 2f;
            float height = PanelPad + ColumnIcon + ColumnCount + PanelPad;
            recipePanel = WorldUI.CreatePanel("Cauldron Recipe Hidden", width, height, MeterScale, MeterBack);

            float left = (width - columns * ColumnWidth) * 0.5f;
            for (int i = 0; i < columns; i++, left += ColumnWidth)
            {
                var what = WorldUI.CreateText($"Hidden {i + 1}", recipePanel, "???", 34f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
                WorldUI.Place((Graphic)what, left, PanelPad, ColumnWidth, ColumnIcon);
                var amount = WorldUI.CreateText($"Hidden Count {i + 1}", recipePanel, "x ?", 30f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
                WorldUI.Place((Graphic)amount, left, PanelPad + ColumnIcon, ColumnWidth, ColumnCount);
            }
            PlacePanel(recipePanel);
        }

        // The candy pot's card: a candy over how many are in, "3/8" (green once it's full). Shown from the start (no
        // magic hat needed).
        private void BuildMysteryPanel()
        {
            int need = Mathf.Max(1, mysteryCount);
            float width = ColumnWidth * 2f + PanelPad * 2f;
            float height = PanelPad + ColumnIcon + ColumnCount + PanelPad;
            recipePanel = WorldUI.CreatePanel("Cauldron Candy Pot", width, height, MeterScale, MeterBack);
            if (candyIcon != null)
            {
                var icon = WorldUI.CreateImage("Candy", recipePanel, Color.white);
                icon.sprite = candyIcon;
                icon.preserveAspect = true;
                WorldUI.Place(icon, (width - ColumnIcon) * 0.5f, PanelPad, ColumnIcon, ColumnIcon);
            }
            else
            {
                var words = WorldUI.CreateText("Candy", recipePanel, "Candy", 22f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
                WorldUI.Place((Graphic)words, PanelPad, PanelPad, width - PanelPad * 2f, ColumnIcon);
            }
            var color = count >= need ? MeterFill : Color.white;
            var amount = WorldUI.CreateText("Count", recipePanel, $"{Mathf.Min(count, need)}/{need}", 30f, FontStyles.Bold, TextAlignmentOptions.Center, color, font);
            WorldUI.Place((Graphic)amount, PanelPad, PanelPad + ColumnIcon, width - PanelPad * 2f, ColumnCount);
            PlacePanel(recipePanel);
        }

        private void BuildMeter()
        {
            meter = WorldUI.CreatePanel("Cauldron Meter", MeterWidth, MeterPixelsHigh, MeterScale, MeterBack);
            meterText = WorldUI.CreateText("Text", meter, "", 30f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, font);
            WorldUI.Place((Graphic)meterText, 10f, 4f, MeterWidth - 20f, 40f);

            var bar = WorldUI.CreateImage("Bar", meter, new Color(1f, 1f, 1f, 0.15f));
            WorldUI.Place(bar, 16f, 50f, MeterWidth - 32f, 22f);
            meterBar = bar.rectTransform;
            meterFillImage = WorldUI.CreateImage("Fill", meterBar, MeterFill);
            var fill = meterFillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        private Vector3 MouthPoint()
        {
            return transform.TransformPoint(mouthCenter);
        }

        private Vector3 FirePoint()
        {
            return transform.TransformPoint(fireCenter);
        }

        private static void Play(AudioClip clip, Vector3 at)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at);
        }

        private static Transform SpawnedStuff()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.6f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(mouthCenter, mouthSize);
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.8f);
            Gizmos.DrawWireCube(fireCenter, fireSize);
        }
#endif
    }
}
