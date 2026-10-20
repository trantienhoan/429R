using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Leaves fall from a tree when it gets hurt: a few on every hit that takes health (more for a bigger hit), and a
    /// big burst when it dies, which keeps falling after the tree is gone. It watches the health in the Health FSM on
    /// this object, so every kind of damage counts (spider bites, punches, thrown things, blasts). The leaves are the
    /// Leaves particle system, a child that only drops leaves when this tells it to; they flutter down, land and shrink
    /// away. Burst(n) drops some from anywhere else (e.g. PlayMaker's Call Method). Goes on the tree's root, next to
    /// its Health FSM; Tools > 429 Game > VFX > Add Leaf Burst To Pumpkin Tree sets it up.
    /// </summary>
    [DisallowMultipleComponent]
    public class LeafBurst : MonoBehaviour
    {
        [Tooltip("The falling leaves: a child particle system with no emission of its own.")]
        [SerializeField] private ParticleSystem leaves;
        [Tooltip("The FSM holding the tree's health, and its float variable.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        [Header("How many leaves")]
        [Tooltip("Leaves for any hit that takes health.")]
        [Min(0)]
        [SerializeField] private int perHit = 3;
        [Tooltip("More leaves for each point of health the hit takes.")]
        [Min(0f)]
        [SerializeField] private float perDamage = 2f;
        [Tooltip("The most leaves one hit (or hits close together) drops.")]
        [Min(1)]
        [SerializeField] private int mostPerHit = 14;
        [Tooltip("Hits closer together than this many seconds drop their leaves together (spider bites come fast).")]
        [Min(0f)]
        [SerializeField] private float gap = 0.15f;
        [Tooltip("Leaves that fall at once when the tree dies (its health reaches 0). They keep falling after it's gone.")]
        [Min(0)]
        [SerializeField] private int whenItDies = 40;

        private HutongGames.PlayMaker.FsmFloat health;
        private float lastHealth;
        private int waiting;
        private float nextBurst;
        private bool dead;

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                // Find, not Get: Get hands back a new, unconnected variable when the name doesn't exist.
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                break;
            }
            if (health != null) lastHealth = health.Value;
            else Debug.LogWarning($"[Leaf Burst] '{name}' has no '{healthFsm}' FSM with a float '{healthVariable}'; leaves only fall when told to.", this);
            // Playing with no emission of its own, so the leaves it's given move and fall.
            if (leaves != null && !leaves.isPlaying) leaves.Play();
        }

        private void Update()
        {
            if (health != null && !dead)
            {
                float now = health.Value;
                if (now < lastHealth - 0.0001f)
                {
                    if (now <= 0f)
                    {
                        dead = true;
                        waiting = 0;
                        FallAll();
                        return;
                    }
                    waiting = Mathf.Min(mostPerHit, waiting + perHit + Mathf.RoundToInt((lastHealth - now) * perDamage));
                }
                lastHealth = now;
            }
            if (waiting > 0 && Time.time >= nextBurst)
            {
                Burst(waiting);
                waiting = 0;
                nextBurst = Time.time + gap;
            }
        }

        /// <summary>Drops this many leaves now.</summary>
        public void Burst(int count)
        {
            if (leaves == null || count <= 0) return;
            if (!leaves.isPlaying) leaves.Play();
            leaves.Emit(count);
        }

        // The tree is dying: the last leaves fall on their own, left behind in the world, and go when they're done.
        private void FallAll()
        {
            if (leaves == null) return;
            Burst(whenItDies);
            leaves.transform.SetParent(null, true);
            var main = leaves.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            leaves.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            leaves = null;
        }
    }
}
