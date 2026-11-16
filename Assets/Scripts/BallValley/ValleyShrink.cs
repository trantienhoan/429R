using System.Collections.Generic;
using UnityEngine;
using ValleyGame = Balls.BallValleyStage;   // 429R: the gate stage took BALLS' ValleyGame's place

namespace Balls
{
    /// <summary>
    /// How the valley shrinks: it's an island over a dark chasm, in a few phases (BorderPhases). On the round clock it
    /// shrinks smoothly from one phase to the next, and everything moves in together: the striped lawn and the floor (drawn
    /// in code so they can be any size), the border between the holes, and the six holes themselves (each a solid piece,
    /// always its full size). A new match grows it back the same way. Nothing breaks off and nothing warns: it only scales.
    /// Everything here is just looks, run on every computer: the host decides the size.
    /// </summary>
    public partial class Valley
    {
        // Palette (BALLS_Palette.png) spots the map model uses: the two lawn stripes and the dark rock of the pits.
        static readonly Vector2 StripeA = new Vector2(0.094f, 0.938f), StripeB = new Vector2(0.031f, 0.938f), PitRock = new Vector2(0.28f, 0.81f);
        const float BallsStripeWidth = 10f;   // the lawn stripes, as modelled
        static float StripeWidth = BallsStripeWidth;   // 429R: set by Build for this valley's size (the static helpers use it)
        const float ChasmDepth = 30f;

        Mesh fieldMesh;
        readonly Transform[] holes = new Transform[6];
        float pending = -1f;

        /// <summary>Half size the valley is shrinking to (while it shrinks), else the current one: bots, drop-ins and berries keep inside it.</summary>
        public float SafeHalfX => t.halfX * (pending > 0f ? Mathf.Min(pending, Scale) : Scale);
        public float SafeHalfZ => t.halfZ * (pending > 0f ? Mathf.Min(pending, Scale) : Scale);

        /// <summary>The valley is shrinking to <paramref name="to"/> (1 = full size); below 0: it isn't.</summary>
        public void SetPending(float to) => pending = to > 0f && to < Scale - 0.0005f ? to : -1f;

        /// <summary>The six hole models sit on their mouths (which move in and out with the valley's size).</summary>
        void MoveHoles()
        {
            for (int c = 0; c < 6; c++)
                if (holes[c]) holes[c].localPosition = caves[c].mouth;
        }

        // ---------- building ----------

        /// <summary>The modelled map: the field is replaced by the code-drawn one, the pockets become six solid holes.</summary>
        void SetupModelMap(Transform map)
        {
            var pockets = new List<MeshFilter>();
            // 429R: measured at the model's own size (BALLS' 200 x 100 m valley), scaled with the map below.
            Bounds ring = new Bounds(Vector3.zero, new Vector3(Tuning.BallsHalfX * 2f + 80f, 1f, Tuning.BallsHalfZ * 2f + 80f));
            foreach (var mf in map.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                if (n == "Field") mf.gameObject.SetActive(false);
                else if (n.StartsWith("Pocket")) pockets.Add(mf);
                else if (n == "Ground_Outside") ring = mf.GetComponent<Renderer>().bounds;
            }
            SplitPockets(pockets);
            // 429R: the map (mountains, ground ring) is modelled round BALLS' 200 x 100 m valley: it's shrunk to fit this one
            // (Tuning.ArtScale). The holes were taken out of it first, at the model's size, and sized for this valley's caves.
            var art = new GameObject("Map (scaled)").transform;
            art.SetParent(transform, false);
            art.localScale = t.ArtScale;
            map.SetParent(art, false);
            ring = new Bounds(Vector3.Scale(ring.center, t.ArtScale), Vector3.Scale(ring.size, t.ArtScale));
            BuildChasm(Mathf.Max(-ring.min.x, ring.max.x), Mathf.Max(-ring.min.z, ring.max.z));

            var field = new GameObject("Field (shrinking)");
            field.transform.SetParent(transform, false);
            fieldMesh = new Mesh { name = "Field" };
            madeMeshes.Add(fieldMesh);   // 429R
            fieldMesh.MarkDynamic();
            field.AddComponent<MeshFilter>().sharedMesh = fieldMesh;
            field.AddComponent<MeshRenderer>().sharedMaterial = Gfx.PaletteMaterial();
            RebuildField();
        }

        /// <summary>Splits the pocket meshes into six solid holes, each sitting on its cave mouth (moving a hole = moving it).</summary>
        void SplitPockets(List<MeshFilter> pockets)
        {
            // 429R: split at the model's own size (BALLS' full-size mouths: the map isn't scaled yet), then each hole is
            // sized to this valley's caves (Tuning.HoleScale / MiddleHoleScale), its opening and edges measured first.
            var design = new Tuning { halfX = Tuning.BallsHalfX, halfZ = Tuning.BallsHalfZ };
            var meshes = SplitHoleMeshes(pockets, transform, design);
            foreach (var mf in pockets) Destroy(mf.gameObject);
            for (int c = 0; c < 6; c++)
            {
                var go = new GameObject("Hole " + c);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = caves[c].mouth;
                holes[c] = go.transform;
                if (!meshes[c]) continue;
                madeMeshes.Add(meshes[c]);   // 429R
                float s = caves[c].corner ? t.HoleScale : t.MiddleHoleScale;   // 429R: the hole model's size here
                go.AddComponent<MeshFilter>().sharedMesh = meshes[c];
                go.AddComponent<MeshRenderer>().sharedMaterial = Gfx.PaletteMaterial();
                var opening = FindOpening(meshes[c]);   // the lawn and the floor get cut out there
                caves[c].openingOffset = opening.offset * s;   // 429R: * s
                caves[c].openingRadius = opening.radius * s;
                MeasureEdges(caves[c], meshes[c], s);   // where the walls and their rocks meet this hole (429R: s)
                ScaleMesh(meshes[c], s);   // 429R
                AddPocketBack(go.transform, caves[c]);
            }
        }

        /// <summary>
        /// The back of a hole's pit, like a snooker pocket's: an invisible wall round its opening on the far side from
        /// the lawn, from deep down to above the lawn. A fast ball that would sail across the pit and on over the hole's
        /// collar hits it and drops in. The lawn side stays open (that's where balls roll in). Moves with the hole.
        /// </summary>
        void AddPocketBack(Transform hole, Cave c)
        {
            if (c.openingRadius <= 0f) return;
            const int Pieces = 24;
            const float Thick = 0.6f, Top = 3f, Bottom = -6f;
            float r = c.openingRadius + Thick * 0.5f;
            float piece = 2f * Mathf.PI * r / Pieces * 1.08f;   // (a little overlap: no cracks between them)
            var back = new GameObject("Pocket Back").transform;
            back.SetParent(hole, false);
            back.localPosition = c.openingOffset;
            for (int i = 0; i < Pieces; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / Pieces;
                var along = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 side = Vector3.Cross(Vector3.up, along) * (piece * 0.5f);
                // No piece may reach onto the lawn (any of its corners), or it would block the way into the pocket.
                bool lawnSide = false;
                for (int k = 0; k < 4 && !lawnSide; k++)
                {
                    Vector3 q = c.openingOffset + along * (r + ((k & 1) == 0 ? -Thick : Thick) * 0.5f) + ((k & 2) == 0 ? side : -side);
                    lawnSide = c.corner ? q.x * c.side.x < 0f && q.z * c.side.z < 0f : q.z * c.side.z < 0f;
                }
                if (lawnSide) continue;
                var w = new GameObject("Back").transform;
                w.SetParent(back, false);
                w.localPosition = along * r + Vector3.up * ((Top + Bottom) * 0.5f);
                w.localRotation = Quaternion.LookRotation(along, Vector3.up);
                var box = w.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(piece, Top - Bottom, Thick);
                box.sharedMaterial = Gfx.WallPhys;
                BallPhysics.RegisterPocketBack(box);   // dead: a ball that hits it drops instead of bouncing back out
            }
        }

        const float RockTuck = 0.25f;   // the first border rock tucks this far into the hole's collar: no seam
        static readonly float[] BandSlices = { 0.6f, 1.2f, 1.8f, 2.4f, 3.0f, 3.5f };   // across the rocks' band, out from the edge

        /// <summary>
        /// Where the border meets <paramref name="c"/> along each of its edges. The solid wall runs right up to the pit (where
        /// its rim crosses the lawn edge), so a ball only drops where you see a pit. The rocks run up to where the model's
        /// collar ends: the collars are hand made and each ends somewhere else (4.6 to 7 m from the mouth), so a fixed gap
        /// left a hole between them here and an overlap there.
        /// </summary>
        static void MeasureEdges(Cave c, Mesh hole, float s = 1f)
        {
            if (c.openingRadius <= 0f) return;   // no pit found: the old gap
            var collar = CollarFootprint(hole);
            foreach (var along in c.EdgeDirections)
            {
                Vector3 outward = Mathf.Abs(along.x) > 0.5f ? new Vector3(0f, 0f, c.side.z) : new Vector3(c.side.x, 0f, 0f);
                // 429R: the mesh is still at the model's size here, the cave's opening already at this valley's (times s).
                float pit = PitEdge(c.openingOffset / s, c.openingRadius / s, along);
                float end = CollarEnd(collar, along, outward);
                c.SetEdge(along, pit * s, (end > pit + RockTuck ? end - RockTuck : pit) * s);
            }
        }

        /// <summary>429R: a hole mesh (vertices round its mouth) made <paramref name="s"/> times as big, every way.</summary>
        static void ScaleMesh(Mesh m, float s)
        {
            if (Mathf.Approximately(s, 1f)) return;
            var vs = m.vertices;
            for (int i = 0; i < vs.Length; i++) vs[i] *= s;
            m.vertices = vs;
            m.RecalculateBounds();
        }

        /// <summary>How far along the edge (from the mouth, in direction <paramref name="along"/>) the pit's rim crosses it.</summary>
        static float PitEdge(Vector3 offset, float radius, Vector3 along)
        {
            var o = new Vector2(offset.x, offset.z);
            var d = new Vector2(along.x, along.z);
            float od = Vector2.Dot(o, d), disc = od * od - o.sqrMagnitude + radius * radius;
            return disc > 0f ? od + Mathf.Sqrt(disc) : 0f;
        }

        /// <summary>The collar seen from above: the hole model's triangles that are wholly above the lawn, flattened (x, z).</summary>
        static List<Vector2> CollarFootprint(Mesh hole)
        {
            var tris = new List<Vector2>();
            var vs = hole.vertices;
            var ts = hole.triangles;
            for (int k = 0; k < ts.Length; k += 3)
            {
                Vector3 a = vs[ts[k]], b = vs[ts[k + 1]], d = vs[ts[k + 2]];
                if (a.y < 0.3f || b.y < 0.3f || d.y < 0.3f) continue;
                tris.Add(new Vector2(a.x, a.z));
                tris.Add(new Vector2(b.x, b.z));
                tris.Add(new Vector2(d.x, d.z));
            }
            return tris;
        }

        /// <summary>
        /// How far along the edge the collar reaches, everywhere across the rocks' band (the nearest of its far ends, so
        /// the first rock touches it all the way across). Below 0: no collar there.
        /// </summary>
        static float CollarEnd(List<Vector2> tris, Vector3 along, Vector3 outward)
        {
            var d = new Vector2(along.x, along.z);
            float nearest = float.MaxValue;
            foreach (float slice in BandSlices)
            {
                var o = new Vector2(outward.x, outward.z) * slice;
                float far = float.MinValue;
                for (int i = 0; i < tris.Count; i += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        Vector2 p = tris[i + e], q = tris[i + (e + 1) % 3], pq = q - p;
                        float den = d.x * pq.y - d.y * pq.x;
                        if (Mathf.Abs(den) < 1e-6f) continue;   // parallel
                        Vector2 po = p - o;
                        float t = (po.x * pq.y - po.y * pq.x) / den, s = (po.x * d.y - po.y * d.x) / den;
                        if (s >= 0f && s <= 1f && t > far) far = t;
                    }
                if (far > 0f) nearest = Mathf.Min(nearest, far);
            }
            return nearest < float.MaxValue ? nearest : -1f;
        }

        /// <summary>
        /// Splits the map model's pocket meshes (one of them holds two corners) into one mesh per hole, each triangle
        /// going to the hole it's nearest, with vertices relative to that hole's mouth at full size. Holes in the order
        /// top-left, top-middle, top-right, bottom-left, bottom-middle, bottom-right (null where there's nothing).
        /// <paramref name="space"/>: the valley's transform (null: the meshes' world space). Also used by the editor preview.
        /// </summary>
        public static Mesh[] SplitHoleMeshes(IList<MeshFilter> pockets, Transform space, Tuning t)
        {
            var mouths = new Vector3[6];
            var spots = HoleSpots(t, 1f);
            for (int c = 0; c < 6; c++) mouths[c] = spots[c].mouth;
            var verts = new List<Vector3>[6];
            var norms = new List<Vector3>[6];
            var uvs = new List<Vector2>[6];
            var tris = new List<int>[6];
            for (int c = 0; c < 6; c++)
            {
                verts[c] = new List<Vector3>();
                norms[c] = new List<Vector3>();
                uvs[c] = new List<Vector2>();
                tris[c] = new List<int>();
            }
            foreach (var mf in pockets)
            {
                var m = mf ? mf.sharedMesh : null;
                if (!m || !m.isReadable) continue;
                var toSpace = space ? space.worldToLocalMatrix * mf.transform.localToWorldMatrix : mf.transform.localToWorldMatrix;
                var vs = m.vertices;
                var ns = m.normals;
                var us = m.uv;
                var ts = m.triangles;
                var remap = new Dictionary<int, int>[6];
                for (int c = 0; c < 6; c++) remap[c] = new Dictionary<int, int>();
                for (int k = 0; k < ts.Length; k += 3)
                {
                    Vector3 centre = toSpace.MultiplyPoint3x4((vs[ts[k]] + vs[ts[k + 1]] + vs[ts[k + 2]]) / 3f);
                    int hole = 0;
                    for (int c = 1; c < 6; c++)
                        if (FlatDist(centre, mouths[c]) < FlatDist(centre, mouths[hole])) hole = c;
                    for (int j = 0; j < 3; j++)
                    {
                        int vi = ts[k + j];
                        if (!remap[hole].TryGetValue(vi, out int ni))
                        {
                            ni = verts[hole].Count;
                            remap[hole][vi] = ni;
                            verts[hole].Add(toSpace.MultiplyPoint3x4(vs[vi]) - mouths[hole]);
                            norms[hole].Add(ns.Length > vi ? toSpace.MultiplyVector(ns[vi]).normalized : Vector3.up);
                            uvs[hole].Add(us.Length > vi ? us[vi] : Vector2.zero);
                        }
                        tris[hole].Add(ni);
                    }
                }
            }
            var meshes = new Mesh[6];
            for (int c = 0; c < 6; c++)
            {
                DropPebbles(verts[c], norms[c], uvs[c], tris[c]);
                if (tris[c].Count == 0) continue;
                var mesh = new Mesh { name = "Hole " + c };
                if (verts[c].Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts[c]);
                mesh.SetNormals(norms[c]);
                mesh.SetUVs(0, uvs[c]);
                mesh.SetTriangles(tris[c], 0);
                mesh.RecalculateBounds();
                meshes[c] = mesh;
            }
            return meshes;
        }

        /// <summary>
        /// Takes the little rocks out of a hole model: the loose pebbles (about 1 m each) lying at its entrance. A piece is
        /// a group of triangles that share corners; a pebble is a small one sitting on the ground. The pit, the collar and
        /// the flat cracks on the lawn stay.
        /// </summary>
        static void DropPebbles(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
        {
            if (tris.Count == 0) return;
            // Corners at the same spot belong together (the model's faces don't share vertices: flat shading).
            var spot = new Dictionary<Vector3Int, int>();
            var root = new int[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * 500f), Mathf.RoundToInt(v.y * 500f), Mathf.RoundToInt(v.z * 500f));
                if (!spot.TryGetValue(key, out int s)) spot[key] = s = i;
                root[i] = s;
            }
            int Find(int x)
            {
                while (root[x] != x) x = root[x] = root[root[x]];
                return x;
            }
            for (int k = 0; k < tris.Count; k += 3)
            {
                int a = Find(tris[k]), b = Find(tris[k + 1]), d = Find(tris[k + 2]);
                root[b] = a;
                root[Find(d)] = Find(a);
            }
            var min = new Dictionary<int, Vector3>();
            var max = new Dictionary<int, Vector3>();
            foreach (int i in tris)
            {
                int r = Find(i);
                Vector3 v = verts[i];
                min[r] = min.TryGetValue(r, out var lo) ? Vector3.Min(lo, v) : v;
                max[r] = max.TryGetValue(r, out var hi) ? Vector3.Max(hi, v) : v;
            }
            bool Pebble(int r)
            {
                Vector3 size = max[r] - min[r];
                return size.x < 2f && size.z < 2f && size.y > 0.1f && size.y < 1.5f && min[r].y > -1f;
            }
            // Keep the rest, with only the vertices it uses.
            var keep = new List<int>(tris.Count);
            var remap = new Dictionary<int, int>();
            var nv = new List<Vector3>();
            var nn = new List<Vector3>();
            var nu = new List<Vector2>();
            for (int k = 0; k < tris.Count; k += 3)
            {
                if (Pebble(Find(tris[k]))) continue;
                for (int j = 0; j < 3; j++)
                {
                    int i = tris[k + j];
                    if (!remap.TryGetValue(i, out int ni))
                    {
                        ni = nv.Count;
                        remap[i] = ni;
                        nv.Add(verts[i]);
                        nn.Add(norms[i]);
                        nu.Add(uvs[i]);
                    }
                    keep.Add(ni);
                }
            }
            verts.Clear(); verts.AddRange(nv);
            norms.Clear(); norms.AddRange(nn);
            uvs.Clear(); uvs.AddRange(nu);
            tris.Clear(); tris.AddRange(keep);
        }

        /// <summary>The chasm round the island: a dark floor far below and cliffs under the ground ring's inner edge.</summary>
        void BuildChasm(float outerX, float outerZ)
        {
            var mb = new MeshBuild();
            float hx = t.halfX, hz = t.halfZ, d = -ChasmDepth;
            mb.Flat(-outerX, outerX, -outerZ, outerZ, d, PitRock);
            // Cliffs under the ground ring, facing the island.
            mb.Side(new Vector3(0f, 0f, hz), Vector3.back, hx, -0.02f, d, PitRock);
            mb.Side(new Vector3(0f, 0f, -hz), Vector3.forward, hx, -0.02f, d, PitRock);
            mb.Side(new Vector3(hx, 0f, 0f), Vector3.left, hz, -0.02f, d, PitRock);
            mb.Side(new Vector3(-hx, 0f, 0f), Vector3.right, hz, -0.02f, d, PitRock);
            var go = new GameObject("Chasm");
            go.transform.SetParent(transform, false);
            var chasm = mb.ToMesh("Chasm");
            madeMeshes.Add(chasm);   // 429R
            go.AddComponent<MeshFilter>().sharedMesh = chasm;
            go.AddComponent<MeshRenderer>().sharedMaterial = Gfx.PaletteMaterial();
        }

        readonly List<Wall> lipWalls = new List<Wall>();

        /// <summary>The striped lawn at its current size, with cliffs down its sides (it's an island now), reaching out under the border rocks.</summary>
        void RebuildField()
        {
            if (!fieldMesh) return;
            lipWalls.Clear();
            foreach (var w in liveWalls) lipWalls.Add(rimBlocks ? Inset(w, LipEndInset) : w);   // the border fills its walls
            BuildFieldMesh(fieldMesh, HalfX, HalfZ, lipWalls, t.lawnUnderBorder, Cuts());
        }

        readonly List<Cut> cuts = new List<Cut>();

        /// <summary>The holes' pit openings as they are now: each where its hole model is (on its mouth, which moves with the valley's size).</summary>
        List<Cut> Cuts()
        {
            cuts.Clear();
            for (int i = 0; i < caves.Count; i++)
            {
                var c = caves[i];
                if (c.openingRadius <= 0f) continue;
                Vector3 at = i < holes.Length && holes[i] ? holes[i].localPosition : c.mouth;
                cuts.Add(new Cut { x = at.x + c.openingOffset.x, z = at.z + c.openingOffset.z, radius = c.openingRadius });
            }
            return cuts;
        }

        /// <summary>A hole's pit opening: a circle on the ground (valley space). The lawn and the floor stop at its rim.</summary>
        public struct Cut
        {
            public float x, z, radius;
        }

        /// <summary>A pit opening found in a hole model: its centre from the hole's mouth, and its radius.</summary>
        public struct Opening
        {
            public Vector3 offset;
            public float radius;
        }

        /// <summary>
        /// Where a hole model's pit opens at lawn level (vertices relative to its mouth): the ring of vertices where the flat
        /// top meets the pit wall going down. Radius 0 if there's no such ring. Also used by the editor preview.
        /// </summary>
        public static Opening FindOpening(Mesh hole)
        {
            if (!hole) return default;
            var vs = hole.vertices;
            var ts = hole.triangles;
            var ring = new HashSet<int>();
            for (int k = 0; k < ts.Length; k += 3)
            {
                if (vs[ts[k]].y > -1f && vs[ts[k + 1]].y > -1f && vs[ts[k + 2]].y > -1f) continue;   // not part of the pit wall
                for (int j = 0; j < 3; j++)
                    if (Mathf.Abs(vs[ts[k + j]].y) < 0.35f) ring.Add(ts[k + j]);
            }
            if (ring.Count < 8) return default;
            var centre = Vector3.zero;
            foreach (int i in ring) centre += new Vector3(vs[i].x, 0f, vs[i].z);
            centre /= ring.Count;
            float radius = 0f;
            foreach (int i in ring) radius += Vector2.Distance(new Vector2(vs[i].x, vs[i].z), new Vector2(centre.x, centre.z));
            return new Opening { offset = centre, radius = radius / ring.Count };
        }

        const float LipEndInset = 0.6f, LipBridge = 2f;

        /// <summary>
        /// The stretches of border the lawn reaches out under: along each of <paramref name="walls"/>, wherever there are
        /// rocks (<paramref name="rocks"/>: their world bounds). Joints and small gaps between rocks are bridged; at a rock's
        /// end it stops a little short (under the bevel), so it never pokes out beside a rock or in front of a hole.
        /// Also used by the editor preview.
        /// </summary>
        public static void LipUnderRocks(IList<Wall> walls, IList<Bounds> rocks, List<Wall> into)
        {
            var spans = new List<Vector2>();
            foreach (var w in walls)
            {
                Vector3 d = w.b - w.a;
                float len = d.x + d.z;   // a -> b runs along +x or +z
                if (len < 0.3f) continue;
                Vector3 along = d / len;
                float a0 = Vector3.Dot(w.a, along), a1 = a0 + len, edge = Vector3.Dot(w.a, w.outward);
                spans.Clear();
                foreach (var b in rocks)
                {
                    float out0 = Vector3.Dot(b.center, w.outward) - edge;   // how far past this edge the rock's middle is
                    if (out0 < -1f || out0 > RimThickness + 1.5f) continue;    // not on this wall's band
                    float lo = Vector3.Dot(b.min, along), hi = Vector3.Dot(b.max, along);
                    float inset = Mathf.Min(LipEndInset, (hi - lo) * 0.2f);
                    lo = Mathf.Max(a0, lo + inset);
                    hi = Mathf.Min(a1, hi - inset);
                    if (hi - lo > 0.05f) spans.Add(new Vector2(lo, hi));
                }
                if (spans.Count == 0) continue;
                spans.Sort((p, q) => p.x.CompareTo(q.x));
                Vector2 cur = spans[0];
                for (int i = 1; i <= spans.Count; i++)
                {
                    if (i < spans.Count && spans[i].x <= cur.y + LipBridge) { cur.y = Mathf.Max(cur.y, spans[i].y); continue; }
                    into.Add(new Wall { a = w.a + along * (cur.x - a0), b = w.a + along * (cur.y - a0), outward = w.outward });
                    if (i < spans.Count) cur = spans[i];
                }
            }
        }

        static Wall Inset(Wall w, float by)
        {
            Vector3 d = w.b - w.a;
            float len = d.x + d.z;
            if (len <= by * 2f + 0.1f) return w;
            Vector3 along = d / len;
            return new Wall { a = w.a + along * by, b = w.b - along * by, outward = w.outward };
        }

        /// <summary>
        /// The striped lawn <paramref name="hx"/> x <paramref name="hz"/> (half sizes) with cliffs down its sides, into
        /// <paramref name="m"/>. Along each of <paramref name="walls"/> (the border between the holes) it reaches
        /// <paramref name="lip"/> m further out, under the border rocks, so no chasm shows between them and the lawn or
        /// through their joints; in front of the holes it stops at the edge. Where a hole's pit reaches in over the edge
        /// (<paramref name="cuts"/>) the lawn is cut away along its round rim, so the whole pit shows. Also used by the
        /// editor preview.
        /// </summary>
        public static void BuildFieldMesh(Mesh m, float hx, float hz, IList<Wall> walls = null, float lip = 0f, IList<Cut> cuts = null)
        {
            var mb = new MeshBuild();
            CutLawn(mb, hx, hz, cuts, true);
            Cliffs(mb, hx, hz, cuts);
            if (walls != null && lip > 0.01f)
                foreach (var w in walls)
                {
                    Vector3 d = w.b - w.a;
                    float len = d.x + d.z;   // a -> b runs along +x or +z
                    if (len < 0.3f) continue;
                    Vector3 far = w.outward * lip, along = d / len, mid = (w.a + w.b) * 0.5f;
                    LawnPiece(mb, Mathf.Min(w.a.x, w.a.x + far.x), Mathf.Max(w.b.x, w.b.x + far.x),
                        Mathf.Min(w.a.z, w.a.z + far.z), Mathf.Max(w.b.z, w.b.z + far.z));
                    mb.Side(mid + far, w.outward, len * 0.5f, 0f, -ChasmDepth, PitRock);       // its outer edge, under the rocks
                    mb.Side(w.a + far * 0.5f, -along, lip * 0.5f, 0f, -ChasmDepth, PitRock);   // its ends, beside the holes
                    mb.Side(w.b + far * 0.5f, along, lip * 0.5f, 0f, -ChasmDepth, PitRock);
                }
            mb.Into(m);
        }

        /// <summary>The floor collider's shape: the lawn's top with the pits cut out (no stripes, no cliffs).</summary>
        static void FloorMesh(Mesh m, float hx, float hz, IList<Cut> cuts)
        {
            var mb = new MeshBuild();
            CutLawn(mb, hx, hz, cuts, false);
            mb.Into(m);
        }

        static readonly List<float> slices = new List<float>();
        static readonly List<Vector2> spans = new List<Vector2>();

        /// <summary>
        /// The lawn -hx..hx by -hz..hz with the pits cut out of its edges: in slices across x (one per stripe, plus thin ones
        /// along each pit's curve), each running from the bottom edge to the top edge at that x.
        /// </summary>
        static void CutLawn(MeshBuild mb, float hx, float hz, IList<Cut> cuts, bool striped)
        {
            slices.Clear();
            slices.Add(-hx);
            slices.Add(hx);
            if (striped)
                for (float x = (Mathf.Floor(-hx / StripeWidth) + 1f) * StripeWidth; x < hx - 0.001f; x += StripeWidth) slices.Add(x);
            if (cuts != null)
                foreach (var c in cuts) CurveSlices(c, hx, hz, slices);
            slices.Sort();
            for (int i = 0; i + 1 < slices.Count; i++)
            {
                float xa = slices[i], xb = slices[i + 1];
                if (xb - xa < 0.001f) continue;
                float ta = EdgeAt(xa, hz, cuts, true), tb = EdgeAt(xb, hz, cuts, true);
                float ba = EdgeAt(xa, hz, cuts, false), bb = EdgeAt(xb, hz, cuts, false);
                if (ta <= ba && tb <= bb) continue;   // pits from both sides meet here: no lawn left
                int stripe = Mathf.FloorToInt((xa + xb) * 0.5f / StripeWidth);
                mb.FlatQuad(new Vector3(xa, 0f, ba), new Vector3(xa, 0f, Mathf.Max(ta, ba)), new Vector3(xb, 0f, Mathf.Max(tb, bb)), new Vector3(xb, 0f, bb),
                            striped && (stripe & 1) != 0 ? StripeB : StripeA);
            }
        }

        /// <summary>Where the lawn ends at <paramref name="x"/> on its top side (or bottom side): the edge, or nearer in where a pit cuts in.</summary>
        static float EdgeAt(float x, float hz, IList<Cut> cuts, bool top)
        {
            float e = top ? hz : -hz;
            if (cuts == null) return e;
            foreach (var c in cuts)
            {
                if (top ? c.z < hz - 0.01f : c.z > -hz + 0.01f) continue;   // a pit on the other side
                float dx = x - c.x, h2 = c.radius * c.radius - dx * dx;
                if (h2 <= 0f) continue;
                float h = Mathf.Sqrt(h2);
                e = top ? Mathf.Min(e, c.z - h) : Mathf.Max(e, c.z + h);
            }
            return e;
        }

        /// <summary>How far along x a pit cuts into the lawn's top or bottom edge (its centre sits on or outside that edge): false if it doesn't reach.</summary>
        static bool CutSpan(Cut c, float hz, out float x0, out float x1)
        {
            float o = Mathf.Abs(c.z) - hz;   // how far outside the edge the pit's centre is
            x0 = x1 = c.x;
            if (o < -0.01f || c.radius <= o) return false;
            float w = Mathf.Sqrt(c.radius * c.radius - Mathf.Max(0f, o) * Mathf.Max(0f, o));
            x0 = c.x - w;
            x1 = c.x + w;
            return true;
        }

        /// <summary>Slice positions along a pit's curve where it cuts into the lawn: even steps round the circle, so closer together where the curve is steep.</summary>
        static void CurveSlices(Cut c, float hx, float hz, List<float> xs)
        {
            if (!CutSpan(c, hz, out float x0, out float x1)) return;
            float reach = Mathf.Asin(Mathf.Clamp01((x1 - x0) * 0.5f / c.radius));
            const int Steps = 24;
            for (int k = 0; k <= Steps; k++)
            {
                float x = c.x + c.radius * Mathf.Sin(Mathf.Lerp(-reach, reach, k / (float)Steps));
                if (x > -hx && x < hx) xs.Add(x);
            }
        }

        /// <summary>The island's cliffs down its four sides, leaving out the stretches the pits cut away (their own walls go down there).</summary>
        static void Cliffs(MeshBuild mb, float hx, float hz, IList<Cut> cuts)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                // Top / bottom side: in pieces between the pits.
                float z = s * hz, from = -hx;
                var normal = s > 0 ? Vector3.forward : Vector3.back;
                spans.Clear();
                if (cuts != null)
                    foreach (var c in cuts)
                        if ((s > 0 ? c.z >= hz - 0.01f : c.z <= -hz + 0.01f) && CutSpan(c, hz, out float x0, out float x1)) spans.Add(new Vector2(x0, x1));
                spans.Sort((p, q) => p.x.CompareTo(q.x));
                foreach (var cut in spans)
                {
                    if (cut.x > from) CliffPiece(mb, from, Mathf.Min(cut.x, hx), z, normal);
                    from = Mathf.Max(from, cut.y);
                }
                if (from < hx) CliffPiece(mb, from, hx, z, normal);

                // Left / right side: between the lawn's top and bottom edge there (the corner pits take its ends).
                float x = s * hx, zt = EdgeAt(x, hz, cuts, true), zb = EdgeAt(x, hz, cuts, false);
                if (zt - zb > 0.01f)
                    mb.Side(new Vector3(x, 0f, (zt + zb) * 0.5f), s > 0 ? Vector3.right : Vector3.left, (zt - zb) * 0.5f, 0f, -ChasmDepth, PitRock);
            }
        }

        static void CliffPiece(MeshBuild mb, float x0, float x1, float z, Vector3 normal)
        {
            if (x1 - x0 < 0.01f) return;
            mb.Side(new Vector3((x0 + x1) * 0.5f, 0f, z), normal, (x1 - x0) * 0.5f, 0f, -ChasmDepth, PitRock);
        }

        /// <summary>A flat piece of lawn, in the modelled stripes (they run across x, so pieces line up with each other).</summary>
        static void LawnPiece(MeshBuild mb, float x0, float x1, float z0, float z1)
        {
            while (x0 < x1 - 0.001f)
            {
                float xe = Mathf.Min(x1, (Mathf.Floor(x0 / StripeWidth + 0.0001f) + 1f) * StripeWidth);
                int stripe = Mathf.FloorToInt((x0 + xe) * 0.5f / StripeWidth);
                mb.Flat(x0, xe, z0, z1, 0f, (stripe & 1) == 0 ? StripeA : StripeB);
                x0 = xe;
            }
        }

        // 429R: BALLS' HideHandLayouts (for its BorderLayouts editor sketch pad) isn't brought over.

        // ---------- shared with the editor (BorderLayouts) ----------

        /// <summary>Thickness of the border band (a Rim_Block), outward from the field edge.</summary>
        public const float RimThickness = RimBlockThick;

        /// <summary>
        /// The valley's phases (sizes), in order: 1 (full size) first, Min Map Scale last, Phases of them evenly apart. On
        /// the round clock it shrinks smoothly from one to the next; a new match grows it back from the last to the first.
        /// </summary>
        public static List<float> BorderPhases(Tuning t)
        {
            float least = Mathf.Clamp(t.minMapScale, 0.05f, 1f);
            int n = Mathf.Max(2, t.phases);
            var list = new List<float>(n);
            for (int i = 0; i < n; i++) list.Add(Mathf.Lerp(1f, least, i / (float)(n - 1)));
            return list;
        }

        public struct Wall
        {
            public Vector3 a, b, outward;
        }

        /// <summary>The six border walls at size <paramref name="s"/> with the holes at their normal width: a -> b along +x or +z.</summary>
        public static Wall[] BorderWalls(Tuning t, float s)
        {
            float hx = t.halfX * s, hz = t.halfZ * s, cg = t.caveCornerWidth * 0.72f, mg = t.caveMiddleWidth * 0.5f;
            return new[]
            {
                new Wall { a = new Vector3(-hx + cg, 0f, hz), b = new Vector3(-mg, 0f, hz), outward = Vector3.forward },
                new Wall { a = new Vector3(mg, 0f, hz), b = new Vector3(hx - cg, 0f, hz), outward = Vector3.forward },
                new Wall { a = new Vector3(-hx + cg, 0f, -hz), b = new Vector3(-mg, 0f, -hz), outward = Vector3.back },
                new Wall { a = new Vector3(mg, 0f, -hz), b = new Vector3(hx - cg, 0f, -hz), outward = Vector3.back },
                new Wall { a = new Vector3(-hx, 0f, -hz + cg), b = new Vector3(-hx, 0f, hz - cg), outward = Vector3.left },
                new Wall { a = new Vector3(hx, 0f, -hz + cg), b = new Vector3(hx, 0f, hz - cg), outward = Vector3.right },
            };
        }

        public struct HoleSpot
        {
            public Vector3 mouth;
            public float baseWidth, maxGap;
            public Vector3[] along;   // the edges its gap runs along
        }

        /// <summary>The six holes at size <paramref name="s"/>: where, how wide normally, and how far their gap can grow along the edge.</summary>
        public static HoleSpot[] HoleSpots(Tuning t, float s)
        {
            float hx = t.halfX * s, hz = t.halfZ * s;
            var spots = new HoleSpot[6];
            int k = 0;
            for (int zs = 1; zs >= -1; zs -= 2)
                for (int xs = -1; xs <= 1; xs++)
                {
                    bool corner = xs != 0;
                    spots[k++] = new HoleSpot
                    {
                        mouth = new Vector3(xs * hx, 0f, zs * hz),
                        baseWidth = corner ? t.caveCornerWidth : t.caveMiddleWidth,
                        maxGap = corner ? t.caveMaxWidth * 0.72f : t.caveMaxWidth * 0.5f,
                        along = corner ? new[] { new Vector3(-xs, 0f, 0f), new Vector3(0f, 0f, -zs) } : new[] { Vector3.right, Vector3.left },
                    };
                }
            return spots;
        }

        /// <summary>
        /// The automatic border for size <paramref name="s"/> (what the game lays when a phase has no hand-placed rocks):
        /// each wall filled with Rim_Blocks, as many 10 m blocks as round to its length, stretched to fit. Calls
        /// <paramref name="place"/>(position, rotation, length scale) per block. The editor uses it for starter rocks.
        /// </summary>
        public static void AutoBorder(Tuning t, float s, System.Action<Vector3, Quaternion, float> place)
        {
            foreach (var w in BorderWalls(t, s))
            {
                float len = Vector3.Distance(w.a, w.b);
                Vector3 d = w.b - w.a;
                if (d.x + d.z < 0.3f || len < 1f) continue;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / RimBlockStep));
                Vector3 dir = d / len;
                float seg = len / n;
                Quaternion rot = Quaternion.Euler(0f, -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg, 0f);
                for (int k = 0; k < n; k++)
                {
                    Vector3 c = w.a + dir * (seg * (k + 0.5f)) + w.outward * (RimBlockThick * 0.5f);
                    place(new Vector3(c.x, 0f, c.z), rot, Mathf.Max(0.05f, seg - RimBlockGap) / RimBlockLength);
                }
            }
        }

        /// <summary>Little helper for flat-coloured palette meshes (quads with one palette spot each).</summary>
        class MeshBuild
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> tri = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 spot)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int k = 0; k < 4; k++) { n.Add(normal); uv.Add(spot); }
                tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
                tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
            }

            /// <summary>A flat rectangle facing up.</summary>
            public void Flat(float x0, float x1, float z0, float z1, float y, Vector2 spot) =>
                Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up, spot);

            /// <summary>A flat four-sided piece facing up, its corners in the same order as <see cref="Flat"/>'s.</summary>
            public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 spot) => Quad(a, b, c, d, Vector3.up, spot);

            /// <summary>A wall facing <paramref name="normal"/>, centred on <paramref name="centre"/>, from <paramref name="top"/> down to <paramref name="bottom"/>.</summary>
            public void Side(Vector3 centre, Vector3 normal, float halfLength, float top, float bottom, Vector2 spot)
            {
                Vector3 right = Vector3.Cross(Vector3.up, -normal).normalized;   // the viewer's right, looking at it
                Vector3 l = centre - right * halfLength, r = centre + right * halfLength;
                Quad(new Vector3(l.x, top, l.z), new Vector3(r.x, top, r.z), new Vector3(r.x, bottom, r.z), new Vector3(l.x, bottom, l.z), normal, spot);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                Into(m);
                return m;
            }

            public void Into(Mesh m)
            {
                m.Clear();
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.SetTriangles(tri, 0);
                m.RecalculateBounds();
            }
        }
    }

    /// <summary>Something falling out of sight (a ball down a hole, a chunk of rock), spinning, then gone.</summary>
    public class Falling : MonoBehaviour
    {
        Vector3 vel, spin;
        float dieAt;

        /// <summary>How long something that went over a hole keeps falling (out of sight) before it's gone.</summary>
        public const float HoleSeconds = 1.5f;

        public static void Drop(Transform t, Vector3 velocity, float seconds, Vector3 spinDegrees = default)
        {
            var f = t.gameObject.AddComponent<Falling>();
            f.vel = velocity;
            f.spin = spinDegrees == default ? Random.insideUnitSphere * 180f : spinDegrees;
            f.dieAt = Time.time + seconds;
            Gfx.NoShadows(t);   // 429R: always (BALLS: phones only)
        }

        /// <summary>
        /// A look-alike of <paramref name="t"/> (its meshes and the ones under it, nothing solid) falls instead, so the
        /// real thing can go at once (a pocketed main ball).
        /// </summary>
        public static void DropCopy(Transform t, Vector3 velocity, float seconds, Vector3 spinDegrees = default)
        {
            var copy = new GameObject(t.name + " (falling)").transform;
            if (ValleyGame.I && ValleyGame.I.world) copy.SetParent(ValleyGame.I.world, false);   // 429R: under the stage (BALLS: the scene root)
            copy.SetPositionAndRotation(t.position, t.rotation);
            copy.localScale = t.lossyScale;
            CopyLooks(t, copy);
            Drop(copy, velocity, seconds, spinDegrees);
        }

        static void CopyLooks(Transform from, Transform to)
        {
            if (from.TryGetComponent<MeshFilter>(out var mf) && from.TryGetComponent<MeshRenderer>(out var mr) && mr.enabled)
            {
                to.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                to.gameObject.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
            }
            foreach (Transform c in from)
            {
                if (!c.gameObject.activeSelf) continue;
                var k = new GameObject(c.name).transform;
                k.SetParent(to, false);
                k.localPosition = c.localPosition;
                k.localRotation = c.localRotation;
                k.localScale = c.localScale;
                CopyLooks(c, k);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            vel += Physics.gravity * (1.6f * dt);
            transform.position += vel * dt;
            transform.Rotate(spin * dt, Space.World);
            if (Time.time > dieAt) Destroy(gameObject);
        }
    }
}
