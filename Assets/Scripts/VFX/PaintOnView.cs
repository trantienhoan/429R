using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Paint splatted on the player's view: a few blobs in front of the eyes that slide down and fade (a slime that
    /// dive-bombs the face, user 2026-11-05). Splat makes them; each blob runs itself and is gone after Seconds.
    /// </summary>
    public class PaintOnView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Vector3 at;
        private float size;
        private float seconds;
        private float born;
        private Color tint;
        private Renderer part;
        private MaterialPropertyBlock block;

        /// <summary>
        /// Count blobs (or 3 to 5) of 'material' (a soft round blob, alpha blended) in front of the main camera, in
        /// 'colour' (rainbow: each its own colour), sliding down and fading over Seconds.
        /// </summary>
        public static void Splat(Material material, Color colour, bool rainbow = false, int count = -1, float seconds = 1.4f)
        {
            var cam = Camera.main;
            if (cam == null || material == null) return;
            if (count < 0) count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var blob = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(blob.GetComponent<Collider>());
                blob.name = "Paint On View";
                blob.transform.SetParent(cam.transform, false);
                var splat = blob.AddComponent<PaintOnView>();
                splat.at = new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.08f, 0.1f), 0.3f);
                splat.size = Random.Range(0.06f, 0.14f) * (i == 0 ? 1.6f : 1f);
                splat.seconds = Mathf.Max(0.2f, seconds);
                splat.born = Time.time;
                splat.tint = rainbow ? Color.HSVToRGB((i / (float)count + Random.value * 0.1f) % 1f, 0.75f, 1f) : colour;
                splat.part = blob.GetComponent<Renderer>();
                splat.part.sharedMaterial = material;
                splat.part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                splat.part.receiveShadows = false;
                blob.transform.localPosition = splat.at;
                blob.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                blob.transform.localScale = Vector3.zero;
            }
        }

        private void Update()
        {
            float t = Time.time - born;
            if (t >= seconds)
            {
                Destroy(gameObject);
                return;
            }
            float k = t / seconds;
            transform.localPosition = at + Vector3.down * (0.06f * k * k);
            transform.localScale = new Vector3(size, size * (1f + 0.5f * k), size) * Mathf.Min(1f, t / 0.06f);
            block ??= new MaterialPropertyBlock();
            var c = tint;
            c.a = 0.9f * (1f - k * k);
            part.GetPropertyBlock(block);
            block.SetColor(BaseColor, c);
            part.SetPropertyBlock(block);
        }
    }
}
