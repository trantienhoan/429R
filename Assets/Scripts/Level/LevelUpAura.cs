using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Leveling
{
    /// <summary>
    /// The level-up aura: at every level up a burst of light grows out from the player's feet, and its edge pushes away
    /// what it reaches. Enemies are knocked back, ghost balloons blown off, loose things thrown up and away (heavy ones
    /// less, the heaviest not at all: Push Weight). Things in the player's hands, and the player, stay put. The higher
    /// the level, the bigger the aura and the harder it pushes (each "x, y" below: x at level 1, y at the top level).
    /// Its look is the particle systems under it (Effect), made for a 1 m aura and scaled to the reach; the push grows
    /// out over Grow Seconds, the same time the effect's ring takes. Tools > 429 Game > Player > Set Up Level System makes it.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelUpAura : MonoBehaviour
    {
        [Tooltip("The aura's look, made for a 1 m aura: played at each level up at the player's feet, scaled to the reach, " +
                 "and following the player while it plays.")]
        [SerializeField] private ParticleSystem effect;
        [Tooltip("How far the aura reaches, in metres: x at level 1, y at the top level.")]
        [SerializeField] private Vector2 radius = new(1.5f, 4f);
        [Tooltip("How fast it sends loose things away, in metres per second: x at level 1, y at the top level. Heavy " +
                 "things go slower.")]
        [SerializeField] private Vector2 pushSpeed = new(3f, 9f);
        [Tooltip("How far it knocks enemies back, compared with a big hit: x at level 1, y at the top level.")]
        [SerializeField] private Vector2 enemyKnockBack = new(0.8f, 2.5f);
        [Tooltip("Seconds the push takes to grow out to its full reach (the effect's ring takes 0.5 s).")]
        [Min(0.05f)]
        [SerializeField] private float growSeconds = 0.5f;
        [Tooltip("How much of the push on loose things goes upwards, so they tumble (0 = none).")]
        [Range(0f, 1f)]
        [SerializeField] private float lift = 0.35f;
        [Tooltip("Optional sound of the burst (the level-up sound plays anyway).")]
        [SerializeField] private AudioClip burstSound;

        // How high the push reaches above the feet, in metres: the player's height and a bit.
        private const float Height = 2.2f;

        private readonly HashSet<Object> pushed = new();
        private readonly Collider[] hits = new Collider[256];
        private XROrigin origin;
        private float started = -1f;
        private int burstFrame = -1;
        private float reach;
        private float speed;
        private float knock;

        private void OnEnable()
        {
            PlayerLevel.LeveledUp += Burst;
        }

        private void OnDisable()
        {
            PlayerLevel.LeveledUp -= Burst;
            started = -1f;
        }

        /// <summary>Bursts out around the player as big and as strong as for reaching this level.</summary>
        public void Burst(int level)
        {
            var levels = PlayerLevel.Instance;
            int top = levels != null ? Mathf.Max(1, levels.MaxLevel) : 10;
            BurstShare(top > 1 ? Mathf.InverseLerp(1f, top, level) : 1f);
        }

        /// <summary>
        /// Bursts out around the player, as big and as strong as Share says: 0 = as at level 1, 1 = as at the top level.
        /// E.g. Player Revive's wake-up aura.
        /// </summary>
        public void BurstShare(float share)
        {
            float t = Mathf.Clamp01(share);
            float size = Mathf.Lerp(radius.x, radius.y, t);
            // Several levels at once: one burst, the biggest.
            if (burstFrame == Time.frameCount && size <= reach) return;
            burstFrame = Time.frameCount;

            reach = Mathf.Max(0.1f, size);
            speed = Mathf.Lerp(pushSpeed.x, pushSpeed.y, t);
            knock = Mathf.Lerp(enemyKnockBack.x, enemyKnockBack.y, t);
            if (!FindPlayer()) return;

            started = Time.time;
            pushed.Clear();
            transform.SetPositionAndRotation(Feet(), Quaternion.identity);
            if (effect != null)
            {
                effect.transform.localScale = Vector3.one * reach;
                effect.Clear(true);
                effect.Play(true);
            }
            if (burstSound != null) AudioSource.PlayClipAtPoint(burstSound, transform.position);
        }

        private void LateUpdate()
        {
            // The light stays around the player while it plays.
            if (origin != null && (started >= 0f || (effect != null && effect.IsAlive(true))))
                transform.position = Feet();
        }

        // The push grows out from the player, and whatever its edge reaches is pushed, once.
        private void FixedUpdate()
        {
            if (started < 0f || origin == null) return;

            float grown = Mathf.Clamp01((Time.time - started) / growSeconds);
            var feet = Feet();
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * 0.2f, feet + Vector3.up * Height, Mathf.Max(0.05f, reach * grown),
                hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) Push(hits[i], feet);
            if (grown >= 1f) started = -1f;
        }

        private void Push(Collider hit, Vector3 feet)
        {
            if (hit == null || hit.transform.IsChildOf(origin.transform)) return;

            // Enemies walk by themselves: they're knocked back, away from the player.
            var enemy = hit.GetComponentInParent<EnemyHitDamage>();
            // Furniture with a health bar is still a loose thing: it's pushed below like the rest.
            if (enemy != null && !enemy.IsFurniture)
            {
                if (pushed.Add(enemy)) enemy.KnockBack(knock, 1f);
                return;
            }
            var balloon = hit.GetComponentInParent<GhostBalloon>();
            if (balloon != null)
            {
                if (pushed.Add(balloon)) balloon.Punched(speed, feet, Away(balloon.transform.position, feet));
                return;
            }

            var body = hit.attachedRigidbody;
            if (body == null || body.isKinematic || !pushed.Add(body) || Fist.IsFistBall(body)) return;
            // Things in someone's hand stay there.
            if (body.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected) return;
            float share = PushWeight.Share(body);
            if (share <= 0f) return;

            var direction = (Away(body.worldCenterOfMass, feet) + Vector3.up * lift).normalized;
            // Up to that speed away, not on top of how fast it's already going that way.
            float wanted = speed * share;
            float already = Vector3.Dot(body.linearVelocity, direction);
            if (already < wanted) body.AddForce(direction * (wanted - already), ForceMode.VelocityChange);
        }

        // Along the floor, from the player to the point (or the way the player looks, right under them).
        private Vector3 Away(Vector3 point, Vector3 feet)
        {
            var away = point - feet;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) return away.normalized;
            var forward = origin.Camera != null ? origin.Camera.transform.forward : origin.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        // Under the player's head, at the floor of the play area.
        private Vector3 Feet()
        {
            var head = origin.Camera != null ? origin.Camera.transform.position : origin.transform.position;
            return new Vector3(head.x, origin.transform.position.y, head.z);
        }

        private bool FindPlayer()
        {
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            return origin != null;
        }
    }
}
