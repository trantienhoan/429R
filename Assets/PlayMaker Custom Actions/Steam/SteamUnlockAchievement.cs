using Game.Steam;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Steam")]
    [Tooltip("Unlocks a Steam achievement. Use the achievement's API name from the Steamworks site. " +
             "Does nothing on Quest or when Steam isn't running, so it's safe in every build. Unlocking one twice is harmless.")]
    public class SteamUnlockAchievement : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The achievement's API name, exactly as on the Steamworks site, e.g. BEAT_THE_SPIDER.")]
        public FsmString achievementName;

        public override void Reset()
        {
            achievementName = "";
        }

        public override void OnEnter()
        {
            SteamAchievements.Unlock(achievementName.Value);
            Finish();
        }
    }
}
