using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The Fairy's teleport flash light: bright at once, out after a short time, then removed (added by Story Fairy Acts
    /// to its own light object, so it fades even if she is destroyed).
    /// </summary>
    public class StoryFlashFade : MonoBehaviour
    {
        private Light lamp;
        private float from;
        private float start;
        private float seconds = 0.7f;

        public void Begin(float fadeSeconds)
        {
            lamp = GetComponent<Light>();
            from = lamp != null ? lamp.intensity : 0f;
            seconds = Mathf.Max(0.01f, fadeSeconds);
            start = Time.time;
        }

        private void Update()
        {
            float t = (Time.time - start) / seconds;
            if (t >= 1f || lamp == null)
            {
                Destroy(gameObject);
                return;
            }
            lamp.intensity = from * (1f - t) * (1f - t);
        }
    }
}
