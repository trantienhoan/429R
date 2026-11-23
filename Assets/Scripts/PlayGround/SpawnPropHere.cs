using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// Puts a copy of a breakable house prop (a prefab with its own free Rigidbody: barrel, box, candy basket, clock ...)
    /// on this spot when the PlayGround comes out, the way the PlayGround's own *_Pos markers do it with PlayMaker's
    /// Create Object: at this spot's place and turn, x this spot's size, under the game's CurrentlySpawnedStuffs (cleared
    /// with the stage), because a free physics body can't ride the PlayGround as its child.
    /// Gate 1, 2026-11-23: the user's own house models in the PlayGround (Tools > 429 Game > PlayGround > Place Bedroom
    /// Models). In Prefab Mode the child "Look (editor only)" shows the prop where it will stand: move or turn this spot
    /// (not the look) to move it. The look is switched off as the game starts and isn't in builds (EditorOnly).
    /// </summary>
    public class SpawnPropHere : MonoBehaviour
    {
        public const string LookName = "Look (editor only)";

        [Tooltip("The prop to put here (a prefab).")]
        public GameObject prefab;

        /// <summary>The copy put here (null before Start, or once it's broken).</summary>
        public GameObject Spawned { get; private set; }

        private void Awake()
        {
            var look = transform.Find(LookName);
            if (look != null) look.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (prefab == null) return;
            var holder = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            var parent = holder != null && holder.Value != null ? holder.Value.transform : null;
            Spawned = Instantiate(prefab, transform.position, transform.rotation, parent);
            // this spot's size in the world (the PlayGround's x1.5 cancelled by Bedroom_Models), on the prefab's own scale
            var s = transform.lossyScale;
            var p = parent != null ? parent.lossyScale : Vector3.one;
            Spawned.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(s.x / p.x, s.y / p.y, s.z / p.z));
            Spawned.name = prefab.name;
        }
    }
}
