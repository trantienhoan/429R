using System.Collections.Generic;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The road the Paint Bees fly in on (Paint_Bees_Road; user, 2026-11-03: "deco the bee fly road with some particle
    /// lights the way, make it fun and interesting to look at, dangerously also"), laid on the floor in front of the
    /// player when the bees start (its +Z the way the bees come from), all to the song's beat:
    /// - lanes fanning out from the player towards where the bees come from, each a thin red laser line lined with
    ///   runway lights, both throbbing on every beat, and a glow at its far end where the bees come in;
    /// - on every half beat a sparkling light sets off from the far end of each lane and marches in to the player's feet,
    ///   gold far off and hot red close by (bigger on the beat);
    /// - embers rising off the floor all over it;
    /// - a red danger ring round the player's feet, throbbing to the beat, that flares and flickers while a bee dives.
    /// The round drives it (Half Beat, Diving) and fades it out at the end (Stop).
    /// </summary>
    public class PaintBeesRoad : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Tooltip("The lights that march in along the lanes (emitted by the road; no emission of their own).")]
        [SerializeField] private ParticleSystem marchers;
        [Tooltip("The still lights along the lanes (laid by the road at the start).")]
        [SerializeField] private ParticleSystem runway;
        [Tooltip("The glows at the lanes' far ends, where the bees come in (laid by the road at the start).")]
        [SerializeField] private ParticleSystem laneEnds;
        [Tooltip("Embers rising off the floor (they play by themselves).")]
        [SerializeField] private ParticleSystem embers;
        [Tooltip("The danger ring round the player's feet.")]
        [SerializeField] private Renderer dangerRing;
        [Tooltip("The lanes' laser lines (an additive material). Empty: no lines.")]
        [SerializeField] private Material laneMaterial;
        [SerializeField] private Color laneColour = new(1f, 0.1f, 0.05f, 1f);
        [Tooltip("The lanes, in degrees from straight ahead.")]
        [SerializeField] private float[] lanes = { -50f, -25f, 0f, 25f, 50f };
        [Tooltip("The lanes run from this far from the player (x) to this close (y), in metres.")]
        [SerializeField] private Vector2 reach = new(7f, 0.9f);
        [Tooltip("Beats a marching light takes to come in.")]
        [Min(0.5f)]
        [SerializeField] private float marchBeats = 4f;
        [Tooltip("Metres between the still lights along a lane.")]
        [Min(0.2f)]
        [SerializeField] private float runwayEvery = 0.6f;
        [SerializeField] private Color runwayColour = new(1f, 0.6f, 0.15f, 1f);
        [SerializeField] private Color ringColour = new(1f, 0.12f, 0.06f, 1f);
        [Tooltip("Seconds it takes to fade out at the end.")]
        [Min(0.1f)]
        [SerializeField] private float fadeSeconds = 1.5f;

        // Over the floor: the arena's floor past its middle circle is a few centimetres higher.
        // Lanes from fires stop this far short of them, in metres.
        private const float FireClear = 1f;
        private const float OverFloor = 0.07f;
        private const float MarchOverFloor = 0.12f;

        private PaintBeesRound round;
        private MaterialPropertyBlock block;
        private LineRenderer[] laneLines = System.Array.Empty<LineRenderer>();
        private float shown = 0f;
        private bool stopping;

        /// <summary>A bee is diving at the player right now: the danger ring flares.</summary>
        public bool Diving { get; set; }

        /// <summary>
        /// Its lanes lead from these (the fire columns the bees fly out of), out to just short of each, instead of its own
        /// fan in front. Before Setup.
        /// </summary>
        public void LeadFrom(List<Transform> from)
        {
            var angles = new List<float>();
            float far = 0f;
            foreach (var t in from)
            {
                if (t == null) continue;
                var flat = t.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude < 1f) continue;
                angles.Add(Vector3.SignedAngle(transform.forward, flat, Vector3.up));
                far = Mathf.Max(far, flat.magnitude - FireClear);
            }
            if (angles.Count == 0) return;
            lanes = angles.ToArray();
            reach = new Vector2(far, reach.y);
        }

        /// <summary>Lays the road for this round (it throbs to its beat).</summary>
        public void Setup(PaintBeesRound owner)
        {
            round = owner;
            block = new MaterialPropertyBlock();
            if (runway != null)
            {
                var light = new ParticleSystem.EmitParams { startLifetime = 100000f, velocity = Vector3.zero };
                foreach (float angle in lanes)
                {
                    var way = Way(angle);
                    for (float d = reach.y + 0.3f; d <= reach.x; d += runwayEvery)
                    {
                        light.position = transform.position + way * d + Vector3.up * OverFloor;
                        light.startSize = Mathf.Lerp(0.3f, 0.2f, d / reach.x);
                        runway.Emit(light, 1);
                    }
                }
            }
            if (laneEnds != null)
            {
                var glow = new ParticleSystem.EmitParams { startLifetime = 100000f, velocity = Vector3.zero, startSize = 1.2f };
                foreach (float angle in lanes)
                {
                    glow.position = transform.position + Way(angle) * reach.x + Vector3.up * OverFloor;
                    laneEnds.Emit(glow, 1);
                }
            }
            if (embers != null) embers.Play();
            MakeLaneLines();
            Paint(0f);
        }

        // A thin laser line down each lane, brighter close by.
        private void MakeLaneLines()
        {
            if (laneMaterial == null) return;
            laneLines = new LineRenderer[lanes.Length];
            for (int i = 0; i < lanes.Length; i++)
            {
                var line = new GameObject("Lane " + (i + 1)).AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                var way = Way(lanes[i]);
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPosition(0, transform.position + way * reach.y + Vector3.up * OverFloor);
                line.SetPosition(1, transform.position + way * reach.x + Vector3.up * OverFloor);
                line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0.04f));
                line.alignment = LineAlignment.TransformZ;
                line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                line.sharedMaterial = laneMaterial;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                laneLines[i] = line;
            }
        }

        /// <summary>A half beat of the song: a light sets off down every lane (bigger on a whole beat).</summary>
        public void HalfBeat(bool wholeBeat, float beatSeconds)
        {
            if (stopping || marchers == null) return;
            float seconds = marchBeats * beatSeconds;
            var light = new ParticleSystem.EmitParams { startLifetime = seconds, startSize = wholeBeat ? 0.75f : 0.5f };
            foreach (float angle in lanes)
            {
                var way = Way(angle);
                light.position = transform.position + way * reach.x + Vector3.up * MarchOverFloor;
                light.velocity = -way * ((reach.x - reach.y) / seconds);
                marchers.Emit(light, 1);
            }
        }

        /// <summary>It fades out and is gone.</summary>
        public void Stop()
        {
            if (stopping) return;
            stopping = true;
            if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, fadeSeconds + 3f);
        }

        private void Update()
        {
            float target = stopping ? 0f : 1f;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime / fadeSeconds);
            Paint(round != null ? round.BeatPulse : 0f);
        }

        // The runway throbs to the beat; the ring too, and while a bee dives it flares, flickering hot.
        private void Paint(float pulse)
        {
            if (block == null) return;
            if (runway != null)
            {
                var r = runway.GetComponent<ParticleSystemRenderer>();
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, runwayColour * ((0.6f + 1.1f * pulse) * shown));
                r.SetPropertyBlock(block);
            }
            if (laneEnds != null)
            {
                var r = laneEnds.GetComponent<ParticleSystemRenderer>();
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(ringColour, runwayColour, 0.35f) * ((0.5f + 1.0f * pulse) * shown));
                r.SetPropertyBlock(block);
            }
            if (marchers != null)
            {
                var r = marchers.GetComponent<ParticleSystemRenderer>();
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.white * shown);
                r.SetPropertyBlock(block);
            }
            foreach (var line in laneLines)
            {
                if (line == null) continue;
                var near = laneColour * ((0.75f + 1.1f * pulse) * shown);
                var far = near;
                near.a = shown;
                far.a = 0.7f * shown;
                line.startColor = near;
                line.endColor = far;
            }
            if (dangerRing != null)
            {
                float flare = Diving ? 1.3f + 0.8f * Mathf.Abs(Mathf.Sin(Time.time * 26f)) : 0.55f + 0.9f * pulse;
                var colour = Diving ? Color.Lerp(ringColour, Color.white, 0.25f) : ringColour;
                dangerRing.GetPropertyBlock(block);
                block.SetColor(BaseColor, colour * (flare * shown));
                dangerRing.SetPropertyBlock(block);
                dangerRing.transform.localScale = Vector3.one * (1.7f * (1f + 0.06f * pulse + (Diving ? 0.05f : 0f)));
            }
        }

        // A lane's way out from the player, flat.
        private Vector3 Way(float angle) => transform.rotation * (Quaternion.Euler(0f, angle, 0f) * Vector3.forward);
    }
}
