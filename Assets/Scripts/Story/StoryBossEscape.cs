using System.Collections.Generic;
using Game.Enemies;
using Game.PlayGround;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Story
{
    /// <summary>
    /// A room's mini boss escapes (user, 2026-11-25): when the room's laughing pumpkin breaks, the mini boss pops out of
    /// it with the pumpkin's piece of the Green Seed on its back, hisses at the player, runs off and digs into the
    /// floor, leaving a hole. Then the room goes on as before (walls drop, waves). When the room's EnemyManager calls the
    /// boss at the end, it climbs out of that hole, still carrying the piece.
    /// User 2026-11-26 (Escape = Smoke Bomb, now the default): it pops out, plays its Alert look facing the player, and
    /// is gone in a ninja's smoke bomb; no run, no hole. The real boss comes in a smoke bomb too. No seed piece on its
    /// back (Seed On Boss off): the boss drops the room's piece when it dies, as before.
    /// User 2026-11-27 (Escape = Run And Smoke, now the default): "Make them run away with a black smoke bomb": after its
    /// Alert look it turns and runs off along a route away from the player (Run Smoke Seconds at most), then is gone in a
    /// black smoke bomb (Prefabs/Story/Smoke_Bomb_Black); the real boss comes in one too.
    ///
    /// Played by the Story Boss Escapes action (one GAMESTAGES state per room, between the room's waiting state and its
    /// wall drop). The boss shown here is a look-only copy of the real prefab: no brain, no health, no colliders, so it
    /// can't hurt or be hurt. Settings: Assets/Resources/Story Boss Escape Settings.
    /// </summary>
    public class StoryBossEscape : MonoBehaviour
    {
        private enum Phase { Pop, Hiss, Run, Dig, Smoke, Done }

        // A hole waiting for its boss: the boss prefab's name, and the GAMESTAGES states it belongs to.
        private class Burrow
        {
            public string boss, escape, fight;
            public StoryBurrowHole hole;
            public int agentType;
            public bool cameOut;
        }

        private static readonly List<Burrow> Burrows = new();
        private const float RecentBreak = 12f;
        // Its route keeps at least this far from the player (metres along the floor), and rather this far.
        private const float MinClearance = 0.6f, GoodClearance = 1.2f;
        // The biggest bosses' holes stay this size at most (radius, metres).
        private const float MostHoleRadius = 0.9f;
        // Seconds its sounds go on after the dig, then the seconds they fade over.
        private const float SoundHold = 1.5f, SoundFade = 1.5f;

        /// <summary>How many escapes are playing now (for tests).</summary>
        public static int Playing { get; private set; }

        /// <summary>The last escape's dig spot and hole (for tests).</summary>
        public static Vector3 LastDigAt { get; private set; }

        public bool Done => phase == Phase.Done;

        private StoryBossEscapeSettings settings;
        private Transform body;
        private Animator animator;
        private AudioSource voice;
        private string runLook, alertLook, appearLook;
        private float runLookSpeed, scale, length;
        private Phase phase;
        private float phaseStart, nextDust;
        private Vector3[] path;
        private int corner;
        private Vector3 digAt;
        private Burrow burrow;
        private AudioClip[] hisses;
        private bool counted;
        private float fadeFrom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Burrows.Clear();
            Playing = 0;
            SmokeBombs = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Listen()
        {
            SpiderBrain.Started -= BossStarted;
            SpiderBrain.Started += BossStarted;
        }

        /// <summary>
        /// Plays the escape of this boss prefab from the runner that broke last (or in front of the player). Escape and
        /// Fight are the GAMESTAGES states of the escape and of the room's fight: the boss comes back out of its hole
        /// while either is on. Null when there is nothing to show.
        /// </summary>
        public static StoryBossEscape Play(GameObject bossPrefab, string escapeState, string fightState)
        {
            if (bossPrefab == null || !bossPrefab.TryGetComponent(out SpiderBrain brain)) return null;
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return null;
            var show = new GameObject("Story_Boss_Escape (" + bossPrefab.name + ")").AddComponent<StoryBossEscape>();
            show.transform.SetParent(SpawnedStuff(), true);
            if (!show.Begin(bossPrefab, brain, head, escapeState, fightState))
            {
                Destroy(show.gameObject);
                return null;
            }
            return show;
        }

        /// <summary>Stops a show that is still playing (its state was left): the copy goes at once.</summary>
        public void Stop()
        {
            if (phase == Phase.Done) return;
            Finish();
        }

        private bool Begin(GameObject prefab, SpiderBrain prefabBrain, Transform head, string escapeState, string fightState)
        {
            settings = StoryBossEscapeSettings.Instance;
            int agentType = prefab.TryGetComponent(out NavMeshAgent prefabAgent) ? prefabAgent.agentTypeID : 0;
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };

            // Where it pops out: the runner that just broke (on the floor under it), or in front of the player.
            float since = Time.time - RunAwayPumpkin.LastBrokeTime;
            var from = RunAwayPumpkin.LastBrokeTime >= 0f && since >= 0f && since <= RecentBreak
                ? RunAwayPumpkin.LastBrokeAt
                : head.position + Flat(head.forward).normalized * 2f;
            if (NavMesh.SamplePosition(from, out var hit, 2.5f, filter)) from = hit.position;
            else from.y = head.position.y - 0.7f;

            if (!MakeCopy(prefab, prefabBrain)) return false;
            scale = prefabBrain.LookScale;
            body.SetPositionAndRotation(from, Quaternion.LookRotation(FacePlayer(from)));
            body.localScale = Vector3.one * scale * 0.2f;
            // Its body length (for the hole), from its look at full size.
            length = BodyLength() / 0.2f;

            // A smoke bomb vanishes it where it stands: no route, no hole. Run And Smoke runs off along a route away from the
            // player first (as to a dig spot), then smokes.
            if (SmokeBombStyle) { digAt = from; path = null; }
            else digAt = PickDigSpot(from, head.position, filter, out path);
            corner = 0;
            burrow = new Burrow { boss = prefab.name, escape = escapeState, fight = fightState, agentType = agentType };
            // A newer escape of the same boss replaces an old hole.
            for (int i = Burrows.Count - 1; i >= 0; i--)
            {
                if (Burrows[i].boss != burrow.boss) continue;
                if (Burrows[i].hole != null) Destroy(Burrows[i].hole.gameObject);
                Burrows.RemoveAt(i);
            }
            Burrows.Add(burrow);

            if (settings.PopPuff != null) EffectPool.Play(settings.PopPuff, from + Vector3.up * 0.2f, settings.PopPuffSize * scale, 4f);
            Sound(settings.PopSound, 1f);
            if (settings.SeedOnBoss) StoryBossSeed.Give(body.gameObject);
            Playing++;
            counted = true;
            Enter(Phase.Pop);
            return true;
        }

        // ---- The look-only copy ----

        private bool MakeCopy(GameObject prefab, SpiderBrain prefabBrain)
        {
            // Made under a switched-off holder, so none of its scripts wake up before they are taken off.
            var shell = new GameObject("Story_Boss_Escape_Shell");
            shell.SetActive(false);
            var copy = Instantiate(prefab, shell.transform);
            copy.name = prefab.name + " (escaping)";
            var brain = copy.GetComponent<SpiderBrain>();
            var models = brain.LookModels;
            animator = brain.LookAnimator;
            runLook = brain.LookRun;
            runLookSpeed = Mathf.Max(0.1f, brain.LookRunSpeed);
            alertLook = brain.LookAlert;
            appearLook = brain.LookAppear;
            hisses = brain.LookNoticeSounds;
            if (models == null || animator == null)
            {
                Destroy(shell);
                return false;
            }

            // Everything that thinks, hurts or collides goes; only the look stays.
            var scripts = copy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = scripts.Length - 1; i >= 0; i--) DestroyImmediate(scripts[i]);
            foreach (var part in copy.GetComponentsInChildren<Joint>(true)) DestroyImmediate(part);
            foreach (var part in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(part);
            foreach (var part in copy.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(part);
            foreach (var part in copy.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(part);
            foreach (var part in copy.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(part);
            foreach (Transform child in copy.transform)
            {
                if (!models.IsChildOf(child)) child.gameObject.SetActive(false);
            }
            // One of its hidden body variants, as a real one picks.
            var hidden = new List<GameObject>();
            foreach (Transform child in models)
            {
                if (!child.gameObject.activeSelf && child.GetComponentInChildren<Renderer>(true) != null) hidden.Add(child.gameObject);
            }
            if (hidden.Count > 0) hidden[Random.Range(0, hidden.Count)].SetActive(true);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Not under this show: the show stays a little longer than the copy, for its sounds to end.
            body = copy.transform;
            body.SetParent(transform.parent, false);
            Destroy(shell);

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.minDistance = 2f;
            voice.maxDistance = 25f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            return true;
        }

        private float BodyLength()
        {
            var bounds = new Bounds(body.position, Vector3.zero);
            bool any = false;
            foreach (var look in body.GetComponentsInChildren<Renderer>())
            {
                if (any) bounds.Encapsulate(look.bounds);
                else bounds = look.bounds;
                any = true;
            }
            return any ? Mathf.Max(bounds.size.x, bounds.size.z) : 0.5f;
        }

        // ---- Where it digs ----

        // A spot a few metres off, away from the player, on open floor it can run to. A cramped room gets a second,
        // looser go (nearer, closer to the player). None: it digs where it is.
        private Vector3 PickDigSpot(Vector3 from, Vector3 player, NavMeshQueryFilter filter, out Vector3[] route)
        {
            var range = settings.RunDistance;
            float near = Mathf.Max(0.5f, Mathf.Min(range.x, range.y)), far = Mathf.Max(range.x, range.y);
            var spot = PickDigSpot(from, player, filter, new[] { far, Mathf.Lerp(far, near, 0.5f), near },
                settings.KeepFromPlayer, MinClearance, out route);
            if (route != null) return spot;
            return PickDigSpot(from, player, filter, new[] { near, Mathf.Lerp(near, 1.2f, 0.5f), 1.2f },
                settings.KeepFromPlayer * 0.6f, MinClearance * 0.75f, out route);
        }

        private Vector3 PickDigSpot(Vector3 from, Vector3 player, NavMeshQueryFilter filter, float[] distances,
            float keepFromPlayer, float minClearance, out Vector3[] route)
        {
            route = null;
            var away = Flat(from - player);
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
            var navPath = new NavMeshPath();
            float best = float.NegativeInfinity;
            var bestSpot = from;
            for (int a = 0; a < 16; a++)
            {
                var dir = Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward;
                foreach (float distance in distances)
                {
                    if (!OpenFloor(from + dir * distance, filter, out var spot)) continue;
                    float fromPlayer = Flat(spot - player).magnitude;
                    if (fromPlayer < keepFromPlayer) continue;
                    if (!NavMesh.CalculatePath(from, spot, filter, navPath) || navPath.status != NavMeshPathStatus.PathComplete) continue;
                    float along = PathLength(navPath.corners);
                    if (along > distance * 2f + 0.5f) continue;
                    // It never runs through the player's legs; it rather keeps clear of them.
                    float clear = Clearance(navPath.corners, player);
                    if (clear < minClearance) continue;
                    float score = fromPlayer + 1.5f * Vector3.Dot(dir, away) - 0.3f * along - 3f * Mathf.Max(0f, GoodClearance - clear);
                    if (score <= best) continue;
                    best = score;
                    bestSpot = spot;
                    route = navPath.corners;
                }
            }
            return bestSpot;
        }

        // The open floor nearest this point, Wall Gap clear of walls and furniture: a point in or against furniture is
        // moved out of it, away from the floor's edge.
        private bool OpenFloor(Vector3 point, NavMeshQueryFilter filter, out Vector3 spot)
        {
            spot = point;
            if (!NavMesh.SamplePosition(point, out var hit, 0.8f, filter)) return false;
            spot = hit.position;
            float gap = settings.WallGap;
            if (!NavMesh.FindClosestEdge(spot, out var edge, filter) || edge.distance >= gap) return true;
            var outward = Flat(edge.normal);
            if (outward.sqrMagnitude < 0.0001f) return false;
            var moved = spot + outward.normalized * (gap - edge.distance + 0.05f);
            if (!NavMesh.SamplePosition(moved, out hit, 0.2f, filter)) return false;
            spot = hit.position;
            return !NavMesh.FindClosestEdge(spot, out edge, filter) || edge.distance >= gap * 0.9f;
        }

        // The nearest its route comes to the player, along the floor (from where it starts, which is next to them, a
        // little way on: it may begin close).
        private static float Clearance(Vector3[] corners, Vector3 player)
        {
            float nearest = float.MaxValue;
            var p = new Vector2(player.x, player.z);
            for (int i = 1; i < corners.Length; i++)
            {
                var a = new Vector2(corners[i - 1].x, corners[i - 1].z);
                var b = new Vector2(corners[i].x, corners[i].z);
                var ab = b - a;
                float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                // Its first half metre doesn't count: it pops out where the player caught the pumpkin.
                if (i == 1 && ab.magnitude > 0.0001f) t = Mathf.Max(t, Mathf.Min(1f, 0.5f / ab.magnitude));
                nearest = Mathf.Min(nearest, Vector2.Distance(p, a + ab * t));
            }
            return nearest;
        }

        private static float PathLength(Vector3[] corners)
        {
            float sum = 0f;
            for (int i = 1; i < corners.Length; i++) sum += Vector3.Distance(corners[i - 1], corners[i]);
            return sum;
        }

        // ---- The show ----

        private void Enter(Phase next)
        {
            phase = next;
            phaseStart = Time.time;
            switch (next)
            {
                case Phase.Hiss:
                    Look(alertLook, 1f);
                    if (hisses != null && hisses.Length > 0) Sound(hisses[Random.Range(0, hisses.Length)], 1f);
                    break;
                case Phase.Run:
                    Look(runLook, Mathf.Clamp(settings.RunSpeed / (runLookSpeed * scale), 0.6f, 2.5f));
                    break;
                case Phase.Dig:
                    Look(runLook, 2.5f);
                    Sound(settings.DigSound, settings.DigVolume);
                    nextDust = Time.time;
                    LastDigAt = digAt;
                    OpenHole();
                    break;
                case Phase.Smoke:
                    // The smoke bomb at its feet; it's gone inside it a moment later.
                    LastDigAt = body.position;
                    SmokeBombAt(body.position, length, settings);
                    var poofs = settings.SmokeBombSounds;
                    if (poofs != null && poofs.Length > 0) Sound(poofs[Random.Range(0, poofs.Length)], settings.SmokeBombVolume);
                    break;
            }
        }

        private bool SmokeBombStyle => settings != null && settings.Escape == StoryBossEscapeSettings.EscapeStyle.SmokeBomb;
        private bool RunSmokeStyle => settings != null && settings.Escape == StoryBossEscapeSettings.EscapeStyle.RunAndSmoke;

        /// <summary>How many smoke bombs have gone off (the escapes' and the real bosses' coming), for tests.</summary>
        public static int SmokeBombs { get; private set; }

        private static void SmokeBombAt(Vector3 feet, float bodyLength, StoryBossEscapeSettings settings)
        {
            SmokeBombs++;
            if (settings.SmokeBomb == null) return;
            EffectPool.Play(settings.SmokeBomb, feet + Vector3.up * (bodyLength * 0.15f),
                settings.SmokeBombSize * Mathf.Max(0.6f, bodyLength), 5f);
        }

        private void Update()
        {
            if (phase == Phase.Done)
            {
                FadeOut();
                return;
            }
            if (body == null)
            {
                Finish();
                return;
            }
            float t = Time.time - phaseStart;
            transform.position = body.position;
            switch (phase)
            {
                case Phase.Pop:
                {
                    float share = Mathf.Clamp01(t / settings.PopSeconds);
                    body.localScale = Vector3.one * scale * Mathf.Max(0.2f, OutBack(share));
                    Turn(FacePlayer(body.position), 900f);
                    if (share >= 1f) Enter(Phase.Hiss);
                    break;
                }
                case Phase.Hiss:
                    Turn(FacePlayer(body.position), 360f);
                    if (t >= settings.HissSeconds)
                        Enter(SmokeBombStyle ? Phase.Smoke : path != null && path.Length > 1 ? Phase.Run : RunSmokeStyle ? Phase.Smoke : Phase.Dig);
                    break;
                case Phase.Run:
                    // Run And Smoke: off it goes, and a moment later the smoke bomb.
                    if (RunSmokeStyle && t >= settings.RunSmokeSeconds) Enter(Phase.Smoke);
                    else RunAlong();
                    break;
                case Phase.Dig:
                    Dig(t);
                    break;
                case Phase.Smoke:
                    if (t >= settings.VanishAfter) Finish();
                    break;
            }
        }

        private void RunAlong()
        {
            float step = settings.RunSpeed * Time.deltaTime;
            while (step > 0f && corner < path.Length)
            {
                var target = path[corner];
                var to = target - body.position;
                float distance = to.magnitude;
                if (distance <= step)
                {
                    body.position = target;
                    step -= distance;
                    corner++;
                    continue;
                }
                body.position += to / distance * step;
                step = 0f;
                var flat = Flat(to);
                if (flat.sqrMagnitude > 0.0001f) Turn(flat, 720f);
            }
            if (corner >= path.Length)
            {
                body.position = digAt;
                Enter(RunSmokeStyle ? Phase.Smoke : Phase.Dig);
            }
        }

        private void Dig(float t)
        {
            float whole = settings.DigSeconds;
            float scratch = whole * settings.ScratchShare;
            if (Time.time >= nextDust && t < whole)
            {
                nextDust = Time.time + settings.DustEvery;
                if (settings.DigDust != null)
                {
                    var spray = digAt + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * (length * 0.25f);
                    EffectPool.Play(settings.DigDust, spray, settings.DigDustSize * Mathf.Max(0.6f, scale * 0.6f), 3f);
                }
            }
            if (t >= scratch && !string.IsNullOrEmpty(appearLook))
            {
                // Its climbing-out look played backwards: it starts under the floor and is out by a quarter of the way.
                float dive = Mathf.Clamp01((t - scratch) / Mathf.Max(0.05f, whole - scratch));
                animator.speed = 0f;
                animator.Play(appearLook, 0, Mathf.Lerp(0.35f, 0f, dive));
                // And a little lower, so nothing shows over the floor.
                body.position = digAt + Vector3.down * (length * 0.3f * dive * dive);
            }
            else if (t >= scratch)
            {
                float dive = Mathf.Clamp01((t - scratch) / Mathf.Max(0.05f, whole - scratch));
                body.position = digAt + Vector3.down * (length * 0.8f * dive);
            }
            if (t >= whole) Finish();
        }

        private void OpenHole()
        {
            if (settings.Hole == null) return;
            var go = Instantiate(settings.Hole, digAt + Vector3.up * 0.003f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.transform.SetParent(SpawnedStuff(), true);
            if (!go.TryGetComponent(out StoryBurrowHole hole)) hole = go.AddComponent<StoryBurrowHole>();
            hole.Open(Mathf.Clamp(length * settings.HoleSize, 0.15f, MostHoleRadius), settings.DigSeconds * 0.6f);
            if (burrow != null) burrow.hole = hole;
        }

        private void Finish()
        {
            phase = Phase.Done;
            if (counted)
            {
                counted = false;
                Playing = Mathf.Max(0, Playing - 1);
            }
            if (body != null) Destroy(body.gameObject);
            body = null;
            fadeFrom = Time.time + SoundHold;
        }

        // Its dig sound (a long rumble) runs on into the room's earthquake, then fades; then the show is gone.
        private void FadeOut()
        {
            float share = 1f - Mathf.Clamp01((Time.time - fadeFrom) / SoundFade);
            if (voice != null) voice.volume = share;
            if (share <= 0f || voice == null || !voice.isPlaying) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (counted) Playing = Mathf.Max(0, Playing - 1);
            counted = false;
            if (body != null) Destroy(body.gameObject);
        }

        // ---- The real boss comes back ----

        private static void BossStarted(SpiderBrain brain)
        {
            if (brain == null || !brain.IsBoss || Burrows.Count == 0) return;
            var named = brain.gameObject.name;
            int cut = named.IndexOf('(');
            var kind = (cut > 0 ? named.Substring(0, cut) : named).Trim();
            var now = GameStages.ActiveState();
            for (int i = Burrows.Count - 1; i >= 0; i--)
            {
                var burrow = Burrows[i];
                // The room has moved on: its hole and piece are done with.
                if (now != burrow.escape && now != burrow.fight)
                {
                    if (burrow.hole != null) Destroy(burrow.hole.gameObject);
                    Burrows.RemoveAt(i);
                    continue;
                }
                if (burrow.boss != kind) continue;
                var settings = StoryBossEscapeSettings.Instance;
                if (settings.SeedOnBoss) StoryBossSeed.Give(brain.gameObject);
                if (burrow.cameOut) continue;
                // Gone in a smoke bomb, back in one: where it appears.
                if (settings.Smokes)
                {
                    burrow.cameOut = true;
                    if (settings.ComeBackInSmoke) SmokeIn(brain, settings);
                    continue;
                }
                if (burrow.hole == null || !settings.ComeBackFromHole) continue;
                burrow.cameOut = true;
                ComeOut(brain, burrow, settings);
            }
        }

        private static void SmokeIn(SpiderBrain brain, StoryBossEscapeSettings settings)
        {
            var at = brain.transform.position;
            float bodyLength = 1f;
            var bounds = new Bounds(at, Vector3.zero);
            bool any = false;
            foreach (var look in brain.GetComponentsInChildren<Renderer>())
            {
                if (look is ParticleSystemRenderer || !look.enabled) continue;
                if (any) bounds.Encapsulate(look.bounds);
                else bounds = look.bounds;
                any = true;
            }
            if (any) bodyLength = Mathf.Max(bounds.size.x, bounds.size.z);
            SmokeBombAt(at, bodyLength, settings);
            var poofs = settings.SmokeBombSounds;
            if (poofs != null && poofs.Length > 0)
            {
                var clip = poofs[Random.Range(0, poofs.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, at, settings.SmokeBombVolume * settings.Volume);
            }
        }

        private static void ComeOut(SpiderBrain brain, Burrow burrow, StoryBossEscapeSettings settings)
        {
            var at = burrow.hole.transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = burrow.agentType, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(at, out var hit, 1.5f, filter)) return;
            if (brain.TryGetComponent(out NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh) agent.Warp(hit.position);
            else brain.transform.position = hit.position;
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head != null)
            {
                var face = Flat(head.position - hit.position);
                if (face.sqrMagnitude > 0.0001f) brain.transform.rotation = Quaternion.LookRotation(face);
            }
            if (settings.ComeBackDust != null)
            {
                float size = settings.DigDustSize * Mathf.Max(0.6f, brain.transform.lossyScale.y * 0.6f);
                for (int i = 0; i < 3; i++)
                {
                    var spray = hit.position + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 0.25f;
                    EffectPool.Play(settings.ComeBackDust, spray, size, 3f);
                }
            }
            if (settings.DigSound != null) AudioSource.PlayClipAtPoint(settings.DigSound, hit.position, settings.DigVolume * settings.Volume);
            burrow.hole.Close(settings.HoleClosesAfter, 1f);
        }

        // ---- Bits ----

        private void Look(string state, float speed)
        {
            if (animator == null || string.IsNullOrEmpty(state)) return;
            if (!animator.HasState(0, Animator.StringToHash(state))) return;
            animator.speed = speed;
            animator.CrossFadeInFixedTime(state, 0.1f, 0);
        }

        private void Sound(AudioClip clip, float volume)
        {
            if (clip == null || voice == null) return;
            voice.PlayOneShot(clip, volume * settings.Volume);
        }

        private void Turn(Vector3 toward, float degreesPerSecond)
        {
            if (toward.sqrMagnitude < 0.0001f) return;
            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(toward), degreesPerSecond * Time.deltaTime);
        }

        private static Vector3 FacePlayer(Vector3 from)
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            var face = head != null ? Flat(head.position - from) : Vector3.forward;
            return face.sqrMagnitude > 0.0001f ? face.normalized : Vector3.forward;
        }

        private static float OutBack(float t)
        {
            const float overshoot = 1.7f;
            t -= 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private static Transform SpawnedStuff()
        {
            var holder = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }
}
