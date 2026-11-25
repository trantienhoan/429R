using Game.Saving;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The start rooftop told by the story, three ways (user 2026-11-25):
    /// - First Meeting (a new save; the player has never met the Fairy): the dark walk, no guide Knight, only her light
    ///   from afar and her calls for help; the pumpkin drops on her, she barely dodges it by teleporting (a green flash,
    ///   Story Fairy Acts), it laughs and the rooftop lights up.
    /// - Fairy Gone (met her, the final boss not beaten yet): she is missing, only the empty purple jail; the rooftop path
    ///   lights up slowly, the guide Knight stands on it; near the jail the pumpkin drops as normal.
    /// - Fairy Free (the final boss beaten once): she is there with no cage and no lock tower; touching her brings up her
    ///   board (Story Fairy Board): Gate 1, 2, 3 (seeds as now; she opens the way) or the first chest's seed (no pumpkin).
    /// Both flags are save unlocks (per save slot): Story_Met_Fairy (set when the first meeting's pumpkin drops: Story
    /// Start The Start) and Story_Boss_Beaten (GAMESTAGES "Thank For Playing": Story Remember). Saves made before this
    /// change with any progress (a level or EXP, a gate bought) count as having met her (user's call).
    /// </summary>
    public static class StoryStart
    {
        public const string MetFairy = "Story_Met_Fairy";
        public const string BossBeaten = "Story_Boss_Beaten";

        public enum Mode
        {
            FirstMeeting,
            FairyGone,
            FairyFree,
        }

        /// <summary>The way the start rooftop goes for this save now.</summary>
        public static Mode Current
        {
            get
            {
                if (Unlocks.Has(BossBeaten)) return Mode.FairyFree;
                if (Unlocks.Has(MetFairy) || HasProgress()) return Mode.FairyGone;
                return Mode.FirstMeeting;
            }
        }

        /// <summary>Saves this for good in the save slot (an unlock), if it isn't yet.</summary>
        public static void Remember(string id)
        {
            if (!Unlocks.Has(id)) Unlocks.Add(id);
        }

        // A save from before the story start: any progress counts as having met her.
        private static bool HasProgress()
        {
            var level = Game.Leveling.PlayerLevel.Instance;
            if (level != null && (level.Level > 1 || level.Exp > 0)) return true;
            return Unlocks.Has("Cube_1") || Unlocks.Has("Cube_2") || Unlocks.Has("Cube_3");
        }

        /// <summary>
        /// Dresses the Fairy just made on the start rooftop for this way of the story: missing (only the empty jail, her
        /// calls quiet) or free (no cage, no lock tower, her full glow, her board).
        /// </summary>
        public static void Dress(GameObject fairy, Mode mode, GameObject seed)
        {
            if (fairy == null) return;
            if (mode == Mode.FirstMeeting)
            {
                // She dodges the first pumpkin by teleporting (user 2026-11-25).
                Acts(fairy).WatchForPumpkin();
                return;
            }
            foreach (var voice in fairy.GetComponents<AudioSource>()) voice.mute = true;
            if (mode == Mode.FairyGone)
            {
                // Only the jail is left: her body (and anything on it) is not shown, and her weak glow stops.
                foreach (Transform child in fairy.transform)
                {
                    if (!child.name.StartsWith("Fairy_Girl")) continue;
                    foreach (var look in child.GetComponentsInChildren<Renderer>(true)) look.enabled = false;
                }
                var weak = fairy.GetComponent<StoryWeakFairy>();
                if (weak != null) Object.Destroy(weak);
                return;
            }
            // No jail any more: the cage, the lock tower and the jail's purple beams and skulls (her Spot Light children).
            foreach (Transform child in fairy.transform)
                if (child.name == "The_Cage" || child.name == "Fairy_Lock_Tower" || child.name.StartsWith("Spot Light"))
                    child.gameObject.SetActive(false);
            var weakOne = fairy.GetComponent<StoryWeakFairy>();
            if (weakOne != null) weakOne.Free();
            var board = fairy.GetComponent<StoryFairyBoard>();
            if (board == null) board = fairy.AddComponent<StoryFairyBoard>();
            board.SeedPrefab = seed;
            // Her free idle; she looks at the player, or at her board's card (user 2026-11-25).
            Acts(fairy).BeFree();
        }

        private static StoryFairyActs Acts(GameObject fairy)
        {
            var acts = fairy.GetComponent<StoryFairyActs>();
            return acts != null ? acts : fairy.AddComponent<StoryFairyActs>();
        }

        /// <summary>The Fairy made on the start rooftop (the one with the weak-glow script, or by name).</summary>
        public static GameObject FindFairy()
        {
            var weak = Object.FindAnyObjectByType<StoryWeakFairy>();
            if (weak != null) return weak.gameObject;
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (fsm.FsmName == "FairyHealth" && fsm.gameObject.name.StartsWith("Fairy")) return fsm.gameObject;
            return null;
        }
    }
}
