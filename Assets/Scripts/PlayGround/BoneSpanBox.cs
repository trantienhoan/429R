using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// Keeps a wall's Box Collider stretched from this bone to the bone at the wall's other end (a child bone), so the
    /// box always matches the wall, however long the model makes it and while the walls grow (COME_2, COME3) or sink
    /// (GO). Only the box's length along the wall changes; its height and thickness stay as set. Goes on the PlayGround's
    /// wall bones (Bone_1 ... Bone_5); Tools > 429 Game > PlayGround > Fit PlayGround Wall Colliders sets it up.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BoneSpanBox : MonoBehaviour
    {
        [Tooltip("The wall's box on this bone.")]
        [SerializeField] private BoxCollider box;
        [Tooltip("The bone at the wall's other end (a child of this one).")]
        [SerializeField] private Transform otherEnd;
        [Tooltip("Where the box starts, from this bone along the wall (negative: past it), in the bone's units.")]
        [SerializeField] private float startPad;
        [Tooltip("Where the box stops, short of the other end (negative: past it), in the bone's units.")]
        [SerializeField] private float endPad;

        private void LateUpdate()
        {
            Fit();
        }

        /// <summary>Stretches the box between the two bones now.</summary>
        public void Fit()
        {
            if (box == null || otherEnd == null) return;
            var span = transform.InverseTransformPoint(otherEnd.position);
            int axis = 0;
            if (Mathf.Abs(span.y) > Mathf.Abs(span[axis])) axis = 1;
            if (Mathf.Abs(span.z) > Mathf.Abs(span[axis])) axis = 2;
            float length = Mathf.Abs(span[axis]);
            float sign = Mathf.Sign(span[axis]);
            float size = Mathf.Max(0.01f, length - startPad - endPad);
            float middle = sign * (startPad + size * 0.5f);

            var center = box.center;
            var extent = box.size;
            if (Mathf.Abs(center[axis] - middle) < 0.001f && Mathf.Abs(extent[axis] - size) < 0.001f) return;
            center[axis] = middle;
            extent[axis] = size;
            box.center = center;
            box.size = extent;
        }

        /// <summary>Sets it up (the setup menu).</summary>
        public void Set(BoxCollider wallBox, Transform end, float fromStart, float fromEnd)
        {
            box = wallBox;
            otherEnd = end;
            startPad = fromStart;
            endPad = fromEnd;
            Fit();
        }
    }
}
