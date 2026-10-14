using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Weapons
{
    /// <summary>
    /// Keeps a bullet from hitting the gun that fired it and the player. Bullets appear at the muzzle, often already
    /// touching the gun, and some are far heavier than it (the Elon bullet weighs 4,200 to the gun's 500): the push
    /// knocked the gun out of place in the hand when firing. As it appears, it stops colliding with every held thing and
    /// every part of the player around it. Goes on the bullet prefab; Tools > 429 Game > Weapons > Stop Bullets Hitting
    /// Their Gun adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class IgnoreShooter : MonoBehaviour
    {
        [Tooltip("How far around the bullet, in metres past its own size, the gun and the player are looked for.")]
        [Min(0f)]
        [SerializeField] private float searchRadius = 0.6f;

        private static readonly Collider[] nearby = new Collider[64];

        // In Awake, which runs as the bullet is created, before it can touch anything.
        private void Awake()
        {
            var own = GetComponentsInChildren<Collider>(true);
            if (own.Length == 0) return;

            var bounds = own[0].bounds;
            foreach (var c in own) bounds.Encapsulate(c.bounds);
            int count = Physics.OverlapSphereNonAlloc(bounds.center, bounds.extents.magnitude + searchRadius, nearby, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = nearby[i];
                if (other == null || other.transform.IsChildOf(transform) || !IsShooterOrPlayer(other)) continue;
                foreach (var mine in own) Physics.IgnoreCollision(mine, other);
            }
        }

        private static bool IsShooterOrPlayer(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body != null && (Fist.IsFistBall(body) || (body.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected))) return true;
            return other.GetComponentInParent<XROrigin>() != null;
        }
    }
}
