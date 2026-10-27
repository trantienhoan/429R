using UnityEngine;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.VFX
{
    /// <summary>
    /// A firework show round this object while it's out, for a win (on Thank_You_GAME_OVER, the Thank You For Playing
    /// banner the end chest lets out). A volley goes up First Delay seconds after it appears, then one every Interval
    /// seconds until Show Seconds (the song), from the Spots in turn: both sides, the middle, one side, the other. When
    /// its PlayMaker FSM reaches Finale State (the banner flying away, "GO UP!") every spot goes up at once. The spots
    /// are round this object, turning with it (x right, y up, z its front; the banner faces the player, so -z is behind
    /// it). Each firework is its own object (it outlives the banner) and is gone after Firework Seconds.
    /// Tools > 429 Game > VFX > Fireworks For Thank You For Playing puts it on the banner.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireworkShow : MonoBehaviour
    {
        [Tooltip("The firework (Firework_Win).")]
        [SerializeField] private GameObject firework;
        [Min(0f)]
        [SerializeField] private float firstDelay = 0.3f;
        [Tooltip("Seconds between volleys.")]
        [Min(0.5f)]
        [SerializeField] private float interval = 4.5f;
        [Tooltip("Volleys keep going up until this many seconds after it appears (the Thank You For Playing song is 30 s).")]
        [Min(0f)]
        [SerializeField] private float showSeconds = 30f;
        [Tooltip("Where the fireworks go up from, round this object (x right, y up, z its front), in metres.")]
        [SerializeField] private Vector3[] spots = { new(-3.6f, -1.2f, -1.8f), new(3.6f, -1.2f, -1.8f), new(0f, -1.4f, -3f) };
        [Tooltip("When its FSM gets to this state, every spot goes up at once. Empty: no finale.")]
        [SerializeField] private string finaleState = "GO UP!";
        [Min(0.1f)]
        [SerializeField] private float fireworkScale = 1f;
        [Min(1f)]
        [SerializeField] private float fireworkSeconds = 5f;

        // Which spots go up in each volley, in turn (indexes into Spots).
        private static readonly int[][] Volleys = { new[] { 0, 1 }, new[] { 2 }, new[] { 1 }, new[] { 0 } };

        private float startedAt;
        private float nextVolley;
        private int volley;
        private bool finaleDone;
        private PlayMakerFSM fsm;

        private void OnEnable()
        {
            startedAt = Time.time;
            nextVolley = startedAt + firstDelay;
            volley = 0;
            finaleDone = false;
            TryGetComponent(out fsm);
        }

        private void Update()
        {
            if (firework == null || spots.Length == 0) return;
            if (!finaleDone && !string.IsNullOrEmpty(finaleState) && fsm != null && fsm.ActiveStateName == finaleState)
            {
                finaleDone = true;
                for (int i = 0; i < spots.Length; i++) Launch(i);
                return;
            }
            if (finaleDone || Time.time < nextVolley || Time.time - startedAt > showSeconds) return;
            nextVolley = Time.time + interval;
            foreach (int i in Volleys[volley % Volleys.Length])
                if (i < spots.Length) Launch(i);
            volley++;
        }

        private void Launch(int spot)
        {
            var at = transform.position + transform.rotation * spots[spot];
            var show = Instantiate(firework, at, Quaternion.identity, SpawnedStuff());
            show.transform.localScale = Vector3.one * fireworkScale;
            Destroy(show, fireworkSeconds);
        }

        // Under CurrentlySpawnedStuffs, so a new run clears any that are left.
        private static Transform SpawnedStuff()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f);
            foreach (var spot in spots)
            {
                var at = transform.position + transform.rotation * spot;
                Gizmos.DrawWireSphere(at, 0.25f);
                Gizmos.DrawLine(at, at + Vector3.up * 2.4f * fireworkScale);
            }
        }
    }
}
