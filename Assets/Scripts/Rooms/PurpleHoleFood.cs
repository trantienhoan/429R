using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// The purple hole eats this stage-made thing even though nothing on it moves or takes hits (user 2026-11-26: "the
    /// nest seems to not be removed with the purple hole"). For looks-only props a room drops (the lair nests and egg
    /// piles: no collider, no Rigidbody), which the hole, looking for loose or breakable things, never saw. It still
    /// keeps everything else the hole keeps (candy, held things, keepers, anything bigger than its Biggest).
    /// </summary>
    [DisallowMultipleComponent]
    public class PurpleHoleFood : MonoBehaviour
    {
    }
}
