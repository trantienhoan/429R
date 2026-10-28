using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// A light running round a "429/Effects/Running Glow Ring" ring: moves the material's bright head round (one lap
    /// every Lap Seconds) and carries a small point light along with it, so what's next to the ring lights up as it
    /// passes. Only added by Tools > 429 Game > VFX > Add Running Light Round Lid when its Running switch is on; the lid
    /// itself now has the steady glow only (user, 2026-10-28). The ring's local space is flat and centred: x/z round,
    /// y up.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class RunningGlow : MonoBehaviour
    {
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");

        [Tooltip("Seconds for the light to go once round.")]
        [SerializeField] private float lapSeconds = 4f;
        [Tooltip("The light carried round with the head (optional).")]
        [SerializeField] private Light runner;
        [Tooltip("How far out from the lid's centre the light runs (metres, in the ring's own space).")]
        [SerializeField] private float lightRadius = 2.6f;
        [Tooltip("How high above the floor the light runs.")]
        [SerializeField] private float lightHeight = 0.3f;

        private Renderer ring;
        private MaterialPropertyBlock block;
        private float phase;

        private void Awake()
        {
            ring = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            phase = Random.value;
        }

        private void Update()
        {
            phase = Mathf.Repeat(phase + Time.deltaTime / Mathf.Max(0.1f, lapSeconds), 1f);
            ring.GetPropertyBlock(block);
            block.SetFloat(PhaseId, phase);
            ring.SetPropertyBlock(block);
            if (runner == null) return;
            // Same place as the head in the shader: UV x = angle / 2 pi, measured from +x towards +z.
            float angle = phase * Mathf.PI * 2f;
            runner.transform.localPosition = new Vector3(Mathf.Cos(angle) * lightRadius, lightHeight, Mathf.Sin(angle) * lightRadius);
        }
    }
}
