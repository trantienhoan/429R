using UnityEngine;

namespace Game.Shopping
{
    /// <summary>
    /// Added at runtime (by CandyBoxSpill) to each candy a Candy Box spills: for a moment the candy can't touch the
    /// player's Mouth, so breaking a box in front of the face doesn't feed the player its candies straight away (play test
    /// 2026-11-10: 1-2 of the 6 were eaten at once). Afterwards it's a normal candy again and removes itself.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpilledCandyMouthDelay : MonoBehaviour
    {
        private const string MouthName = "Mouth";
        private Collider[] parts;
        private Collider mouth;
        private float until;

        /// <summary>Keeps this candy away from the Mouth for this many seconds.</summary>
        public void Hold(float seconds)
        {
            until = Time.time + seconds;
            mouth = FindMouth();
            parts = GetComponentsInChildren<Collider>();
            Ignore(true);
        }

        private void Update()
        {
            if (Time.time < until) return;
            Ignore(false);
            Destroy(this);
        }

        private void OnDestroy() => Ignore(false);

        private void Ignore(bool ignore)
        {
            if (mouth == null || parts == null) return;
            foreach (var part in parts)
            {
                if (part != null) Physics.IgnoreCollision(part, mouth, ignore);
            }
        }

        // The small trigger ball "Mouth" under the player's camera (Tools > 429 Game > Candy Mouth set-up).
        private static Collider FindMouth()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var found = cam.transform.Find(MouthName);
            return found != null ? found.GetComponent<Collider>() : null;
        }
    }
}
