using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>Finds parts of the player's XR rig, e.g. the "Left Controller" and "Right Controller" objects.</summary>
    public static class XRRig
    {
        /// <summary>
        /// The object named "Left Controller" / "Right Controller" under the XR Origin, or else the first object
        /// whose name says left/right and controller/hand.
        /// </summary>
        public static Transform FindController(bool left)
        {
            string exact = left ? "Left Controller" : "Right Controller";
            string side = left ? "left" : "right";

            var roots = new List<Transform>();
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null) roots.Add(origin.transform);
            else foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) roots.Add(root.transform);

            Transform fallback = null;
            foreach (var root in roots)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == exact) return t;

                    var lower = t.name.ToLowerInvariant();
                    if (fallback == null && lower.Contains(side) && (lower.Contains("controller") || lower.Contains("hand"))
                        && !lower.Contains("stabilized") && !lower.Contains("attach"))
                        fallback = t;
                }
            }
            return fallback;
        }
    }
}
