using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The hole a room's mini boss digs as it escapes (Story Boss Escape): it opens as the boss digs, stays while the
    /// room's fight goes on, and closes a few seconds after the boss has climbed back out of it. Only a look: no
    /// collider. Its mesh has radius 1, so its scale is its radius in metres.
    /// </summary>
    public class StoryBurrowHole : MonoBehaviour
    {
        private float radius = 1f, from, to, started, seconds;
        private bool closing;

        /// <summary>Opens to this radius (metres) over these seconds.</summary>
        public void Open(float size, float time)
        {
            radius = Mathf.Max(0.05f, size);
            Go(0f, 1f, time);
        }

        /// <summary>Closes over these seconds, after a wait, then it's gone.</summary>
        public void Close(float after, float time)
        {
            closing = true;
            from = Current();
            to = 0f;
            started = Time.time + Mathf.Max(0f, after);
            seconds = Mathf.Max(0.05f, time);
            enabled = true;
        }

        private void Go(float a, float b, float time)
        {
            from = a;
            to = b;
            started = Time.time;
            seconds = Mathf.Max(0.05f, time);
            enabled = true;
            Apply(a);
        }

        private float Current() => radius > 0f ? transform.localScale.x / radius : 0f;

        private void Apply(float share)
        {
            transform.localScale = new Vector3(radius * share, radius * Mathf.Max(share, 0.001f), radius * share);
        }

        private void Update()
        {
            if (Time.time < started) return;
            float t = Mathf.Clamp01((Time.time - started) / seconds);
            float eased = t * t * (3f - 2f * t);
            Apply(Mathf.Lerp(from, to, eased));
            if (t < 1f) return;
            enabled = false;
            if (closing) Destroy(gameObject);
        }
    }
}
