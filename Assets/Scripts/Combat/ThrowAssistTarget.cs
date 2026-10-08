using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Makes this something the Throw Assist aims at, for targets without the Enemy or Monster tag (e.g. the ghost
    /// balloons). Goes on the target's root.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThrowAssistTarget : MonoBehaviour
    {
        [Tooltip("Optional: the point throws are aimed at. Empty: the middle of its collider.")]
        [SerializeField] private Transform aimPoint;

        internal static readonly List<ThrowAssistTarget> All = new();

        public Vector3 AimPoint => aimPoint != null ? aimPoint.position : ThrowAssist.MiddleOf(gameObject);

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }
    }
}
