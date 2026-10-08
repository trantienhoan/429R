using UnityEngine;

namespace Game.Combat
{
    /// <summary>The Throw Assist leaves this object alone: it always flies exactly as thrown. Goes on its root.</summary>
    [DisallowMultipleComponent]
    public class NoThrowAssist : MonoBehaviour, IThrowAssistOptOut
    {
        public bool OptOutOfThrowAssist => true;
    }
}
