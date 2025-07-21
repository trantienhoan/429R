using System.Collections;
using UnityEngine;

namespace Cam
{
    public class VRRigShake : MonoBehaviour
    {
        public static VRRigShake Instance;

        private Vector3 originalPosition;

        private void Awake()
        {
            Instance = this;
            originalPosition = transform.localPosition;
        }

        public void Shake(float intensity, float duration)
        {
            StopAllCoroutines();
            StartCoroutine(ShakeRoutine(intensity, duration));
        }

        private IEnumerator ShakeRoutine(float intensity, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                Vector3 offset = Random.insideUnitSphere * intensity;
                transform.localPosition = originalPosition + offset;
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = originalPosition;
        }
    }
}