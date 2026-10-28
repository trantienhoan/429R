using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Makes a PlayMaker breakable that judges hits by their speed alone (a Damage FSM with collisionForce and a Check
    /// Force state, like the whack pumpkins) count only the player's hits (user, 2026-10-28), judged the way enemies are:
    /// as fast as the weapon moved where it hit (a swing lands with its tip, not its middle), times the weapon's Power (a
    /// hammer 2, a pillow 0.6). A weapon lying around that's shoved or dropped onto it doesn't count. One hit that passes
    /// the FSM's own threshold is one whack (Whack 6: over 12, Whack 7: over 15); the player's blast is one whack too.
    /// Punches are judged by the Fist itself. The FSM's own Collision Events are switched off while this is on, so it
    /// only hears hits through here. Goes on the breakable's root, next to its Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    public class FairHits : MonoBehaviour
    {
        [Tooltip("Only things with this tag count, as in the FSM's Collision Event.")]
        [SerializeField] private string weaponTag = "Weapon";

        private const string BlastEvent = "Blast";

        private readonly List<HutongGames.PlayMaker.Actions.CollisionEvent> muted = new();
        private bool started;

        // In Start, once the FSMs have loaded their actions.
        private void Start()
        {
            started = true;
            Mute();
        }

        private void OnEnable()
        {
            if (started) Mute();
        }

        // The FSM's own Collision Events would count any Weapon-tagged bump: they're off, and Play Maker Hits knows them.
        private void Mute()
        {
            if (muted.Count > 0) return;
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != PlayMakerHits.DamageFsm) continue;
                foreach (var state in fsm.FsmStates)
                {
                    foreach (var action in state.Actions)
                    {
                        if (action is not HutongGames.PlayMaker.Actions.CollisionEvent listens || !listens.Enabled) continue;
                        listens.Enabled = false;
                        muted.Add(listens);
                        PlayMakerHits.Muted.Add(listens);
                    }
                }
            }
        }

        private void OnDisable()
        {
            foreach (var listens in muted)
            {
                listens.Enabled = true;
                PlayMakerHits.Muted.Remove(listens);
            }
            muted.Clear();
        }

        private void OnCollisionEnter(Collision collision)
        {
            var body = collision.rigidbody;
            if (body == null || Fist.IsFistBall(body) || Fist.IsFlyingFist(body)) return;
            if (!string.IsNullOrEmpty(weaponTag) && !collision.gameObject.CompareTag(weaponTag)) return;
            // Only the player's: held, just thrown, or a shot.
            if (!HeldThings.ByPlayer(collision)) return;

            float speed = collision.relativeVelocity.magnitude;
            for (int i = 0; i < collision.contactCount; i++)
                speed = Mathf.Max(speed, body.GetPointVelocity(collision.GetContact(i).point).magnitude);
            float power = body.TryGetComponent(out WeaponPower weapon) ? weapon.Power : 1f;
            PlayMakerHits.Judge(gameObject, collision, speed * power);
        }

        /// <summary>The player's blast reached it: one whack, as the FSM's own "Blast" does.</summary>
        public void Blasted()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.enabled && fsm.FsmName == PlayMakerHits.DamageFsm) fsm.SendEvent(BlastEvent);
            }
        }
    }
}
