using System;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Game.Saving
{
    /// <summary>
    /// The player's run: the last stage start reached (e.g. "CP Bedroom"), and how many times this run they can still
    /// die and get back up where they fell (Revives Left: 1 a run, plus Extra Revives earned for good, e.g. from a
    /// special chest, plus the Extra Revive level-up bonus; Player Revive does the getting up). Each revive left is a
    /// lucky coin flipped when the player goes down (Revive Coins): a clover gets them up and uses one up, all skulls
    /// and the run is over. A run starts at the very start and ends when the player dies with no revive left (back to
    /// the start, a new run), beats the game, or picks "Re-Dream" in the pause menu's Quit box. Reaching a checkpoint
    /// saves the game at once (the Save Manager, with the small "Saved" sign). Starting the game in a save slot goes on
    /// from that slot's last checkpoint, with the revives left and the health kept by "Get a Rest". The Save Manager
    /// keeps it all, one save per slot. GAMESTAGES uses the PlayMaker actions "Set Checkpoint", "Go To Checkpoint",
    /// "Start New Run" and "Add Extra Revive" (Save category); PlayerDeath uses "Try Revive".
    /// </summary>
    public static class Checkpoints
    {
        /// <summary>Revives every run has, before any extra ones.</summary>
        public const int BaseRevives = 1;

        /// <summary>What the save keeps.</summary>
        [Serializable]
        public class SavedCheckpoint
        {
            public string checkpoint = "";
            // -1: never saved (a fresh run, all revives).
            public int revivesLeft = -1;
            public int extraRevives;
            // The player's health when they last picked Get a Rest (-1: none, they start with full health).
            public float restHealth = -1f;
        }

        /// <summary>Raised when anything here changes, so the save is written.</summary>
        public static event Action Changed;

        /// <summary>Raised when the player reaches a new checkpoint (after Changed): the Save Manager saves at once.</summary>
        public static event Action<string> Reached;

        /// <summary>
        /// Raised once the game has gone on from the save (Go To Checkpoint as the game starts: the player is at the
        /// checkpoint, or at the start). The loading screen waits for it.
        /// </summary>
        public static event Action Resumed;

        /// <summary>The checkpoint reached this run ("" = none: the game goes on from the start).</summary>
        public static string Current { get; private set; } = "";

        /// <summary>How many more times this run the player can die and get back up.</summary>
        public static int RevivesLeft { get; private set; } = BaseRevives;

        /// <summary>Revives added for good (every run gets them), e.g. from a special chest.</summary>
        public static int ExtraRevives { get; private set; }

        /// <summary>Revives from the Extra Revive level-up bonus (Player Level tells this how many; saved with the level).</summary>
        public static int LevelRevives { get; private set; }

        /// <summary>Whether the save has been read yet (until then the checkpoint isn't known). False until a save slot is picked.</summary>
        public static bool IsLoaded { get; private set; }

        /// <summary>Whether the game has gone on from the save in this scene yet (see Resumed).</summary>
        public static bool HasResumed { get; private set; }

        /// <summary>Revives a run starts with.</summary>
        public static int RevivesPerRun => BaseRevives + ExtraRevives + LevelRevives;

        /// <summary>How many times the player has died and got back up this run (the health HUD's heart cracks after one).</summary>
        public static int DeathsThisRun => Mathf.Max(0, RevivesPerRun - RevivesLeft);

        /// <summary>The player's health when they last picked Get a Rest, until it's given back (-1: none).</summary>
        public static float RestHealth { get; private set; } = -1f;

        /// <summary>
        /// The player is down and the revive coins are still in the air. Until they land the save says the run is over (a
        /// new run from the start), so quitting during the flip can't keep the checkpoint; a clover takes that back
        /// (StopDying), a game over ends the run for real (EndRun).
        /// </summary>
        public static bool IsDying { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = "";
            RevivesLeft = BaseRevives;
            ExtraRevives = 0;
            LevelRevives = 0;
            RestHealth = -1f;
            IsDying = false;
            Changed = null;
            Reached = null;
            Resumed = null;
            IsLoaded = false;
            HasResumed = false;
        }

        /// <summary>
        /// The scene (re)loaded: the checkpoint counts as not read until the Save Manager reads the save slot again, so Go
        /// To Checkpoint waits for that. The Save Manager calls it as the scene starts.
        /// </summary>
        public static void ForgetLoaded()
        {
            IsLoaded = false;
            HasResumed = false;
        }

        /// <summary>
        /// The player reached a checkpoint: the game is saved at once, and starting this save slot again (e.g. after Get a
        /// Rest) goes on from here. Reaching the same one again does nothing.
        /// </summary>
        public static void Reach(string checkpoint)
        {
            if (string.IsNullOrEmpty(checkpoint) || checkpoint == Current) return;
            Current = checkpoint;
            Changed?.Invoke();
            Reached?.Invoke(checkpoint);
        }

        /// <summary>
        /// The game is starting: where it goes on from (the saved checkpoint; "" = the very start). Go To Checkpoint calls
        /// it as the game starts; it raises Resumed.
        /// </summary>
        public static string Resume()
        {
            HasResumed = true;
            Resumed?.Invoke();
            return Current;
        }

        /// <summary>The player died: uses up one of this run's revives (true), or false when none is left.</summary>
        public static bool UseRevive()
        {
            if (RevivesLeft <= 0) return false;
            RevivesLeft--;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// The player died and stays down although revives were left (the lucky coins all landed on skulls): the run's
        /// revives are gone, so the game over starts a new run.
        /// </summary>
        public static void UseAllRevives()
        {
            if (RevivesLeft <= 0) return;
            RevivesLeft = 0;
            Changed?.Invoke();
        }

        /// <summary>
        /// The game is over (the player died with no revive left): the checkpoint to go back to while there's a revive
        /// left (only when nothing used them up as the player fell), or "" with a new run started (back to the start).
        /// </summary>
        public static string Die()
        {
            RestHealth = -1f;
            if (!string.IsNullOrEmpty(Current) && RevivesLeft > 0)
            {
                RevivesLeft--;
                Changed?.Invoke();
                return Current;
            }
            StartNewRun();
            return "";
        }

        /// <summary>A new run: no checkpoint, all revives (beating the game, "Re-Dream", dying with none left).</summary>
        public static void StartNewRun()
        {
            Current = "";
            RevivesLeft = RevivesPerRun;
            RestHealth = -1f;
            IsDying = false;
            Changed?.Invoke();
        }

        /// <summary>The revive coins are thrown (Try Revive): saved at once as if the run were over, until they land.</summary>
        public static void StartDying()
        {
            IsDying = true;
            var saves = SaveManager.Instance;
            if (saves != null) saves.SaveNow();
        }

        /// <summary>A clover got the player up again (or the flip was called off): the save keeps the run again, at once.</summary>
        public static void StopDying()
        {
            if (!IsDying) return;
            IsDying = false;
            var saves = SaveManager.Instance;
            if (saves != null) saves.SaveNow();
        }

        /// <summary>
        /// The player died with no revive left (Try Revive's game over): the run is over, so a new run starts and is saved
        /// at once. Quitting during the game over (Get a Rest, closing the game) then can't keep the checkpoint; Go To
        /// Checkpoint after dying goes back to the start as before.
        /// </summary>
        public static void EndRun()
        {
            StartNewRun();
            var saves = SaveManager.Instance;
            if (saves != null) saves.SaveNow();
        }

        /// <summary>One more revive every run, for good (and one more right now).</summary>
        public static void AddExtraRevive(int amount = 1)
        {
            if (amount <= 0) return;
            ExtraRevives += amount;
            RevivesLeft += amount;
            Changed?.Invoke();
        }

        /// <summary>
        /// Sets how many revives the Extra Revive level-up bonus gives every run (Player Level calls it, and saves the
        /// points itself). Give: one more revive right now for each new one too (just taken in the Level Up menu).
        /// </summary>
        public static void SetLevelRevives(int amount, bool give)
        {
            amount = Mathf.Max(0, amount);
            if (amount == LevelRevives) return;
            if (give && amount > LevelRevives) RevivesLeft += amount - LevelRevives;
            LevelRevives = amount;
            RevivesLeft = Mathf.Min(RevivesLeft, RevivesPerRun);
            Changed?.Invoke();
        }

        /// <summary>
        /// Puts the player standing at a point (e.g. 0,0,0, the middle of the rooms), keeping the way they face. Go To
        /// Checkpoint does it as the game goes on from a save slot's checkpoint. False with no XR rig.
        /// </summary>
        public static bool PlacePlayer(Vector3 at)
        {
            var origin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            var head = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            if (head == null) return false;

            // The rig moves so the head is right above the point and the floor is at its height.
            var rig = origin.transform;
            var under = new Vector3(head.position.x, rig.position.y, head.position.z);
            rig.position += at - under;
            // So the character controller on the rig doesn't put it back.
            Physics.SyncTransforms();
            return true;
        }

        /// <summary>Keeps the player's health for when they come back (Get a Rest in the pause menu).</summary>
        public static void Rest(float health)
        {
            RestHealth = health > 0f ? health : -1f;
            Changed?.Invoke();
        }

        /// <summary>The health kept by Get a Rest, given back once (-1: none). The Save Manager calls it when it reads the save slot.</summary>
        public static float TakeRestHealth()
        {
            float health = RestHealth;
            RestHealth = -1f;
            return health;
        }

        public static SavedCheckpoint ToSaveData()
        {
            // While the coins are in the air the run counts as lost (see IsDying).
            if (IsDying)
            {
                return new SavedCheckpoint
                {
                    checkpoint = "",
                    revivesLeft = RevivesPerRun,
                    extraRevives = ExtraRevives,
                    restHealth = -1f,
                };
            }
            return new SavedCheckpoint
            {
                checkpoint = Current,
                revivesLeft = RevivesLeft,
                extraRevives = ExtraRevives,
                restHealth = RestHealth,
            };
        }

        /// <summary>
        /// The saved checkpoint, from the Save Manager when the save slot is read (null with no save: a fresh run). The
        /// game goes on from it (Go To Checkpoint), with the revives left and the health kept by Get a Rest. Read the
        /// level first, so its revives count.
        /// </summary>
        public static void LoadSaveData(SavedCheckpoint saved)
        {
            ExtraRevives = saved != null ? Mathf.Max(0, saved.extraRevives) : 0;
            Current = saved != null && saved.checkpoint != null ? saved.checkpoint : "";
            RevivesLeft = saved != null && saved.revivesLeft >= 0 ? Mathf.Min(saved.revivesLeft, RevivesPerRun) : RevivesPerRun;
            RestHealth = saved != null && saved.restHealth > 0f ? saved.restHealth : -1f;
            IsDying = false;
            IsLoaded = true;
        }

        /// <summary>Forgets everything, extra revives too (Reset Progress).</summary>
        public static void Reset()
        {
            ExtraRevives = 0;
            Current = "";
            RestHealth = -1f;
            RevivesLeft = RevivesPerRun;
            IsDying = false;
        }
    }
}
