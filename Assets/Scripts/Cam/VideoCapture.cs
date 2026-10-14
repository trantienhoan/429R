#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Cam
{
    /// <summary>
    /// Editor-only video button for gameplay clips while playing (Quest Link): press Right Ctrl to start recording and
    /// again to stop. A steady "cameraman" camera follows the headset with smoothing and no head tilt, so the video
    /// isn't shaky like raw headset footage, and the game's sound is recorded too (you keep hearing it while recording).
    /// The video is an MP4 (1920x1080, H.264, about 30 fps on the headset's beat) in Exports/Videos; the log line says
    /// what each frame cost (render, encode) when it's saved. A small red dot blinks at the bottom left of your view
    /// while recording (it isn't in the video); a high beep means started, a low one stopped.
    /// The comfort vignette, the red damage lines and the warning panels are left out of the video; the hands stay in.
    /// It adds itself when play mode starts and is never part of a build; stopping play mode ends the recording. Its
    /// settings can be changed during play on the "Video Capture (Right Ctrl)" object under DontDestroyOnLoad.
    /// </summary>
    public class VideoCapture : MonoBehaviour
    {
        [Tooltip("Frames per second of the video. 0 = match the headset: its refresh rate divided down to about 30 (36 at " +
                 "72 Hz, 30 at 90 Hz and 120 Hz), so every video frame is a headset frame and the motion is even. A rate " +
                 "that doesn't divide the headset's makes pans judder, and 60 costs twice the work, which made the headset " +
                 "drop frames while recording.")]
        [Range(0, 60)]
        [SerializeField] private int frameRate;
        [Tooltip("Vertical field of view of the video, in degrees.")]
        [Range(40f, 100f)]
        [SerializeField] private float fieldOfView = 70f;
        [Tooltip("How long the camera takes to catch up with your head, in seconds: more is steadier but lags more.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float smoothing = 0.15f;
        [Tooltip("Video quality, in megabits per second.")]
        [Range(4, 40)]
        [SerializeField] private int megabits = 16;

        private const int Width = 1920, Height = 1080;
        // Windows' video encoder is slow to start the first time (about a second); that happens before the first
        // recording of the session, not in the middle of it.
        private static bool warmedUp;

        private Camera cam;
        private RenderTexture target;
        private MediaEncoder encoder;
        private bool recording;
        private bool firstFrame;
        private float startsAt = -1f;
        private double startTime;
        private long lastSlot;
        private int fps = 30;
        private bool keyWasDown;
        private string file;
        private int frames;
        private int pendingFrames;
        private bool finishing;
        private float finishBy;
        private Vector3 velocity;
        private AudioSource beeper;
        private AudioClip highBeep, lowBeep;
        private GameObject recDot;
        private Material recDotMaterial;
        private AudioTap tap;
        private readonly ConcurrentQueue<float[]> audioBlocks = new();
        private readonly List<Renderer> hiddenRenderers = new();
        private readonly List<Canvas> hiddenCanvases = new();
        // How long the extra camera's render and the encoding take on the main thread, for the log.
        private readonly System.Diagnostics.Stopwatch renderTime = new(), encodeTime = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("Video Capture (Right Ctrl)");
            DontDestroyOnLoad(go);
            go.AddComponent<VideoCapture>();
        }

        private void Awake()
        {
            beeper = gameObject.AddComponent<AudioSource>();
            beeper.playOnAwake = false;
            beeper.spatialBlend = 0f;
            beeper.ignoreListenerPause = true;
            highBeep = MakeBeep(1320f);
            lowBeep = MakeBeep(520f);
        }

        private void Update()
        {
            bool down = RightCtrlDown();
            bool pressed = down && !keyWasDown;
            keyWasDown = down;
            if (pressed)
            {
                if (recording) StopRecording();
                else if (startsAt < 0f && !finishing)
                {
                    // Beep first, start a moment later, so the beep isn't in the video.
                    beeper.PlayOneShot(highBeep, 0.6f);
                    startsAt = Time.unscaledTime + 0.3f;
                }
            }
            if (startsAt >= 0f && Time.unscaledTime >= startsAt)
            {
                startsAt = -1f;
                WarmUp();
                StartRecording();
            }
            if (recDot != null) recDot.SetActive(recording && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f);
            if (finishing && (pendingFrames <= 0 || Time.unscaledTime > finishBy)) Finish();
        }

#if UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        // Read straight from Windows, so it works with the headset on even when Unity isn't the focused window.
        private static bool RightCtrlDown() => (GetAsyncKeyState(0xA3) & 0x8000) != 0;
#else
        private static bool RightCtrlDown() =>
            UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rightCtrlKey.isPressed;
#endif

        private void StartRecording()
        {
            var main = Camera.main;
            if (main == null)
            {
                Debug.LogWarning("[Video] No main camera to record from.");
                return;
            }

            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Exports", "Videos"));
            Directory.CreateDirectory(folder);
            file = Path.Combine(folder, "Play_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4");

            try
            {
                fps = ChooseFrameRate();
                encoder = new MediaEncoder(file, VideoTrack(fps), AudioTrack());
            }
            catch (Exception e)
            {
                Debug.LogError("[Video] Couldn't start the video file: " + e.Message);
                encoder = null;
                return;
            }

            var go = new GameObject("Video Camera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.enabled = false;
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = Mathf.Min(main.nearClipPlane, 0.03f);
            var data = cam.GetUniversalAdditionalCameraData();
            var mainData = main.GetUniversalAdditionalCameraData();
            data.allowXRRendering = false;
            data.renderPostProcessing = true;
            // MSAA (on the render texture) smooths the edges; SMAA on top only cost time.
            data.antialiasing = AntialiasingMode.None;
            data.volumeLayerMask = mainData.volumeLayerMask;
            data.volumeTrigger = go.transform;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            cam.targetTexture = target;
            var head = main.transform;
            go.transform.SetPositionAndRotation(head.position, Level(head.forward, head.rotation));
            velocity = Vector3.zero;

            // The sound as the player hears it, taken from the listener (this doesn't mute it, unlike Unity Recorder).
            while (audioBlocks.TryDequeue(out _)) { }
            if (AttachTap()) tap.Capturing = true;
            else Debug.LogWarning("[Video] No AudioListener: the video will be silent.");

            ShowRecDot(main);
            frames = 0;
            pendingFrames = 0;
            firstFrame = true;
            recording = true;
            Debug.Log($"[Video] Recording to {file} at {fps} fps");
        }

        // Frame Rate, or with 0 the headset's refresh rate divided down to about 30 fps, so a video frame is every 2nd
        // (72 Hz), 3rd (90 Hz) or 4th (120 Hz) headset frame.
        private int ChooseFrameRate()
        {
            if (frameRate > 0) return frameRate;
            float refresh = 0f;
            var displays = new List<UnityEngine.XR.XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
            {
                if (display.running && display.TryGetDisplayRefreshRate(out float rate) && rate > 1f)
                {
                    refresh = rate;
                    break;
                }
            }
            if (refresh <= 1f) refresh = (float)Screen.currentResolution.refreshRateRatio.value;
            if (refresh <= 1f) return 30;
            int every = Mathf.Max(1, Mathf.RoundToInt(refresh / 30f));
            return Mathf.Clamp(Mathf.RoundToInt(refresh / every), 24, 60);
        }

        private VideoTrackEncoderAttributes VideoTrack(int rate)
        {
            var h264 = new H264EncoderAttributes { gopSize = (uint)rate, numConsecutiveBFrames = 0, profile = UnityEditor.VideoEncodingProfile.H264High };
            return new VideoTrackEncoderAttributes(h264)
            {
                frameRate = new MediaRational(rate),
                width = Width,
                height = Height,
                includeAlpha = false,
                bitRateMode = UnityEditor.VideoBitrateMode.High,
                targetBitRate = (uint)megabits * 1000000u,
            };
        }

        private static AudioTrackAttributes AudioTrack() => new()
        {
            sampleRate = new MediaRational(AudioSettings.outputSampleRate),
            channelCount = 2,
            language = "",
        };

        // The sound tap stays on the listener once added (adding and removing it stalls the game the first time), and
        // only copies the sound while recording.
        private bool AttachTap()
        {
            if (tap != null) return true;
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener == null) return false;
            tap = listener.gameObject.AddComponent<AudioTap>();
            tap.Blocks = audioBlocks;
            return true;
        }

        // Slow first-time setup, before the first recording begins rather than in it: a throwaway one-frame video (the
        // encoder takes about a second to start the first time), the sound tap and the red dot.
        private void WarmUp()
        {
            AttachTap();
            if (Camera.main != null)
            {
                ShowRecDot(Camera.main);
                recDot.SetActive(false);
            }
            if (warmedUp) return;
            warmedUp = true;
            var path = Path.Combine(Application.temporaryCachePath, "video_warm_up.mp4");
            try
            {
                using (var warm = new MediaEncoder(path, VideoTrack(30), AudioTrack()))
                using (var pixels = new NativeArray<byte>(Width * Height * 4, Allocator.Temp))
                using (var silence = new NativeArray<float>(AudioSettings.outputSampleRate / 30 * 2, Allocator.Temp))
                {
                    warm.AddFrame(Width, Height, 0, TextureFormat.RGBA32, pixels, new MediaTime(0, 1000000));
                    warm.AddSamples(silence);
                }
                File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Video] Encoder warm-up failed: " + e.Message);
            }
        }

        // Stops taking frames and sound; the file is finished once the frames still on their way from the GPU are in.
        private void StopRecording()
        {
            if (!recording) return;
            recording = false;
            if (tap != null) tap.Capturing = false;
            FeedAudio();
            finishing = true;
            finishBy = Time.unscaledTime + 3f;
            if (recDot != null) recDot.SetActive(false);
            beeper.PlayOneShot(lowBeep, 0.6f);
        }

        private void Finish()
        {
            finishing = false;
            encoder?.Dispose();
            encoder = null;
            if (cam != null) Destroy(cam.gameObject);
            cam = null;
            if (target != null)
            {
                target.Release();
                Destroy(target);
            }
            target = null;
            int n = Mathf.Max(1, frames);
            Debug.Log($"[Video] Saved {file} ({frames} frames; per frame: render {renderTime.Elapsed.TotalMilliseconds / n:F1} ms, " +
                      $"encode {encodeTime.Elapsed.TotalMilliseconds / n:F1} ms)");
            renderTime.Reset();
            encodeTime.Reset();
        }

        // Play mode is ending: wrap up now with whatever frames have arrived.
        private void OnDisable()
        {
            StopRecording();
            if (!finishing) return;
            AsyncGPUReadback.WaitAllRequests();
            Finish();
        }

        private void OnDestroy()
        {
            if (tap != null) Destroy(tap);
            if (recDot != null) Destroy(recDot);
            if (recDotMaterial != null) Destroy(recDotMaterial);
        }

        private void LateUpdate()
        {
            if (!recording || cam == null) return;
            var main = Camera.main;
            if (main == null) return;

            // Follow the head smoothly, level with the horizon.
            var head = main.transform;
            float dt = Time.unscaledDeltaTime;
            var t = cam.transform;
            t.position = smoothing > 0f ? Vector3.SmoothDamp(t.position, head.position, ref velocity, smoothing * 0.6f, Mathf.Infinity, dt) : head.position;
            float follow = smoothing > 0f ? 1f - Mathf.Exp(-dt / smoothing) : 1f;
            t.rotation = Quaternion.Slerp(t.rotation, Level(head.forward, head.rotation), follow);
            cam.fieldOfView = fieldOfView;

            // Picture and sound both start here, so a slow setup (the first recording of a session) can't put them
            // out of step: the sound from before is dropped.
            if (firstFrame)
            {
                firstFrame = false;
                while (audioBlocks.TryDequeue(out _)) { }
                startTime = Time.realtimeSinceStartupAsDouble;
                lastSlot = -1;
            }
            FeedAudio();

            double now = Time.realtimeSinceStartupAsDouble - startTime;
            // The video frame this moment belongs to, on an even 1/fps grid (a headset frame a little early still
            // counts). Each video frame is stamped exactly on the grid, so the motion in the video is even; a frame the
            // game didn't make in time is simply shown a little longer.
            long slot = (long)Math.Floor(now * fps + 0.25);
            if (slot <= lastSlot) return;
            lastSlot = slot;

            HideOverlays(main);
            renderTime.Start();
            try
            {
                cam.Render();
            }
            finally
            {
                renderTime.Stop();
                foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
                foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
            }
            var time = new MediaTime(slot, (uint)fps);
            pendingFrames++;
            AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request => WriteFrame(request, time));
        }

        private void WriteFrame(AsyncGPUReadbackRequest request, MediaTime time)
        {
            pendingFrames--;
            if (encoder == null || request.hasError) return;
            encodeTime.Start();
            if (encoder.AddFrame(Width, Height, 0, TextureFormat.RGBA32, request.GetData<byte>(), time)) frames++;
            encodeTime.Stop();
        }

        private void FeedAudio()
        {
            if (encoder == null) return;
            while (audioBlocks.TryDequeue(out var block))
            {
                using var samples = new NativeArray<float>(block, Allocator.Temp);
                encoder.AddSamples(samples);
            }
        }

        private static Quaternion Level(Vector3 forward, Quaternion fallback) =>
            Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.98f ? Quaternion.LookRotation(forward, Vector3.up) : fallback;

        // What only makes sense inside the headset: everything fixed to the head (comfort vignette, damage lines, the
        // red dot) and the warning panels.
        private void HideOverlays(Camera main)
        {
            hiddenRenderers.Clear();
            hiddenCanvases.Clear();
            var roots = new List<Transform> { main.transform };
            foreach (var w in FindObjectsByType<Menus.HealthWarning>(FindObjectsSortMode.None)) roots.Add(w.transform);
            foreach (var w in FindObjectsByType<Menus.CongratulationWarning>(FindObjectsSortMode.None)) roots.Add(w.transform);
            foreach (var root in roots)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    r.enabled = false;
                    hiddenRenderers.Add(r);
                }
                foreach (var c in root.GetComponentsInChildren<Canvas>())
                {
                    if (!c.enabled) continue;
                    c.enabled = false;
                    hiddenCanvases.Add(c);
                }
            }
        }

        // A small blinking red dot at the bottom left of the player's view while recording.
        private void ShowRecDot(Camera main)
        {
            if (recDot == null)
            {
                recDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                recDot.name = "Recording Dot";
                Destroy(recDot.GetComponent<Collider>());
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    recDotMaterial = new Material(shader);
                    recDotMaterial.SetColor("_BaseColor", new Color(1f, 0.1f, 0.1f));
                    recDot.GetComponent<Renderer>().sharedMaterial = recDotMaterial;
                }
                recDot.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            recDot.transform.SetParent(main.transform, false);
            recDot.transform.localPosition = new Vector3(-0.13f, -0.11f, 0.45f);
            recDot.transform.localScale = Vector3.one * 0.012f;
        }

        private static AudioClip MakeBeep(float pitch)
        {
            const int rate = 44100;
            var samples = new float[(int)(rate * 0.12f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 25f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * pitch * t) * envelope * 0.5f;
            }
            var clip = AudioClip.Create("Video Beep", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Sits on the AudioListener and copies what the player hears (as stereo) for the video, on the audio thread.
        /// </summary>
        private sealed class AudioTap : MonoBehaviour
        {
            public ConcurrentQueue<float[]> Blocks;
            public volatile bool Capturing;

            private void OnAudioFilterRead(float[] data, int channels)
            {
                if (!Capturing || Blocks == null || channels <= 0) return;
                int frames = data.Length / channels;
                var stereo = new float[frames * 2];
                for (int i = 0; i < frames; i++)
                {
                    stereo[2 * i] = data[i * channels];
                    stereo[2 * i + 1] = data[i * channels + (channels > 1 ? 1 : 0)];
                }
                Blocks.Enqueue(stereo);
            }
        }
    }
}
#endif
