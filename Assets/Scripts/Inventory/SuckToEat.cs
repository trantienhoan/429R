using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Inventory
{
    /// <summary>
    /// A treat you have to suck to eat, like a lollipop: hold it at your mouth for Suck Seconds and it's eaten (it heals,
    /// like a candy, and adds its Reward to the inventory). While you suck it gets smaller and buzzes in your hand;
    /// take it away and the sucking slowly wears off. Touching it doesn't eat it. Goes on the treat's root, next to its
    /// XR Grab Interactable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SuckToEat : MonoBehaviour
    {
        [Tooltip("Seconds it has to be held at the mouth to be eaten.")]
        [Min(0.1f)]
        [SerializeField] private float suckSeconds = 3f;
        [Tooltip("How close to the mouth it has to be, in metres.")]
        [Min(0.01f)]
        [SerializeField] private float mouthReach = 0.12f;
        [Tooltip("Event sent to the player's Damage FSM when it's eaten: Health1 heals, like eating a candy.")]
        [SerializeField] private string healEvent = "Health1";
        [Tooltip("Added to the inventory when it's eaten, e.g. Candy (shown as Wrap). Empty: nothing.")]
        [SerializeField] private ItemDefinition reward;
        [Min(0)]
        [SerializeField] private int rewardAmount = 1;
        [Tooltip("How small it has got by the time it's eaten, as a share of its size.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float shrinkTo = 0.6f;
        [Tooltip("Buzz in the hand while sucking, 0-1 (0 = none).")]
        [Range(0f, 1f)]
        [SerializeField] private float buzz = 0.15f;

        private const string PlayerVariable = "Player";
        private const string PlayerDamageFsm = "Damage";
        private const float BuzzEvery = 0.2f;

        private readonly List<Renderer> shapes = new();
        private XRGrabInteractable grab;
        private Vector3 fullSize;
        private float sucked;
        private float nextBuzz;
        private bool eaten;

        public float Progress => sucked / suckSeconds;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            // Only its own meshes count, not effects like a smoke puff.
            foreach (var shape in GetComponentsInChildren<Renderer>(true))
            {
                if (shape is MeshRenderer || shape is SkinnedMeshRenderer) shapes.Add(shape);
            }
        }

        private void Start()
        {
            fullSize = transform.localScale;
        }

        private void Update()
        {
            if (eaten) return;

            var head = Camera.main != null ? Camera.main.transform : null;
            if (grab.isSelected && head != null && AtMouth(head))
            {
                sucked += Time.deltaTime;
                Buzz();
                if (sucked >= suckSeconds)
                {
                    Eat();
                    return;
                }
            }
            else if (sucked > 0f)
            {
                // Wears off twice as fast as it builds up.
                sucked = Mathf.Max(0f, sucked - Time.deltaTime * 2f);
            }

            transform.localScale = fullSize * Mathf.Lerp(1f, shrinkTo, Mathf.Clamp01(sucked / suckSeconds));
        }

        // The mouth is a little below and in front of the eyes.
        private bool AtMouth(Transform head)
        {
            var mouth = head.position + head.forward * 0.06f - head.up * 0.08f;
            foreach (var shape in shapes)
            {
                if (shape == null || !shape.enabled) continue;
                if ((shape.bounds.ClosestPoint(mouth) - mouth).sqrMagnitude <= mouthReach * mouthReach) return true;
            }
            return false;
        }

        private void Buzz()
        {
            if (buzz <= 0f || Time.time < nextBuzz) return;
            nextBuzz = Time.time + BuzzEvery;
            if (grab.firstInteractorSelecting is XRBaseInputInteractor hand) hand.SendHapticImpulse(buzz, 0.1f);
        }

        private void Eat()
        {
            eaten = true;
            if (!string.IsNullOrEmpty(healEvent)) SendToPlayer(healEvent);
            if (reward != null && rewardAmount > 0 && PlayerInventory.Instance != null) PlayerInventory.Instance.Add(reward, rewardAmount);
            Destroy(gameObject);
        }

        private static void SendToPlayer(string eventName)
        {
            var player = FsmVariables.GlobalVariables.FindFsmGameObject(PlayerVariable);
            if (player == null || player.Value == null) return;
            foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == PlayerDamageFsm) fsm.SendEvent(eventName);
            }
        }
    }
}
