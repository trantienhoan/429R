using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Combat
{
    /// <summary>
    /// A burst of red lines around the edges of the player's view whenever their health drops, from any hit (bites,
    /// webs, bosses). Bigger hits give a stronger burst, and each hit gets a new pattern of lines, which then pull back
    /// out to the edge and fade. Goes on the XR Origin next to the PlayerHealth FSM; it draws on a sphere around the
    /// headset camera, so it works in both eyes. Tools > 429 Game > Combat > Add Damage Vignette sets it up.
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
        [Tooltip("A hit this big or bigger gives the full burst; smaller ones a weaker one.")]
        [Min(1f)]
        [SerializeField] private float fullBurstDamage = 30f;
        [Tooltip("How strong the burst is for the smallest hits.")]
        [Range(0f, 1f)]
        [SerializeField] private float smallestBurst = 0.55f;
        [Tooltip("Seconds the burst stays at full strength, then how long it takes to fade away.")]
        [Min(0f)]
        [SerializeField] private float holdSeconds = 0.08f;
        [Min(0.05f)]
        [SerializeField] private float fadeSeconds = 0.6f;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        private FsmFloat health;
        private float lastHealth;
        private MeshRenderer view;
        private Material instance;
        private float peak;
        private float hitAt = -100f;

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
            if (show) instance.SetFloat(IntensityId, intensity);
        }

        private void Hit(float damage)
        {
            float strength = Mathf.Lerp(smallestBurst, 1f, Mathf.Clamp01(damage / fullBurstDamage));
            // A second hit during a burst never makes it weaker.
            peak = Mathf.Max(strength, CurrentIntensity());
            hitAt = Time.unscaledTime;
            instance.SetFloat(SeedId, Random.Range(0f, 100f));
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
