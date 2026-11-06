using System.Collections;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The end of the game's jackpot (user, 2026-11-06: the Thank You For Playing part "not really rewarding the Player
    /// much"): as the end chest opens, a big firework bursts over it, a miracle sound plays, and a stream of gold EXP orbs
    /// pours out of the chest into the player for Seconds while the EXP is paid in step with them (it's saved: the levels
    /// stay for the next run, unlike candy points, which a new run drops). Started by the PlayMaker action Play End Game
    /// Jackpot (category Bonus) in Treasure_Chest_GAMEOVER's Open_Chest state.
    /// </summary>
    public class EndGameJackpot : MonoBehaviour
    {
        private Vector3 at;
        private int exp;
        private float seconds;
        private int orbsPerBurst;
        private AudioClip sound;
        private GameObject[] effects;

        /// <summary>Starts it at a point (the chest's top).</summary>
        public static EndGameJackpot Play(Vector3 at, int exp, float seconds, int orbsPerBurst, AudioClip sound, GameObject[] effects)
        {
            var go = new GameObject("End Game Jackpot");
            var jackpot = go.AddComponent<EndGameJackpot>();
            jackpot.at = at;
            jackpot.exp = Mathf.Max(0, exp);
            jackpot.seconds = Mathf.Max(0.2f, seconds);
            jackpot.orbsPerBurst = Mathf.Max(1, orbsPerBurst);
            jackpot.sound = sound;
            jackpot.effects = effects ?? System.Array.Empty<GameObject>();
            return jackpot;
        }

        /// <summary>The EXP paid so far (Play tests).</summary>
        public int Paid { get; private set; }

        private IEnumerator Start()
        {
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                var made = Instantiate(effect, at + Vector3.up * 1.2f, Quaternion.identity);
                Destroy(made, 8f);
            }
            if (sound != null) AudioSource.PlayClipAtPoint(sound, at, 1f);

            const float Gap = 0.12f;
            int bursts = Mathf.Max(1, Mathf.RoundToInt(seconds / Gap));
            for (int i = 0; i < bursts; i++)
            {
                Game.Leveling.ExpOrbs.Burst(at + Random.insideUnitSphere * 0.15f, orbsPerBurst, 0.35f);
                // The EXP in step with the orbs (the rest with the last burst).
                int share = i < bursts - 1 ? exp / bursts : exp - Paid;
                if (share > 0)
                {
                    Game.Leveling.PlayerLevel.AddExp(share);
                    Paid += share;
                }
                yield return new WaitForSeconds(Gap);
            }
            yield return new WaitForSeconds(1.5f);
            Destroy(gameObject);
        }
    }
}
