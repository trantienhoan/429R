using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace Game.Locomotion
{
    /// <summary>
    /// Switches the comfort vignette off while it is fully open (the player isn't moving or turning). XRI keeps drawing
    /// it even then: a see-through shell over the whole view, which costs GPU time on Quest for nothing.
    /// Goes next to the Tunneling Vignette Controller; Tools > 429 Game > Locomotion > Add Comfort Vignette adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TunnelingVignetteController), typeof(MeshRenderer))]
    public class HideVignetteWhenOpen : MonoBehaviour
    {
        private static readonly int ApertureSize = Shader.PropertyToID("_ApertureSize");
        // Fully open is 1; just below it the vignette is already invisible.
        private const float OpenAperture = 0.995f;

        private MeshRenderer shell;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            shell = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
        }

        // After the controller's Update, which writes this frame's opening size to the renderer.
        private void LateUpdate()
        {
            shell.GetPropertyBlock(block);
            shell.enabled = block.GetFloat(ApertureSize) < OpenAperture;
        }
    }
}
