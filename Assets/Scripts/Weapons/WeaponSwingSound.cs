using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// A swish when a held weapon is swung fast (user 2026-11-25, ElevenLabs weapon_swing_1-3): when its far end goes
    /// faster than Swing Speed, one plays at random (louder the faster), and not again until the swing has slowed down.
    /// Only while a hand holds it (not a socket). The speed is measured against the player's rig, so walking, turning
    /// and teleporting don't swish. On the broom, the bat and the frying pan (Tools > 429 Game > Audio > Wire
    /// ElevenLabs Sounds (P1)).
    /// </summary>
    public class WeaponSwingSound : MonoBehaviour
    {
        [Tooltip("One at random for each swing, never the same twice in a row (weapon_swing_1-3).")]
        [SerializeField] private AudioClip[] swings = System.Array.Empty<AudioClip>();
        [Tooltip("Speed of the weapon's far end (m/s) that makes a swish.")]
        [Min(0.5f)]
        [SerializeField] private float swingSpeed = 4.5f;
        [Tooltip("Speed (m/s) at which the swish is at full volume (at Swing Speed it is at half).")]
        [Min(0.5f)]
        [SerializeField] private float fullSpeed = 9f;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;
        [Tooltip("Seconds before another swish, however it is swung.")]
        [Min(0f)]
        [SerializeField] private float gap = 0.25f;

        private XRGrabInteractable grab;
        private Transform rig;
        private Vector3 tipLocal;
        private Vector3 lastTip;
        private bool hasLast;
        private bool armed = true;
        private float lastAt = -10f;
        private int lastIndex = -1;
        private AudioSource speaker;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            tipLocal = FarEnd();
        }

        private void OnDisable() => hasLast = false;

        // The point of its meshes farthest from where it is held (its attach point, or its own origin).
        private Vector3 FarEnd()
        {
            Vector3 grip = grab != null && grab.attachTransform != null
                ? transform.InverseTransformPoint(grab.attachTransform.position)
                : Vector3.zero;
            Vector3 best = grip;
            float bestDistance = -1f;
            foreach (var look in GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = look is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : look is MeshRenderer && look.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var local = transform.InverseTransformPoint(look.transform.TransformPoint(corner));
                    float distance = (local - grip).sqrMagnitude;
                    if (distance <= bestDistance) continue;
                    bestDistance = distance;
                    best = local;
                }
            }
            return best;
        }

        private void LateUpdate()
        {
            bool held = grab != null && grab.isSelected && !(grab.firstInteractorSelecting is XRSocketInteractor);
            float dt = Time.deltaTime;
            if (!held || dt <= 0f || dt > 0.1f)
            {
                hasLast = false;
                armed = true;
                return;
            }
            if (rig == null)
            {
                var origin = FindAnyObjectByType<XROrigin>();
                if (origin == null) return;
                rig = origin.transform;
            }
            // Where the far end is in the rig's space: the rig moving or turning doesn't count as a swing.
            var tip = rig.InverseTransformPoint(transform.TransformPoint(tipLocal));
            if (!hasLast)
            {
                lastTip = tip;
                hasLast = true;
                return;
            }
            float speed = Vector3.Distance(tip, lastTip) * rig.lossyScale.x / dt;
            lastTip = tip;

            if (speed < swingSpeed * 0.6f) armed = true;
            if (!armed || speed < swingSpeed || Time.time - lastAt < gap) return;
            armed = false;
            lastAt = Time.time;
            Swish(speed);
        }

        private void Swish(float speed)
        {
            if (swings == null || swings.Length == 0) return;
            int i = Random.Range(0, swings.Length);
            if (swings.Length > 1 && i == lastIndex) i = (i + 1) % swings.Length;
            lastIndex = i;
            if (swings[i] == null) return;
            if (speaker == null)
            {
                speaker = gameObject.AddComponent<AudioSource>();
                speaker.playOnAwake = false;
                speaker.spatialBlend = 1f;
                speaker.minDistance = 1f;
                speaker.maxDistance = 15f;
                speaker.dopplerLevel = 0f;
            }
            speaker.pitch = Random.Range(0.95f, 1.05f);
            float loud = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(swingSpeed, fullSpeed, speed));
            speaker.PlayOneShot(swings[i], volume * loud);
        }
    }
}
