using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A found piece of the Knight's armor (Knight Armor Hider puts it where its hiding place broke): it floats and turns,
    /// glowing; touch it (anything tagged Player: the player's body or hands) and it is put on, its armor and stat for the
    /// rest of the run (Knight Armor). Made by Tools > 429 Game > Items > Set Up Knight Armor Hunt.
    /// </summary>
    public class KnightArmorPickup : MonoBehaviour
    {
        [Tooltip("Which piece (its id in Knight Armor Settings).")]
        [SerializeField] private string pieceId = "Helmet";
        [Tooltip("Degrees a second it turns.")]
        [SerializeField] private float spin = 70f;
        [Tooltip("How far it bobs up and down (metres), and how fast.")]
        [SerializeField] private float bob = 0.05f;
        [SerializeField] private float bobSpeed = 2f;

        private Vector3 home;
        private bool started;
        private bool taken;

        public string PieceId => pieceId;

        public void Set(string id) => pieceId = id;

        private void Start()
        {
            home = transform.position;
            started = true;
        }

        private void Update()
        {
            if (!started) return;
            transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
            transform.position = home + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bob);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!taken && other.CompareTag("Player")) Take();
        }

        /// <summary>Puts it on (touched, or a test), with its sound and a puff, and it's gone.</summary>
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
    }
}
