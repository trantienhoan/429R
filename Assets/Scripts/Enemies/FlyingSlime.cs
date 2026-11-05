using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The flying paint slime's looks (user 2026-11-05: no rig, so its model's parts are moved here). It hovers, bobbing up
    /// on each beat of its bat wings, and breathes; it leans into the way it flies and stretches with speed; it squashes and
    /// wobbles when bonked (a white flash, eyes shut, a few drops shaken off); its stem lags behind and wobbles; its
    /// glowing eyes pulse, blink and follow the player; paint drops form under it, hang a moment, then fall and splat on the
    /// floor; Pop swells it and bursts it like a balloon of paint. Whatever moves it (an enemy brain, a bonus round) only
    /// moves and turns this object (its front = blue arrow) and calls Dress, Bonk, WindUp and Pop.
    /// Tools > 429 Game > Enemies > Set Up Flying Slime builds the prefab (Prefabs/Enemies/Flying_Slime).
    /// </summary>
    [DisallowMultipleComponent]
    public class FlyingSlime : MonoBehaviour
    {
        [Header("Parts (Set Up Flying Slime fills these)")]
        [Tooltip("Scaled along the way it stretches (Body Squash, the model's grandparent).")]
        [SerializeField] private Transform squash;
        [Tooltip("Undoes Body Squash's turn and leans the model (Body Turn, the model's parent).")]
        [SerializeField] private Transform turn;
        [Tooltip("The model (the FBX): its parts below are moved.")]
        [SerializeField] private Transform model;
        [SerializeField] private Renderer body;
        [SerializeField] private Transform wingLeft;
        [SerializeField] private Transform wingRight;
        [SerializeField] private Transform stem;
        [SerializeField] private Transform eyeLeft;
        [SerializeField] private Transform eyeRight;
        [Tooltip("Paint drops forming under it and falling (world space; emitted from here).")]
        [SerializeField] private ParticleSystem drips;
        [Tooltip("Blobs and drops of the pop (and the few a bonk shakes off).")]
        [SerializeField] private ParticleSystem popBlobs;
        [Tooltip("The paint skin bursting open, a quick round splash.")]
        [SerializeField] private ParticleSystem popShell;
        [Tooltip("The plane drops splat on (kept at the floor under it).")]
        [SerializeField] private Transform floor;

        [Header("Colour")]
        [SerializeField] private Color colour = new Color(0.89f, 0.15f, 0.18f);
        [Tooltip("Rainbow paint: its colour goes round the rainbow, and its drops are every colour.")]
        [SerializeField] private bool rainbow;
        [Tooltip("Rainbow: times round the rainbow a second (the wings' colour, and the skin turning round it).")]
        [Min(0f)]
        [SerializeField] private float rainbowSpeed = 0.25f;
        [Tooltip("Rainbow: the body's skin (swirled rainbow stripes; Set Up Flying Slime makes it).")]
        [SerializeField] private Material rainbowMaterial;
        [Tooltip("The wings are this much as bright as the body.")]
        [Range(0f, 1f)]
        [SerializeField] private float wingShade = 0.82f;
        [Tooltip("Its paint glows this much of its colour, so it reads in the dark rooms.")]
        [Range(0f, 1f)]
        [SerializeField] private float glow = 0.14f;

        [Header("Hover and wings")]
        [Tooltip("Wing beats a second hovering, and flying at Fast Speed or quicker.")]
        [SerializeField] private float flapsPerSecond = 4.2f;
        [SerializeField] private float fastFlapsPerSecond = 7f;
        [Tooltip("Metres a second at which it beats its wings fastest (and leans and stretches the most).")]
        [Min(0.1f)]
        [SerializeField] private float fastSpeed = 2.5f;
        [Tooltip("Degrees the wings go up, and down, from where they're modelled.")]
        [SerializeField] private float flapUp = 38f;
        [SerializeField] private float flapDown = 26f;
        [Tooltip("Metres it rises on each downbeat of its wings.")]
        [SerializeField] private float liftPerFlap = 0.012f;
        [Tooltip("A slow bob on top, metres up and down.")]
        [SerializeField] private float hoverBob = 0.008f;
        [Tooltip("It breathes: this much taller and shorter (0.025 = 2.5%).")]
        [SerializeField] private float breathe = 0.025f;

        [Header("Flying")]
        [Tooltip("Degrees it leans into the way it flies, per metre a second, and the most.")]
        [SerializeField] private float leanPerSpeed = 14f;
        [SerializeField] private float mostLean = 30f;
        [Tooltip("How much longer it stretches along the way it flies, per metre a second, and the most.")]
        [SerializeField] private float stretchPerSpeed = 0.07f;
        [SerializeField] private float mostStretch = 0.3f;

        [Header("Squish")]
        [Tooltip("Wobbles a second, and seconds until it has settled.")]
        [SerializeField] private float wobblesPerSecond = 4.5f;
        [SerializeField] private float settleTime = 0.9f;
        [Tooltip("A strength 1 bonk squashes it this much (0.35 = 35% flatter) and rocks it this many degrees away.")]
        [SerializeField] private float bonkSquash = 0.35f;
        [SerializeField] private float bonkTilt = 24f;
        [Tooltip("Seconds of the white flash on a bonk.")]
        [SerializeField] private float flashSeconds = 0.14f;
        [Tooltip("Drops a strength 1 bonk shakes off.")]
        [SerializeField] private int bonkDrops = 4;
        [Tooltip("Winding up (before a dive): this much flatter, leaning back this many degrees.")]
        [SerializeField] private float windUpSquash = 0.22f;
        [SerializeField] private float windUpLean = 16f;

        [Header("Stem")]
        [Tooltip("Degrees the stem lags per metre a second² it speeds up or slows down, and the most.")]
        [SerializeField] private float stemPerAcceleration = 2.5f;
        [SerializeField] private float stemMost = 28f;
        [SerializeField] private float stemWobblesPerSecond = 3f;
        [SerializeField] private float stemSettleTime = 1.2f;

        [Header("Eyes")]
        [Tooltip("Seconds between blinks (random between).")]
        [SerializeField] private Vector2 blinkEvery = new Vector2(2f, 5f);
        [SerializeField] private float blinkSeconds = 0.13f;
        [ColorUsage(false, true)]
        [SerializeField] private Color eyeGlow = new Color(2.2f, 0.92f, 0.13f);
        [Tooltip("The glow pulses this much (0.2 = 20% brighter and dimmer).")]
        [Range(0f, 1f)]
        [SerializeField] private float eyePulse = 0.2f;
        [Tooltip("How far (metres) the pupils move in their sockets to look at the player.")]
        [SerializeField] private float lookReach = 0.004f;

        [Header("Drips")]
        [Tooltip("Drops a second (on average).")]
        [Min(0f)]
        [SerializeField] private float dripsPerSecond = 1.1f;
        [Tooltip("Metres across a drop (random between).")]
        [SerializeField] private Vector2 dripSize = new Vector2(0.02f, 0.032f);
        [Tooltip("Drops form this far out from the middle of its bottom, at most (metres).")]
        [SerializeField] private float dripSpread = 0.075f;

        [Header("Pop")]
        [Tooltip("Seconds it shudders and swells before it bursts, and how big it swells.")]
        [SerializeField] private float popSeconds = 0.08f;
        [SerializeField] private float popSwell = 1.35f;
        [SerializeField] private int popBigBlobs = 14;
        [SerializeField] private int popDroplets = 20;
        [Tooltip("The bursting skin's size, metres across.")]
        [SerializeField] private float popShellSize = 0.36f;
        [SerializeField] private AudioClip[] popSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float popVolume = 0.8f;
        [Tooltip("Gone once it has burst (its drops and splats stay until they're done).")]
        [SerializeField] private bool destroyOnPop = true;

        /// <summary>It has burst (after its swell): where, and its colour.</summary>
        public event Action<Vector3, Color> Popped;
        public Color Colour => colour;
        public bool Rainbow => rainbow;
        public bool Popping { get; private set; }

        private const float TiltSpeed = 0.8f;
        private const float MaxStep = 1f / 240f;
        private const float DropLife = 6f;

        private Vector3 lastPosition;
        private Vector3 velocity, acceleration;
        private bool started;
        private float flapPhase, flapBoost = 1f;
        private float squashAmount, squashSpeed;
        private Vector3 tiltAmount, tiltSpeed;
        private Vector2 stemAmount, stemSpeed;
        private Vector3 stretchAxis = Vector3.up;
        private float windUpUntil, windUpLength;
        private float flashUntil;
        private float nextBlink, blinkUntil, shutUntil;
        private float nextDrip;
        private float floorY, nextFloorCheck;
        private float popStarted;
        private Vector3 popPush;
        private float hue;

        private Quaternion wingLeftRest, wingRightRest, stemRest;
        private Vector3 eyeLeftRest, eyeRightRest, eyeLeftScale, eyeRightScale;
        private float wingLeftSide = -1f, wingRightSide = 1f;
        private Renderer[] wingRenderers = Array.Empty<Renderer>();
        private Renderer[] eyeRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock block;
        private Bounds bodyBounds;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private Material[] bodyMaterials;
        private float skinTurn;
        // The textured model's skin (429/Slime Lit): its texture says what takes the paint colour; rainbow is in the
        // shader; the body's Glow Colour lights the sockets with the eyes' pulse.
        private bool slimeShader;
        private Color eyeNow;
        private static readonly int PaintGlowId = Shader.PropertyToID("_PaintGlow");
        private static readonly int RainbowId = Shader.PropertyToID("_Rainbow");

        /// <summary>Its paint: a colour, or rainbow.</summary>
        public void Dress(Color paint, bool isRainbow = false)
        {
            colour = paint;
            rainbow = isRainbow;
            if (rainbow) Color.RGBToHSV(paint, out hue, out _, out _);
            Skin();
            // Now, not from its next frame: one just made (a split-off half) would show the material's red for a frame.
            if (block != null) Paint();
        }

        // The body's own paint, or the rainbow skin.
        private void Skin()
        {
            if (body == null || slimeShader) return;
            if (bodyMaterials == null) bodyMaterials = body.sharedMaterials;
            if (rainbow && rainbowMaterial != null && bodyMaterials.Length > 0)
            {
                var mats = (Material[])bodyMaterials.Clone();
                mats[0] = rainbowMaterial;
                body.sharedMaterials = mats;
            }
            else body.sharedMaterials = bodyMaterials;
        }

        /// <summary>Bonked from 'from' (e.g. the fist): squashes, rocks away, flashes, shuts its eyes, shakes off drops.</summary>
        public void Bonk(Vector3 from, float strength = 1f)
        {
            if (Popping || strength <= 0f) return;
            float omega = 2f * Mathf.PI * wobblesPerSecond;
            squashSpeed -= bonkSquash * strength * omega;
            var away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            var axis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, away.normalized));
            tiltSpeed += axis * (bonkTilt * strength * omega * TiltSpeed);
            flashUntil = Time.time + flashSeconds;
            shutUntil = Time.time + 0.28f;
            flapPhase = 0.42f;   // wings flung up
            ShakeOffDrops(Mathf.RoundToInt(bonkDrops * Mathf.Clamp(strength, 0.5f, 2f)), away.normalized);
        }

        /// <summary>A squish with no flash and no drops (a giggle, straining to poop): strength 1 = a bonk's squash.</summary>
        public void Wobble(float strength = 1f)
        {
            if (Popping || strength <= 0f) return;
            squashSpeed -= bonkSquash * strength * 2f * Mathf.PI * wobblesPerSecond;
        }

        /// <summary>Winds up for Seconds (flatter, leaning back, wings beating harder), then springs out long: before a dive.</summary>
        public void WindUp(float seconds = 0.3f)
        {
            if (Popping) return;
            windUpLength = Mathf.Max(0.05f, seconds);
            windUpUntil = Time.time + windUpLength;
        }

        /// <summary>Swells for a moment, then bursts like a balloon of paint (blobs flying away from 'from', if given).</summary>
        public void Pop() => Pop(null);

        public void Pop(Vector3? from)
        {
            if (Popping) return;
            Popping = true;
            popStarted = Time.time;
            popPush = from.HasValue ? (transform.position - from.Value).normalized : Vector3.zero;
        }

        /// <summary>
        /// Lets go of its drops, the pop's blobs and their floor (and splats), so they fall and fade by themselves when it's
        /// gone (e.g. the Paint Bees round bursting it with its own splash).
        /// </summary>
        public void ReleaseDrops()
        {
            foreach (var system in new[] { drips, popBlobs, popShell })
            {
                if (system == null || system.transform.parent == null) continue;
                system.transform.SetParent(null, true);
                Destroy(system.gameObject, DropLife);
            }
            if (floor != null && floor.parent != null)
            {
                floor.SetParent(null, true);
                Destroy(floor.gameObject, DropLife);
            }
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (wingLeft != null)
            {
                wingLeftRest = wingLeft.localRotation;
                wingLeftSide = WingSide(wingLeft, -1f);
                wingRenderers = Append(wingRenderers, wingLeft.GetComponent<Renderer>());
            }
            if (wingRight != null)
            {
                wingRightRest = wingRight.localRotation;
                wingRightSide = WingSide(wingRight, 1f);
                wingRenderers = Append(wingRenderers, wingRight.GetComponent<Renderer>());
            }
            if (stem != null) stemRest = stem.localRotation;
            if (eyeLeft != null)
            {
                eyeLeftRest = eyeLeft.localPosition;
                eyeLeftScale = eyeLeft.localScale;
                eyeRenderers = Append(eyeRenderers, eyeLeft.GetComponent<Renderer>());
            }
            if (eyeRight != null)
            {
                eyeRightRest = eyeRight.localPosition;
                eyeRightScale = eyeRight.localScale;
                eyeRenderers = Append(eyeRenderers, eyeRight.GetComponent<Renderer>());
            }
            var filter = body != null ? body.GetComponent<MeshFilter>() : null;
            bodyBounds = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.26f);
            if (rainbow) Color.RGBToHSV(colour, out hue, out _, out _);
            slimeShader = body != null && body.sharedMaterial != null && body.sharedMaterial.HasProperty(PaintGlowId);
            eyeNow = eyeGlow;
            Skin();
            flapPhase = UnityEngine.Random.value;
            nextBlink = Time.time + UnityEngine.Random.Range(blinkEvery.x, blinkEvery.y);
            nextDrip = UnityEngine.Random.Range(0.2f, 1f) / Mathf.Max(0.01f, dripsPerSecond);
        }

        private void OnEnable()
        {
            started = false;
            nextFloorCheck = 0f;
        }

        // Which way the wing points out (its mesh's middle, left or right of its hinge), so flapping lifts its tip.
        private static float WingSide(Transform wing, float fallback)
        {
            var filter = wing.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return fallback;
            float x = filter.sharedMesh.bounds.center.x;
            return Mathf.Abs(x) < 0.001f ? fallback : Mathf.Sign(x);
        }

        private static Renderer[] Append(Renderer[] list, Renderer item)
        {
            if (item == null) return list;
            var longer = new Renderer[list.Length + 1];
            list.CopyTo(longer, 0);
            longer[list.Length] = item;
            return longer;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Track(dt);
            UpdateFloor();
            if (Popping)
            {
                UpdatePop();
                return;
            }
            Springs(dt);
            Pose(dt);
            Eyes();
            Paint();
            Drip(dt);
        }

        // Its velocity and acceleration, from how it's moved (a jump, e.g. when it's placed, counts as standing still).
        private void Track(float dt)
        {
            var position = transform.position;
            if (!started)
            {
                started = true;
                lastPosition = position;
                velocity = acceleration = Vector3.zero;
                return;
            }
            var raw = (position - lastPosition) / dt;
            lastPosition = position;
            if (raw.sqrMagnitude > 30f * 30f) raw = velocity;
            var before = velocity;
            velocity = Vector3.Lerp(velocity, raw, 1f - Mathf.Exp(-dt / 0.08f));
            acceleration = Vector3.Lerp(acceleration, (velocity - before) / dt, 1f - Mathf.Exp(-dt / 0.06f));
        }

        private void Springs(float dt)
        {
            float speed = velocity.magnitude;
            float fast = Mathf.Clamp01(speed / fastSpeed);
            bool windingUp = Time.time < windUpUntil;

            // What the squash spring settles to: longer with speed, a breath, flatter while winding up.
            float target = Mathf.Min(speed * stretchPerSpeed, mostStretch) + breathe * Mathf.Sin(Time.time * 2f * Mathf.PI * 0.9f);
            if (windingUp) target -= windUpSquash * Mathf.Clamp01((Time.time - (windUpUntil - windUpLength)) / (windUpLength * 0.6f));
            if (!windingUp && windUpLength > 0f)
            {
                // Wound up, it springs out long.
                squashSpeed += windUpSquash * 2.2f * 2f * Mathf.PI * wobblesPerSecond;
                windUpLength = 0f;
            }
            flapBoost = Mathf.Lerp(flapBoost, windingUp ? 1.7f : 1f, 1f - Mathf.Exp(-dt * 8f));

            float omega = 2f * Mathf.PI * wobblesPerSecond;
            float damping = 2f * Mathf.Min(1f, 3f / (omega * settleTime)) * omega;
            float tiltOmega = omega * TiltSpeed;
            float tiltDamping = 2f * Mathf.Min(1f, 3f / (tiltOmega * settleTime)) * tiltOmega;
            float stemOmega = 2f * Mathf.PI * stemWobblesPerSecond;
            float stemDamping = 2f * Mathf.Min(1f, 3f / (stemOmega * stemSettleTime)) * stemOmega;

            // The stem's top lags behind as it speeds up (in the model's own frame), and sways a little.
            var accelLocal = model != null ? model.InverseTransformDirection(acceleration) : Vector3.zero;
            var stemTarget = Vector2.ClampMagnitude(new Vector2(-accelLocal.x, -accelLocal.z) * stemPerAcceleration, stemMost)
                             + new Vector2(Mathf.Sin(Time.time * 1.7f), Mathf.Sin(Time.time * 1.3f + 1f)) * 2.5f;

            float left = Mathf.Min(dt, 0.1f);
            while (left > 0f)
            {
                float step = Mathf.Min(left, MaxStep);
                left -= step;
                squashSpeed += (-omega * omega * (squashAmount - target) - damping * squashSpeed) * step;
                squashAmount = Mathf.Clamp(squashAmount + squashSpeed * step, -0.6f, 0.6f);
                tiltSpeed += (-tiltOmega * tiltOmega * tiltAmount - tiltDamping * tiltSpeed) * step;
                tiltAmount += tiltSpeed * step;
                stemSpeed += (-stemOmega * stemOmega * (stemAmount - stemTarget) - stemDamping * stemSpeed) * step;
                stemAmount += stemSpeed * step;
            }
            tiltAmount = Vector3.ClampMagnitude(tiltAmount, 60f);
            stemAmount = Vector2.ClampMagnitude(stemAmount, stemMost * 1.5f);

            // The way it stretches: along its flight when it's quick, up and down when it's slow.
            var want = speed > 0.2f ? velocity / speed : Vector3.up;
            if (Vector3.Dot(want, stretchAxis) < 0f) want = -want;
            stretchAxis = Vector3.Slerp(stretchAxis, Vector3.Lerp(Vector3.up, want, fast), 1f - Mathf.Exp(-dt * 10f)).normalized;

            flapPhase = Mathf.Repeat(flapPhase + Mathf.Lerp(flapsPerSecond, fastFlapsPerSecond, fast) * flapBoost * dt, 1f);
        }

        private void Pose(float dt)
        {
            if (squash == null || turn == null) return;
            // A quick downbeat (40% of a beat), a slower beat back up. +1 = wings up.
            float stroke = flapPhase < 0.4f ? Mathf.Cos(flapPhase / 0.4f * Mathf.PI) : -Mathf.Cos((flapPhase - 0.4f) / 0.6f * Mathf.PI);
            float angle = stroke > 0f ? stroke * flapUp : stroke * flapDown;
            if (wingLeft != null) wingLeft.localRotation = wingLeftRest * Quaternion.AngleAxis(angle * wingLeftSide, Vector3.forward);
            if (wingRight != null) wingRight.localRotation = wingRightRest * Quaternion.AngleAxis(angle * wingRightSide, Vector3.forward);

            // Up as its wings come down, and a slow bob.
            float lift = -stroke * liftPerFlap * 0.5f + Mathf.Sin(Time.time * 2f * Mathf.PI * 0.55f) * hoverBob;
            squash.localPosition = Vector3.up * lift;

            // Stretch along the stretch axis (in this object's frame), keeping its volume.
            var axis = transform.InverseTransformDirection(stretchAxis);
            var toAxis = Quaternion.FromToRotation(Vector3.up, axis);
            float height = 1f + squashAmount;
            float width = 1f / Mathf.Sqrt(Mathf.Max(0.2f, height));
            squash.localRotation = toAxis;
            squash.localScale = new Vector3(width, height, width);

            // Lean into the way it flies (its top towards it), back while winding up, plus the bonk's rocking.
            var flat = transform.InverseTransformDirection(velocity);
            flat.y = 0f;
            float leanDegrees = Mathf.Min(flat.magnitude * leanPerSpeed, mostLean);
            var lean = flat.sqrMagnitude > 0.0001f ? Quaternion.AngleAxis(leanDegrees, Vector3.Cross(Vector3.up, flat.normalized)) : Quaternion.identity;
            if (Time.time < windUpUntil) lean = Quaternion.AngleAxis(windUpLean, Vector3.Cross(Vector3.up, Vector3.back)) * lean;
            float rock = tiltAmount.magnitude;
            if (rock > 0.0001f) lean = Quaternion.AngleAxis(rock, tiltAmount / rock) * lean;
            turn.localRotation = Quaternion.Inverse(toAxis) * lean;

            if (stem != null)
            {
                float bend = stemAmount.magnitude;
                var bendAxis = bend > 0.0001f ? Vector3.Cross(Vector3.up, new Vector3(stemAmount.x, 0f, stemAmount.y) / bend) : Vector3.right;
                stem.localRotation = Quaternion.AngleAxis(bend, bendAxis) * stemRest;
            }
        }

        private void Eyes()
        {
            float now = Time.time;
            if (now >= nextBlink)
            {
                blinkUntil = now + blinkSeconds;
                nextBlink = now + UnityEngine.Random.Range(blinkEvery.x, blinkEvery.y);
            }
            float open = 1f;
            if (now < blinkUntil) open = 1f - Mathf.Sin((1f - (blinkUntil - now) / blinkSeconds) * Mathf.PI) * 0.88f;
            if (now < shutUntil) open = 0.12f;

            var cam = Camera.main;
            Vector3 look = Vector3.zero;
            if (cam != null && model != null)
            {
                var dir = model.InverseTransformDirection((cam.transform.position - transform.position).normalized);
                look = Vector3.ClampMagnitude(new Vector3(dir.x, dir.y, 0f), 1f) * lookReach;
            }
            if (eyeLeft != null)
            {
                eyeLeft.localPosition = eyeLeftRest + look;
                eyeLeft.localScale = new Vector3(eyeLeftScale.x, eyeLeftScale.y * open, eyeLeftScale.z);
            }
            if (eyeRight != null)
            {
                eyeRight.localPosition = eyeRightRest + look;
                eyeRight.localScale = new Vector3(eyeRightScale.x, eyeRightScale.y * open, eyeRightScale.z);
            }
            float pulse = 1f + eyePulse * Mathf.Sin(now * 2f * Mathf.PI * 0.8f);
            eyeNow = eyeGlow * pulse;
            foreach (var r in eyeRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, eyeNow);
                r.SetPropertyBlock(block);
            }
        }

        private Color PaintNow()
        {
            if (!rainbow) return colour;
            hue = Mathf.Repeat(hue + rainbowSpeed * Time.deltaTime, 1f);
            return Color.HSVToRGB(hue, 0.85f, 1f);
        }

        private void Paint(float whiten = -1f)
        {
            var c = PaintNow();
            if (whiten < 0f) whiten = Time.time < flashUntil ? (flashUntil - Time.time) / Mathf.Max(0.01f, flashSeconds) : 0f;
            if (slimeShader)
            {
                PaintTextured(c, whiten);
                return;
            }
            bool skin = rainbow && rainbowMaterial != null;
            // The rainbow skin keeps its own colours (white = as painted) and turns slowly round it.
            var shown = Color.Lerp(skin ? Color.white : c, Color.white, whiten * 0.85f);
            if (body != null)
            {
                body.GetPropertyBlock(block, 0);
                block.SetColor(BaseColorId, shown);
                block.SetColor(EmissionId, shown * (glow + whiten * 0.6f));
                if (skin)
                {
                    skinTurn = Mathf.Repeat(skinTurn + rainbowSpeed * 0.4f * Time.deltaTime, 1f);
                    block.SetVector(BaseMapStId, new Vector4(1f, 1f, skinTurn, 0f));
                }
                body.SetPropertyBlock(block, 0);
            }
            var wing = Color.Lerp(c * wingShade, Color.white, whiten * 0.85f);
            wing.a = 1f;
            foreach (var r in wingRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, wing);
                block.SetColor(EmissionId, wing * glow);
                r.SetPropertyBlock(block);
            }
        }

        // The textured model: the paint colour (white for rainbow: the shader makes it), its glow, the flash.
        private void PaintTextured(Color c, float whiten)
        {
            var paint = rainbow ? Color.white : c;
            var shown = Color.Lerp(paint, Color.white, whiten * 0.85f);
            float shine = glow + whiten * 0.6f;
            if (body != null)
            {
                body.GetPropertyBlock(block);
                block.SetColor(BaseColorId, shown);
                block.SetFloat(PaintGlowId, shine);
                block.SetFloat(RainbowId, rainbow ? 1f : 0f);
                block.SetColor(EmissionId, eyeNow);
                body.SetPropertyBlock(block);
            }
            var wing = Color.Lerp(paint * wingShade, Color.white, whiten * 0.85f);
            wing.a = 1f;
            foreach (var r in wingRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, wing);
                block.SetFloat(PaintGlowId, shine);
                block.SetFloat(RainbowId, rainbow ? 1f : 0f);
                r.SetPropertyBlock(block);
            }
        }

        private Color DropColour() => rainbow ? Color.HSVToRGB(UnityEngine.Random.value, 0.9f, 1f) : colour;

        // A drop forms at a spot under its bottom; the drops' system makes it hang, then fall.
        private void Drip(float dt)
        {
            if (drips == null || dripsPerSecond <= 0f) return;
            nextDrip -= dt;
            if (nextDrip > 0f) return;
            nextDrip = UnityEngine.Random.Range(0.5f, 1.5f) / dripsPerSecond;
            float r = Mathf.Sqrt(UnityEngine.Random.value) * dripSpread;
            float a = UnityEngine.Random.value * Mathf.PI * 2f;
            float rim = Mathf.Max(0.01f, bodyBounds.extents.x);
            float y = bodyBounds.min.y * Mathf.Sqrt(Mathf.Max(0f, 1f - (r / rim) * (r / rim)));
            float scale = Mathf.Abs(transform.lossyScale.x);
            float size = UnityEngine.Random.Range(dripSize.x, dripSize.y) * scale;
            var at = (model != null ? model : transform).TransformPoint(new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)));
            var emit = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = at + Vector3.down * size * 0.3f,
                velocity = Vector3.zero,
                startSize = size,
                startColor = DropColour(),
            };
            drips.Emit(emit, 1);
        }

        private void ShakeOffDrops(int count, Vector3 away)
        {
            if (popBlobs == null || count <= 0) return;
            float scale = Mathf.Abs(transform.lossyScale.x);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var dir = (away + UnityEngine.Random.insideUnitSphere * 0.8f + Vector3.up * 0.4f).normalized;
                emit.position = transform.position + dir * 0.11f * scale;
                emit.velocity = dir * UnityEngine.Random.Range(0.7f, 1.6f);
                emit.startSize = UnityEngine.Random.Range(0.014f, 0.026f) * scale;
                emit.startColor = DropColour();
                popBlobs.Emit(emit, 1);
            }
        }

        // The floor under it, for the splats (checked now and then, kept under it every frame).
        private void UpdateFloor()
        {
            if (floor == null) return;
            var position = transform.position;
            if (Time.time >= nextFloorCheck)
            {
                nextFloorCheck = Time.time + 0.25f;
                floorY = position.y - 3f;
                var hits = Physics.RaycastAll(position, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                foreach (var hit in hits)
                {
                    if (hit.normal.y < 0.7f || hit.transform.IsChildOf(transform)) continue;
                    if (hit.distance < best)
                    {
                        best = hit.distance;
                        floorY = hit.point.y + 0.003f;
                    }
                }
            }
            floor.SetPositionAndRotation(new Vector3(position.x, floorY, position.z), Quaternion.identity);
        }

        private void UpdatePop()
        {
            float t = (Time.time - popStarted) / Mathf.Max(0.01f, popSeconds);
            if (t < 1f && squash != null)
            {
                // Squashed flat first (40%), then swelling round and big, shaking, going pale.
                Vector3 shape;
                if (t < 0.4f) shape = Vector3.Lerp(Vector3.one, new Vector3(1.22f, 0.72f, 1.22f), t / 0.4f);
                else shape = Vector3.Lerp(new Vector3(1.22f, 0.72f, 1.22f), Vector3.one * popSwell, (t - 0.4f) / 0.6f);
                squash.localRotation = Quaternion.identity;
                squash.localScale = shape;
                squash.localPosition = UnityEngine.Random.insideUnitSphere * 0.006f;
                if (turn != null) turn.localRotation = Quaternion.identity;
                Paint(t * 0.5f);
                return;
            }
            Burst();
        }

        private void Burst()
        {
            var centre = transform.position;
            float scale = Mathf.Abs(transform.lossyScale.x);
            var paint = PaintNow();
            if (squash != null) squash.gameObject.SetActive(false);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            if (popBlobs != null)
            {
                int total = popBigBlobs + popDroplets;
                for (int i = 0; i < total; i++)
                {
                    bool big = i < popBigBlobs;
                    var dir = Vector3.Slerp(UnityEngine.Random.onUnitSphere, popPush, popPush == Vector3.zero ? 0f : 0.35f).normalized;
                    emit.position = centre + dir * 0.1f * scale;
                    emit.velocity = dir * (big ? UnityEngine.Random.Range(0.9f, 2.2f) : UnityEngine.Random.Range(1.8f, 3.6f)) + Vector3.up * 0.7f;
                    emit.startSize = (big ? UnityEngine.Random.Range(0.035f, 0.065f) : UnityEngine.Random.Range(0.012f, 0.022f)) * scale;
                    emit.startColor = rainbow ? DropColour() : Color.Lerp(paint, paint * 0.8f, UnityEngine.Random.value);
                    popBlobs.Emit(emit, 1);
                }
            }
            if (popShell != null)
            {
                emit.position = centre;
                emit.velocity = Vector3.zero;
                emit.startSize = popShellSize * scale;
                emit.startColor = paint;
                popShell.Emit(emit, 1);
            }
            if (popSounds.Length > 0)
            {
                var clip = popSounds[UnityEngine.Random.Range(0, popSounds.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, centre, popVolume);
            }
            Popped?.Invoke(centre, paint);
            ReleaseDrops();
            enabled = false;
            if (destroyOnPop) Destroy(gameObject);
        }
    }
}
