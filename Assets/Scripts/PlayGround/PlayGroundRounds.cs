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
    /// Runs the PlayGround's rounds around the cauldron (goes next to the Cauldron):
    /// 1. The cauldron starts cooking: its smell calls this round's bugs (Waves, one per round, like EnemyManager
    ///    0-3 but endgame+: twice the bugs and two bosses). They come in from the edges of the PlayGround (its Ghost
    ///    Roam Area) and go for the player, a few alive at a time, the bosses once most of the wave is beaten.
    ///    A wrong recipe doesn't stop them: they keep coming until this round's pumpkin is caught.
    /// 2. The right recipe spits out the laughing pumpkin. When it's caught (it breaks), the wave stops and the Fairy
    ///    Shield sweeps the PlayGround (the bugs and bosses left pop; the cauldron doesn't), then the round's Chest drops
    ///    in the middle of the PlayGround.
    /// 3. Taking the chest's Yellow Seed ends the round: the PlayGround FSM hears about it (below) and the cauldron
    ///    moves on to the next recipe (the magic hat has to be hit again).
    /// After the last round the cauldron wants the mystery pot (? ? ? ? ?); when it bursts it leaves the Green Seed.
    /// PlayGround FSM (global PlayGround_Z, FSM PlayGround_Manager): it's sent the events PG_COOKING, PG_PUMPKIN,
    /// PG_CAUGHT, PG_SEED_1 ... PG_SEED_4 (the seed of round N was taken: grow the PlayGround), PG_MYSTERY and
    /// PG_POT_BROKEN; add them to its Events and use them as global transitions. For the old phase states, Seed
    /// Bools are also set true on it (round 1 -> Pumpkin_09, round 2 -> Pumpkin_10).
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
            [Tooltip("Just a name for the Inspector, e.g. 'Round 1: small spiders'.")]
            public string label;
            [Tooltip("The bugs it sends; each pick is at random.")]
            public GameObject[] enemies = Array.Empty<GameObject>();
            [Tooltip("How many bugs it sends in all (after that only the bosses come).")]
            [Min(0)] public int waveSize = 12;
            [Tooltip("Most of its bugs alive at once (the game's enemy limit also applies).")]
            [Min(1)] public int maxAlive = 5;
            [Tooltip("Seconds between two bugs (min, max).")]
            public Vector2 spawnWait = new(1.5f, 3.5f);
            [Tooltip("The boss it sends once the wave is (almost) beaten.")]
            public GameObject boss;
            [Min(0)] public int bosses = 2;
            [Tooltip("The bosses come once this few of the wave are left alive.")]
            [Min(0)] public int bossWhenLeft = 2;
            [Tooltip("The chest that drops in the middle when the pumpkin is caught, with a Yellow Seed inside.")]
            public GameObject chest;
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
        [Tooltip("When the pumpkin is caught, the Fairy Shield (the GAMESTAGES Shield_Sphere that 'clean it' uses) " +
                 "sweeps the PlayGround: the bugs and bosses left pop, and so does anything else it pops (the " +
                 "cauldron, candies and chests don't). Off, or no shield found: just the wave's bugs die.")]
        [SerializeField] private bool cleanWithFairyShield = true;
        [Tooltip("The shield object's name in the scene.")]
        [SerializeField] private string shieldName = "Shield_Sphere";
        [Tooltip("The chest drops this many seconds after the shield goes off (it grows over 3 s).")]
        [Min(0f)] [SerializeField] private float chestAfterShield = 3.2f;

        [Header("PlayGround FSM")]
        [Tooltip("The PlayMaker global holding the PlayGround.")]
        [SerializeField] private string playGroundVariable = "PlayGround_Z";
        [Tooltip("Its FSM that's sent the PG_ events.")]
        [SerializeField] private string managerFsm = "PlayGround_Manager";
        [Tooltip("Bools set true on that FSM when round 1, 2 ... 's seed is taken (for the old phase states).")]
        [SerializeField] private string[] seedBools = { "Pumpkin_09", "Pumpkin_10" };
        [Tooltip("The inventory item a chest's seed gives; taking one ends the round.")]
        [SerializeField] private string seedItem = "Yellow_Seed";

        /// <summary>The round's wave started (round index from 0).</summary>
        public static event Action<PlayGroundRounds, int> WaveStarted;
        /// <summary>The round's pumpkin was caught and its wave ended.</summary>
        public static event Action<PlayGroundRounds, int> RoundWon;

        private Cauldron cauldron;
        private readonly HashSet<LaughingPumpkin> pumpkins = new();
        private Coroutine spawning;
        private int waveRound = -1;
        private int seedRound = -1;
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
            BlackPumpkin.BossSpawned += OnMiniBoss;
        }

        private void OnDisable()
        {
            Cauldron.CookingStarted -= OnCookingStarted;
            Cauldron.Cooked -= OnCooked;
            Cauldron.RecipeChanged -= OnRecipeChanged;
            Cauldron.PotBroken -= OnPotBroken;
            LaughingPumpkin.Caught -= OnCaught;
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
        }

        // ------------------------------------------------------------------ cauldron

        private void OnCookingStarted(Cauldron who)
        {
            if (who != cauldron) return;
            Tell("PG_COOKING");
            int round = cauldron.RoundIndex;
            if (cauldron.InMystery || round >= waves.Length || waveRound == round) return;
            StartWave(round);
        }

        private void OnCooked(Cauldron who, GameObject made)
        {
            if (who != cauldron || made == null) return;
            if (made.TryGetComponent<LaughingPumpkin>(out var pumpkin)) pumpkins.Add(pumpkin);
            Tell("PG_PUMPKIN");
        }

        // A wrong recipe's mini boss joins the running wave, so it dies with it when the pumpkin is caught.
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
            // The EnemyManager ends; the Fairy Shield sweeps up what's left (or, without it, the wave's bugs die).
            bool swept = cleanWithFairyShield && FireFairyShield();
            EndWave(!swept);
            Tell("PG_CAUGHT");
            RoundWon?.Invoke(this, round);

            var wave = round >= 0 && round < waves.Length ? waves[round] : null;
            if (wave != null && wave.chest != null)
            {
                seedRound = round;
                StartCoroutine(DropChestLater(wave.chest, swept ? chestAfterShield : 0f));
            }
            else
            {
                FinishRound(round);
            }
        }

        private IEnumerator DropChestLater(GameObject chest, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            DropChest(chest);
        }

        // Switches the GAMESTAGES Fairy Shield on: it grows over the stage by itself and switches itself off again.
        private bool FireFairyShield()
        {
            GameObject shield = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name != shieldName || !candidate.scene.IsValid() || !candidate.CompareTag("Fairy_Shield")) continue;
                shield = candidate;
                break;
            }
            if (shield == null) return false;
            if (shield.activeSelf) shield.SetActive(false);
            shield.SetActive(true);
            return true;
        }

        private void OnInventory(ItemDefinition item, int change, int newCount)
        {
            if (seedRound < 0 || change <= 0 || item == null || item.Id != seedItem) return;
            FinishRound(seedRound);
        }

        private void FinishRound(int round)
        {
            seedRound = -1;
            if (round >= 0)
            {
                Tell("PG_SEED_" + (round + 1));
                if (round < seedBools.Length && !string.IsNullOrEmpty(seedBools[round])) SetManagerBool(seedBools[round], true);
            }
            cauldron.Continue();
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
            EnemyWave.Start(this);
            spawning = StartCoroutine(RunWave(waves[round]));
            WaveStarted?.Invoke(this, round);
        }

        private IEnumerator RunWave(Wave wave)
        {
            yield return new WaitForSeconds(1f);
            int sent = 0;
            while (sent < wave.waveSize)
            {
                while (EnemyWave.Alive(this) >= wave.maxAlive || !EnemyLimit.TryTakeSlot()) yield return new WaitForSeconds(0.25f);
                if (Spawn(Pick(wave.enemies)) != null) sent++;
                yield return new WaitForSeconds(Random.Range(wave.spawnWait.x, Mathf.Max(wave.spawnWait.x, wave.spawnWait.y)));
            }
            while (EnemyWave.Alive(this) > wave.bossWhenLeft) yield return new WaitForSeconds(0.5f);
            yield return new WaitForSeconds(Random.Range(2f, 4f));
            for (int i = 0; i < wave.bosses && wave.boss != null; i++)
            {
                Spawn(wave.boss);
                yield return new WaitForSeconds(1.5f);
            }
            spawning = null;
        }

        /// <summary>Stops the wave; with 'killAll' every bug and boss of it still alive dies.</summary>
        public void EndWave(bool killAll)
        {
            if (spawning != null) StopCoroutine(spawning);
            spawning = null;
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

        private void SetManagerBool(string variable, bool value)
        {
            var fsm = ManagerFsm();
            var flag = fsm != null ? fsm.FsmVariables.FindFsmBool(variable) : null;
            if (flag != null) flag.Value = value;
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
