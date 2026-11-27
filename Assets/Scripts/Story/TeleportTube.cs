using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The teleport tube (user 2026-11-27: "then poof, create a teleport tube run around Player then move them to
    /// center"): a glowing green tube of light streaks that rises out of the floor round the player and spins, with rings
    /// sweeping up it and sparks spiralling up its wall. It stands where the player's head is (on their floor), so it
    /// goes with them when they are moved and keeps running round them at the middle, then sinks away (End).
    /// Made by <see cref="GreenPathRun"/>; its parts by Tools > 429 Game > Story > Set Up Green Path
    /// (Prefabs/Story/Teleport_Tube). The tube's mesh is made here (an open cylinder, bright low down, fading to the top).
    /// </summary>
    public class TeleportTube : MonoBehaviour
    {
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Parts (made by Set Up Green Path)")]
        [SerializeField] private MeshFilter tubeMesh;
        [SerializeField] private MeshRenderer tube;
        [SerializeField] private LineRenderer[] rings = new LineRenderer[0];
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private AudioSource sound;

        [Header("Size and motion")]
        [Tooltip("Its radius round the player's head (metres).")]
        [SerializeField] private float radius = 0.72f;
        [Tooltip("Its height when up (metres).")]
        [SerializeField] private float height = 2.7f;
        [Tooltip("Seconds it takes to rise out of the floor.")]
        [SerializeField] private float growSeconds = 0.35f;
        [Tooltip("Degrees a second it spins.")]
        [SerializeField] private float spin = 150f;
        [Tooltip("How fast its streaks run up it.")]
        [SerializeField] private float scroll = 1.1f;
        [Tooltip("Rings that sweep up it, each this many times a second.")]
        [SerializeField] private float ringsPerSecond = 0.9f;
        [SerializeField] private float ringWidth = 0.035f;
        [SerializeField] private float sparksPerSecond = 70f;
        [Tooltip("Around its wall, how many times its streak picture repeats.")]
        [SerializeField] private int repeatsAround = 3;
        [SerializeField] private int segments = 36;

        private Transform head, rig;
        private float grow, target = 1f, shrinkSpeed = 2f, clock, owedSparks;
        private Material material;
        private Color colour = new Color(0.55f, 1f, 0.45f, 1f);
        private float volume = 0.5f;
        private bool ending;

        /// <summary>How far up it is (0 = gone, 1 = fully up).</summary>
        public float Grow => grow;

        /// <summary>A tube round the player (their head, on the rig's floor), rising. Null without looks.</summary>
        public static TeleportTube Make(GreenPathSettings settings, Transform head, Transform rig)
        {
            if (settings == null || settings.TubePrefab == null || head == null) return null;
            var go = Instantiate(settings.TubePrefab);
            go.name = "Teleport_Tube";
            var t = go.GetComponent<TeleportTube>();
            if (t == null)
            {
                Destroy(go);
                return null;
            }
            t.head = head;
            t.rig = rig;
            t.colour = settings.Colour;
            t.volume = settings.TubeVolume;
            t.Place();
            if (t.sound != null && settings.TubeSound != null)
            {
                t.sound.clip = settings.TubeSound;
                t.sound.loop = true;
                t.sound.volume = 0f;
                t.sound.Play();
            }
            return t;
        }

        /// <summary>Sinks away over these seconds, then it goes.</summary>
        public void End(float seconds)
        {
            target = 0f;
            shrinkSpeed = 1f / Mathf.Max(0.05f, seconds);
            ending = true;
        }

        private void Awake()
        {
            if (tubeMesh != null) tubeMesh.sharedMesh = MakeMesh();
            if (tube != null) material = tube.material;
            foreach (var ring in rings)
            {
                if (ring == null) continue;
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = 40;
                for (int i = 0; i < 40; i++)
                {
                    float a = i * Mathf.PI * 2f / 40f;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius * 1.03f));
                }
                ring.widthMultiplier = ringWidth;
            }
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (tubeMesh != null && tubeMesh.sharedMesh != null) Destroy(tubeMesh.sharedMesh);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            clock += dt;
            float speed = target > grow ? 1f / Mathf.Max(0.01f, growSeconds) : shrinkSpeed;
            grow = Mathf.MoveTowards(grow, target, dt * speed);
            Place();
            float g = grow * grow * (3f - 2f * grow);
            float h = Mathf.Max(0.001f, height * g);
            if (tube != null)
            {
                tube.transform.localScale = new Vector3(radius, h, radius);
                tube.transform.localRotation = Quaternion.Euler(0f, clock * spin, 0f);
            }
            if (material != null)
            {
                material.SetVector(BaseMapST, new Vector4(repeatsAround, 1.5f, 0f, -clock * scroll));
                var c = colour;
                c.a = g;
                material.SetColor(BaseColorId, c);
            }
            for (int i = 0; i < rings.Length; i++)
            {
                var ring = rings[i];
                if (ring == null) continue;
                float phase = (clock * ringsPerSecond + i / (float)Mathf.Max(1, rings.Length)) % 1f;
                ring.transform.localPosition = Vector3.up * (phase * h);
                var c = colour;
                c.a = Mathf.Sin(phase * Mathf.PI) * g;
                ring.startColor = c;
                ring.endColor = c;
            }
            if (sparks != null && !ending)
            {
                owedSparks += sparksPerSecond * dt * g;
                var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (; owedSparks >= 1f; owedSparks -= 1f)
                {
                    float a = clock * spin * Mathf.Deg2Rad * 2f + Random.Range(0, 3) * Mathf.PI * 2f / 3f + Random.Range(-0.2f, 0.2f);
                    var round = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    p.position = transform.position + round * (radius * 0.96f) + Vector3.up * Random.Range(0f, h * 0.9f);
                    p.velocity = Vector3.up * Random.Range(0.8f, 1.7f) + Vector3.Cross(Vector3.up, round) * 0.6f;
                    sparks.Emit(p, 1);
                }
            }
            if (sound != null)
            {
                sound.volume = volume * g;
                sound.pitch = 0.85f + 0.45f * g;
            }
            if (ending && grow <= 0f) Destroy(gameObject);
        }

        // On the player's floor, under their head.
        private void Place()
        {
            if (head == null) return;
            float floor = rig != null ? rig.position.y : head.position.y - 1.6f;
            transform.position = new Vector3(head.position.x, floor, head.position.z);
            transform.rotation = Quaternion.identity;
        }

        // An open cylinder, radius 1 and 1 high (scaled up by the tube): bright where it meets the floor, fading out at
        // the top (vertex colours, the shader multiplies them in); its streak picture wraps round it.
        private Mesh MakeMesh()
        {
            float[] rows = { 0f, 0.08f, 0.5f, 1f };
            float[] alphas = { 0.6f, 1f, 0.55f, 0f };
            int around = Mathf.Max(8, segments);
            var vertices = new Vector3[(around + 1) * rows.Length];
            var uvs = new Vector2[vertices.Length];
            var colours = new Color[vertices.Length];
            var normals = new Vector3[vertices.Length];
            for (int r = 0; r < rows.Length; r++)
            {
                for (int i = 0; i <= around; i++)
                {
                    float a = i * Mathf.PI * 2f / around;
                    int v = r * (around + 1) + i;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    vertices[v] = dir + Vector3.up * rows[r];
                    normals[v] = dir;
                    uvs[v] = new Vector2(i / (float)around, rows[r]);
                    colours[v] = new Color(1f, 1f, 1f, alphas[r]);
                }
            }
            var triangles = new int[around * (rows.Length - 1) * 6];
            int t = 0;
            for (int r = 0; r < rows.Length - 1; r++)
            {
                for (int i = 0; i < around; i++)
                {
                    int a = r * (around + 1) + i;
                    int b = a + around + 1;
                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }
            var mesh = new Mesh { name = "Teleport Tube (made)" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colours;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.up * 0.5f, new Vector3(2.2f, 1.2f, 2.2f));
            return mesh;
        }
    }
}
