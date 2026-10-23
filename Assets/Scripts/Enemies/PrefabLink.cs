using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Points at a prefab from outside it. A prefab can't keep a link to itself (it would point at its own copy once
    /// in the game), so e.g. Cockroach_Small, whose rotten pumpkin bursts into more Cockroach_Small, links to it through
    /// one of these. Assets > Create > 429 Game > Enemies > Prefab Link.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Enemies/Prefab Link", fileName = "Prefab Link")]
    public class PrefabLink : ScriptableObject
    {
        public GameObject prefab;
    }
}
