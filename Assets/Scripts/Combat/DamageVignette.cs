using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Combat
{
    /// <summary>
    /// A burst of red lines at the edge of the player's view on the side a hit came from, whenever their health drops
    /// (bites, webs, bosses). Bigger hits give a stronger burst (longer, thicker, more solid lines: Full Burst Damage,
    /// Opacity to Big Hit Opacity), and each hit gets a new pattern of lines, which then pull back out to the edge,
    /// thin and fade; while they fade they keep pointing at the attacker as the player turns.
    /// Where a hit came from: C# attackers report it with HitFrom (spiders, cockroaches, web balls, ghost balloons,
    /// falling blocks, overheating weapons); for the PlayMaker enemies it's the nearest object tagged Enemy. With
    /// neither, the whole edge flashes. Goes on the XR Origin next to the PlayerHealth FSM; it draws on a sphere around
    /// the headset camera, so it works in both eyes. Tools > 429 Game > Combat > Add Damage Vignette sets it up.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageVignette : MonoBehaviour
    {
        [Tooltip("Material with the 429/Damage Vignette shader (the setup menu makes it). Its colour, number of lines " +
                 "and how far they reach in are set there.")]
        [SerializeField] private Material material;
        [Tooltip("The FSM on this object that holds the player's health, and its float variable.")]
        [SerializeField] private string healthFsm = "PlayerHealth";
        [SerializeField] private string healthVariable = "PlayerHealth";
        [Tooltip("How see-through the lines are for the smallest hits (1 = as the material sets them).")]
        [Range(0f, 1f)]
        [SerializeField] private float opacity = 0.5f;
        [Tooltip("How see-through they are for a Full Burst Damage hit or bigger; in between, in proportion to the damage. " +
                 "So the bigger the hit, the more solid the lines.")]
        [Range(0f, 1f)]
        [SerializeField] private float bigHitOpacity = 1f;
        [Tooltip("A hit this big or bigger gives the full burst (the longest, thickest, most solid lines); smaller ones a " +
                 "weaker one. Bites do 6-9, heavy hits 40-50, the pumpkin boss 70-140.")]
        [Min(1f)]
        [SerializeField] private float fullBurstDamage = 60f;
        [Tooltip("How strong the burst is for the smallest hits.")]
        [Range(0f, 1f)]
        [SerializeField] private float smallestBurst = 0.55f;
        [Tooltip("Seconds the burst stays at full strength, then how long it takes to fade away.")]
        [Min(0f)]
        [SerializeField] private float holdSeconds = 0.08f;
        [Min(0.05f)]
        [SerializeField] private float fadeSeconds = 0.6f;

        [Header("Direction")]
        [Tooltip("How wide the lit part of the edge is, in degrees either side of the hit's direction.")]
        [Range(10f, 180f)]
        [SerializeField] private float arcDegrees = 60f;
        [Tooltip("Hits nobody reported (PlayMaker enemies) come from the nearest object with one of these tags...")]
        [SerializeField] private string[] enemyTags = { "Enemy", "Monster" };
        [Tooltip("...within this many metres of the head. Further than that, or none: the whole edge flashes.")]
        [Min(0.5f)]
        [SerializeField] private float nearestEnemyRange = 6f;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int HitDirId = Shader.PropertyToID("_HitDir");
        private static readonly int ArcId = Shader.PropertyToID("_Arc");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // How long a reported source stays good for the health drop that follows it (the FSM lowers health a frame or
        // so after the attacker sends its damage event).
        private const float ReportedSeconds = 0.5f;
        private static Vector3 reportedPoint;
        private static Transform reportedTransform;
        private static float reportedAt = -100f;

        private FsmFloat health;
        private float lastHealth;
        private MeshRenderer view;
        private Transform head;
        private Material instance;
        private float peak;
        private float baseAlpha;
        private float burstOpacity;
        private float hitAt = -100f;
        private bool hasSource;
        private Vector3 sourcePoint;
        private Transform sourceTransform;

        /// <summary>An attacker about to hurt the player tells where from, so the red lines show on that side.</summary>
        public static void HitFrom(Vector3 worldPoint)
        {
            reportedPoint = worldPoint;
            reportedTransform = null;
            reportedAt = Time.unscaledTime;
        }

        /// <summary>As HitFrom(point), and the lines keep pointing at the attacker while it moves.</summary>
        public static void HitFrom(Transform attacker)
        {
            if (attacker == null) return;
            reportedPoint = attacker.position;
            reportedTransform = attacker;
            reportedAt = Time.unscaledTime;
        }

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == healthFsm) health = fsm.FsmVariables.FindFsmFloat(healthVariable);
            }
            var origin = GetComponent<XROrigin>();
            var cam = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            if (health == null || cam == null || material == null)
            {
                Debug.LogWarning($"[Damage Vignette] Needs a '{healthFsm}' FSM with a '{healthVariable}' float on this object, " +
                                 "a camera and a material. Run Tools > 429 Game > Combat > Add Damage Vignette.", this);
                enabled = false;
                return;
            }
            lastHealth = health.Value;
            head = cam.transform;

            // A sphere around the head, a quarter of a metre out: well past the camera's near clip.
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
            var sphere = new GameObject("Damage Vignette");
            sphere.transform.SetParent(cam.transform, false);
            sphere.transform.localScale = Vector3.one * 0.5f;
            sphere.AddComponent<MeshFilter>().sharedMesh = mesh;
            view = sphere.AddComponent<MeshRenderer>();
            instance = new Material(material);
            // Each hit sets how solid its lines are (Opacity to Big Hit Opacity), from the material's own.
            baseAlpha = instance.GetColor(ColorId).a;
            view.sharedMaterial = instance;
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = LightProbeUsage.Off;
            view.reflectionProbeUsage = ReflectionProbeUsage.Off;
            view.enabled = false;
        }

        private void OnDestroy()
        {
            if (view != null) Destroy(view.gameObject);
            if (instance != null) Destroy(instance);
        }

        private void Update()
        {
            float now = health.Value;
            if (now < lastHealth - 0.01f) Hit(lastHealth - now);
            lastHealth = now;

            float intensity = CurrentIntensity();
            bool show = intensity > 0.002f;
            if (view.enabled != show) view.enabled = show;
            if (!show) return;
            instance.SetFloat(IntensityId, intensity);
            instance.SetFloat(ArcId, arcDegrees);
            instance.SetVector(HitDirId, HitDirection());
        }

        private void Hit(float damage)
        {
            float size = Mathf.Clamp01(damage / fullBurstDamage);
            float strength = Mathf.Lerp(smallestBurst, 1f, size);
            float solid = Mathf.Lerp(opacity, bigHitOpacity, size);
            // A second hit during a burst never makes it weaker.
            bool bursting = CurrentIntensity() > 0.002f;
            peak = Mathf.Max(strength, CurrentIntensity());
            burstOpacity = bursting ? Mathf.Max(solid, burstOpacity) : solid;
            var colour = instance.GetColor(ColorId);
            colour.a = baseAlpha * burstOpacity;
            instance.SetColor(ColorId, colour);
            hitAt = Time.unscaledTime;
            instance.SetFloat(SeedId, Random.Range(0f, 100f));
            FindSource();
        }

        private void FindSource()
        {
            if (Time.unscaledTime - reportedAt <= ReportedSeconds)
            {
                hasSource = true;
                sourcePoint = reportedPoint;
                sourceTransform = reportedTransform;
                reportedAt = -100f;
                return;
            }
            // Nobody reported it: a PlayMaker enemy, most likely the nearest one.
            hasSource = false;
            sourceTransform = null;
            float best = nearestEnemyRange * nearestEnemyRange;
            foreach (var tag in enemyTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                GameObject[] enemies;
                try { enemies = GameObject.FindGameObjectsWithTag(tag); }
                catch (UnityException) { continue; }   // the tag doesn't exist
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.activeInHierarchy) continue;
                    float d = (enemy.transform.position - head.position).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    hasSource = true;
                    sourceTransform = enemy.transform;
                    sourcePoint = enemy.transform.position;
                }
            }
        }

        // The hit's direction in the camera's space (w = 1), so the shader lights that side; w = 0 lights all of it.
        private Vector4 HitDirection()
        {
            if (!hasSource) return Vector4.zero;
            if (sourceTransform != null) sourcePoint = sourceTransform.position;
            var toward = sourcePoint - head.position;
            if (toward.sqrMagnitude < 0.0001f) return Vector4.zero;
            var local = head.InverseTransformDirection(toward.normalized);
            return new Vector4(local.x, local.y, local.z, 1f);
        }

        private float CurrentIntensity()
        {
            float t = Time.unscaledTime - hitAt;
            if (t <= holdSeconds) return peak;
            float fade = Mathf.Clamp01((t - holdSeconds) / fadeSeconds);
            return peak * (1f - fade * fade * (3f - 2f * fade));
        }
    }
}
