using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Game.Locomotion
{
    /// <summary>
    /// Falling: stepping (or teleporting) over a Fall Edge (the view drops Edge Drop metres while it blacks out), or
    /// falling out of the world (below Fall Below, e.g. through a hole in the ground; the view fades out). Either way
    /// they're put back where they stood a few seconds before they fell, it costs them health (Damage Event to their
    /// PlayerHealth FSM: FallDamage = 55) and the view fades back in.
    /// Not their doing, no fall: a Fall Edge that has only just appeared round them (a new room popped up while they
    /// stood outside it) or that the game put them into (Game Over, a checkpoint) just moves them inside, without the
    /// drop or the damage. Where they stood is kept while they're on solid ground, never over a Fall Edge; with nowhere
    /// kept, they go to the middle of the base (0, 0, 0). Goes on the XR Origin.
    /// </summary>
    [DisallowMultipleComponent]
    public class FallOutOfWorld : MonoBehaviour
    {
        [Tooltip("Height (world Y, metres) the player has to fall below. The floors are at 0, the ground's underside at about -3.4.")]
        [SerializeField] private float fallBelow = -4f;
        [Tooltip("Put back where they stood this many seconds before they left the ground (further from the edge).")]
        [Min(0f)]
        [SerializeField] private float secondsBack = 2f;
        [Tooltip("Sent to the player's health FSM once they're back.")]
        [SerializeField] private string damageEvent = "FallDamage";
        [SerializeField] private string healthFsm = "PlayerHealth";
        [Min(0f)]
        [SerializeField] private float fadeOutSeconds = 0.3f;
        [Min(0f)]
        [SerializeField] private float fadeInSeconds = 0.6f;

        [Header("Fall Edges")]
        [Tooltip("Stepping over a Fall Edge: how far the view drops (metres), speeding up like a real fall, while it blacks out.")]
        [Min(0f)]
        [SerializeField] private float edgeDrop = 1f;
        [Tooltip("Seconds the drop and the blackout take.")]
        [Min(0.05f)]
        [SerializeField] private float edgeDropSeconds = 0.45f;
        [Tooltip("A Fall Edge younger than this (seconds) didn't get stepped into: it appeared round the player.")]
        [Min(0f)]
        [SerializeField] private float newEdgeSeconds = 1f;

        // Where they stood: the floor under the head, and when.
        private readonly List<(Vector3 spot, float time)> stood = new();
        private const float Every = 0.2f;
        private const float KeepFor = 10f;
        // Moving further than this (metres) in one frame isn't a step: the game (or a teleport) moved them.
        private const float Jump = 1f;
        private XROrigin origin;
        private CharacterController body;
        private XRBodyTransformer mover;
        private TeleportationProvider teleport;
        private PlayMakerFSM health;
        private Game.Combat.PlayerLaunch launch;
        private float nextLook;
        private float fellAt = -1f;
        private bool dropping;
        private bool hurts;
        private Vector3 dropFrom;
        private bool holding;
        private bool bodyWasOn;
        private bool moverWasOn;
        private bool hasLast;
        private Vector3 lastUnder;
        private float teleportedAt = -10f;

        private void Awake()
        {
            origin = GetComponent<XROrigin>();
            body = GetComponent<CharacterController>();
            mover = GetComponent<XRBodyTransformer>();
            teleport = GetComponentInChildren<TeleportationProvider>(true);
            if (teleport != null) teleport.locomotionEnded += OnTeleported;
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == healthFsm) health = fsm;
            }
        }

        private void OnDestroy()
        {
            if (teleport != null) teleport.locomotionEnded -= OnTeleported;
        }

        private void OnDisable()
        {
            Hold(false);
        }

        // The player teleported themselves: landing over an edge counts as stepping in.
        private void OnTeleported(LocomotionProvider _) => teleportedAt = Time.time;

        private void Update()
        {
            if (fellAt >= 0f)
            {
                fellAt += Time.deltaTime;
                if (dropping)
                {
                    float t = Mathf.Clamp01(fellAt / edgeDropSeconds);
                    transform.position = dropFrom + Vector3.down * (edgeDrop * t * t);
                }
                if (fellAt >= (dropping ? edgeDropSeconds : fadeOutSeconds)) PutBack();
                return;
            }
            var under = HeadOnFloor(transform.position.y);
            bool jumped = hasLast && Vector3.Distance(under, lastUnder) > Jump && Time.time - teleportedAt > 0.3f;
            lastUnder = under;
            hasLast = true;
            // Dead: the death fall plays; nothing here.
            if (health != null && health.ActiveStateName == "DEAD") return;

            if (transform.position.y < fallBelow)
            {
                Fall(false, true);
                return;
            }
            var edge = FallEdge.At(under);
            if (edge != null)
            {
                // Stepped in: the real fall. Appeared round them, or put there by the game: just moved inside.
                bool stepped = !jumped && edge.WorkingFor >= newEdgeSeconds;
                Fall(stepped && edgeDrop > 0f, stepped);
                return;
            }

            if (Time.time < nextLook) return;
            nextLook = Time.time + Every;
            if (!OnSolidGround(out var floor) || FallEdge.Covers(floor)) return;
            stood.Add((floor, Time.time));
            while (stood.Count > 0 && stood[0].time < Time.time - KeepFor) stood.RemoveAt(0);
        }

        // Fades out; over an edge the view also drops (they can't move meanwhile). 'hurt': it costs health once back.
        private void Fall(bool drop, bool hurt)
        {
            // Thrown over the edge (a boss hit, a bug's leap): the throw ends here, so it doesn't carry on after the
            // put-back. Before the body is held, while it's still on.
            StopLaunch();
            fellAt = 0f;
            dropping = drop;
            hurts = hurt;
            if (drop)
            {
                dropFrom = transform.position;
                Hold(true);
            }
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(drop ? edgeDropSeconds : fadeOutSeconds);
        }

        private void PutBack()
        {
            fellAt = -1f;
            dropping = false;
            var fade = HeadsetFade.Find();
            // Only moved (not fallen) and the edge has gone meanwhile (e.g. Game Over cleared the room): stay put.
            if (!hurts && FallEdge.At(HeadOnFloor(transform.position.y)) == null)
            {
                if (fade != null) fade.FadeIn(fadeInSeconds);
                return;
            }
            var spot = SpotBeforeFall();
            stood.Clear();

            // A throw that started during the fade-out doesn't carry on from the spot either.
            StopLaunch();
            // The head ends up over the spot, the feet on its floor (they keep facing the way they face).
            Hold(true);
            var head = HeadOnFloor(transform.position.y);
            var to = transform.position + new Vector3(spot.x - head.x, 0f, spot.z - head.z);
            to.y = spot.y;
            transform.position = to;
            Hold(false);
            hasLast = false;

            if (fade != null) fade.FadeIn(fadeInSeconds);
            // Already dead: no second death.
            if (hurts && health != null && health.ActiveStateName != "DEAD") health.SendEvent(damageEvent);
        }

        // Ends a flight from Player Launch (on this object, once anything has thrown the player), if one is going.
        private void StopLaunch()
        {
            if (launch == null) TryGetComponent(out launch);
            if (launch != null) launch.Stop();
        }

        // The body and its mover off while the rig is moved by hand (the body would stop it at the floor, and walking or
        // gravity would move it meanwhile); back on as they were.
        private void Hold(bool on)
        {
            if (on == holding) return;
            holding = on;
            if (on)
            {
                bodyWasOn = body != null && body.enabled;
                moverWasOn = mover != null && mover.enabled;
                if (bodyWasOn) body.enabled = false;
                if (moverWasOn) mover.enabled = false;
                return;
            }
            if (bodyWasOn && body != null) body.enabled = true;
            if (moverWasOn && mover != null) mover.enabled = true;
            Physics.SyncTransforms();
        }

        // The newest spot from at least Seconds Back before they left the ground; else the oldest kept; else the
        // middle of the base. Spots a Fall Edge covers now are skipped.
        private Vector3 SpotBeforeFall()
        {
            float before = stood.Count > 0 ? stood[^1].time - secondsBack : 0f;
            for (int i = stood.Count - 1; i >= 0; i--)
            {
                if (stood[i].time <= before && !FallEdge.Covers(stood[i].spot)) return stood[i].spot;
            }
            foreach (var (spot, _) in stood)
            {
                if (!FallEdge.Covers(spot)) return spot;
            }
            return FreeSpotNearMiddle();
        }

        // The middle of the base (0, 0, 0), or the nearest spot round it with room to stand (furniture or a pumpkin may
        // be in the middle) that no Fall Edge covers.
        private Vector3 FreeSpotNearMiddle()
        {
            for (float r = 0f; r <= 4f; r += 0.5f)
            {
                int steps = r == 0f ? 1 : 12;
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (FallEdge.Covers(p)) continue;
                    if (Physics.CheckCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.6f, 0.3f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                    return p;
                }
            }
            return Vector3.zero;
        }

        // Something solid right under the head at foot height: a floor, not a loose thing or the player's own body.
        private bool OnSolidGround(out Vector3 floor)
        {
            floor = default;
            bool found = false;
            var from = HeadOnFloor(transform.position.y) + Vector3.up * 0.4f;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<XROrigin>() != null) continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
                if (!found || hit.point.y > floor.y) floor = hit.point;
                found = true;
            }
            return found;
        }

        // The point under the head at this height.
        private Vector3 HeadOnFloor(float y)
        {
            var head = origin != null && origin.Camera != null ? origin.Camera.transform.position : transform.position;
            return new Vector3(head.x, y, head.z);
        }
    }
}
