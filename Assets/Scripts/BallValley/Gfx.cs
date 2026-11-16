using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Balls
{
    /// <summary>Runtime-made materials, meshes, physics materials and particle effects (keeps the prototype free of asset wiring).</summary>
    public static class Gfx
    {
        public static Shader VertexColorShader;
        public static PhysicsMaterial BallPhys, GroundPhys, WallPhys, RockPhys;

        static Shader litShader, particleShader;
        static readonly Dictionary<string, Material> litCache = new Dictionary<string, Material>();
        static Mesh sphere, cylinder, cube, cone, quad, ballSphere, smallSphere, tinySphere;
        static readonly Mesh[] rocks = new Mesh[4];
        static Texture2D soft;
        static Material particleMat;
        static ParticleSystem sparks, puffs;

        /// <summary>
        /// Unity's built-in sphere (768 triangles). Only for one-off things: the ball in the sky at PLAY, the winner's
        /// portrait, the stand-in slime and the code-made rocks. Anything that comes in numbers uses
        /// <see cref="BallSphere"/> or <see cref="SmallSphere"/>.
        /// </summary>
        public static Mesh Sphere => sphere ? sphere : (sphere = PrimitiveMesh(PrimitiveType.Sphere));

        /// <summary>
        /// The main balls (heads) and the loose balls: a light sphere of 180 triangles instead of the built-in 768, same
        /// size (radius 0.5). There can be hundreds of loose balls, each drawn again into the shadow map. Tail balls use
        /// <see cref="SmallSphere"/>.
        /// </summary>
        public static Mesh BallSphere => ballSphere ? ballSphere : (ballSphere = Geosphere(3, "Ball Sphere"));

        /// <summary>The tail balls and small round things (eyes, pupils, the spots on a ball, the crown's gem): 80 triangles.</summary>
        public static Mesh SmallSphere => smallSphere ? smallSphere : (smallSphere = Geosphere(2, "Small Sphere"));

        /// <summary>
        /// Berries: an icosahedron with smooth normals, 20 triangles (there can be well over 100 lying about), turned
        /// with a corner straight up. Berries spin round the up axis, and from the game's camera that keeps their outline
        /// rounder than with an edge on top.
        /// </summary>
        public static Mesh TinySphere => tinySphere ? tinySphere : (tinySphere = CornerUp(Geosphere(1, "Tiny Sphere")));

        public static Mesh Cylinder => cylinder ? cylinder : (cylinder = PrimitiveMesh(PrimitiveType.Cylinder));
        public static Mesh Cube => cube ? cube : (cube = PrimitiveMesh(PrimitiveType.Cube));
        public static Mesh Cone => cone ? cone : (cone = BuildCone(12));
        public static Mesh Quad => quad ? quad : (quad = PrimitiveMesh(PrimitiveType.Quad));

        public static void Init(Transform fxParent)
        {
            BallPhys = new PhysicsMaterial("Ball")
            {
                dynamicFriction = 0.5f, staticFriction = 0.5f, bounciness = 0.7f,
                frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Average
            };
            // Frictionless table: rolling is handled in code (BallPhysics.RollWithVelocity), slowdown by damping.
            GroundPhys = new PhysicsMaterial("Ground")
            {
                dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
            };
            // Rim walls are the snooker cushions: lively bounce so knocked balls keep rolling after hitting them.
            WallPhys = new PhysicsMaterial("Wall")
            {
                dynamicFriction = 0.1f, staticFriction = 0.1f, bounciness = 0.85f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Maximum
            };
            RockPhys = new PhysicsMaterial("Rock")
            {
                dynamicFriction = 0.4f, staticFriction = 0.4f, bounciness = 0.5f,
                frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Average
            };

            sparks = MakeSystem("FX Sparks", fxParent, 1.4f, 1200);   // 429R: 1200 (BALLS 3000)
            var sz = sparks.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            puffs = MakeSystem("FX Puffs", fxParent, -0.05f, 1200);   // 429R: 1200 (BALLS 3000)
            var pz = puffs.sizeOverLifetime;
            pz.enabled = true;
            pz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.3f));
        }

        // ---------- materials ----------

        public static Material Lit(Color c, float smooth = 0.5f, Color emission = default, float metal = 0f)
        {
            string key = $"{c.r:F3},{c.g:F3},{c.b:F3}|{smooth:F2}|{emission.r:F3},{emission.g:F3},{emission.b:F3}|{metal:F2}";
            if (litCache.TryGetValue(key, out var m) && m) return m;
            m = NewLit(c, smooth, emission, metal);
            litCache[key] = m;
            return m;
        }

        static Material palette, slimePalette;

        /// <summary>Swatch columns in BALLS_Palette.png (16 x 8 swatches of 32 px).</summary>
        public const int PaletteColumns = 16;

        /// <summary>
        /// The slime skin, as in 429R: the "429/Slime Lit" shader with one painted texture (white where it takes colour,
        /// its alpha marking where) and a glow map for the eyes. Each slime sets its own Paint Colour (_BaseColor), so
        /// one texture gives any number of colours. Null if Resources/BALLS/Slime_Mat is missing.
        /// </summary>
        public static Material SlimeMaterial()
        {
            if (!slimePalette) slimePalette = Resources.Load<Material>("BALLS/Slime_Mat");
            return slimePalette;
        }

        /// <summary>
        /// The one material every BALLS_Map.blend model uses: Resources/BALLS/BALLS_Palette.png, a grid of colour swatches
        /// that each face's UVs point into. Falls back to plain grey if the texture is missing.
        /// </summary>
        public static Material PaletteMaterial()
        {
            if (palette) return palette;
            palette = NewLit(Color.white, 0.12f, Color.black);
            palette.name = "BALLS_Palette";
            var tex = Resources.Load<Texture2D>("BALLS/BALLS_Palette");
            if (tex)
            {
                tex.filterMode = FilterMode.Point;   // crisp swatches, no bleeding between neighbours
                palette.SetTexture("_BaseMap", tex);
                palette.mainTexture = tex;
            }
            else
            {
                Debug.LogWarning("Resources/BALLS/BALLS_Palette.png not found; map models will be grey.");
                palette.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.6f));
            }
            return palette;
        }

        public static Material NewLit(Color c, float smooth, Color emission, float metal = 0f)
        {
            if (!litShader) litShader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(litShader);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        public static Material VertexColorMaterial()
        {
            var sh = VertexColorShader ? VertexColorShader : Shader.Find("BALLS/VertexColorLit");
            if (sh && sh.isSupported) return new Material(sh);
            Debug.LogWarning("BALLS/VertexColorLit shader not found, terrain falls back to plain Lit.");
            return NewLit(new Color(0.4f, 0.5f, 0.3f), 0.1f, Color.black);
        }

        /// <summary>The particle material (see-through, unlit) with another picture on it (e.g. the dizzy stars).</summary>
        public static Material ParticleMaterialWith(Texture2D picture)
        {
            var m = new Material(ParticleMaterial);
            m.SetTexture("_BaseMap", picture);
            return m;
        }

        static Material ParticleMaterial
        {
            get
            {
                if (particleMat) return particleMat;
                if (!particleShader) particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                var m = new Material(particleShader);
                m.SetTexture("_BaseMap", SoftTexture());
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                particleMat = m;
                return m;
            }
        }

        public static Texture2D SoftTexture()
        {
            if (soft) return soft;
            const int n = 64;
            soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                soft.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            soft.Apply();
            return soft;
        }

        // ---------- meshes / objects ----------

        static Mesh PrimitiveMesh(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return m;
        }

        // 429R: no on-screen panels (BALLS' UiFont, Face, GuiScale and its fonts).

        public static Transform Visual(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot)
        {
            var go = new GameObject(name);
            var tr = go.transform;
            tr.SetParent(parent, false);
            tr.localPosition = localPos;
            tr.localRotation = localRot;
            tr.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return tr;
        }

        static Mesh BuildCone(int seg)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            Vector3 tip = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                int b = v.Count;
                v.Add(tip); v.Add(p1); v.Add(p0);
                tri.Add(b); tri.Add(b + 1); tri.Add(b + 2);
                b = v.Count;
                v.Add(Vector3.zero); v.Add(p0); v.Add(p1);
                tri.Add(b); tri.Add(b + 1); tri.Add(b + 2);
            }
            var m = new Mesh { name = "Cone" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// A geodesic sphere of radius 0.5: an icosahedron whose 20 faces are each cut into detail x detail triangles,
        /// pushed out onto the sphere (20 x detail x detail triangles: 20, 80, 180, 320...). The triangles are spread evenly, so it
        /// stays round with far fewer of them than the built-in sphere. Normals point straight out; no UVs (plain colours).
        /// </summary>
        static Mesh Geosphere(int detail, string name)
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-1f, g, 0f), new Vector3(1f, g, 0f), new Vector3(-1f, -g, 0f), new Vector3(1f, -g, 0f),
                new Vector3(0f, -1f, g), new Vector3(0f, 1f, g), new Vector3(0f, -1f, -g), new Vector3(0f, 1f, -g),
                new Vector3(g, 0f, -1f), new Vector3(g, 0f, 1f), new Vector3(-g, 0f, -1f), new Vector3(-g, 0f, 1f)
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var byPlace = new Dictionary<Vector3Int, int>();   // points on a face's edges are shared with the next face
            var grid = new int[detail + 1, detail + 1];
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = corners[faces[f]], ab = corners[faces[f + 1]] - a, ac = corners[faces[f + 2]] - a;
                for (int i = 0; i <= detail; i++)
                    for (int j = 0; i + j <= detail; j++)
                    {
                        Vector3 p = (a + ab * ((float)i / detail) + ac * ((float)j / detail)).normalized * 0.5f;
                        var key = Vector3Int.RoundToInt(p * 1000f);
                        if (!byPlace.TryGetValue(key, out int v))
                        {
                            v = verts.Count;
                            verts.Add(p);
                            byPlace.Add(key, v);
                        }
                        grid[i, j] = v;
                    }
                for (int i = 0; i < detail; i++)
                    for (int j = 0; i + j < detail; j++)
                    {
                        AddOutward(tris, verts, grid[i, j], grid[i + 1, j], grid[i, j + 1]);
                        if (i + j + 1 < detail) AddOutward(tris, verts, grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]);
                    }
            }
            var normals = new List<Vector3>(verts.Count);
            foreach (var p in verts) normals.Add(p * 2f);
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Turns a round mesh so its highest corner points straight up.</summary>
        static Mesh CornerUp(Mesh m)
        {
            var vs = m.vertices;
            var ns = m.normals;
            Vector3 top = vs[0];
            foreach (var v in vs)
                if (v.y > top.y) top = v;
            var turn = Quaternion.FromToRotation(top, Vector3.up);
            for (int i = 0; i < vs.Length; i++)
            {
                vs[i] = turn * vs[i];
                ns[i] = turn * ns[i];
            }
            m.vertices = vs;
            m.normals = ns;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Adds a triangle wound so its front side faces out of the sphere.</summary>
        static void AddOutward(List<int> tris, List<Vector3> v, int a, int b, int c)
        {
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), v[a] + v[b] + v[c]) < 0f) (b, c) = (c, b);
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }

        public static Mesh RockMesh(int variant)
        {
            variant = Mathf.Abs(variant) % rocks.Length;
            if (rocks[variant]) return rocks[variant];
            var m = Object.Instantiate(Sphere);
            m.name = "Rock " + variant;
            var vs = m.vertices;
            float ox = variant * 13.7f + 3.1f, oz = variant * 7.3f + 1.7f;
            for (int i = 0; i < vs.Length; i++)
            {
                var p = vs[i];
                float n1 = Mathf.PerlinNoise(p.x * 2.2f + ox, p.z * 2.2f + p.y * 1.7f + oz);
                float n2 = Mathf.PerlinNoise(p.y * 4f + oz, p.x * 4f + ox);
                vs[i] = p * (0.75f + 0.45f * n1 + 0.12f * n2);
            }
            m.vertices = vs;
            m.RecalculateNormals();
            m.RecalculateBounds();
            rocks[variant] = m;
            return m;
        }

        const string CrownModel = "Map/Crown";
        static Material crownMat;

        /// <summary>
        /// The King of the Valley's crown (also on the winner's portrait): the Crown model from BALLS_Map.blend
        /// (Resources/BALLS/Map/Crown, its origin the middle of the band), coloured by its palette swatches but shiny like
        /// gold. The old code-built crown stands in if the model is missing.
        /// </summary>
        public static Transform MakeCrown(Transform parent)
        {
            var root = new GameObject("Crown").transform;
            root.SetParent(parent, false);
            if (ModelLib.HasPath(CrownModel) && ModelLib.Place(CrownModel, root, CrownMaterial())) return root;
            var gold = Lit(new Color(1f, 0.78f, 0.15f), 0.9f, new Color(0.45f, 0.3f, 0f), 1f);
            Visual("Band", Cylinder, gold, root, Vector3.zero, new Vector3(1f, 0.13f, 1f), Quaternion.identity);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Visual("Spike", Cone, gold, root, new Vector3(Mathf.Cos(a) * 0.4f, 0.1f, Mathf.Sin(a) * 0.4f), new Vector3(0.3f, 0.5f, 0.3f), Quaternion.identity);
            }
            Visual("Gem", SmallSphere, Lit(new Color(0.9f, 0.1f, 0.2f), 0.95f, new Color(0.6f, 0f, 0.05f)), root,
                new Vector3(0f, 0.05f, 0.5f), Vector3.one * 0.22f, Quaternion.identity);
            return root;
        }

        /// <summary>The palette, but metallic and smooth, and glowing a little in each swatch's own colour (as the old gold did).</summary>
        static Material CrownMaterial()
        {
            if (crownMat) return crownMat;
            var tex = PaletteMaterial().GetTexture("_BaseMap");
            crownMat = NewLit(Color.white, 0.85f, new Color(0.45f, 0.45f, 0.45f), 1f);
            crownMat.name = "Crown";
            if (tex)
            {
                crownMat.SetTexture("_BaseMap", tex);
                crownMat.mainTexture = tex;
                crownMat.SetTexture("_EmissionMap", tex);
            }
            return crownMat;
        }

        // ---------- 429R: Quest and the stage's coming and going ----------

        /// <summary>429R: small things (berries, grass, slimes, loose balls) cast no shadows (BALLS: on phones only).</summary>
        public static void NoShadows(Component root)
        {
            if (!root) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// 429R: the stage is gone: everything made here (materials, meshes, pictures, physics materials) is destroyed and
        /// forgotten, so the next stage starts clean. Unity's own meshes and loaded assets are only let go of.
        /// </summary>
        public static void Shutdown()
        {
            foreach (var m in litCache.Values) if (m) Object.Destroy(m);
            litCache.Clear();
            Kill(ref palette);
            Kill(ref crownMat);
            Kill(ref particleMat);
            slimePalette = null;
            Kill(ref soft);
            Kill(ref ballSphere);
            Kill(ref smallSphere);
            Kill(ref tinySphere);
            Kill(ref cone);
            for (int i = 0; i < rocks.Length; i++) Kill(ref rocks[i]);
            sphere = cylinder = cube = quad = null;
            Kill(ref BallPhys);
            Kill(ref GroundPhys);
            Kill(ref WallPhys);
            Kill(ref RockPhys);
            sparks = puffs = null;
        }

        static void Kill<T>(ref T thing) where T : Object
        {
            if (thing) Object.Destroy(thing);
            thing = null;
        }

        // ---------- particles ----------

        static ParticleSystem MakeSystem(string name, Transform parent, float gravity, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 1f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var ren = go.GetComponent<ParticleSystemRenderer>();
            ren.sharedMaterial = ParticleMaterial;
            ren.renderMode = ParticleSystemRenderMode.Billboard;
            ren.shadowCastingMode = ShadowCastingMode.Off;
            ren.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>The "burnout" smoke under a charging / dashing ball.</summary>
        public static ParticleSystem MakeSmoke(Transform parent)
        {
            var ps = MakeSystem("Smoke", parent, -0.05f, 800);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.9f, 0.9f, 0.75f), new Color(0.62f, 0.62f, 0.65f, 0.6f));
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(0f);
            vol.y = new ParticleSystem.MinMaxCurve(1.6f);
            vol.z = new ParticleSystem.MinMaxCurve(0f);
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f));
            ps.Play();
            return ps;
        }

        // 429R: no network: BALLS' FxRelay is gone and "relay" means nothing here (kept so the callers stay as they are).
        public static void Sparks(Vector3 pos, Color c, int count, float speed = 8f, bool relay = true)
        {
            if (!sparks) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos;
                ep.velocity = Random.insideUnitSphere * speed + Vector3.up * (speed * 0.5f);
                ep.startColor = c;
                ep.startSize = Random.Range(0.15f, 0.4f);
                ep.startLifetime = Random.Range(0.3f, 0.65f);
                sparks.Emit(ep, 1);
            }
        }

        public static void Puff(Vector3 pos, Color c, int count, float size, float speed, bool relay = true)
        {
            if (!puffs) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Random.insideUnitSphere * (size * 0.3f);
                ep.velocity = Random.insideUnitSphere * speed + Vector3.up * (speed * 0.3f);
                ep.startColor = c;
                ep.startSize = size * Random.Range(0.6f, 1.2f);
                ep.startLifetime = Random.Range(0.6f, 1.2f);
                puffs.Emit(ep, 1);
            }
        }
    }
}
