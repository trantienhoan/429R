using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// Spreads a room set's spawning over a few frames (smoothness pass, 2026-11-19). GAMESTAGES switches a set on
    /// (--Bedroom--, --MiniBedroom_Chaos-- and the like) and each spawn point in it makes its furniture the moment it is
    /// switched on, all in one frame: a 60-160 ms freeze in the editor test, at every room. So the set's simple spawn
    /// points (Points) are kept switched off in the scene, and this switches them on Per Frame at a time when the set
    /// comes on: the room fills in about a third of a second, under the smoke. Each point goes off again the frame after
    /// it has made its thing (only the thing stays), ready to make it again the next time the set comes on, as before.
    /// Don't remove this without switching its points back on (Tools > 429 Game > Performance > Restore Room Set Spawns),
    /// or the set makes nothing. Added by Tools > 429 Game > Performance > Spread Room Set Spawns.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaggeredSpawn : MonoBehaviour
    {
        [Tooltip("Spawn points switched on Per Frame at a time when this set comes on. Kept switched off in the scene.")]
        [SerializeField] private GameObject[] points = System.Array.Empty<GameObject>();
        [Tooltip("Spawn points switched on each frame. Each makes one thing (about 1.5 ms in the editor).")]
        [Min(1)]
        [SerializeField] private int perFrame = 2;

        public IReadOnlyList<GameObject> Points => points;

        private void OnEnable()
        {
            StartCoroutine(LetGo());
        }

        private IEnumerator LetGo()
        {
            var justOn = new List<(GameObject point, int children)>();
            int next = 0;
            while (next < points.Length || justOn.Count > 0)
            {
                // Last frame's points have made their things: off again, so they wait for the set next time too.
                foreach (var (point, children) in justOn)
                {
                    if (point != null && point.activeSelf && Done(point, children)) point.SetActive(false);
                }
                justOn.Clear();
                for (int n = 0; n < perFrame && next < points.Length; next++)
                {
                    var point = points[next];
                    if (point == null || point.activeSelf) continue;
                    int children = point.transform.childCount;
                    point.SetActive(true);
                    justOn.Add((point, children));
                    n++;
                }
                yield return null;
            }
        }

        // Its FSMs have all finished (a last state, its actions done), and it made nothing under itself.
        private static bool Done(GameObject point, int childrenBefore)
        {
            if (point.transform.childCount != childrenBefore) return false;
            foreach (var fsm in point.GetComponents<PlayMakerFSM>())
            {
                if (fsm.Fsm == null || !fsm.Fsm.Finished) return false;
            }
            return true;
        }
    }
}
