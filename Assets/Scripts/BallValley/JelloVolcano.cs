using UnityEngine;

namespace Balls
{
    /// <summary>
    /// 429R gate 3 (user 2026-11-23: "jello volcano spit out candy occasionally"): a jelly volcano on the lawn. Every
    /// Every seconds (shorter as the gate's song speeds up: Hurry) it wobbles and spits a few candies out of its crater onto
    /// the lawn round it; a hit (a ball crashing into it, a punch) makes it spit one at once. It never breaks; the shrinking
    /// border swallows it like a boulder. Made by the Ball Valley Stage (its Kit Volcanoes), one per volcano.
    /// </summary>
    [DisallowMultipleComponent]
    public class JelloVolcano : MonoBehaviour
    {
        [Tooltip("Seconds between spits, least to most (divided by Hurry).")]
        public Vector2 every = new Vector2(7f, 13f);
        [Tooltip("Candies a spit, least to most.")]
        public Vector2Int candies = new Vector2Int(2, 4);
        [Tooltip("A hit makes it spit one candy, at most this often (seconds).")]
        [Min(0.1f)] public float hitSpitEvery = 1.5f;
        [Tooltip("Its crater's height over its foot (m, at size 1).")]
        public float craterHeight = 1.7f;

        /// <summary>How much faster every volcano spits (1 = Every as set): the gate raises it as its song speeds up.</summary>
        public static float Hurry = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Hurry = 1f;

        private BallValleyStage stage;
        private FieldProp prop;
        private float nextSpit, nextHitSpit;

        /// <summary>Its stage and its field prop (its hits come through the prop).</summary>
        public void Init(BallValleyStage valleyStage, FieldProp fieldProp)
        {
            stage = valleyStage;
            prop = fieldProp;
            if (prop != null) prop.onHit += OnHit;
            nextSpit = Time.time + Random.Range(2f, every.y);
        }

        private void OnDestroy()
        {
            if (prop != null) prop.onHit -= OnHit;
        }

        private void Update()
        {
            if (stage == null || prop == null || prop.Broken || prop.Waiting) return;
            if (Time.time < nextSpit) return;
            Spit(Random.Range(candies.x, candies.y + 1));
            nextSpit = Time.time + Random.Range(every.x, every.y) / Mathf.Max(0.5f, Hurry);
        }

        private void OnHit(float damage, Vector3 at)
        {
            if (damage < 2f || Time.time < nextHitSpit) return;
            nextHitSpit = Time.time + hitSpitEvery;
            Spit(1);
        }

        private void Spit(int n)
        {
            float size = transform.localScale.x;
            var top = transform.position + Vector3.up * (craterHeight * size);
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var flat = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                stage.SpawnPellet(top + flat * 0.25f, flat * Random.Range(2f, 4.5f) + Vector3.up * Random.Range(7f, 10f), stage.RandomBerryColor());
            }
            if (prop.jiggle) prop.jiggle.Hit(0.7f, Vector3.up);
            Gfx.Puff(top, new Color(1f, 0.6f, 0.85f, 0.8f), 10, 1.1f * size, 3f);
            Sfx.Play(Sfx.Sound.SlimeEmerge, top, 0.8f);
        }
    }
}
