using Game.Leveling;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Makes a PlayMaker breakable that judges hits by their speed alone (a Damage FSM with collisionForce and a Check
    /// Force state, like the whack pumpkins) judge the player's weapons and throws the way enemies do: as fast as the
    /// weapon moved where it hit (a swing lands with its tip, not its middle), times the weapon's Power (a hammer 2, a
    /// pillow 0.6), with the Weapon Efficiency level-up and a two-handed grip. The FSM's own thresholds stay as they are
    /// (Whack 6: over 12, Whack 7: over 15). Punches are judged by the Fist itself. Goes on the breakable's root, next
    /// to its Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    public class FairHits : MonoBehaviour
    {
        [Tooltip("Only things with this tag count, as in the FSM's Collision Event.")]
        [SerializeField] private string weaponTag = "Weapon";

        private void OnCollisionEnter(Collision collision)
        {
            var body = collision.rigidbody;
            if (body == null || Fist.IsFistBall(body)) return;
            if (!string.IsNullOrEmpty(weaponTag) && !collision.gameObject.CompareTag(weaponTag)) return;

            float speed = collision.relativeVelocity.magnitude;
            for (int i = 0; i < collision.contactCount; i++)
                speed = Mathf.Max(speed, body.GetPointVelocity(collision.GetContact(i).point).magnitude);
            float power = body.TryGetComponent(out WeaponPower weapon) ? weapon.Power * weapon.boost : 1f;
            PlayMakerHits.Judge(gameObject, collision, speed * power * PlayerLevel.WeaponDamageScale);
        }
    }
}
