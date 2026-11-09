using UnityEngine;

namespace Game.Bowling
{
    /// <summary>
    /// The numbers of the Gate 2 bowling arena (slime version, 2026-11-08), in metres, in the frame of the arena's "X"
    /// child (turned 45 degrees). Lane k runs along LaneDir(k) from the centre: 0 = +Z (the player faces it at the start),
    /// 1 = -X, 2 = -Z, 3 = +X. A point on a lane is "along" metres out from the centre and "side" metres to its right.
    /// The arena menu, the pin sets, the vines, the fire and the sparkles all read these.
    /// </summary>
    public static class BowlingLayout
    {
        // The dirt island in the middle (a rounded square).
        public const float DirtHalf = 2.5f, DirtCorner = 0.6f, DirtTop = 0.02f, DirtSkirt = 0.35f;

        // The lanes: floor from LaneStart to LaneEnd, rails at +-RailSide, the foul line at the dirt's edge.
        public const float LaneStart = 2.4f, LaneEnd = 22.6f, LaneWidth = 2f, RailSide = 0.95f, FoulLine = 2.5f;

        // The pin deck: head pin, rack spacing and row depth, deck centre (ring, light, hover), ring size.
        public const float HeadPin = 20.8f, PinSpacing = 0.52f, RowDepth = 0.45f, DeckAt = 21.3f, RingSize = 2f;

        // Fire columns beside the deck, and the ball sprouts' ring on the dirt.
        public const float FireAt = 21.3f, FireSide = 1.6f, SproutRadius = 1.1f;

        // Spot light heights (deck lights and the dirt light).
        public const float DeckLightHeight = 6.5f, DirtLightHeight = 7f;

        // The roll assist's aim (over the head pin), the lane counter and the rules sign.
        public const float AimHeight = 0.16f, CounterAt = 2.9f, RulesAt = 3.2f, RulesHeight = 2f;

        /// <summary>The 8 ball sprouts' yaw angles (degrees, X frame), in the order the seeds open them. Never on a lane.</summary>
        public static readonly float[] SproutYaws = { 22.5f, 112.5f, 202.5f, 292.5f, 67.5f, 247.5f, 157.5f, 337.5f };

        /// <summary>How many sprouts each seed opens (start seed, then the 3 phase seeds).</summary>
        public static readonly int[] SproutGroups = { 4, 2, 1, 1 };

        /// <summary>The 6-pin rack (1-2-3), slot 0..5, each as (side, along).</summary>
        public static readonly Vector2[] Rack =
            { new(0f, 20.8f), new(-0.26f, 21.25f), new(0.26f, 21.25f), new(-0.52f, 21.7f), new(0f, 21.7f), new(0.52f, 21.7f) };

        /// <summary>Lane k's flat direction out from the centre (X frame).</summary>
        public static Vector3 LaneDir(int k) => Quaternion.Euler(0f, -90f * k, 0f) * Vector3.forward;

        /// <summary>Lane k's right-hand side (X frame).</summary>
        public static Vector3 LaneRight(int k) => Quaternion.Euler(0f, 90f - 90f * k, 0f) * Vector3.forward;

        /// <summary>A point on lane k: along metres out, side metres to the right, y up (X frame).</summary>
        public static Vector3 LanePoint(int k, float along, float side, float y = 0f) =>
            LaneDir(k) * along + LaneRight(k) * side + Vector3.up * y;

        /// <summary>Ball sprout i's point on the dirt (X frame, y 0).</summary>
        public static Vector3 SproutPoint(int i) =>
            Quaternion.Euler(0f, SproutYaws[Mathf.Clamp(i, 0, SproutYaws.Length - 1)], 0f) * Vector3.forward * SproutRadius;

        /// <summary>How high a slime of this size hovers over a pin deck (metres over the lane floor).</summary>
        public static float HoverHeight(float size) => 2.6f + 0.125f * size;
    }
}
