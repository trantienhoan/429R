using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.PlayGround
{
    /// <summary>
    /// The PlayGround's witch cauldron. It cooks a few dishes in a row (Rounds), each with its own recipe of pumpkins
    /// (Pumpkin_On_Tree) and random lollipops (Lolipop_1 ... 7); the magic hat shows the current one. Ingredients dropped
    /// or thrown into the pot are taken in with a puff of green smoke; once the player has read the recipe off the hat,
    /// what's in the pot and what's still missing shows above it. Once it holds as many as the recipe asks for, it starts
    /// cooking: the surface bubbles, coloured bubbles and skulls fly out and a meter fills above it. Firewood thrown into
    /// the fire under it makes the fire roar and the cooking go faster for a while. Done: the right recipe makes the
    /// round's pumpkins (Pumpkin_8, then 2x Pumpkin_9 for the last) jump out and the next round's recipe begins; a wrong
    /// one spits out a black pumpkin (pumpkin_black) that bursts into a mini boss where it lands, the magic hat shakes,
    /// and the same recipe has to be tried again. After the last round it's done and takes nothing more. Goes on
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

        /// <summary>One dish: what its recipe asks for, and what it makes when cooked right.</summary>
        [Serializable]
        public class Round
        {
            [Tooltip("How many pumpkins (Pumpkin_On_Tree) the recipe asks for.")]
            [Min(0)] public int pumpkins = 1;
            [Tooltip("How many lollipops the recipe asks for; each is picked at random.")]
            [Min(0)] public int lollipops = 2;
            [Tooltip("What it makes when cooked right, e.g. Pumpkin_8.")]
            public GameObject result;
            [Tooltip("How many of them jump out.")]
            [Min(1)] public int results = 1;
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

        [Header("Rounds (cooked one after another; new random lollipops each time the PlayGround opens)")]
        [Tooltip("The dishes it cooks, in order. A wrong recipe doesn't count: that round's recipe stays until it's " +
                 "cooked right. After the last one it takes nothing more.")]
        [SerializeField] private Round[] rounds =
        {
            new() { pumpkins = 1, lollipops = 2 },
            new() { pumpkins = 1, lollipops = 3 },
            new() { pumpkins = 1, lollipops = 4 },
            new() { pumpkins = 2, lollipops = 5, results = 2 },
        };
        [Tooltip("The lollipops a recipe can ask for (Lolipop_1 ... 7).")]
        [SerializeField] private GameObject[] lollipops = Array.Empty<GameObject>();
        [Tooltip("Most different lollipops one recipe asks for, so it fits on the card.")]
        [Min(1)] [SerializeField] private int maxKinds = 3;
        [Tooltip("The pumpkin recipes ask for (Pumpkin_On_Tree).")]
        [SerializeField] private GameObject pumpkin;
        [Tooltip("Each new round's recipe has to be read off the magic hat again before it shows above the cauldron.")]
        [SerializeField] private bool hatEachRound = true;

        [Header("Taking in")]
        [Tooltip("Puff at the pot each time an ingredient goes in, e.g. smoke; it's tinted Add Effect Color.")]
        [SerializeField] private GameObject addEffect;
        [SerializeField] private Color addEffectColor = new(0.35f, 1f, 0.3f, 1f);
        [Tooltip("Size of the puff. The Hyper Casual FX smoke is about 13 m wide, so 0.07 makes it about 0.9 m.")]
        [Min(0.01f)] [SerializeField] private float addEffectScale = 0.07f;

        [Header("Cooking")]
        [Tooltip("Seconds it takes to cook with no extra firewood.")]
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
        [Tooltip("What a wrong recipe spits out (pumpkin_black), and how many; each bursts into a mini boss where it lands.")]
        [SerializeField] private GameObject badPumpkin;
        [Min(0)] [SerializeField] private int minBadPumpkins = 1;
        [Min(0)] [SerializeField] private int maxBadPumpkins = 1;
        [Tooltip("How far from the cauldron they land, in metres.")]
        [Min(0.5f)] [SerializeField] private float spitDistance = 2.5f;
        [SerializeField] private AudioClip failSound;
        [Tooltip("Burst at the pot when it finishes cooking, e.g. smoke.")]
        [SerializeField] private GameObject doneEffect;
        [Min(0.01f)] [SerializeField] private float doneEffectScale = 0.15f;

        [Header("Meter")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("How far above the pot's rim the meter floats, in metres.")]
        [SerializeField] private float meterHeight = 0.5f;

        /// <summary>Raised when a wrong recipe finishes cooking (the magic hat shakes).</summary>
        public static event Action<Cauldron> CookFailed;
        /// <summary>Raised when a right recipe finishes cooking, once for each thing it made.</summary>
        public static event Action<Cauldron, GameObject> Cooked;
        /// <summary>Raised when it moves on to the next round's recipe, or has cooked them all.</summary>
        public static event Action<Cauldron> RecipeChanged;

        // Things it spawns go here, so the stage clean-up removes them.
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        private const float MeterWidth = 320f;
        private const float MeterPixelsHigh = 86f;
        private const float MeterScale = 0.0018f;
        // Seconds a black pumpkin is in the air.
        private const float SpitSeconds = 1.1f;
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
        private ParticleSystem[] surfaces = Array.Empty<ParticleSystem>();
        private int count;
        private bool cooking;
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

        public IReadOnlyList<Line> Recipe => recipe;
        public bool IsCooking => cooking;
        public float Heat => heat;
        /// <summary>Which round it's on, from 0; equals RoundCount once it has cooked them all.</summary>
        public int RoundIndex => round;
        public int RoundCount => rounds.Length;
        /// <summary>It has cooked every round and takes nothing more.</summary>
        public bool AllCooked => round >= rounds.Length;
        /// <summary>The player has read this round's recipe off the magic hat, so it shows above the pot.</summary>
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

        private void Awake()
        {
            MakeZone(Zone.Mouth, "Cauldron Mouth", mouthCenter, mouthSize);
            MakeZone(Zone.Fire, "Cauldron Fire", fireCenter, fireSize);
            if (pot != null && pot.GetComponentInChildren<Collider>() == null && pot.TryGetComponent<MeshFilter>(out var shape))
                pot.gameObject.AddComponent<MeshCollider>().sharedMesh = shape.sharedMesh;
            MakeRecipe();
        }

        private void Start()
        {
            if (surface != null)
            {
                surfaces = surface.GetComponentsInChildren<ParticleSystem>(true);
                surface.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (fire != null)
            {
                fireRate = fire.emission.rateOverTimeMultiplier;
                var main = fire.main;
                fireSizeBase = main.startSizeMultiplier;
                main.maxParticles = Mathf.Max(main.maxParticles, 80);
            }
        }

        private void OnEnable()
        {
            cauldrons.Add(this);
        }

        private void OnDisable()
        {
            cauldrons.Remove(this);
        }

        private void OnDestroy()
        {
            if (meter != null) Destroy(meter.gameObject);
            if (recipePanel != null) Destroy(recipePanel.gameObject);
        }

        /// <summary>The magic hat showed the recipe: from now on it shows above the pot as ingredients go in.</summary>
        public void RevealRecipe()
        {
            if (recipeKnown) return;
            recipeKnown = true;
            RefreshRecipePanel();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            heat = Mathf.Max(0f, heat - heatFade * dt);
            UpdateFire();

            if (cooking)
            {
                progress += dt / cookSeconds * (1f + heat);
                foreach (var system in surfaces)
                {
                    var main = system.main;
                    main.simulationSpeed = 1f + heat * 0.5f;
                }
                if (progress >= 1f) Finish();
            }
        }

        private void LateUpdate()
        {
            UpdateMeter();
        }

        /// <summary>Something entered one of its zones: ingredients go in the pot, firewood burns.</summary>
        public void OnZoneEnter(Zone zone, Collider other)
        {
            if (zone == Zone.Mouth)
            {
                // All cooked: what goes in just lies there.
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

            if (!cooking && count >= RecipeTotal) StartCooking();
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

        private void StartCooking()
        {
            cooking = true;
            progress = 0f;
            if (surface != null) surface.Play(true);
        }

        private void Finish()
        {
            cooking = false;
            progress = 0f;
            if (surface != null) surface.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            var at = MouthPoint();
            if (doneEffect != null)
            {
                var effect = Instantiate(doneEffect, at, Quaternion.identity);
                effect.transform.localScale = Vector3.one * doneEffectScale;
                Destroy(effect, 4f);
            }

            bool right = RightRecipe();
            if (right) MakeResult(at);
            else Fail(at);

            contents.Clear();
            count = 0;
            if (right) NextRound();
            RefreshRecipePanel();
        }

        // On to the next dish, with a new recipe; after the last one it's done.
        private void NextRound()
        {
            round++;
            if (hatEachRound) recipeKnown = false;
            if (AllCooked)
            {
                recipe.Clear();
                if (!string.IsNullOrEmpty(cookedBool)) GameStages.SetBool(cookedBool, true, this);
            }
            else
            {
                MakeRecipe();
            }
            RecipeChanged?.Invoke(this);
        }

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
            for (int i = 0; i < amount; i++)
            {
                float angle = (start + i * 360f / amount) * Mathf.Deg2Rad;
                var away = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // Side by side, not inside each other.
                var from = at + Vector3.up * 0.3f + (amount > 1 ? away * 0.35f : Vector3.zero);
                var made = Instantiate(dish.result, from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
                if (made.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
                    body.linearVelocity = away * 1.2f + Vector3.up * 4.5f;
                Cooked?.Invoke(this, made);
            }
        }

        // Black pumpkins fly out of the pot in an arc and land around the cauldron, where they burst into mini bosses.
        private void Fail(Vector3 at)
        {
            Play(failSound, at);
            int wanted = badPumpkin != null ? Random.Range(Mathf.Min(minBadPumpkins, maxBadPumpkins), maxBadPumpkins + 1) : 0;
            float start = Random.Range(0f, 360f);
            var from = at + Vector3.up * 0.4f;
            for (int i = 0; i < wanted; i++)
            {
                float angle = (start + i * 360f / wanted) * Mathf.Deg2Rad;
                var away = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var landing = transform.position + away * spitDistance;
                if (NavMesh.SamplePosition(landing, out var onMesh, 3f, NavMesh.AllAreas)) landing = onMesh.position;

                var made = Instantiate(badPumpkin, from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
                if (made.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
                {
                    body.linearVelocity = Throw(from, landing, SpitSeconds);
                    body.angularVelocity = Random.insideUnitSphere * 3f;
                }
            }
            CookFailed?.Invoke(this);
        }

        // How fast to throw something from 'from' so it comes down on 'to' after 'seconds'.
        private static Vector3 Throw(Vector3 from, Vector3 to, float seconds)
        {
            return (to - from - 0.5f * seconds * seconds * Physics.gravity) / seconds;
        }

        // This round's lollipops, each picked at random (at most Max Kinds different ones), and its pumpkins.
        private void MakeRecipe()
        {
            recipe.Clear();
            if (AllCooked) return;
            var dish = rounds[round];

            var pool = new List<GameObject>();
            foreach (var lollipop in lollipops)
            {
                if (lollipop != null) pool.Add(lollipop);
            }

            var kinds = new List<GameObject>();
            var counts = new List<int>();
            for (int i = 0; i < dish.lollipops && pool.Count > 0; i++)
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

        // A filling bar while it cooks, orange when the fire is hot. Before that, once the player has read the recipe off
        // the hat and put something in, the recipe with what's in and what's missing (see RefreshRecipePanel).
        private void UpdateMeter()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
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
        // there are enough, red when too many), and a red "?" for anything that isn't in the recipe. Shown only once the
        // magic hat has shown the recipe, and only while ingredients are going in. Rebuilt whenever that changes.
        private void RefreshRecipePanel()
        {
            if (recipePanel != null)
            {
                Destroy(recipePanel.gameObject);
                recipePanel = null;
            }
            if (!recipeKnown || cooking || count == 0 || AllCooked || recipe.Count == 0) return;

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
