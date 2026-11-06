using System.Collections;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// A fountain of candies (user, 2026-11-06: the cooking-round chests): Count candies, each one of Candies at random,
    /// tossed gently up and out of a point over Seconds, into CurrentlySpawnedStuffs (cleared with the stage). It runs on
    /// its own object, so it isn't cut short when the state that started it moves on. Started by the PlayMaker action
    /// Spill Candies (category Bonus).
    /// </summary>
    public class CandyFountain : MonoBehaviour
    {
        private Vector3 at;
        private int count;
        private GameObject[] candies;
        private float seconds;
        private Vector2 up, side;

        /// <summary>Starts a fountain at a point.</summary>
        public static CandyFountain Play(Vector3 at, int count, GameObject[] candies, float seconds, Vector2 upSpeed, Vector2 outSpeed)
        {
            var fountain = new GameObject("Candy Fountain").AddComponent<CandyFountain>();
            fountain.at = at;
            fountain.count = Mathf.Max(0, count);
            fountain.candies = candies ?? System.Array.Empty<GameObject>();
            fountain.seconds = Mathf.Max(0f, seconds);
            fountain.up = upSpeed;
            fountain.side = outSpeed;
            return fountain;
        }

        /// <summary>
        /// Just above the top of a thing's solid colliders (candies made inside them would be shoved out hard enough to
        /// break: a candy breaks on a knock over 7). No colliders: above its meshes; nothing: its position + 0.45 m.
        /// </summary>
        public static Vector3 TopOf(GameObject thing)
        {
            if (thing == null) return Vector3.zero;
            bool any = false;
            var box = new Bounds();
            foreach (var c in thing.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                if (!any) { box = c.bounds; any = true; } else box.Encapsulate(c.bounds);
            }
            if (!any)
            {
                foreach (var r in thing.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled) continue;
                    if (!any) { box = r.bounds; any = true; } else box.Encapsulate(r.bounds);
                }
            }
            return any ? new Vector3(box.center.x, box.max.y + 0.15f, box.center.z) : thing.transform.position + Vector3.up * 0.45f;
        }

        private IEnumerator Start()
        {
            var holder = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            var parent = holder != null && holder.Value != null ? holder.Value.transform : null;
            float every = count > 0 ? seconds / count : 0f;
            for (int i = 0; i < count; i++)
            {
                One(parent);
                if (every > 0f) yield return new WaitForSeconds(every);
            }
            Destroy(gameObject);
        }

        private void One(Transform parent)
        {
            if (candies.Length == 0) return;
            var prefab = candies[Random.Range(0, candies.Length)];
            if (prefab == null) return;
            var spread = Random.insideUnitCircle * 0.25f;
            var made = Instantiate(prefab, at + new Vector3(spread.x, Random.Range(0f, 0.1f), spread.y), Random.rotation, parent);
            if (made.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
            {
                var dir = Random.insideUnitCircle.normalized * Random.Range(side.x, side.y);
                body.linearVelocity = new Vector3(dir.x, Random.Range(up.x, up.y), dir.y);
                body.angularVelocity = Random.insideUnitSphere * 6f;
            }
        }
    }
}
