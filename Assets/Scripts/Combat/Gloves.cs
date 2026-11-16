using System;
using System.Collections.Generic;
using Game.Leveling;
using Game.Saving;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The player's boxing gloves (user, 2026-11-12: "When we press grip and trigger, we pop up a glove for really
    /// punching"). Every run starts with the Knight, who carries one pair hanging from his boong, a kind picked at random
    /// (Glove Kinds; never the same kind two runs in a row). Touching it (a hand or the body) picks it up (Glove Pickup):
    /// both hands are gloved for the rest of the run, through revives, a Get a Rest and a checkpoint resume (the save
    /// keeps it with the run, see Checkpoints), until the run is over (a game over with no revive left, Re-Dream, beating
    /// the game). A new run brings a new Knight with a new pair; walking past without touching it means no gloves.
    /// With gloves, grip + trigger on an empty hand pops a glove onto it, and that glove punches (Fist). Without gloves,
    /// grip + trigger does nothing (Glove Kinds' Need Gloves). The pair also gives its kind's level-up points for the run
    /// (Boxing: +3 Fist Size, Throwing: +3 Throw Power, Soldier: +3 Weapon Efficiency), counted like eaten candies' points
    /// (Player Level), except that they also count towards what the stat's own points unlock, and each kind unlocks its own
    /// (user, 2026-11-12): Boxing the Jab, Swing and Uppercut at once, Throwing exploding throws, Soldier Impact, while
    /// they're worn (outside special stages).
    /// A save from before the gloves has none: no pair until the next run's Knight (user, 2026-11-12).
    /// </summary>
    public static class Gloves
    {
        /// <summary>The kinds' file, by its name in Resources.</summary>
        public const string KindsAsset = "Glove Kinds";

        private static GloveKinds kinds;
        private static bool looked;

        /// <summary>The kinds of gloves (Assets/Resources/Glove Kinds.asset), or null when the game has none.</summary>
        public static GloveKinds Kinds
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    kinds = Resources.Load<GloveKinds>(KindsAsset);
                }
                return kinds;
            }
        }

        /// <summary>Whether punching takes gloves (there are kinds, and Need Gloves is ticked).</summary>
        public static bool Required => Kinds != null && Kinds.NeedGloves && Kinds.Count > 0;

        /// <summary>The pair the player wears this run, or null.</summary>
        public static GloveKinds.Kind Worn { get; private set; }

        /// <summary>Whether the player wears gloves this run.</summary>
        public static bool Has => Worn != null;

        /// <summary>Whether a fist punches: with gloves on, or when punching doesn't take gloves.</summary>
        public static bool CanPunch => Worn != null || !Required;

        /// <summary>The kind the Knight carries this run (picked when he first shows it), or null.</summary>
        public static GloveKinds.Kind Offered { get; private set; }

        /// <summary>The Id of the kind the Knight carried the run before ("" = none): this run's isn't the same.</summary>
        public static string LastRunId { get; private set; } = "";

        public static string WornId => Worn != null ? Worn.id : "";
        public static string OfferedId => Offered != null ? Offered.id : "";

        /// <summary>The gloves worn changed: put on, taken off (a new run), or read from the save.</summary>
        public static event Action Changed;

        /// <summary>A pair was just picked up (not read from the save): the hands show them for a moment.</summary>
        public static event Action<GloveKinds.Kind> PickedUp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            kinds = null;
            looked = false;
            Worn = null;
            Offered = null;
            LastRunId = "";
            Changed = null;
            PickedUp = null;
        }

        /// <summary>The kind with this Id, or null (none, an unknown Id, no Glove Kinds).</summary>
        public static GloveKinds.Kind Find(string id) => Kinds != null ? Kinds.Find(id) : null;

        /// <summary>The points the gloves worn add to this stat (0 without gloves, or for another stat).</summary>
        public static float PointsFor(Stat stat) => Worn != null && Worn.stat == stat ? Worn.points : 0f;

        /// <summary>
        /// The kind the Knight carries this run: picked at random the first time he asks in a run (never the kind of
        /// the run before, when there's another), then the same one until the run is over. Null without kinds.
        /// </summary>
        public static GloveKinds.Kind OfferForThisRun()
        {
            if (Offered != null) return Offered;
            var all = Kinds != null ? Kinds.All : null;
            if (all == null || all.Count == 0) return null;
            var choices = new List<GloveKinds.Kind>();
            foreach (var kind in all)
            {
                if (kind != null && (kind.left != null || kind.right != null)) choices.Add(kind);
            }
            if (choices.Count > 1) choices.RemoveAll(kind => kind.id == LastRunId);
            if (choices.Count == 0) return null;
            Offered = choices[UnityEngine.Random.Range(0, choices.Count)];
            // Kept with the run, so a save resumed at the very start shows the same pair again.
            Checkpoints.RunChanged();
            return Offered;
        }

        /// <summary>The player picked up a pair (Glove Pickup): both hands are gloved for the rest of the run.</summary>
        public static void PickUp(GloveKinds.Kind kind)
        {
            if (kind == null || kind == Worn) return;
            Offered = kind;
            SetWorn(kind, true);
            PickedUp?.Invoke(kind);
        }

        /// <summary>For tests: takes the gloves off (a new run does it by itself).</summary>
        public static void TakeOff()
        {
            SetWorn(null, false);
        }

        /// <summary>A new run (Checkpoints.StartNewRun): the gloves come off, and the next Knight carries a new pair.</summary>
        public static void NewRun()
        {
            if (Offered != null) LastRunId = Offered.id;
            Offered = null;
            SetWorn(null, false, false);
        }

        /// <summary>Everything forgotten (Reset Progress).</summary>
        public static void Forget()
        {
            Offered = null;
            LastRunId = "";
            SetWorn(null, false, false);
        }

        /// <summary>
        /// The run's gloves as the save kept them (Checkpoints, when the save slot is read): the pair worn ("" = none),
        /// the Knight's pair this run and his pair the run before. Unknown Ids count as none.
        /// </summary>
        public static void Load(string worn, string offered, string lastRun)
        {
            LastRunId = lastRun ?? "";
            var kind = Find(worn);
            Offered = kind ?? Find(offered);
            SetWorn(kind, false, false);
        }

        // Picked Up (a pair just picked up): a raised health cap heals too, and what the pair unlocks is told (Player Level).
        // Save: the run changed, so it's saved.
        private static void SetWorn(GloveKinds.Kind kind, bool pickedUp, bool save = true)
        {
            if (kind == Worn) return;
            Worn = kind;
            PlayerLevel.GlovesChanged(pickedUp);
            Changed?.Invoke();
            if (save) Checkpoints.RunChanged();
        }

        /// <summary>
        /// A copy of a glove prefab to look at only (on the Knight's boong, a flying punch's echo): made under Parent, its
        /// colliders switched off and removed. Null without a prefab.
        /// </summary>
        public static Transform MakeLook(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;
            var look = UnityEngine.Object.Instantiate(prefab, parent, false).transform;
            look.name = prefab.name;
            foreach (var collider in look.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
            }
            return look;
        }

        /// <summary>
        /// The box its look (meshes) takes up in its own space, in its own units (its scale left out), e.g. for how big
        /// a glove is. Empty (size 0) with no mesh.
        /// </summary>
        public static Bounds LookBounds(Transform root)
        {
            var box = new Bounds();
            bool any = false;
            if (root == null) return box;
            var toRoot = root.worldToLocalMatrix;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var toLook = toRoot * renderer.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var at = toLook.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        box = new Bounds(at, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        box.Encapsulate(at);
                    }
                }
            }
            return box;
        }

        /// <summary>
        /// Copies a Box, Sphere, Capsule or (convex) Mesh collider onto a new child of Into, placed as it is in Root's
        /// space (so Into stands for Root): a glove's punch shape put onto the fist's ball. Not a trigger. Null for another
        /// kind of collider.
        /// </summary>
        public static Collider CopyCollider(Collider source, Transform into, Transform root)
        {
            if (source == null || into == null || root == null) return null;
            var part = new GameObject(source.name).transform;
            part.SetParent(into, false);
            var from = source.transform;
            part.localPosition = root.InverseTransformPoint(from.position);
            part.localRotation = Quaternion.Inverse(root.rotation) * from.rotation;
            var rootScale = root.lossyScale;
            var ownScale = from.lossyScale;
            part.localScale = new Vector3(Ratio(ownScale.x, rootScale.x), Ratio(ownScale.y, rootScale.y), Ratio(ownScale.z, rootScale.z));
            part.gameObject.layer = into.gameObject.layer;
            Collider made;
            switch (source)
            {
                case SphereCollider sphere:
                {
                    var copy = part.gameObject.AddComponent<SphereCollider>();
                    copy.center = sphere.center;
                    copy.radius = sphere.radius;
                    made = copy;
                    break;
                }
                case CapsuleCollider capsule:
                {
                    var copy = part.gameObject.AddComponent<CapsuleCollider>();
                    copy.center = capsule.center;
                    copy.radius = capsule.radius;
                    copy.height = capsule.height;
                    copy.direction = capsule.direction;
                    made = copy;
                    break;
                }
                case BoxCollider box:
                {
                    var copy = part.gameObject.AddComponent<BoxCollider>();
                    copy.center = box.center;
                    copy.size = box.size;
                    made = copy;
                    break;
                }
                case MeshCollider mesh when mesh.sharedMesh != null:
                {
                    var copy = part.gameObject.AddComponent<MeshCollider>();
                    copy.sharedMesh = mesh.sharedMesh;
                    // A moving body's mesh collider must be convex.
                    copy.convex = true;
                    made = copy;
                    break;
                }
                default:
                    UnityEngine.Object.Destroy(part.gameObject);
                    return null;
            }
            made.sharedMaterial = source.sharedMaterial;
            made.isTrigger = false;
            return made;
        }

        /// <summary>
        /// How thick a punch shape is, as a ball's radius, in Into's units: its widest sphere or capsule (or its box's
        /// half its smallest side), for things that treat the fist as a ball (the fist bump's gap, the flying punch's
        /// look). 0 with none.
        /// </summary>
        public static float ShapeRadius(IReadOnlyList<Collider> shape, Transform into)
        {
            float widest = 0f;
            if (shape == null || into == null) return 0f;
            foreach (var collider in shape)
            {
                if (collider == null) continue;
                var scale = collider.transform.localScale;
                float most = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                float r = collider switch
                {
                    SphereCollider sphere => sphere.radius,
                    CapsuleCollider capsule => capsule.radius,
                    BoxCollider box => 0.5f * Mathf.Min(box.size.x, Mathf.Min(box.size.y, box.size.z)),
                    MeshCollider mesh when mesh.sharedMesh != null => 0.5f * Mathf.Min(mesh.sharedMesh.bounds.size.x, Mathf.Min(mesh.sharedMesh.bounds.size.y, mesh.sharedMesh.bounds.size.z)),
                    _ => 0f,
                };
                widest = Mathf.Max(widest, r * most);
            }
            return widest;
        }

        private static float Ratio(float own, float of) => Mathf.Abs(of) > 0.0001f ? own / of : own;
    }
}
