using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// A pumpkin bursting nearby shakes the player's view a little: harder the closer they are (View Shake). Goes on a
    /// pumpkin's break (its pieces, e.g. pumpkin_2_Drop), so it shakes the moment the pieces appear; a bigger break
    /// reaches farther. Tools > 429 Game > Effects > Set Up Screen Shakes adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class BurstShake : MonoBehaviour
    {
        [Tooltip("Within this many metres (at scale 1) the view shakes medium...")]
        [Min(0f)]
        [SerializeField] private float near = 2f;
        [Tooltip("...and within this many, a little.")]
        [Min(0f)]
        [SerializeField] private float far = 6f;

        private void Start()
        {
            float size = Mathf.Max(0.5f, transform.lossyScale.x);
            ViewShake.Near(transform.position, near * size, far * size);
        }
    }
}
