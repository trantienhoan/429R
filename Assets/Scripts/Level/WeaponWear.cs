using UnityEngine;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Leveling
{
    /// <summary>
    /// Changes how fast a weapon wears out. Weapons made in PlayMaker lose 1 "health" (their Health FSM) on every hard
    /// hit and break at 0. With the player's Weapon Efficiency (a level-up stat) part of each point is given back, so with
    /// 20% less wear a hit costs 0.8. While Boost is above 1 (2 while it's held in two hands, Two Handed Grip) each hit
    /// costs that many times as much, so it can take more than the FSM did. Big losses (a pop by the Fairy's shield) are
    /// left alone. A worn weapon warns the player before it breaks: it cracks when its health first drops to Player
    /// Level's Crack Share of what it had at the first grab, then creaks at every hard hit, higher and higher (sounds on
    /// Player Level, Worn weapons); tree pumpkins (Exp When Broken) and things named in Player Level's No Crack Names
    /// (pumpkins, boxes) don't crack. Player Level puts it on each weapon the player picks up; nothing to set up.
    /// </summary>
    // Before the PlayMaker FSMs, so the weapon's Health FSM never sees the full loss.
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public class WeaponWear : MonoBehaviour
    {
        private const string HealthFsm = "Health";
        private const string HealthVariable = "health";
        // A hard hit costs 1; anything bigger isn't wear.
        private const float MostWear = 1.5f;
        // Weapons with less health than this at the first grab, or this much or more (the Knight's legendary gear), never
        // crack or creak.
        private const float FewestForCracks = 3f;
        private const float MostForCracks = 9000f;
        // The creaks go from this pitch at the crack up to the highest just before it breaks.
        private const float CreakPitch = 1f;
        private const float HighestCreakPitch = 1.4f;

        /// <summary>
        /// Extra wear set while the game runs (not saved): each hard hit costs this many times as much, e.g. 2 while the
        /// weapon is held in two hands (Two Handed Grip). 1 = none.
        /// </summary>
        [System.NonSerialized] public float boost = 1f;

        private FsmFloat health;
        private float last;
        // Its health at the first grab, and whether it has cracked yet.
        private float full;
        private bool cracked;
        // Not a real weapon (a tree pumpkin, a stage pumpkin, a box): it wears, but never cracks or creaks.
        private bool noCracks;
        private AudioSource voice;

        /// <summary>Starts looking after the thing if it's a weapon that wears out (and isn't looked after yet).</summary>
        public static void Watch(GameObject thing)
        {
            if (thing == null || thing.TryGetComponent<WeaponWear>(out _)) return;
            if (!thing.CompareTag("Weapon") && !thing.TryGetComponent<Game.Combat.WeaponPower>(out _)) return;

            foreach (var fsm in thing.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != HealthFsm) continue;
                var value = fsm.FsmVariables.FindFsmFloat(HealthVariable);
                if (value == null) continue;
                var wear = thing.AddComponent<WeaponWear>();
                wear.health = value;
                wear.last = value.Value;
                wear.full = value.Value;
                wear.noCracks = thing.TryGetComponent<ExpWhenBroken>(out _) || PlayerLevel.NoCracksFor(thing.name);
                return;
            }
        }

        private void Update()
        {
            if (health == null)
            {
                enabled = false;
                return;
            }

            float lost = last - health.Value;
            // What a hard hit really costs: less with Weapon Efficiency, more while boosted (then some more is taken).
            bool worn = lost > 0.001f && lost <= MostWear;
            if (worn) health.Value += lost * (1f - PlayerLevel.WeaponWearShare * Mathf.Max(0f, boost));
            last = health.Value;
            if (worn) Warn(health.Value);
        }

        // After a hard hit (with what it costs now): the crack once it's down to Crack Share, a creak at every hit after.
        private void Warn(float left)
        {
            float share = PlayerLevel.CrackShare;
            // Broken now: its own break sound plays.
            if (share <= 0f || left <= 0f || noCracks || full < FewestForCracks || full >= MostForCracks) return;
            float crackAt = full * share;
            if (left > crackAt)
            {
                // Mended above the crack again: it can crack again.
                cracked = false;
                return;
            }
            if (!cracked)
            {
                cracked = true;
                Play(PlayerLevel.CrackSound, PlayerLevel.CrackVolume, 1f);
                return;
            }
            float nearlyGone = 1f - Mathf.Clamp01(left / Mathf.Max(crackAt, 0.001f));
            Play(PlayerLevel.RandomCreak(), PlayerLevel.CreakVolume, Mathf.Lerp(CreakPitch, HighestCreakPitch, nearlyGone));
        }

        // From the weapon, where it is (a child of its own, so its FSMs' sounds are left alone).
        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || volume <= 0f) return;
            if (voice == null)
            {
                var go = new GameObject("Wear Sound");
                go.transform.SetParent(transform, false);
                voice = go.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.minDistance = 1f;
                voice.maxDistance = 15f;
            }
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume);
        }
    }
}
