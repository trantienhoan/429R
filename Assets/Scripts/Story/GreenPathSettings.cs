using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The green light path's numbers (user 2026-11-27: "when we pick up the piece of the seed, from that point create a
    /// green light path ... follow that green light path will reach to a zone to teleport player back to the center and
    /// the new room will come"; "use that green light path to lead Player at the start of the game to reach to the
    /// Fairy"). Resources/Green Path Settings; Tools > 429 Game > Story > Set Up Green Path makes it and the looks.
    /// </summary>
    public class GreenPathSettings : ScriptableObject
    {
        [Header("Looks (made by Set Up Green Path)")]
        [Tooltip("The path, its sparks and the zone (Prefabs/Story/Green_Path).")]
        [SerializeField] private GameObject pathPrefab;
        [Tooltip("The teleport tube that runs round the player (Prefabs/Story/Teleport_Tube).")]
        [SerializeField] private GameObject tubePrefab;
        [Tooltip("The dark hallway with green light down both sides that leads out of the room over the drop " +
                 "(Prefabs/Story/Green_Hallway).")]
        [SerializeField] private GameObject hallwayPrefab;

        [System.Serializable]
        public class Doorway
        {
            [Tooltip("A model whose wall can't sink on its own (the rooftop's: one mesh).")]
            public Mesh whole;
            [Tooltip("The same with the wall at the exit cut away (made by Set Up Green Path).")]
            public Mesh opened;
        }

        [Tooltip("Models swapped for a copy with the doorway cut out while the hallway is there (the start rooftop's walls and " +
                 "battlements are one mesh each; the rooms' kit pieces sink instead).")]
        [SerializeField] private Doorway[] doorways = new Doorway[0];
        [Tooltip("The path's green (the Seed's glow).")]
        [SerializeField] private Color colour = new Color(0.55f, 1f, 0.45f, 1f);
        [Tooltip("How bright the band is between the pulses (1 = as bright as a pulse).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float bandBrightness = 0.55f;

        [Header("The way")]
        [Tooltip("Metres between the path's points (it follows the floor's height at each).")]
        [Min(0.05f)]
        [SerializeField] private float step = 0.2f;
        [Tooltip("Metres the path sits above the floor.")]
        [SerializeField] private float floorLift = 0.03f;
        [Tooltip("How fast the path draws itself out from the player's feet, in metres a second.")]
        [Min(0.5f)]
        [SerializeField] private float drawSpeed = 9f;
        [Tooltip("The bright pulses that run along it towards where it leads: metres a second, metres long, metres apart " +
                 "(at least half the path, so two show at most).")]
        [Min(0.2f)]
        [SerializeField] private float pulseSpeed = 3.6f;
        [Min(0.1f)]
        [SerializeField] private float pulseLength = 1.4f;
        [Min(1f)]
        [SerializeField] private float pulseSpacing = 5f;
        [Tooltip("To a teleport zone, the walk (the path in the room to its doorway, then the hallway) is at least this many " +
                 "metres (user: \"make player walk at least 12m\", then \"make the path longer, try 24m\"): the hallway is made as long as it takes.")]
        [Min(0f)]
        [SerializeField] private float minWalk = 24f;
        [Tooltip("The hallway out of the room is at least / at most this long (metres): 24 and 24 (user: \"Hallway alone 24 m\"), " +
                 "so it is always 24 m from the doorway, whatever the walk in the room adds.")]
        [Min(1f)]
        [SerializeField] private float hallwayMin = 24f;
        [Min(1f)]
        [SerializeField] private float hallwayMax = 24f;
        [Tooltip("The player this far off the path (metres) for Reroute After seconds: it draws again from their feet.")]
        [Min(0.5f)]
        [SerializeField] private float rerouteFrom = 2.5f;
        [Min(0.1f)]
        [SerializeField] private float rerouteAfter = 1.2f;
        [Tooltip("Leading to someone (the Fairy): the path ends this far short of them.")]
        [Min(0f)]
        [SerializeField] private float leadStopShort = 0.9f;
        [Tooltip("The exit spot moves to the nearest walkable floor within this many metres (furniture moves between room sets).")]
        [Min(0f)]
        [SerializeField] private float exitSnap = 2.5f;
        [Tooltip("Seconds after the state starts before the path shows (the seed's green smoke goes first).")]
        [Min(0f)]
        [SerializeField] private float showAfter = 0.6f;

        [Header("The zone")]
        [Tooltip("The teleport zone's size: the player's head this close (metres, flat) to its middle sets it off.")]
        [Min(0.2f)]
        [SerializeField] private float zoneRadius = 0.65f;
        [Tooltip("The portal (the zone) only gets clearer and brighter the closer the player is (user 2026-11-27): fully " +
                 "clear within Clear At metres, at Far Share of it from Faint At metres and further, smoothly between.")]
        [Min(0f)]
        [SerializeField] private float zoneClearAt = 1.2f;
        [Min(0.5f)]
        [SerializeField] private float zoneFaintAt = 9f;
        [Range(0f, 1f)]
        [SerializeField] private float zoneFarShare = 0.08f;

        [Header("The teleport")]
        [Tooltip("Seconds the world takes to go dark round the path once the player is in the zone.")]
        [Min(0.05f)]
        [SerializeField] private float darkSeconds = 0.6f;
        [Tooltip("Seconds the tube runs round them before they are moved.")]
        [Min(0.2f)]
        [SerializeField] private float tubeSeconds = 1.5f;
        [Tooltip("The jump: the view flashes this colour for this many seconds each way while they are moved.")]
        [SerializeField] private Color jumpColour = new Color(0.62f, 1f, 0.55f, 1f);
        [Min(0.02f)]
        [SerializeField] private float jumpFade = 0.16f;
        [Tooltip("Seconds the tube keeps running round them at the middle before it sinks away.")]
        [Min(0f)]
        [SerializeField] private float afterJump = 0.7f;
        [Tooltip("Seconds the world takes to light up again.")]
        [Min(0.05f)]
        [SerializeField] private float lightBack = 1f;
        [Tooltip("The poof: the seed's green smoke bomb, this size (a taken piece's is 0.15).")]
        [Min(0.01f)]
        [SerializeField] private float poofSize = 0.22f;

        [Header("The dark (share of each left at full dark)")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientLeft = 0.03f;
        [Range(0f, 1f)]
        [SerializeField] private float skyLeft = 0.04f;
        [Range(0f, 1f)]
        [SerializeField] private float lightsLeft = 0f;

        [Header("Sounds")]
        [SerializeField] private AudioClip appearSound;
        [Range(0f, 1f)]
        [SerializeField] private float appearVolume = 0.45f;
        [SerializeField] private AudioClip zoneHum;
        [Range(0f, 1f)]
        [SerializeField] private float zoneHumVolume = 0.25f;
        [SerializeField] private AudioClip poofSound;
        [Range(0f, 1f)]
        [SerializeField] private float poofVolume = 0.8f;
        [SerializeField] private AudioClip tubeSound;
        [Range(0f, 1f)]
        [SerializeField] private float tubeVolume = 0.55f;
        [SerializeField] private AudioClip jumpSound;
        [Range(0f, 1f)]
        [SerializeField] private float jumpVolume = 0.8f;

        public GameObject PathPrefab => pathPrefab;
        public GameObject TubePrefab => tubePrefab;
        public GameObject HallwayPrefab => hallwayPrefab;

        /// <summary>The cut copy of this mesh (null if it has none).</summary>
        public Mesh OpenedFor(Mesh whole)
        {
            if (whole == null || doorways == null) return null;
            foreach (var d in doorways)
                if (d != null && d.whole == whole && d.opened != null) return d.opened;
            return null;
        }
        public float HallwayMin => hallwayMin;
        public float HallwayMax => Mathf.Max(hallwayMin, hallwayMax);
        public Color Colour => colour;
        public float BandBrightness => bandBrightness;
        public float Step => step;
        public float FloorLift => floorLift;
        public float DrawSpeed => drawSpeed;
        public float PulseSpeed => pulseSpeed;
        public float PulseLength => pulseLength;
        public float PulseSpacing => pulseSpacing;
        public float MinWalk => minWalk;
        public float RerouteFrom => rerouteFrom;
        public float RerouteAfter => rerouteAfter;
        public float LeadStopShort => leadStopShort;
        public float ExitSnap => exitSnap;
        public float ShowAfter => showAfter;
        public float ZoneRadius => zoneRadius;
        public float ZoneClearAt => zoneClearAt;
        public float ZoneFaintAt => Mathf.Max(zoneClearAt + 0.1f, zoneFaintAt);
        public float ZoneFarShare => zoneFarShare;
        public float DarkSeconds => darkSeconds;
        public float TubeSeconds => tubeSeconds;
        public Color JumpColour => jumpColour;
        public float JumpFade => jumpFade;
        public float AfterJump => afterJump;
        public float LightBack => lightBack;
        public float PoofSize => poofSize;
        public float AmbientLeft => ambientLeft;
        public float SkyLeft => skyLeft;
        public float LightsLeft => lightsLeft;
        public AudioClip AppearSound => appearSound;
        public float AppearVolume => appearVolume;
        public AudioClip ZoneHum => zoneHum;
        public float ZoneHumVolume => zoneHumVolume;
        public AudioClip PoofSound => poofSound;
        public float PoofVolume => poofVolume;
        public AudioClip TubeSound => tubeSound;
        public float TubeVolume => tubeVolume;
        public AudioClip JumpSound => jumpSound;
        public float JumpVolume => jumpVolume;

        private static GreenPathSettings instance;

        /// <summary>Resources/Green Path Settings, or the defaults if it is missing (no looks then).</summary>
        public static GreenPathSettings Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<GreenPathSettings>("Green Path Settings");
                if (instance == null) instance = CreateInstance<GreenPathSettings>();
                return instance;
            }
        }
    }
}
