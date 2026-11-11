using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Rebuild Boss Cracks (user, 2026-11-11: "paint the cracks myself in Blender"). Reads the
    /// user's crack painting, Assets/M_Textures/Pumpkin_Boss/Boss_Butt_Cracks_Paint.png (painted on the giant pumpkin in
    /// Blender through its CrackUV map: the same cylinder round the pumpkin the crack overlay uses, its front in the
    /// middle), and makes the crack overlay's map from it, Assets/Materials/Boss/Boss_Butt_Cracks_Map.png:
    /// - each painted line is a crack, its colour when it shows: RED first (from the first hits to about 30% cracked),
    ///   GREEN next (to about 60%), BLUE last (to about 92%, just before the pumpkin breaks). A colour that's mostly red,
    ///   green or blue counts; black is nothing (paint black to rub out); anything else is left out and counted;
    /// - each line (everything of one colour that touches) grows from its middle out to its ends over its colour's window;
    /// - a crack opens from a hairline down the middle of the line to the line's own width (the dark core), with a thin pale
    ///   lip of pumpkin flesh and a faint shadow just outside it; so only lines need painting.
    /// The old map is copied to Documents/429R_claude_backups/2026-11-11/boss_slide/cracks/ first (each run its own copy).
    /// The crack overlay reads the new map as it is (nothing else changes). Running it again gives the same map.
    /// </summary>
    public static class PumpkinBossCracksMenu
    {
        private const string PaintPath = "Assets/M_Textures/Pumpkin_Boss/Boss_Butt_Cracks_Paint.png";
        // The map: half the painting's size (its distance field keeps the lines sharp).
        private const int Shrink = 2;
        // The colours' windows (crack amount: when a line starts to show at its middle, and when it reaches its ends).
        private static readonly Vector2[] Windows = { new(0.03f, 0.30f), new(0.30f, 0.60f), new(0.60f, 0.92f) };
        // The overlay's full width in its G (BossCrackOverlay's Max Width), and how far round a line the distance field
        // reaches, in the map's pixels (G = 1 there).
        private const float FullWidth = 0.3f;
        private const float ReachPixels = 6f;
        // How far round a line the painting's lines are looked for, in painting pixels.
        private const int Look = 12;
        // The corner where CrackUV puts everything that isn't the pumpkin (stray paint there is nothing).
        private const int Corner = 8;

        [MenuItem("Tools/429 Game/Enemies/Rebuild Boss Cracks")]
        public static void Rebuild()
        {
            if (!File.Exists(PaintPath))
            {
                Debug.LogError("[Boss Cracks] " + PaintPath + " is missing: paint the cracks in Blender first (see the how-to).");
                return;
            }
            var paint = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!paint.LoadImage(File.ReadAllBytes(PaintPath), false))
            {
                Debug.LogError("[Boss Cracks] " + PaintPath + " couldn't be read as an image.");
                return;
            }
            var notes = new List<string>();
            var map = Build(paint.GetPixels32(), paint.width, paint.height, notes);
            Object.DestroyImmediate(paint);
            string backup = Backup();
            File.WriteAllBytes(PumpkinBossSlideMenu.CrackMapPath, map.EncodeToPNG());
            Object.DestroyImmediate(map);
            AssetDatabase.ImportAsset(PumpkinBossSlideMenu.CrackMapPath, ImportAssetOptions.ForceUpdate);
            PumpkinBossSlideMenu.CrackMapReady(notes);
            Debug.Log("[Boss Cracks] " + PumpkinBossSlideMenu.CrackMapPath + " rebuilt from " + PaintPath + " (" + backup + "):\n" +
                      string.Join("\n", notes));
        }

        /// <summary>The crack map (R when, G from the middle, B lip) from a painting's pixels (row 0 at the bottom).</summary>
        public static Texture2D Build(Color32[] pixels, int width, int height, List<string> notes)
        {
            int n = width * height;
            // Which colour each pixel is: 0 nothing, 1 red, 2 green, 3 blue.
            var stage = new byte[n];
            int others = 0;
            var counts = new int[4];
            for (int i = 0; i < n; i++)
            {
                int x = i % width, y = i / width;
                if (x < Corner && y < Corner) continue;
                var c = pixels[i];
                int r = c.r, g = c.g, b = c.b, most = Mathf.Max(r, Mathf.Max(g, b));
                if (c.a < 128 || most < 100) continue;
                if (r == most && r >= 1.6f * Mathf.Max(g, b)) stage[i] = 1;
                else if (g == most && g >= 1.6f * Mathf.Max(r, b)) stage[i] = 2;
                else if (b == most && b >= 1.6f * Mathf.Max(r, g)) stage[i] = 3;
                else { others++; continue; }
                counts[stage[i]]++;
            }

            // Each line (one colour, touching): from its middle out along it, over its colour's window.
            var when = new float[n];
            for (int i = 0; i < n; i++) when[i] = 1f;
            var seen = new bool[n];
            var queue = new List<int>();
            var steps = new int[n];
            int lines = 0;
            for (int start = 0; start < n; start++)
            {
                if (stage[start] == 0 || seen[start]) continue;
                lines++;
                // The line's pixels.
                queue.Clear();
                queue.Add(start);
                seen[start] = true;
                for (int q = 0; q < queue.Count; q++)
                {
                    foreach (int next in Around(queue[q], width, height))
                    {
                        if (stage[next] != stage[start] || seen[next]) continue;
                        seen[next] = true;
                        queue.Add(next);
                    }
                }
                // Its middle: the pixel nearest its centre (round the wrap).
                float sx = 0f, sz = 0f, sy = 0f;
                foreach (int p in queue)
                {
                    float a = (p % width) * 2f * Mathf.PI / width;
                    sx += Mathf.Cos(a);
                    sz += Mathf.Sin(a);
                    sy += p / width;
                }
                float cx = Mathf.Repeat(Mathf.Atan2(sz, sx), 2f * Mathf.PI) * width / (2f * Mathf.PI), cy = sy / queue.Count;
                int middle = queue[0];
                float best = float.MaxValue;
                foreach (int p in queue)
                {
                    float dx = Mathf.Abs(p % width - cx);
                    dx = Mathf.Min(dx, width - dx);
                    float d = dx * dx + (p / width - cy) * (p / width - cy);
                    if (d < best) { best = d; middle = p; }
                }
                // Steps along it from its middle.
                var inLine = new HashSet<int>(queue);
                var front = new List<int> { middle };
                var done = new HashSet<int> { middle };
                steps[middle] = 0;
                int far = 0;
                for (int q = 0; q < front.Count; q++)
                {
                    int p = front[q];
                    foreach (int next in Around(p, width, height))
                    {
                        if (!inLine.Contains(next) || !done.Add(next)) continue;
                        steps[next] = steps[p] + 1;
                        far = Mathf.Max(far, steps[next]);
                        front.Add(next);
                    }
                }
                var window = Windows[stage[start] - 1];
                foreach (int p in queue)
                {
                    float along = far > 0 && done.Contains(p) ? steps[p] / (float)far : 0f;
                    when[p] = Mathf.Lerp(window.x, window.y, along);
                }
            }

            // Distances: inside a line to its edge, outside to the nearest line (and that line's time), up to Look pixels.
            var inside = new float[n];
            var outside = new float[n];
            var outsideWhen = new float[n];
            for (int i = 0; i < n; i++) { inside[i] = Look; outside[i] = Look; outsideWhen[i] = 1f; }
            for (int i = 0; i < n; i++)
            {
                bool line = stage[i] != 0;
                bool edge = false;
                foreach (int next in Beside(i, width, height))
                {
                    if ((stage[next] != 0) != line) { edge = true; break; }
                }
                if (!edge) continue;
                int x0 = i % width, y0 = i / width;
                for (int dy = -Look; dy <= Look; dy++)
                {
                    int y = y0 + dy;
                    if (y < 0 || y >= height) continue;
                    for (int dx = -Look; dx <= Look; dx++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > Look) continue;
                        int j = y * width + (int)Mathf.Repeat(x0 + dx, width);
                        if (line && stage[j] == 0 && d < outside[j]) { outside[j] = d; outsideWhen[j] = when[i]; }
                        else if (!line && stage[j] != 0 && d < inside[j]) inside[j] = d;
                    }
                }
            }
            // A line's own half width round each of its pixels (the most inside distance nearby).
            var halfWidth = new float[n];
            const int Near = 4;
            for (int i = 0; i < n; i++)
            {
                if (stage[i] == 0) continue;
                int x0 = i % width, y0 = i / width;
                float most = 0.75f;
                for (int dy = -Near; dy <= Near; dy++)
                {
                    int y = y0 + dy;
                    if (y < 0 || y >= height) continue;
                    for (int dx = -Near; dx <= Near; dx++)
                    {
                        int j = y * width + (int)Mathf.Repeat(x0 + dx, width);
                        if (stage[j] == stage[i] && inside[j] > most) most = inside[j];
                    }
                }
                halfWidth[i] = most;
            }

            // The map, Shrink times smaller: R when, G from the middle (Full Width at the line's edge), B the lip's noise.
            int w = Mathf.Max(1, width / Shrink), h = Mathf.Max(1, height / Shrink);
            float reach = ReachPixels * Shrink;
            var lip = LipNoise(w, h);
            var output = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float r = 0f, g = 0f;
                    int k = 0;
                    for (int sy = 0; sy < Shrink; sy++)
                    {
                        for (int sx = 0; sx < Shrink; sx++)
                        {
                            int px = x * Shrink + sx, py = y * Shrink + sy;
                            if (px >= width || py >= height) continue;
                            int i = py * width + px;
                            if (stage[i] != 0)
                            {
                                r += when[i];
                                g += FullWidth * Mathf.Clamp01(1f - inside[i] / halfWidth[i]);
                            }
                            else
                            {
                                r += outside[i] < Look ? outsideWhen[i] : 1f;
                                g += Mathf.Min(1f, FullWidth + outside[i] / reach);
                            }
                            k++;
                        }
                    }
                    output[y * w + x] = new Color32((byte)Mathf.RoundToInt(255f * r / k), (byte)Mathf.RoundToInt(255f * g / k),
                        (byte)Mathf.RoundToInt(255f * lip[y * w + x]), 255);
                }
            }
            var map = new Texture2D(w, h, TextureFormat.RGB24, false, true);
            map.SetPixels32(output);
            map.Apply();
            notes.Add($"{lines} lines: {counts[1]} red, {counts[2]} green, {counts[3]} blue pixels; map {w} x {h}");
            if (others > 0) notes.Add($"{others} painted pixels of other colours left out (only red, green and blue count)");
            return map;
        }

        // Where the lip shows along the cracks: a smooth noise that wraps round (sums of waves).
        public static float[] LipNoise(int w, int h)
        {
            var noise = new float[w * h];
            float lo = float.MaxValue, hi = float.MinValue;
            int[] waves = { 6, 13, 27 };
            float[] phases = { 1.3f, 4.1f, 2.6f };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float v = 0f;
                    for (int k = 0; k < waves.Length; k++)
                    {
                        v += Mathf.Sin(x * 2f * Mathf.PI * waves[k] / w + phases[k]) *
                             Mathf.Cos(y * (waves[k] * 0.9f) * 2f * Mathf.PI / h + phases[k] * 1.7f) / waves[k];
                    }
                    noise[y * w + x] = v;
                    lo = Mathf.Min(lo, v);
                    hi = Mathf.Max(hi, v);
                }
            }
            for (int i = 0; i < noise.Length; i++) noise[i] = (noise[i] - lo) / Mathf.Max(0.0001f, hi - lo);
            return noise;
        }

        // The 8 pixels round one (x wraps round the pumpkin, y doesn't).
        private static IEnumerable<int> Around(int i, int width, int height)
        {
            int x = i % width, y = i / width;
            for (int dy = -1; dy <= 1; dy++)
            {
                int yy = y + dy;
                if (yy < 0 || yy >= height) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    yield return yy * width + (x + dx + width) % width;
                }
            }
        }

        // The 4 pixels beside one.
        private static IEnumerable<int> Beside(int i, int width, int height)
        {
            int x = i % width, y = i / width;
            yield return y * width + (x + 1) % width;
            yield return y * width + (x - 1 + width) % width;
            if (y > 0) yield return i - width;
            if (y < height - 1) yield return i + width;
        }

        // A copy of the map as it is now, time-stamped (each run keeps its own).
        private static string Backup()
        {
            if (!File.Exists(PumpkinBossSlideMenu.CrackMapPath)) return "no old map";
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-11", "boss_slide", "cracks");
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, "Boss_Butt_Cracks_Map_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            File.Copy(PumpkinBossSlideMenu.CrackMapPath, to, true);
            return "old map backed up to " + to;
        }
    }
}
