using UnityEngine;

namespace Game.Steam
{
    /// <summary>
    /// The Steam demo's settings: Assets/Resources/Demo Settings.asset (Tools > 429 Game > Steam Demo > Demo Settings).
    /// See DemoBuild.
    /// </summary>
    public class DemoSettings : ScriptableObject
    {
        /// <summary>The asset's name in Resources.</summary>
        public const string ResourceName = "Demo Settings";

        [Tooltip("The demo's own Steam App ID. Steamworks: your game's page > All associated packages, DLC, demos and " +
                 "tools > Add Demo gives it. 0 = not set yet (Build Steam Demo asks for it).")]
        [Min(0)]
        public int demoAppId;

        [Tooltip("Where Build Steam Demo puts the demo build (the .exe and its _Data folder). Upload this folder to the " +
                 "demo's depot, without the *_BurstDebugInformation_DoNotShip folder.")]
        public string buildFolder = "E:/429_Demo";
    }
}
