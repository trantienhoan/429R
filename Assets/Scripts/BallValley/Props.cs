using UnityEngine;
using ValleyGame = Balls.BallValleyStage;   // 429R: the gate stage took BALLS' ValleyGame's place

namespace Balls
{
    /// <summary>
    /// A tree or boulder on the field. It has hit points: crashing into it hurts it (and jiggles it like jelly), and
    /// when they run out it breaks into a burst of berries. The colliders stay on the root, the model wobbles on a
    /// child pivot, so a jiggle never shoves balls around. Broken ones come back when the next match starts, and a
    /// replay moves them all somewhere new; either way they wait, hidden, until the growing field reaches them, then
    /// pop up out of the grass. The host decides all that; a network client only shows it (429R: no network copies).
    /// </summary>
    public class FieldProp : MonoBehaviour
    {
        /// <summary>Place in ValleyGame.props (the same on every computer: the field is built from one seed).</summary>
        public int index;
        public bool tree;
        public float hp, maxHp;
        /// <summary>The crash that shakes it fully (bigger = stiffer). Apart from the hit points, so tougher props still wobble.</summary>
        public float shakeHp = 12f;
        public int food;
        public Color chipColor;
        public PropJiggle jiggle;
        /// <summary>Where it stands (the border and the holes come to it as the valley shrinks) and how big it is.</summary>
        public Vector3 basePos, baseScale;
        /// <summary>Roughly how far it reaches from its centre (on the ground), at full size.</summary>
        public float baseRadius;
        /// <summary>When it last lost hit points (its health bar shows for a few seconds after).</summary>
        [System.NonSerialized] public float lastHit = -99f;
        /// <summary>
        /// 429R gate 3 (user 2026-11-23: "candy tree drop candy or fly candy out when it gets hit"): candies its hits still
        /// knock out of it (one a hit, at most every Hit Candy Every seconds); breaking it still bursts its Food.
        /// </summary>
        [System.NonSerialized] public int hitCandiesLeft;
        [System.NonSerialized] public float hitCandyEvery = 0.35f;
        /// <summary>429R gate 3: told of every hit (its damage, where), e.g. a jello volcano spits candy.</summary>
        [System.NonSerialized] public System.Action<float, Vector3> onHit;
        float nextHitCandy;

        public bool Broken { get; private set; }
        /// <summary>Hidden until the field reaches it (a new match: the valley grows back, or it was moved), see <see cref="Arrive"/>.</summary>
        public bool Waiting { get; private set; }
        public float Health01 => maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;

        /// <summary>Host: a ball crashed into it (<paramref name="dir"/>: the way the ball was going).</summary>
        public void Hit(float dmg, Vector3 point, Vector3 dir, BallPlayer by)
        {
            if (Broken || dmg <= 0f) return;
            hp = Mathf.Max(0f, hp - dmg);
            lastHit = Time.time;
            if (jiggle) jiggle.Hit(Mathf.Clamp(dmg / Mathf.Max(1f, shakeHp), 0.35f, 1.25f), dir);
            Gfx.Sparks(point, chipColor, Mathf.Clamp(Mathf.RoundToInt(dmg * 0.6f), 3, 14), 6f);
            if (hitCandiesLeft > 0 && dmg >= 2f && Time.time >= nextHitCandy) KnockCandyOut(dir);   // 429R gate 3
            onHit?.Invoke(dmg, point);   // 429R gate 3
            if (hp <= 0f) Break(true, by);
        }

        // 429R gate 3: one candy out of its top, flying on the way the hit went (a little to either side).
        void KnockCandyOut(Vector3 dir)
        {
            var g = ValleyGame.I;
            if (!g) return;
            hitCandiesLeft--;
            nextHitCandy = Time.time + hitCandyEvery;
            var away = new Vector3(dir.x, 0f, dir.z);
            if (away.sqrMagnitude < 1e-4f) away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
            away = Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * away.normalized;
            float size = transform.localScale.x;
            var from = transform.position + Vector3.up * ((tree ? 3.4f : 1.2f) * size) + away * 0.6f;
            g.SpawnPellet(from, away * Random.Range(2.5f, 5f) + Vector3.up * Random.Range(4f, 6.5f), g.RandomBerryColor());
        }

        /// <summary>Breaks it: a crunch, a cloud of chips and (if <paramref name="dropFood"/>) a burst of berries.</summary>
        public void Break(bool dropFood, BallPlayer by = null)
        {
            if (Broken) return;
            Broken = true;
            hp = 0f;
            Vector3 c = transform.position;
            float size = transform.localScale.x;
            Gfx.Puff(c + Vector3.up * (1.2f * size), chipColor, 22, 2.8f * size, 5f);
            Gfx.Sparks(c + Vector3.up * size, Color.Lerp(chipColor, Color.white, 0.4f), 24, 9f);
            if (tree) Gfx.Puff(c + Vector3.up * (3f * size), new Color(0.2f, 0.45f, 0.18f, 0.9f), 16, 2.2f * size, 4f);   // leaves
            Sfx.Play(tree ? Sfx.Sound.TreeBreak : Sfx.Sound.RockBreak, c, 1f);
            if (by) ValleyGame.I.Shake(by, 0.35f);
            var g = ValleyGame.I;
            if (dropFood && g)
                for (int i = 0; i < food; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    Vector3 v = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(3f, 9f) + Vector3.up * Random.Range(6f, 11f);
                    g.SpawnPellet(new Vector3(c.x, 1.2f, c.z), v, g.RandomBerryColor());
                }
            gameObject.SetActive(false);
        }

        /// <summary>Host: back as new (a new match). It waits, hidden, until the field reaches it (<see cref="Arrive"/>).</summary>
        public void Restore()
        {
            Broken = false;
            hp = maxHp;
            lastHit = -99f;
            Wait();
        }

        /// <summary>
        /// Stands somewhere new (a replay moves all the trees and boulders; every computer does the same from one seed).
        /// It waits, hidden, until the field reaches it.
        /// </summary>
        public void MoveTo(Vector3 pos, float yaw)
        {
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            basePos = pos;
            Wait();
        }

        void Wait()
        {
            Waiting = true;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>Host: the field has reached it. Unless it's broken, it pops up out of the grass.</summary>
        public void Arrive()
        {
            if (!Waiting) return;
            Waiting = false;
            if (!Broken) PopUp();
        }

        void PopUp()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (jiggle) jiggle.PopUp();
            float size = transform.localScale.x;
            Gfx.Puff(transform.position + Vector3.up * 0.3f, tree ? new Color(0.4f, 0.75f, 0.3f, 0.8f) : new Color(0.5f, 0.42f, 0.32f, 0.8f),
                8, 1.6f * size, 3f, false);
        }

        // 429R: no network copies (BALLS' ProxyHp).
    }

    /// <summary>
    /// The jelly jiggle from 429R (VFX/Jiggle.cs): a hit squashes it flat, it springs back up taller and rocks away from
    /// the hit, wobbling a few times before it settles. Quick hits add up (to a limit); soft ones wait until it has settled.
    /// Moves only its own scale and rotation (rest = no scale, no turn), around its pivot at the base.
    /// </summary>
    public class PropJiggle : MonoBehaviour
    {
        [Range(0f, 0.6f)] public float squash = 0.18f;
        [Range(0f, 45f)] public float tilt = 8f;
        [Range(1f, 12f)] public float wobblesPerSecond = 4.5f;
        [Range(0.2f, 3f)] public float settleTime = 0.9f;
        public float softHitPause = 0.75f;

        const float TiltSpeed = 0.8f, MaxSquash = 0.6f, MaxStep = 1f / 240f, MostHits = 1.25f;

        const float GrowSeconds = 0.45f;

        bool moving;
        float squashAmount, squashSpeed, nextSoftHit;
        Vector3 tiltAmount, tiltSpeed;
        float grow = 1f;   // 0..1 while it pops up out of the grass

        /// <summary>Pops up out of the grass: grows from nothing past its size and back, stretching up and wobbling.</summary>
        public void PopUp()
        {
            grow = 0f;
            float speed = 2f * Mathf.PI * wobblesPerSecond;
            squashAmount = 0f;
            squashSpeed = squash * speed * 0.9f;   // shoots up tall, then wobbles down
            tiltAmount = tiltSpeed = Vector3.zero;
            moving = true;
            transform.localScale = Vector3.zero;
        }

        /// <summary>Strength 1 = a full jiggle; <paramref name="away"/>: the direction it rocks toward.</summary>
        public void Hit(float strength, Vector3 away)
        {
            if (strength <= 0f || !isActiveAndEnabled) return;
            bool soft = strength < 1f;
            if (soft && (moving || Time.time < nextSoftHit)) return;
            if (soft) nextSoftHit = Time.time + softHitPause;

            float speed = 2f * Mathf.PI * wobblesPerSecond;
            float mostSquash = squash * speed * MostHits;
            squashSpeed = Mathf.Clamp(squashSpeed - squash * strength * speed, -mostSquash, mostSquash);
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
            Vector3 axis = Vector3.Cross(Vector3.up, away).normalized;   // turning round this tips its top away
            tiltSpeed = Vector3.ClampMagnitude(tiltSpeed + axis * (tilt * strength * speed * TiltSpeed), tilt * speed * TiltSpeed * MostHits);
            moving = true;
        }

        void OnDisable()
        {
            if (moving) Settle();
        }

        void LateUpdate()
        {
            if (!moving) return;
            float squashOmega = 2f * Mathf.PI * wobblesPerSecond;
            float tiltOmega = squashOmega * TiltSpeed;
            float squashDamping = 2f * Mathf.Min(1f, 3f / (squashOmega * settleTime)) * squashOmega;
            float tiltDamping = 2f * Mathf.Min(1f, 3f / (tiltOmega * settleTime)) * tiltOmega;

            float left = Mathf.Min(Time.deltaTime, 0.1f);
            while (left > 0f)
            {
                float dt = Mathf.Min(left, MaxStep);
                left -= dt;
                squashSpeed += (-squashOmega * squashOmega * squashAmount - squashDamping * squashSpeed) * dt;
                squashAmount = Mathf.Clamp(squashAmount + squashSpeed * dt, -MaxSquash, MaxSquash);
                tiltSpeed += (-tiltOmega * tiltOmega * tiltAmount - tiltDamping * tiltSpeed) * dt;
                tiltAmount += tiltSpeed * dt;
            }
            tiltAmount = Vector3.ClampMagnitude(tiltAmount, 60f);
            if (grow < 1f) grow = Mathf.Min(1f, grow + Time.deltaTime / GrowSeconds);

            if (grow >= 1f && Mathf.Abs(squashAmount) < 0.001f && Mathf.Abs(squashSpeed) < 0.01f &&
                tiltAmount.sqrMagnitude < 0.01f && tiltSpeed.sqrMagnitude < 0.25f)
            {
                Settle();
                return;
            }

            float height = 1f + squashAmount;   // taller is thinner, flatter is wider: it keeps its volume
            float width = 1f / Mathf.Sqrt(height);
            float g = grow >= 1f ? 1f : EaseOutBack(grow);
            transform.localScale = new Vector3(width, height, width) * g;
            var parentRotation = transform.parent ? transform.parent.rotation : Quaternion.identity;
            float angle = tiltAmount.magnitude;
            var rock = angle > 0.0001f ? Quaternion.AngleAxis(angle, tiltAmount / angle) : Quaternion.identity;
            transform.localRotation = Quaternion.Inverse(parentRotation) * rock * parentRotation;
        }

        /// <summary>0 to 1, overshooting a little on the way (a pop).</summary>
        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float a = x - 1f;
            return 1f + c3 * a * a * a + c1 * a * a;
        }

        void Settle()
        {
            moving = false;
            grow = 1f;
            squashAmount = squashSpeed = 0f;
            tiltAmount = tiltSpeed = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }
    }
}
