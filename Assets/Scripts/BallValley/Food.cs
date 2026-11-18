using UnityEngine;
using ValleyGame = Balls.BallValleyStage;   // 429R: the gate stage took BALLS' ValleyGame's place

namespace Balls
{
    /// <summary>
    /// Small glowing berry. Spawns around the valley and bursts out of smashed objects.
    /// 429R (user, 2026-11-18: "change all the berry on the field to random candy"): with the stage's Candy Prefabs set,
    /// every berry is a real 429R candy instead (<see cref="SetupCandy"/>): it hovers and turns where a berry would, balls
    /// eat it by rolling over it like a berry (it touches nothing while it lies there), and the player picks it up and eats
    /// it like any candy: grabbed, it stops being field food and is a plain candy again (it falls when let go).
    /// </summary>
    public class Pellet : MonoBehaviour
    {
        public int value = 1;
        public bool edible = true;
        // 429R: no netId (no network)
        public Color color;

        Vector3 vel;
        bool flying;
        Transform vis;
        float phase;
        // 429R: a candy: its own body (held still, touching nothing, while it lies on the field), its hover height, its
        // contacts as they were (given back when it's picked up).
        Rigidbody candyBody;
        float restY;
        LayerMask candyExcluded;
        bool candy, taken;
        /// <summary>429R: where a candy hovers over the lawn (m): high enough to grab without kneeling.</summary>
        public const float CandyHover = 0.7f;

        public bool Flying => flying;

        /// <summary>429R: a real candy now, not field food (it was picked up).</summary>
        public bool Taken => taken;

        public void Setup(Transform visual, Vector3 velocity)
        {
            vis = visual;
            vel = velocity;
            flying = velocity.sqrMagnitude > 0.01f;
            phase = Random.value * 10f;
        }

        /// <summary>429R: this pellet is the candy it sits on (see the class summary).</summary>
        public void SetupCandy(Vector3 velocity)
        {
            candy = true;
            vis = null;
            vel = velocity;
            flying = velocity.sqrMagnitude > 0.01f;
            phase = Random.value * 10f;
            restY = Mathf.Max(transform.position.y, CandyHover);
            candyBody = GetComponent<Rigidbody>();
            if (candyBody)
            {
                candyExcluded = candyBody.excludeLayers;
                candyBody.isKinematic = true;
                candyBody.useGravity = false;
                candyBody.excludeLayers = ~0;   // rolling balls pass through it (they eat it), as through a berry
            }
            var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab)
            {
                grab.selectEntered.AddListener(_ => Take());
                // Let go: a plain candy that falls (the grab puts back the still body it found when it was taken).
                grab.selectExited.AddListener(_ =>
                {
                    if (!candyBody) return;
                    candyBody.isKinematic = false;
                    candyBody.useGravity = true;
                });
            }
        }

        // 429R: gone however it went (a candy can be taken away by the game, e.g. the candy limit popping the oldest; play
        // test 2026-11-18: the bots' food search then hit a destroyed one): off the list.
        void OnDestroy()
        {
            if (ValleyGame.I) ValleyGame.I.pellets.Remove(this);
        }

        /// <summary>429R: picked up: no longer field food, its contacts back (the mouth eats it, it lands when dropped).</summary>
        void Take()
        {
            if (taken) return;
            taken = true;
            edible = false;
            flying = false;
            if (ValleyGame.I) ValleyGame.I.pellets.Remove(this);
            if (candyBody) candyBody.excludeLayers = candyExcluded;
        }

        void Update()
        {
            if (taken) return;   // 429R: a plain candy now
            float dt = Time.deltaTime;
            if (candy)
            {
                // 429R: like a berry, but the candy itself moves: it flies (out of something smashed), then hovers and turns.
                phase += dt;
                if (flying) Fly(dt, restY);
                else transform.position = new Vector3(transform.position.x, restY + Mathf.Sin(phase * 3f) * 0.08f, transform.position.z);
                transform.rotation = Quaternion.Euler(0f, phase * 90f, 0f);
                return;
            }
            if (flying)
            {
                var valley = ValleyGame.I.valley;
                Vector3 p = transform.position + vel * dt;
                vel.y -= 30f * dt;
                float lx = valley.HalfX - 1.5f, lz = valley.HalfZ - 1.5f;
                if (Mathf.Abs(p.x) > lx) { p.x = Mathf.Sign(p.x) * lx; vel.x = -vel.x * 0.4f; }
                if (Mathf.Abs(p.z) > lz) { p.z = Mathf.Sign(p.z) * lz; vel.z = -vel.z * 0.4f; }
                if (p.y < 0.4f)
                {
                    p.y = 0.4f;
                    if (vel.y < 0f) vel.y = -vel.y * 0.35f;
                    vel.x *= 0.75f;
                    vel.z *= 0.75f;
                    if (vel.sqrMagnitude < 1.5f) flying = false;
                }
                transform.position = p;
            }
            if (vis)
            {
                phase += dt;
                vis.localPosition = new Vector3(0f, Mathf.Sin(phase * 3f) * 0.08f, 0f);
                vis.localRotation = Quaternion.Euler(0f, phase * 90f, 0f);
            }
        }

        /// <summary>429R: a candy flying out of something smashed: as a berry flies, landing at its hover height.</summary>
        void Fly(float dt, float landY)
        {
            var valley = ValleyGame.I.valley;
            Vector3 p = transform.position + vel * dt;
            vel.y -= 30f * dt;
            float lx = valley.HalfX - 1.5f, lz = valley.HalfZ - 1.5f;
            if (Mathf.Abs(p.x) > lx) { p.x = Mathf.Sign(p.x) * lx; vel.x = -vel.x * 0.4f; }
            if (Mathf.Abs(p.z) > lz) { p.z = Mathf.Sign(p.z) * lz; vel.z = -vel.z * 0.4f; }
            if (p.y < landY)
            {
                p.y = landY;
                if (vel.y < 0f) vel.y = -vel.y * 0.35f;
                vel.x *= 0.75f;
                vel.z *= 0.75f;
                if (vel.sqrMagnitude < 1.5f) flying = false;
            }
            transform.position = p;
        }

        public void Eat()
        {
            if (!edible) return;
            edible = false;
            Gfx.Sparks(transform.position, new Color(1f, 0.95f, 0.6f), 5, 3f);
            ValleyGame.I.pellets.Remove(this);
            Destroy(gameObject);
        }
    }

    /// <summary>A baby ball knocked loose (or freed when its owner got pocketed): a white ball. Rolls around; anyone can eat it.</summary>
    public class LooseBall : MonoBehaviour
    {
        public int value = 3;
        // 429R: no netId (no network)
        public Color color;
        public BallPlayer from;
        public float eatableAt, ownerEatableAt, dieAt;
        public Rigidbody rb;
        public Vector3 prevVel, prevPos;   // before this physics step (a special-move hit works out where they met)
        public int physId;
        bool gone;
        bool knocked;
        float burstUntil;
        // Went over a hole: drops out of sight (still sent to clients, so they see it fall), then it's gone.
        bool falling;
        float fallEnd;

        /// <summary>
        /// Hit by a special move: it carries the shot on until <paramref name="burstEnd"/> (the striker's burst), then
        /// brakes like any struck ball (see BallPlayer: the same cloth for every ball).
        /// </summary>
        public void Knock(float burstEnd)
        {
            knocked = true;
            burstUntil = burstEnd;
        }

        public bool CanEat(BallPlayer p) => !gone && !falling && Time.time >= (p == from ? ownerEatableAt : eatableAt);

        public void Expire() => dieAt = Mathf.Min(dieAt, Time.time);

        void OnDestroy() => BallPhysics.Unregister(physId);

        void FixedUpdate()
        {
            if (gone) return;
            var tune = ValleyGame.I.tune;
            if (falling)
            {
                rb.AddForce(Physics.gravity * tune.extraGravity, ForceMode.Acceleration);
                if (Time.time > fallEnd) Remove();
                return;
            }
            // Rolled over a cave lip (real physics: it tips in once its centre is past the edge) or off the crumbling
            // border, and dropped: it keeps falling out of sight, then it's gone.
            if (rb.position.y < -3f)
            {
                falling = true;
                fallEnd = Time.time + Falling.HoleSeconds;
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
                rb.linearDamping = 0f;
                return;
            }
            prevVel = rb.linearVelocity;
            prevPos = rb.position;
            if (prevVel.y > 3f) rb.linearVelocity = new Vector3(prevVel.x, 3f, prevVel.z);   // stays on the table
            if (knocked)
            {
                Vector3 v = rb.linearVelocity, flat = new Vector3(v.x, 0f, v.z);
                if (Time.time < burstUntil) rb.linearDamping = tune.dashDamping;
                else
                {
                    rb.linearDamping = Mathf.Max(tune.dampingSmall, tune.shotDamping);
                    flat = Vector3.MoveTowards(flat, Vector3.zero, tune.shotFriction * Time.fixedDeltaTime);
                    rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
                }
                if (Time.time > burstUntil && flat.magnitude < tune.shotRecoverSpeed) { knocked = false; rb.linearDamping = 0.7f; }
            }
            rb.AddForce(Physics.gravity * tune.extraGravity, ForceMode.Acceleration);
            if (Time.time > dieAt)
            {
                transform.localScale *= 0.92f;
                if (transform.localScale.x < 0.05f) Remove();
            }
        }

        public void Eat()
        {
            if (gone) return;
            Gfx.Sparks(transform.position, Color.white, 6, 3f);
            Remove();
        }

        void Remove()
        {
            if (gone) return;
            gone = true;
            ValleyGame.I.looseBalls.Remove(this);
            Destroy(gameObject);
        }
    }
}
