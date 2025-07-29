using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit;
using Core;
using Items;
using System.Collections;

namespace Enemies
{
    public class ShadowMonster : BaseMonster
    {
        [Header("References")]
        [SerializeField] public Animator animator;
        [SerializeField] public NavMeshAgent agent;
        [SerializeField] private Transform[] wanderPoints;
        [SerializeField] public SpiderAttackHitbox attackHitbox;
        [SerializeField] public Rigidbody rb;
        [SerializeField] private ParticleSystem deathVFX;
        [SerializeField] private AudioClip deathSFX;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
        [SerializeField] public HealthComponent healthComponent;

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
        public float chargeTimer { get; private set; }
        private bool isCharging;
        public StateMachine stateMachine { get; private set; }

        public bool isGrounded { get; private set; }
        public bool IsBeingHeld { get; private set; }

        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundCheckDistance = 0.5f;
        [SerializeField] private LayerMask groundLayer;

        public bool IsGrounded()
        {
            bool grounded = Physics.Raycast(groundCheckPoint.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer);
            isGrounded = grounded;
            if (animator != null)
            {
                animator.SetBool("isGrounded", grounded);
                animator.Update(0f); // Force Animator update
                //Debug.Log($"Set isGrounded to {grounded} in Animator: {animator.name}, Actual Animator isGrounded: {animator.GetBool("isGrounded")}, GameObject: {gameObject.name}");
            }
            else
            {
                Debug.LogError("Animator is not assigned!");
            }
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            //Debug.Log($"IsGrounded: {grounded}, Position: {groundCheckPoint.position}, Hit: {(grounded ? hit.collider.name : "None")}, Animator isGrounded: {animator.GetBool("isGrounded")}, Current State: {stateInfo.fullPathHash} ({GetStateName(stateInfo.fullPathHash)})");
            //Debug.DrawRay(groundCheckPoint.position, Vector3.down * groundCheckDistance, grounded ? Color.green : Color.red, 1f);
            return grounded;
        }

        public string GetStateName(int stateHash)
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

        public void EnsureAgentOnNavMesh()
        {
            if (agent != null && !agent.isActiveAndEnabled)
            {
                agent.enabled = true;
                agent.speed = 3.5f;
                agent.stoppingDistance = 0.5f;
                Debug.Log("EnsureAgentOnNavMesh: Enabled NavMeshAgent");
            }
            if (agent != null && !agent.isOnNavMesh && isGrounded)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    agent.Warp(hit.position);
                    Debug.Log($"EnsureAgentOnNavMesh: Warped to NavMesh position {hit.position}, Agent Active = {agent.isActiveAndEnabled}, OnNavMesh = {agent.isOnNavMesh}");
                }
                else
                {
                    Debug.LogWarning("EnsureAgentOnNavMesh: Failed to find NavMesh position");
                }
            }
            else if (agent != null)
            {
                Debug.Log($"EnsureAgentOnNavMesh: Agent Active = {agent.isActiveAndEnabled}, OnNavMesh = {agent.isOnNavMesh}");
            }
        }

        public void EnableAI()
        {
            EnsureAgentOnNavMesh();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                stateMachine.ChangeState(new IdleState(this));
                Debug.Log("EnableAI: NavMeshAgent enabled, set to IdleState");
            }
            else
            {
                Debug.LogWarning("EnableAI: NavMeshAgent is not active or not on NavMesh");
            }
        }

        public Vector3 GetRandomWanderPoint()
        {
            if (wanderPoints == null || wanderPoints.Length == 0)
            {
                Debug.LogWarning("No wander points set!");
                return transform.position;
            }
            Vector3 wanderPoint = wanderPoints[Random.Range(0, wanderPoints.Length)].position;
            Debug.Log($"GetRandomWanderPoint: Selected wander point at {wanderPoint}");
            return wanderPoint;
        }

        protected override void Awake()
        {
            base.Awake();
            stateMachine = new StateMachine();
            audioSource = GetComponent<AudioSource>();
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            healthComponent = GetComponent<HealthComponent>();
            gameObject.tag = "Enemy";
            if (!GetComponent<HealthComponent>())
            {
                gameObject.AddComponent<HealthComponent>();
            }
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                Debug.LogWarning("Animator fetched via GetComponent!");
            }
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
            if (attackHitbox != null)
            {
                attackHitbox.Initialize(gameObject, attackDamage, pushForce);
            }
            if (animator != null)
            {
                animator.SetBool("isGrounded", isGrounded);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                Debug.Log($"Awake: Set isGrounded = {isGrounded}, isRunning = false, isCharging = false, Attack trigger reset");
            }
            EnsureAgentOnNavMesh();
        }

        protected override void Start()
        {
            base.Start();
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnGrabbed);
                grabInteractable.selectExited.AddListener(OnReleased);
            }
            healthComponent.OnTakeDamage += OnHealthChanged;
            healthComponent.OnDeath += OnDeath;
            if (animator != null)
            {
                animator.SetBool("isGrounded", isGrounded);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                Debug.Log($"Start: Set isGrounded = {isGrounded}, isRunning = false, isCharging = false, Attack trigger reset");
            }
            EnableAI();
        }

        private void OnEnable()
        {
            ResetSpider();
        }

        private void Update()
        {
            isGrounded = IsGrounded();
            EnsureAgentOnNavMesh();
            stateMachine.Tick();
            CheckIfStuck();
            if (Input.GetKeyDown(KeyCode.G) && isGrounded)
            {
                if (animator != null)
                {
                    animator.SetBool("isGrounded", true);
                    animator.SetBool("isRunning", false);
                    animator.SetBool("isCharging", false);
                    animator.ResetTrigger("Attack");
                    stateMachine.ChangeState(new IdleState(this));
                    Debug.Log("Forced isGrounded to true, isRunning to false, isCharging to false, Attack trigger reset, and Idle state");
                }
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                stateMachine.ChangeState(new ChaseState(this));
                Debug.Log("Forced ChaseState");
            }
        }

        private void CheckIfStuck()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && Vector3.Distance(transform.position, lastPosition) < 0.1f)
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
            if (e.DamageAmount > 0 && !healthComponent.IsDead() && stateMachine.CurrentState is not HurtState && !IsBeingHeld)
            {
                isCharging = false;
                stateMachine.ChangeState(new HurtState(this));
            }

            if (healthComponent.GetHealthPercentage() < 0.19f && stateMachine.CurrentState is not KamikazeState && !IsBeingHeld)
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
            currentTarget = null;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.localScale = Vector3.one;
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
                animator.SetBool("isGrounded", isGrounded);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                Debug.Log($"ResetSpider: Set isGrounded = {isGrounded}, isRunning = false, isCharging = false, Attack trigger reset, Cleared currentTarget");
            }
            EnsureAgentOnNavMesh();
            healthComponent.ResetHealth();
            if (attackHitbox != null)
            {
                attackHitbox.Initialize(gameObject, attackDamage, pushForce);
            }
            stateMachine.ChangeState(new IdleState(this));
            Debug.Log("ResetSpider: Transitioned to IdleState");
        }

        public void Pickup()
        {
            IsBeingHeld = true;
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.enabled = false;
                Debug.Log("Pickup: Disabled NavMeshAgent");
            }
            if (animator != null)
            {
                animator.SetBool("isGrounded", false);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                animator.Update(0f);
                Debug.Log("Pickup: Set isGrounded = false, isRunning = false, isCharging = false, Attack trigger reset");
            }
            stateMachine.ChangeState(new HeldState(this));
        }

        public void Release()
        {
            IsBeingHeld = false;
            currentTarget = null;
            if (animator != null)
            {
                animator.SetBool("isGrounded", isGrounded);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                animator.Update(0f);
                Debug.Log($"Release: Set isGrounded = {isGrounded}, isRunning = false, isCharging = false, Attack trigger reset, Cleared currentTarget");
            }
            EnsureAgentOnNavMesh();
            if (isGrounded && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                stateMachine.ChangeState(new IdleState(this));
                Debug.Log("Release: Transitioned to IdleState");
            }
            else
            {
                Debug.LogWarning("Release: Cannot transition to IdleState, NavMeshAgent not ready");
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
                if (obj != null && obj.activeInHierarchy)
                {
                    var treePot = obj.GetComponent<TreeOfLightPot>();
                    if (treePot != null && treePot.IsGrowing) // Only target growing pot
                    {
                        float distance = Vector3.Distance(transform.position, obj.transform.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestTarget = obj.transform;
                        }
                    }
                }
            }

            if (closestTarget == null)
            {
                foreach (GameObject obj in players)
                {
                    if (obj != null && obj.activeInHierarchy)
                    {
                        float distance = Vector3.Distance(transform.position, obj.transform.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestTarget = obj.transform;
                        }
                    }
                }
            }

            currentTarget = closestTarget;
            Debug.Log($"GetClosestTarget: Selected {(closestTarget != null ? closestTarget.name : "None")}, Distance = {closestDistance}, PlayersFound = {players.Length}, TreesFound = {trees.Length}");
            return closestTarget;
        }

        public void StartCharge()
        {
            isCharging = true;
            chargeTimer = 0f;
            if (animator != null)
            {
                animator.SetBool("isCharging", true);
                animator.SetBool("isRunning", false);
                animator.SetBool("isGrounded", isGrounded);
                animator.ResetTrigger("Attack");
                animator.Update(0f);
                Debug.Log($"StartCharge: Set isCharging = true, isRunning = false, isGrounded = {isGrounded}, chargeTimer = {chargeTimer}, Attack trigger reset, Animator isCharging = {animator.GetBool("isCharging")}, Current State = {animator.GetCurrentAnimatorStateInfo(0).fullPathHash} ({GetStateName(animator.GetCurrentAnimatorStateInfo(0).fullPathHash)})");
            }
        }

        public bool IsChargeComplete()
        {
            chargeTimer += Time.deltaTime;
            Debug.Log($"IsChargeComplete: chargeTimer = {chargeTimer}, chargeDelay = {chargeDelay}, Complete = {chargeTimer >= chargeDelay}");
            return chargeTimer >= chargeDelay;
        }

        public void PerformAttack()
        {
            isCharging = false;
            if (attackHitbox != null)
            {
                attackHitbox.Initialize(gameObject, attackDamage, pushForce);
                attackHitbox.TriggerExplosion();
                Debug.Log($"PerformAttack: Triggered explosion via SpiderAttackHitbox, Damage = {attackDamage}, PushForce = {pushForce}");
            }
            else
            {
                Debug.LogWarning("PerformAttack: attackHitbox is null");
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
            if (currentTarget == null)
            {
                currentTarget = GetClosestTarget();
                Debug.Log("GetDistanceToTarget: currentTarget was null, re-acquired");
            }
            return currentTarget != null ? Vector3.Distance(transform.position, currentTarget.position) : Mathf.Infinity;
        }

        public void PlayDeathEffects()
        {
            if (animator != null)
            {
                animator.SetTrigger("Dead");
            }
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
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                grabInteractable.selectExited.RemoveListener(OnReleased);
            }
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