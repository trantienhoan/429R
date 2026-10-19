using System.Collections;
using Game.Leveling;
using Game.Locomotion;
using Game.Saving;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Getting back up after dying, while the run has a revive left (see Checkpoints): instead of the game over, the
    /// death fall stops part way (most of the way; after the lucky coins, right where it was while they flew) and holds
    /// there for a moment (1-2 s), the player hears "Not yet", and the fall plays backwards as they get up, at
    /// full health (or the share the lucky coins won: Revive Coins), with a wake-up aura (the level-up aura's burst,
    /// pushing enemies and loose things away) and the heart
    /// on the health HUD cracked for the rest of the run. Nothing can hurt them while they're down, nor for a moment
    /// after. The PlayerDeath FSM starts with "Revive?" (Try Revive): with a revive left it goes to "Not Yet" (Revive
    /// Player), which runs this; with none, the game over goes on as before. Goes on the XR Origin;
    /// Tools > 429 Game > Player > Set Up Revive adds it and the PlayerDeath states.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerRevive : MonoBehaviour
    {
        [Tooltip("The \"Not yet\" voices: one of them, picked at random (not the same one twice in a row), plays as the " +
                 "player gets back up.")]
        [SerializeField] private AudioClip[] notYetSounds = new AudioClip[0];
        [Range(0f, 1f)]
        [SerializeField] private float notYetVolume = 1f;
        [Tooltip("Seconds after the player starts getting up before the \"Not yet\" plays. Negative: that long before " +
                 "getting up, while still lying down.")]
        [SerializeField] private float voiceDelay = 0.4f;
        // The one "Not yet" voice from before the list; it's moved into Not Yet Sounds.
        [SerializeField, HideInInspector] private AudioClip notYetSound;
        [Tooltip("How far the death fall goes before it stops (0.5 = half way; 1 = all the way down).")]
        [Range(0f, 1f)]
        [SerializeField] private float fallShare = 0.7f;
        [Tooltip("Seconds the fall holds there before the player gets up: a random time from x to y.")]
        [SerializeField] private Vector2 holdSeconds = new(1f, 2f);
        [Tooltip("The hold after the lucky coins were flipped (they already kept the player waiting): from x to y seconds.")]
        [SerializeField] private Vector2 holdAfterCoins = new(0.3f, 0.6f);
        [Tooltip("Seconds getting back up takes: the fall played backwards, while the view fades back in.")]
        [Min(0.1f)]
        [SerializeField] private float riseSeconds = 1.2f;
        [Tooltip("Seconds after getting up before anything can hurt the player again.")]
        [Min(0f)]
        [SerializeField] private float safeSeconds = 1.5f;
        [Tooltip("How big and strong the wake-up aura is: 0 = the level-up aura at level 1, 1 = at the top level.")]
        [Range(0f, 1f)]
        [SerializeField] private float auraStrength = 1f;
        [Tooltip("The name of the player's FSM that takes hits: it's switched off while the player is down.")]
        [SerializeField] private string damageFsm = "Damage";

        private AudioSource voice;
        private int lastVoice = -1;

        public static PlayerRevive Instance { get; private set; }

        /// <summary>Whether a revive is going on (from the fall until the player can be hurt again).</summary>
        public bool IsReviving { get; private set; }

        /// <summary>
        /// The health the next revive gives, as a share of the cap (1 = full), e.g. what the lucky coins won. Set it
        /// before Revive; it goes back to full afterwards.
        /// </summary>
        public float NextHealthShare { get; set; } = 1f;

        // The next revive comes after the lucky coins, so its hold is shorter.
        private bool afterCoins;

        /// <summary>The lucky coins were flipped: the next revive gives this share of the health, after a short hold.</summary>
        public void SetCoinResult(float healthShare)
        {
            NextHealthShare = healthShare;
            afterCoins = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void OnValidate()
        {
            MoveOldSoundIntoList();
        }

        private void MoveOldSoundIntoList()
        {
            if (notYetSound == null) return;
            if (System.Array.IndexOf(notYetSounds, notYetSound) < 0)
            {
                var list = new AudioClip[(notYetSounds?.Length ?? 0) + 1];
                list[0] = notYetSound;
                notYetSounds?.CopyTo(list, 1);
                notYetSounds = list;
            }
            notYetSound = null;
        }

        private void Awake()
        {
            MoveOldSoundIntoList();
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Player Revive] The scene already has one ({Instance.name}); '{name}' is ignored.", this);
                enabled = false;
                return;
            }
            Instance = this;

            // Its own voice in the player's head, not the rig's AudioSource the FSMs play their sounds on.
            var child = new GameObject("Revive Voice");
            child.transform.SetParent(transform, false);
            voice = child.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Gets the player back up where they fell (the revive itself was already used up by Try Revive).</summary>
        public void Revive()
        {
            if (!IsReviving) StartCoroutine(Reviving());
        }

        /// <summary>
        /// The player just went down and the lucky coins are coming: nothing can hurt them meanwhile (a hit used to start
        /// the death over and throw the coins away). Revive keeps it so until they're safely up; a game over's Dying...
        /// keeps it off too.
        /// </summary>
        public void GoDown()
        {
            SetDamage(false);
        }

        /// <summary>The coins were called off and nobody gets up this way: hits count again.</summary>
        public void CancelDown()
        {
            if (!IsReviving) SetDamage(true);
        }

        private IEnumerator Reviving()
        {
            IsReviving = true;
            SetDamage(false);
            bool coins = afterCoins;

            // The death fall stops part way; wait till it's there (it starts as the camera drops, a frame later), with a
            // limit in case it never does. After the lucky coins it stops right where it was while they flew: a clover
            // gets the player up from there, without falling any further first.
            var fall = FindAnyObjectByType<DeathCameraFall>();
            if (fall != null)
            {
                if (coins) fall.StopHere();
                else fall.StopPartWay(fallShare);
            }
            for (float waited = 0f; fall != null && !fall.IsHolding && waited < 3f; waited += Time.deltaTime) yield return null;
            // Getting up from part of the fall takes part of the time (at least half), so it's as gentle as a full one.
            float rise = riseSeconds;
            if (coins && fall != null && fall.IsDown) rise *= Mathf.Clamp(fall.Progress / Mathf.Max(0.05f, fallShare), 0.5f, 1f);

            var range = coins ? holdAfterCoins : holdSeconds;
            float hold = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
            if (voiceDelay < 0f)
            {
                // "Not yet" while still down, then up.
                yield return new WaitForSeconds(Mathf.Max(0f, hold + voiceDelay));
                SayNotYet();
                yield return new WaitForSeconds(Mathf.Min(-voiceDelay, hold));
            }
            else
            {
                yield return new WaitForSeconds(hold);
                StartCoroutine(SayNotYetIn(voiceDelay));
            }

            // Full health, or what the coins won (the HUD shows it, the heart cracked), up again, and the aura bursts out.
            PlayerBoosts.HealToShare(NextHealthShare);
            NextHealthShare = 1f;
            afterCoins = false;
            if (fall != null && fall.IsDown) fall.Rise(rise);
            else BackOnRig();
            var aura = FindAnyObjectByType<LevelUpAura>();
            if (aura != null) aura.BurstShare(auraStrength);
            yield return new WaitForSeconds(rise);

            // In case the fall didn't put the camera back (e.g. no Death Camera Fall).
            BackOnRig();
            yield return new WaitForSeconds(safeSeconds);
            SetDamage(true);
            IsReviving = false;
        }

        private IEnumerator SayNotYetIn(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            SayNotYet();
        }

        private void SayNotYet()
        {
            var notYet = PickVoice();
            if (notYet != null) voice.PlayOneShot(notYet, notYetVolume);
        }

        // A random "Not yet", not the one played last time (when there's another to pick). Null when there's none.
        private AudioClip PickVoice()
        {
            if (notYetSounds == null) return null;
            var choices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < notYetSounds.Length; i++)
            {
                if (notYetSounds[i] != null) choices.Add(i);
            }
            if (choices.Count > 1) choices.Remove(lastVoice);
            if (choices.Count == 0) return null;
            lastVoice = choices[Random.Range(0, choices.Count)];
            return notYetSounds[lastVoice];
        }

        private void SetDamage(bool on)
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == damageFsm) fsm.enabled = on;
            }
        }

        // The camera back on the rig, as PlayerHealth's Restart does.
        private void BackOnRig()
        {
            var origin = GetComponent<XROrigin>();
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null || origin.CameraFloorOffsetObject == null) return;
            var head = origin.Camera.transform;
            var offset = origin.CameraFloorOffsetObject.transform;
            if (head.parent == offset) return;
            head.SetParent(offset, false);
            head.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }
}
