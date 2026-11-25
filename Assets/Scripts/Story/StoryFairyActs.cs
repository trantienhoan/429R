using System.Collections;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The Fairy's new ways (user 2026-11-25), on the Fairy made on the start rooftop (added by Story Start's Dress):
    /// - Her teleport: a green flash and its light where she was, her teleport sound, and she is gone (body hidden, her
    ///   calls quiet). After a pick on her board she first says a send-off line ("Good luck", "Have fun", "Be careful out
    ///   there": the user's recordings, Story Fairy Settings) and then goes, leaving the seed behind.
    /// - First meeting: the first pumpkin is dropped right over her and she barely dodges it by teleporting, leaving the
    ///   green flash before it hits (she is missing on the later runs, the empty jail).
    /// - Free (the final boss beaten): her free idle (Animator state, Story Fairy Settings), and she turns to look at the
    ///   player, or at the board's card while her board is up (Story Fairy Board sets LookTarget).
    /// Her clips (Fairy_Z.blend, 2026-11-25): free_idle, hello (her board opens), goodluck (before she leaves after a
    /// pick), teleport (0.3 s right before every flash), cheer / talk (after the final boss, FairyHealth Win / Win 2),
    /// startled (the first meeting, FairyHealth DEAD). Code clips blend over Story Fairy Settings' Fade.
    /// </summary>
    [DisallowMultipleComponent]
    public class StoryFairyActs : MonoBehaviour
    {
        private const string PumpkinName = "StartGamePumpkin(Clone)";
        private const string PumpkinBody = "pumpkin_Start";

        /// <summary>She has teleported away: hidden for the rest of this run.</summary>
        public bool Gone { get; private set; }

        /// <summary>She dodged the first pumpkin (for tests).</summary>
        public bool Dodged { get; private set; }

        /// <summary>What she looks at while free; null = the player's head.</summary>
        public Transform LookTarget { get; set; }

        private StoryFairySettings settings;
        private Transform head;
        private Transform headBone;
        private Animator animator;
        private AudioSource template;
        private bool looking;
        private bool watchPumpkin;
        private Transform pumpkinBody;
        private float lastHeight = float.NaN;
        private Vector3 lastMiddle;
        private float pitch;
        private float yaw;
        private Transform boltBone;
        private bool teleportStarted;
        private float nextHello;

        private void Awake()
        {
            settings = StoryFairySettings.Instance;
            animator = GetComponentInChildren<Animator>(true);
            template = GetComponent<AudioSource>();
        }

        /// <summary>First meeting: watch for the first pumpkin and dodge it.</summary>
        public void WatchForPumpkin()
        {
            watchPumpkin = true;
        }

        /// <summary>Free: her free idle, and she looks at the player (or LookTarget).</summary>
        public void BeFree()
        {
            looking = true;
            headBone = FindDeep(transform, settings.HeadBone);
            if (settings.HideBolts) boltBone = FindDeep(transform, settings.BoltBone);
            PlayIdle();
        }

        /// <summary>Back to her free idle (her board closed).</summary>
        public void PlayIdle()
        {
            PlayState(settings.IdleState);
        }

        /// <summary>
        /// Her hello (her board opens): her hello move (the Animator goes back to her idle by itself) and a hello line,
        /// not again sooner than Story Fairy Settings' Hello Every.
        /// </summary>
        public void Greet()
        {
            if (Gone) return;
            PlayState(settings.HelloState);
            var line = StoryFairySettings.Pick(settings.HelloLines);
            if (line == null || Time.unscaledTime < nextHello) return;
            nextHello = Time.unscaledTime + settings.HelloEvery;
            PlayAt(line, Middle(), settings.LineVolume);
        }

        /// <summary>
        /// Says her lines one after another where she is (Gap seconds of quiet between, Delay before the first), in game
        /// time. Returns the seconds it all takes (0 = nothing to say).
        /// </summary>
        public float Say(AudioClip[] lines, float delay, float gap)
        {
            var list = new System.Collections.Generic.List<AudioClip>();
            if (lines != null)
                foreach (var line in lines) if (line != null) list.Add(line);
            if (list.Count == 0) return 0f;
            float total = Mathf.Max(0f, delay);
            for (int i = 0; i < list.Count; i++) total += list[i].length + (i > 0 ? gap : 0f);
            StartCoroutine(SayLines(list, delay, gap));
            return total;
        }

        private IEnumerator SayLines(System.Collections.Generic.List<AudioClip> lines, float delay, float gap)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0 && gap > 0f) yield return new WaitForSeconds(gap);
                PlayAt(lines[i], Middle(), settings.LineVolume);
                yield return new WaitForSeconds(lines[i].length);
            }
        }

        /// <summary>Blends into one of her Animator states; false if she has no such state.</summary>
        public bool PlayState(string state, float fade = -1f)
        {
            if (animator == null || string.IsNullOrEmpty(state)) return false;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) return false;
            animator.CrossFadeInFixedTime(hash, fade < 0f ? settings.Fade : fade, 0);
            return true;
        }

        /// <summary>A clip's length in seconds (0 if she has no such clip).</summary>
        public float ClipLength(string clipName)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return 0f;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == clipName) return clip.length;
            return 0f;
        }

        /// <summary>Her teleport move, then the flash (for PlayMaker: Story Fairy Teleport).</summary>
        public void StartTeleport()
        {
            if (!Gone && !teleportStarted) StartCoroutine(TeleportRoutine());
        }

        /// <summary>Says a send-off line (if there is one yet), then teleports away; then runs the given step.</summary>
        public void LeaveWithLine(System.Action then)
        {
            if (Gone) return;
            StartCoroutine(Leave(settings.RandomLine(), then));
        }

        /// <summary>Teleports away at once: the green flash, her teleport sound, and she is gone.</summary>
        public void Teleport()
        {
            if (Gone) return;
            Gone = true;
            looking = false;
            Flash(Middle());

            // Gone: her body, her own light (the dark walk's), her calls.
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Fairy_Girl"))
                    foreach (var look in child.GetComponentsInChildren<Renderer>(true)) look.enabled = false;
                if (child.name == "Story_Fairy_Light") child.gameObject.SetActive(false);
            }
            foreach (var voice in GetComponents<AudioSource>()) voice.mute = true;
            var weak = GetComponent<StoryWeakFairy>();
            if (weak != null) weak.enabled = false;
        }

        // The green flash, its light and her teleport sound where she was.
        private void Flash(Vector3 at)
        {
            if (settings.FlashPrefab != null)
            {
                var flash = Instantiate(settings.FlashPrefab, at, Quaternion.identity);
                flash.transform.localScale *= settings.FlashScale;
                Destroy(flash, 5f);
            }
            var lightGo = new GameObject("Fairy_Teleport_Flash");
            lightGo.transform.position = at;
            var flashLight = lightGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = settings.FlashColour;
            flashLight.range = settings.FlashRange;
            flashLight.intensity = settings.FlashIntensity;
            flashLight.shadows = LightShadows.None;
            lightGo.AddComponent<StoryFlashFade>().Begin(settings.FlashSeconds);
            PlayAt(settings.RandomTeleportSound(), at, settings.TeleportVolume);
        }

        // Her good-luck move while she says her line, then her teleport move and the flash. She stays until the line is
        // said: the flash comes Line Tail before its end (her teleport move fills the moment before).
        private IEnumerator Leave(AudioClip line, System.Action then)
        {
            float wait = settings.LineLead;
            if (PlayState(settings.GoodLuckState)) wait = Mathf.Max(wait, ClipLength(settings.GoodLuckClip) - 0.1f);
            if (line != null)
            {
                PlayAt(line, Middle(), settings.LineVolume);
                wait = Mathf.Max(wait, line.length - settings.LineTail - ClipLength(settings.TeleportClip));
            }
            if (wait > 0f) yield return new WaitForSeconds(wait);
            yield return TeleportRoutine();
            then?.Invoke();
        }

        private IEnumerator TeleportRoutine()
        {
            if (Gone || teleportStarted) yield break;
            teleportStarted = true;
            if (PlayState(settings.TeleportState, 0.05f))
            {
                float length = ClipLength(settings.TeleportClip);
                if (length > 0f) yield return new WaitForSeconds(length);
            }
            Teleport();
        }

        private void Update()
        {
            if (watchPumpkin && !Gone)
            {
                lastMiddle = Middle();
                Dodge();
            }
        }

        // The first pumpkin falls from high up onto her (pumpkin_Start, tweened down by its own FSM): she teleports when
        // it is about Dodge Lead seconds from reaching her, judged by its speed.
        private void Dodge()
        {
            if (pumpkinBody == null)
            {
                var pumpkin = GameObject.Find(PumpkinName);
                if (pumpkin == null) return;
                pumpkinBody = pumpkin.transform.Find(PumpkinBody);
                if (pumpkinBody == null) pumpkinBody = pumpkin.transform;
                lastHeight = float.NaN;
            }
            var middle = Middle();
            var at = pumpkinBody.position;
            var flat = at - middle;
            flat.y = 0f;
            float height = at.y - middle.y;
            float speed = float.IsNaN(lastHeight) || Time.deltaTime <= 0f ? 0f : (lastHeight - height) / Time.deltaTime;
            lastHeight = height;
            if (flat.magnitude > settings.DodgeReach) return;
            float timeLeft = speed > 0.5f ? height / speed : float.MaxValue;
            // Her teleport move starts so that it ends as the flash comes (Dodge Lead before the pumpkin reaches her).
            if (!teleportStarted && timeLeft < settings.DodgeLead + ClipLength(settings.TeleportClip))
            {
                teleportStarted = true;
                PlayState(settings.TeleportState, 0.05f);
            }
            bool close = height < settings.DodgeHeight || timeLeft < settings.DodgeLead;
            if (!close) return;
            Dodged = true;
            watchPumpkin = false;
            Teleport();
        }

        // Her own FSM takes her away on the first meeting a moment before the pumpkin lands: if that comes first, the
        // flash still shows where she was.
        private void OnDestroy()
        {
            if (!watchPumpkin || Gone || !gameObject.scene.isLoaded || pumpkinBody == null) return;
            Dodged = true;
            Flash(lastMiddle);
        }

        private void LateUpdate()
        {
            if (!looking || Gone) return;
            // A free Fairy has no distress bolts (never exactly 0: see the Fairy bolts note).
            if (boltBone != null) boltBone.localScale = Vector3.one * 0.001f;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            var target = LookTarget != null ? LookTarget.position : head != null ? head.position : (Vector3?)null;
            if (target == null) return;

            float ease = 1f - Mathf.Exp(-settings.TurnSpeed * Time.deltaTime);

            // Her body faces the player (the board floats right over her, so turning to the card would spin her).
            if (head != null)
            {
                var flat = head.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                {
                    var want = Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Euler(0f, settings.FaceYaw, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, want, ease);
                }
            }

            // Her head turns and tips to what she looks at (after her animation has posed it this frame): up to the card
            // and after it as it slides, or to the player.
            if (headBone == null || settings.HeadTilt <= 0f) return;
            var local = transform.InverseTransformDirection(target.Value - headBone.position);
            float wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(local.z, 0.3f)) * Mathf.Rad2Deg, -settings.HeadTilt, settings.HeadTilt);
            float flatLength = new Vector2(local.x, local.z).magnitude;
            float wantPitch = Mathf.Clamp(Mathf.Atan2(local.y, Mathf.Max(0.001f, flatLength)) * Mathf.Rad2Deg, -settings.HeadTilt, settings.HeadTilt);
            yaw = Mathf.Lerp(yaw, wantYaw, ease);
            pitch = Mathf.Lerp(pitch, wantPitch, ease);
            headBone.rotation = Quaternion.AngleAxis(yaw, transform.up) *
                                Quaternion.AngleAxis(settings.HeadTiltSign * pitch, transform.right) * headBone.rotation;
        }

        /// <summary>
        /// Where she really is: her skinned body's bounds, kept up to date (the stored bounds stay where the model was
        /// made, about 1 m from where her animation flies her).
        /// </summary>
        public Vector3 Middle()
        {
            bool any = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Fairy_Girl")) continue;
                foreach (var look in child.GetComponentsInChildren<Renderer>())
                {
                    if (look is SkinnedMeshRenderer skin && !skin.updateWhenOffscreen) skin.updateWhenOffscreen = true;
                    if (!any) { bounds = look.bounds; any = true; }
                    else bounds.Encapsulate(look.bounds);
                }
            }
            return any ? bounds.center : transform.position + Vector3.up * 0.3f;
        }

        // A sound where she is, through a copy of her own speaker's settings (its mixer, its 3D fall-off), heard even
        // though her own speaker is muted.
        private void PlayAt(AudioClip clip, Vector3 at, float volume)
        {
            if (clip == null) return;
            var go = new GameObject("Fairy_Voice");
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            if (template != null)
            {
                source.outputAudioMixerGroup = template.outputAudioMixerGroup;
                source.spatialBlend = template.spatialBlend;
                source.rolloffMode = template.rolloffMode;
                source.minDistance = template.minDistance;
                source.maxDistance = template.maxDistance;
                source.dopplerLevel = 0f;
            }
            else source.spatialBlend = 1f;
            source.clip = clip;
            source.volume = volume;
            source.Play();
            Destroy(go, clip.length + 0.2f);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
