using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Bowling
{
    /// <summary>
    /// The vines on the bowling arena's dirt island (Gate 2). Each vine runs from the middle out to one ball sprout, so
    /// the bowling pumpkins grow on its tip. They start hidden; each seed the player eats grows the next group
    /// (GrowNext: 4, 2, 1, 1 vines, as BowlingLayout.SproutGroups) out of the dirt in 0.9 s with a puff, a magic sound and
    /// a few leaves flying. A vine grows by drawing more and more of its own mesh, ring by ring from the root to the
    /// tip (nothing is rebuilt); its leaf cards pop in (Game.GrowIn) as the tip passes them. Built by
    /// Tools > 429 Game > Bowling > Set Up Bowling Look.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VineGrow : MonoBehaviour
    {
        /// <summary>One vine: its mesh, which seed grows it, how much of the mesh to draw up to each ring, its leaves.</summary>
        [Serializable]
        public sealed class Vine
        {
            [Tooltip("The vine's tube (vertices and triangles ordered root to tip). It gets its own copy at start.")]
            public MeshFilter filter;
            [Tooltip("Which seed grows it: 0 = the start seed, 1, 2, 3 = the phase chests' seeds.")]
            public int group;
            [Tooltip("For each ring, root to tip: how many triangle indices to draw to show the vine up to that ring.")]
            public int[] ringIndexEnds = new int[0];
            [Tooltip("Leaf cards (inactive until the tip passes them; each has Grow In).")]
            public GameObject[] leaves = new GameObject[0];
            [Tooltip("For each leaf: how far along the vine it sits (0 = root, 1 = tip). It pops in when the vine gets there.")]
            public float[] leafShares = new float[0];
        }

        [Header("Vines")]
        [Tooltip("The vines (one per ball sprout), built by Set Up Bowling Look.")]
        [SerializeField] private Vine[] vines = new Vine[0];
        [Tooltip("Seconds a vine takes to grow from the root to the tip.")]
        [Min(0.05f)]
        [SerializeField] private float growSeconds = 0.9f;

        [Header("When a group grows")]
        [Tooltip("Smoke puff at the middle (Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject puff;
        [Tooltip("The puff's size.")]
        [Min(0f)]
        [SerializeField] private float puffScale = 0.5f;
        [Tooltip("Sound as the vines pop out (A_magical_glowing).")]
        [SerializeField] private AudioClip growSound;
        [Tooltip("The sound's volume.")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.7f;
        [Tooltip("Optional: a few leaves flying up from the middle.")]
        [SerializeField] private ParticleSystem leafBurst;
        [Tooltip("How many leaves the burst throws each time.")]
        [Min(0)]
        [SerializeField] private int burstLeaves = 6;

        private Mesh[] copies;
        private MeshRenderer[] renderers;
        private int[] totals;
        private int grown;

        /// <summary>How many groups there are (one per seed): 4.</summary>
        public int GroupCount => BowlingLayout.SproutGroups.Length;

        /// <summary>How many groups have grown so far.</summary>
        public int Grown => grown;

        private void Awake()
        {
            Prepare();
            HideAll();
        }

        private void OnDestroy()
        {
            if (copies == null) return;
            foreach (var mesh in copies)
            {
                if (mesh != null) Destroy(mesh);
            }
        }

        /// <summary>Grows the next group of vines out of the dirt (puff, sound, leaves). Does nothing once all have grown.</summary>
        public void GrowNext()
        {
            if (grown >= GroupCount) return;
            Prepare();
            int group = grown++;
            bool canAnimate = isActiveAndEnabled;
            for (int i = 0; i < vines.Length; i++)
            {
                if (vines[i] == null || vines[i].group != group) continue;
                if (canAnimate) StartCoroutine(Reveal(i));
                else SetShare(i, 1f);
            }
            var at = transform.position;
            if (puff != null) EffectPool.Play(puff, at, puffScale);
            if (growSound != null) AudioSource.PlayClipAtPoint(growSound, at + Vector3.up * 0.5f, volume);
            if (leafBurst != null && burstLeaves > 0 && canAnimate) leafBurst.Emit(burstLeaves);
        }

        /// <summary>Shows every vine and leaf at once (for tests).</summary>
        public void ShowAll()
        {
            Prepare();
            StopAllCoroutines();
            for (int i = 0; i < vines.Length; i++) SetShare(i, 1f);
            grown = GroupCount;
        }

        /// <summary>Hides every vine and leaf (done at start).</summary>
        public void HideAll()
        {
            Prepare();
            StopAllCoroutines();
            for (int i = 0; i < vines.Length; i++)
            {
                var vine = vines[i];
                if (vine == null) continue;
                if (renderers[i] != null) renderers[i].enabled = false;
                if (vine.leaves == null) continue;
                foreach (var leaf in vine.leaves)
                {
                    if (leaf != null) leaf.SetActive(false);
                }
            }
            grown = 0;
        }

        // Each vine gets its own copy of its mesh, so drawing part of it never changes the shared asset.
        private void Prepare()
        {
            if (copies != null) return;
            copies = new Mesh[vines.Length];
            renderers = new MeshRenderer[vines.Length];
            totals = new int[vines.Length];
            for (int i = 0; i < vines.Length; i++)
            {
                var filter = vines[i]?.filter;
                if (filter == null || filter.sharedMesh == null) continue;
                copies[i] = Instantiate(filter.sharedMesh);
                copies[i].name = filter.sharedMesh.name + " (growing)";
                // The whole mesh, before any part of it is hidden.
                totals[i] = (int)copies[i].GetIndexCount(0);
                filter.sharedMesh = copies[i];
                renderers[i] = filter.GetComponent<MeshRenderer>();
            }
        }

        // Root to tip with a little overshoot (as the PlayGround's things grow in).
        private IEnumerator Reveal(int index)
        {
            for (float t = 0f; t < growSeconds; t += Time.deltaTime)
            {
                float k = t / growSeconds;
                float share = Mathf.SmoothStep(0.05f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.08f;
                SetShare(index, Mathf.Clamp01(share));
                yield return null;
            }
            SetShare(index, 1f);
        }

        // Draws the vine from its root up to this share of its length; leaves it has reached pop in.
        private void SetShare(int index, float share)
        {
            var vine = vines[index];
            var mesh = copies[index];
            if (vine == null || mesh == null) return;
            var ends = vine.ringIndexEnds;
            if (ends == null || ends.Length == 0) return;
            int ring = Mathf.Clamp(Mathf.FloorToInt(share * (ends.Length - 1) + 0.0001f), 0, ends.Length - 1);
            int count = Mathf.Min(ends[ring], totals[index]);
            if (count > 0) mesh.SetSubMesh(0, new SubMeshDescriptor(0, count));
            if (renderers[index] != null) renderers[index].enabled = count > 0;

            if (vine.leaves == null) return;
            for (int j = 0; j < vine.leaves.Length; j++)
            {
                var leaf = vine.leaves[j];
                if (leaf == null || leaf.activeSelf) continue;
                float at = vine.leafShares != null && j < vine.leafShares.Length ? vine.leafShares[j] : 1f;
                if (share >= at) leaf.SetActive(true);
            }
        }
    }
}
