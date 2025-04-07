using UnityEngine;
using System.Collections;

namespace Enemies
{
    public class ShadowMonsterSpider : ShadowMonster
    {
        [Header("Movement Settings")]
        [SerializeField] private float patrolSpeed = 5f;
        [SerializeField] private float chaseSpeed = 10f;
        [SerializeField] private float slowdownDistance = 2f; // Distance to slow down before attacking
        [SerializeField] private float attackSpeedMultiplier = 0.5f; // Speed multiplier during attack
        [SerializeField] private float reTargetingRange = 20f; //Range to find a new tree

        [Header("Attack Settings")]
        [SerializeField] private float attackDamage = 20f;
        [SerializeField] private float jumpHeight = 2f; // Height of the jump during the attack

        private GameObject _treeOfLightPot; // The TreeOfLightPot target.
        private Vector2 _currentDirection; // Current movement direction if no tree
        private bool _isAttacking = false;
        private SpiderController _spiderController;
        private Rigidbody _rb;

        private void Start()
        {
            _spiderController = GetComponent<SpiderController>();
            _rb = GetComponent<Rigidbody>(); // Get the Rigidbody
            if (_spiderController == null)
            {
                Debug.LogError("SpiderController not found on ShadowMonsterSpider!");
                enabled = false;
                return;
            }

            // Find the TreeOfLightPot (you might want to use a more robust method)
            FindTreeOfLightPot();

            if (_treeOfLightPot == null)
            {
                Debug.LogWarning("TreeOfLightPot not found. Spider will patrol randomly.");
                SetRandomDirection();
            }
            else
            {
                SetMovementDirection(_treeOfLightPot.transform.position, patrolSpeed); // Start patrolling
            }
        }

        private void Update()
        {
            if (_treeOfLightPot != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, _treeOfLightPot.transform.position);

                if (distanceToTarget <= slowdownDistance && !_isAttacking)
                {
                    // Slow down before attacking
                    SetMovementDirection(_treeOfLightPot.transform.position, patrolSpeed * attackSpeedMultiplier);
                    _isAttacking = true;
                    Attack(); // Initiate the attack
                }
                else
                {
                    // Chase the target at full speed
                    SetMovementDirection(_treeOfLightPot.transform.position, chaseSpeed);
                }
            }
            else
            {
                // No TreeOfLightPot found, move in a random direction
                SetMovementDirection(_currentDirection, patrolSpeed);

                // Periodically check for the tree
                if (Random.value < 0.01f) // Check every so often
                {
                    FindTreeOfLightPot();

                    //If the tree is found but out of range, move to a random direction so that monster dont chase infinitely
                    if (_treeOfLightPot != null && Vector3.Distance(transform.position, _treeOfLightPot.transform.position) > reTargetingRange)
                    {
                        SetRandomDirection();
                        _treeOfLightPot = null;
                    }
                    else if (_treeOfLightPot != null)
                    {
                        SetMovementDirection(_treeOfLightPot.transform.position, patrolSpeed);
                    }
                }
            }
        }

        private void FindTreeOfLightPot()
        {
            // Find the TreeOfLightPot (you might want to use a more robust method, like a tag or a dedicated manager)
            _treeOfLightPot = GameObject.FindGameObjectWithTag("TreeOfLightPot");
        }

        private void SetMovementDirection(Vector3 targetPosition, float speed)
        {
            Vector2 direction = (targetPosition - transform.position).normalized;
            SetMovementDirection(direction, speed);
        }

        private void SetMovementDirection(Vector2 direction, float speed)
        {
            Debug.Log("SetMovementDirection called: direction=" + direction + ", speed=" + speed);
            _spiderController.SetMovementDirection(direction * speed); // Use SpiderController
        }

        private void SetRandomDirection()
        {
            _currentDirection = Random.insideUnitCircle.normalized;
        }

        private void Attack()
        {
            // Implement Attack logic:
            Debug.Log("Spider Attack!");
            // Determine if jump or run
            if (jumpHeight > 0 && Random.value > 0.5f)
            {
                JumpAttack();
            }
            else
            {
                RunAttack();
            }
        }

        private void JumpAttack()
        {
            // Implement jump attack logic:
            Debug.Log("Spider Jump Attack!");

            // Calculate the required velocity to reach the target
            //Vector3 velocity = CalculateJumpVelocity(_targetPosition - transform.position, timeToTarget, jumpHeight);

            // Apply the velocity to the rigidbody
            if (_rb != null)
            {
                //_rb.linearVelocity = velocity;
            }

            // Subscribe to collision, to apply damage to the target.
            //StartCoroutine(OnCollisionCoroutine(timeToTarget));
        }

        //private IEnumerator OnCollisionCoroutine(float delay)
        //{
        //    yield return new WaitForSeconds(delay);
        //
        //    // Apply damage to TreeOfLightPot (assuming it has a HealthComponent)
        //    Core.HealthComponent health = _treeOfLightPot.GetComponent<Core.HealthComponent>();
        //    if (health != null)
        //    {
        //        health.TakeDamage(attackDamage);
        //    }
        //
        //    //ReEnable the navmesh
        //    //_spiderController.SetMovementDirection(Vector2.zero); // Stop movement after attack - REMOVE THIS LINE
        //    _isAttacking = false;
        //}

        private Vector3 CalculateJumpVelocity(Vector3 displacement, float timeToTarget, float jumpHeight)
        {
            float gravity = Physics.gravity.y;
            float verticalVelocity = (jumpHeight * 2) / timeToTarget - (gravity * timeToTarget) / 2;
            Vector3 horizontalVelocity = displacement / timeToTarget;
            return new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);
        }

        private void RunAttack()
        {
            // Implement run attack logic:
            Debug.Log("Spider Run Attack!");
            _isAttacking = false;
        }

        //IK Implementation for legs to walk around
        private void OnAnimatorIK(int layerIndex)
        {
           //No need to use NavMeshAgent here
        }
    }
}