using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Enemies
{
    public class ShadowMonster : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public NavMeshAgent agent;
        public Transform[] wanderPoints;
        public GameObject attackHitbox;
        public Rigidbody rb;

        [Header("Settings")]
        public float chaseRange = 15f;
        public float attackRange = 2f;
        public float wanderDelay = 3f;
        public float chargeDelay = 2.5f;
        public float damageAmount = 50f;
        public float stuckTimeThreshold = 5f;

        private Transform currentTarget;
        private Vector3 lastPosition;
        private float stuckTimer;

        private StateMachine stateMachine;
        
        private float maxHealth;
        private float currentHealth;
        
        public void SetMaxHealth(float value)
        {
            maxHealth = value;
            currentHealth = value;
        }
        
        public bool isGrounded { get; private set; }
        public bool IsBeingHeld { get; private set; }
        
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private LayerMask groundLayer;
        public bool IsGrounded()
        {
            return Physics.Raycast(groundCheckPoint.position, Vector3.down, groundCheckDistance, groundLayer);
        }
        public void EnableAI()
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.enabled = true;
                SetState(new IdleState(this));
            }
        }
        public Vector3 GetRandomWanderPoint()
        {
            if (wanderPoints == null || wanderPoints.Length == 0)
            {
                Debug.LogWarning("No wander points set!");
                return transform.position;
            }

            return wanderPoints[Random.Range(0, wanderPoints.Length)].position;
        }

        private void Awake()
        {
            stateMachine = new StateMachine();
        }

        private void OnEnable()
        {
            ResetSpider();
        }

        private void Update()
        {
            stateMachine.Tick();
            CheckIfStuck();
        }

        private void CheckIfStuck()
        {
            if (Vector3.Distance(transform.position, lastPosition) < 0.1f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > stuckTimeThreshold)
                {
                    Explode();
                }
            }
            else
            {
                stuckTimer = 0f;
                lastPosition = transform.position;
            }
        }

        public void SetState(IState newState)
        {
            stateMachine.ChangeState(newState);
        }

        public void Explode()
        {
            Debug.Log("BOOM! Spider explodes.");
            // ... your explosion logic here ...
        }

        public void ResetSpider()
        {
            stuckTimer = 0f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.localScale = Vector3.one;
            animator.Rebind();
            animator.Update(0f);
            agent.enabled = true;

            SetState(new IdleState(this));
        }

        public void Pickup()
        {
            IsBeingHeld = true;
            SetState(new HeldState(this));
        }

        public void Release()
        {
            IsBeingHeld = false;
            SetState(new IdleState(this));
        }

        public Transform GetClosestTarget()
        {
            // Your prioritization logic: TreeOfLight > Player > Furniture
            return currentTarget;
        }

        public IEnumerator ScaleDownAndDisable()
        {
            throw new System.NotImplementedException();
        }
    }
}