using System.Collections;
using System.Linq;
using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(SpiderController)), DefaultExecutionOrder(2)]
    public class Spider : MonoBehaviour
    {
        [SerializeField] private Transform[] legs;
        [SerializeField] private Transform[] orbits;
        [SerializeField] private Transform[] targets;
        [SerializeField] private Transform[] targetsG1;
        
        private float[] orbitsLegsDists;
        private float[] orbitsOriginsDists;

        [SerializeField] private float stepTime = 0.25f;
        [Range(0, 1)] private float stepDelayRand = 0f;
        [SerializeField] private float stepHeight = 0.6f;
        [SerializeField] private AnimationCurve stepHeightCurve;
        [SerializeField, Range(0, 1)] private float desyncG2 = 1;

        [SerializeField] private UpdateOrbitsMethode updateOrbitsMethode = UpdateOrbitsMethode.LegDistance;

        private enum UpdateOrbitsMethode
        {
            LegDistance,
            Score
        }

        [SerializeField, Range(0, 360)] private float arcAngle = 270;
        [SerializeField] private float arcRadius = 0.3f;
        [SerializeField] private int arcResolution = 4;
        [SerializeField] private LayerMask arcLayer;

        [SerializeField] private AudioClip[] stepStartSounds;
        [SerializeField] private AudioClip[] stepMoveSounds;
        [SerializeField] private AudioClip[] stepEndSounds;
        [SerializeField, Range(0, 1)] private float stepSoundSpeedProgress = 0.3f;
        [SerializeField, Range(0, 1)] private float stepSoundVolume = 1;
        [SerializeField] private float stepSoundPitchMin = 0.9f;
        [SerializeField] private float stepSoundPitchMax = 1.1f;

        [SerializeField] private bool drawGizmos = true;

        private SpiderController spiderController;

        public Transform[] legsArray => legs;
        public Transform[] targetsArray => targets;
        public Transform[] targetsG1Array => targetsG1;
        public Transform[] orbitsArray => orbits;

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            if (legs == null || targets == null || orbits == null ||
                legs.Length != targets.Length || legs.Length != orbits.Length)
                return;

            if (!Application.isPlaying)
                Cache();

            UpdateOrbits(true);
            GizmoDrawEndStep();
        }

        void Awake()
        {
            Cache();
        }

        void Cache()
        {
            spiderController = GetComponent<SpiderController>();
            CacheOrbitsDists();
        }

        void CacheOrbitsDists()
        {
            if (orbits == null || legs == null)
                return;
                
            orbitsLegsDists = new float[orbits.Length];
            orbitsOriginsDists = new float[orbits.Length];

            for (int i = 0; i < orbits.Length; i++)
            {
                if (orbits[i] != null && legs[i] != null && orbits[i].parent != null)
                {
                    orbitsLegsDists[i] = (orbits[i].position - legs[i].position).magnitude;
                    orbitsOriginsDists[i] = (orbits[i].position - orbits[i].parent.position).magnitude;
                }
            }
        }

        private void OnEnable()
        {
            StartCoroutine(StepLoop());
        }

        void OnDisable()
        {
            StopAllCoroutines();
        }

        void Update()
        {
            UpdateOrbits();
        }

        public void UpdateOrbits(bool gizmo = false)
        {
            if (orbits == null || orbits.Length == 0 || 
                legs == null || legs.Length == 0 ||
                orbitsLegsDists == null || orbitsOriginsDists == null)
                return;

            if (updateOrbitsMethode == UpdateOrbitsMethode.LegDistance)
            {
                for (int i = 0; i < orbits.Length; i++)
                {
                    if (orbits[i] != null && orbits[i].parent != null && legs[i] != null)
                    {
                        UpdateOrbitLegDistance(orbits[i], orbits[i].parent, legs[i], 
                            orbitsLegsDists[i], orbitsOriginsDists[i], gizmo);
                    }
                }
            }
            else if (updateOrbitsMethode == UpdateOrbitsMethode.Score)
            {
                for (int i = 0; i < orbits.Length; i++)
                {
                    if (orbits[i] != null && orbits[i].parent != null && legs[i] != null)
                    {
                        UpdateOrbitScore(orbits[i], orbits[i].parent, legs[i], 
                            orbitsLegsDists[i], orbitsOriginsDists[i], gizmo);
                    }
                }
            }
        }

        void UpdateOrbitScore(Transform orbit, Transform orbitOrigin, Transform leg, float dstLeg, float dstOrigin,
            bool gizmo = false)
        {
            Vector3 pos = orbitOrigin.position;
            Quaternion rot = orbitOrigin.rotation;

            Quaternion dRot = Quaternion.Inverse(transform.rotation) * rot;

            if (!gizmo)
            {
                orbit.position = pos;
                orbit.rotation = rot * Quaternion.Inverse(dRot);
            }

            float score = Mathf.Pow((pos - leg.position).magnitude - dstLeg, 2) +
                          Mathf.Pow((pos - orbitOrigin.position).magnitude - dstOrigin, 2);
            float scoreCrt = score;
            float scoreMax = (Mathf.Pow(dstLeg, 2) + Mathf.Pow(dstOrigin, 2)) * 1f;

            if (gizmo)
                Gizmos.color = new Color(1, 0.5f, 0, 0.3f);

            for (int i = 0; i < 50 && scoreCrt < scoreMax; i++)
            {
                if (PhysicsExtension.ArcCast(pos, rot, arcAngle, arcRadius, arcResolution, arcLayer,
                        out RaycastHit hit))
                {
                    if (gizmo)
                        Gizmos.DrawSphere(hit.point, 0.05f);

                    pos = hit.point;
                    rot.MatchUp(hit.normal);
                    scoreCrt = Mathf.Pow((pos - leg.position).magnitude - dstLeg, 2) +
                               Mathf.Pow((pos - orbitOrigin.position).magnitude - dstOrigin, 2);

                    if (scoreCrt < score)
                    {
                        if (!gizmo)
                        {
                            orbit.position = pos;
                            orbit.rotation = rot * Quaternion.Inverse(dRot);
                        }

                        score = scoreCrt;
                    }
                }
                else return;
            }
        }

        void UpdateOrbitLegDistance(Transform orbit, Transform orbitOrigin, Transform leg, float distLeg, float distOrigin,
             bool gizmo = false)
        {
            Vector3 pos = orbitOrigin.position;
            Quaternion rot = orbitOrigin.rotation;

            Quaternion dRot = Quaternion.Inverse(transform.rotation) * rot;

            float dist = (pos - leg.position).magnitude;
            bool checkSup = dist < distLeg;

            for (int i = 0; i < 50 && dist < distLeg * 1.5f; i++)
            {
                if (PhysicsExtension.ArcCast(pos, rot, arcAngle, arcRadius, arcResolution, arcLayer,
                        out RaycastHit hit))
                {
                    if (gizmo)
                    {
                        Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
                        Gizmos.DrawSphere(hit.point, 0.05f);
                    }

                    Vector3 nextPos = hit.point;
                    Quaternion nextRot = MathExtension.RotationMatchUp(rot, hit.normal);
                    float nextDist = (nextPos - leg.position).magnitude;

                    if (checkSup == nextDist > distLeg)
                    {
                        float progress = Mathf.InverseLerp(dist, nextDist, distLeg);

                        if (gizmo)
                        {
                            Gizmos.color = new Color(1, 0.5f, 0, 1);
                            Gizmos.DrawSphere(Vector3.Lerp(pos, nextPos, progress), 0.1f);
                        }
                        else
                        {
                            orbit.position = Vector3.Lerp(pos, nextPos, progress);
                            orbit.rotation = Quaternion.Lerp(rot * Quaternion.Inverse(dRot),
                                nextRot * Quaternion.Inverse(dRot), progress);
                        }

                        checkSup = !checkSup;
                        //return;
                    }

                    pos = nextPos;
                    rot = nextRot;
                    dist = nextDist;
                }
                else return;
            }
        }

        public (Vector3, Quaternion) EndStep(Transform orbit, bool gizmo = false)
        {
            if (spiderController == null || spiderController.Velocity == Vector2.zero)
                return (orbit.position, orbit.rotation);

            Vector3 velocityForward = orbit.TransformVector(spiderController.Velocity3);

            // warning without
            if (velocityForward == Vector3.zero || velocityForward == orbit.up)
                return (orbit.position, orbit.rotation);

            Vector3 pos = orbit.position;
            Quaternion rot = Quaternion.LookRotation(velocityForward, orbit.up);

            float dist = spiderController.Speed * stepTime / 2;

            if (gizmo)
                Gizmos.color = new Color(1, 0, 0, 0.3f);

            for (int stepCount = 0; stepCount < 100; stepCount++)
            {
                if (PhysicsExtension.ArcCast(pos, rot, arcAngle, arcRadius, arcResolution, arcLayer,
                        out RaycastHit hit))
                {
                    if (gizmo)
                        Gizmos.DrawSphere(hit.point, 0.05f);

                    float distCrt = (hit.point - pos).magnitude;

                    if (distCrt >= dist)
                    {
                        Vector3 nextPos = hit.point;
                        Quaternion nextRot = MathExtension.RotationMatchUp(rot, hit.normal);

                        float progress = Mathf.InverseLerp(0, distCrt, dist);

                        pos = Vector3.Lerp(pos, nextPos, progress);
                        rot = Quaternion.Lerp(rot, nextRot, progress);

                        if (gizmo)
                        {
                            Gizmos.color = new Color(1, 0, 0, 1);
                            Gizmos.DrawSphere(pos, 0.1f);
                        }

                        break;
                    }

                    dist -= distCrt;

                    pos = hit.point;
                    rot.MatchUp(hit.normal);
                }
                else break;
            }

            return (pos, rot);
        }

        void GizmoDrawEndStep()
        {
            if (orbits == null)
                return;
                
            for (int i = 0; i < orbits.Length; i++)
            {
                if (orbits[i] != null)
                {
                    EndStep(orbits[i], true);
                }
            }
        }

        IEnumerator StepLoop()
        {
            bool isGroup1 = true;

            while (isActiveAndEnabled)
            {
                if (targets != null && targetsG1 != null)
                {
                    for (int i = 0; i < targets.Length; i++)
                    {
                        if (targets[i] != null && 
                            (isGroup1 == targetsG1.Contains(targets[i])))
                        {
                            StartCoroutine(DelayStep(i, stepTime * stepDelayRand * Random.value));
                        }
                    }
                }

                yield return new WaitForSeconds(stepTime * (isGroup1 ? desyncG2 : 2 - desyncG2));

                isGroup1 = !isGroup1;
            }
        }
        
        #pragma warning restore CS1998

        IEnumerator DelayStep(int idx, float delay)
        {
            if (delay > 0)
                yield return new WaitForSeconds(delay);

            StartCoroutine(Step(idx));
        }

        IEnumerator Step(int idx)
        {
            if (targets == null || orbits == null || legs == null || 
                idx < 0 || idx >= targets.Length || idx >= orbits.Length || idx >= legs.Length)
                yield break;

            float t = 0, progress;

            Transform target = targets[idx];
            Transform orbit = orbits[idx];
            Transform leg = legs[idx];

            if (target == null || orbit == null || leg == null)
                yield break;

            Vector3 targetStartPosProj = leg.InverseTransformPoint(target.position);
            Quaternion targetStartRotProj = Quaternion.Inverse(leg.rotation) * target.rotation;

            PlayStepSound(target.position, stepStartSounds);
            PlayStepSound(target.position, stepMoveSounds);

            while (t < stepTime)
            {
                t += Time.deltaTime;
                progress = Mathf.Clamp01(t / stepTime);

                Vector3 startPos, endPos;
                Quaternion startRot, endRot;
                startPos = leg.TransformPoint(targetStartPosProj);
                startRot = leg.rotation * targetStartRotProj;
                (endPos, endRot) = EndStep(orbit);

                target.position = Vector3.Lerp(startPos, endPos, progress);
                
                if (spiderController != null)
                {
                    target.position += stepHeight * spiderController.SpeedProgress * 
                                       stepHeightCurve.Evaluate(progress) * leg.up;
                }

                target.rotation = Quaternion.Lerp(startRot, endRot, progress);

                if (t < stepTime)
                    yield return new WaitForEndOfFrame();
            }

            PlayStepSound(target.position, stepEndSounds);
        }

        void PlayStepSound(Vector3 pos, AudioClip[] sounds)
        {
            if (sounds == null || sounds.Length == 0 || spiderController == null || 
                spiderController.SpeedProgress <= stepSoundSpeedProgress)
                return;

            AudioClip clip = sounds[(int)(Random.value * sounds.Length)];
            float volume = Mathf.InverseLerp(stepSoundSpeedProgress, 1, spiderController.SpeedProgress) *
                           stepSoundVolume;
            float pitch = Random.Range(stepSoundPitchMin, stepSoundPitchMax);

            if (sounds == stepMoveSounds)
                pitch *= clip.length / stepTime;

            AudioSourceExtension.PlayClipAtPoint(clip, pos, volume, pitch);
        }
    }
}