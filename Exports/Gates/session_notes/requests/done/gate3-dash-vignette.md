# Request from gate 3: comfort vignette during dashes and shoves (shared code)

**What:** the comfort vignette (TunnelingVignetteController + ComfortVignetteLevel on the XR Origin's camera) also closes
in while the rig is moved by script: Ball Valley's two-fist dash (12 m/s for 0.35 s, `BallValleyPlayer.Dash`) and every
PlayerLaunch throw (a ball or the giant pumpkin shoving the player, up to 6 m; also every other gate's and room's
launches).

**Why:** the vignette only follows XRI locomotion providers (ComfortVignetteSetupMenu adds a
`LocomotionVignetteProvider` per provider). Scripted moves bypass it, and they're the fastest moves in the game. Comfort
first in VR (the user's rule for the gates).

**The exact change (smallest):**
1. New shared component `Assets/Scripts/Locomotion/ScriptedMoveVignette.cs` (namespace Game.Locomotion), on the same
   object as ComfortVignetteLevel (add it in ComfortVignetteSetupMenu too):
   ```csharp
   public class ScriptedMoveVignette : MonoBehaviour, ITunnelingVignetteProvider
   {
       public VignetteParameters vignetteParameters => parameters;   // [SerializeField] VignetteParameters parameters (defaults = the controller's)
       static ScriptedMoveVignette instance; static readonly HashSet<object> movers = new();
       public static void Begin(object who) { if (instance && movers.Add(who) && movers.Count == 1) instance.controller.BeginTunnelingVignette(instance); }
       public static void End(object who)   { if (instance && movers.Remove(who) && movers.Count == 0) instance.controller.EndTunnelingVignette(instance); }
       // Awake: instance = this, controller = GetComponent<TunnelingVignetteController>(); OnDisable: movers.Clear() + End
   }
   ```
   (ComfortVignetteLevel's strength applies to it like to the other providers; Off = nothing.)
2. `Combat/PlayerLaunch.cs`: `ScriptedMoveVignette.Begin(this)` when a launch starts flying, `End(this)` when it lands or
   `Stop()` is called.
3. Gate 3 then calls `ScriptedMoveVignette.Begin(this)` / `End(this)` round its dash (my file, I'll add it once the
   component exists; until then gate 3 compiles without it).

**Test:** gate 3 dash and a ball shove at Comfort Vignette Low/High/Off; a boss throw in the main game.


**Coordinator, 2026-11-23 01:12: DONE.** `Assets/Scripts/Locomotion/ScriptedMoveVignette.cs` (Game.Locomotion): `ScriptedMoveVignette.Begin(this)` / `End(this)`; it adds itself next to the Tunneling Vignette Controller on first use (no scene change) and uses the player's Comfort Vignette level (Off = nothing). PlayerLaunch calls it only while `ScriptedMoveVignette.Launches` is on, which Gate Run sets while a gate runs (the main game is unchanged until the user decides). Add Begin/End round your dash.
