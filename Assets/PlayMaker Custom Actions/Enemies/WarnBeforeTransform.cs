using Game.VFX;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Put it in the state where a pumpkin waits before it turns into its boss, after the Wait. In the last Warn " +
             "Last seconds of that Wait the pumpkin gets angry, so the player can tell it's about to change: it wobbles " +
             "(its Jiggle) and laughs, more and more often and deeper and deeper. In the last Rumble Last seconds it also " +
             "trembles with a low growl. It stops when the state ends (the boss comes, the pumpkin breaks, or Hurry " +
             "Pumpkin Boss). It reads the time from the Wait action in its own state, so nothing has to be kept in step.")]
    public class WarnBeforeTransform : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The pumpkin. Its Jiggle (on it or a child) wobbles, and its sounds come from where it is.")]
        public FsmOwnerDefault gameObject;

        [Tooltip("How long the wait is, in seconds. 0 = the Time of the Wait action in this state.")]
        public FsmFloat waitTime;

        [Tooltip("The warnings start this many seconds before the wait ends. 0 = no warnings.")]
        public FsmFloat warnLast;

        [ActionSection("Laughs and wobbles")]
        [Tooltip("Laughs: one picked at random each time.")]
        public AudioClip[] laughSounds;
        public FsmFloat laughVolume;

        [Tooltip("Seconds between laughs (each with a wobble) when the warnings start...")]
        public FsmFloat firstEvery;
        [Tooltip("...and at the end: they come more and more often.")]
        public FsmFloat lastEvery;

        [Tooltip("The laughs get deeper and deeper, from pitch 1 down to this at the end.")]
        public FsmFloat lowestPitch;

        [Tooltip("How hard it wobbles when the warnings start, and at the end (1 = a full hit's jiggle). A wobble below " +
                 "1 is skipped while it's still wobbling.")]
        public FsmFloat firstWobble;
        public FsmFloat lastWobble;

        [ActionSection("Rumble")]
        [Tooltip("The rumble starts this many seconds before the wait ends: a tremble (a full wobble) with a low growl " +
                 "every Tremble Every seconds. 0 = no rumble.")]
        public FsmFloat rumbleLast;

        [Tooltip("The growl. A short one pulses with the trembles; a long one plays to its end before it starts again.")]
        [ObjectType(typeof(AudioClip))]
        public FsmObject rumbleSound;
        public FsmFloat rumbleVolume;

        [Tooltip("Seconds between trembles during the rumble.")]
        public FsmFloat trembleEvery;

        private GameObject pumpkin;
        private Jiggle jiggle;
        private AudioSource laughs;
        private AudioSource rumble;
        private float total;
        private bool realTime;
        private float elapsed;
        private float nextLaugh;
        private float nextTremble;

        public override void Reset()
        {
            gameObject = null;
            waitTime = 0f;
            warnLast = 60f;
            laughSounds = new AudioClip[0];
            laughVolume = 1f;
            firstEvery = 8f;
            lastEvery = 1.2f;
            lowestPitch = 0.7f;
            firstWobble = 0.3f;
            lastWobble = 0.8f;
            rumbleLast = 10f;
            rumbleSound = null;
            rumbleVolume = 1f;
            trembleEvery = 0.6f;
        }

        public override void OnEnter()
        {
            pumpkin = Fsm.GetOwnerDefaultTarget(gameObject);
            elapsed = 0f;
            nextTremble = 0f;
            realTime = false;
            total = waitTime.Value;
            if (total <= 0f)
            {
                foreach (var action in State.Actions)
                {
                    if (action is not Wait wait) continue;
                    total = wait.time.Value;
                    realTime = wait.realTime;
                    break;
                }
            }
            if (pumpkin == null || total <= 0f || warnLast.Value <= 0f) return;

            jiggle = pumpkin.GetComponentInChildren<Jiggle>();
            // The first laugh comes as the warnings start.
            nextLaugh = Mathf.Max(0f, total - warnLast.Value);
        }

        public override void OnUpdate()
        {
            if (pumpkin == null || total <= 0f || warnLast.Value <= 0f) return;
            elapsed += realTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float left = total - elapsed;
            if (left > warnLast.Value || left <= 0f) return;

            // 0 as the warnings start, 1 when the wait is over.
            float anger = 1f - Mathf.Clamp01(left / warnLast.Value);

            if (elapsed >= nextLaugh)
            {
                nextLaugh = elapsed + Mathf.Max(0.1f, Mathf.Lerp(firstEvery.Value, lastEvery.Value, anger));
                Wobble(Mathf.Lerp(firstWobble.Value, lastWobble.Value, anger));
                Laugh(Mathf.Lerp(1f, lowestPitch.Value, anger));
            }

            if (rumbleLast.Value > 0f && left <= rumbleLast.Value && elapsed >= nextTremble)
            {
                nextTremble = elapsed + Mathf.Max(0.1f, trembleEvery.Value);
                Wobble(1f);
                Rumble();
            }
        }

        // A growl with each tremble (a short sound pulses with them; a long one plays to its end before it starts again).
        private void Rumble()
        {
            var clip = rumbleSound != null ? rumbleSound.Value as AudioClip : null;
            if (clip == null || rumbleVolume.Value <= 0f) return;
            var source = Voice(ref rumble, "Warning Rumble");
            if (!source.isPlaying) source.PlayOneShot(clip, rumbleVolume.Value);
        }

        public override void OnExit()
        {
            // The boss came, the pumpkin broke, or it was hurried: the warnings end with the state.
            if (laughs != null) laughs.Stop();
            if (rumble != null) rumble.Stop();
        }

        private void Wobble(float strength)
        {
            if (jiggle != null && strength > 0f) jiggle.Hit(strength);
        }

        private void Laugh(float pitch)
        {
            if (laughSounds == null || laughSounds.Length == 0 || laughVolume.Value <= 0f) return;
            var clip = laughSounds[Random.Range(0, laughSounds.Length)];
            if (clip == null) return;
            var source = Voice(ref laughs, "Warning Laughs");
            source.pitch = pitch;
            source.PlayOneShot(clip, laughVolume.Value);
        }

        // A sound of its own on a child of the pumpkin (so the pumpkin's own sounds aren't cut or bent), heard from where
        // it is like the pumpkin's own Audio Source.
        private AudioSource Voice(ref AudioSource source, string name)
        {
            if (source != null) return source;
            var child = pumpkin.transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(pumpkin.transform, false);
            }
            if (!child.TryGetComponent(out source)) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            if (pumpkin.TryGetComponent(out AudioSource own))
            {
                source.outputAudioMixerGroup = own.outputAudioMixerGroup;
                source.spatialBlend = own.spatialBlend;
                source.rolloffMode = own.rolloffMode;
                source.minDistance = own.minDistance;
                source.maxDistance = own.maxDistance;
                source.dopplerLevel = own.dopplerLevel;
            }
            else
            {
                source.spatialBlend = 1f;
                source.minDistance = 3f;
                source.maxDistance = 25f;
            }
            return source;
        }
    }
}
