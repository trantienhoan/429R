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
    /// Grenade). The player is never hurt by it; Explode only says whether they were in reach.
    /// </summary>
    public static class Blast
    {
        // Spawned effects are removed after this many seconds; they don't remove themselves.
        private const float EffectLifetime = 4f;
        // Sent to things in the blast that have a Damage FSM but no health (see Grenade).
        private const string BlastEvent = "Blast";

        private static readonly Collider[] hits = new Collider[256];
        private static readonly HashSet<GameObject> hurt = new();
        private static readonly HashSet<Rigidbody> pushed = new();
        private static readonly List<PlayMakerFSM> fsms = new();

        /// <summary>
        /// Explodes at 'point', leaving out 'self' (the thing exploding). Returns whether the player was in reach.
        /// </summary>
        public static bool Explode(Vector3 point, float radius, float damage, float push, Rigidbody self,
            GameObject[] effects, AudioClip sound, float volume)
        {
            foreach (var effect in effects)
            {
                if (effect != null) UnityEngine.Object.Destroy(UnityEngine.Object.Instantiate(effect, point, Quaternion.identity), EffectLifetime);
            }
            if (sound != null) AudioSource.PlayClipAtPoint(sound, point, volume);

            bool player = false;
            hurt.Clear();
            pushed.Clear();
            int count = Physics.OverlapSphereNonAlloc(point, radius, hits, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.attachedRigidbody == self) continue;
                if (IsPlayer(hit))
                {
                    player = true;
                    continue;
                }

                Hurt(hit.transform, point, damage);
                Push(hit.attachedRigidbody, point, radius, push);
            }
            return player;
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
            // Things in someone's hand stay there.
            if (target.TryGetComponent<XRGrabInteractable>(out var held) && held.isSelected) return;

            target.AddExplosionForce(push, point, radius, 0.5f, ForceMode.VelocityChange);
        }
    }
}
