using System.Collections;
using System.Collections.Generic;
using Game.Locomotion;
using Game.Menus;
using Game.Saving;
using TMPro;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Lucky coins for getting back up. Every revive the player has left this run is a coin (one a run, plus Extra
    /// Revives from the level-up bonus and special chests; at most Max Coins are flipped at once). When the player goes
    /// down with a coin left, the fall goes on for a moment, then the game stops and their coins appear in a row in front
    /// of their eyes, all spinning in place (like Fear & Hunger's coin flip), slowing down until they stop one after
    /// another on a four-leaf clover or a skull, half and half. (Physics Throw on: a little table instead, the coins
    /// thrown on it with real physics, see Coin Toss.) Every clover
    /// gives Health Per Clover of their health (50%: one clover gets them up at half health, four at 200%); getting up
    /// uses up one coin for the rest of the run. All skulls and they stay down: the game over goes on, and the run is
    /// over. PlayerDeath's Try Revive starts it and waits for the coins; Player Revive then gets the player up with the
    /// health the clovers won. Goes on the XR Origin, next to Player Revive; Tools > 429 Game > Player > Set Up Revive
    /// adds it. Put your own coin in Coin Prefab and pick which side of it is the clover. Toss flips the same coins for
    /// other things (the Toss Coin action, e.g. the bedroom's shortcut door).
    /// </summary>
    [DisallowMultipleComponent]
    public class ReviveCoins : MonoBehaviour
    {
        public enum Side
        {
            Up,
            Down,
            Forward,
            Back,
            Right,
            Left,
        }

        [Header("Coin")]
        [Tooltip("The coin: any prefab with a clover on one side and a skull on the other. While it's shown, its " +
                 "colliders, rigidbodies and scripts are switched off.")]
        [SerializeField] private GameObject coinPrefab;
        [Tooltip("Which way the clover side faces, by the prefab's arrows in the Scene view: Up = the green Y arrow, " +
                 "Forward = the blue Z arrow, Right = the red X arrow; Down, Back and Left are the opposite ones.")]
        [SerializeField] private Side cloverSide = Side.Up;
        [Tooltip("Which way is up on the clover picture, the same way. The skull picture should be the same way up when " +
                 "the coin is turned around its up/down line.")]
        [SerializeField] private Side pictureUp = Side.Forward;
        [Tooltip("How wide a coin is shown, in metres (the prefab is scaled to it).")]
        [Min(0.01f)]
        [SerializeField] private float coinWidth = 0.09f;

        [Header("Rules")]
        [Tooltip("The most coins flipped at once (the player may have more revives left than this).")]
        [Range(1, 8)]
        [SerializeField] private int maxCoins = 4;
        [Tooltip("Health each clover gives, as a share of max health: 0.5 = 50%, so four clovers give 200%.")]
        [Min(0f)]
        [SerializeField] private float healthPerClover = 0.5f;
        [Tooltip("The chance each coin lands on the clover.")]
        [Range(0f, 1f)]
        [SerializeField] private float cloverChance = 0.5f;

        [Header("Physics throw")]
        [Tooltip("Throw all the coins at once with real physics onto a table in front of the eyes; the side that lands up " +
                 "counts. Off: the old toss, one coin after another (its settings are under Show).")]
        [SerializeField] private bool physicsThrow = false;
        [Tooltip("How far from the eyes the table is, in metres (the coins jump up toward the eyes from it).")]
        [Min(0.2f)]
        [SerializeField] private float tableDistance = 0.6f;
        [Tooltip("The table's radius, in metres; a low invisible wall at its edge keeps the coins on it.")]
        [Min(0.05f)]
        [SerializeField] private float tableRadius = 0.19f;
        [Tooltip("How high the coins are thrown, toward the eyes, in metres.")]
        [Min(0.02f)]
        [SerializeField] private float throwHeight = 0.15f;
        [Tooltip("How many times a second a coin turns over in the air: a random number from x to y.")]
        [SerializeField] private Vector2 turnsPerSecond = new(2f, 4f);
        [Tooltip("How much bigger the coins are in their physics world than on screen. Bigger = a slower, floatier fall.")]
        [Range(1f, 20f)]
        [SerializeField] private float tossScale = 6.5f;
        [Tooltip("How bouncy the coins are (0 = not at all, 1 = fully), and how much they slide.")]
        [Range(0f, 1f)]
        [SerializeField] private float bounciness = 0.25f;
        [Range(0f, 1f)]
        [SerializeField] private float friction = 0.4f;
        [Tooltip("Longest the throw may take, in seconds; then every coin counts as it lies.")]
        [Min(1f)]
        [SerializeField] private float maxThrowSeconds = 4f;
        [Tooltip("Optional: a model shown as the table (e.g. a furniture prefab), its top toward the eyes, sized so the " +
                 "coins land on its top. Only its look is used. Empty: a plain disc in Table Color with a rim.")]
        [SerializeField] private GameObject tablePrefab;
        [Tooltip("The table's colour, and its rim's (the plain disc, when there's no Table Prefab).")]
        [SerializeField] private Color tableColor = new(0.09f, 0.04f, 0.13f, 0.8f);
        [SerializeField] private Color tableRimColor = new(1f, 0.78f, 0.3f, 0.9f);

        [Header("Show")]
        [Tooltip("Seconds the death fall goes on before the game stops for the coins.")]
        [Min(0f)]
        [SerializeField] private float fallFirst = 0.45f;
        [Tooltip("How far in front of the eyes the coins are, in metres.")]
        [Min(0.15f)]
        [SerializeField] private float distance = 0.5f;
        [Tooltip("How far below eye height, in metres.")]
        [SerializeField] private float belowEyes = 0.04f;
        [Tooltip("From one coin's middle to the next, in metres.")]
        [Min(0f)]
        [SerializeField] private float spacing = 0.11f;
        [Tooltip("How high the coins rise while they spin, in metres.")]
        [Min(0f)]
        [SerializeField] private float tossHeight = 0.015f;
        [Tooltip("Seconds the first coin spins before it stops.")]
        [Min(0.2f)]
        [SerializeField] private float flipSeconds = 1.8f;
        [Tooltip("How many times the first coin turns over (later ones a few more, as they spin longer).")]
        [Min(1)]
        [SerializeField] private int turns = 7;
        [Tooltip("Seconds from one coin stopping to the next.")]
        [Min(0f)]
        [SerializeField] private float nextCoinAfter = 0.55f;
        [Tooltip("How far the coins rock from side to side once they've stopped, in degrees (shows their rim).")]
        [Min(0f)]
        [SerializeField] private float rockDegrees = 12f;
        [Tooltip("Seconds the result stays once every coin has landed.")]
        [Min(0f)]
        [SerializeField] private float resultSeconds = 1.5f;

        [Header("Look")]
        [SerializeField] private Color cloverColor = new(0.35f, 1f, 0.4f, 1f);
        [SerializeField] private Color skullColor = new(1f, 0.22f, 0.18f, 1f);
        [Tooltip("A soft light in front of the coins, so they show in the dark.")]
        [SerializeField] private Color coinLight = new(1f, 0.93f, 0.78f, 1f);
        [Min(0f)]
        [SerializeField] private float coinLightIntensity = 0.35f;
        [Tooltip("Write the result above the coins (\"+150% health\", \"Out of luck...\"). Off: the coins' faces say it.")]
        [SerializeField] private bool showResultText;
        [Tooltip("The result's font. Empty: TextMeshPro's default font.")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Sounds")]
        [SerializeField] private AudioClip tossSound;
        [Tooltip("One of these as each coin lands.")]
        [SerializeField] private AudioClip[] landSounds = new AudioClip[0];
        [Tooltip("For each clover, a little higher each time.")]
        [SerializeField] private AudioClip cloverSound;
        [SerializeField] private AudioClip skullSound;
        [Tooltip("As the result shows, when the player gets up.")]
        [SerializeField] private AudioClip winSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.9f;

        // Assets/Resources/Damage Number.mat: TextMeshPro drawn on top, with an outline (it ships with the build).
        private const string TextMaterial = "Damage Number";
        private const float AppearSeconds = 0.3f;
        private const float SettleSeconds = 0.4f;
        private const float GoneSeconds = 0.3f;

        private class Coin
        {
            public Transform pivot;
            public CoinToss.Coin toss;
            public bool announced;
            public float announcedAt;
            public float lastClink;
            public Vector3 home;
            public bool clover;
            public float spinFor;
            public bool tossed;
            public bool landed;
            public Light glow;
            public AudioSource sound;
        }

        private readonly List<Coin> coins = new();
        private Transform show;
        private CoinToss toss;
        private static Texture2D tableTexture;
        private TextMeshPro text;
        private HeadsetFade fade;
        private bool held;
        private Coroutine running;
        private float showTime;
        // Flipping to get the player up (after the death fall, darkening after), not a Toss for something else.
        private bool forRevive;

        public static ReviveCoins Instance { get; private set; }

        /// <summary>Whether the coins are being shown (from the moment the player goes down until the result has gone).</summary>
        public bool IsFlipping { get; private set; }

        /// <summary>How many coins landed on the clover last time.</summary>
        public int Clovers { get; private set; }

        /// <summary>The health the last flip won, as a share of max health (0 = the player stays down).</summary>
        public float HealthShare => Clovers * healthPerClover;

        /// <summary>The coins the player would flip if they went down now: their revives left, at most Max Coins.</summary>
        public int CoinsNow => Mathf.Clamp(Checkpoints.RevivesLeft, 0, maxCoins);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Revive Coins] The scene already has one ({Instance.name}); '{name}' is ignored.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CleanUp();
        }

        private void OnDisable()
        {
            Cancel();
        }

        /// <summary>The player went down: flips the coins they have left (see Clovers and Health Share once it's over).</summary>
        public void Flip()
        {
            if (IsFlipping || !isActiveAndEnabled) return;
            IsFlipping = true;
            Clovers = 0;
            forRevive = true;
            running = StartCoroutine(Flipping(Mathf.Max(1, CoinsNow)));
        }

        /// <summary>
        /// Tosses coins for something other than getting up, e.g. the bedroom's shortcut door: the same show (the game
        /// stops meanwhile), without the death fall first or the darkening after. Once Is Flipping is false again, Clovers
        /// says how many landed on the clover. False (nothing tossed) if coins are already being flipped.
        /// </summary>
        public bool Toss(int count = 1)
        {
            if (IsFlipping || !isActiveAndEnabled) return false;
            IsFlipping = true;
            Clovers = 0;
            forRevive = false;
            running = StartCoroutine(Flipping(Mathf.Clamp(count, 1, maxCoins)));
            return true;
        }

        /// <summary>Stops the coins at once (the game goes on, nobody gets up).</summary>
        public void Cancel()
        {
            if (running != null) StopCoroutine(running);
            running = null;
            CleanUp();
            IsFlipping = false;
        }

        private IEnumerator Flipping(int count)
        {
            // The fall goes on for a moment first.
            if (forRevive)
            {
                for (float t = 0f; t < fallFirst; t += Time.unscaledDeltaTime) yield return null;
            }

            // Then the game stops (time and sound), and the view stays as it is.
            PauseMenu.Hold(this);
            held = true;
            fade = forRevive ? HeadsetFade.Find() : null;
            if (fade != null) fade.FadeTo(fade.Alpha, 0f);

            var eyes = Camera.main != null ? Camera.main.transform : null;
            if (eyes == null || coinPrefab == null)
            {
                if (coinPrefab == null) Debug.LogWarning("[Revive Coins] No Coin Prefab: the coins are flipped without being shown.", this);
                for (int i = 0; i < count; i++)
                {
                    if (Random.value < cloverChance) Clovers++;
                }
                Finish();
                yield break;
            }

            bool thrown = false;
            if (physicsThrow)
            {
                try
                {
                    BuildTable(eyes, count);
                    thrown = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Revive Coins] The physics throw couldn't be set up, so the coins are tossed the old way: " + e.Message, this);
                    CleanUpShow();
                }
            }
            if (thrown)
            {
                yield return Throwing();
            }
            else
            {
                BuildShow(eyes, count);

                // They all spin, then stop one after another; wait till the last one has settled.
                showTime = 0f;
                float allDone = flipSeconds + (count - 1) * nextCoinAfter + SettleSeconds;
                int cloversSoFar = 0;
                while (showTime < allDone)
                {
                    if (PauseMenu.IsPaused)
                    {
                        yield return WhilePaused();
                        continue;
                    }
                    showTime += Time.unscaledDeltaTime;
                    foreach (var coin in coins) Animate(coin, showTime, ref cloversSoFar);
                    yield return null;
                }
            }

            // The result.
            ShowResult(count);
            for (float t = 0f; t < resultSeconds; t += Time.unscaledDeltaTime)
            {
                if (PauseMenu.IsPaused)
                {
                    yield return WhilePaused();
                    continue;
                }
                if (toss == null)
                {
                    // Still rocking.
                    showTime += Time.unscaledDeltaTime;
                    int none = 0;
                    foreach (var coin in coins) Animate(coin, showTime, ref none);
                }
                foreach (var coin in coins) Glow(coin, 1f + 0.25f * Mathf.Sin(t * 9f));
                yield return null;
            }

            // Away they go.
            for (float t = 0f; t < GoneSeconds; t += Time.unscaledDeltaTime)
            {
                if (PauseMenu.IsPaused)
                {
                    yield return WhilePaused();
                    continue;
                }
                float s = 1f - Mathf.SmoothStep(0f, 1f, t / GoneSeconds);
                show.localScale = Vector3.one * s;
                yield return null;
            }
            // Not into the dark over an open menu.
            yield return WhilePaused();
            Finish();
        }

        // While the pause menu (or the headset's system menu) is open, the show stands still and is hidden.
        private IEnumerator WhilePaused()
        {
            if (!PauseMenu.IsPaused) yield break;
            if (show != null) show.gameObject.SetActive(false);
            while (PauseMenu.IsPaused) yield return null;
            if (show != null) show.gameObject.SetActive(true);
        }

        private void Finish()
        {
            CleanUp();
            // The view goes on darkening as the fall goes on: all the way for a game over, part way for a revive.
            if (fade != null) fade.FadeOut(2f);
            IsFlipping = false;
            running = null;
        }

        private void CleanUp()
        {
            CleanUpShow();
            if (held)
            {
                held = false;
                PauseMenu.Release(this);
            }
        }

        private void CleanUpShow()
        {
            if (show != null) Destroy(show.gameObject);
            show = null;
            text = null;
            coins.Clear();
            toss?.Dispose();
            toss = null;
        }

        // ---- The physics throw

        // Turns the toss world (Y up from the table) toward the eyes: its up is toward them, so the table is seen from
        // above and a coin that lands shows its face.
        private static readonly Quaternion TowardEyes = Quaternion.AngleAxis(-90f, Vector3.right);

        // The table in front of the eyes (going with the view, as the coins do in the old toss), the coins on it and their
        // physics world.
        private void BuildTable(Transform eyes, int count)
        {
            float k = Mathf.Max(1f, tossScale);
            toss = new CoinToss(coinPrefab, Axis(cloverSide), count, new CoinToss.Settings
            {
                coinWidth = coinWidth * k,
                tableRadius = tableRadius * k,
                throwHeight = throwHeight * k,
                turnsPerSecond = turnsPerSecond,
                bounciness = bounciness,
                friction = friction,
            });

            show = new GameObject("Revive Coins").transform;
            show.SetParent(eyes, false);
            show.localPosition = new Vector3(0f, -belowEyes, tableDistance);
            show.localRotation = Quaternion.identity;

            var lamp = new GameObject("Coin Light").AddComponent<Light>();
            lamp.transform.SetParent(show, false);
            // Above, to the side and toward the eyes, so the coins' faces don't mirror it straight into them.
            lamp.transform.localPosition = new Vector3(-0.14f, 0.2f, -0.22f);
            lamp.type = LightType.Point;
            lamp.range = 1f;
            lamp.intensity = coinLightIntensity;
            lamp.color = coinLight;
            lamp.shadows = LightShadows.None;

            if (tablePrefab != null) AddTableModel();
            else AddTableDisc();

            for (int i = 0; i < count; i++)
            {
                var tc = toss.Coins[i];
                var coin = new Coin { toss = tc };
                coin.pivot = new GameObject("Coin " + (i + 1)).transform;
                coin.pivot.SetParent(show, false);
                var model = Instantiate(coinPrefab, coin.pivot);
                model.name = coinPrefab.name;
                Quiet(model);
                // The same shape and place as its physics copy, at screen size.
                model.transform.localPosition = tc.Model.localPosition / k;
                model.transform.localRotation = tc.Model.localRotation;
                model.transform.localScale = tc.Model.localScale / k;

                coin.glow = new GameObject("Glow").AddComponent<Light>();
                coin.glow.transform.SetParent(show, false);
                coin.glow.type = LightType.Point;
                coin.glow.range = 0.1f;
                coin.glow.intensity = 0f;
                coin.glow.shadows = LightShadows.None;

                coin.sound = coin.pivot.gameObject.AddComponent<AudioSource>();
                coin.sound.playOnAwake = false;
                coin.sound.spatialBlend = 0f;
                coin.sound.ignoreListenerPause = true;
                coin.sound.volume = volume;
                coins.Add(coin);
                Place(coin);
            }
        }

        // The Table Prefab, its top toward the eyes and just behind the coins lying on it, sized so the coins' landing
        // area (Table Radius, where their invisible wall is) fits inside its top.
        private void AddTableModel()
        {
            var pivot = new GameObject("Table").transform;
            pivot.SetParent(show, false);
            pivot.localPosition = new Vector3(0f, 0f, 0.0015f);
            pivot.localRotation = TowardEyes;
            var model = Instantiate(tablePrefab, pivot);
            model.name = tablePrefab.name;
            Quiet(model);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var own = OwnBounds(model.transform, out bool any);
            float top = any ? Mathf.Min(own.size.x, own.size.z) : 1f;
            float scale = top > 0.0001f ? tableRadius * 2.2f / top : 1f;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = -new Vector3(own.center.x, own.max.y, own.center.z) * scale;
        }

        // A soft dark disc with a golden rim, just behind the coins lying on it.
        private void AddTableDisc()
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Quad);
            table.name = "Table";
            Destroy(table.GetComponent<Collider>());
            table.transform.SetParent(show, false);
            table.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            table.transform.localScale = Vector3.one * (tableRadius * 2.24f);
            var look = new Material(Shader.Find("Sprites/Default")) { mainTexture = TableTexture(), name = "Coin Table" };
            table.GetComponent<MeshRenderer>().sharedMaterial = look;
        }

        private IEnumerator Throwing()
        {
            // The table and the coins pop in, then they're all thrown at once.
            for (float t = 0f; t < AppearSeconds; t += Time.unscaledDeltaTime)
            {
                if (PauseMenu.IsPaused)
                {
                    yield return WhilePaused();
                    continue;
                }
                show.localScale = Vector3.one * Back(Mathf.Clamp01(t / AppearSeconds));
                yield return null;
            }
            show.localScale = Vector3.one;
            if (coins.Count > 0) Play(coins[coins.Count / 2].sound, tossSound, Random.Range(0.95f, 1.1f));

            float restingFor = 0f;
            int cloversSoFar = 0;
            while (true)
            {
                if (PauseMenu.IsPaused)
                {
                    yield return WhilePaused();
                    continue;
                }
                float dt = Time.unscaledDeltaTime;
                toss.Step(dt);
                bool timeUp = toss.Elapsed >= maxThrowSeconds;
                if (timeUp) toss.Finish();
                foreach (var coin in coins)
                {
                    Place(coin);
                    Listen(coin, ref cloversSoFar);
                }
                restingFor = toss.AllSettled ? restingFor + dt : 0f;
                if (timeUp || restingFor >= 0.25f) break;
                yield return null;
            }
            Clovers = 0;
            foreach (var coin in coins)
            {
                if (coin.toss.Clover) Clovers++;
            }
        }

        // The shown coin where its physics copy is.
        private void Place(Coin coin)
        {
            float k = Mathf.Max(1f, tossScale);
            toss.Pose(coin.toss, out var position, out var rotation);
            coin.pivot.localPosition = TowardEyes * (position / k);
            coin.pivot.localRotation = TowardEyes * rotation;
            if (coin.glow != null) coin.glow.transform.localPosition = coin.pivot.localPosition + new Vector3(0f, 0f, -0.06f);
        }

        // Clinks as it lands, and once it's at rest it lights up green (clover) or red (skull) with its sound.
        private void Listen(Coin coin, ref int cloversSoFar)
        {
            var tc = coin.toss;
            float now = Time.unscaledTime;
            if (tc.Impact > 0f && now - coin.lastClink > 0.07f && landSounds != null && landSounds.Length > 0)
            {
                coin.lastClink = now;
                Play(coin.sound, landSounds[Random.Range(0, landSounds.Length)], Random.Range(1.4f, 1.7f), Mathf.Clamp01(tc.Impact / 6f) * 0.8f + 0.2f);
            }
            if (tc.Settled && !coin.announced)
            {
                coin.announced = true;
                coin.announcedAt = now;
                coin.glow.color = tc.Clover ? cloverColor : skullColor;
                if (tc.Clover)
                {
                    Play(coin.sound, cloverSound, 1f + 0.12f * cloversSoFar);
                    cloversSoFar++;
                }
                else
                {
                    Play(coin.sound, skullSound, 1f);
                }
            }
            else if (!tc.Settled && coin.announced)
            {
                // Knocked over again by another coin: it lights up again when it's still.
                coin.announced = false;
                if (tc.Clover) cloversSoFar = Mathf.Max(0, cloversSoFar - 1);
            }
            float since = now - coin.announcedAt;
            Glow(coin, coin.announced ? Mathf.Clamp01(since / 0.12f) * (1f + 1.5f * Mathf.Exp(-6f * since)) : 0f);
        }

        // A dark disc fading out toward its edge, with a thin golden rim.
        private Texture2D TableTexture()
        {
            if (tableTexture != null) return tableTexture;
            const int size = 128;
            tableTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Coin Table", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            // The table reaches 1 / 1.12 of the picture's radius; the rim sits on its edge.
            const float edge = 1f / 1.12f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
                    var c = tableColor;
                    c.a *= Mathf.Clamp01((edge - r) / 0.08f) * Mathf.Lerp(1f, 0.75f, r / edge);
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(r - edge) / 0.025f);
                    px[y * size + x] = Color.Lerp(c, tableRimColor, rim * tableRimColor.a);
                }
            }
            tableTexture.SetPixels(px);
            tableTexture.Apply();
            return tableTexture;
        }

        // ---- The old toss

        // The coins in a row in front of the eyes, facing them, with a soft light so they show in the dark. They go with
        // the view: dying puts the camera on the death fall (somewhere else), and the coins must stay in sight.
        private void BuildShow(Transform eyes, int count)
        {
            show = new GameObject("Revive Coins").transform;
            show.SetParent(eyes, false);
            show.localPosition = new Vector3(0f, -belowEyes, distance);
            show.localRotation = Quaternion.identity;

            var lamp = new GameObject("Coin Light").AddComponent<Light>();
            lamp.transform.SetParent(show, false);
            // From above and to the side, so shiny coins facing the eyes don't mirror it straight into them.
            lamp.transform.localPosition = new Vector3(-0.22f, 0.26f, -0.04f);
            lamp.type = LightType.Point;
            lamp.range = 1f;
            lamp.intensity = coinLightIntensity;
            lamp.color = coinLight;
            lamp.shadows = LightShadows.None;

            // The clover side towards the eyes, its picture the right way up.
            var cloverAxis = Axis(cloverSide);
            var upAxis = Axis(pictureUp);
            if (Mathf.Abs(Vector3.Dot(cloverAxis, upAxis)) > 0.9f) upAxis = Mathf.Abs(cloverAxis.y) > 0.9f ? Vector3.forward : Vector3.up;
            var facing = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(cloverAxis, upAxis));

            for (int i = 0; i < count; i++)
            {
                var coin = new Coin
                {
                    home = new Vector3((i - (count - 1) * 0.5f) * spacing, 0f, 0f),
                    clover = Random.value < cloverChance,
                    spinFor = flipSeconds + i * nextCoinAfter,
                };
                coin.pivot = new GameObject("Coin " + (i + 1)).transform;
                coin.pivot.SetParent(show, false);
                coin.pivot.localPosition = coin.home;

                var model = Instantiate(coinPrefab, coin.pivot);
                model.name = coinPrefab.name;
                Quiet(model);
                model.transform.localRotation = facing;
                model.transform.localScale = Vector3.one;
                // Its size and middle in its own space (turned any way, its box in the world would be bigger).
                var own = OwnBounds(model.transform, out bool any);
                float width = any ? Mathf.Max(own.size.x, Mathf.Max(own.size.y, own.size.z)) : 0f;
                float scale = width > 0.0001f ? coinWidth / width : 1f;
                model.transform.localScale = Vector3.one * scale;
                // Centred on the pivot, whatever the prefab's own middle is.
                model.transform.localPosition = -(facing * (own.center * scale));

                coin.glow = new GameObject("Glow").AddComponent<Light>();
                coin.glow.transform.SetParent(coin.pivot, false);
                coin.glow.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                coin.glow.type = LightType.Point;
                // Its own coin only, not the next one.
                coin.glow.range = 0.12f;
                coin.glow.intensity = 0f;
                coin.glow.color = coin.clover ? cloverColor : skullColor;
                coin.glow.shadows = LightShadows.None;

                coin.sound = coin.pivot.gameObject.AddComponent<AudioSource>();
                coin.sound.playOnAwake = false;
                coin.sound.spatialBlend = 0f;
                // Heard while the rest of the game's sound is paused.
                coin.sound.ignoreListenerPause = true;
                coin.sound.volume = volume;
                coin.sound.panStereo = count > 1 ? Mathf.Lerp(-0.35f, 0.35f, i / (float)(count - 1)) : 0f;

                coin.pivot.localScale = Vector3.zero;
                coins.Add(coin);
            }
        }

        private void Animate(Coin coin, float time, ref int cloversSoFar)
        {
            // Pops in.
            float appear = Mathf.Clamp01(time / AppearSeconds);
            coin.pivot.localScale = Vector3.one * Back(appear);

            if (!coin.tossed)
            {
                coin.tossed = true;
                Play(coin.sound, tossSound, Random.Range(0.95f, 1.15f));
            }

            // A skull lands turned around its up/down line, so its picture is the right way up.
            var result = coin.clover ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            float u = time / coin.spinFor;
            if (u < 1f)
            {
                // Spins head over heels where it is, fast at first and slower and slower, until it stops on its face (the
                // last slow turn keeps everyone guessing). Coins that spin longer turn more, so they all slow alike.
                float turnsHere = turns * coin.spinFor / flipSeconds;
                float slowing = 1f - Mathf.Pow(1f - u, 2.2f);
                coin.pivot.localPosition = coin.home + Vector3.up * (tossHeight * Mathf.Sin(Mathf.PI * u));
                coin.pivot.localRotation = Quaternion.AngleAxis(360f * turnsHere * (slowing - 1f), Vector3.right) * result;
                return;
            }

            if (!coin.landed)
            {
                coin.landed = true;
                if (landSounds != null && landSounds.Length > 0) Play(coin.sound, landSounds[Random.Range(0, landSounds.Length)], 1.5f);
                if (coin.clover)
                {
                    Play(coin.sound, cloverSound, 1f + 0.12f * cloversSoFar);
                    cloversSoFar++;
                    Clovers++;
                }
                else
                {
                    Play(coin.sound, skullSound, 1f);
                }
            }

            // Stops with a little wobble, lights up green (clover) or red (skull), then rocks gently from side to side so
            // the light runs over it and its rim shows.
            float since = time - coin.spinFor;
            float wobble = 14f * Mathf.Exp(-9f * since) * Mathf.Sin(since * 30f);
            float rock = rockDegrees * Mathf.SmoothStep(0f, 1f, since / 0.6f) * Mathf.Sin(since * 1.8f + coin.home.x * 20f);
            coin.pivot.localPosition = coin.home;
            coin.pivot.localRotation = Quaternion.AngleAxis(rock, Vector3.up) * Quaternion.AngleAxis(wobble, Vector3.right) * result;
            Glow(coin, Mathf.Clamp01(since / 0.12f) * (1f + 1.5f * Mathf.Exp(-6f * since)));
        }

        private void Glow(Coin coin, float amount)
        {
            if (coin.glow != null) coin.glow.intensity = 0.045f * amount;
        }

        // The win sound for any clover; with Show Result Text, "+150% health" and the coins left, or "Out of luck...",
        // above the coins.
        private void ShowResult(int count)
        {
            if (Clovers > 0 && winSound != null && coins.Count > 0) Play(coins[0].sound, winSound, 1f);
            if (!showResultText) return;
            if (text == null) text = MakeText();
            if (text == null) return;
            if (Clovers > 0)
            {
                int percent = Mathf.RoundToInt(HealthShare * 100f);
                // Getting up used one coin up (taken as the coins were thrown).
                int left = Mathf.Max(0, Checkpoints.RevivesLeft);
                string coinsLeft = left == 0 ? "no coins left" : left == 1 ? "1 coin left" : $"{left} coins left";
                text.text = $"+{percent}% health\n<size=55%><color=#FFE9A8>{coinsLeft}</color></size>";
                text.color = cloverColor;
            }
            else
            {
                text.text = count > 1 ? "Out of luck..." : "Skull...";
                text.color = skullColor;
            }
        }

        private TextMeshPro MakeText()
        {
            var go = new GameObject("Result");
            go.transform.SetParent(show, false);
            // Above the table, or above the row of coins.
            go.transform.localPosition = toss != null ? new Vector3(0f, tableRadius + 0.02f, -0.01f) : new Vector3(0f, coinWidth * 0.5f + 0.03f, 0f);
            // A TextMeshPro at font size 10 is about 1 unit tall: 3.5 cm.
            go.transform.localScale = Vector3.one * 0.035f;
            var tmp = go.AddComponent<TextMeshPro>();
            var useFont = font != null ? font : TMP_Settings.defaultFontAsset;
            if (useFont == null) return null;
            tmp.font = useFont;
            var look = font == null ? Resources.Load<Material>(TextMaterial) : null;
            if (look != null) tmp.fontSharedMaterial = look;
            tmp.fontSize = 10f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Bottom;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(12f, 3f);
            tmp.rectTransform.pivot = new Vector2(0.5f, 0f);
            return tmp;
        }

        private static void Play(AudioSource source, AudioClip clip, float pitch, float loudness = 1f)
        {
            if (source == null || clip == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, loudness);
        }

        // A coin from any prefab: just its look.
        private static void Quiet(GameObject model)
        {
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in model.GetComponentsInChildren<Rigidbody>(true))
            {
                r.isKinematic = true;
                r.detectCollisions = false;
            }
            foreach (var b in model.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            foreach (var a in model.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            foreach (var p in model.GetComponentsInChildren<ParticleSystem>(true))
            {
                p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                p.gameObject.SetActive(false);
            }
        }

        // The box around the coin's meshes in the coin's own space.
        internal static Bounds OwnBounds(Transform model, out bool any)
        {
            any = false;
            var bounds = new Bounds();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                else if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var m = model.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) bounds = new Bounds(p, Vector3.zero);
                    else bounds.Encapsulate(p);
                    any = true;
                }
            }
            return bounds;
        }

        private static Vector3 Axis(Side side)
        {
            switch (side)
            {
                case Side.Down: return Vector3.down;
                case Side.Forward: return Vector3.forward;
                case Side.Back: return Vector3.back;
                case Side.Right: return Vector3.right;
                case Side.Left: return Vector3.left;
                default: return Vector3.up;
            }
        }

        // Overshoots a little and settles, for popping in.
        private static float Back(float t)
        {
            const float s = 1.7f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
