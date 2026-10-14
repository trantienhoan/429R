using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Combat
{
    /// <summary>
    /// Real physics for tossing coins (Revive Coins), in a little physics world of its own that's stepped by hand: it
    /// runs while the game is stopped (time scale 0) and never touches the game's own physics. All the coins are thrown
    /// up at once from a round table with a low invisible wall; they tumble, bump into each other, bounce and settle, and
    /// whichever side ends up facing up counts. A coin that comes to rest on its edge is nudged over. The toss world has
    /// Y up and the table top at Y = 0, in its own units (bigger than on screen, so the fall looks slower).
    /// </summary>
    public sealed class CoinToss : System.IDisposable
    {
        /// <summary>How the toss goes, in the toss world's units (metres there).</summary>
        public struct Settings
        {
            public float coinWidth;
            public float tableRadius;
            public float throwHeight;
            public Vector2 turnsPerSecond;
            public float bounciness;
            public float friction;
        }

        public sealed class Coin
        {
            public Rigidbody Body;
            /// <summary>The coin's look (the prefab, its renderers off) under Body, for copying onto the shown coin.</summary>
            public Transform Model;
            /// <summary>At rest, and which side is up.</summary>
            public bool Settled;
            public bool Clover;
            /// <summary>Has hit the table (or another coin) at least once.</summary>
            public bool Landed;
            /// <summary>How fast it hit something during the last Step (0 = nothing), for a clink.</summary>
            public float Impact;
            internal Vector3 lastPosition;
            internal Quaternion lastRotation;
            internal float stillFor;
            internal int nudges;
            internal float fallSpeed;
        }

        public readonly List<Coin> Coins = new();

        /// <summary>Seconds simulated since the throw.</summary>
        public float Elapsed { get; private set; }

        /// <summary>
        /// Where a coin is to be drawn this frame: between its last two physics steps, so it moves smoothly at any frame
        /// rate instead of jumping a whole step at a time.
        /// </summary>
        public void Pose(Coin c, out Vector3 position, out Quaternion rotation)
        {
            float t = Mathf.Clamp01(pending / StepSeconds);
            position = Vector3.Lerp(c.lastPosition, c.Body.position, t);
            rotation = Quaternion.Slerp(c.lastRotation, c.Body.rotation, t);
        }

        public bool AllSettled
        {
            get
            {
                foreach (var c in Coins)
                {
                    if (!c.Settled) return false;
                }
                return Coins.Count > 0;
            }
        }

        private const float StepSeconds = 1f / 120f;
        private const int MaxStepsPerFrame = 16;
        // At rest: this slow (units/s) and turning this little (rad/s) for this long.
        private const float StillSpeed = 0.1f;
        private const float StillSpin = 0.6f;
        private const float StillSeconds = 0.15f;
        // In the air a coin keeps its spin; once it has landed it's slowed down hard, so it stops rolling and wobbling
        // round the table soon instead of for seconds like a real coin.
        private const float AirSpinDrag = 0.05f;
        private const float LandedSpinDrag = 4f;
        private const float LandedDrag = 1.2f;
        // A landed coin that's slow but still tilted is helped to fall flat, toward the side it's already leaning to, as
        // it would anyway (only sooner): this hard, below this speed.
        private const float FallFlat = 40f;
        private const float FallFlatBelow = 1.2f;
        // Resting with the clover axis this close to sideways counts as on its edge.
        private const float OnEdge = 0.5f;
        private const int MaxNudges = 4;
        private const int WallSegments = 24;

        private static int serial;
        private readonly Scene scene;
        private readonly PhysicsScene physics;
        private readonly Vector3 cloverAxis;
        private readonly PhysicsMaterial material;
        private float pending;

        /// <summary>
        /// Sets up the table and the coins (copies of the prefab with only their colliders working; a prefab without
        /// colliders gets a box) and throws them. cloverAxis: the side of the prefab the clover is on, in its own space.
        /// </summary>
        public CoinToss(GameObject coinPrefab, Vector3 cloverAxisLocal, int count, Settings s)
        {
            cloverAxis = cloverAxisLocal.normalized;
            scene = SceneManager.CreateScene("Coin Toss " + ++serial, new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
            material = new PhysicsMaterial("Coin")
            {
                bounciness = s.bounciness,
                dynamicFriction = s.friction,
                staticFriction = s.friction * 1.3f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average,
            };

            // The table (its top at y = 0) and a ring of walls, tall enough that nothing flies out.
            float wallHeight = s.throwHeight * 2.5f + s.coinWidth * 2f;
            float thick = s.coinWidth;
            AddBox("Table", new Vector3(0f, -thick * 0.5f, 0f), Quaternion.identity, new Vector3(s.tableRadius * 3f, thick, s.tableRadius * 3f));
            float segment = 2f * Mathf.PI * (s.tableRadius + thick * 0.5f) / WallSegments * 1.15f;
            for (int i = 0; i < WallSegments; i++)
            {
                float a = i * Mathf.PI * 2f / WallSegments;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                AddBox("Wall", outward * (s.tableRadius + thick * 0.5f) + Vector3.up * wallHeight * 0.5f,
                    Quaternion.LookRotation(outward), new Vector3(segment, wallHeight, thick));
            }

            // The coins start on a little ring (far enough apart not to touch), at slightly different heights.
            float ring = count <= 1 ? 0f : s.coinWidth * 0.6f / Mathf.Sin(Mathf.PI / count);
            float g = Mathf.Abs(Physics.gravity.y) > 0.01f ? Mathf.Abs(Physics.gravity.y) : 9.81f;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / Mathf.Max(1, count) + Random.Range(-0.2f, 0.2f);
                var start = new Vector3(Mathf.Cos(a) * ring, s.coinWidth * 0.5f + i * s.coinWidth * 0.2f, Mathf.Sin(a) * ring);
                // Any way up to start with, so neither side is favoured.
                var tumbleAxis = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.right;
                var turn = Quaternion.AngleAxis(Random.Range(0f, 360f), tumbleAxis) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                var coin = MakeCoin(coinPrefab, i, start, turn, s.coinWidth);

                // Thrown up (a little toward the middle, a little at random), spinning over and over.
                float up = Mathf.Sqrt(2f * g * s.throwHeight) * Random.Range(0.85f, 1.15f);
                var side = Random.insideUnitCircle * (up * 0.12f);
                var inward = -new Vector3(start.x, 0f, start.z) * 0.6f;
                coin.Body.linearVelocity = new Vector3(side.x, up, side.y) + inward;
                var spinAxis = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.right;
                coin.Body.angularVelocity = spinAxis * (2f * Mathf.PI * Random.Range(s.turnsPerSecond.x, s.turnsPerSecond.y)) +
                                            Vector3.up * Random.Range(-2f, 2f);
                Coins.Add(coin);
            }
        }

        private void AddBox(string name, Vector3 position, Quaternion rotation, Vector3 size)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(position, rotation);
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = material;
        }

        private Coin MakeCoin(GameObject prefab, int index, Vector3 position, Quaternion rotation, float width)
        {
            var root = new GameObject("Coin " + (index + 1));
            SceneManager.MoveGameObjectToScene(root, scene);
            // Placed before its body is made, so the body starts there too (transforms aren't synced to physics by itself).
            root.transform.SetPositionAndRotation(position, rotation);

            var model = Object.Instantiate(prefab);
            model.name = prefab.name;
            model.transform.SetParent(root.transform, false);
            // Only its shape takes part: no look, no sound, no scripts, no bodies of its own.
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var l in model.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var a in model.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            foreach (var b in model.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);

            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var own = ReviveCoins.OwnBounds(model.transform, out bool any);
            float size = any ? Mathf.Max(own.size.x, Mathf.Max(own.size.y, own.size.z)) : 1f;
            if (model.GetComponentInChildren<Collider>(true) == null)
            {
                var box = model.AddComponent<BoxCollider>();
                box.center = own.center;
                box.size = own.size;
            }
            float scale = size > 0.0001f ? width / size : 1f;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = -own.center * scale;
            foreach (var c in model.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = true;
                if (c is MeshCollider mc) mc.convex = true;
                c.sharedMaterial = material;
            }

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.maxAngularVelocity = 80f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.angularDamping = AirSpinDrag;
            body.linearDamping = 0f;
            return new Coin { Body = body, Model = model.transform, lastPosition = position, lastRotation = rotation };
        }

        /// <summary>Runs the toss on by this many (real) seconds.</summary>
        public void Step(float seconds)
        {
            foreach (var c in Coins) c.Impact = 0f;
            pending += Mathf.Min(seconds, 0.1f);
            for (int n = 0; pending >= StepSeconds && n < MaxStepsPerFrame; n++)
            {
                pending -= StepSeconds;
                foreach (var c in Coins)
                {
                    c.lastPosition = c.Body.position;
                    c.lastRotation = c.Body.rotation;
                }
                physics.Simulate(StepSeconds);
                Elapsed += StepSeconds;
                foreach (var c in Coins) Watch(c);
            }
        }

        private void Watch(Coin c)
        {
            var body = c.Body;
            var v = body.linearVelocity;
            // A hit: it was falling and now isn't.
            if (c.fallSpeed > 0.8f && v.y > -0.2f)
            {
                c.Impact = Mathf.Max(c.Impact, c.fallSpeed);
                if (!c.Landed)
                {
                    c.Landed = true;
                    body.angularDamping = LandedSpinDrag;
                    body.linearDamping = LandedDrag;
                }
            }
            c.fallSpeed = Mathf.Max(0f, -v.y);

            if (c.Landed && v.magnitude < FallFlatBelow)
            {
                var axisNow = body.rotation * cloverAxis;
                float upNow = Vector3.Dot(axisNow, Vector3.up);
                if (Mathf.Abs(upNow) < 0.985f)
                {
                    // The face it's leaning toward turns up.
                    var face = upNow >= 0f ? axisNow : -axisNow;
                    var turn = Vector3.Cross(face, Vector3.up);
                    if (turn.sqrMagnitude > 1e-6f) body.AddTorque(turn.normalized * (FallFlat * (1f - Mathf.Abs(upNow))), ForceMode.Acceleration);
                }
            }

            bool still = body.IsSleeping() || (v.magnitude < StillSpeed && body.angularVelocity.magnitude < StillSpin);
            c.stillFor = still ? c.stillFor + StepSeconds : 0f;
            if (!still)
            {
                // Knocked about again by another coin: not settled any more.
                if (c.Settled && v.magnitude > StillSpeed * 4f) c.Settled = false;
                return;
            }
            if (c.Settled || c.stillFor < StillSeconds) return;

            float up = Vector3.Dot(body.rotation * cloverAxis, Vector3.up);
            if (Mathf.Abs(up) < OnEdge && c.nudges < MaxNudges)
            {
                // Resting on its edge (or leaning on the wall or another coin): a little push over.
                c.nudges++;
                c.stillFor = 0f;
                body.WakeUp();
                var axis = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.right;
                body.AddForce(Vector3.up * 1.2f, ForceMode.VelocityChange);
                body.AddTorque(axis * 6f, ForceMode.VelocityChange);
                return;
            }
            c.Settled = true;
            c.Clover = up > 0f;
        }

        /// <summary>Time's up: every coin counts as it lies now.</summary>
        public void Finish()
        {
            foreach (var c in Coins)
            {
                if (c.Settled) continue;
                c.Settled = true;
                c.Clover = Vector3.Dot(c.Body.rotation * cloverAxis, Vector3.up) > 0f;
            }
        }

        public void Dispose()
        {
            Coins.Clear();
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            if (material != null) Object.Destroy(material);
        }
    }
}
