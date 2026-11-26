using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// On a seed piece's chest (Treasure_Chest_and_Seed_*): now and then it also holds a piece of the Knight's armor or a
    /// legendary weapon (Knight Armor Hider, Chest Chance), which pops out when the chest opens (its Chest_Control FSM
    /// reaches Open_Chest). Put on the chests by Tools > 429 Game > Items > Set Up Knight Armor Hunt.
    /// </summary>
    public class KnightArmorChest : MonoBehaviour
    {
        [Tooltip("The chest's FSM, and the state in which it opens.")]
        [SerializeField] private string controlFsm = "Chest_Control";
        [SerializeField] private string openState = "Open_Chest";

        private PlayMakerFSM control;
        private bool told;
        private bool opened;
        private float nextLook;

        private void OnEnable()
        {
            told = false;
            opened = false;
        }

        private void Update()
        {
            var hider = KnightArmorHider.Instance;
            if (hider == null) return;
            if (!told)
            {
                told = true;
                hider.ChestAppeared(this);
            }
            if (opened) return;
            if (control == null)
            {
                if (Time.time < nextLook) return;
                nextLook = Time.time + 0.5f;
                foreach (var fsm in GetComponentsInChildren<PlayMakerFSM>())
                    if (fsm.FsmName == controlFsm) control = fsm;
                if (control == null) return;
            }
            if (control.ActiveStateName != openState) return;
            opened = true;
            hider.ChestOpened(this);
        }
    }
}
