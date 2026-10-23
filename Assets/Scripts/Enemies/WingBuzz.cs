using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Makes a flier's wings buzz like a real bee's: while it flies, each wing bone beats fast (Beats Per Second, about
    /// 26) around the middle of its own flap and along the same swing as its animation, instead of the animation's slow
    /// flap (about 6 beats a second). Only the wing bones; the body keeps its animations. On the headset the fast beat
    /// reads as a buzzing blur. It stops when the wings come off (the bee drops to the floor) and when it dies, so those
    /// animations play as made. Runs after the Animator (LateUpdate).
    /// Goes on the bee's root, next to its Spider Brain; Tools > 429 Game > Enemies > Bee Wing Buzz measures the wings
    /// from the Bee_Fly clip and fills it in.
    /// </summary>
    [DisallowMultipleComponent]
    public class WingBuzz : MonoBehaviour
    {
        [Serializable]
        public class Wing
        {
            [Tooltip("The wing bone's name, e.g. Up_Wing_01.L.")]
            public string bone;
            [Tooltip("Its pose in the middle of its flap (local rotation).")]
            public Quaternion middle = Quaternion.identity;
            [Tooltip("The local axis it swings about.")]
            public Vector3 axis = Vector3.forward;
            [Tooltip("Its whole swing in the animation, in degrees.")]
            public float swing = 60f;
            [Tooltip("Where in the beat it is, 0 to 1 (the lower wings a little behind the upper ones).")]
            [Range(0f, 1f)]
            public float phase;
        }

        [SerializeField] private Wing[] wings = Array.Empty<Wing>();
        [Tooltip("Wing beats per second. Real bees beat hundreds of times; 20-30 reads as a buzz on the headset.")]
        [Min(1f)]
        [SerializeField] private float beatsPerSecond = 26f;
        [Tooltip("How much of the animation's swing each beat covers (0.8 = 80%).")]
        [Range(0.1f, 1.5f)]
        [SerializeField] private float swingShare = 0.8f;

        private Transform[] bones;
        private SpiderBrain brain;
        private float offset;

        private void Awake()
        {
            TryGetComponent(out brain);
            bones = new Transform[wings.Length];
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < wings.Length; i++)
            {
                foreach (var t in all)
                {
                    if (t.name != wings[i].bone) continue;
                    bones[i] = t;
                    break;
                }
            }
            // Bees in a swarm don't beat in step.
            offset = UnityEngine.Random.value;
        }

        private void LateUpdate()
        {
            if (brain != null && (!brain.IsFlying || brain.Doing == "Dead" || brain.Doing == "Appear")) return;
            float beat = (Time.time * beatsPerSecond + offset) * Mathf.PI * 2f;
            for (int i = 0; i < wings.Length; i++)
            {
                var bone = bones[i];
                if (bone == null) continue;
                var wing = wings[i];
                float angle = wing.swing * 0.5f * swingShare * Mathf.Sin(beat + wing.phase * Mathf.PI * 2f);
                bone.localRotation = wing.middle * Quaternion.AngleAxis(angle, wing.axis);
            }
        }
    }
}
