using UnityEngine;

namespace Game
{
    /// <summary>
    /// Keeps this object's own scale relative to whatever it's put under, so it grows and shrinks with its parent.
    /// For pieces that PlayMaker's Create Object spawns inside something scaled, e.g. the cracked pumpkin inside a
    /// half-size pumpkin: Create Object keeps the new object's world size when it sets the parent, so without this
    /// the crack shows up at full size. The scale set on the prefab becomes its size relative to the parent.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScaleWithParent : MonoBehaviour
    {
        private Vector3 ownScale;

        private void Awake()
        {
            ownScale = transform.localScale;
        }

        private void OnTransformParentChanged()
        {
            if (transform.parent != null) transform.localScale = ownScale;
        }
    }
}
