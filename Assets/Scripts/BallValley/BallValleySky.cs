using System.Collections.Generic;
using UnityEngine;

namespace Balls
{
    /// <summary>
    /// 429R (Ball Valley): the sky sphere's shape. The picture on it (Assets/BallValley/Sky/BallValley_Sky.png, made once by
    /// Tools > 429 Game > Ball Valley > Set Up Ball Valley) is equirectangular: u goes round (0 = straight ahead, +z, a
    /// quarter = +x), v goes up (0 = straight down, 1 = straight up). The stage puts the sphere round the head every frame.
    /// </summary>
    public static class BallValleySky
    {
        /// <summary>
        /// The sky's render queue: still in the opaque pass, but after the valley's opaque things, so most of the sky is
        /// hidden by them before it's drawn (less to draw on Quest).
        /// </summary>
        public const int Queue = 2450;

        /// <summary>
        /// A sphere of radius 1 seen from inside (its faces point in), <paramref name="around"/> steps round and
        /// <paramref name="up"/> steps from bottom to top, with the equirectangular UVs above.
        /// </summary>
        public static Mesh SphereMesh(int around = 48, int up = 24)
        {
            around = Mathf.Max(8, around);
            up = Mathf.Max(4, up);
            var verts = new List<Vector3>((around + 1) * (up + 1));
            var uvs = new List<Vector2>(verts.Capacity);
            for (int j = 0; j <= up; j++)
            {
                float v = j / (float)up;
                float lat = (v - 0.5f) * Mathf.PI;
                for (int i = 0; i <= around; i++)
                {
                    float u = i / (float)around;
                    verts.Add(Direction(u, lat));
                    uvs.Add(new Vector2(u, v));
                }
            }
            var tris = new List<int>(around * up * 6);
            int row = around + 1;
            for (int j = 0; j < up; j++)
                for (int i = 0; i < around; i++)
                {
                    int bottomLeft = j * row + i, bottomRight = bottomLeft + 1, topLeft = bottomLeft + row, topRight = topLeft + 1;
                    // From the middle, looking out: top-left, top-right, bottom-right runs clockwise (Unity's front face).
                    tris.Add(topLeft); tris.Add(topRight); tris.Add(bottomRight);
                    tris.Add(topLeft); tris.Add(bottomRight); tris.Add(bottomLeft);
                }
            var m = new Mesh { name = "Ball Valley Sky" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The direction a point of the picture shows: <paramref name="u"/> 0..1 round, <paramref name="lat"/> -90..90 degrees (in radians).</summary>
        public static Vector3 Direction(float u, float lat)
        {
            float lon = u * Mathf.PI * 2f, c = Mathf.Cos(lat);
            return new Vector3(c * Mathf.Sin(lon), Mathf.Sin(lat), c * Mathf.Cos(lon));
        }
    }
}
