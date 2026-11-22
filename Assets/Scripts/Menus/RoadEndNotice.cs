using System.Linq;
using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// The start rooftop's long south arm leads nowhere yet (user, 2026-11-20: "when Player get to the end of the road, show
    /// a notice"): when the player's head gets to its end, the Tutorial Notice shows a short note (the game pauses; a
    /// trigger or a punch closes it). It shows again only after they walk away and come back.
    /// Only on a rooftop that came at the start of a run (GAMESTAGES was in Only When Spawned In as it came): the same
    /// rooftop comes back for the end-game fights, and a pausing notice there would get in the way.
    /// On Rooftop_Base_Z; Tools > 429 Game > Menu > Road End Notice puts it there.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoadEndNotice : MonoBehaviour
    {
        [Tooltip("The road's end (this object's space): the note shows when the player's head is in it.")]
        [SerializeField] private Bounds end = new(new Vector3(0f, 2f, -31f), new Vector3(10.6f, 6f, 3.6f));
        [Tooltip("How far (metres) the head must go back out of the end before the note can show again.")]
        [Min(0f)]
        [SerializeField] private float leaveBy = 1.5f;
        [Tooltip("GAMESTAGES states this rooftop must have come in (empty = any).")]
        [SerializeField] private string[] onlyWhenSpawnedIn = { "Walk Tuttorial" };

        [Header("The note (user's words)")]
        [SerializeField] private string title = "NOTHING HERE YET";
        [TextArea]
        [SerializeField] private string body = "There is nothing here yet, please come back and break them pumpkins.\nThank you for being here though. Love u and be safe!";
        [SerializeField] private string closeLine = "Pull a trigger to go back";

        private bool active, inside;

        /// <summary>Whether this rooftop shows the note (it came at the start of a run).</summary>
        public bool Active => active;

        private void Start()
        {
            string state = StagesState();
            active = onlyWhenSpawnedIn == null || onlyWhenSpawnedIn.Length == 0 || onlyWhenSpawnedIn.Contains(state);
        }

        private void Update()
        {
            if (!active) return;
            var cam = Camera.main;
            if (cam == null) return;
            var at = transform.InverseTransformPoint(cam.transform.position);
            if (!inside)
            {
                if (!end.Contains(at)) return;
                var notice = TutorialNotice.Find();
                // Another notice is up (a tutorial, a level up): this one waits for it, as Show Notice does.
                var levelUp = FindAnyObjectByType<Game.Leveling.LevelUpNotice>();
                if (notice == null || notice.IsShowing || (levelUp != null && levelUp.IsShowing)) return;
                inside = true;
                notice.Show(new[] { new TutorialNotice.Page(title, body, null) }, closeLine);
            }
            else
            {
                var away = end;
                away.Expand(leaveBy * 2f);
                if (!away.Contains(at)) inside = false;
            }
        }

        private static string StagesState()
        {
            foreach (var fsm in FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (fsm.FsmName == "GAMESTAGES") return fsm.ActiveStateName;
            return "";
        }
    }
}
