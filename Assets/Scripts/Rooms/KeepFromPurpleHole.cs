using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// The purple hole never eats this object or anything under it (2026-11-23, gate 3's request): e.g. a gate's chests
    /// still waiting to be opened when the player dies and is revived mid-gate. Gate Run puts it on every chest it drops.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeepFromPurpleHole : MonoBehaviour
    {
    }
}
