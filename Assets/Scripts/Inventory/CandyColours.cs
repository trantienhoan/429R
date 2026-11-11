using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// One colour per candy kind (user, 2026-11-10: six candies, one per stat, and six slime colours to match: Candy_1
    /// Pumpkin Candy orange, Candy_2 blue, Candy_4 yellow, Candy_5 Marshmallow pink, Candy_7 green, Candy_8 red, until
    /// the user repaints the candies). The one table every candy
    /// colour the slimes match: Tools > 429 Game > Balance 2026-11-10 > Candy Colours gives the coloured slimes their
    /// colour's candy through For Colour, and gives each row its slime skin (the cauldron's wrong-recipe slime wears the
    /// skin of a candy the recipe still needs). It never touches the candies themselves: the user paints them (set a
    /// row's colour to its candy's real colour afterwards).
    /// The asset: Assets/Data/Inventory/Candy_Colours.asset (the menu makes it). Change a colour, run the menu again.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Inventory/Candy Colours", fileName = "Candy_Colours")]
    public class CandyColours : ScriptableObject
    {
        [Serializable]
        public struct Row
        {
            [Tooltip("The candy (Candy_1..8 in Prefabs/Items/Breakable).")]
            public GameObject candy;
            [Tooltip("Its colour's name (for the logs).")]
            public string colourName;
            [Tooltip("Its colour: what the slimes that drop it match (set it to the candy's real colour).")]
            public Color colour;
            [Tooltip("A slime of this colour wears this (the Candy Colours menu fills it: the PlayGround slime of that " +
                     "colour's own skin, or a new Slime_<Colour>_Mat).")]
            public Material slimeSkin;
        }

        [Tooltip("One row per candy kind.")]
        [SerializeField] private Row[] rows = Array.Empty<Row>();
        [Tooltip("A colour paler than this (saturation, 0 to 1) counts as white: it has no candy of its own, so For " +
                 "Colour gives none and the slime drops random ones (the White Slime, the rainbow).")]
        [Range(0f, 1f)]
        [SerializeField] private float whiteBelow = 0.25f;

        // Darker than this (HSV value) has no hue worth matching either.
        private const float BlackBelow = 0.1f;

        public IReadOnlyList<Row> Rows => rows;

        /// <summary>
        /// The candy whose colour is nearest in hue (round the colour wheel) to this one; null for a white, grey or black
        /// colour (= random candy).
        /// </summary>
        public GameObject ForColour(Color colour)
        {
            Color.RGBToHSV(colour, out float hue, out float saturation, out float value);
            if (saturation < whiteBelow || value < BlackBelow) return null;
            GameObject best = null;
            float bestGap = float.MaxValue;
            foreach (var row in rows)
            {
                if (row.candy == null) continue;
                Color.RGBToHSV(row.colour, out float rowHue, out float rowSaturation, out _);
                if (rowSaturation < whiteBelow) continue;
                float gap = Mathf.Abs(Mathf.DeltaAngle(hue * 360f, rowHue * 360f));
                if (gap >= bestGap) continue;
                bestGap = gap;
                best = row.candy;
            }
            return best;
        }

        /// <summary>The row of this candy (by prefab, or by name for a copy: "Candy_4(Clone)"); false if it has none.</summary>
        public bool TryGetRow(GameObject candy, out Row found)
        {
            found = default;
            if (candy == null) return false;
            foreach (var row in rows)
            {
                if (row.candy == null) continue;
                if (row.candy == candy || candy.name.StartsWith(row.candy.name + "(") || candy.name == row.candy.name)
                {
                    found = row;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Any one of the candies, at random (null if the table is empty).</summary>
        public GameObject RandomCandy()
        {
            int count = 0;
            foreach (var row in rows) if (row.candy != null) count++;
            if (count == 0) return null;
            int pick = UnityEngine.Random.Range(0, count);
            foreach (var row in rows)
            {
                if (row.candy == null) continue;
                if (pick-- == 0) return row.candy;
            }
            return null;
        }
    }
}
