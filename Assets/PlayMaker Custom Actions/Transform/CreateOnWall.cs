using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.GameObject)]
    [ActionTarget(typeof(GameObject), "gameObject", true)]
    [Tooltip("Creates a wall-hung prop (a picture, a banner) on a spot on the wall, turned so its front (the painting) " +
             "faces the way the spot's props face, however its model was made: Banner_Hang and Picture_Hang_1, 2, 3, " +
             "8, 9 face Left (away from their red X arrow), Picture_Hang_4-7 face Forward (their blue Z arrow), and " +
             "they all come out the same way round. Its back goes on the spot. A prop's front is told from its size: " +
             "its thin side, on the side it sticks out from its back. Like Create Object otherwise.")]
    public class CreateOnWall : FsmStateAction
    {
        public enum Side
        {
            Forward,
            Back,
            Right,
            Left,
        }

        [RequiredField]
        [Tooltip("The prop to create. Usually a Prefab.")]
        public FsmGameObject gameObject;

        [Tooltip("Optional Parent (e.g. CurrentlySpawnedStuffs, so it goes with the room).")]
        public FsmGameObject parent;

        [RequiredField]
        [Tooltip("The spot on the wall: the prop's back goes on it.")]
        public FsmGameObject spawnPoint;

        [Tooltip("Which way the spot's props face, by the spot's arrows in the Scene view: Left = the side opposite " +
                 "its red X arrow, Forward = its blue Z arrow, Right = its red X arrow, Back = opposite the blue arrow.")]
        public Side spotFront;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optionally store the created object.")]
        public FsmGameObject storeObject;

        public override void Reset()
        {
            gameObject = null;
            parent = null;
            spawnPoint = null;
            spotFront = Side.Left;
            storeObject = null;
        }

        public override void OnEnter()
        {
            var prop = gameObject.Value;
            var spot = spawnPoint.Value;
            if (prop != null && spot != null)
            {
                var turn = Quaternion.Euler(0f, TurnFor(prop, Direction(spotFront)), 0f);
                var made = Object.Instantiate(prop, spot.transform.position, spot.transform.rotation * turn);
                if (storeObject != null) storeObject.Value = made;
                // Keeps its place on the wall, like Create Object's parent.
                if (parent != null && parent.Value != null) made.transform.SetParent(parent.Value.transform, true);
            }
            Finish();
        }

        /// <summary>
        /// How far round its up axis a wall-hung prop is turned (degrees) so its own front lands on a spot's front
        /// (both in their own space); 0 when its front can't be told.
        /// </summary>
        public static float TurnFor(GameObject prop, Vector3 spotFront)
        {
            var front = FrontOf(prop);
            return front == Vector3.zero ? 0f : Vector3.SignedAngle(front, spotFront, Vector3.up);
        }

        /// <summary>
        /// A wall-hung prop's front in its own space: its thin side (along X or Z), the way it sticks out from its
        /// back (where its pivot is). From its model's size, else its box collider's; zero when it can't be told.
        /// </summary>
        public static Vector3 FrontOf(GameObject prop)
        {
            Bounds size;
            if (prop.TryGetComponent(out MeshFilter model) && model.sharedMesh != null) size = model.sharedMesh.bounds;
            else if (prop.TryGetComponent(out BoxCollider box)) size = new Bounds(box.center, box.size);
            else return Vector3.zero;
            bool alongX = size.size.x < size.size.z;
            float off = alongX ? size.center.x : size.center.z;
            if (Mathf.Abs(off) < 0.002f) return Vector3.zero;
            return alongX ? new Vector3(Mathf.Sign(off), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(off));
        }

        public static Vector3 Direction(Side side)
        {
            switch (side)
            {
                case Side.Back: return Vector3.back;
                case Side.Right: return Vector3.right;
                case Side.Left: return Vector3.left;
                default: return Vector3.forward;
            }
        }

#if UNITY_EDITOR
        public override string AutoName()
        {
            return "Create On Wall: " + ActionHelpers.GetValueLabel(gameObject);
        }

        // In the Scene view: an arrow from the spot the way its props will face.
        public override void OnDrawActionGizmos()
        {
            var spot = spawnPoint != null ? spawnPoint.Value : null;
            if (spot == null) return;
            var at = spot.transform.position;
            var front = spot.transform.TransformDirection(Direction(spotFront));
            var side = Vector3.Cross(Vector3.up, front).normalized;
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            Gizmos.DrawLine(at, at + front * 0.4f);
            Gizmos.DrawLine(at + front * 0.4f, at + front * 0.3f + side * 0.06f);
            Gizmos.DrawLine(at + front * 0.4f, at + front * 0.3f - side * 0.06f);
            // A picture's outline on the wall.
            var a = at + (side + Vector3.up) * 0.3f;
            var b = at + (-side + Vector3.up) * 0.3f;
            var c = at + (-side - Vector3.up) * 0.3f;
            var d = at + (side - Vector3.up) * 0.3f;
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        }
#endif
    }
}
