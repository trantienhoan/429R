using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The seed pieces go home. User's story, 2026-11-23: "all the green pieces I collected start flying out and join in
    /// together and dive down under the dirt space, not waiting too long, a big tree starts growing up"; 2026-11-26: "when
    /// the seal on the rooftop is broken, all the collected pieces fly out, circling and join in as whole seed, dig to the
    /// ground and grow up as the Pumpkin Tree". Played by the Seeds Into Soil action on the rooftop's Pumpkin_Tree_Pos as
    /// the lid breaks, during its wait before the tree:
    /// 1. one piece for each seed taken this run (the action's count: its GAMESTAGES gear bools, the start + 4 rooms = 5;
    ///    at most Most Pieces Shown) pops out in front of the player, a chime each;
    /// 2. they fly up to above the dirt (a whoosh) into a ring, and circle, the ring tightening and turning faster;
    /// 3. they join into the whole Seed (Whole Seed Look, The_Seed) with a green flash (the merge sound), which hangs a
    ///    moment, then
    /// 4. drills into the dirt, spinning, with puffs of earth; a last puff (the plunge and the tree's sound) and the tree
    ///    grows (the action's state waits, then makes it).
    /// The pieces in the inventory (Piece Item, Yellow_Seed) are used up (Spend Pieces): the count goes back to 0, so none
    /// carry over to the next run. Everything only shows: no FSM, no physics.
    /// </summary>
    public class StorySeedsFlight : MonoBehaviour
    {
        // Parts of the whole time: popping out, flying, circling, joining, hanging; the rest is the dig.
        private const float PopEnd = 0.1f, FlyEnd = 0.4f, CircleEnd = 0.7f, JoinEnd = 0.8f, HangEnd = 0.86f;
        private const float GatherHeight = 1.8f, Arc = 1.0f, RingStart = 1.15f, RingEnd = 0.18f;
        // Bigger than a seed on the floor, so they still read when they are far off.
        private const float FlightSize = 1.8f;
        private const float PuffEvery = 0.12f;

        private Transform[] pieces;
        private Vector3[] from, bend, size;
        private float[] angle;
        private Vector3 soil, gather;
        private float seconds, started, nextPuff;
        private GameObject puff;
        private Transform whole;
        private Vector3 wholeSize;
        private Light glow;
        private StorySeedSettings settings;
        private bool chimed, whooshed, merged, dug;

        /// <summary>How many flights are playing now (for tests).</summary>
        public static int Playing { get; private set; }

        /// <summary>How many pieces the last flight showed and how many it used up (for tests).</summary>
        public static int LastShown { get; private set; }
        public static int LastSpent { get; private set; }

        /// <summary>
        /// Plays the flight to Soil over Seconds: Count pieces (the seeds taken this run); the inventory's pieces are spent.
        /// </summary>
        public static void Play(Vector3 soil, GameObject piece, GameObject puff, int count, float seconds)
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (piece == null || head == null) return;
            var settings = StorySeedSettings.Instance;
            // One piece for each seed taken THIS run (the action's count: its gear bools, the start + 4 rooms = 5). The
            // inventory's count is no good for this (user 2026-11-26: "why we have so many pieces? 1 at start and 4 more
            // is 5"): it carries over between runs and old saves still hold seeds from when they were money.
            int shown = Mathf.Min(count, settings.MostPiecesShown);
            if (shown <= 0) return;
            LastShown = shown;
            LastSpent = 0;
            // The pieces in the inventory leave the player too: used up, they become the Seed (none carry over).
            if (settings.PieceItem != null && settings.SpendPieces)
            {
                var inventory = FindAnyObjectByType<Game.Inventory.PlayerInventory>(FindObjectsInactive.Include);
                int collected = inventory != null ? inventory.GetCount(settings.PieceItem) : 0;
                if (collected > 0 && inventory.TryRemove(settings.PieceItem, collected)) LastSpent = collected;
            }
            new GameObject("Story_Seeds_Into_Soil").AddComponent<StorySeedsFlight>()
                .Begin(head, soil, piece, puff, shown, seconds, settings);
        }

        private void Begin(Transform head, Vector3 soilAt, GameObject piece, GameObject puffPrefab, int count, float time,
            StorySeedSettings cfg)
        {
            Playing++;
            settings = cfg;
            soil = soilAt;
            gather = soil + Vector3.up * GatherHeight;
            seconds = Mathf.Max(1f, time);
            puff = puffPrefab;
            started = Time.time;
            transform.position = gather;
            pieces = new Transform[count];
            from = new Vector3[count];
            bend = new Vector3[count];
            size = new Vector3[count];
            angle = new float[count];

            var ahead = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (ahead.sqrMagnitude < 0.01f) ahead = Vector3.forward;
            ahead.Normalize();
            var side = Vector3.Cross(Vector3.up, ahead);
            for (int i = 0; i < count; i++)
            {
                // Out of the player's chest, in a little fan.
                float across = count == 1 ? 0f : Mathf.Lerp(-0.5f, 0.5f, i / (float)(count - 1));
                var start = head.position + ahead * 0.45f + side * across + Vector3.down * (0.35f + 0.1f * Mathf.Abs(across));
                var go = Instantiate(piece, start, Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), 0f));
                OnlyShows(go, settings.GlowParts);
                StorySeed.TintGlow(go, settings.Glow, settings.GlowParts);
                size[i] = go.transform.localScale * FlightSize;
                go.transform.localScale = Vector3.zero;
                pieces[i] = go.transform;
                from[i] = start;
                // Their places on the ring, spread evenly.
                angle[i] = i * 360f / count;
                // Up and round on the way: above both ends, swung out to its own side.
                bend[i] = Vector3.Lerp(start, RingPoint(i, 0f), 0.5f) + Vector3.up * Arc + side * across * 2f;
            }
            glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = settings.FlightLight;
            glow.range = 4f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;
        }

        // A piece's place on the ring at circling progress c (0 = its start, 1 = the ring at its smallest).
        private Vector3 RingPoint(int i, float c)
        {
            float radius = Mathf.Lerp(RingStart, RingEnd, c * c);
            // Faster and faster: a turn and a half in all.
            float a = (angle[i] + 540f * c * c) * Mathf.Deg2Rad;
            return gather + new Vector3(Mathf.Cos(a) * radius, 0.12f * Mathf.Sin(a * 2f + i), Mathf.Sin(a) * radius);
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
            if (t < JoinEnd) Pieces(t);
            else if (!merged) Merge();
            if (merged) Whole(t);
            if (t >= 1f) End();
        }

        private void Pieces(float t)
        {
            if (!chimed)
            {
                chimed = true;
                Sound(settings.PieceChime, from[0]);
            }
            if (!whooshed && t >= PopEnd)
            {
                whooshed = true;
                Sound(settings.FlightWhoosh, Vector3.Lerp(from[0], gather, 0.3f));
            }
            float grow = Smooth(t / PopEnd);
            float fly = Smooth((t - PopEnd) / (FlyEnd - PopEnd));
            float circle = Mathf.Clamp01((t - FlyEnd) / (CircleEnd - FlyEnd));
            float join = Smooth((t - CircleEnd) / (JoinEnd - CircleEnd));
            glow.intensity = Mathf.Lerp(0.6f, 2.2f, Mathf.Clamp01(t / CircleEnd)) + join * 2f;
            glow.transform.position = t < FlyEnd ? Vector3.Lerp(from[0], gather, fly) : gather;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                Vector3 at;
                float scale = 1f;
                if (t < FlyEnd) at = Curve(from[i], bend[i], RingPoint(i, 0f), fly);
                else if (t < CircleEnd) at = RingPoint(i, circle);
                else
                {
                    // Into the middle, shrinking into the Seed.
                    at = Vector3.Lerp(RingPoint(i, 1f), gather, join);
                    scale = Mathf.Lerp(1f, 0.35f, join);
                }
                pieces[i].position = at;
                pieces[i].localScale = size[i] * (grow * scale);
                pieces[i].Rotate(140f * Time.deltaTime, 360f * Time.deltaTime, 0f, Space.Self);
            }
        }

        // The pieces are one: the whole Seed shows in their middle, with a flash.
        private void Merge()
        {
            merged = true;
            foreach (var piece in pieces)
                if (piece != null) Destroy(piece.gameObject);
            pieces = new Transform[0];
            var look = settings.WholeSeedLook;
            if (look != null)
            {
                var go = Instantiate(look, gather, Quaternion.identity);
                OnlyShows(go, settings.GlowParts);
                StorySeed.TintGlow(go, settings.Glow, settings.GlowParts);
                wholeSize = go.transform.localScale * settings.WholeSeedSize;
                go.transform.localScale = Vector3.zero;
                whole = go.transform;
            }
            if (puff != null) Destroy(Instantiate(puff, gather, Quaternion.identity), 6f);
            Sound(settings.MergeSound, gather);
        }

        private void Whole(float t)
        {
            float pop = Smooth((t - JoinEnd) / (HangEnd - JoinEnd));
            float dig = Mathf.Clamp01((t - HangEnd) / (1f - HangEnd));
            glow.intensity = dig <= 0f ? Mathf.Lerp(4.2f, 2.5f, pop) : Mathf.Lerp(2.5f, 0.5f, dig);
            if (whole == null) return;
            // It springs to its size, hangs a beat, then drills down, faster and faster, spinning hard.
            whole.localScale = wholeSize * (dig <= 0f ? BackOut(pop) : Mathf.Lerp(1f, 0.55f, dig));
            var at = Vector3.Lerp(gather, soil + Vector3.down * 0.45f, dig * dig);
            if (dig > 0f) at += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (0.025f * (1f - dig));
            whole.position = at;
            glow.transform.position = at + Vector3.up * 0.3f;
            whole.Rotate(0f, Mathf.Lerp(200f, 1600f, dig) * Time.deltaTime, 0f, Space.World);
            // Earth thrown up where it goes in (brown dirt; the action's puff if none is set).
            var earth = settings.DigPuff != null ? settings.DigPuff : puff;
            if (dig > 0.35f && earth != null && Time.time >= nextPuff)
            {
                nextPuff = Time.time + PuffEvery;
                var made = Instantiate(earth, soil + new Vector3(Random.Range(-0.15f, 0.15f), 0.05f, Random.Range(-0.15f, 0.15f)), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                made.transform.localScale *= settings.DigPuff != null ? settings.DigPuffSize : 0.55f;
                Destroy(made, 4f);
            }
        }

        private void End()
        {
            if (dug) return;
            dug = true;
            var earth = settings.DigPuff != null ? settings.DigPuff : puff;
            if (earth != null)
            {
                var made = Instantiate(earth, soil, Quaternion.identity);
                made.transform.localScale *= settings.DigPuff != null ? settings.LastPuffSize : 1.4f;
                Destroy(made, 6f);
            }
            Sound(settings.PlungeSound, soil);
            Sound(settings.TreeGrowSound, soil + Vector3.up);
            if (whole != null) Destroy(whole.gameObject);
            Destroy(gameObject);
        }

        private void Sound(AudioClip clip, Vector3 at)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, settings.FlightVolume);
        }

        private void OnDestroy()
        {
            Playing = Mathf.Max(0, Playing - 1);
            if (pieces != null)
                foreach (var piece in pieces)
                    if (piece != null) Destroy(piece.gameObject);
            if (whole != null) Destroy(whole.gameObject);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float BackOut(float t)
        {
            t = Mathf.Clamp01(t);
            const float s = 1.6f;
            float u = t - 1f;
            return u * u * ((s + 1f) * u + s) + 1f;
        }

        private static Vector3 Curve(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
