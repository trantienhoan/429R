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
    /// The PlayGround's witch cauldron. Each time the PlayGround opens it thinks up a recipe: a few lollipops (Lolipop_1
    /// ... 7, e.g. 3x Lolipop 1 + 1x Lolipop 7) and a Pumpkin_On_Tree; the magic hat shows it. Ingredients dropped or
    /// thrown into the pot are taken in; once it holds as many as the recipe asks for, it starts cooking: the surface
    /// bubbles, coloured bubbles and skulls fly out and a meter fills above it. Firewood thrown into the fire under it
    /// makes the fire roar and the cooking go faster for a while. Done: the right recipe makes a laughing pumpkin
    /// (Pumpkin_0, 1 or 2) jump out; a wrong one spits out a black pumpkin (pumpkin_black) that bursts into a mini boss
    /// where it lands, and the magic hat shakes. Then it's empty, ready for the next try. Goes on Fire_Woods_n_Cauldron's
    /// root; it makes its own zones, meter and pot collider when the game runs.
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

        [Header("Recipe (a new one each time the PlayGround opens)")]
        [Tooltip("The lollipops a recipe can ask for (Lolipop_1 ... 7).")]
        [SerializeField] private GameObject[] lollipops = Array.Empty<GameObject>();
        [Tooltip("How many lollipops a recipe asks for, in all.")]
        [Min(1)] [SerializeField] private int minLollipops = 3;
        [Min(1)] [SerializeField] private int maxLollipops = 5;
        [Tooltip("How many different lollipops a recipe asks for.")]
        [Min(1)] [SerializeField] private int minKinds = 1;
        [Min(1)] [SerializeField] private int maxKinds = 2;
        [Tooltip("The pumpkin every recipe needs (Pumpkin_On_Tree), and how many.")]
        [SerializeField] private GameObject pumpkin;
        [Min(0)] [SerializeField] private int pumpkins = 1;

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
        [Tooltip("What a right recipe makes; one is picked at random (Pumpkin_0, 1, 2).")]
        [SerializeField] private GameObject[] results = Array.Empty<GameObject>();
        [Tooltip("Played with it; one at random.")]
        [SerializeField] private AudioClip[] laughs = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip successSound;
        [Tooltip("Optional: a GAMESTAGES bool that turns true when it cooks the right recipe, for the PlayGround's FSMs. " +
                 "Add it to the GAMESTAGES FSM's Variables.")]
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
        /// <summary>Raised when a right recipe finishes cooking, with what it made.</summary>
        public static event Action<Cauldron, GameObject> Cooked;

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
        private Transform head;

        public IReadOnlyList<Line> Recipe => recipe;
        public bool IsCooking => cooking;
        public float Heat => heat;
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
            Destroy(ingredient.gameObject);

            if (!cooking && count >= RecipeTotal) StartCooking();
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

            if (RightRecipe()) MakeResult(at);
            else Fail(at);

            contents.Clear();
            count = 0;
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

        // A laughing pumpkin jumps out of the pot.
        private void MakeResult(Vector3 at)
        {
            if (!string.IsNullOrEmpty(cookedBool)) GameStages.SetBool(cookedBool, true, this);
            Play(successSound, at);
            if (laughs.Length > 0) Play(laughs[Random.Range(0, laughs.Length)], at);

            var prefab = results.Length > 0 ? results[Random.Range(0, results.Length)] : null;
            if (prefab == null) return;

            var made = Instantiate(prefab, at + Vector3.up * 0.3f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
            if (made.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
            {
                var sideways = Random.insideUnitCircle.normalized * 1.2f;
                body.linearVelocity = new Vector3(sideways.x, 4.5f, sideways.y);
            }
            Cooked?.Invoke(this, made);
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

        // A few lollipops of one or two kinds, and the pumpkin.
        private void MakeRecipe()
        {
            recipe.Clear();
            var pool = new List<GameObject>();
            foreach (var lollipop in lollipops)
            {
                if (lollipop != null) pool.Add(lollipop);
            }
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            if (pool.Count > 0)
            {
                int kinds = Mathf.Clamp(Random.Range(minKinds, Mathf.Max(minKinds, maxKinds) + 1), 1, pool.Count);
                int total = Mathf.Max(kinds, Random.Range(minLollipops, Mathf.Max(minLollipops, maxLollipops) + 1));
                var counts = new int[kinds];
                for (int i = 0; i < kinds; i++) counts[i] = 1;
                for (int left = total - kinds; left > 0; left--) counts[Random.Range(0, kinds)]++;
                for (int i = 0; i < kinds; i++) recipe.Add(LineFor(pool[i], counts[i]));
            }
            if (pumpkin != null && pumpkins > 0) recipe.Add(LineFor(pumpkin, pumpkins));
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

        // "2 / 5" while ingredients go in; a filling bar while it cooks, orange when the fire is hot.
        private void UpdateMeter()
        {
            bool show = cooking || count > 0;
            if (!show)
            {
                if (meter != null && meter.gameObject.activeSelf) meter.gameObject.SetActive(false);
                return;
            }
            if (meter == null) BuildMeter();
            if (!meter.gameObject.activeSelf) meter.gameObject.SetActive(true);

            meterText.text = cooking ? (heat > 0.5f ? "Cooking hot!" : "Cooking...") : $"{count} / {RecipeTotal}";
            meterBar.gameObject.SetActive(cooking);
            var fill = meterFillImage.rectTransform;
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            meterFillImage.color = Color.Lerp(MeterFill, MeterHot, Mathf.Clamp01(heat / Mathf.Max(0.01f, maxHeat)));

            var top = pot != null && pot.TryGetComponent<Renderer>(out var shape) ? shape.bounds.max.y : MouthPoint().y;
            meter.position = new Vector3(transform.position.x, top + meterHeight, transform.position.z);
            if (head == null && Camera.main != null) head = Camera.main.transform;
            WorldUI.Face(meter, head);
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
