using UnityEngine;
using Items;   // For TreeOfLightPot
using Enemies; // For ShadowMonsterSpawner events

namespace Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private AudioClip treeGrowthMusic;  // BGM when tree starts growing
        [SerializeField] private AudioClip monsterSpawnSfx;  // SFX on each monster spawn
        [SerializeField] private AudioClip lampBreakSfx;     // SFX on lamp break (global)
        [SerializeField] private AudioSource bgmSource;      // Background music source
        [SerializeField] private AudioSource sfxSource;      // SFX source

        [SerializeField] private TreeOfLightPot treePot;  // Added missing field

        private static GameManager _instance;
        public static GameManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Auto-find audio sources if not assigned
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

            bgmSource.loop = true;  // Loop BGM
        }

        private void Start()
        {
            // Subscribe to events
            ShadowMonsterSpawner.OnMonsterSpawned += PlayMonsterSpawnSfx;  // Assume added event in spawner
            Lamp.OnLampBroken += PlayLampBreakSfx;

            // Tree growth (assume TreeOfLightPot has OnGrowthStart event; add if not)
            if (treePot != null)
            {
                treePot.OnGrowthStart += PlayTreeGrowthMusic;
            }

            Debug.Log("Game Manager Started (Audio Events).");
        }

        private void OnDestroy()
        {
            // Unsubscribe
            ShadowMonsterSpawner.OnMonsterSpawned -= PlayMonsterSpawnSfx;
            Lamp.OnLampBroken -= PlayLampBreakSfx;
            if (treePot != null)
            {
                treePot.OnGrowthStart -= PlayTreeGrowthMusic;
            }
        }

        private void PlayTreeGrowthMusic()
        {
            if (bgmSource != null && treeGrowthMusic != null)
            {
                bgmSource.clip = treeGrowthMusic;
                bgmSource.Play();
            }
        }

        private void PlayMonsterSpawnSfx()
        {
            if (sfxSource != null && monsterSpawnSfx != null)
            {
                sfxSource.PlayOneShot(monsterSpawnSfx);
            }
        }

        private void PlayLampBreakSfx(Lamp lamp)
        {
            if (sfxSource != null && lampBreakSfx != null)
            {
                sfxSource.PlayOneShot(lampBreakSfx);
            }
        }
    }
}