using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Quest (Android) build settings from the performance audit (Tools > 429 Game > Build). Preview logs what would
    /// change; Apply changes it. Running Apply again leaves settings that are already done alone.
    /// </summary>
    public static class QuestOptimizationMenu
    {
        private const string Android = "Android";
        private const string QuestQualityLevel = "Performant";
        private const int MaxTextureSize = 1024;
        private const float MusicQuality = 0.4f;
        private const uint MusicSampleRate = 44100;

        // URP features the Quest pipeline asset turns off. Each one is either costly on Quest or unused by the game,
        // and every enabled feature multiplies the shader variants that ship.
        private static readonly (string property, string label)[] QuestPipelineFeaturesOff =
        {
            ("m_SupportsHDR", "HDR"),
            ("m_AdditionalLightShadowsSupported", "point/spot light shadows"),
            ("m_SupportsLightCookies", "light cookies (no light uses one)"),
            ("m_EnableLODCrossFade", "LOD cross-fade (no LOD Groups)"),
            ("m_SupportsTerrainHoles", "terrain holes (terrain is only in the Pocket Portal demo scenes)"),
            ("m_SupportDataDrivenLensFlare", "lens flares (no Lens Flare components)"),
        };

        [MenuItem("Tools/429 Game/Build/Preview Quest Optimizations")]
        public static void Preview() => Run(false);

        [MenuItem("Tools/429 Game/Build/Apply Quest Optimizations")]
        public static void Apply()
        {
            if (!EditorUtility.DisplayDialog("Apply Quest optimizations",
                    "Changes the Quest render pipeline, quality levels, Android graphics API, stack traces, and the " +
                    "import settings of shipped textures and audio (then reimports them). PC settings stay as they are.\n\n" +
                    "Run Preview first to see the list in the Console.", "Apply", "Cancel"))
                return;
            Run(true);
        }

        /// <summary>Apply without the dialog, for batch mode: Unity -batchmode -quit -executeMethod Game.EditorTools.QuestOptimizationMenu.ApplyFromCommandLine</summary>
        public static void ApplyFromCommandLine() => Run(true);

        private static void Run(bool apply)
        {
            var quest = QuestPipeline();
            if (quest == null)
            {
                Debug.LogError($"[Quest] No quality level named '{QuestQualityLevel}' with a render pipeline asset. Nothing changed.");
                return;
            }

            var log = new List<string>();
            TurnOffPipelineFeatures(quest, apply, log);
            KeepOtherQualityLevelsOffAndroid(apply, log);
            UseQuestPipelineAsDefault(quest, apply, log);
            UseVulkanOnly(apply, log);
            DropLogStackTraces(apply, log);
            int reimported = TuneShippedImports(apply, log);
            if (apply) AssetDatabase.SaveAssets();

            string header = apply ? $"[Quest] Applied {log.Count} changes ({reimported} assets reimported):"
                                  : $"[Quest] Preview, nothing changed yet. Apply would make {log.Count} changes:";
            Debug.Log(log.Count > 0 ? header + "\n- " + string.Join("\n- ", log) : "[Quest] Everything is already set. Nothing to change.");
        }

        private static RenderPipelineAsset QuestPipeline()
        {
            int level = System.Array.IndexOf(QualitySettings.names, QuestQualityLevel);
            return level < 0 ? null : QualitySettings.GetRenderPipelineAssetAt(level);
        }

        private static void TurnOffPipelineFeatures(RenderPipelineAsset quest, bool apply, List<string> log)
        {
            var so = new SerializedObject(quest);
            foreach (var (property, label) in QuestPipelineFeaturesOff)
            {
                var setting = so.FindProperty(property);
                if (setting == null)
                {
                    log.Add($"{quest.name}: setting {property} not found, skipped");
                    continue;
                }
                if (!setting.boolValue) continue;

                setting.boolValue = false;
                log.Add($"{quest.name}: {label} off");
            }
            if (apply && so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(quest);
        }

        /// <summary>
        /// URP ships the shader variants of every pipeline asset a build can use, so the Balanced and High Fidelity
        /// assets made Quest carry their soft shadows, reflection probes, 8 lights and more.
        /// </summary>
        private static void KeepOtherQualityLevelsOffAndroid(bool apply, List<string> log)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (settings == null) return;

            var so = new SerializedObject(settings);
            var levels = so.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                string name = level.FindPropertyRelative("name").stringValue;
                if (name == QuestQualityLevel) continue;

                var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
                bool already = Enumerable.Range(0, excluded.arraySize)
                    .Any(j => excluded.GetArrayElementAtIndex(j).stringValue == Android);
                if (already) continue;

                excluded.InsertArrayElementAtIndex(excluded.arraySize);
                excluded.GetArrayElementAtIndex(excluded.arraySize - 1).stringValue = Android;
                log.Add($"Quality level '{name}': left out of Android builds");
            }
            if (apply && so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(settings);
        }

        /// <summary>The default pipeline ships with every build, even though each quality level picks its own.</summary>
        private static void UseQuestPipelineAsDefault(RenderPipelineAsset quest, bool apply, List<string> log)
        {
            var current = GraphicsSettings.defaultRenderPipeline;
            if (current == quest) return;

            log.Add($"Default render pipeline: {(current != null ? current.name : "none")} -> {quest.name} " +
                    "(the editor and PC still use their own quality level's pipeline)");
            if (apply) GraphicsSettings.defaultRenderPipeline = quest;
        }

        /// <summary>Quest runs Vulkan; Auto also compiled and shipped an OpenGL ES copy of every shader.</summary>
        private static void UseVulkanOnly(bool apply, List<string> log)
        {
            bool auto = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (!auto && apis.Length == 1 && apis[0] == GraphicsDeviceType.Vulkan) return;

            log.Add($"Android graphics API: {(auto ? "Auto" : string.Join(", ", apis))} -> Vulkan only");
            if (!apply) return;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        }

        /// <summary>Every Debug.Log/LogWarning in a build captured a script stack trace. Errors keep theirs.</summary>
        private static void DropLogStackTraces(bool apply, List<string> log)
        {
            foreach (var type in new[] { LogType.Log, LogType.Warning })
            {
                var current = PlayerSettings.GetStackTraceLogType(type);
                if (current == StackTraceLogType.None) continue;

                log.Add($"Stack traces for {type}: {current} -> None");
                if (apply) PlayerSettings.SetStackTraceLogType(type, StackTraceLogType.None);
            }
        }

        /// <summary>Textures and audio the build ships: what the enabled scenes and Resources folders reference.</summary>
        private static string[] ShippedAssets()
        {
            var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                .Concat(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/")))
                .Where(p => !p.Contains("/Editor/"))
                .ToArray();
            return AssetDatabase.GetDependencies(roots, true).Where(p => !p.Contains("/Editor/")).ToArray();
        }

        private static int TuneShippedImports(bool apply, List<string> log)
        {
            var changed = new List<AssetImporter>();
            var monoSounds = new List<string>();
            foreach (var path in ShippedAssets())
            {
                switch (AssetImporter.GetAtPath(path))
                {
                    case TextureImporter texture when TuneTexture(texture, apply, log):
                        changed.Add(texture);
                        break;
                    case AudioImporter audio when TuneAudio(audio, apply, log, monoSounds):
                        changed.Add(audio);
                        break;
                }
            }
            if (monoSounds.Count > 0)
                log.Add($"{monoSounds.Count} sound effects: Force To Mono (halves their size and memory): {string.Join(", ", monoSounds)}");

            if (!apply || changed.Count == 0) return 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var importer in changed) importer.SaveAndReimport();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return changed.Count;
        }

        /// <summary>
        /// Android override: model textures above 1024 are capped at 1024, and normal maps imported uncompressed
        /// get compressed. Sprites and UI textures are left alone, and a hand-picked Android format is kept.
        /// </summary>
        private static bool TuneTexture(TextureImporter texture, bool apply, List<string> log)
        {
            if (texture.textureShape != TextureImporterShape.Texture2D) return false;
            if (texture.textureType != TextureImporterType.Default && texture.textureType != TextureImporterType.NormalMap) return false;

            var android = texture.GetPlatformTextureSettings(Android);
            var current = android.overridden ? android : texture.GetDefaultPlatformTextureSettings();
            texture.GetSourceTextureWidthAndHeight(out int width, out int height);
            int size = Mathf.Min(current.maxTextureSize, Mathf.Max(width, height));
            bool tooBig = size > MaxTextureSize;
            bool uncompressedNormal = texture.textureType == TextureImporterType.NormalMap
                                      && current.format == TextureImporterFormat.Automatic
                                      && current.textureCompression == TextureImporterCompression.Uncompressed;
            if (!tooBig && !uncompressedNormal) return false;

            var what = new List<string>();
            if (tooBig) what.Add($"{size} -> {MaxTextureSize}");
            if (uncompressedNormal) what.Add("uncompressed -> compressed");
            log.Add($"Texture {texture.assetPath}: Android {string.Join(", ", what)}");
            if (!apply) return true;

            // Start from what Android uses today; a fresh override would pick up stale values from the .meta.
            var settings = new TextureImporterPlatformSettings();
            current.CopyTo(settings);
            settings.name = Android;
            settings.overridden = true;
            if (tooBig) settings.maxTextureSize = MaxTextureSize;
            if (uncompressedNormal) settings.textureCompression = TextureImporterCompression.Compressed;
            texture.SetPlatformTextureSettings(settings);
            return true;
        }

        /// <summary>
        /// Music (streamed clips) gets a lighter Vorbis encode on Android only; PC keeps its quality. Stereo sound
        /// effects become mono on every platform: all game AudioSources are 2D, and mono is what 3D audio needs anyway.
        /// </summary>
        private static bool TuneAudio(AudioImporter audio, bool apply, List<string> log, List<string> monoSounds)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audio.assetPath);
            if (clip == null || audio.ambisonic) return false;

            var defaults = audio.defaultSampleSettings;
            if (defaults.loadType == AudioClipLoadType.Streaming)
            {
                var s = audio.ContainsSampleSettingsOverride(Android) ? audio.GetOverrideSampleSettings(Android) : defaults;
                bool lowerQuality = s.compressionFormat != AudioCompressionFormat.Vorbis || s.quality > MusicQuality + 0.001f;
                bool resample = clip.frequency > MusicSampleRate
                                && (s.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate || s.sampleRateOverride != MusicSampleRate);
                if (!lowerQuality && !resample) return false;

                log.Add($"Music {audio.assetPath}: Android Vorbis {Mathf.RoundToInt(s.quality * 100)}% -> " +
                        $"{Mathf.RoundToInt(Mathf.Min(s.quality, MusicQuality) * 100)}%" +
                        (resample ? $", {clip.frequency} Hz -> {MusicSampleRate} Hz" : ""));
                if (!apply) return true;

                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = Mathf.Min(s.quality, MusicQuality);
                if (resample)
                {
                    s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                    s.sampleRateOverride = MusicSampleRate;
                }
                return audio.SetOverrideSampleSettings(Android, s);
            }

            if (audio.forceToMono || clip.channels < 2) return false;

            monoSounds.Add(clip.name);
            if (apply) audio.forceToMono = true;
            return true;
        }
    }
}
