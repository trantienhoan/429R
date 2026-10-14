using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.PlayGround
{
    /// <summary>
    /// Lets this grabbable be picked up only by a hand that's right at it: no grabbing it from afar with the ray.
    /// The hand's attach point has to be within Reach (metres) of the object's colliders. Goes next to the
    /// XR Grab Interactable (e.g. the laughing pumpkins).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class NearGrabOnly : MonoBehaviour, IXRSelectFilter
    {
        [Tooltip("How far from the object's surface the hand may be, in metres.")]
        [Min(0f)]
        [SerializeField] private float reach = 0.25f;

        private XRGrabInteractable grab;
        private Collider[] shapes;

        public bool canProcess => isActiveAndEnabled;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            shapes = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable() => grab.selectFilters.Add(this);
        private void OnDisable() => grab.selectFilters.Remove(this);

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            var hand = interactor.GetAttachTransform(interactable);
            if (hand == null) return true;
            var point = hand.position;
            foreach (var shape in shapes)
            {
                if (shape == null || !shape.enabled || shape.isTrigger) continue;
                var closest = shape is MeshCollider { convex: false } ? shape.bounds.ClosestPoint(point) : shape.ClosestPoint(point);
                if ((closest - point).sqrMagnitude <= reach * reach) return true;
            }
            return false;
        }
    }
}
