using Game.PlayGround;
using Game.Saving;
using Game.VFX;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Story
{
    /// <summary>
    /// The laugh that calls something (user's story, 2026-11-23): when a room's runner pumpkin laughs from hiding, the
    /// room's ceiling lamp stutters (its own Lamp Flicker) and turns a cold blue while the laugh sounds, and every laugh
    /// makes the view a bit colder; the cold slowly goes while that pumpkin keeps quiet, and faster once it is broken.
    /// Main game rooms only (Story Laugh Settings' room bases): a laugh with no room ceiling light near it does nothing
    /// (the start rooftop, the gates). Game time: a pause or a notice holds it.
    /// Made by itself in every scene; settings in Resources/Story Laugh Settings (Tools > 429 Game > Story > Set Up Laugh
    /// Chill makes the asset).
    /// </summary>
    public class LaughChill : MonoBehaviour
    {
        private static LaughChill current;

        private RunAwayPumpkin laugher;
        private bool hadLaugher;
        private Light lamp;
        private LampFlicker flicker;
        private Color lampColor;
        private float weird;
        private float cold, coldTarget;
        private Volume volume;

        /// <summary>How cold the view is now, 0 to 1.</summary>
        public static float Cold => current != null ? current.cold : 0f;

        /// <summary>How far the ceiling light is from its own look now, 0 to 1.</summary>
        public static float Weird => current != null && current.lamp != null ? current.weird : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make()
        {
            if (FindAnyObjectByType<LaughChill>() != null) return;
            new GameObject("Story_Laugh_Chill").AddComponent<LaughChill>();
        }

        private void OnEnable()
        {
            current = this;
            RunAwayPumpkin.Laughed += Heard;
        }

        private void OnDisable()
        {
            RunAwayPumpkin.Laughed -= Heard;
            RestoreLamp();
            if (volume != null) volume.weight = 0f;
            if (current == this) current = null;
        }

        private void Heard(RunAwayPumpkin pumpkin)
        {
            if (pumpkin == null) return;
            var settings = StoryLaughSettings.Instance;
            var light = CeilingLight(pumpkin.transform.position, settings);
            if (light == null) return;
            if (lamp != light)
            {
                RestoreLamp();
                lamp = light;
                lampColor = light.color;
                flicker = light.GetComponent<LampFlicker>();
            }
            if (flicker != null) flicker.Stutter();
            laugher = pumpkin;
            hadLaugher = true;
            coldTarget = Mathf.Min(settings.MostCold, Mathf.Max(cold, coldTarget) + settings.ColdPerLaugh);
        }

        // The room's ceiling light: the nearest Point Light inside one of the main game's room bases.
        private static Light CeilingLight(Vector3 near, StoryLaughSettings settings)
        {
            Light best = null;
            float bestDistance = settings.LightRange;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Point || !InRoom(light.transform, settings.RoomBases)) continue;
                float distance = Vector3.Distance(light.transform.position, near);
                if (distance >= bestDistance) continue;
                best = light;
                bestDistance = distance;
            }
            return best;
        }

        private static bool InRoom(Transform t, string[] bases)
        {
            for (var p = t; p != null; p = p.parent)
                foreach (var name in bases)
                    if (!string.IsNullOrEmpty(name) && p.name.StartsWith(name)) return true;
            return false;
        }

        private void Update()
        {
            var settings = StoryLaughSettings.Instance;
            float dt = Time.deltaTime;
            bool laughing = laugher != null && laugher.IsLaughing && lamp != null;

            // The light: turns cold while the laugh sounds, then comes back (its brightness is the Lamp Flicker's).
            if (lamp != null)
            {
                weird = Mathf.MoveTowards(weird, laughing ? 1f : 0f, dt / (laughing ? 0.15f : settings.LightBackSeconds));
                var turned = Color.Lerp(lampColor, settings.WeirdColor, settings.WeirdMix);
                lamp.color = Color.Lerp(lampColor, turned, weird);
                if (!laughing && weird <= 0f) RestoreLamp();
            }

            // The cold: comes in after each laugh, goes slowly while the pumpkin keeps quiet, faster once it is broken.
            bool gone = hadLaugher && laugher == null;
            if (!laughing)
                coldTarget = Mathf.Max(0f, coldTarget - (gone ? settings.ColdFadeWhenGone : settings.ColdFadeWhileThere) * dt);
            if (Checkpoints.IsDying) coldTarget = 0f;
            cold = cold < coldTarget ? Mathf.MoveTowards(cold, coldTarget, dt / settings.ColdInSeconds) : coldTarget;
            if (gone && cold <= 0f) hadLaugher = false;
            ShowCold(settings);
        }

        private void RestoreLamp()
        {
            if (lamp != null) lamp.color = lampColor;
            lamp = null;
            flicker = null;
            weird = 0f;
        }

        private void ShowCold(StoryLaughSettings settings)
        {
            if (cold <= 0f && volume == null) return;
            if (volume == null) volume = MakeVolume(settings);
            volume.weight = cold;
        }

        // Its own global volume over the game's own (priority 0) and under Chaos Darkness (1), so nothing else changes.
        private Volume MakeVolume(StoryLaughSettings settings)
        {
            var go = new GameObject("Story_Cold");
            go.transform.SetParent(transform, false);
            var made = go.AddComponent<Volume>();
            made.isGlobal = true;
            made.priority = 0.5f;
            made.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var colours = profile.Add<ColorAdjustments>();
            colours.colorFilter.Override(settings.ColdFilter);
            colours.saturation.Override(settings.ColdSaturation);
            var balance = profile.Add<WhiteBalance>();
            balance.temperature.Override(settings.ColdTemperature);
            made.sharedProfile = profile;
            return made;
        }
    }
}
