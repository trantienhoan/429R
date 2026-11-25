using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The seed pieces go home (user's story, 2026-11-23: "all the green pieces I collected start flying out and join in
    /// together and dive down under the dirt space, not waiting too long, a big tree starts growing up"). Played by the
    /// Seeds Into Soil action on the rooftop's Pumpkin_Tree_Pos, during its wait before the tree: one green glowing piece
    /// per seed taken this run pops out in front of the player, they fly up and over to above the dirt, join into one
    /// and it dives in, with a puff. The pieces only show: no FSM, no physics, nothing to take.
    /// </summary>
    public class StorySeedsFlight : MonoBehaviour
    {
        // Parts of the whole time: popping out, flying, joining; the rest is the dive.
        private const float PopEnd = 0.12f, FlyEnd = 0.7f, JoinEnd = 0.84f;
        private const float GatherHeight = 1.6f, Arc = 1.2f;
        // Bigger than a seed on the floor, so they still read when they are far off.
        private const float FlightSize = 1.8f;

        private Transform[] pieces;
        private Vector3[] from, bend, size;
        private Vector3 soil, gather;
        private float seconds, started;
        private GameObject puff;
        private bool joined;

        /// <summary>How many flights are playing now (for tests).</summary>
        public static int Playing { get; private set; }

        /// <summary>Plays the flight: Count pieces from in front of the player to Soil, over Seconds.</summary>
        public static void Play(Vector3 soil, GameObject piece, GameObject puff, int count, float seconds)
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (piece == null || head == null || count <= 0) return;
            new GameObject("Story_Seeds_Into_Soil").AddComponent<StorySeedsFlight>()
                .Begin(head, soil, piece, puff, count, seconds);
        }

        private void Begin(Transform head, Vector3 soilAt, GameObject piece, GameObject puffPrefab, int count, float time)
        {
            Playing++;
            soil = soilAt;
            gather = soil + Vector3.up * GatherHeight;
            seconds = Mathf.Max(0.5f, time);
            puff = puffPrefab;
            started = Time.time;
            pieces = new Transform[count];
            from = new Vector3[count];
            bend = new Vector3[count];
            size = new Vector3[count];

            var ahead = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (ahead.sqrMagnitude < 0.01f) ahead = Vector3.forward;
            ahead.Normalize();
            var side = Vector3.Cross(Vector3.up, ahead);
            var settings = StorySeedSettings.Instance;
            for (int i = 0; i < count; i++)
            {
                float across = count == 1 ? 0f : Mathf.Lerp(-0.45f, 0.45f, i / (float)(count - 1));
                var start = head.position + ahead * 0.6f + side * across + Vector3.down * 0.3f;
                var go = Instantiate(piece, start, Quaternion.identity);
                OnlyShows(go, settings.GlowParts);
                StorySeed.TintGlow(go, settings.Glow, settings.GlowParts);
                size[i] = go.transform.localScale * FlightSize;
                go.transform.localScale = Vector3.zero;
                pieces[i] = go.transform;
                from[i] = start;
                // Up and round on the way: above both ends, swung out to its own side.
                bend[i] = Vector3.Lerp(start, gather, 0.5f) + Vector3.up * Arc + side * across * 2f;
            }
        }

        // A seed prefab that only shows its glow: its FSM would give gear and could be taken, so it never starts; its
        // spawn smoke and shadow stay off.
        internal static void OnlyShows(GameObject go, string[] glowParts)
        {
            foreach (var particles in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (System.Array.IndexOf(glowParts, particles.name) >= 0) continue;
                var emission = particles.emission;
                emission.enabled = false;
                particles.Clear(false);
                if (particles.TryGetComponent(out ParticleSystemRenderer look)) look.enabled = false;
            }
            foreach (var fsm in go.GetComponentsInChildren<PlayMakerFSM>(true)) fsm.enabled = false;
            foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (var hit in go.GetComponentsInChildren<Collider>(true)) hit.enabled = false;
            foreach (var seed in go.GetComponentsInChildren<StorySeed>(true)) Destroy(seed);
        }

        private void Update()
        {
            float t = (Time.time - started) / seconds;
            if (t < JoinEnd)
            {
                bool flying = t < FlyEnd;
                float grow = Smooth(t / PopEnd);
                float along = Smooth((t - PopEnd) / (FlyEnd - PopEnd));
                float join = Smooth((t - FlyEnd) / (JoinEnd - FlyEnd));
                for (int i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] == null) continue;
                    if (flying)
                    {
                        pieces[i].position = Curve(from[i], bend[i], gather, along);
                        pieces[i].localScale = size[i] * grow;
                    }
                    else
                    {
                        // Joining: the first grows into the whole seed, the others shrink into it.
                        pieces[i].position = gather;
                        pieces[i].localScale = size[i] * (i == 0 ? Mathf.Lerp(1f, 1.6f, join) : Mathf.Lerp(1f, 0.4f, join));
                    }
                    pieces[i].Rotate(0f, 360f * Time.deltaTime, 0f, Space.World);
                }
                return;
            }

            if (!joined)
            {
                joined = true;
                for (int i = 1; i < pieces.Length; i++)
                    if (pieces[i] != null) Destroy(pieces[i].gameObject);
            }
            var whole = pieces[0];
            float dive = Mathf.Clamp01((t - JoinEnd) / (1f - JoinEnd));
            if (whole != null)
            {
                // Faster and faster into the dirt.
                whole.position = Vector3.Lerp(gather, soil + Vector3.down * 0.3f, dive * dive);
                whole.localScale = size[0] * Mathf.Lerp(1.6f, 0.4f, dive);
                whole.Rotate(0f, 720f * Time.deltaTime, 0f, Space.World);
            }
            if (dive < 1f) return;
            if (puff != null) Destroy(Instantiate(puff, soil, Quaternion.identity), 6f);
            if (whole != null) Destroy(whole.gameObject);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Playing = Mathf.Max(0, Playing - 1);
            if (pieces == null) return;
            foreach (var piece in pieces)
                if (piece != null) Destroy(piece.gameObject);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static Vector3 Curve(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
