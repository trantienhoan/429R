using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// The final boss's arena border (user 2026-11-26: "some light or can just be some particle to light up the border
    /// of that area, when Player move out of that area, they will just fall by dropping at the Fall Edge"). When the
    /// boss comes the rooftop shrinks away and the fight is on Boss_Area's floor, which is invisible: only the 4 corner
    /// bonfires showed where it ends. This keeps its particle layers (embers, a faint glowing curtain) going, spread
    /// evenly along the floor's 4 edges (a box-edge shape would pile a third of them on the corners); a line renderer
    /// draws the glowing line on the ground and Fall Edge boxes start right at it.
    /// Built and sized by Tools > 429 Game > Enemies > Set Up Boss Arena Border, on the Boss_Area prefab.
    /// </summary>
    public class BossArenaBorder : MonoBehaviour
    {
        [System.Serializable]
        public class Layer
        {
            [Tooltip("A particle system with its own emission off (world space); this emits it along the border.")]
            public ParticleSystem system;
            [Tooltip("New particles a second, over the whole border.")]
            public float perSecond = 40f;
            [Tooltip("How high above the floor they start (metres).")]
            public float height = 0.05f;
        }

        [Tooltip("Half the floor's size along x and z (metres), measured from this object.")]
        public Vector2 halfSize = new Vector2(7.5f, 7.6f);
        [Tooltip("The floor's top, from this object (metres).")]
        public float floorY;
        public Layer[] layers = new Layer[0];

        private float[] owed;

        private void OnEnable()
        {
            owed = new float[layers.Length];
        }

        private void Update()
        {
            if (owed == null || owed.Length != layers.Length) owed = new float[layers.Length];
            float dt = Time.deltaTime;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null || layer.system == null) continue;
                owed[i] += layer.perSecond * dt;
                int count = Mathf.Min(64, Mathf.FloorToInt(owed[i]));
                owed[i] -= count;
                var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (int k = 0; k < count; k++)
                {
                    emit.position = transform.TransformPoint(PointOnBorder(layer.height));
                    layer.system.Emit(emit, 1);
                }
            }
        }

        // A random point on the rectangle's outline, evenly spread by length.
        private Vector3 PointOnBorder(float height)
        {
            float x = halfSize.x, z = halfSize.y;
            float t = Random.value * 4f * (x + z);
            float y = floorY + height;
            if (t < 2f * x) return new Vector3(-x + t, y, -z);
            t -= 2f * x;
            if (t < 2f * z) return new Vector3(x, y, -z + t);
            t -= 2f * z;
            if (t < 2f * x) return new Vector3(x - t, y, z);
            t -= 2f * x;
            return new Vector3(-x, y, z - t);
        }
    }
}
