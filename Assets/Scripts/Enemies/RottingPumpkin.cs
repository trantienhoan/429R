using System.Collections.Generic;
using Game.Combat;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A tree pumpkin with a cockroach hiding inside (Spider Brain > Hiding in pumpkins): it rots black over Rot Seconds,
    /// wobbling, hissing and puffing dark smoke more and more, then bursts into Burst Count small cockroaches. The roach
    /// has eaten the candies: whichever way it breaks, no candy comes out (its pieces fly as usual, rotten black).
    /// Smash it first and the roach dies inside it: the player gets the roach's EXP, and no small ones
    /// come out. Held in a hand it keeps rotting, and bursts there. Spider Brain adds it as the roach dives in; nothing
    /// to set up by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public class RottingPumpkin : MonoBehaviour
    {
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color Rotten = new(0.07f, 0.05f, 0.04f);
        // Pumpkins someone is rotting in (or a roach is heading for): no second roach goes for them.
        private static readonly HashSet<GameObject> taken = new();

        public class Settings
        {
            public GameObject burstPrefab;
            public int burstCount = 3;
            public float rotSeconds = 5.5f;
            public GameObject[] puffs;
            public GameObject[] burstEffects;
            public AudioClip[] hiss;
            public AudioClip burstSound;
            public AudioClip squashSound;
            [Tooltip("The pieces it breaks into, if its own drop's FSM can't be read (Pumpkin_On_Tree: pumpkin_2_Drop).")]
            public GameObject pieces;
        }

        private GameObject roach;
        private Settings settings;
        private FsmFloat health;
        private PlayMakerFSM healthFsm;
        private Rigidbody body;
        private XRGrabInteractable grab;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Color[] startColours;
        private GameObject dropPrefab;
        private float startedAt;
        private float nextWobble;
        private float nextHiss;
        private float nextPuff;
        private bool finished;

        /// <summary>A roach is rotting in it, or one is on its way to hide in it.</summary>
        public static bool IsTaken(GameObject pumpkin) => pumpkin != null && taken.Contains(pumpkin);

        /// <summary>A roach claims it on its way there (none other goes for it); false if another got there first.</summary>
        public static bool Claim(GameObject pumpkin) => pumpkin != null && taken.Add(pumpkin);

        public static void Release(GameObject pumpkin)
        {
            if (pumpkin != null) taken.Remove(pumpkin);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => taken.Clear();

        /// <summary>The roach (already hidden away inside) starts rotting this pumpkin.</summary>
        public static RottingPumpkin Begin(GameObject pumpkin, GameObject hidingRoach, Settings with)
        {
            var rot = pumpkin.AddComponent<RottingPumpkin>();
            rot.roach = hidingRoach;
            rot.settings = with;
            taken.Add(pumpkin);
            return rot;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            startedAt = Time.time;
            startColours = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].sharedMaterial;
                startColours[i] = material == null ? Color.white
                    : material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId)
                    : material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
            }
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "Health") continue;
                healthFsm = fsm;
                health = fsm.FsmVariables.FindFsmFloat("health");
            }
            // Its own break would drop the pieces with candy in them: that's switched off, and the break spawns them
            // here, without the candy.
            foreach (var fsm in GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.gameObject == gameObject || !fsm.gameObject.name.ToLower().Contains("drop")) continue;
                dropPrefab = CreatedBy(fsm);
                fsm.enabled = false;
                fsm.gameObject.SetActive(false);
                break;
            }
        }

        private void Start()
        {
            if (dropPrefab == null && settings != null) dropPrefab = settings.pieces;
        }

        private void Update()
        {
            if (finished) return;
            // Broken before it burst (the player squashed it, or something did), or popped by the fairy's shield
            // (its Health FSM's "pop"): the roach inside dies.
            if ((health != null && health.Value <= 0f) || (healthFsm != null && healthFsm.ActiveStateName == "pop"))
            {
                Squashed();
                return;
            }
            // The roach (shrunk away) stays inside it, wherever it rolls or is carried.
            if (roach != null) roach.transform.position = Centre();
            float t = Mathf.Clamp01((Time.time - startedAt) / Mathf.Max(0.1f, settings.rotSeconds));
            Tint(t);
            Wobble(t);
            if (Time.time >= nextHiss && settings.hiss != null && settings.hiss.Length > 0)
            {
                nextHiss = Time.time + Mathf.Lerp(1.6f, 0.6f, t);
                var clip = settings.hiss[Random.Range(0, settings.hiss.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, Mathf.Lerp(0.35f, 0.9f, t));
            }
            if (t > 0.3f && Time.time >= nextPuff && settings.puffs != null)
            {
                nextPuff = Time.time + Mathf.Lerp(1.2f, 0.4f, t);
                foreach (var puff in settings.puffs)
                    if (puff != null) EffectPool.Play(puff, Centre() + Random.insideUnitSphere * 0.08f, Mathf.Lerp(0.25f, 0.5f, t));
            }
            if (t >= 1f) Burst();
        }

        // Orange to rotten black, bit by bit.
        private void Tint(float t)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(block);
                var c = Color.Lerp(startColours[i], Rotten * startColours[i].a, Mathf.SmoothStep(0f, 1f, t));
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                renderers[i].SetPropertyBlock(block);
            }
        }

        // Little shoves and hops, harder and quicker as it rots (not while it's in a hand).
        private void Wobble(float t)
        {
            if (Time.time < nextWobble || body == null || body.isKinematic || (grab != null && grab.isSelected)) return;
            nextWobble = Time.time + Mathf.Lerp(0.45f, 0.12f, t);
            float strength = Mathf.Lerp(0.6f, 2.4f, t);
            body.AddTorque(Random.onUnitSphere * strength, ForceMode.VelocityChange);
            if (Random.value < Mathf.Lerp(0.1f, 0.5f, t)) body.AddForce(Vector3.up * Mathf.Lerp(0.6f, 1.6f, t), ForceMode.VelocityChange);
        }

        // Bursts by itself: no EXP for the pumpkin (the player didn't break it), the small roaches pour out.
        private void Burst()
        {
            finished = true;
            var at = Centre();
            Game.Leveling.PlayerLevel.NoRewardFor(gameObject);
            if (roach != null)
            {
                if (roach.TryGetComponent(out EnemyHitDamage hits)) hits.noReward = true;
                Destroy(roach);
            }
            if (settings.burstEffects != null)
                foreach (var effect in settings.burstEffects) if (effect != null) EffectPool.Play(effect, at, 0.9f);
            if (settings.burstSound != null) AudioSource.PlayClipAtPoint(settings.burstSound, at);
            SpawnPieces(at);
            for (int i = 0; i < settings.burstCount && settings.burstPrefab != null; i++)
            {
                var spot = at + Quaternion.Euler(0f, i * 360f / settings.burstCount + Random.Range(-30f, 30f), 0f) * Vector3.forward * Random.Range(0.3f, 0.6f);
                if (UnityEngine.AI.NavMesh.SamplePosition(spot, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas)) spot = hit.position;
                var made = Instantiate(settings.burstPrefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
                if (made.TryGetComponent(out SpiderBrain brain)) brain.ComeAtOnce();
            }
            if (health != null) health.Value = 0f;
            Release(gameObject);
        }

        // Smashed with the roach still inside: it dies there (its EXP goes to the player if they broke it), no small ones.
        private void Squashed()
        {
            finished = true;
            var at = Centre();
            if (roach != null)
            {
                roach.transform.position = at;
                // Its EXP only when the player broke the pumpkin.
                if (!Earned()) Game.Leveling.PlayerLevel.NoRewardFor(roach);
                EnemyWave.Kill(roach);
            }
            if (settings.squashSound != null) AudioSource.PlayClipAtPoint(settings.squashSound, at);
            SpawnPieces(at);
            Release(gameObject);
        }

        // Its pieces, as its own break makes them, but without the candies it held (the roach ate them), and rotten.
        private void SpawnPieces(Vector3 at)
        {
            if (dropPrefab == null) return;
            var holder = new GameObject("Rotten Drop (held)");
            holder.SetActive(false);
            var drop = Instantiate(dropPrefab, at, transform.rotation, holder.transform);
            foreach (var fsm in drop.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                var made = CreatedBy(fsm);
                if (made != null && made.name.Contains("Candy")) fsm.enabled = false;
            }
            // Gone before the pieces wake up (it's still switched off), so no candy ever appears.
            var candies = new List<GameObject>();
            foreach (var t in drop.GetComponentsInChildren<Transform>(true))
                if (t != drop.transform && t.name.Contains("Candy")) candies.Add(t.gameObject);
            foreach (var candy in candies) if (candy != null) DestroyImmediate(candy);
            foreach (var r in drop.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Rotten);
                block.SetColor(ColorId, Rotten);
                r.SetPropertyBlock(block);
            }
            drop.transform.SetParent(SpawnedStuff(), true);
            Destroy(holder);
        }

        private void OnDestroy()
        {
            Release(gameObject);
            // Gone some other way before it noticed (broken and gone in one go, the fairy's shield, the stage clearing
            // up): the roach inside dies with it.
            if (!finished && roach != null)
            {
                // Broken by the player in one go pays its EXP; cleared away with the stage doesn't.
                if (health == null || health.Value > 0f || !Earned()) Game.Leveling.PlayerLevel.NoRewardFor(roach);
                EnemyWave.Kill(roach);
            }
        }

        // The player broke it: not the giant, a fall, the tree pumpkins' limit or the fairy's shield (they mark it No
        // Reward, or the shield pops it).
        private bool Earned()
        {
            if (healthFsm != null && healthFsm.ActiveStateName == "pop") return false;
            if (TryGetComponent(out Game.Leveling.ExpWhenBroken reward) && reward.noReward) return false;
            return !(TryGetComponent(out EnemyHitDamage hits) && hits.noReward);
        }

        private Vector3 Centre()
        {
            return TryGetComponent(out Collider shape) && shape.enabled ? shape.bounds.center : transform.position;
        }

        // The prefab a PlayMaker FSM's Create Object makes, if it has one.
        private static GameObject CreatedBy(PlayMakerFSM fsm)
        {
            if (fsm.FsmStates == null) return null;
            foreach (var state in fsm.FsmStates)
            {
                if (state.Actions == null) continue;
                foreach (var action in state.Actions)
                {
                    if (action is HutongGames.PlayMaker.Actions.CreateObject create && create.gameObject != null && create.gameObject.Value != null)
                        return create.gameObject.Value;
                }
            }
            return null;
        }

        private static Transform SpawnedStuff()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }
}
