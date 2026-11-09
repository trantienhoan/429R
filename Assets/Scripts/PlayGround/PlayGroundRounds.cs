using System;
using System.Collections;
using System.Collections.Generic;
using Game.Enemies;
using Game.Inventory;
using UnityEngine;
using UnityEngine.AI;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.PlayGround
{
    /// <summary>
    /// Runs the PlayGround's three cooking rounds around the cauldron (goes next to the Cauldron; user, 2026-11-07):
    /// 1. A right recipe starts cooking: this round's wave starts (Waves, one per round). Its bugs come in from the edges
    ///    of the PlayGround (its Ghost Roam Area) non-stop, a few alive at a time, until the round's laughing pumpkin is
    ///    caught. The PlayGround Field drops the walls (GO) at the same moment. A wrong recipe starts nothing (the
    ///    cauldron spits a Big Slime by itself).
    /// 2. Cooked: the pot spits the laughing pumpkin, and the round's one mini boss (Boss) comes in with the wave.
    /// 3. Caught: the bugs stop coming (the ones alive stay), the pot's bubble tower goes off and the round's Chest drops
    ///    in the middle at once. Not caught in time, the pumpkin turns into the Pumpkin Boss instead: the bugs stop
    ///    coming, and there's no chest.
    /// 4. Taking the chest's Yellow Seed ends the round: the Fairy Shield sweeps the PlayGround, the wave's bugs left die,
    ///    the PlayGround Field clears the field and grows it, and the cauldron moves on to the next recipe (the magic hat
    ///    has to be hit again). If the seed isn't taken in Seed Wait Seconds, the round ends anyway and its Gear Bool is
    ///    set on GAMESTAGES.
    /// After the last round the cauldron wants the mystery pot (the finale).
    /// PlayGround FSM (global PlayGround_Z, FSM PlayGround_Manager): it's sent the events PG_COOKING, PG_PUMPKIN,
    /// PG_CAUGHT, PG_BOSS, PG_SEED_1 ... PG_SEED_3, PG_MYSTERY and PG_POT_BROKEN (add them to its Events to use them).
    /// Tools > 429 Game > PlayGround > Set Up PlayGround Rounds adds it and fills in the waves.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Cauldron))]
    public class PlayGroundRounds : MonoBehaviour
    {
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";

        [Serializable]
        public class Wave
        {
            [Tooltip("Just a name for the Inspector, e.g. 'Round 1: Spider_Small, Slime_Big_Blue'.")]
            public string label;
            [Tooltip("The bugs it sends, non-stop from the start of cooking until the pumpkin is caught; each pick is at random.")]
            public GameObject[] enemies = Array.Empty<GameObject>();
            [Tooltip("Most of its bugs alive at once (the mini boss doesn't count; the game's enemy limit also applies).")]
            [Min(1)] public int maxAlive = 5;
            [Tooltip("Seconds between two bugs (min, max).")]
            public Vector2 spawnWait = new(1.5f, 3.5f);
            [Tooltip("The round's one mini boss: it comes in when the pot spits the pumpkin, and joins the wave.")]
            public GameObject boss;
            [Tooltip("The chest that drops in the middle when the pumpkin is caught, with a Yellow Seed inside.")]
            public GameObject chest;
            [Tooltip("The GAMESTAGES bool of the chest's gear (e.g. Chest_Gear): set true if the seed is never taken and " +
                     "the round ends by itself, so the game can still go on.")]
            public string gearBool;
        }

        [Tooltip("One per cauldron round, in order.")]
        [SerializeField] private Wave[] waves = Array.Empty<Wave>();

        [Header("Spawning")]
        [Tooltip("Puff where a bug comes in, e.g. Door_smoke_black or Smoke_Blast.")]
        [SerializeField] private GameObject spawnEffect;
        [Min(0.01f)] [SerializeField] private float spawnEffectScale = 0.08f;
        [Tooltip("Bugs never come in closer than this to the player, in metres.")]
        [Min(0f)] [SerializeField] private float minPlayerDistance = 5f;
        [Tooltip("How far in from the PlayGround's edge they come in, in metres.")]
        [Min(0f)] [SerializeField] private float edgeInset = 1f;
        [Tooltip("Puff where the chest drops.")]
        [SerializeField] private GameObject chestEffect;
        [Min(0.01f)] [SerializeField] private float chestEffectScale = 0.12f;

        [Header("Clean-up: the Fairy Shield")]
        [Tooltip("When the seed is taken, the Fairy Shield (the GAMESTAGES Shield_Sphere that 'clean it' uses) sweeps " +
                 "the PlayGround: the bugs and bosses left pop, and so does anything else it pops (the cauldron, candies " +
                 "and chests don't). The wave's bugs left die either way.")]
        [SerializeField] private bool cleanWithFairyShield = true;
        [Tooltip("The shield object's name in the scene.")]
        [SerializeField] private string shieldName = "Shield_Sphere";
        [Tooltip("The smoke that goes off with the shield, as everywhere the shield is used (user, 2026-10-28): its name in " +
                 "the scene. Empty: none.")]
        [SerializeField] private string shieldSmokeName = "Smoke_Blast_Green";

        [Header("PlayGround FSM")]
        [Tooltip("The PlayMaker global holding the PlayGround.")]
        [SerializeField] private string playGroundVariable = "PlayGround_Z";
        [Tooltip("Its FSM that's sent the PG_ events.")]
        [SerializeField] private string managerFsm = "PlayGround_Manager";
        [Tooltip("The inventory item a chest's seed gives; taking one ends the round.")]
        [SerializeField] private string seedItem = "Yellow_Seed";
        [Tooltip("If the seed isn't taken this many seconds after the pumpkin is caught (it rolled out of reach, or the " +
                 "chest can't be got at), the round goes on anyway and the wave's Gear Bool is set.")]
        [Min(1f)]
        [SerializeField] private float seedWaitSeconds = 320f;

        /// <summary>The round's wave started (round index from 0): the cauldron started cooking a right recipe.</summary>
        public static event Action<PlayGroundRounds, int> WaveStarted;
        /// <summary>The round's seed was taken from its chest (round index from 0): the PlayGround moves on.</summary>
        public static event Action<PlayGroundRounds, int> SeedTaken;

        private Cauldron cauldron;
        private readonly HashSet<LaughingPumpkin> pumpkins = new();
        private Coroutine spawning;
        private Coroutine bossing;
        private GameObject bossMade;
        private bool bossSent;
        private int waveRound = -1;
        private int seedRound = -1;
        private float seedSince;
        private bool subscribedInventory;

        public int ActiveWave => waveRound;
        public bool WaitingForSeed => seedRound >= 0;

        private void Awake()
        {
            cauldron = GetComponent<Cauldron>();
            cauldron.HoldBetweenRounds = true;
        }

        private void OnEnable()
        {
            Cauldron.CookingStarted += OnCookingStarted;
            Cauldron.Cooked += OnCooked;
            Cauldron.RecipeChanged += OnRecipeChanged;
            Cauldron.PotBroken += OnPotBroken;
            LaughingPumpkin.Caught += OnCaught;
            LaughingPumpkin.TurnedIntoBoss += OnPumpkinTurned;
            BlackPumpkin.BossSpawned += OnMiniBoss;
        }

        private void OnDisable()
        {
            Cauldron.CookingStarted -= OnCookingStarted;
            Cauldron.Cooked -= OnCooked;
            Cauldron.RecipeChanged -= OnRecipeChanged;
            Cauldron.PotBroken -= OnPotBroken;
            LaughingPumpkin.Caught -= OnCaught;
            LaughingPumpkin.TurnedIntoBoss -= OnPumpkinTurned;
            BlackPumpkin.BossSpawned -= OnMiniBoss;
            if (subscribedInventory && PlayerInventory.Instance != null) PlayerInventory.Instance.Changed -= OnInventory;
            subscribedInventory = false;
        }

        private void Update()
        {
            // The inventory may come up after this; hook it as soon as it's there.
            if (!subscribedInventory && PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.Changed += OnInventory;
                subscribedInventory = true;
            }
            // The seed was never taken: the PlayGround mustn't stop here, and GAMESTAGES still needs the round's gear.
            if (seedRound >= 0 && Time.time - seedSince > seedWaitSeconds)
            {
                var wave = WaveOf(seedRound);
                if (wave != null && !string.IsNullOrEmpty(wave.gearBool)) GameStages.SetBool(wave.gearBool, true, this);
                FinishRound(seedRound);
            }
        }

        // ------------------------------------------------------------------ cauldron

        // Only a right recipe starts cooking now (rounds 1-3), so the wave and the walls' GO start with it.
        private void OnCookingStarted(Cauldron who)
        {
            if (who != cauldron) return;
            Tell("PG_COOKING");
            int round = cauldron.RoundIndex;
            if (cauldron.InMystery || round < 0 || round >= waves.Length || waveRound == round) return;
            StartWave(round);
        }

        private void OnCooked(Cauldron who, GameObject made)
        {
            if (who != cauldron || made == null) return;
            if (made.TryGetComponent<LaughingPumpkin>(out var pumpkin)) pumpkins.Add(pumpkin);
            Tell("PG_PUMPKIN");
            // The round's one mini boss comes in as the pumpkin is spat out; it's part of the wave.
            var wave = WaveOf(waveRound);
            if (wave == null || wave.boss == null || bossSent) return;
            bossSent = true;
            bossing = StartCoroutine(SpawnBoss(wave.boss));
        }

        // A black pumpkin's boss joins the running wave, so it dies with it when the seed is taken.
        private void OnMiniBoss(GameObject boss)
        {
            if (waveRound >= 0 && boss != null) EnemyWave.Add(this, boss);
        }

        private void OnRecipeChanged(Cauldron who)
        {
            if (who == cauldron && cauldron.InMystery) Tell("PG_MYSTERY");
        }

        private void OnPotBroken(Cauldron who)
        {
            if (who != cauldron) return;
            EndWave(true);
            Tell("PG_POT_BROKEN");
        }

        // ------------------------------------------------------------------ pumpkin caught, chest, seed

        private void OnCaught(LaughingPumpkin pumpkin)
        {
            if (!pumpkins.Remove(pumpkin)) return;
            // Some rounds may make more than one pumpkin: the round is won when the last is caught.
            pumpkins.RemoveWhere(p => p == null || p.IsBroken);
            if (pumpkins.Count > 0) return;

            int round = cauldron.WaitingForNextRound ? cauldron.RoundIndex : waveRound;
            // The bugs stop coming at once; the ones alive (and the mini boss) stay until the seed is taken.
            EndWave(false);
            cauldron.PumpkinCaught();
            Tell("PG_CAUGHT");

            var wave = WaveOf(round);
            if (wave != null && wave.chest != null)
            {
                seedRound = round;
                seedSince = Time.time;
                DropChest(wave.chest);
            }
            else
            {
                FinishRound(round);
            }
        }

        // The pumpkin wasn't caught in time and turned into the Pumpkin Boss (user, 2026-11-07: killing it wins the game,
        // as on the runner path). No chest, no seed: the bugs stop coming (the boss's own bug pumpkins need the enemy
        // slots); the ones alive and the mini boss stay. The boss isn't part of the wave, so nothing here removes it.
        private void OnPumpkinTurned(LaughingPumpkin pumpkin, GameObject boss)
        {
            if (!pumpkins.Remove(pumpkin)) return;
            pumpkins.RemoveWhere(p => p == null || p.IsBroken);
            if (pumpkins.Count > 0) return;
            EndWave(false);
            cauldron.PumpkinCaught();
            Tell("PG_BOSS");
        }

        // Switches the GAMESTAGES Fairy Shield on, and its green smoke with it: the shield grows over the stage by itself and
        // switches itself off again, and so does the smoke.
        private bool FireFairyShield()
        {
            GameObject shield = null, smoke = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!candidate.scene.IsValid()) continue;
                if (shield == null && candidate.name == shieldName && candidate.CompareTag("Fairy_Shield")) shield = candidate;
                else if (smoke == null && !string.IsNullOrEmpty(shieldSmokeName) && candidate.name == shieldSmokeName) smoke = candidate;
            }
            if (shield == null) return false;
            if (shield.activeSelf) shield.SetActive(false);
            shield.SetActive(true);
            if (smoke != null)
            {
                if (smoke.activeSelf) smoke.SetActive(false);
                smoke.SetActive(true);
            }
            return true;
        }

        private void OnInventory(ItemDefinition item, int change, int newCount)
        {
            if (seedRound < 0 || change <= 0 || item == null || item.Id != seedItem) return;
            FinishRound(seedRound);
        }

        // The round is over: the Fairy Shield sweeps the PlayGround, the wave's bugs left die, the cauldron moves on and
        // the PlayGround Field clears the field (its creatures too) and grows it.
        private void FinishRound(int round)
        {
            seedRound = -1;
            if (cleanWithFairyShield) FireFairyShield();
            EnemyWave.KillAll(this);
            // In the same frame: no slime the player hit a moment ago splits as the wave dies, and every other slime pops.
            SlimeBody.ClearAll();
            bossMade = null;
            if (round >= 0) Tell("PG_SEED_" + (round + 1));
            cauldron.Continue();
            if (round >= 0) SeedTaken?.Invoke(this, round);
        }

        private void DropChest(GameObject chest)
        {
            var at = Middle();
            var made = Instantiate(chest, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
            if (made.TryGetComponent<Rigidbody>(out var body)) body.linearVelocity = Vector3.zero;
            Puff(chestEffect, at, chestEffectScale);
        }

        // ------------------------------------------------------------------ waves

        private void StartWave(int round)
        {
            EndWave(false);
            waveRound = round;
            bossSent = false;
            bossMade = null;
            EnemyWave.Start(this);
            spawning = StartCoroutine(RunWave(waves[round]));
            WaveStarted?.Invoke(this, round);
        }

        // Non-stop: a bug whenever there's room, until EndWave (the pumpkin caught or turned, the pot broken).
        private IEnumerator RunWave(Wave wave)
        {
            yield return new WaitForSeconds(1f);
            while (true)
            {
                while (Minions() >= wave.maxAlive || !EnemyLimit.TryTakeSlot()) yield return new WaitForSeconds(0.25f);
                Spawn(Pick(wave.enemies));
                yield return new WaitForSeconds(Random.Range(wave.spawnWait.x, Mathf.Max(wave.spawnWait.x, wave.spawnWait.y)));
            }
        }

        // The mini boss comes in even when the enemy limit is full; if there's no spot yet it tries again until it's in
        // or the wave has ended.
        private IEnumerator SpawnBoss(GameObject prefab)
        {
            while (waveRound >= 0)
            {
                bossMade = Spawn(prefab);
                if (bossMade != null) break;
                yield return new WaitForSeconds(0.5f);
            }
            bossing = null;
        }

        // The wave's bugs alive, not counting the mini boss.
        private int Minions()
        {
            int alive = EnemyWave.Alive(this);
            return bossMade != null && EnemyWave.IsAlive(bossMade) ? alive - 1 : alive;
        }

        /// <summary>Stops the wave (no more bugs, no mini boss yet to come); with 'killAll' every bug and boss of it still alive dies.</summary>
        public void EndWave(bool killAll)
        {
            if (spawning != null) StopCoroutine(spawning);
            spawning = null;
            if (bossing != null) StopCoroutine(bossing);
            bossing = null;
            if (killAll) EnemyWave.KillAll(this);
            waveRound = -1;
        }

        private GameObject Spawn(GameObject prefab)
        {
            if (prefab == null) return null;
            if (!SpawnPoint(out var at)) return null;
            var enemy = Instantiate(prefab, at, Quaternion.identity, SpawnedStuff());
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head != null)
            {
                var look = head.position - at;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) enemy.transform.rotation = Quaternion.LookRotation(look);
            }
            EnemyWave.Add(this, enemy);
            Puff(spawnEffect, at, spawnEffectScale);
            return enemy;
        }

        // A spot on the PlayGround's edge, on the NavMesh, not right next to the player.
        private bool SpawnPoint(out Vector3 at)
        {
            var area = GhostRoamArea.Around(transform.position);
            var head = Camera.main != null ? Camera.main.transform.position : transform.position;
            for (int i = 0; i < 12; i++)
            {
                var spot = area != null
                    ? area.RandomEdgePoint(edgeInset)
                    : transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(6f, 10f);
                // Down onto the floor first (the area's edge point is up in the air, at its middle height).
                if (Physics.Raycast(spot + Vector3.up * 1f, Vector3.down, out var floor, 15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    spot = floor.point;
                if (!NavMesh.SamplePosition(spot, out var hit, 2f, NavMesh.AllAreas)) continue;
                var flat = hit.position - head;
                flat.y = 0f;
                if (flat.magnitude < minPlayerDistance) continue;
                at = hit.position;
                return true;
            }
            at = default;
            return false;
        }

        // ------------------------------------------------------------------ helpers

        private Wave WaveOf(int round) => round >= 0 && round < waves.Length ? waves[round] : null;

        private Vector3 Middle()
        {
            var area = GhostRoamArea.Around(transform.position);
            var middle = area != null ? area.Middle : transform.position + transform.forward * 2.5f;
            if (Physics.Raycast(middle + Vector3.up * 2f, Vector3.down, out var hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                middle = hit.point;
            if (NavMesh.SamplePosition(middle, out var onMesh, 3f, NavMesh.AllAreas)) middle = onMesh.position;
            return middle;
        }

        private void Tell(string eventName)
        {
            var fsm = ManagerFsm();
            if (fsm != null) fsm.SendEvent(eventName);
        }

        private PlayMakerFSM ManagerFsm()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(playGroundVariable);
            if (holder == null || holder.Value == null) return null;
            foreach (var fsm in holder.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == managerFsm) return fsm;
            }
            return null;
        }

        private static GameObject Pick(GameObject[] choices)
        {
            if (choices == null || choices.Length == 0) return null;
            return choices[Random.Range(0, choices.Length)];
        }

        private static void Puff(GameObject effect, Vector3 at, float scale)
        {
            if (effect == null) return;
            var made = Instantiate(effect, at, Quaternion.identity);
            made.transform.localScale = Vector3.one * scale;
            made.SetActive(true);
            Destroy(made, 4f);
        }

        private static Transform SpawnedStuff()
        {
            var variable = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return variable != null && variable.Value != null ? variable.Value.transform : null;
        }
    }
}
