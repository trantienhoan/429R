using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Watches a few enemies and sets an FSM bool once all of them are dead or gone, e.g. the Big Bee a boss ghost
    /// balloon let out: EnemyManager4 / EnemyManager / BigBeeDown. Made from code (Watch); it removes itself after.
    /// </summary>
    public class FlagWhenAllGone : MonoBehaviour
    {
        private readonly List<GameObject> watched = new();
        private SpiderBrain.DeathFlag flag;

        /// <summary>Sets the flag once every one of these is dead (right away if there are none).</summary>
        public static void Watch(IEnumerable<GameObject> enemies, SpiderBrain.DeathFlag flag, Transform parent)
        {
            if (flag == null || string.IsNullOrEmpty(flag.boolName)) return;
            var watcher = new GameObject("Flag When All Gone (" + flag.boolName + ")").AddComponent<FlagWhenAllGone>();
            if (parent != null) watcher.transform.SetParent(parent, false);
            watcher.flag = flag;
            watcher.watched.AddRange(enemies);
        }

        /// <summary>Sets an FSM bool the way a boss's Set On Death does.</summary>
        public static void Set(SpiderBrain.DeathFlag flag, Object context)
        {
            if (flag == null || string.IsNullOrEmpty(flag.boolName)) return;
            GameStages.SetFsmBool(flag.globalObject, flag.fsm, flag.boolName, flag.value, context);
        }

        private void Update()
        {
            watched.RemoveAll(e => !EnemyWave.IsAlive(e));
            if (watched.Count > 0) return;
            Set(flag, this);
            Destroy(gameObject);
        }
    }
}
