using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Combat
{
    /// <summary>
    /// A found piece of the Knight's armor (Knight Armor Hider puts it where its hiding place broke): it floats and turns,
    /// glowing, in its green aura. User 2026-11-26: "make the armor act like candies, let's player can grab and eat it":
    /// grab it (its XR Grab Interactable), bring it to your mouth (the Mouth ball under the camera, tag Mouth) and it is
    /// eaten: put on, its armor and stat for the rest of the run (Knight Armor). Let go, it falls and lies like a candy.
    /// Gone uneaten (the room cleared away, the purple hole), it hides again in the next room. Touch Tag picks what eats
    /// it: Mouth for the armor; Player (the body or hands) for her lost things. Made by Tools > 429 Game > Items > Set Up
    /// Knight Armor Hunt and Knight Armor: Rarer, Grab And Eat.
    /// </summary>
    public class KnightArmorPickup : MonoBehaviour
    {
        [Tooltip("Which piece (its id in Knight Armor Settings).")]
        [SerializeField] private string pieceId = "Helmet";
        [Tooltip("What has to touch it for it to be taken: Mouth (eaten, like a candy) or Player (picked up on touch).")]
        [SerializeField] private string touchTag = "Player";
        [Tooltip("Degrees a second it turns while it floats.")]
        [SerializeField] private float spin = 70f;
        [Tooltip("How far it bobs up and down while it floats (metres), and how fast.")]
        [SerializeField] private float bob = 0.05f;
        [SerializeField] private float bobSpeed = 2f;

        private Vector3 home;
        private bool started;
        private bool taken;
        // Grabbed once: it no longer floats; let go, it falls.
        private bool loose;
        private XRGrabInteractable grab;
        private Rigidbody body;

        public string PieceId => pieceId;

        /// <summary>Picked up by a hand now (for tests).</summary>
        public bool IsHeld => grab != null && grab.isSelected;

        public void Set(string id) => pieceId = id;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (grab == null) return;
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnLetGo);
        }

        private void OnDisable()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnLetGo);
        }

        private void Start()
        {
            home = transform.position;
            started = true;
        }

        private void Update()
        {
            if (!started || loose) return;
            transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
            transform.position = home + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bob);
        }

        private void OnGrabbed(SelectEnterEventArgs args) => loose = true;

        // XR Interaction Toolkit has just given the body back as it was (floating, kinematic): it falls and can be thrown
        // now (the throw is applied at the end of this frame).
        private void OnLetGo(SelectExitEventArgs args)
        {
            if (grab.isSelected || body == null) return;
            body.isKinematic = false;
            body.useGravity = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!taken && other.CompareTag(touchTag)) Take();
        }

        /// <summary>Puts it on (eaten, touched, or a test), with its sound and a puff, and it's gone.</summary>
        public void Take()
        {
            if (taken) return;
            taken = true;
            var settings = KnightArmorSettings.Instance;
            // One of her lost things: kept for the run (the Knight Armor Hider plays her sound, and her thanks for the last).
            if (pieceId.StartsWith(KnightArmorSettings.KeepsakePrefix))
            {
                if (KnightArmor.Collect(pieceId) && settings != null && settings.FoundPuff != null)
                    Destroy(Instantiate(settings.FoundPuff, transform.position, Quaternion.identity), 4f);
                Destroy(gameObject);
                return;
            }
            if (KnightArmor.Equip(pieceId) && settings != null)
            {
                KnightArmorHider.PlayAt(settings.EquipSound, transform.position, settings.EquipVolume);
                if (settings.FoundPuff != null) Destroy(Instantiate(settings.FoundPuff, transform.position, Quaternion.identity), 4f);
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Gone uneaten: it hides again (not when the game is closing: the hider may be gone first).
            if (taken || !started || KnightArmorHider.Instance == null || !gameObject.scene.isLoaded) return;
            KnightArmorHider.Instance.Lost(pieceId);
        }
    }
}
