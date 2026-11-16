using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Balls
{
    /// <summary>
    /// Snooker-table contact rules, applied inside the physics step:
    /// - Ball vs ball: the contact normal is flattened onto the table, so a big ball hitting a small one pushes it
    ///   sideways instead of launching either one into the air. Ball-ball contacts are frictionless and bouncy.
    /// - Ball vs obstacles (rocks, trees, pumpkins, rim walls): flattened too, so obstacles act like solid
    ///   walls - balls bounce off (and take damage) instead of ramping or climbing over them. These bounces are
    ///   frictionless with one fixed bounciness, so a ball always comes off at the mirror angle (plannable bank shots;
    ///   the aim guide predicts them the same way).
    /// - Only contacts with the table floor are left untouched.
    /// </summary>
    public static class BallPhysics
    {
        static readonly HashSet<int> ballColliders = new HashSet<int>();
        // The backs of the holes' pits: dead, like a real pocket's, so a ball that hits one drops instead of bouncing out.
        static readonly HashSet<int> pocketBacks = new HashSet<int>();
        const float PocketBackBounce = 0.1f;
        static bool hooked;
        static float ballBounce = 0.92f, obstacleBounce = 0.8f;
        static int floorId;

        /// <summary>How bouncy a ball's hit on a wall, rock or tree is (the aim guide predicts bank shots with it).</summary>
        public static float ObstacleBounce => obstacleBounce;

        public static void SetFloor(Collider floor) => floorId = floor.GetInstanceID();

        public static void Hook(float bounce, float obstacle)
        {
            ballBounce = bounce;
            obstacleBounce = obstacle;
            if (hooked) return;
            Physics.ContactModifyEvent += Modify;
            Physics.ContactModifyEventCCD += Modify;
            hooked = true;
        }

        public static void Unhook()
        {
            if (!hooked) return;
            Physics.ContactModifyEvent -= Modify;
            Physics.ContactModifyEventCCD -= Modify;
            hooked = false;
            ballColliders.Clear();
            pocketBacks.Clear();
            floorId = 0;   // 429R: the next stage brings its own floor
        }

        /// <summary>A piece of a hole's back wall: a ball hitting it barely bounces (it drops into the pit).</summary>
        public static void RegisterPocketBack(Collider c) => pocketBacks.Add(c.GetInstanceID());

        /// <summary>Registers a ball collider; returns its id for <see cref="Unregister"/>.</summary>
        public static int Register(Collider c)
        {
            c.hasModifiableContacts = true;
            int id = c.GetInstanceID();
            ballColliders.Add(id);
            return id;
        }

        public static void Unregister(int id) => ballColliders.Remove(id);

        // Runs on physics worker threads: only reads the id set (written on the main thread between steps).
        static void Modify(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            for (int p = 0; p < pairs.Length; p++)
            {
                var pair = pairs[p];
                bool a = ballColliders.Contains(pair.colliderInstanceID);
                bool b = ballColliders.Contains(pair.otherColliderInstanceID);
                if (!a && !b) continue;
                if (pair.colliderInstanceID == floorId || pair.otherColliderInstanceID == floorId) continue;   // table: keep
                bool ballBall = a && b;
                float bounce = ballBall ? ballBounce
                    : pocketBacks.Contains(a ? pair.otherColliderInstanceID : pair.colliderInstanceID) ? PocketBackBounce : obstacleBounce;
                for (int i = 0; i < pair.contactCount; i++)
                {
                    Vector3 n = pair.GetNormal(i);
                    Vector3 h = new Vector3(n.x, 0f, n.z);
                    if (h.sqrMagnitude < 0.0025f)
                    {
                        if (!ballBall) continue;                         // resting on top of an obstacle
                        // A small ball wedged under a big one: still push them apart sideways, never
                        // into the floor (the floor would then shoot the small ball into the air).
                        Vector3 c = pair.position - pair.otherPosition;
                        Vector3 cf = new Vector3(c.x, 0f, c.z);
                        if (cf.sqrMagnitude < 1e-6f) cf = Vector3.right;
                        h = Vector3.Dot(n, c) >= 0f ? cf : -cf;
                    }
                    pair.SetNormal(i, h.normalized);
                    // Frictionless both ways: ball on ball (snooker), and ball on a wall, rock or tree, where it comes
                    // off at the mirror angle with a fixed loss (no spin or scraping deflects a bank shot).
                    pair.SetBounciness(i, bounce);
                    pair.SetDynamicFriction(i, 0f);
                    pair.SetStaticFriction(i, 0f);
                }
                pairs[p] = pair;
            }
        }

        /// <summary>
        /// Make a ball's spin match its rolling speed (keeps the spots rolling naturally, kills odd spin from hits).
        /// <paramref name="spin"/>: 1 = natural roll, 0 = sliding with no spin (stun), below 0 = backspin (a shot that
        /// will screw back). The table is frictionless, so this is only how it looks: the shot's spin is worked out in code.
        /// </summary>
        public static void RollWithVelocity(Rigidbody rb, float radius, float spin = 1f)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            rb.angularVelocity = Vector3.Cross(Vector3.up, v) * (spin / Mathf.Max(0.05f, radius));
        }
    }
}
