using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Combat
{
    /// <summary>
    /// Makes grabbing from a distance gentle. On its own, XRI yanks a far-grabbed object into the hand within a few
    /// hundredths of a second, so from a few metres away it flies at 50-100 m/s: whatever it brushes on the way gets
    /// smashed, and its own Damage FSM takes the bump as a huge hit (a big puff of smoke and lost health). With this,
    /// the grab starts where the object is and it flies to the hand at Pull Speed, slow enough that a bump on the way
    /// doesn't count as a big hit (weapons' Damage FSMs need 20-30 m/s). It still collides on the way: switching its
    /// collisions off would count as leaving the trigger it's in, and things that die on leaving a FIREKILL area would.
    /// Near grabs are untouched. Goes on the XR Origin; it listens to the hands' interactors, so it costs nothing while
    /// nothing is being pulled.
    /// </summary>
    [DisallowMultipleComponent]
    public class GentleFarGrab : MonoBehaviour
    {
        [Tooltip("A grab from further than this, in metres from the hand, counts as far.")]
        [Min(0.05f)]
        [SerializeField] private float farDistance = 0.4f;
        [Tooltip("How fast a far-grabbed object flies to the hand, in metres per second.")]
        [Min(0.5f)]
        [SerializeField] private float pullSpeed = 6f;
        [Tooltip("The longest a pull takes, in seconds: things further away fly faster than Pull Speed to make it.")]
        [Min(0.1f)]
        [SerializeField] private float maxPullSeconds = 0.6f;

        // Close enough to the hand to count as arrived, in metres.
        private const float Arrived = 0.02f;

        private class Pull
        {
            public XRGrabInteractable grab;
            public NearFarInteractor hand;
            public float speed;
            public float giveUpAt;
        }

        private readonly List<XRBaseInteractor> interactors = new();
        private readonly List<Pull> pulls = new();

        private void OnEnable()
        {
            GetComponentsInChildren(true, interactors);
            foreach (var interactor in interactors)
            {
                interactor.selectEntered.AddListener(OnGrabbed);
                interactor.selectExited.AddListener(OnLetGo);
            }
        }

        private void OnDisable()
        {
            foreach (var interactor in interactors)
            {
                if (interactor == null) continue;
                interactor.selectEntered.RemoveListener(OnGrabbed);
                interactor.selectExited.RemoveListener(OnLetGo);
            }
            interactors.Clear();
            for (int i = pulls.Count - 1; i >= 0; i--) End(i, true);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (args.interactorObject is not NearFarInteractor hand || hand.interactionAttachController == null) return;
            if (args.interactableObject is not XRGrabInteractable grab) return;
            float distance = HandDistance(hand.transform, grab);
            if (distance < farDistance) return;

            // XRI has just put the grab point in the hand; put it back where the object is, so the object starts from
            // where it lies, and Update brings it in.
            hand.interactionAttachController.MoveTo(grab.GetAttachTransform(hand).position);
            pulls.Add(new Pull
            {
                grab = grab,
                hand = hand,
                speed = Mathf.Max(pullSpeed, distance / maxPullSeconds),
                giveUpAt = Time.time + maxPullSeconds + 0.5f,
            });
        }

        private void OnLetGo(SelectExitEventArgs args)
        {
            for (int i = pulls.Count - 1; i >= 0; i--)
            {
                if (pulls[i].grab == (Object)args.interactableObject) End(i, false);
            }
        }

        private void Update()
        {
            for (int i = pulls.Count - 1; i >= 0; i--)
            {
                var pull = pulls[i];
                if (pull.grab == null || pull.hand == null || !pull.grab.isSelected || Time.time >= pull.giveUpAt)
                {
                    End(i, true);
                    continue;
                }

                // The grab point slides from the object to the hand (where it sits with no offset).
                var point = pull.hand.GetAttachTransform(pull.grab);
                var handPoint = point.parent != null ? point.parent.position : pull.hand.transform.position;
                var next = Vector3.MoveTowards(point.position, handPoint, pull.speed * Time.deltaTime);
                if ((next - handPoint).sqrMagnitude <= Arrived * Arrived) End(i, true);
                else pull.hand.interactionAttachController.MoveTo(next);
            }
        }

        private void End(int index, bool backToHand)
        {
            var pull = pulls[index];
            pulls.RemoveAt(index);
            if (backToHand && pull.hand != null && pull.hand.interactionAttachController != null && pull.grab != null && pull.grab.isSelected)
                pull.hand.interactionAttachController.ResetOffset();
        }

        /// <summary>How far the nearest part of 'thing' is from the hand, in metres.</summary>
        public static float HandDistance(Transform hand, IXRInteractable thing)
        {
            var point = hand.position;
            float nearest = float.MaxValue;
            if (thing is XRBaseInteractable interactable)
            {
                foreach (var collider in interactable.colliders)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    // ClosestPoint only works on convex shapes; a concave mesh counts by its bounds.
                    var closest = collider is MeshCollider { convex: false } ? collider.bounds.ClosestPoint(point) : collider.ClosestPoint(point);
                    nearest = Mathf.Min(nearest, (closest - point).sqrMagnitude);
                }
            }
            return nearest < float.MaxValue ? Mathf.Sqrt(nearest) : Vector3.Distance(point, thing.transform.position);
        }
    }
}
