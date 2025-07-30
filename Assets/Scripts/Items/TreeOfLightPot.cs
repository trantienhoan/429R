using UnityEngine;
using UnityEngine.Events;
using Items;
using Core;

namespace Items
{
    public class TreeOfLightPot : MonoBehaviour
    {
        [SerializeField] private GameObject treeOfLight;
        [SerializeField] private HealthComponent healthComponent;
        [SerializeField] private ItemDropHandler itemDropHandler;
        [SerializeField] private float growTime = 5f;

        public UnityEvent OnTreeGrown;

        private bool isGrowing;
        private float growTimer;
        private GameObject currentTree;

        public bool IsGrowing => isGrowing;

        private void Awake()
        {
            if (healthComponent == null)
            {
                healthComponent = GetComponent<HealthComponent>();
            }
            if (itemDropHandler == null)
            {
                itemDropHandler = GetComponent<ItemDropHandler>();
            }
            if (treeOfLight == null)
            {
                Debug.LogWarning($"[TreeOfLightPot {gameObject.name}] TreeOfLight reference is not set!");
            }
            gameObject.tag = "TreeOfLight";
        }

        private void Start()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDeath.AddListener(OnTreeDeath);
            }
        }

        private void OnDestroy()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDeath.RemoveListener(OnTreeDeath);
            }
        }

        public void TriggerDestruction()
        {
            if (itemDropHandler != null)
            {
                itemDropHandler.DropItems();
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Dropped items via ItemDropHandler");
            }
            if (healthComponent != null)
            {
                healthComponent.Kill(gameObject);
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Triggering destruction");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("MagicalSeed") && !isGrowing)
            {
                StartGrowing();
                Destroy(other.gameObject);
            }
        }

        private void Update()
        {
            if (isGrowing)
            {
                growTimer += Time.deltaTime;
                if (growTimer >= growTime)
                {
                    CompleteGrowth();
                }
            }
        }

        private void StartGrowing()
        {
            isGrowing = true;
            growTimer = 0f;
            if (healthComponent != null)
            {
                healthComponent.IsInvulnerable = false;
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Started growing, invulnerability disabled");
            }
        }

        private void CompleteGrowth()
        {
            isGrowing = false;
            if (treeOfLight != null)
            {
                currentTree = Instantiate(treeOfLight, transform.position, transform.rotation);
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Tree grown at {transform.position}");
            }
            OnTreeGrown?.Invoke();
            TriggerDestruction();
        }

        private void OnTreeDeath(HealthComponent health)
        {
            if (currentTree != null)
            {
                var treeHealth = currentTree.GetComponent<HealthComponent>();
                if (treeHealth != null)
                {
                    treeHealth.Kill(gameObject);
                    Debug.Log($"[TreeOfLightPot {gameObject.name}] Tree destroyed along with pot");
                }
                else
                {
                    Destroy(currentTree);
                }
            }
            Debug.Log($"[TreeOfLightPot {gameObject.name}] OnTreeDeath called");
        }
    }
}