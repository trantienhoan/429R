using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Keeps this object turned to the player's head (the headset camera), staying upright: it only turns around its up
    /// axis. Unlike a Look At the $Player or $MainCam globals (the XR Origin and its camera offset, the middle of the play
    /// area), it follows where the player really stands. E.g. the bonus round's pumpkin chest.
    /// </summary>
    [DisallowMultipleComponent]
    public class FaceThePlayer : MonoBehaviour
    {
        [Tooltip("Extra turn in degrees, for a model whose front isn't its forward (blue) axis.")]
        [SerializeField] private float turnOffset;
        [Tooltip("How fast it turns, in degrees per second. 0 = at once.")]
        [Min(0f)]
        [SerializeField] private float turnSpeed;

        private Transform head;

        private void LateUpdate()
        {
            if (head == null)
            {
                if (Camera.main == null) return;
                head = Camera.main.transform;
            }
            var to = head.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(to, Vector3.up) * Quaternion.Euler(0f, turnOffset, 0f);
            transform.rotation = turnSpeed > 0f ? Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime) : target;
        }
    }
}
