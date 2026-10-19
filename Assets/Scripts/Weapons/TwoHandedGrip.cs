using Game.Combat;
using Game.Leveling;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Game.Weapons
{
    /// <summary>
    /// Lets a weapon be held in two hands (the broom, the pillow, the legendary sword...). The first hand holds it by its
    /// grip (the XR Grab Interactable's Attach Transform); the other hand takes hold wherever it grabs and steers it round
    /// the first. While both hands hold it, its hits do Damage Boost times the damage (1.5) but it wears out Wear Boost
    /// times as fast (2: a hard hit costs 2 health instead of 1). Let go with one hand and it's back to normal; the hand
    /// still holding keeps it where it is. Only a hand close to it (Second Hand Reach) can join in, so pointing the other
    /// hand's ray at it doesn't. It sets the XR Grab Interactable's Select Mode to Multiple and gives it an XR General Grab
    /// Transformer that turns it with two hands but never scales it with them (the legendary gear grows by its trigger
    /// instead). Goes on the weapon's root. Tools > 429 Game > Weapons > Make Selected Two-Handed adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class TwoHandedGrip : MonoBehaviour, IXRSelectFilter
    {
        [Tooltip("Held in two hands, its hits do this many times the damage (1.5 = half as much again).")]
        [Min(0.1f)]
        [SerializeField] private float damageBoost = 1.5f;
        [Tooltip("Held in two hands, it wears out this many times as fast (2 = each hard hit costs twice the health).")]
        [Min(0f)]
        [SerializeField] private float wearBoost = 2f;
        [Tooltip("The other hand can only take hold when it's this close to the weapon, in metres, so a far grab with the " +
                 "other hand's ray doesn't join in.")]
        [Min(0.05f)]
        [SerializeField] private float secondHandReach = 0.3f;
        [Tooltip("When one hand lets go, the other keeps holding it where it is. Off: it slides so its grip (Attach) lands " +
                 "in that hand.")]
        [SerializeField] private bool keepGripWhenOneLetsGo = true;

        private XRGrabInteractable grab;
        private WeaponPower weapon;
        private WeaponWear wear;
        private bool startDynamicAttach;
        private float damageApplied = 1f;
        private int handsHolding;

        /// <summary>Whether two hands hold it right now.</summary>
        public bool IsTwoHanded { get; private set; }

        public float DamageBoost => damageBoost;
        public float WearBoost => wearBoost;

        /// <summary>Two hands took hold (true), or one let go (false).</summary>
        public event System.Action<bool> TwoHandedChanged;

        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        /// <summary>Whether this thing is a two-handed weapon held in both hands right now.</summary>
        public static bool IsHeldInTwoHands(Component thing)
        {
            return thing != null && thing.TryGetComponent(out TwoHandedGrip grip) && grip.IsTwoHanded;
        }

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            TryGetComponent(out weapon);
            grab.selectMode = InteractableSelectMode.Multiple;
            // So the hand still holding gets a fresh grab point where it is when the other lets go.
            if (keepGripWhenOneLetsGo) grab.reinitializeDynamicAttachEverySingleGrab = true;
            startDynamicAttach = grab.useDynamicAttach;

            // XRI's own transformer for one and two hands. Set up by the menu already; added here if it wasn't.
            if (!TryGetComponent(out XRGeneralGrabTransformer transformer))
            {
                transformer = gameObject.AddComponent<XRGeneralGrabTransformer>();
                transformer.allowTwoHandedRotation = XRGeneralGrabTransformer.TwoHandedRotationMode.FirstHandDirectedTowardsSecondHand;
            }
            // Two-handed scaling would undo Legendary Gear's grow.
            transformer.allowTwoHandedScaling = false;
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnLetGo);
            grab.selectFilters.Add(this);
            Refresh();
        }

        private void OnDisable()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnLetGo);
            grab.selectFilters.Remove(this);
            handsHolding = 0;
            SetTwoHanded(false);
            grab.useDynamicAttach = startDynamicAttach;
        }

        private void OnGrabbed(SelectEnterEventArgs args) => Refresh();

        private void OnLetGo(SelectExitEventArgs args)
        {
            Refresh();
            // One hand let go and another still holds it: that hand keeps it where it is instead of the weapon sliding to
            // put its grip in that hand. Let go altogether, the next grab takes it by the grip again.
            if (!grab.isSelected) grab.useDynamicAttach = startDynamicAttach;
            else if (keepGripWhenOneLetsGo) grab.useDynamicAttach = true;
        }

        // Only a hand near it may join a hand that already holds it; never a second grab by the same hand.
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor is not XRBaseInputInteractor hand || handsHolding == 0 || grab.interactorsSelecting.Contains(interactor)) return true;
            foreach (var holder in grab.interactorsSelecting)
            {
                if (holder is XRBaseInputInteractor other && HandOf(other) == HandOf(hand)) return false;
            }
            return GentleFarGrab.HandDistance(hand.transform, grab) <= secondHandReach;
        }

        private void Refresh()
        {
            handsHolding = CountHands();
            SetTwoHanded(handsHolding >= 2);
        }

        private void SetTwoHanded(bool twoHanded)
        {
            if (twoHanded == IsTwoHanded) return;
            IsTwoHanded = twoHanded;

            // Multiplied in and divided out, so other boosts on the same weapon (Weapon Power's boost) stay as they are.
            if (weapon != null)
            {
                if (twoHanded)
                {
                    damageApplied = damageBoost;
                    weapon.boost *= damageApplied;
                }
                else
                {
                    weapon.boost /= damageApplied;
                    damageApplied = 1f;
                    if (Mathf.Abs(weapon.boost - 1f) < 0.0001f) weapon.boost = 1f;
                }
            }

            if (twoHanded && wear == null)
            {
                // Normally already there (Player Level adds it on pick-up).
                WeaponWear.Watch(gameObject);
                TryGetComponent(out wear);
            }
            if (wear != null) wear.boost = twoHanded ? wearBoost : 1f;

            TwoHandedChanged?.Invoke(twoHanded);
        }

        // How many different hands hold it (0, 1 or 2). Sockets and the like don't count.
        private int CountHands()
        {
            int count = 0;
            Object first = null;
            foreach (var interactor in grab.interactorsSelecting)
            {
                if (interactor is not XRBaseInputInteractor hand) continue;
                var which = HandOf(hand);
                if (count == 0)
                {
                    first = which;
                    count = 1;
                }
                else if (which != first) return 2;
            }
            return count;
        }

        // The hand an interactor belongs to: its controller's interaction group (direct, near-far and poke share one),
        // or else its parent (the controller).
        private static Object HandOf(XRBaseInteractor interactor)
        {
            if (interactor.containingGroup is Object group && group != null) return group;
            var t = interactor.transform;
            return t.parent != null ? t.parent : t;
        }
    }
}
