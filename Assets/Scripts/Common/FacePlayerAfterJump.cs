using UnityEngine;

namespace Game
{
    /// <summary>
    /// After every jump (anything that sends it flying, e.g. the Damage FSM's Set Velocity when the player touches it),
    /// once it's back on the ground it turns round to face the player and stops spinning. Its front is its blue Z arrow,
    /// the side PlayMaker's Look At turns toward the target. Only turns it round its up/down line, so it stays upright.
    /// With Face Range it only pays attention to a player that close: it keeps facing them (also while it waits), and
    /// with the player farther away it doesn't turn at all, not even after a jump.
    /// Goes on a pumpkin with a Rigidbody (Pumpkin_3, 5, 7). Tools > 429 Game > Items > Pumpkins Face The Player After
    /// Jumping adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class FacePlayerAfterJump : MonoBehaviour
    {
        [Tooltip("Moving faster than this (metres a second) counts as a jump.")]
        [Min(0.1f)]
        [SerializeField] private float jumpSpeed = 2.5f;
        [Tooltip("How fast it turns to face the player, in degrees a second.")]
        [Min(1f)]
        [SerializeField] private float turnSpeed = 300f;
        [Tooltip("Keep facing the player all the time, not only after a jump.")]
        [SerializeField] private bool keepFacing;
        [Tooltip("With the player farther than this (metres, along the floor) it pays them no attention: no turning, " +
                 "not even after a jump. Closer, it keeps facing them, also while it waits. 0: it turns to them after " +
                 "every jump, wherever they are, as before.")]
        [Min(0f)]
        [SerializeField] private float faceRange = 5f;

        // Slower than this (metres a second) counts as stopped.
        private const float StoppedSpeed = 0.15f;

        private Rigidbody body;
        private Transform head;
        private bool jumped;
        private bool turning;
        private float jumpedAt;
        private float groundedUntil;

        // Touching something it can stand on, in the last physics step.
        private bool Grounded => Time.fixedTime <= groundedUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            var v = body.linearVelocity;
            if (v.magnitude > jumpSpeed && !Grounded)
            {
                if (!jumped) jumpedAt = Time.fixedTime;
                jumped = true;
                turning = false;
            }
            // Landed: back on the ground and no longer going up (a moment after the take-off, which starts on the ground).
            else if (jumped && Grounded && v.y < 0.5f && Time.fixedTime - jumpedAt > 0.15f)
            {
                jumped = false;
                turning = true;
            }

            if (!turning && !keepFacing && faceRange <= 0f) return;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;

            var toPlayer = head.position - body.position;
            toPlayer.y = 0f;
            if (faceRange > 0f)
            {
                // Too far away: it pays the player no attention, not even after a jump.
                if (toPlayer.magnitude > faceRange)
                {
                    turning = false;
                    return;
                }
                // Close by: it keeps facing them, except in the air.
                if (!jumped) turning = true;
            }
            if (!turning && !keepFacing) return;
            if (toPlayer.sqrMagnitude < 0.0001f) return;

            var facing = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            // Its own spin is stopped, so it doesn't turn away again once it's facing the player.
            body.angularVelocity = Vector3.zero;
            var next = Quaternion.RotateTowards(body.rotation, facing, turnSpeed * Time.fixedDeltaTime);
            body.MoveRotation(next);
            // Done once it faces the player and has stopped sliding (sliding on changes where the player is from it).
            if (Quaternion.Angle(next, facing) < 0.5f && v.sqrMagnitude < StoppedSpeed * StoppedSpeed) turning = false;
        }

        private void OnCollisionStay(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                // Something under it (a floor, a table), not a wall at its side.
                if (collision.GetContact(i).normal.y > 0.6f)
                {
                    groundedUntil = Time.fixedTime + Time.fixedDeltaTime * 1.5f;
                    return;
                }
            }
        }
    }
}
