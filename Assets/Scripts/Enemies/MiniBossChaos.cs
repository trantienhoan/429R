using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The mini bosses' chaos rebirth (user, 2026-11-19): a mini boss (a Spider Brain of the Boss size: the spider,
    /// cockroach and bee bosses, plain and Pumpkin Butt) that the player beats without it ever hurting them comes back at
    /// once, Chance of the time, as its chaos version: Health Times the health, Damage Times its blows, EXP Times the EXP
    /// and a red aura, like the chaos pumpkins and the chaos pumpkin boss. Once only: a chaos one stays dead. It takes
    /// over everything that waited for the first one (its death flags, its waves, a swarm that runs until it's gone), so
    /// a stage goes on only when the chaos one is beaten too. The big slimes (also Boss size) are left out: they split and
    /// get their colour from the cauldron. Lives in Resources as "Mini Boss Chaos"; Tools > 429 Game > Enemies > Set Up
    /// Mini Boss Chaos Rebirth makes it.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Enemies/Mini Boss Chaos", fileName = "Mini Boss Chaos")]
    public class MiniBossChaos : ScriptableObject
    {
        public const string ResourceName = "Mini Boss Chaos";

        [Tooltip("The chance a mini boss beaten without hurting the player comes back as its chaos version. 0 = never.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 0.75f;
        [Tooltip("The chaos version has this many times the health...")]
        [Min(1f)]
        [SerializeField] private float healthTimes = 2f;
        [Tooltip("...its blows (bites, charges, bombs, web balls and stings) do this many times the damage (before the " +
                 "player's Toughness)...")]
        [Min(1f)]
        [SerializeField] private float damageTimes = 1.5f;
        [Tooltip("...and it gives this many times the EXP.")]
        [Min(1f)]
        [SerializeField] private float expTimes = 2.5f;
        [Tooltip("The red aura it wears (Prefabs/Effects/Chaos_Aura, the chaos pumpkins' and the chaos pumpkin boss's).")]
        [SerializeField] private GameObject aura;
        [Tooltip("How wide the aura is, in shares of its body's width.")]
        [Min(0.1f)]
        [SerializeField] private float auraWidth = 1.2f;
        [Tooltip("The puff where it comes back. Empty: none.")]
        [SerializeField] private GameObject rebirthEffect;
        [Min(0.1f)]
        [SerializeField] private float rebirthEffectSize = 2f;
        [Tooltip("The mini boss prefabs that can come back: each comes back as a copy of its own prefab.")]
        [SerializeField] private GameObject[] bosses = System.Array.Empty<GameObject>();

        private static MiniBossChaos loaded;
        private static bool looked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loaded = null;
            looked = false;
        }

        /// <summary>The settings (Resources/Mini Boss Chaos), or null: then no mini boss comes back.</summary>
        public static MiniBossChaos Settings
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    loaded = Resources.Load<MiniBossChaos>(ResourceName);
                }
                return loaded;
            }
        }

        public float Chance => chance;
        public float HealthTimes => healthTimes;
        public float DamageTimes => damageTimes;
        public float ExpTimes => expTimes;
        public GameObject Aura => aura;
        public float AuraWidth => auraWidth;

        /// <summary>The prefab an enemy was made from, by its name ("Spider_Boss(Clone)" is Spider_Boss), or null.</summary>
        public GameObject PrefabFor(string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName) || bosses == null) return null;
            int cut = instanceName.IndexOf('(');
            string name = (cut >= 0 ? instanceName.Substring(0, cut) : instanceName).Trim();
            foreach (var boss in bosses)
            {
                if (boss != null && boss.name == name) return boss;
            }
            return null;
        }

        /// <summary>The puff where a chaos one comes back, sized for a mini boss of this Size (its scale).</summary>
        public void PlayRebirth(Vector3 at, float size)
        {
            if (rebirthEffect != null) Game.EffectPool.Play(rebirthEffect, at + Vector3.up * 0.2f, rebirthEffectSize * Mathf.Max(0.5f, size), 2f);
        }
    }
}
