using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Balls
{
    /// <summary>One of the six snooker-style pockets: a gap in the rocky rim with a bottomless pit behind it.</summary>
    public class Cave
    {
        public Vector3 mouth;      // point on the valley edge (corner or middle of a long side); moves in as the valley shrinks
        public Vector3 side;       // which corner / edge: (+-1 or 0, 0, +-1); mouth = side * (halfX, 0, halfZ)
        public Vector3 outward;    // direction out of the valley
        public Vector3 pitCenter;
        public bool corner;
        public float baseWidth, width, pitRadius;   // width: the gap in the border (always its base width now)
        /// <summary>
        /// The hole model's pit opening at lawn level: its centre from the mouth and its radius (0: no model hole). The lawn
        /// and the floor are cut away there, so the whole pit is open and a ball tips in once its centre is over the rim.
        /// </summary>
        public Vector3 openingOffset;
        public float openingRadius;

        /// <summary>Corner: open length along each of the two edges. Middle: half of the gap.</summary>
        public float EdgeGap => corner ? width * 0.72f : width * 0.5f;
        /// <summary>How close to the mouth counts as dangerous: bots and slimes keep this far away (the holes don't pull).</summary>
        public float DangerRadius => width * 0.9f + (BallValleyStage.I ? BallValleyStage.I.HoleM(3f) : 3f);   // 429R: the 3 m scaled with the holes (play test 2026-11-16: bots barely shot)

        // Along each edge from the mouth (+x, -x, +z, -z): where the solid wall starts (right at the pit's rim) and where
        // its rocks start (where the hole model's collar ends, so no gap shows between them). Measured from the hole
        // model once (Valley.MeasureEdges); without one, both are the gap's old half width.
        readonly float[] wallFrom = new float[4], rocksFrom = new float[4];
        bool measured;

        static int Key(Vector3 along) => along.x > 0.5f ? 0 : along.x < -0.5f ? 1 : along.z > 0.5f ? 2 : 3;

        /// <summary>How far from the mouth, along the edge in direction <paramref name="along"/>, the solid wall starts.</summary>
        public float WallFrom(Vector3 along) => measured ? wallFrom[Key(along)] : EdgeGap;

        /// <summary>How far from the mouth, along the edge in direction <paramref name="along"/>, the border rocks start.</summary>
        public float RocksFrom(Vector3 along) => measured ? rocksFrom[Key(along)] : EdgeGap;

        public void SetEdge(Vector3 along, float wall, float rocks)
        {
            wallFrom[Key(along)] = wall;
            rocksFrom[Key(along)] = rocks;
            measured = true;
        }

        /// <summary>The edge directions leading away from this hole to its two walls.</summary>
        public Vector3[] EdgeDirections => corner
            ? new[] { new Vector3(-side.x, 0f, 0f), new Vector3(0f, 0f, -side.z) }
            : new[] { Vector3.right, Vector3.left };
    }

    /// <summary>Builds the valley: flat floor (snooker table), rocky rim walls with 6 cave gaps, and the surrounding mountains.</summary>
    public partial class Valley : MonoBehaviour
    {
        public readonly List<Cave> caves = new List<Cave>();

        /// <summary>How big the valley is right now (1 = full size). The border, the floor and the hole positions follow it; the holes keep their width.</summary>
        public float Scale { get; private set; } = 1f;
        /// <summary>Half the width / depth of the playing field right now.</summary>
        public float HalfX => t.halfX * Scale;
        public float HalfZ => t.halfZ * Scale;

        Tuning t;
        MeshCollider floorCol;
        Mesh floorMesh;
        readonly Transform[] wallCol = new Transform[6];
        readonly Transform[] wallVis = new Transform[6];
        /// <summary>The border between the holes as it is now (the lawn reaches out under it).</summary>
        readonly Wall[] liveWalls = new Wall[6];
        // Rim made of rock models from BALLS_Map.blend, re-laid along each wall whenever the valley's size changes:
        // Rock_Border if it's there (one rock, stood four different ways so neighbours never match), else Rim_Block.
        bool rimBlocks;
        readonly List<Transform>[] blocks = new List<Transform>[6];
        string rimModel;
        Vector3 rimSize, rimCenter;   // the rock as modelled (measured at start): its length runs along X, its depth along Z
        bool rimTurns;                // Rock_Border: each rock stands differently from the one before it (see RockTurn)

        const float WallT = 4f, WallVisH = 2.2f, WallColH = 40f;
        const string MapModel = "Map/Map_Static", RimBlockModel = "Map/Rim_Block", RockBorderModel = "Map/Rock_Border";
        const float RimBlockLength = 9.7f, RimBlockThick = 3.6f, RimBlockGap = 0.3f, RimBlockStep = 10f;
        /// <summary>
        /// Rock_Border spacing as a share of its length: in BALLS_Map.blend the second rock sits 7.70 m from the first and
        /// a rock is 8.07 m long, so neighbours overlap a little and no gap shows. Every rock is stretched along its length
        /// by the same small amount to fill its wall exactly.
        /// </summary>
        const float RockBorderSpacing = 0.954f;

        // 429R: meshes made in code go with the valley (the stage may come and go many times in a session).
        readonly List<Mesh> madeMeshes = new List<Mesh>();

        void OnDestroy()
        {
            foreach (var m in madeMeshes) if (m) Destroy(m);
            madeMeshes.Clear();
        }

        public void Build(Tuning tune)
        {
            t = tune;
            StripeWidth = BallsStripeWidth * t.LayoutScale;   // 429R: the lawn's stripes shrink with the valley (BALLS: 10 m)
            float hx = t.halfX, hz = t.halfZ;
            AddCave(new Vector3(-hx, 0f, hz), true);   // 0 top-left
            AddCave(new Vector3(0f, 0f, hz), false);   // 1 top-middle
            AddCave(new Vector3(hx, 0f, hz), true);    // 2 top-right
            AddCave(new Vector3(-hx, 0f, -hz), true);  // 3 bottom-left
            AddCave(new Vector3(0f, 0f, -hz), false);  // 4 bottom-middle
            AddCave(new Vector3(hx, 0f, -hz), true);   // 5 bottom-right

            // The modelled map (mountains, ground ring; its field and pockets become the shrinking island and six solid
            // holes, see ValleyShrink.cs), otherwise the code-built terrain (which doesn't shrink).
            var map = ModelLib.Place(MapModel, transform, Gfx.PaletteMaterial());
            if (map) SetupModelMap(map);
            else BuildTerrain();
            BuildFloor();   // after the map: the floor is cut around the holes' pits
            BuildWalls();
            UpdateWalls();
            // 429R: no HideHandLayouts (BALLS' hand-placed border sketch pad, BorderLayouts, isn't brought over).
        }

        /// <summary>
        /// Sets the valley's size (<paramref name="s"/> = 1 is full size): the lawn, the floor, the border and the six holes
        /// follow it at once (the holes keep their size). The round clock calls it every frame while the valley shrinks
        /// from one phase to the next, or grows back for a new match.
        /// </summary>
        public void SetScale(float s)
        {
            s = Mathf.Clamp(s, 0.05f, 1f);
            if (Mathf.Abs(s - Scale) < 0.00005f) return;
            Scale = s;
            float hx = HalfX, hz = HalfZ;
            foreach (var c in caves)
            {
                c.mouth = new Vector3(c.side.x * hx, 0f, c.side.z * hz);
                c.pitCenter = c.mouth + c.outward * 5f;
            }
            MoveHoles();
            RebuildFloor();
            UpdateWalls();   // (rebuilds the lawn too)
        }

        void AddCave(Vector3 mouth, bool corner)
        {
            var c = new Cave { mouth = mouth, corner = corner };
            c.side = new Vector3(Mathf.Approximately(mouth.x, 0f) ? 0f : Mathf.Sign(mouth.x), 0f, Mathf.Sign(mouth.z));
            c.outward = corner
                ? new Vector3(Mathf.Sign(mouth.x), 0f, Mathf.Sign(mouth.z)).normalized
                : new Vector3(0f, 0f, Mathf.Sign(mouth.z));
            c.baseWidth = c.width = corner ? t.caveCornerWidth : t.caveMiddleWidth;
            c.pitCenter = mouth + c.outward * 5f;
            c.pitRadius = corner ? t.caveMaxWidth * 0.75f + 7f : t.caveMaxWidth * 0.5f + 7f;
            caves.Add(c);
        }

        // ---------- queries ----------

        public Cave NearestCave(Vector3 pos)
        {
            Cave best = null;
            float bd = float.MaxValue;
            foreach (var c in caves)
            {
                float d = FlatDist(pos, c.mouth);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        public float DistanceToNearestCave(Vector3 pos)
        {
            float bd = float.MaxValue;
            foreach (var c in caves) bd = Mathf.Min(bd, FlatDist(pos, c.mouth));
            return bd;
        }

        /// <summary>How far <paramref name="pos"/> is from the nearest pit's rim (below 0: over the pit), on the ground.</summary>
        public float DistanceToNearestPit(Vector3 pos)
        {
            float bd = float.MaxValue;
            foreach (var c in caves)
                bd = Mathf.Min(bd, c.openingRadius > 0f
                    ? FlatDist(pos, c.mouth + c.openingOffset) - c.openingRadius
                    : FlatDist(pos, c.mouth) - c.width * 0.5f);   // (no hole model: the gap's half width)
            return bd;
        }

        static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ---------- building ----------

        /// <summary>
        /// The table the balls roll on: a flat mesh the shape of the lawn, with the holes' pits cut out, so a ball rolls up
        /// to a pit's round rim and tips in over it once its centre is past the edge.
        /// </summary>
        void BuildFloor()
        {
            var floor = new GameObject("Floor Collider");
            floor.transform.SetParent(transform, false);
            floorMesh = new Mesh { name = "Floor" };
            madeMeshes.Add(floorMesh);   // 429R
            floorMesh.MarkDynamic();
            floorCol = floor.AddComponent<MeshCollider>();
            floorCol.sharedMaterial = Gfx.GroundPhys;
            RebuildFloor();
            BallPhysics.SetFloor(floorCol);
        }

        /// <summary>The floor at the valley's size now (its edges and the pits move with the border).</summary>
        void RebuildFloor()
        {
            if (!floorCol) return;
            FloorMesh(floorMesh, HalfX, HalfZ, Cuts());
            floorCol.sharedMesh = null;   // assigning it again makes the collider take the new shape
            floorCol.sharedMesh = floorMesh;
        }

        void BuildWalls()
        {
            var rimMat = Gfx.Lit(new Color(0.36f, 0.33f, 0.30f), 0.1f);
            rimModel = ModelLib.HasPath(RockBorderModel) ? RockBorderModel : ModelLib.HasPath(RimBlockModel) ? RimBlockModel : null;
            rimBlocks = rimModel != null && MeasureRim();
            for (int i = 0; i < 6; i++)
            {
                var col = new GameObject("Rim Wall " + i);
                col.transform.SetParent(transform, false);
                var bc = col.AddComponent<BoxCollider>();
                bc.sharedMaterial = Gfx.WallPhys;
                col.AddComponent<Hazard>();   // the rim hurts as much as a boulder
                wallCol[i] = col.transform;
                if (rimBlocks)
                {
                    wallVis[i] = new GameObject("Rim " + i).transform;
                    wallVis[i].SetParent(transform, false);
                    blocks[i] = new List<Transform>();
                }
                else wallVis[i] = Gfx.Visual("Rim " + i, Gfx.Cube, rimMat, transform, Vector3.zero, Vector3.one, Quaternion.identity);
            }
        }

        /// <summary>
        /// The border is up (always, unless Tuning's Moment Of Truth Crumble is on: then it's gone in the Moment of Truth,
        /// only the lawn and the six holes are left, and a ball can roll off any edge).
        /// </summary>
        public bool BordersOn { get; private set; } = true;

        /// <summary>
        /// Puts the border up or takes it away (every computer does it itself, from the round state). Taking it away
        /// crumbles it: each rock bursts into dust and chips and tumbles down into the chasm. A new match brings it back.
        /// </summary>
        public void SetBorders(bool on)
        {
            if (on == BordersOn || caves.Count < 6) return;
            if (!on) Crumble();
            BordersOn = on;
            UpdateWalls();
        }

        static readonly Color RockDust = new Color(0.62f, 0.58f, 0.52f, 0.85f), RockChips = new Color(0.5f, 0.46f, 0.42f);

        /// <summary>Every rock of the border (or plain wall) still standing breaks up and falls away outward.</summary>
        void Crumble()
        {
            bool any = false;
            for (int i = 0; i < 6; i++)
            {
                Vector3 outward = liveWalls[i].outward;
                if (rimBlocks)
                {
                    foreach (var b in blocks[i])
                        if (b && b.gameObject.activeSelf) { CrumbleRock(b, outward, b.localScale.x * rimSize.x); any = true; }
                }
                else if (wallVis[i] && wallVis[i].gameObject.activeSelf) { CrumbleRock(wallVis[i], outward, wallVis[i].localScale.z); any = true; }
            }
            if (any) Sfx.PlayHere(Sfx.Sound.RockBreak, Vector3.zero, 1f);
        }

        static void CrumbleRock(Transform rock, Vector3 outward, float length)
        {
            Vector3 c = rock.position, along = Vector3.Cross(outward, Vector3.up);
            int bursts = Mathf.Clamp(Mathf.RoundToInt(length / 4f), 1, 3);
            for (int k = 0; k < bursts; k++)
            {
                Vector3 at = c + along * (length * ((k + 0.5f) / bursts - 0.5f)) + Vector3.up * 1.2f;
                Gfx.Puff(at, RockDust, 10, 2.4f, 4f, false);   // (each computer crumbles its own border: nothing to relay)
                Gfx.Sparks(at, RockChips, 10, 8f, false);
            }
            // It tips over outward and falls into the chasm (a copy falls; the real one is put away).
            Vector3 tip = Vector3.Cross(Vector3.up, outward) * Random.Range(70f, 140f) + Random.insideUnitSphere * 25f;
            Vector3 push = outward * Random.Range(2f, 6f) + Vector3.up * Random.Range(1f, 4f);
            Falling.DropCopy(rock, push, 2.4f, tip);
        }

        /// <summary>The six walls between the holes at the valley's size now (and the lawn reaching out under them).</summary>
        void UpdateWalls()
        {
            Cave tl = caves[0], tm = caves[1], tr = caves[2], bl = caves[3], bm = caves[4], br = caves[5];
            SetWall(0, tl, Vector3.right, tm, Vector3.left, Vector3.forward);
            SetWall(1, tm, Vector3.right, tr, Vector3.left, Vector3.forward);
            SetWall(2, bl, Vector3.right, bm, Vector3.left, Vector3.back);
            SetWall(3, bm, Vector3.right, br, Vector3.left, Vector3.back);
            SetWall(4, bl, Vector3.forward, tl, Vector3.back, Vector3.left);
            SetWall(5, br, Vector3.forward, tr, Vector3.back, Vector3.right);
            RebuildField();   // the lawn under the border follows the walls
        }

        /// <summary>
        /// The wall between hole <paramref name="from"/> and hole <paramref name="to"/> (it leaves <paramref name="from"/>
        /// along <paramref name="dirA"/> and <paramref name="to"/> along <paramref name="dirB"/>). It's solid right up to
        /// both pits, so a ball only drops where a pit is; its rocks run up to both holes' collars, so no gap shows.
        /// </summary>
        void SetWall(int i, Cave from, Vector3 dirA, Cave to, Vector3 dirB, Vector3 outward)
        {
            // a -> b runs along +x or +z; once the holes on both ends meet (a tiny valley), or in the Moment of Truth,
            // there's no wall.
            Vector3 a = from.mouth + dirA * from.WallFrom(dirA), b = to.mouth + dirB * to.WallFrom(dirB);
            Vector3 ra = from.mouth + dirA * from.RocksFrom(dirA), rb = to.mouth + dirB * to.RocksFrom(dirB);
            Vector3 d = b - a, rd = rb - ra;
            bool on = BordersOn && d.x + d.z > 0.3f, rocksOn = on && rd.x + rd.z > 0.3f;
            if (!rimBlocks) { ra = a; rb = b; rocksOn = on; }   // the plain wall shows the whole solid part
            liveWalls[i] = new Wall { a = ra, b = rocksOn ? rb : ra, outward = outward };
            if (wallCol[i].gameObject.activeSelf != on) wallCol[i].gameObject.SetActive(on);
            if (!rimBlocks && wallVis[i].gameObject.activeSelf != on) wallVis[i].gameObject.SetActive(on);
            if (rimBlocks) LayBlocks(i, ra, rocksOn ? rb : ra, outward);   // (none if the collars meet)
            if (!on) return;
            Vector3 mid = (a + b) * 0.5f + outward * (WallT * 0.5f);
            float len = Mathf.Max(0.1f, Vector3.Distance(a, b));
            Quaternion rot = Quaternion.LookRotation((b - a).normalized, Vector3.up);
            wallCol[i].SetPositionAndRotation(mid + Vector3.up * (WallColH * 0.5f - 1f), rot);
            wallCol[i].localScale = new Vector3(WallT, WallColH, len);
            if (rimBlocks) return;
            wallVis[i].SetPositionAndRotation(mid + Vector3.up * (WallVisH * 0.5f - 0.2f), rot);
            wallVis[i].localScale = new Vector3(WallT, WallVisH, len);
        }

        /// <summary>
        /// Measures the border rock once, as modelled: its size and the middle of its bounds (each rock turns about its
        /// middle, so an upside-down one stays where it was). False if it has nothing to measure.
        /// </summary>
        bool MeasureRim()
        {
            var probe = ModelLib.Place(rimModel, transform, null);   // 429R: under the valley (BALLS: the scene root)
            if (!probe) return false;
            probe.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            bool any = false;
            var b = new Bounds();
            foreach (var r in probe.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            probe.gameObject.SetActive(false);
            Destroy(probe.gameObject);
            if (!any || b.size.x < 0.5f) return false;
            rimSize = b.size;
            rimCenter = b.center;
            rimTurns = rimModel == RockBorderModel;
            return true;
        }

        /// <summary>
        /// Fills the wall from a to b with border rocks, side by side and stretched along their length to fit it exactly.
        /// Rock_Border: neighbours overlap a little (as laid out in BALLS_Map.blend) and never stand the same way; the
        /// older Rim_Block: about 10 m each, with a small gap between them.
        /// </summary>
        void LayBlocks(int i, Vector3 a, Vector3 b, Vector3 outward)
        {
            float len = Vector3.Distance(a, b);
            var list = blocks[i];
            float step = rimTurns ? rimSize.x * RockBorderSpacing : RimBlockStep;
            int n = len < 1f ? 0 : Mathf.Max(1, Mathf.RoundToInt(len / step));
            while (list.Count < n)
            {
                var pivot = new GameObject("Rock").transform;
                pivot.SetParent(wallVis[i], false);
                var rock = ModelLib.Place(rimModel, pivot, Gfx.PaletteMaterial());
                if (rock) rock.SetLocalPositionAndRotation(-rimCenter, Quaternion.identity);   // the pivot is the rock's middle
                list.Add(pivot);
            }
            if (n == 0) { foreach (var p in list) p.gameObject.SetActive(false); return; }
            Vector3 dir = (b - a) / len;
            float seg = len / n;
            Quaternion along = Quaternion.Euler(0f, -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg, 0f);   // the rock's length runs along its X
            float stretch = rimTurns ? seg / step : Mathf.Max(0.05f, seg - RimBlockGap) / rimSize.x;
            int turn = -1;
            for (int k = 0; k < list.Count; k++)
            {
                var p = list[k];
                bool on = k < n;
                if (p.gameObject.activeSelf != on) p.gameObject.SetActive(on);
                if (!on) continue;
                turn = rimTurns ? RockTurn(i, k, turn) : 0;
                Vector3 c = a + dir * (seg * (k + 0.5f)) + outward * (rimSize.z * 0.5f);
                p.SetPositionAndRotation(new Vector3(c.x, rimCenter.y, c.z), along * RockTurns[turn]);
                p.localScale = new Vector3(stretch, 1f, 1f);
            }
        }

        /// <summary>
        /// The four ways a border rock stands: as modelled, upside down (Blender X 180), turned round (Blender Z 180), and
        /// both (in Unity's axes: X 180, Y 180, Z 180).
        /// </summary>
        static readonly Quaternion[] RockTurns =
            { Quaternion.identity, Quaternion.Euler(180f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f), Quaternion.Euler(0f, 0f, 180f) };

        /// <summary>
        /// How rock <paramref name="k"/> of wall <paramref name="wall"/> stands: never the same as the rock before it
        /// (<paramref name="previous"/>), otherwise mixed up. Worked out from the numbers alone, so it's the same on every
        /// computer and each rock keeps its turn while the valley shrinks.
        /// </summary>
        static int RockTurn(int wall, int k, int previous)
        {
            uint h = (uint)(wall * 7919 + k * 104729 + 1) * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return previous < 0 ? (int)(h % 4u) : (previous + 1 + (int)(h % 3u)) % 4;
        }

        void BuildTerrain()
        {
            const float cell = 2f, margin = 90f;
            float minX = -t.halfX - margin, minZ = -t.halfZ - margin;
            int nx = Mathf.RoundToInt((2f * t.halfX + 2f * margin) / cell) + 1;
            int nz = Mathf.RoundToInt((2f * t.halfZ + 2f * margin) / cell) + 1;
            var verts = new Vector3[nx * nz];
            var cols = new Color[nx * nz];
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x = minX + ix * cell, z = minZ + iz * cell;
                Sample(x, z, out float h, out Color c);
                int i = iz * nx + ix;
                verts[i] = new Vector3(x, h, z);
                cols[i] = c.linear;
            }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int k = 0;
            for (int iz = 0; iz < nz - 1; iz++)
            for (int ix = 0; ix < nx - 1; ix++)
            {
                int i = iz * nx + ix;
                tris[k++] = i; tris[k++] = i + nx; tris[k++] = i + 1;
                tris[k++] = i + 1; tris[k++] = i + nx; tris[k++] = i + nx + 1;
            }
            var mesh = new Mesh { name = "Valley Terrain", indexFormat = IndexFormat.UInt32 };
            madeMeshes.Add(mesh);   // 429R
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Terrain");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Gfx.VertexColorMaterial();
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void Sample(float x, float z, out float h, out Color col)
        {
            float dx = Mathf.Max(Mathf.Abs(x) - t.halfX, 0f), dz = Mathf.Max(Mathf.Abs(z) - t.halfZ, 0f);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float fine = Mathf.PerlinNoise(x * 0.15f + 3.3f, z * 0.15f + 9.1f);

            if (d < 0.01f)
            {
                // Valley floor: mowed-lawn stripes (helps you feel speed), darker "danger" grass near caves.
                h = 0f;
                bool stripe = (Mathf.FloorToInt((x + 1000f) / 10f) & 1) == 0;
                Color g = stripe ? new Color(0.33f, 0.58f, 0.24f) : new Color(0.38f, 0.64f, 0.28f);
                g *= 0.92f + 0.16f * fine;
                float dc = float.MaxValue;
                foreach (var c in caves) dc = Mathf.Min(dc, FlatDist(new Vector3(x, 0f, z), c.mouth));
                float danger = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 20f, dc));
                g = Color.Lerp(g, new Color(0.16f, 0.22f, 0.12f), danger * 0.65f);
                g.a = 1f;
                col = g;
                return;
            }

            float n = Fbm(x * 0.02f + 11.3f, z * 0.02f + 7.1f);
            float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.035f + 41f, z * 0.035f + 13f) * 2f - 1f);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 42f, d));
            h = 0.6f + Mathf.Sqrt(rise) * (16f + 46f * n + 18f * ridge * rise) + (fine - 0.5f) * 2.5f * rise;

            float pit = 0f;
            foreach (var c in caves)
            {
                float pd = FlatDist(new Vector3(x, 0f, z), c.pitCenter);
                pit = Mathf.Max(pit, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c.pitRadius * 0.5f, c.pitRadius, pd)));
            }
            h = Mathf.Lerp(h, -26f, pit);

            Color grass = new Color(0.27f, 0.44f, 0.20f);
            Color rock = Color.Lerp(new Color(0.36f, 0.32f, 0.28f), new Color(0.52f, 0.49f, 0.44f), fine);
            Color snow = new Color(0.95f, 0.96f, 1f);
            Color c2 = Color.Lerp(grass, rock, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 9f, h + (fine - 0.5f) * 6f)));
            c2 = Color.Lerp(c2, snow, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(54f, 62f, h + (fine - 0.5f) * 10f)));
            if (h < 0.5f) c2 = Color.Lerp(c2, new Color(0.02f, 0.015f, 0.02f), Mathf.InverseLerp(0.5f, -6f, h));
            c2.a = 1f;
            col = c2;
        }

        static float Fbm(float x, float z)
        {
            float a = 0.5f, s = 0f, norm = 0f;
            for (int i = 0; i < 4; i++)
            {
                s += a * Mathf.PerlinNoise(x, z);
                norm += a;
                x *= 2.03f;
                z *= 2.03f;
                a *= 0.5f;
            }
            return s / norm;
        }
    }
}
