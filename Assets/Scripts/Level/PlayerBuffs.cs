using System;
using Game.Locomotion;
using UnityEngine;

namespace Game.Leveling
{
    /// <summary>The timed bonuses a smoked boong (or the bonus round) gives the player.</summary>
    public enum Buff
    {
        /// <summary>Elon_Boong: punches reach 0.5 m further, hit twice as hard, and every punch that hits something explodes again.</summary>
        ElonBoong,
        /// <summary>Hammer_Boong_Bone: +7 Toughness points.</summary>
        BoneBoong,
        /// <summary>Hammer_Boong / Hammer_Boong 2ND (the green one): the player stands 0.5 m taller and flying punches fly 5 m further.</summary>
        GreenBoong,
        /// <summary>Bus_Boong: +7 Move Speed points.</summary>
        BusBoong,
        /// <summary>The bonus round's invisible Candy Helmet: every candy around flies into the player's mouth (Candy Helmet).</summary>
        CandyHelmet,
    }

    /// <summary>
    /// The timed bonuses a smoked boong gives the player (Boong Smoke calls Give). Each lasts 60 seconds of game time: the
    /// clock stops while the game is paused (pause menu, notices). Smoking the same boong again starts its time over;
    /// different boongs add up. Nothing is kept in the save, and a new scene (Re-Dream, a reload) starts with none.
    /// - Elon Boong: punches reach 0.5 m further (the fist's own punches and the flying ones), do double damage
    ///   (pumpkins and other PlayMaker breakables feel them twice as hard too), and every punch that hits anything
    ///   explodes once more where it hit, a small blast that never hurts the player (Fist: Elon Boong).
    /// - Bone Boong: +7 Toughness points (less damage and knockback), on top of the level-up points.
    /// - Green Boong: the player stands 0.5 m taller (eased in and out over 1.5 s), flying punches fly 5 m further, and
    ///   fists are 7 Fist Size points bigger (size only; the flying punches 70% bigger, the hand a little).
    /// - Bus Boong: +7 Move Speed points (faster walking).
    /// - Any boong also makes the player high while it's on: they take 1.5x damage, candy heals them 1.5x and they walk
    ///   30% slower (High; once, however many boongs are on).
    /// - Candy Helmet (every pumpkin the bonus round's player breaks adds 5 s, Add): every candy around flies to the
    ///   player, over their head and into the top of it, and is eaten, even ones still flying out of the pumpkin
    ///   (Game.Inventory.CandyHelmet does it).
    /// The extra points are never saved and never count as a stat's big last point; the Level Up menu keeps showing the
    /// player's own points. The health HUD shows them: while any is on the heart smokes white and stays in view, with a
    /// countdown under it for each (in the buff's colour, no names). With no buff on, everything is exactly as without this.
    /// </summary>
    public static class PlayerBuffs
    {
        public const float DefaultSeconds = 60f;

        /// <summary>How many kinds of buff there are (the values of Buff).</summary>
        public const int Count = 5;

        /// <summary>Elon Boong: how much further punches reach, in metres.</summary>
        public const float ElonReach = 0.5f;

        /// <summary>Elon Boong: punches' damage is multiplied by this.</summary>
        public const float ElonDamage = 2f;

        /// <summary>Bone Boong: extra Toughness points.</summary>
        public const int BoneToughness = 7;

        /// <summary>Green Boong: how much taller the player stands, in metres.</summary>
        public const float GreenHeight = 0.5f;

        /// <summary>Green Boong: how much further flying punches fly, in metres.</summary>
        public const float GreenFlyReach = 5f;

        /// <summary>Green Boong: Fist Size points' worth of bigger fists (size only: no damage, no new punches).</summary>
        public const int GreenFistSize = 7;

        /// <summary>Bus Boong: extra Move Speed points.</summary>
        public const int BusSpeed = 7;

        /// <summary>High (any boong buff on): the damage the player takes is multiplied by this.</summary>
        public const float HighDamageTaken = 1.5f;

        /// <summary>High: candy heals are multiplied by this.</summary>
        public const float HighHeal = 1.5f;

        /// <summary>High: the walking speed is multiplied by this (0.7 = 30% slower).</summary>
        public const float HighSpeed = 0.7f;

        // Seconds the eased bonuses take to come in and to go: the height (a snap is uncomfortable in VR) and the punch reach.
        private const float HeightEaseSeconds = 1.5f;
        private const float ReachEaseSeconds = 0.5f;

        private static readonly bool[] on = new bool[Count];
        // When each ends, in game time (Time.time).
        private static readonly float[] until = new float[Count];
        // How far each eased bonus has come in: 0 = none, 1 = all of it.
        private static readonly float[] eased = new float[Count];
        private static Runner runner;

        /// <summary>Raised when a buff starts or is refreshed.</summary>
        public static event Action<Buff> Given;

        /// <summary>Raised when a buff runs out.</summary>
        public static event Action<Buff> Ended;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClearState();
            runner = null;
            Given = null;
            Ended = null;
        }

        /// <summary>Starts a buff (or refreshes it to the full time) for 'seconds' of game time.</summary>
        public static void Give(Buff buff, float seconds = DefaultSeconds)
        {
            int i = (int)buff;
            if (i < 0 || i >= Count || seconds <= 0f || !EnsureRunner()) return;

            bool was = IsActive(buff);
            on[i] = true;
            until[i] = was ? Mathf.Max(until[i], Time.time + seconds) : Time.time + seconds;
            runner.enabled = true;
            if (!was) PointsChanged(buff);
            Debug.Log($"[Player Buffs] {buff} {(was ? "refreshed" : "on")} for {seconds:0} s.");
            Given?.Invoke(buff);
        }

        /// <summary>Adds 'seconds' of game time to the buff, on top of what it has left (starts it if it's off): it stacks.</summary>
        public static void Add(Buff buff, float seconds)
        {
            int i = (int)buff;
            if (i < 0 || i >= Count || seconds <= 0f || !EnsureRunner()) return;

            bool was = IsActive(buff);
            on[i] = true;
            until[i] = (was ? until[i] : Time.time) + seconds;
            runner.enabled = true;
            if (!was) PointsChanged(buff);
            Debug.Log($"[Player Buffs] {buff} {(was ? "+" : "on for ")}{seconds:0} s ({until[i] - Time.time:0} s left).");
            Given?.Invoke(buff);
        }

        /// <summary>Whether the buff is on right now.</summary>
        public static bool IsActive(Buff buff)
        {
            int i = (int)buff;
            return i >= 0 && i < Count && on[i] && Time.time < until[i] && runner != null;
        }

        /// <summary>Seconds of game time the buff has left (0 when it's off).</summary>
        public static float SecondsLeft(Buff buff) => IsActive(buff) ? until[(int)buff] - Time.time : 0f;

        /// <summary>Whether any buff is on.</summary>
        public static bool AnyActive
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    if (IsActive((Buff)i)) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// The player is high: a smoked boong's buff is on (Elon, Bone, Green or Bus; not the bonus round's Candy Helmet).
        /// They take more damage, candy heals them more and they walk slower, once however many boongs are on.
        /// </summary>
        public static bool High =>
            IsActive(Buff.ElonBoong) || IsActive(Buff.BoneBoong) || IsActive(Buff.GreenBoong) || IsActive(Buff.BusBoong);

        /// <summary>The damage the player takes is multiplied by this: High Damage Taken while high, else 1.</summary>
        public static float DamageTakenScale => High ? HighDamageTaken : 1f;

        /// <summary>Candy heals are multiplied by this: High Heal while high, else 1.</summary>
        public static float HealScale => High ? HighHeal : 1f;

        /// <summary>The walking speed is multiplied by this: High Speed while high, else 1.</summary>
        public static float SpeedScale => High ? HighSpeed : 1f;

        /// <summary>Ends every buff now (e.g. for testing); each one that was on raises Ended.</summary>
        public static void EndAll()
        {
            for (int i = 0; i < Count; i++)
            {
                if (!on[i]) continue;
                on[i] = false;
                PointsChanged((Buff)i);
                RaiseEnded((Buff)i);
            }
        }

        internal static void RaiseEnded(Buff buff) => Ended?.Invoke(buff);

        // What the buffs give, read by the things they change. Each is the same as with no buff when none is on.

        /// <summary>Metres the fist's own punching ball reaches past the knuckles (Elon Boong, eased in and out).</summary>
        public static float PunchReach => ElonReach * Smooth(eased[(int)Buff.ElonBoong]);

        /// <summary>Metres added to how far every flying punch flies (Elon Boong 0.5, Green Boong 5).</summary>
        public static float FlyReach =>
            (IsActive(Buff.ElonBoong) ? ElonReach : 0f) + (IsActive(Buff.GreenBoong) ? GreenFlyReach : 0f);

        /// <summary>What punches' damage is multiplied by (Elon Boong: 2, else 1).</summary>
        public static float PunchDamage => IsActive(Buff.ElonBoong) ? ElonDamage : 1f;

        /// <summary>Whether every punch that hits something explodes once more (Elon Boong).</summary>
        public static bool ExplodingPunches => IsActive(Buff.ElonBoong);

        /// <summary>Metres the player stands taller (Green Boong, eased in and out).</summary>
        public static float Height => GreenHeight * Smooth(eased[(int)Buff.GreenBoong]);

        /// <summary>
        /// Fist Size points' worth of bigger fists for now (Green Boong, eased in and out): the hand a little and the
        /// flying punches a lot bigger. Size only: they don't add damage, reach or unlock punches.
        /// </summary>
        public static float FistSizePoints => GreenFistSize * Smooth(eased[(int)Buff.GreenBoong]);

        /// <summary>Points the buffs add to a stat for now (Bone Boong: Toughness, Bus Boong: Move Speed), never saved.</summary>
        public static int ExtraPoints(Stat stat)
        {
            switch (stat)
            {
                case Stat.Toughness: return IsActive(Buff.BoneBoong) ? BoneToughness : 0;
                case Stat.MoveSpeed: return IsActive(Buff.BusBoong) ? BusSpeed : 0;
                default: return 0;
            }
        }

        /// <summary>Candy Helmet: candies lying around fly into the player's mouth.</summary>
        public static bool CandyHelmet => IsActive(Buff.CandyHelmet);

        /// <summary>The buff's colour on the health HUD: its countdown under the heart.</summary>
        public static Color ColorOf(Buff buff)
        {
            switch (buff)
            {
                case Buff.ElonBoong: return new Color(1f, 0.62f, 0.25f);
                case Buff.BoneBoong: return new Color(0.96f, 0.93f, 0.84f);
                case Buff.GreenBoong: return new Color(0.45f, 1f, 0.45f);
                case Buff.CandyHelmet: return new Color(1f, 0.45f, 0.8f);
                default: return new Color(1f, 0.86f, 0.2f);
            }
        }

        /// <summary>What the buff does, in a few words, e.g. "Far exploding punches" (for logs and tests).</summary>
        public static string Describe(Buff buff)
        {
            switch (buff)
            {
                case Buff.ElonBoong: return "Far exploding punches";
                case Buff.BoneBoong: return $"+{BoneToughness} Toughness";
                case Buff.GreenBoong: return "Taller, big far flying punches";
                case Buff.CandyHelmet: return "Candies fly to your head";
                default: return $"+{BusSpeed} Move Speed";
            }
        }

        // Eased in and out, not at a steady rate.
        private static float Smooth(float t) => t <= 0f ? 0f : t >= 1f ? 1f : t * t * (3f - 2f * t);

        // A boong buff started or ended (Bus adds Move Speed points, and any boong makes the player high and slower): the
        // walking speed is set again (Toughness and the damage and heal scales are read when needed).
        private static void PointsChanged(Buff buff)
        {
            if (buff != Buff.CandyHelmet) PlayerLevel.BuffsChanged();
        }

        private static void ClearState()
        {
            Array.Clear(on, 0, Count);
            Array.Clear(until, 0, Count);
            Array.Clear(eased, 0, Count);
            PlayerHeight.Bonus = 0f;
        }

        // The clock lives on an object in the scene, so a new scene starts without buffs. (The health HUD shows them.)
        private static bool EnsureRunner()
        {
            if (!Application.isPlaying) return false;
            if (runner != null) return true;
            // A new scene: the buffs of the old one went with it.
            ClearState();
            var go = new GameObject("Player Buffs");
            runner = go.AddComponent<Runner>();
            return runner != null;
        }

        private static float EaseSeconds(int i)
        {
            return i == (int)Buff.GreenBoong ? HeightEaseSeconds : ReachEaseSeconds;
        }

        // Ends the buffs whose time is up and eases the bonuses in and out; switches itself off once nothing is left.
        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                bool busy = false;
                float dt = Time.deltaTime;
                for (int i = 0; i < Count; i++)
                {
                    if (on[i] && Time.time >= until[i])
                    {
                        on[i] = false;
                        Debug.Log($"[Player Buffs] {(Buff)i} ended.");
                        PointsChanged((Buff)i);
                        RaiseEnded((Buff)i);
                    }
                    eased[i] = Mathf.MoveTowards(eased[i], on[i] ? 1f : 0f, dt / EaseSeconds(i));
                    if (on[i] || eased[i] > 0f) busy = true;
                }
                PlayerHeight.Bonus = Height;
                if (!busy) enabled = false;
            }

            private void OnDestroy()
            {
                if (runner != this) return;
                // The scene is closing (or this was removed): no buff carries on. The speed is set back if the scene
                // stays (the Player Level is gone too when the scene closes).
                bool speed = High;
                ClearState();
                runner = null;
                if (speed) PlayerLevel.BuffsChanged();
            }
        }
    }
}
