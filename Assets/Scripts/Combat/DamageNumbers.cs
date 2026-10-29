using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Floating damage numbers: whenever anything with a Health FSM (an FSM whose name ends in "Health" with a float
    /// "health") loses health, a small red number pops up at its upper right, floats up and fades. Enemies, bosses,
    /// breakable pumpkins, furniture, barrels: whatever lowered the health (a weapon, a thrown thing, a shot, a blast)
    /// shows. Big hits (the ones that knock enemies back) are bigger and orange-red; the killing blow is the biggest.
    /// The numbers always face the player, keep the same size on screen at any distance and draw on top, so they're
    /// never hidden inside what was hit. The player's own health (PlayerHealth, the XR rig) and objects with nothing to
    /// see (e.g. a game-logic FSM holding a counter called health) never show numbers.
    /// It makes itself when a scene starts (default look); put one in the scene to change the look. The player switches
    /// the numbers on or off in the pause menu (Enabled; kept between sessions in PlayerPrefs, off for a new player).
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageNumbers : MonoBehaviour
    {
        [Tooltip("Show damage numbers at all in this scene. The player's own on/off (pause menu) is Enabled.")]
        [SerializeField] private bool show = true;
        [Tooltip("Font. Empty: TextMeshPro's default font.")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Colour of a normal hit's number.")]
        [SerializeField] private Color normalColor = new(1f, 0.16f, 0.12f, 1f);
        [Tooltip("Colour of a big hit's number (the ones that knock enemies back) and of the killing blow.")]
        [SerializeField] private Color bigColor = new(1f, 0.42f, 0.08f, 1f);
        [Tooltip("Height of a normal hit's number, as an angle of view in degrees (it keeps this size at any distance).")]
        [Range(0.5f, 6f)]
        [SerializeField] private float size = 2.4f;
        [Tooltip("How much bigger a big hit's number is, and the killing blow's.")]
        [Min(1f)]
        [SerializeField] private float bigScale = 1.35f;
        [Min(1f)]
        [SerializeField] private float killScale = 1.6f;
        [Tooltip("How long a number stays, in seconds, and how far it floats up meanwhile, in metres.")]
        [Min(0.2f)]
        [SerializeField] private float lifetime = 0.9f;
        [Min(0f)]
        [SerializeField] private float rise = 0.25f;
        [Tooltip("At most this many numbers at once; the oldest is reused after that.")]
        [Range(4, 64)]
        [SerializeField] private int maxNumbers = 20;
        [Tooltip("The health variable watched in every FSM whose name ends in Health.")]
        [SerializeField] private string healthVariable = "health";

        private const string OverlayShader = "TextMeshPro/Mobile/Distance Field Overlay";
        // Assets/Resources/Damage Number.mat: the on-top shader with its outline switched on. Being in Resources, it
        // makes the build keep that shader and its outline (Unity drops shader variants no shipped material uses).
        private const string LookMaterial = "Damage Number";
        // New Health FSMs (things spawned during play) are picked up this often, in seconds.
        private const float RescanEvery = 0.5f;

        private class Popup
        {
            public TextMeshPro text;
            public Transform target;
            public Vector3 offset;
            public Vector3 basePosition;
            public float bornAt;
            public float scale;
            public Color color;
        }

        private class Watched
        {
            public PlayMakerFSM fsm;
            public HutongGames.PlayMaker.FsmFloat health;
            public float last;
            // Shown even while its FSM is switched off (Enemy Hit Damage keeps the health: the pumpkin boss).
            public bool always;
            // A thing that can be picked up (a weapon): no numbers while the player holds or has just thrown it.
            public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;
        }

        private static DamageNumbers instance;
        // When each object last took a big hit (told by Enemy Hit Damage), so its number shows big.
        private static readonly Dictionary<int, float> bigHits = new();
        // Objects whose health the game itself is about to empty (Quiet): no number for that.
        private static readonly Dictionary<int, float> quiet = new();
        // Health FSMs handed over by Enemy Hit Damage (Watch) before a Damage Numbers was there to take them.
        private static readonly List<Watched> handedOver = new();
        private readonly List<Popup> popups = new();
        private readonly List<Watched> watched = new();
        private readonly HashSet<PlayMakerFSM> known = new();
        private readonly List<Renderer> renderers = new();
        private Material material;
        private Camera eyes;
        private float nextScan;

        // Where the player's choice is kept (off for a new player).
        private const string EnabledKey = "429 Game.DamageNumbers";
        private static bool? chosen;

        /// <summary>The player's choice (pause menu): whether numbers show. Setting it saves it; off hides any showing.</summary>
        public static bool Enabled
        {
            get => chosen ??= PlayerPrefs.GetInt(EnabledKey, 0) != 0;
            set
            {
                if (value == Enabled) return;
                chosen = value;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value && instance != null) instance.HideAll();
            }
        }

        public static string Label() => Enabled ? "Damage numbers: on" : "Damage numbers: off";

        /// <summary>The settings were wiped (reset to a new install): the choice is read again, off for a new player.</summary>
        public static void ForgetChoice() => chosen = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            chosen = null;
            bigHits.Clear();
            quiet.Clear();
            handedOver.Clear();
        }

        // One per scene, made when the scene starts unless the scene already has one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MakeOne()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Ensure();

        private static void Ensure()
        {
            if (instance == null) instance = FindAnyObjectByType<DamageNumbers>();
            if (instance == null) new GameObject("Damage Numbers").AddComponent<DamageNumbers>();
        }

        /// <summary>The next health drop of <paramref name="target"/> (within a moment) was a big hit: show it big.</summary>
        public static void NoteBigHit(GameObject target)
        {
            // A moment's grace: the health drop is seen on the next frame, which can be late in the editor.
            if (target != null) bigHits[target.GetInstanceID()] = Time.time + 0.4f;
        }

        /// <summary>
        /// The game is about to empty <paramref name="target"/>'s health itself (a tree pumpkin over the limit, a fall off
        /// the edge, paying at the seed lock, a weapon breaking in the hand): no number for that, as the player didn't hit it.
        /// </summary>
        public static void Quiet(GameObject target)
        {
            if (target != null) quiet[target.GetInstanceID()] = Time.time + 0.4f;
        }

        /// <summary>
        /// Watches a Health FSM that's switched off (its health kept by Enemy Hit Damage, e.g. the pumpkin boss's), which
        /// the scan for running FSMs would miss.
        /// </summary>
        public static void Watch(PlayMakerFSM fsm, HutongGames.PlayMaker.FsmFloat health)
        {
            if (fsm == null || health == null) return;
            var w = new Watched { fsm = fsm, health = health, last = health.Value, always = true };
            if (instance != null)
            {
                instance.known.Add(fsm);
                instance.watched.RemoveAll(other => other.fsm == fsm);
                instance.watched.Add(w);
            }
            else handedOver.Add(w);
        }

        // Quieted (Quiet), it or what it sits on.
        private static bool IsQuiet(Transform target)
        {
            for (int up = 0; target != null && up < 3; up++, target = target.parent)
            {
                if (quiet.TryGetValue(target.gameObject.GetInstanceID(), out float until) && Time.time <= until) return true;
            }
            return false;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (material != null) Destroy(material);
        }

        private void Update()
        {
            if (Time.time >= nextScan) Rescan();
            if (handedOver.Count > 0)
            {
                foreach (var w in handedOver)
                {
                    if (w.fsm == null) continue;
                    known.Add(w.fsm);
                    watched.RemoveAll(other => other.fsm == w.fsm);
                    watched.Add(w);
                }
                handedOver.Clear();
            }
            for (int i = watched.Count - 1; i >= 0; i--)
            {
                var w = watched[i];
                if (w.fsm == null)
                {
                    watched.RemoveAt(i);
                    continue;
                }
                float now = w.health.Value;
                if (now >= w.last)
                {
                    w.last = now;
                    continue;
                }
                float before = w.last;
                w.last = now;
                // Nothing for hits on something already dead or broken, or while it's switched off.
                bool on = w.always ? w.fsm.gameObject.activeInHierarchy : w.fsm.isActiveAndEnabled;
                if (before <= 0f || !show || !Enabled || !on) continue;
                // Nor when the game emptied it itself (Quiet), nor on a weapon wearing down in the player's hands.
                if (now <= 0f && IsQuiet(w.fsm.transform)) continue;
                if (w.grab != null && HeldThings.InUse(w.grab)) continue;
                float lost = before - now;
                // A "deal 999 damage" kill shows what it really had left.
                if (now <= 0f && lost > 50f && lost > before * 3f) lost = before;
                if (lost < 0.25f) continue;
                int id = w.fsm.gameObject.GetInstanceID();
                bool big = bigHits.TryGetValue(id, out float until) && Time.time <= until;
                if (big) bigHits.Remove(id);
                Spawn(w.fsm.transform, lost, big, now <= 0f);
            }
        }

        // Picks up every Health FSM that's running (PlayMaker keeps the list), except the player's.
        private void Rescan()
        {
            nextScan = Time.time + RescanEvery;
            foreach (var fsm in PlayMakerFSM.FsmList)
            {
                if (fsm == null || known.Contains(fsm)) continue;
                known.Add(fsm);
                if (!fsm.FsmName.EndsWith("Health") || fsm.FsmName == "PlayerHealth") continue;
                if (fsm.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) continue;
                var health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                if (health == null) continue;
                watched.Add(new Watched
                {
                    fsm = fsm, health = health, last = health.Value,
                    grab = fsm.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(),
                });
            }
            known.RemoveWhere(f => f == null);
            if (quiet.Count > 32)
            {
                var old = new List<int>();
                foreach (var pair in quiet) if (pair.Value < Time.time) old.Add(pair.Key);
                foreach (int id in old) quiet.Remove(id);
            }
        }

        private void Spawn(Transform target, float damage, bool big, bool kill)
        {
            if (!Ready()) return;
            // Nothing to see (a logic object holding a counter): no number.
            if (!TryBounds(target, out var bounds)) return;

            // Its upper right as the player sees it: above its top, a little past half its width to the right.
            var right = eyes.transform.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            float width = Mathf.Min(Mathf.Max(bounds.extents.x, bounds.extents.z), 1.5f);
            var at = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + right * (width * 0.6f) + Vector3.up * 0.05f;
            // Several quick hits on one thing stack up instead of covering each other.
            int stacked = 0;
            foreach (var p in popups)
            {
                if (p.text.gameObject.activeSelf && p.target == target && Time.time - p.bornAt < 0.35f) stacked++;
            }
            at += Vector3.up * (0.09f * stacked) + right * (0.04f * (stacked % 2));

            var popup = Take();
            popup.target = target;
            popup.offset = at - target.position;
            popup.basePosition = at;
            popup.bornAt = Time.time;
            popup.scale = kill ? killScale : big ? bigScale : 1f;
            popup.color = big || kill ? bigColor : normalColor;
            popup.text.text = Mathf.Max(1, Mathf.RoundToInt(damage)).ToString();
            popup.text.fontStyle = big || kill ? FontStyles.Bold : FontStyles.Normal;
            popup.text.color = popup.color;
            popup.text.gameObject.SetActive(true);
            Place(popup, 0f);
        }

        private bool Ready()
        {
            if (eyes == null) eyes = Camera.main;
            if (eyes == null) return false;
            if (material != null) return true;
            if (font == null) font = TMP_Settings.defaultFontAsset;
            if (font == null) return false;
            material = new Material(font.material) { name = "Damage Number" };
            var look = Resources.Load<Material>(LookMaterial);
            var overlay = look != null ? look.shader : Shader.Find(OverlayShader);
            if (overlay != null) material.shader = overlay;
            // A dark edge so the red reads against any background.
            if (material.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                material.EnableKeyword("OUTLINE_ON");
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.08f, 0f, 0f, 1f));
            }
            return true;
        }

        private Popup Take()
        {
            foreach (var p in popups)
            {
                if (!p.text.gameObject.activeSelf) return p;
            }
            if (popups.Count >= maxNumbers)
            {
                var oldest = popups[0];
                foreach (var p in popups) if (p.bornAt < oldest.bornAt) oldest = p;
                return oldest;
            }
            var go = new GameObject("Damage Number");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            text.font = font;
            text.fontSharedMaterial = material;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSize = 10f;
            text.rectTransform.sizeDelta = new Vector2(4f, 2f);
            var popup = new Popup { text = text };
            popups.Add(popup);
            return popup;
        }

        // Switched off, or the game stopped, while some show: gone at once instead of hanging there frozen.
        private void HideAll()
        {
            foreach (var p in popups) p.text.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            // The game is stopped (pause menu, Level Up notice): no numbers hanging frozen over the menu.
            if (Time.timeScale == 0f)
            {
                HideAll();
                return;
            }
            if (eyes == null) eyes = Camera.main;
            foreach (var p in popups)
            {
                if (!p.text.gameObject.activeSelf) continue;
                float age = Time.time - p.bornAt;
                if (age >= lifetime || eyes == null)
                {
                    p.text.gameObject.SetActive(false);
                    continue;
                }
                Place(p, age);
            }
        }

        // Follows what was hit (or stays where it broke), floats up, pops in, fades out, and faces the player at a fixed
        // size on screen.
        private void Place(Popup p, float age)
        {
            float t = age / lifetime;
            if (p.target != null) p.basePosition = p.target.position + p.offset;
            p.text.transform.position = p.basePosition + Vector3.up * (rise * (1f - (1f - t) * (1f - t)));

            var toEye = p.text.transform.position - eyes.transform.position;
            float distance = Mathf.Max(0.3f, toEye.magnitude);
            p.text.transform.rotation = Quaternion.LookRotation(toEye, eyes.transform.up);
            float pop = age < 0.08f ? Mathf.Lerp(0.6f, 1.25f, age / 0.08f) : age < 0.18f ? Mathf.Lerp(1.25f, 1f, (age - 0.08f) / 0.1f) : 1f;
            // A TextMeshPro at font size 10 is about 1 unit tall at scale 1: scale it to `size` degrees at this distance.
            float height = 2f * distance * Mathf.Tan(size * 0.5f * Mathf.Deg2Rad) * p.scale * pop;
            p.text.transform.localScale = Vector3.one * height;
            var c = p.color;
            c.a = t < 0.6f ? 1f : Mathf.Clamp01(1f - (t - 0.6f) / 0.4f);
            p.text.color = c;
        }

        private bool TryBounds(Transform target, out Bounds bounds)
        {
            target.GetComponentsInChildren(renderers);
            bool any = false;
            bounds = default;
            foreach (var r in renderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            renderers.Clear();
            return any;
        }
    }
}
