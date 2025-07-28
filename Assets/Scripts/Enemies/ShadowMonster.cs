using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit;
using Core;
using System.Collections;

namespace Enemies
{
    public class ShadowMonster : BaseMonster
    {
        [Header("References")]
        public Animator animator;
        public NavMeshAgent agent;
        public Transform[] wanderPoints;
        public SpiderAttackHitbox attackHitbox;
        public Rigidbody rb;
        public ParticleSystem deathVFX;
        public AudioClip deathSFX;
        public AudioSource audioSource;
        public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
        public HealthComponent healthComponent;

        [Header("Settings")]
        public float chaseRange = 15f;
        public float attackRange = 2f;
        public float kamikazeRange = 5f;
        public float wanderDelay = 3f;
        public float chargeDelay = 2.5f;
        public float attackDamage = 50f;
        public float kamikazeDamage = 10f;
        public float pushForce = 5f;
        public float stuckTimeThreshold = 5f;
        public float idleTimeBeforeDive = 5f;

        private Transform currentTarget;
        private Vector3 lastPosition;
        private float stuckTimer;
        private float chargeTimer;
        private bool isCharging;
        public StateMachine stateMachine;

        public bool isGrounded { get; private set; }
        public bool IsBeingHeld { get; private set; }

        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundCheckDistance = 0.5f; // Increased for reliability
        [SerializeField] private LayerMask groundLayer;

        public bool IsGrounded()
        {
            bool grounded = Physics.Raycast(groundCheckPoint.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer);
            isGrounded = grounded;
            animator.SetBool("isGrounded", grounded);
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"IsGrounded: {grounded}, Position: {groundCheckPoint.position}, Hit: {(grounded ? hit.collider.name : "None")}, Animator isGrounded: {animator.GetBool("isGrounded")}, Current State: {stateInfo.fullPathHash} ({GetStateName(stateInfo.fullPathHash)})");
            Debug.DrawRay(groundCheckPoint.position, Vector3.down * groundCheckDistance, grounded ? Color.green : Color.red, 0.1f);
            return grounded;
        }

        private string GetStateName(int stateHash)
        {
            if (stateHash == Animator.StringToHash("Base Layer.Idle")) return "Idle";
            if (stateHash == Animator.StringToHash("Base Layer.Idle_On_Air")) return "Idle_On_Air";
            if (stateHash == Animator.StringToHash("Base Layer.Run")) return "Run";
            if (stateHash == Animator.StringToHash("Base Layer.Charge")) return "Charge";
            if (stateHash == Animator.StringToHash("Base Layer.Attack")) return "Attack";
            if (stateHash == Animator.StringToHash("Base Layer.Hurt")) return "Hurt";
            if (stateHash == Animator.StringToHash("Base Layer.Dive")) return "Dive";
            if (stateHash == Animator.StringToHash("Base Layer.Dead")) return "Dead";
            return "Unknown";
        }

        public void EnableAI()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.enabled = true;
                stateMachine.ChangeState(new IdleState(this));
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

        protected override void Awake()
        {
            base.Awake();
            stateMachine = new StateMachine();
            audioSource = GetComponent<AudioSource>();
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            healthComponent = GetComponent<HealthComponent>();
            if (attackHitbox != null)
            {
                attackHitbox.Initialize(gameObject, attackDamage, pushForce);
            }
        }

        protected override void Start()
        {
            base.Start();
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
            healthComponent.OnTakeDamage += OnHealthChanged;
            healthComponent.OnDeath += OnDeath;
        }

        private void OnEnable()
        {
            ResetSpider();
        }

        private void Update()
        {
            isGrounded = IsGrounded();
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
                    if (attackHitbox != null)
                    {
                        attackHitbox.Initialize(gameObject, kamikazeDamage, pushForce);
                        attackHitbox.TriggerExplosion();
                    }
                }
            }
            else
            {
                stuckTimer = 0f;
                lastPosition = transform.position;
            }
        }

        public override void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
        {
            healthComponent.TakeDamage(damageAmount, hitPoint, damageSource);
        }

        private void OnHealthChanged(object sender, HealthComponent.HealthChangedEventArgs e)
        {
            if (e.DamageAmount > 0 && !this.healthComponent.IsDead() && stateMachine.CurrentState is not HurtState && !this.IsBeingHeld)
            {
                isCharging = false;
                stateMachine.ChangeState(new HurtState(this));
            }

            if (this.healthComponent.GetHealthPercentage() < 0.19f && stateMachine.CurrentState is not KamikazeState && !this.IsBeingHeld)
            {
                stateMachine.ChangeState(new KamikazeState(this));
            }
        }

        private void OnDeath(HealthComponent health)
        {
            stateMachine.ChangeState(new DeadState(this));
        }

        protected override void OnDeathHandler()
        {
        }

        public void SetMaxHealth(float value)
        {
            healthComponent.SetMaxHealth(value);
        }

        public void SetState(IState newState)
        {
            stateMachine.ChangeState(newState);
        }

        public void Explode()
        {
            if (attackHitbox != null)
            {
                attackHitbox.Initialize(gameObject, kamikazeDamage, pushForce);
                attackHitbox.TriggerExplosion();
            }
        }

        public void ResetSpider()
        {
            stuckTimer = 0f;
            chargeTimer = 0f;
            isCharging = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.localScale = Vector3.one;
            animator.Rebind();
            animator.Update(0f);
            animator.SetBool("isGrounded", isGrounded);
            if (agent != null && !agent.isActiveAndEnabled && isGrounded)
            {
                agent.enabled = true;
            }
            healthComponent.ResetHealth();
            if (attackHitbox != null)
            {
                attackHitbox.Initialize(gameObject, attackDamage, pushForce);
            }
            stateMachine.ChangeState(new IdleState(this));
        }

        public void Pickup()
        {
            IsBeingHeld = true;
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.enabled = false;
            }
            animator.SetBool("isGrounded", false);
            stateMachine.ChangeState(new HeldState(this));
        }

        public void Release()
        {
            IsBeingHeld = false;
            animator.SetBool("isGrounded", isGrounded);
            if (isGrounded && agent != null && !agent.isActiveAndEnabled)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    stateMachine.ChangeState(new IdleState(this));
                }
            }
        }

        public Transform GetClosestTarget()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            GameObject[] trees = GameObject.FindGameObjectsWithTag("TreeOfLight");
            Transform closestTarget = null;
            float closestDistance = Mathf.Infinity;

            foreach (GameObject obj in trees)
            {
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = obj.transform;
                }
            }

            if (closestTarget == null)
            {
                foreach (GameObject obj in players)
                {
                    float distance = Vector3.Distance(transform.position, obj.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = obj.transform;
                    }
                }
            }

            currentTarget = closestTarget;
            return currentTarget;
        }

        public void StartCharge()
        {
            isCharging = true;
            chargeTimer = 0f;
            animator.SetBool("isCharging", true);
        }

        public bool IsChargeComplete()
        {
            chargeTimer += Time.deltaTime;
            return chargeTimer >= chargeDelay;
        }

        public void PerformAttack()
        {
            isCharging = false;
            animator.SetBool("isCharging", false);
            animator.SetTrigger("Attack");
            if (attackHitbox != null)
            {
                attackHitbox.Initialize(gameObject, attackDamage, pushForce);
                attackHitbox.TriggerExplosion();
            }
        }

        private IEnumerator DisableHitboxAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (attackHitbox != null)
            {
                attackHitbox.gameObject.SetActive(false);
            }
        }

        public float GetDistanceToTarget()
        {
            if (currentTarget == null) return Mathf.Infinity;
            return Vector3.Distance(transform.position, currentTarget.position);
        }

        public void PlayDeathEffects()
        {
            animator.SetTrigger("Dead");
            if (deathVFX != null) deathVFX.Play();
            if (deathSFX != null && audioSource != null) audioSource.PlayOneShot(deathSFX);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            Pickup();
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            Release();
        }

        private void OnDestroy()
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
            healthComponent.OnTakeDamage -= OnHealthChanged;
            healthComponent.OnDeath -= OnDeath;
        }

        public IEnumerator ScaleDownAndDisable()
        {
            yield return StartCoroutine(DestroyAfterDelay(3f));
        }

        private IEnumerator DestroyAfterDelay(float delay)
        {
            float timer = 0f;
            Vector3 startScale = transform.localScale;
            while (timer < delay)
            {
                timer += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, timer / delay);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}