using System.Collections;
using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// A magic table's skull (Skull_Chaos, Skull_Stuffs, Skull_Pumpkin) coming out from under the lifted hat (user,
    /// 2026-11-06: the hats felt fake): it pops up out of the hat's spot, growing past its size and settling back with a
    /// quick spin, a magic sound and a puff (its own Smoke_Blast puffs too). Then it's easy to break: the player near it
    /// counts (its Trigger reaches Near Reach metres round it, instead of the 4 cm it started with), so GAMESTAGES gets
    /// the touch it waits for without the player having to poke the skull itself; its FSM still grows the Trigger after
    /// a while, as before, so it gets them in the end anyway.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkullReveal : MonoBehaviour
    {
        [Header("Coming out of the hat")]
        [Tooltip("How long it takes to pop up, in seconds.")]
        [Min(0.05f)]
        [SerializeField] private float popSeconds = 0.45f;
        [Tooltip("How far past its size it grows before it settles.")]
        [Range(1f, 2f)]
        [SerializeField] private float overshoot = 1.25f;
        [Tooltip("The extra turn it makes as it pops up, in degrees.")]
        [SerializeField] private float spinDegrees = 360f;
        [Tooltip("Played where it comes out (Effect Pool), this big.")]
        [SerializeField] private GameObject[] effects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float effectSize = 0.5f;
        [SerializeField] private AudioClip sound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.9f;

        [Header("Breaking it")]
        [Tooltip("Its Trigger (the sphere GAMESTAGES listens to for the player's touch).")]
        [SerializeField] private SphereCollider touch;
        [Tooltip("The player this close to it, in metres, touches it (its Trigger is made this big)...")]
        [Min(0.05f)]
        [SerializeField] private float nearReach = 0.6f;
        [Tooltip("...once it has popped up and been seen this long, in seconds.")]
        [Min(0f)]
        [SerializeField] private float nearAfter = 0.6f;

        private void Awake()
        {
            if (touch == null)
            {
                var child = transform.Find("Trigger");
                if (child != null) touch = child.GetComponent<SphereCollider>();
            }
        }

        private void Start() => StartCoroutine(Reveal());

        private IEnumerator Reveal()
        {
            var full = transform.localScale;
            transform.localScale = full * 0.01f;
            foreach (var effect in effects)
            {
                if (effect != null) EffectPool.Play(effect, transform.position, effectSize);
            }
            if (sound != null) AudioSource.PlayClipAtPoint(sound, transform.position, volume);
            for (float t = 0f; t < popSeconds; t += Time.deltaTime)
            {
                float k = t / popSeconds;
                // Up past its size, then back: a little bounce.
                float s = k < 0.6f ? Mathf.Lerp(0.01f, overshoot, Mathf.SmoothStep(0f, 1f, k / 0.6f)) : Mathf.Lerp(overshoot, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
                transform.localScale = full * s;
                transform.Rotate(0f, spinDegrees * Time.deltaTime / popSeconds, 0f, Space.Self);
                yield return null;
            }
            transform.localScale = full;

            yield return new WaitForSeconds(nearAfter);
            if (touch != null)
            {
                float scale = Mathf.Max(0.0001f, Mathf.Abs(touch.transform.lossyScale.x));
                touch.radius = Mathf.Max(touch.radius, nearReach / scale);
            }
        }
    }
}
