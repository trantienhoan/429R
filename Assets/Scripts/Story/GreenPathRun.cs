using System.Collections;
using System.Collections.Generic;
using Game.Locomotion;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// One green light path from start to end (the Story Green Path action makes it; it runs on its own object):
    /// - Exit Zone (a room's way on, user 2026-11-27): the way out of the room opens (the wall or door piece at the
    ///   exit spot, or the rooftop's battlements, sink into the floor), the green path shows from the player's feet (the
    ///   seed piece just taken) to that doorway, and the hallway reaches out of it over the drop: a dark walkway with
    ///   green light down both sides, safe to walk (no Fall Edge counts over it), long enough that the whole walk is at
    ///   least Min Walk. The teleport zone is at its far end. Standing in the zone: everything goes dark but the path,
    ///   a poof of the seed's green smoke, the teleport tube rises and runs round them, a flash, and they stand in the
    ///   room's middle inside the tube; the hallway goes, the wall rises again, the tube sinks away, the light comes back
    ///   and it is Done (the room goes and the next one comes, as before).
    /// - Lead (the start of the game): the path shows from the player's feet up to someone (the Fairy) and stays until
    ///   the state ends.
    /// Off the path in the room for a while (Reroute From / After in the settings), it joins them back to it. Without
    /// the looks (Set Up Green Path not run) an Exit Zone run is Done at once, so the game goes on as it did.
    /// </summary>
    public class GreenPathRun : MonoBehaviour
    {
        public enum Kind
        {
            ExitZone,
            Lead,
        }

        /// <summary>The run going now, if any (for tests).</summary>
        public static GreenPathRun Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        /// <summary>The teleport is over (the player is in the middle, the light coming back).</summary>
        public bool Done { get; private set; }

        /// <summary>The player is in the zone and the teleport is under way.</summary>
        public bool Teleporting { get; private set; }

        /// <summary>The path showing (null before it shows).</summary>
        public GreenPath Path => path;

        /// <summary>The hallway out of the room (null before it shows, or without one).</summary>
        public GreenHallway Hallway => hall;

        /// <summary>The tube (while it runs).</summary>
        public TeleportTube Tube => tube;

        /// <summary>How many times it joined the player back to the path.</summary>
        public int Reroutes { get; private set; }

        /// <summary>The wall pieces opened for the doorway, and the models cut open (for tests).</summary>
        public int OpenedPieces => opened.Count + swapped.Count + hidden.Count;

        /// <summary>The whole walk: the path in the room plus the hallway (metres).</summary>
        public float Walk => (path != null ? path.Length : 0f) + (hall != null ? hall.Length : 0f);

        private Kind kind;
        private Vector3 goal, centre, outward;
        private Transform target;
        private GreenPathSettings settings;
        private GreenPath path;
        private GreenHallway hall;
        private TeleportTube tube;
        private XROrigin origin;
        private HeadsetFade fader;
        private Color fadeColourWas;
        private float showAt, offSince = -1f, nextCheck;
        private bool faded, stopped;

        private struct Piece
        {
            public Transform transform;
            public Vector3 local;
            public Vector3 drop;
        }

        private readonly List<Piece> opened = new List<Piece>();
        // Models swapped for their cut copy (the rooftop) and the wall colliders switched off with them.
        private readonly List<(Component renderer, Mesh whole)> swapped = new List<(Component, Mesh)>();
        private readonly List<Collider> shut = new List<Collider>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private float openShare;
        private Coroutine sliding;

        /// <summary>Starts a run. Goal: the exit spot, on the room's wall (Exit Zone), or who to lead to (Lead; Target,
        /// when set, wins and is followed). Centre: where the zone sends the player (the room's middle, on its floor).</summary>
        public static GreenPathRun Begin(Kind kind, Vector3 goal, Vector3 centre, Transform target)
        {
            var go = new GameObject(kind == Kind.Lead ? "Green_Path_Run (lead)" : "Green_Path_Run");
            var run = go.AddComponent<GreenPathRun>();
            run.kind = kind;
            run.goal = goal;
            run.centre = centre;
            run.target = target;
            run.settings = GreenPathSettings.Instance;
            run.showAt = Time.time + run.settings.ShowAfter;
            Current = run;
            if (kind == Kind.ExitZone && (run.settings.PathPrefab == null || run.settings.TubePrefab == null))
            {
                Debug.LogWarning("[Green Path] No looks (Tools > 429 Game > Story > Set Up Green Path): the room goes on at once.");
                run.Done = true;
            }
            return run;
        }

        /// <summary>The state was left before the end: everything goes away (the dark, the tube, the path, the hallway)
        /// and the doorway closes at once.</summary>
        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            StopAllCoroutines();
            if (path != null) path.Leave(0.5f);
            if (hall != null) hall.Leave(0.3f);
            if (tube != null) tube.End(0.3f);
            if (Teleporting) GreenPathDarkness.End(0.4f);
            if (faded && fader != null)
            {
                fader.FadeIn(0.2f);
                fader.Color = fadeColourWas;
            }
            CloseNow();
            if (Current == this) Current = null;
            Destroy(gameObject, 1f);
        }

        /// <summary>For tests: puts the player in the zone (their head over its middle).</summary>
        public bool TestStepIntoZone()
        {
            if (path == null || !path.HasZone || origin == null) return false;
            MovePlayer(path.ZoneAt);
            return true;
        }

        /// <summary>For the play-through bot: where to walk next (the doorway, then the zone).</summary>
        public Vector3 NextGoal(Vector3 head)
        {
            if (path == null) return head;
            if (hall == null || hall.Holds(head) || Flat(head - path.End) < 0.6f) return path.ZoneAt;
            return path.End;
        }

        private void OnDestroy()
        {
            CloseNow();
            if (Current == this) Current = null;
        }

        private void Update()
        {
            if (stopped || Done || Teleporting) return;
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null) return;
            if (path == null)
            {
                if (Time.time < showAt) return;
                if (kind == Kind.ExitZone) StartExit();
                else path = GreenPath.Make(settings, Feet(), Goal(), false, settings.LeadStopShort, centre, 0f);
                if (path != null) GreenPathDarkness.Keep(path.transform);
                return;
            }
            if (hall != null) path.ZoneHeld = !hall.Formed;
            var head = origin.Camera.transform.position;
            if (kind == Kind.ExitZone && path.Drawn && (hall == null || hall.Formed) && Flat(head - path.ZoneAt) <= settings.ZoneRadius)
            {
                StartCoroutine(Teleport());
                return;
            }
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            // Out on the hallway (or by the doorway): not off the way.
            if (hall != null && hall.Holds(new Vector3(head.x, hall.transform.position.y, head.z)))
            {
                offSince = -1f;
                return;
            }
            if (path.DistanceOff(head) <= settings.RerouteFrom)
            {
                offSince = -1f;
                return;
            }
            if (offSince < 0f) offSince = Time.time;
            if (Time.time - offSince < settings.RerouteAfter) return;
            path.Rebuild(Feet(), Goal(), StopShort());
            offSince = -1f;
            Reroutes++;
        }

        // The way out: the doorway opens, the path to it shows, the hallway reaches out of it with the zone at its end.
        private void StartExit()
        {
            var feet = Feet();
            outward = AxisOut(goal, centre);
            // In front of the doorway, inside the room, on its floor.
            var inside = goal - outward * 0.8f;
            inside.y = GreenPath.FloorHeight(inside, feet.y);
            path = GreenPath.Make(settings, feet, inside, false, 0f, centre, 0f);
            if (path == null)
            {
                Done = true;
                return;
            }
            float walkLeft = settings.MinWalk - path.Length;
            float length = Mathf.Clamp(walkLeft + 1f, settings.HallwayMin, settings.HallwayMax);
            var door = new Vector3(goal.x, inside.y, goal.z);
            OpenDoorway(door);
            hall = GreenHallway.Make(settings.HallwayPrefab, door - outward * 0.35f, outward, length + 0.35f, settings.Colour);
            if (hall != null)
            {
                GreenPathDarkness.Keep(hall.transform);
                path.PlaceZoneAt(hall.PointAt(hall.Length - 1f));
                path.ZoneHeld = true;
            }
            else path.PlaceZoneAt(path.End);
        }

        private IEnumerator Teleport()
        {
            Teleporting = true;
            path.Charge = 1f;
            GreenPathDarkness.Begin(settings.DarkSeconds);
            yield return new WaitForSeconds(settings.DarkSeconds);

            // Poof, and the tube rises round them.
            var feet = Feet();
            StorySeed.GreenPoof(feet + Vector3.up * 0.25f, settings.PoofSize);
            PlayAt(settings.PoofSound, settings.PoofVolume, feet + Vector3.up * 0.4f);
            tube = TeleportTube.Make(settings, origin.Camera.transform, origin.transform);
            if (tube != null) GreenPathDarkness.Keep(tube.transform);
            yield return new WaitForSeconds(settings.TubeSeconds);

            // A green flash, and they stand in the middle (the tube goes with them); the hallway goes, the wall rises.
            fader = HeadsetFade.Find();
            if (fader != null)
            {
                fadeColourWas = fader.Color;
                fader.Color = settings.JumpColour;
                fader.FadeOut(settings.JumpFade);
                faded = true;
            }
            yield return new WaitForSeconds(settings.JumpFade + 0.02f);
            MovePlayer(centre);
            PlayAt(settings.JumpSound, settings.JumpVolume, origin.Camera.transform.position);
            if (path != null) path.Leave(0.6f);
            if (hall != null) hall.Leave(0.6f);
            Unswap();
            if (opened.Count > 0) Slide(false, 0.9f);
            if (fader != null) fader.FadeIn(settings.JumpFade * 1.5f);
            yield return new WaitForSeconds(settings.JumpFade * 1.5f + 0.05f);
            if (fader != null) fader.Color = fadeColourWas;
            faded = false;
            yield return new WaitForSeconds(settings.AfterJump);

            // It sinks away and the light comes back.
            if (tube != null) tube.End(0.5f);
            GreenPathDarkness.End(settings.LightBack);
            yield return new WaitForSeconds(0.35f);
            Done = true;
            if (Current == this) Current = null;
            Destroy(gameObject, 3f);
        }

        // ---------- the doorway ----------

        // The way out from the room's middle through the exit spot: along whichever of the world's axes is nearest (the
        // rooms' walls run along them).
        private static Vector3 AxisOut(Vector3 spot, Vector3 middle)
        {
            var d = spot - middle;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return Vector3.forward;
            return Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
        }

        // The wall where the hallway goes out opens: every piece of the room's kit (a wall, a door, a window: what stands
        // under its set) or the rooftop's battlements in a 1.2 m wide doorway there sinks into the floor (and rises again
        // after the teleport, or at once if the run stops).
        private void OpenDoorway(Vector3 door)
        {
            var across = new Vector3(Mathf.Abs(outward.z), 0f, Mathf.Abs(outward.x));
            var size = new Vector3(Mathf.Abs(outward.x) * 1.2f + across.x * 1.2f, 2f, Mathf.Abs(outward.z) * 1.2f + across.z * 1.2f);
            var doorway = new Bounds(door + Vector3.up * 1.2f, size);
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (!r.bounds.Intersects(doorway)) continue;
                // A model with a cut copy (the rooftop's walls, its battlements): the copy goes in, the wall's colliders off.
                if (Swap(r, doorway)) continue;
                var piece = WallPiece(r.transform);
                if (piece == null || opened.Exists(p => p.transform == piece)) continue;
                float depth = Height(piece) + 0.1f;
                var drop = piece.parent != null ? piece.parent.InverseTransformVector(Vector3.down * depth) : Vector3.down * depth;
                opened.Add(new Piece { transform = piece, local = piece.localPosition, drop = drop });
            }
            if (opened.Count > 0) Slide(true, 0.8f);
            if (swapped.Count > 0) Physics.SyncTransforms();
        }

        private bool Swap(Renderer r, Bounds doorway)
        {
            Mesh whole = null;
            MeshFilter filter = null;
            var skin = r as SkinnedMeshRenderer;
            if (skin != null) whole = skin.sharedMesh;
            else if (r.TryGetComponent(out filter)) whole = filter.sharedMesh;
            var cut = settings.OpenedFor(whole);
            if (cut == null)
            {
                // The rooftop's battlements without their cut copy (Set Up Green Path not run since): they can't sink
                // (the rooftop's Animator holds them), so they hide while the hallway is there.
                if (!r.name.EndsWith("Battlements")) return false;
                r.enabled = false;
                hidden.Add(r);
                ShutColliders(r, doorway);
                return true;
            }
            if (skin != null) skin.sharedMesh = cut;
            else filter.sharedMesh = cut;
            swapped.Add((skin != null ? skin : filter, whole));
            ShutColliders(r, doorway);
            return true;
        }

        private void ShutColliders(Renderer r, Bounds doorway)
        {
            foreach (var c in r.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled || !c.bounds.Intersects(doorway)) continue;
                c.enabled = false;
                shut.Add(c);
            }
        }

        // The piece of the room that stands in the doorway: the child of the room set (Mini, Big, Living, Play) that the
        // renderer is part of, or the rooftop's battlements when they have no cut copy. Null for anything else
        // (furniture, the floor, props).
        private Transform WallPiece(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name.EndsWith("Battlements")) return null;
                var parent = p.parent;
                if (parent == null) break;
                string n = parent.name;
                if ((n == "Mini" || n == "Big" || n == "Living" || n == "Play") && parent.parent != null && parent.parent.name.Contains("_Kit_Base"))
                    return p;
            }
            return null;
        }

        private static float Height(Transform piece)
        {
            bool any = false;
            var box = new Bounds();
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any)
                {
                    box = r.bounds;
                    any = true;
                }
                else box.Encapsulate(r.bounds);
            }
            return any ? box.size.y : 3f;
        }

        private void Slide(bool open, float seconds)
        {
            if (sliding != null) StopCoroutine(sliding);
            sliding = StartCoroutine(Sliding(open ? 1f : 0f, seconds));
        }

        private IEnumerator Sliding(float to, float seconds)
        {
            while (!Mathf.Approximately(openShare, to))
            {
                openShare = Mathf.MoveTowards(openShare, to, Time.deltaTime / Mathf.Max(0.05f, seconds));
                Place(openShare);
                yield return null;
            }
            sliding = null;
        }

        private void Place(float share)
        {
            float eased = share * share * (3f - 2f * share);
            foreach (var p in opened)
                if (p.transform != null) p.transform.localPosition = p.local + p.drop * eased;
            Physics.SyncTransforms();
        }

        // Every piece back where it stood, at once.
        private void CloseNow()
        {
            Unswap();
            if (opened.Count == 0) return;
            openShare = 0f;
            Place(0f);
            opened.Clear();
        }

        // The cut models get their whole mesh back and the wall's colliders come on again.
        private void Unswap()
        {
            foreach (var (renderer, whole) in swapped)
            {
                if (renderer is SkinnedMeshRenderer skin && skin != null) skin.sharedMesh = whole;
                else if (renderer is MeshFilter filter && filter != null) filter.sharedMesh = whole;
            }
            swapped.Clear();
            foreach (var r in hidden)
                if (r != null) r.enabled = true;
            hidden.Clear();
            foreach (var c in shut)
                if (c != null) c.enabled = true;
            shut.Clear();
        }

        // ---------- the player ----------

        // Moves the player so their head is over the spot and their floor on its height (as Teleport Player does).
        private void MovePlayer(Vector3 spot)
        {
            var rig = origin.transform;
            var head = origin.Camera.transform;
            if (rig.TryGetComponent(out Game.Combat.PlayerLaunch launch)) launch.Stop();
            var offset = head.position - rig.position;
            rig.position = new Vector3(spot.x - offset.x, spot.y, spot.z - offset.z);
            Physics.SyncTransforms();
        }

        private Vector3 Feet()
        {
            var head = origin.Camera.transform.position;
            return new Vector3(head.x, origin.transform.position.y, head.z);
        }

        private Vector3 Goal() => target != null ? target.position : goal;

        private float StopShort() => kind == Kind.Lead ? settings.LeadStopShort : 0f;

        private static void PlayAt(AudioClip clip, float volume, Vector3 at)
        {
            if (clip == null) return;
            var go = new GameObject("Green_Path_Sound");
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 0.5f;
            source.minDistance = 2f;
            source.maxDistance = 20f;
            source.Play();
            Destroy(go, clip.length + 0.2f);
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
