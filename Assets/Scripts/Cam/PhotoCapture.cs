#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Game.Cam
{
    /// <summary>
    /// Editor-only photo button for promo shots while playing (Quest Link or the XR simulator): press Right Shift and
    /// a separate camera renders what the headset is looking at, at 4K with full anti-aliasing and post-processing,
    /// level with the horizon. The photo is brightened a little for social media and saved into Exports/Screenshots
    /// (1920x1080, plus the 4K version in Exports/Screenshots/4K). A shutter click plays when it's taken.
    /// The comfort vignette, the red damage lines and the warning panels are left out; the hands and anything they
    /// hold stay in. It adds itself when play mode starts and is never part of a build. Its settings can be changed
    /// during play on the "Photo Capture (Right Shift)" object under DontDestroyOnLoad.
    /// </summary>
    public class PhotoCapture : MonoBehaviour
    {
        [Tooltip("Vertical field of view of the photo, in degrees. 60-70 feels like standing there; smaller zooms in.")]
        [Range(20f, 100f)]
        [SerializeField] private float fieldOfView = 65f;
        [Tooltip("Remove head tilt (roll) so the horizon is level.")]
        [SerializeField] private bool levelHorizon = true;
        [Tooltip("Leave the hands (and the rest of the XR rig) out of the photo.")]
        [SerializeField] private bool hideHands;
        [Tooltip("Also save the 4K version into Exports/Screenshots/4K.")]
        [SerializeField] private bool save4K = true;

        [Header("Brighten (same as the promo shots)")]
        [SerializeField] private bool brighten = true;
        [Tooltip("Most the exposure is raised. The brightest highlights are lifted to near white, up to this much.")]
        [Range(1f, 3f)]
        [SerializeField] private float maxGain = 1.8f;
        [Tooltip("Below 1 opens up the shadows (dark rooms: 0.8).")]
        [Range(0.5f, 1.2f)]
        [SerializeField] private float gamma = 0.84f;
        [Range(0.5f, 1.5f)]
        [SerializeField] private float contrast = 1.12f;
        [Range(0.5f, 1.5f)]
        [SerializeField] private float colour = 1.15f;

        private const uint Width = 3840, Height = 2160;
        private float lastShot = -10f;
        private bool keyWasDown;
        private AudioSource shutter;
        private string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("Photo Capture (Right Shift)");
            DontDestroyOnLoad(go);
            go.AddComponent<PhotoCapture>();
        }

        private void Awake()
        {
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Exports", "Screenshots"));
            shutter = gameObject.AddComponent<AudioSource>();
            shutter.playOnAwake = false;
            shutter.spatialBlend = 0f;
            shutter.ignoreListenerPause = true;
            shutter.clip = MakeShutterClick();
        }

        private void Update()
        {
            bool down = RightShiftDown();
            bool pressed = down && !keyWasDown;
            keyWasDown = down;
            if (!pressed || Time.unscaledTime - lastShot < 0.3f) return;
            lastShot = Time.unscaledTime;
            TakePhoto();
        }

#if UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        // Read straight from Windows, so it works with the headset on even when Unity isn't the focused window.
        private static bool RightShiftDown() => (GetAsyncKeyState(0xA1) & 0x8000) != 0;
#else
        private static bool RightShiftDown() => Keyboard.current != null && Keyboard.current.rightShiftKey.isPressed;
#endif

        private void TakePhoto()
        {
            var main = Camera.main;
            if (main == null)
            {
                Debug.LogWarning("[Photo] No main camera to take a photo from.");
                return;
            }

            var hidden = HideOverlays(main);
            var go = new GameObject("Photo Camera");
            RenderTexture rt = null;
            Texture2D tex = null;
            byte[] pixels;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(main);
                cam.enabled = false;
                cam.fieldOfView = fieldOfView;
                cam.nearClipPlane = Mathf.Min(main.nearClipPlane, 0.03f);
                var head = main.transform;
                var forward = head.forward;
                var level = levelHorizon && Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.98f;
                cam.transform.SetPositionAndRotation(head.position,
                    level ? Quaternion.LookRotation(forward, Vector3.up) : head.rotation);

                var data = cam.GetUniversalAdditionalCameraData();
                var mainData = main.GetUniversalAdditionalCameraData();
                data.allowXRRendering = false;
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.volumeLayerMask = mainData.volumeLayerMask;
                data.volumeTrigger = head;

                rt = new RenderTexture((int)Width, (int)Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D((int)Width, (int)Height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                RenderTexture.active = active;
                cam.targetTexture = null;
                pixels = tex.GetRawTextureData<byte>().ToArray();
            }
            finally
            {
                foreach (var b in hidden) b.Show();
                Destroy(go);
                if (rt != null)
                {
                    rt.Release();
                    Destroy(rt);
                }
                if (tex != null) Destroy(tex);
            }

            shutter.Play();
            var fileName = "Play_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var settings = (brighten, maxGain, gamma, contrast, colour, save4K, folder);
            // Brightening, shrinking and PNG encoding happen off the main thread, so the headset doesn't stutter.
            Task.Run(() => Save(pixels, fileName, settings));
        }

        private static void Save(byte[] px, string name,
            (bool brighten, float maxGain, float gamma, float contrast, float colour, bool save4K, string folder) s)
        {
            try
            {
                if (s.brighten) Brighten(px, s.maxGain, s.gamma, s.contrast, s.colour);
                Directory.CreateDirectory(s.folder);
                if (s.save4K)
                {
                    var dir4K = Path.Combine(s.folder, "4K");
                    Directory.CreateDirectory(dir4K);
                    File.WriteAllBytes(Path.Combine(dir4K, name + "_4K.png"),
                        ImageConversion.EncodeArrayToPNG(px, GraphicsFormat.R8G8B8A8_UNorm, Width, Height));
                }
                var small = HalfSize(px, (int)Width, (int)Height);
                var file = Path.Combine(s.folder, name + ".png");
                File.WriteAllBytes(file,
                    ImageConversion.EncodeArrayToPNG(small, GraphicsFormat.R8G8B8A8_UNorm, Width / 2, Height / 2));
                Debug.Log("[Photo] Saved " + file);
            }
            catch (Exception e)
            {
                Debug.LogError("[Photo] Couldn't save the photo: " + e.Message);
            }
        }

        /// <summary>
        /// Lift the exposure so the brightest 0.5% reach ~93% white (up to maxGain), open the shadows with gamma, then a
        /// touch of contrast and colour. Works on RGBA bytes in place.
        /// </summary>
        private static void Brighten(byte[] px, float maxGain, float gamma, float contrast, float colour)
        {
            int n = px.Length / 4;
            var histogram = new int[256];
            for (int i = 0; i < px.Length; i += 4)
                histogram[(int)(0.2126f * px[i] + 0.7152f * px[i + 1] + 0.0722f * px[i + 2])]++;
            int bright = 255, count = 0;
            for (; bright > 0; bright--)
            {
                count += histogram[bright];
                if (count >= n * 0.005f) break;
            }
            float gain = Mathf.Min(maxGain, 0.93f / Mathf.Max(bright / 255f, 0.001f));

            var curve = new float[256];
            for (int v = 0; v < 256; v++) curve[v] = Mathf.Pow(Mathf.Clamp01(v / 255f * gain), gamma) * 255f;

            double sum = 0;
            for (int i = 0; i < px.Length; i += 4)
                sum += 0.299f * curve[px[i]] + 0.587f * curve[px[i + 1]] + 0.114f * curve[px[i + 2]];
            float mean = (float)(sum / n);

            for (int i = 0; i < px.Length; i += 4)
            {
                float r = mean + (curve[px[i]] - mean) * contrast;
                float g = mean + (curve[px[i + 1]] - mean) * contrast;
                float b = mean + (curve[px[i + 2]] - mean) * contrast;
                float grey = 0.299f * r + 0.587f * g + 0.114f * b;
                px[i] = ToByte(grey + (r - grey) * colour);
                px[i + 1] = ToByte(grey + (g - grey) * colour);
                px[i + 2] = ToByte(grey + (b - grey) * colour);
                px[i + 3] = 255;
            }
        }

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);

        /// <summary>Average each 2x2 block: a 4K render shrunk to 1080p is smoother than rendering 1080p directly.</summary>
        private static byte[] HalfSize(byte[] px, int w, int h)
        {
            int hw = w / 2, hh = h / 2;
            var output = new byte[hw * hh * 4];
            for (int y = 0; y < hh; y++)
            for (int x = 0; x < hw; x++)
            {
                int a = ((2 * y) * w + 2 * x) * 4, b = a + w * 4, o = (y * hw + x) * 4;
                for (int c = 0; c < 3; c++)
                    output[o + c] = (byte)((px[a + c] + px[a + 4 + c] + px[b + c] + px[b + 4 + c] + 2) / 4);
                output[o + 3] = 255;
            }
            return output;
        }

        private interface IHidden
        {
            void Show();
        }

        private sealed class HiddenRenderer : IHidden
        {
            private readonly Renderer r;
            public HiddenRenderer(Renderer r) { this.r = r; r.enabled = false; }
            public void Show() { if (r != null) r.enabled = true; }
        }

        private sealed class HiddenCanvas : IHidden
        {
            private readonly Canvas c;
            public HiddenCanvas(Canvas c) { this.c = c; c.enabled = false; }
            public void Show() { if (c != null) c.enabled = true; }
        }

        /// <summary>
        /// Hide what only makes sense inside the headset for the length of the render: everything fixed to the head
        /// (comfort vignette, damage lines), the rig's UI and the warning panels; with Hide Hands, the whole rig.
        /// </summary>
        private List<IHidden> HideOverlays(Camera main)
        {
            var hidden = new List<IHidden>();
            var roots = new List<Transform> { main.transform };
            foreach (var w in FindObjectsByType<Menus.HealthWarning>(FindObjectsSortMode.None)) roots.Add(w.transform);
            foreach (var w in FindObjectsByType<Menus.CongratulationWarning>(FindObjectsSortMode.None)) roots.Add(w.transform);
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin != null)
            {
                foreach (var c in origin.GetComponentsInChildren<Canvas>())
                    if (c.enabled) hidden.Add(new HiddenCanvas(c));
                if (hideHands) roots.Add(origin.transform);
            }
            foreach (var root in roots)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    if (r.enabled) hidden.Add(new HiddenRenderer(r));
                foreach (var c in root.GetComponentsInChildren<Canvas>())
                    if (c.enabled) hidden.Add(new HiddenCanvas(c));
            }
            return hidden;
        }

        /// <summary>A short two-part mechanical click, made in code so there's no asset to ship.</summary>
        private static AudioClip MakeShutterClick()
        {
            const int rate = 44100;
            var samples = new float[(int)(rate * 0.12f)];
            var random = new System.Random(429);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float first = t < 0.05f ? Mathf.Exp(-t * 140f) : 0f;
                float second = t >= 0.06f ? Mathf.Exp(-(t - 0.06f) * 110f) * 0.7f : 0f;
                samples[i] = ((float)random.NextDouble() * 2f - 1f) * (first + second) * 0.6f;
            }
            var clip = AudioClip.Create("Photo Shutter", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
#endif
