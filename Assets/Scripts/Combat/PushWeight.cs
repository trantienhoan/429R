using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// How much a loose thing gives way to a push that isn't plain physics, by its weight (its Rigidbody's Mass). Those
    /// pushes set the thing's speed directly, so without this a stone block would fly off like a book.
    /// A punch (Fist): things up to Light are pushed in full; heavier ones give way less and less (ten times the weight
    /// takes a third off: 10,000 two thirds of the push, 100,000 one third); from Immovable on, not at all.
    /// A boss ploughing through (Bulldozer) and a blast (Blast) still shove light and heavy things alike, as they were
    /// tuned to, except the ones at Immovable or more, which stay put: the Stone_Cube_Dice path blocks (4,200,000).
    /// To make something harder to push, raise its Mass.
    /// </summary>
    public static class PushWeight
    {
        /// <summary>Things up to this Mass take a punch's push in full.</summary>
        public const float Light = 1000f;

        /// <summary>Things of this Mass or more aren't pushed at all.</summary>
        public const float Immovable = 1000000f;

        /// <summary>How much of a punch's push the thing takes: 1 = all of it, 0 = none.</summary>
        public static float Share(Rigidbody body)
        {
            if (body == null || body.mass >= Immovable) return 0f;
            if (body.mass <= Light) return 1f;
            return 1f - Mathf.Log(body.mass / Light) / Mathf.Log(Immovable / Light);
        }

        /// <summary>Whether the thing is too heavy to be pushed at all.</summary>
        public static bool IsImmovable(Rigidbody body)
        {
            return body != null && body.mass >= Immovable;
        }
    }
}
