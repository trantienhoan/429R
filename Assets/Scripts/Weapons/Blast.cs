using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Weapons
{
    /// <summary>
    /// The game's explosion, shared by Grenade and Overheat: its effects and sound play, everything in reach that can be
    /// hit loses health, and loose objects are pushed away. "Can be hit" is what the game's PlayMaker damage uses (see
    /// Grenade). The player in reach (their body or head, or holding the thing that explodes) is hurt like a bug bite:
    /// the Player Event (e.g. "Damage") goes to their Damage FSM. The effects come from the Effect Pool: copies kept
    /// ready, warmed up at load, and never drawn bigger than half the view.
    /// </summary>
    public static class Blast
    {
        // Sent to things in the blast that have a Damage FSM but no health (see Grenade).
        private const string BlastEvent = "Blast";

        private static readonly Collider[] hits = new Collider[256];
        private static readonly HashSet<GameObject> hurt = new();
        private static readonly HashSet<Rigidbody> pushed = new();
        private static readonly List<PlayMakerFSM> fsms = new();

        /// <summary>
        /// Explodes at 'point', leaving out 'self' (the thing exploding), its effects this many times their size. The
        /// player is hurt if they're in reach (within 'playerReach' of it, if that's given and shorter than the radius,
        /// e.g. a thrown blast Throw Power made bigger; what they hold is spared beyond it too), or 'held' (it went off in
        /// their hand): 'playerEvent' is sent to their Damage FSM (empty: never). Returns whether the player was hurt.
        /// </summary>
        public static bool Explode(Vector3 point, float radius, float damage, float push, Rigidbody self,
            GameObject[] effects, AudioClip sound, float volume, string playerEvent, bool held = false, float effectScale = 1f,
            float playerReach = -1f)
        {
            foreach (var effect in effects)
            {
                if (effect != null) EffectPool.Play(effect, point, effectScale);
            }
            if (sound != null) AudioSource.PlayClipAtPoint(sound, point, volume);

            float reachesPlayer = playerReach >= 0f ? Mathf.Min(playerReach, radius) : radius;
            bool player = held || HeadInReach(point, reachesPlayer);
            hurt.Clear();
            pushed.Clear();
            int count = Physics.OverlapSphereNonAlloc(point, radius, hits, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.attachedRigidbody == self) continue;
                if (IsPlayer(hit))
                {
                    if (Within(hit, point, reachesPlayer, radius)) player = true;
                    continue;
                }
                // What the player holds is spared by the extra reach too (a weapon in the other hand doesn't wear out).
                if (InHand(hit) && !Within(hit, point, reachesPlayer, radius)) continue;

                Hurt(hit.transform, point, damage);
                Push(hit.attachedRigidbody, point, radius, push);
            }

            if (!player || string.IsNullOrEmpty(playerEvent)) return false;
            Game.Combat.DamageVignette.HitFrom(point);
            SendToPlayer(playerEvent);
            return true;
        }

        // Whether the collider is within 'reach' of the blast (always, when that's the whole radius). Its box: the body is
        // a Character Controller, which has no exact closest point.
        private static bool Within(Collider hit, Vector3 point, float reach, float radius)
        {
            return reach >= radius || (hit.bounds.ClosestPoint(point) - point).sqrMagnitude <= reach * reach;
        }

        private static bool InHand(Collider hit)
        {
            var body = hit.attachedRigidbody;
            return body != null && body.TryGetComponent<XRGrabInteractable>(out var held) && held.isSelected;
        }

        /// <summary>Gets an explosion's effects ready at load, so the first one doesn't stutter.</summary>
        public static void Warm(GameObject[] effects)
        {
            if (effects == null) return;
            foreach (var effect in effects) EffectPool.Warm(effect);
        }

        // The head counts too: the body's collider may be switched off (e.g. while the player is thrown).
        private static bool HeadInReach(Vector3 point, float radius)
        {
            var eyes = Camera.main;
            return eyes != null && (eyes.transform.position - point).sqrMagnitude <= radius * radius;
        }

        /// <summary>Sends an event to the player's Damage FSM, e.g. "Damage" (a bug bite) or "DamageHeavy".</summary>
        public static void SendToPlayer(string eventName)
        {
            var player = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            if (player == null || player.Value == null) return;
            foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Damage") fsm.SendEvent(eventName);
            }
        }

        /// <summary>
        /// Whether it has a health FSM, and if it can be hit (a Damage FSM next to it), the health to lower.
        /// Also hands back its Damage FSM, if it has one.
        /// </summary>
        public static bool HasHealth(GameObject target, out HutongGames.PlayMaker.FsmFloat health, out PlayMakerFSM damageFsm)
        {
            health = null;
            damageFsm = null;
            target.GetComponents(fsms);
            PlayMakerFSM healthFsm = null;
            foreach (var fsm in fsms)
            {
                string name = fsm.FsmName;
                if (name == "Damage") damageFsm = fsm;
                else if (name.EndsWith("Health", StringComparison.OrdinalIgnoreCase)) healthFsm = fsm;
            }
            if (healthFsm == null) return false;

            // The player's and the fairy's health are never touched.
            if (damageFsm != null && healthFsm.FsmName != "PlayerHealth" && healthFsm.FsmName != "FairyHealth")
                health = healthFsm.FsmVariables.GetFsmFloat("health");
            return true;
        }

        /// <summary>The player's body, head and hands: everything under the XR Origin.</summary>
        public static bool IsPlayer(Collider collider)
        {
            return collider.GetComponentInParent<XROrigin>() != null;
        }

        // A script that handles hits itself (IHittable, e.g. the ghost tree or bush) is hit first. Otherwise it takes
        // 'damage' from the health its own Damage FSM would lower, so breaking, drops and GAMESTAGES flags happen as
        // usual. Colliders are often on children, so the nearest object upwards with a health FSM is the one hit. With
        // no health anywhere upwards, the nearest Damage FSM is sent "Blast".
        private static void Hurt(Transform part, Vector3 point, float damage)
        {
            var hittable = part.GetComponentInParent<Game.Combat.IHittable>();
            if (hittable is Component target)
            {
                if (hurt.Add(target.gameObject)) hittable.Hit(damage, point);
                return;
            }

            PlayMakerFSM nearestDamageFsm = null;
            for (var t = part; t != null; t = t.parent)
            {
                if (!HasHealth(t.gameObject, out var health, out var damageFsm))
                {
                    if (nearestDamageFsm == null) nearestDamageFsm = damageFsm;
                    continue;
                }

                if (health != null && hurt.Add(t.gameObject)) health.Value -= damage;
                return;
            }

            if (nearestDamageFsm != null && hurt.Add(nearestDamageFsm.gameObject)) nearestDamageFsm.SendEvent(BlastEvent);
        }

        private static void Push(Rigidbody target, Vector3 point, float radius, float push)
        {
            if (push <= 0f || target == null || target.isKinematic || !pushed.Add(target)) return;
            // The very heaviest things (the stone blocks that close a path) stay put.
            if (Game.Combat.PushWeight.IsImmovable(target)) return;
            // Things in someone's hand stay there.
            if (target.TryGetComponent<XRGrabInteractable>(out var held) && held.isSelected) return;

            target.AddExplosionForce(push, point, radius, 0.5f, ForceMode.VelocityChange);
        }
    }
}
