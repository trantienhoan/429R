using UnityEngine;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Inventory
{
    /// <summary>
    /// Leaves a trail of candy behind a pumpkin as it bounces around the room: one candy every Spacing metres along its
    /// path (measured along the floor), up to Max Candies, each picked at random from Candies. The candies drop from
    /// where the pumpkin is and fall to the floor, and they pass through the pumpkin that dropped them, so it can't
    /// smash its own trail. They go under the stage's CurrentlySpawnedStuffs, so the stage clean-up removes them like
    /// other drops. Goes on the pumpkin (the object with the Rigidbody).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class CandyTrail : MonoBehaviour
    {
        // The PlayMaker global that holds the current stage's spawned things.
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";

        [Tooltip("Candy prefabs; each drop picks one at random.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Tooltip("Metres travelled (along the floor) between two candies.")]
        [Min(0.05f)]
        [SerializeField] private float spacing = 0.6f;
        [Tooltip("The most candies this pumpkin drops, so a fast pumpkin doesn't flood the room (or the shop's economy).")]
        [Min(0)]
        [SerializeField] private int maxCandies = 20;
        [Tooltip("Moving slower than this (metres per second, along the floor) drops nothing, e.g. settling or slow rolling.")]
        [Min(0f)]
        [SerializeField] private float minSpeed = 1f;

        private Rigidbody body;
        private Collider[] ownColliders;
        private Vector3 lastPosition;
        private float travelled;
        private int dropped;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            lastPosition = body.position;
        }

        // Physics steps, where the pumpkin actually moves.
        private void FixedUpdate()
        {
            var position = body.position;
            var step = position - lastPosition;
            step.y = 0f;
            lastPosition = position;

            if (dropped >= maxCandies || candies.Length == 0) return;
            var velocity = body.linearVelocity;
            if (velocity.x * velocity.x + velocity.z * velocity.z < minSpeed * minSpeed) return;

            float length = step.magnitude;
            if (length <= 0f) return;

            travelled += length;
            while (travelled >= spacing && dropped < maxCandies)
            {
                travelled -= spacing;
                // Back along this step by what's left over, so candies sit evenly spaced even when it moves fast.
                Drop(position - step * (travelled / length));
            }
        }

        private void Drop(Vector3 at)
        {
            var prefab = candies[Random.Range(0, candies.Length)];
            if (prefab == null) return;

            var candy = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
            dropped++;

            foreach (var candyCollider in candy.GetComponentsInChildren<Collider>())
            {
                foreach (var own in ownColliders)
                {
                    if (own != null) Physics.IgnoreCollision(candyCollider, own);
                }
            }
        }

        private static Transform SpawnedStuff()
        {
            var variable = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return variable != null && variable.Value != null ? variable.Value.transform : null;
        }
    }
}
