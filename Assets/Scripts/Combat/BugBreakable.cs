using System.Collections.Generic;
using Game.Enemies;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Bugs break it (user, 2026-10-30: "make those block be easy to break by those bugs, like 2 or 3 hit from a bug
    /// should break it, the metal is not breakable"): every bug that bumps into it (walking into it, knocked into it,
    /// charging) is a hit, and one touching it (pushing against it) hits again every Hit Every seconds; touching is also
    /// looked for every Scan Every seconds, because a heavy block at rest stops reporting a bug leaning on it. Each hit takes its health over
    /// Hits To Break (rounded up), so Hits To Break hits break it, through its own Health FSM (its break, drops and smoke
    /// as usual). The player's hits are left to its Enemy Hit Damage as before. Goes on the wooden Jenga blocks
    /// (Jenga_Block.003, .004, .005; not the brass ones): Tools > 429 Game > Combat > Bugs Break Wooden Jenga Blocks adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class BugBreakable : MonoBehaviour
    {
        [Tooltip("This many bug hits break it.")]
        [Min(1)]
        [SerializeField] private int hitsToBreak = 3;
        [Tooltip("A bug pushing against it hits it again this often, in seconds.")]
        [Min(0.1f)]
        [SerializeField] private float hitEvery = 0.8f;
        [Tooltip("A small puff where a bug hits it, e.g. Smoke_Flash_Small_Puff. Empty: none.")]
        [SerializeField] private GameObject hitEffect;
        [Min(0.01f)]
        [SerializeField] private float hitEffectSize = 0.3f;
        [Tooltip("How close a bug counts as touching it, in metres past its box.")]
        [Min(0f)]
        [SerializeField] private float touchMargin = 0.08f;
        [Min(0.05f)]
        [SerializeField] private float scanEvery = 0.2f;
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        private static readonly Collider[] Near = new Collider[16];
        private readonly Dictionary<SpiderBrain, float> nextHitBy = new();
        private FsmFloat health;
        private float perHit;
        private BoxCollider box;
        private float nextScan;

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                break;
            }
            if (health != null) perHit = Mathf.Ceil(health.Value / hitsToBreak);
            TryGetComponent(out box);
        }

        private void OnCollisionEnter(Collision collision) => Bumped(collision);

        private void OnCollisionStay(Collision collision) => Bumped(collision);

        // Bugs touching it, also while it rests (no collision is reported then).
        private void FixedUpdate()
        {
            if (box == null || Time.time < nextScan || health == null || health.Value <= 0f) return;
            nextScan = Time.time + scanEvery;
            var t = box.transform;
            var middle = t.TransformPoint(box.center);
            var half = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z))) +
                       Vector3.one * touchMargin;
            int count = Physics.OverlapBoxNonAlloc(middle, half, Near, t.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = Near[i];
                if (other == null || other.transform.IsChildOf(transform)) continue;
                Hit(BugOf(other.attachedRigidbody, other), other.ClosestPoint(middle));
            }
        }

        private void Bumped(Collision collision)
        {
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Hit(BugOf(collision.rigidbody, collision.collider), at);
        }

        private static SpiderBrain BugOf(Rigidbody body, Collider part) =>
            body != null ? body.GetComponentInParent<SpiderBrain>() : part.GetComponentInParent<SpiderBrain>();

        // A bug's hit: its share of the health, once per Hit Every for each bug.
        private void Hit(SpiderBrain bug, Vector3 at)
        {
            if (bug == null || health == null || health.Value <= 0f || perHit <= 0f) return;
            if (bug.TryGetComponent(out EnemyHitDamage bugHits) && bugHits.IsDead) return;
            if (nextHitBy.TryGetValue(bug, out float next) && Time.time < next) return;
            nextHitBy[bug] = Time.time + hitEvery;

            health.Value = Mathf.Max(0f, health.Value - perHit);
            if (hitEffect != null) EffectPool.Play(hitEffect, at, hitEffectSize);
        }
    }
}
