using System.Collections.Generic;
using Game.Combat;
using Game.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Leveling
{
    /// <summary>
    /// A short unlock notice (user, 2026-11-12: "Those short notices just fly out in front of the Player, don't stop the
    /// game, and can be broken by the Player's hit"): NEW PUNCH! / JAB, SWING, UPPERCUT, ..., NEW POWER! / EXPLOSIVE
    /// THROWS, BOMBER MAN, IMPACT, WEAPON MASTER, what a pair of gloves unlocks, and the LEVEL UP! notices too ("make the
    /// level-up notices fly out the same way"). A copy of the Level Up Notice's panel
    /// (big title, a few words) pops out from the player's chest and eases out to about Distance ahead at eye height,
    /// facing them and floating a little; the game goes on meanwhile. Up to Max Shown at once, stacked one above the
    /// other. The player breaks it with a hit: a gloved punch or a flying punch, a weapon in the hand, a thing thrown at
    /// it (Held Things' rule), or a squeezed hand going through it (Punch To Shatter); it shatters into shards with the
    /// glass sound. Unhit, it fades away after Seconds. It's a trigger on Ignore Raycast: nothing bumps into it, enemies
    /// don't notice it and it never blocks walking or teleporting. The Level Up Notice makes them (Show).
    /// </summary>
    public class UnlockCard : MonoBehaviour
    {
        /// <summary>The most cards shown at once (stacked).</summary>
        public const int MaxShown = 3;

        // Flying out: from this far below and in front of the eyes, in this many seconds, to Distance ahead at this height.
        private const float StartDown = 0.35f;
        private const float StartAhead = 0.3f;
        private const float FlySeconds = 0.45f;
        private const float EyeHeight = -0.05f;
        // Stacked: each card this much higher than the one below, in metres.
        private const float SlotGap = 0.3f;
        // Floating: up and down this far, this many times a second.
        private const float Bob = 0.015f;
        private const float BobSpeed = 0.6f;
        // Fading in and away, in seconds.
        private const float FadeSeconds = 0.5f;
        // A hit this fast (m/s) or faster breaks it (a weapon held still against it too: a kinematic one can't be measured).
        private const float MinHitSpeed = 1.2f;
        // How thick its hit box is, in metres.
        private const float Depth = 0.12f;

        private static readonly List<UnlockCard> shown = new();

        private CanvasGroup group;
        private PunchToShatter shatter;
        private Transform head;
        private Vector3 from;
        private Vector3 to;
        private int slot;
        private float age;
        private float seconds;
        private float distance;
        // The card's full size (the panel's world scale x Scale).
        private Vector3 baseScale = Vector3.one * 0.001f;
        private bool broken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => shown.Clear();

        /// <summary>Every card up fades away now (the Level Up menu opened).</summary>
        public static void FadeAll()
        {
            foreach (var card in shown)
            {
                if (card != null && !card.broken) card.seconds = Mathf.Min(card.seconds, card.age + FadeSeconds);
            }
        }

        /// <summary>Whether another card can show now (fewer than Max Shown up).</summary>
        public static bool HasRoom
        {
            get
            {
                shown.RemoveAll(card => card == null);
                return shown.Count < MaxShown;
            }
        }

        /// <summary>
        /// Shows a card made from the notice's panel (a world-space canvas, its Title and Body texts; Hide: hidden on the
        /// card, e.g. "Pull a trigger to continue"): this title and text, fading away after this many seconds, this far
        /// ahead, this big (1 = the panel's own size). Null without a panel or a head.
        /// </summary>
        public static UnlockCard Show(CanvasGroup panel, TMP_Text titleText, TMP_Text bodyText, Behaviour hide, string title, string body,
            float seconds, float distance, float scale)
        {
            if (panel == null || Camera.main == null) return null;
            var copy = Instantiate(panel.gameObject);
            copy.name = "Unlock Card";
            var card = copy.AddComponent<UnlockCard>();
            card.Set(panel.transform, copy.transform, titleText, title);
            card.Set(panel.transform, copy.transform, bodyText, body);
            if (hide != null)
            {
                var hidden = Find(panel.transform, copy.transform, hide.transform);
                if (hidden != null) hidden.gameObject.SetActive(false);
            }
            card.baseScale = panel.transform.lossyScale * Mathf.Max(0.1f, scale);
            card.Begin(seconds, distance);
            return card;
        }

        // The copy's text where the panel has this one (found by its path under the panel).
        private void Set(Transform original, Transform copy, TMP_Text text, string value)
        {
            if (text == null) return;
            var found = Find(original, copy, text.transform);
            if (found != null && found.TryGetComponent(out TMP_Text copied)) copied.text = value;
        }

        private static Transform Find(Transform original, Transform copy, Transform part)
        {
            if (part == original) return copy;
            var path = new List<int>();
            for (var t = part; t != null && t != original; t = t.parent) path.Insert(0, t.GetSiblingIndex());
            var at = copy;
            foreach (int index in path)
            {
                if (index >= at.childCount) return null;
                at = at.GetChild(index);
            }
            return at;
        }

        private void Begin(float life, float ahead)
        {
            seconds = Mathf.Max(1f, life);
            distance = ahead;
            head = Camera.main.transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            // Not a menu: pointers go through it.
            foreach (var raycaster in GetComponents<UnityEngine.EventSystems.BaseRaycaster>()) raycaster.enabled = false;
            gameObject.SetActive(true);

            // The lowest free place in the stack.
            shown.RemoveAll(card => card == null);
            for (slot = 0; slot < MaxShown; slot++)
            {
                bool taken = false;
                foreach (var other in shown) taken |= other.slot == slot;
                if (!taken) break;
            }
            shown.Add(this);

            var yaw = Yaw();
            from = head.position + yaw * new Vector3(0f, -StartDown, StartAhead);
            to = head.position + yaw * new Vector3(0f, EyeHeight + slot * SlotGap, distance);
            transform.position = from;
            Face();
            transform.localScale = baseScale * 0.2f;

            // Its hit box (a trigger on Ignore Raycast; a kinematic body so hits are noticed), the size of the panel.
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var box = new GameObject("Hit Box").AddComponent<BoxCollider>();
            box.gameObject.layer = 2;
            box.transform.SetParent(transform, false);
            box.isTrigger = true;
            var rect = (RectTransform)transform;
            box.center = rect.rect.center;
            box.size = new Vector3(rect.rect.width, rect.rect.height, Depth / Mathf.Max(0.000001f, Mathf.Abs(baseScale.z)));

            // A squeezed hand going through it breaks it too (from when it has flown out).
            shatter = PunchToShatter.Add(rect, Shattered, () => !broken && age >= FlySeconds && group.alpha >= 0.5f);
        }

        // Facing the way the head looks, turned to the player (a world-space canvas reads with its forward away from them).
        private Quaternion Yaw()
        {
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void Face()
        {
            var away = transform.position - head.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        private void Update()
        {
            if (head == null || broken) return;
            // The pause menu sits in front: the card hides and waits.
            if (PauseMenu.IsPaused)
            {
                group.alpha = 0f;
                return;
            }
            age += Time.deltaTime;
            float flown = Mathf.Clamp01(age / FlySeconds);
            float ease = 1f - (1f - flown) * (1f - flown) * (1f - flown);
            var at = Vector3.LerpUnclamped(from, to, ease);
            if (flown >= 1f) at.y += Mathf.Sin((age - FlySeconds) * BobSpeed * 2f * Mathf.PI) * Bob;
            transform.position = at;
            Face();
            transform.localScale = baseScale * Mathf.Lerp(0.2f, 1f, ease);
            float left = seconds - age;
            group.alpha = Mathf.Min(Mathf.Clamp01(age / FadeSeconds), Mathf.Clamp01(left / FadeSeconds));
            if (left <= 0f) Destroy(gameObject);
        }

        // A hit from the player: a fist ball or a flying punch, or a thing they hold or just threw (Held Things), fast
        // enough. Enemies and anything else go through it.
        private void OnTriggerEnter(Collider other)
        {
            if (broken || age < FlySeconds || other == null) return;
            var body = other.attachedRigidbody;
            if (body == null) return;
            float speed;
            if (Fist.IsFistBall(body))
            {
                var owner = Fist.OwnerOf(body);
                speed = Fist.IsFlyingFist(body) || owner == null ? body.linearVelocity.magnitude : owner.HandVelocity.magnitude;
            }
            else if (body.TryGetComponent(out XRGrabInteractable grab) && HeldThings.InUse(grab))
            {
                speed = body.isKinematic ? MinHitSpeed : body.linearVelocity.magnitude;
            }
            else return;
            if (speed < MinHitSpeed) return;
            var point = other.ClosestPoint(transform.position);
            if (shatter != null) shatter.ShatterAt(point, body.linearVelocity);
            else Shattered();
        }

        private void Shattered()
        {
            if (broken) return;
            broken = true;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            shown.Remove(this);
        }
    }
}
