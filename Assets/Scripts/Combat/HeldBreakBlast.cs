using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Combat
{
    /// <summary>
    /// A pumpkin broken while the player holds it goes off in their hands like a mini boss's special move (user,
    /// 2026-10-30: "when they are grabbed and drop health = 0, make a good explosion to punish Player for holding them, do
    /// a really good damage like a special move of mini boss"): a big blast that also hurts and throws the bugs around it,
    /// and the player takes Player Event (PumpkinBossHit: 70-90, what the mini bosses' specials do), is thrown back and
    /// sees the red flash. Broken any other way (punched on the floor, thrown) it breaks as before. Its Health FSM still
    /// breaks it (crack, drops); this only adds the blast. Goes on the runner pumpkins (Pumpkin_0, 1, 2, 4, 6 and their
    /// Chaos copies) and Pumpkin_00: Tools > 429 Game > Combat > Held Pumpkins Explode adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class HeldBreakBlast : MonoBehaviour
    {
        [Header("The player")]
        [Tooltip("Sent to the player's PlayerHealth FSM: PumpkinBossHit (70-90, the mini bosses' specials).")]
        [SerializeField] private string playerEvent = "PumpkinBossHit";
        [Tooltip("The player is thrown this high (x) and this far back (y), in metres (Toughness cuts it, never below " +
                 "half).")]
        [SerializeField] private Vector2 launch = new(1.2f, 3f);
        [Tooltip("Still counts as held this many seconds after it left the hand (the break can let go of it first).")]
        [Min(0f)]
        [SerializeField] private float heldGrace = 0.15f;

        [Header("Blast")]
        [Tooltip("How far it reaches, in metres: bugs and things in reach are hurt and pushed (the player by Player " +
                 "Event above).")]
        [Min(0.1f)]
        [SerializeField] private float radius = 2.5f;
        [Min(0f)]
        [SerializeField] private float damage = 3f;
        [Min(0f)]
        [SerializeField] private float push = 6f;
        [Tooltip("Its effects (e.g. the grenades': Flash_round_yellow, Hit_Blast, Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject[] effects = System.Array.Empty<GameObject>();
        [Min(0.01f)]
        [SerializeField] private float effectSize = 1.2f;
        [SerializeField] private AudioClip sound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Header("PlayMaker")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        private const string PlayerHealthFsm = "PlayerHealth";

        private XRGrabInteractable grab;
        private Rigidbody body;
        private FsmFloat health;
        private float heldAt = -100f;
        private bool fired;

        private void Awake()
        {
            TryGetComponent(out grab);
            TryGetComponent(out body);
        }

        // Runs before the FSMs, so it sees the 0 before the Health FSM breaks it.
        private void Update() => Check();

        // The break can also switch it off or destroy it first: caught here.
        private void OnDisable() => Check();

        private void Check()
        {
            if (fired || grab == null) return;
            if (grab.isSelected) heldAt = Time.time;
            if (health == null) health = FindHealth();
            if (health == null || health.Value > 0f) return;
            fired = true;
            if (Time.time - heldAt <= heldGrace && gameObject.scene.isLoaded) GoOff();
        }

        private FsmFloat FindHealth()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == healthFsm) return fsm.FsmVariables.FindFsmFloat(healthVariable);
            }
            return null;
        }

        private void GoOff()
        {
            var center = body != null ? body.worldCenterOfMass : transform.position;
            // The bugs and things around (the player isn't sent anything here: they get the special's hit below).
            Game.Weapons.Blast.Explode(center, radius, damage, push, body, effects, sound, volume, null, false, effectSize);
            SendToPlayer(playerEvent);
            DamageVignette.HitFrom(center);
            PlayerLaunch.Launch(center, launch.x, launch.y, true, true, 0.5f);
        }

        private static void SendToPlayer(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            var player = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            if (player == null || player.Value == null) return;
            foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == PlayerHealthFsm) fsm.SendEvent(eventName);
            }
        }
    }
}
