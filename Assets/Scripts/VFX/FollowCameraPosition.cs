using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Keeps this object centered on the player's camera (position only, it never turns), the way a sky works: the
    /// twinkling stars stay the same distance away wherever the player walks, so they never drift past the camera's
    /// far clip plane. Tools > 429 Game > VFX > Add Twinkling Stars To Skydome adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class FollowCameraPosition : MonoBehaviour
    {
        private Transform target;

        private void LateUpdate()
        {
            if (target == null)
            {
                var main = Camera.main;
                if (main == null) return;
                target = main.transform;
            }
            transform.position = target.position;
        }
    }
}
