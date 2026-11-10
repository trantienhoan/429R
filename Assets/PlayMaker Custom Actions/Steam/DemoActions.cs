using Game.Saving;
using Game.Steam;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Steam")]
    [Tooltip("Is this the free Steam demo? Sends Demo Event in the demo build (and in the Editor while Tools > 429 Game > " +
             "Steam Demo > Play As Demo is ticked), Full Game Event otherwise (empty: the state just finishes, FINISHED).")]
    public class IsDemoBuild : FsmStateAction
    {
        [Tooltip("Sent in the demo.")]
        public FsmEvent demoEvent;

        [Tooltip("Sent in the full game. Empty: FINISHED.")]
        public FsmEvent fullGameEvent;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: true in the demo.")]
        public FsmBool storeResult;

        public override void Reset()
        {
            demoEvent = null;
            fullGameEvent = null;
            storeResult = null;
        }

        public override void OnEnter()
        {
            bool demo = DemoBuild.IsDemo;
            if (storeResult != null && !storeResult.IsNone) storeResult.Value = demo;
            Finish();
            var send = demo ? demoEvent : fullGameEvent;
            if (send != null) Fsm.Event(send);
        }
    }

    [ActionCategory("Steam")]
    [Tooltip("Opens 429's Steam store page, where the player can wishlist or buy the game (the demo's ending). In the " +
             "Steam overlay (in VR: the SteamVR dashboard); without Steam, or with the overlay off, in the web browser.")]
    public class OpenSteamStorePage : FsmStateAction
    {
        [Tooltip("The game whose page opens. 0 = 429, the full game.")]
        public FsmInt appId;

        public override void Reset()
        {
            appId = 0;
        }

        public override void OnEnter()
        {
            SteamStore.Open(appId.Value > 0 ? (uint)appId.Value : DemoBuild.FullGameAppId);
            Finish();
        }
    }

    [ActionCategory("Save")]
    [Tooltip("The demo is over: after Delay (real seconds, e.g. so the store page opens first) it starts over from the " +
             "very beginning, a new run, keeping what the player earned (candy, items, level). Saves, then loads the scene " +
             "again, straight back into the same save slot (no start menu), like Re-Dream.")]
    public class RestartDemo : FsmStateAction
    {
        [Tooltip("Real seconds before it starts over.")]
        public FsmFloat delay;

        private float waited;

        public override void Reset()
        {
            delay = 2f;
        }

        public override void OnEnter()
        {
            waited = 0f;
        }

        public override void OnUpdate()
        {
            waited += Time.unscaledDeltaTime;
            if (waited < delay.Value) return;
            Finish();
            Checkpoints.StartNewRun();
            if (SaveManager.Instance != null) SaveManager.Instance.SaveNow();
            if (Time.timeScale == 0f) Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
