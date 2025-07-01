using UnityEngine;
using Core;

namespace Enemies
{
    public class ShadowMonsterSpiderSmol : ShadowMonster
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string pickupAnimationName = "Pickup";
        [SerializeField] private string idleAnimationName = "Idle";
        [SerializeField] private string jumpAnimationName = "Jump";

        private bool isGrounded;

        private void Awake()
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                Debug.LogError("HealthComponent not found on " + gameObject.name);
                enabled = false;
            }

            if (animator == null)
            {
                Debug.LogError("Animator not assigned on " + gameObject.name);
                enabled = false;
            }
        }

        private void Start()
        {
            // Initial state
            PlayIdleAnimation();
        }

        private void Update()
        {
            //Simple isGrounded detection using Raycast
            isGrounded = Physics.Raycast(transform.position, Vector3.down, 0.1f);

            if (!isGrounded)
            {
                PlayJumpAnimation();
            }
            else
            {
                PlayIdleAnimation();
            }
        }

        public void OnPickup()
        {
            PlayPickupAnimation();
        }

        private void PlayPickupAnimation()
        {
            animator.CrossFade(pickupAnimationName, 0.2f);
        }

        private void PlayIdleAnimation()
        {
            animator.CrossFade(idleAnimationName, 0.2f);
        }

        private void PlayJumpAnimation()
        {
            animator.CrossFade(jumpAnimationName, 0.2f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            //Example of the logic to detect the Player pickup the monster
            if (collision.gameObject.CompareTag("Player"))
            {
                OnPickup();
            }
        }
    }
}