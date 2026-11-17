using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using GdiBitmap = System.Drawing.Bitmap;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using DFRContentHost.ViewModels;

namespace DFRContentHost.Interop
{
    public static class CompanionSpriteHelper
    {
        private static readonly Dictionary<string, AvaBitmap> _cache = new Dictionary<string, AvaBitmap>(StringComparer.OrdinalIgnoreCase);
        public static string CurrentSkin { get; set; } = "Default";

        // High-performance preloaded sequences for custom characters
        private static AvaBitmap[] _skinFrames = null;
        private static AvaBitmap[] _skinFramesFlipped = null;
        private static AvaBitmap[] _walkFrames = null;
        private static AvaBitmap[] _walkFramesFlipped = null;
        private static AvaBitmap[] _idleFrames = null;
        private static AvaBitmap[] _idleFramesFlipped = null;

        public static void SetSkin(string skin)
        {
            CurrentSkin = string.IsNullOrWhiteSpace(skin) ? "Default" : skin;
            _cache.Clear();
            _skinFrames = null;
            _skinFramesFlipped = null;
            _walkFrames = null;
            _walkFramesFlipped = null;
            _idleFrames = null;
            _idleFramesFlipped = null;
            PreloadAllSprites();
        }

        public static void PreloadAllSprites()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. If custom skin is active, preload its frames into RAM with horizontal flip pairs
            if (!string.Equals(CurrentSkin, "Default", StringComparison.OrdinalIgnoreCase))
            {
                string[] searchDirs = new[]
                {
                    Path.Combine(baseDir, "pets", CurrentSkin),
                    Path.Combine(baseDir, "HostApp_v2", "pets", CurrentSkin),
                    Path.Combine(baseDir, "..", "pets", CurrentSkin),
                    Path.Combine(Directory.GetCurrentDirectory(), "pets", CurrentSkin)
                };

                string petFolder = null;
                foreach (var sDir in searchDirs)
                {
                    if (Directory.Exists(sDir))
                    {
                        petFolder = sDir;
                        break;
                    }
                }

                if (petFolder != null)
                {
                    try
                    {
                        var walkFiles = Directory.GetFiles(petFolder, "walk_*.png");
                        var idleFiles = Directory.GetFiles(petFolder, "idle_*.png");

                        if (walkFiles.Length > 0)
                        {
                            Array.Sort(walkFiles, (a, b) => ExtractFrameIndex(a).CompareTo(ExtractFrameIndex(b)));
                            LoadFramePairList(walkFiles, out _walkFrames, out _walkFramesFlipped);
                        }

                        if (idleFiles.Length > 0)
                        {
                            Array.Sort(idleFiles, (a, b) => ExtractFrameIndex(a).CompareTo(ExtractFrameIndex(b)));
                            LoadFramePairList(idleFiles, out _idleFrames, out _idleFramesFlipped);
                        }

                        if (_walkFrames == null && _idleFrames == null)
                        {
                            var files = Directory.GetFiles(petFolder, "*.png");
                            if (files.Length > 0)
                            {
                                Array.Sort(files, (a, b) => ExtractFrameIndex(a).CompareTo(ExtractFrameIndex(b)));
                                LoadFramePairList(files, out _skinFrames, out _skinFramesFlipped);
                            }
                        }
                    }
                    catch { }
                }
            }

            var dir = Path.Combine(baseDir, "Assets", "Companion");
            if (!Directory.Exists(dir))
            {
                dir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Companion");
            }

            if (Directory.Exists(dir))
            {
                try
                {
                    var files = Directory.GetFiles(dir, "*.png");
                    foreach (var file in files)
                    {
                        try
                        {
                            string name = Path.GetFileNameWithoutExtension(file);
                            if (!_cache.ContainsKey(name))
                            {
                                using var stream = File.OpenRead(file);
                                _cache[name] = new AvaBitmap(stream);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // Pre-generate / warm up procedural and flip sprites so runtime never touches disk or GDI+
            string[] proceduralNames = {
                "idle_0", "idle_1", "idle_2", "idle_3", "idle_4", "idle_5", "idle_6", "idle_7", "idle_8", "idle_9",
                "idle_10", "idle_11", "idle_12", "idle_13", "idle_14", "idle_15", "idle_16", "idle_17", "idle_18", "idle_19",
                "dance_0", "dance_1", "dance_2", "dance_3",
                "drag_0", "drag_1", "drag_2",
                "type_1", "type_2",
                "music_1", "music_2",
                "sleep_0", "sleep_1", "sleep_2",
                "pet_1", "pet_2",
                "emote_heart", "emote_note", "emote_sleep", "emote_spark"
            };

            foreach (var pName in proceduralNames)
            {
                GetSprite(pName);
                GetSprite(pName + "_flip");
            }
        }

        private static void LoadFramePairList(string[] files, out AvaBitmap[] regular, out AvaBitmap[] flipped)
        {
            var loaded = new List<AvaBitmap>();
            var loadedFlipped = new List<AvaBitmap>();

            foreach (var file in files)
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    loaded.Add(new AvaBitmap(stream));

                    // Pre-generate flipped version
                    using var gdi = new System.Drawing.Bitmap(file);
                    gdi.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    using var ms = new MemoryStream();
                    gdi.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    loadedFlipped.Add(new AvaBitmap(ms));
                }
                catch { }
            }

            regular = loaded.Count > 0 ? loaded.ToArray() : null;
            flipped = loadedFlipped.Count > 0 ? loadedFlipped.ToArray() : null;
        }

        private static int ExtractFrameIndex(string path)
        {
            try
            {
                string fn = Path.GetFileNameWithoutExtension(path);
                int lastUnder = fn.LastIndexOf('_');
                if (lastUnder >= 0 && int.TryParse(fn.Substring(lastUnder + 1), out int val))
                {
                    return val;
                }
            }
            catch { }
            return 0;
        }

        public static AvaBitmap GetSprite(string name)
        {
            // For custom characters, stream dedicated walk, idle, or generic skin frames
            if (!name.StartsWith("emote_", StringComparison.OrdinalIgnoreCase))
            {
                bool isFlip = name.EndsWith("_flip", StringComparison.OrdinalIgnoreCase) ||
                              name.StartsWith("walk_left", StringComparison.OrdinalIgnoreCase);

                bool isWalking = name.Contains("walk", StringComparison.OrdinalIgnoreCase) ||
                                 name.Contains("run", StringComparison.OrdinalIgnoreCase);

                if (isWalking && _walkFrames != null && _walkFrames.Length > 0)
                {
                    int speed = 3; // ~10 FPS walk cycle at 30 FPS tick
                    int frameIdx = (CompanionViewModel.CurrentFrameTick / speed) % _walkFrames.Length;
                    return isFlip ? _walkFramesFlipped[frameIdx] : _walkFrames[frameIdx];
                }
                else if (!isWalking && _idleFrames != null && _idleFrames.Length > 0)
                {
                    int speed = 4; // ~7.5 FPS gentle idle cycle
                    int frameIdx = (CompanionViewModel.CurrentFrameTick / speed) % _idleFrames.Length;
                    return isFlip ? _idleFramesFlipped[frameIdx] : _idleFrames[frameIdx];
                }
                else if (_skinFrames != null && _skinFrames.Length > 0)
                {
                    int speed = 2; // ~15 FPS at 30 FPS tick
                    if (name.Contains("idle", StringComparison.OrdinalIgnoreCase)) speed = 3;
                    else if (name.Contains("type", StringComparison.OrdinalIgnoreCase)) speed = 1;

                    int frameIdx = (CompanionViewModel.CurrentFrameTick / speed) % _skinFrames.Length;
                    return isFlip ? _skinFramesFlipped[frameIdx] : _skinFrames[frameIdx];
                }
            }

            if (_cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            // 1. Try loading from active skin folder (pets/ or Assets/)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPath = null;

            if (!string.Equals(CurrentSkin, "Default", StringComparison.OrdinalIgnoreCase))
            {
                var petPath = Path.Combine(baseDir, "pets", CurrentSkin, $"{name}.png");
                if (File.Exists(petPath)) localPath = petPath;
                else
                {
                    var assetSkinPath = Path.Combine(baseDir, "Assets", CurrentSkin, $"{name}.png");
                    if (File.Exists(assetSkinPath)) localPath = assetSkinPath;
                    else
                    {
                        // Check if skin folder has frame_X.png
                        var petFolder = Path.Combine(baseDir, "pets", CurrentSkin);
                        if (Directory.Exists(petFolder))
                        {
                            var frames = Directory.GetFiles(petFolder, "*.png");
                            if (frames.Length > 0)
                            {
                                int hash = Math.Abs(name.GetHashCode()) % frames.Length;
                                localPath = frames[hash];
                            }
                        }
                    }
                }
            }

            if (localPath == null)
            {
                localPath = Path.Combine(baseDir, "Assets", "Companion", $"{name}.png");
                if (!File.Exists(localPath))
                {
                    localPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Companion", $"{name}.png");
                }
            }

            if (File.Exists(localPath))
            {
                try
                {
                    using var stream = File.OpenRead(localPath);
                    var bmp = new AvaBitmap(stream);
                    _cache[name] = bmp;
                    return bmp;
                }
                catch { }
            }

            // If name ends with _flip, try loading base and flipping horizontally
            if (name.EndsWith("_flip"))
            {
                string baseName = name.Substring(0, name.Length - 5);
                var baseLocalPath = Path.Combine(baseDir, "Assets", "Companion", $"{baseName}.png");
                if (!File.Exists(baseLocalPath))
                {
                    baseLocalPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Companion", $"{baseName}.png");
                }
                if (File.Exists(baseLocalPath))
                {
                    try
                    {
                        using var gdi = new System.Drawing.Bitmap(baseLocalPath);
                        gdi.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        using var ms = new MemoryStream();
                        gdi.Save(ms, ImageFormat.Png);
                        ms.Position = 0;
                        var bmp = new AvaBitmap(ms);
                        _cache[name] = bmp;
                        return bmp;
                    }
                    catch { }
                }
            }

            // 2. Generate procedurally in memory if file not found
            try
            {
                var generated = GenerateFrame(name);
                if (generated != null)
                {
                    _cache[name] = generated;
                    return generated;
                }
            }
            catch { }

            return null;
        }

        private static AvaBitmap GenerateFrame(string name)
        {
            float dx = 0, dy = 0, earTwitch = 0;
            string eyeState = "open", mouthState = "w", pawState = "chest", particle = "none";
            bool headphones = false, keyboard = false;

            switch (name)
            {
                case "idle_1":
                    break;
                case "idle_2":
                    eyeState = "blink";
                    break;
                case "idle_3":
                    earTwitch = -2.0f;
                    break;
                case "type_1":
                    dy = 1; eyeState = "happy"; mouthState = "open"; pawState = "type_left"; keyboard = true;
                    break;
                case "type_2":
                    dy = 1; eyeState = "happy"; mouthState = "open"; pawState = "type_right"; keyboard = true;
                    break;
                case "music_1":
                    dx = -1; dy = 1; eyeState = "happy"; pawState = "music"; headphones = true; particle = "note1";
                    break;
                case "music_2":
                    dx = 1; dy = -1; eyeState = "happy"; mouthState = "open"; pawState = "music"; headphones = true; particle = "note2";
                    break;
                case "pet_1":
                    eyeState = "happy"; mouthState = "open"; pawState = "petted"; particle = "heart1";
                    break;
                case "pet_2":
                    dy = -3; eyeState = "happy"; mouthState = "open"; pawState = "petted"; particle = "heart2";
                    break;
                case "sleep_1":
                    dy = 1; eyeState = "sleep"; mouthState = "sleep"; pawState = "sleep"; particle = "z1";
                    break;
                case "sleep_2":
                    dy = 2; eyeState = "sleep"; mouthState = "sleep"; pawState = "sleep"; particle = "z2";
                    break;
            }

            using var bmp = new System.Drawing.Bitmap(64, 64, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                using var skinBrush = new SolidBrush(Color.FromArgb(255, 238, 227));
                using var hairBrush = new SolidBrush(Color.FromArgb(238, 180, 220));
                using var hairDarkBrush = new SolidBrush(Color.FromArgb(208, 145, 190));
                using var innerEarBrush = new SolidBrush(Color.FromArgb(255, 185, 205));
                using var blushBrush = new SolidBrush(Color.FromArgb(180, 255, 130, 160));
                using var clothBrush = new SolidBrush(Color.FromArgb(35, 35, 45));
                using var clothTrimBrush = new SolidBrush(Color.FromArgb(0, 229, 255));
                using var eyeDarkBrush = new SolidBrush(Color.FromArgb(35, 25, 55));
                using var eyeBlueBrush = new SolidBrush(Color.FromArgb(70, 170, 250));
                using var eyeSparkleBrush = new SolidBrush(Color.FromArgb(140, 230, 255));
                using var whiteBrush = new SolidBrush(Color.White);
                using var mouthPen = new Pen(Color.FromArgb(170, 75, 95), 1.6f);
                using var eyePen = new Pen(Color.FromArgb(40, 25, 50), 2.0f);
                using var neonCyanBrush = new SolidBrush(Color.FromArgb(0, 230, 255));
                using var neonPinkBrush = new SolidBrush(Color.FromArgb(255, 60, 150));
                using var neonYellowBrush = new SolidBrush(Color.FromArgb(255, 225, 50));
                using var headphonePen = new Pen(Color.FromArgb(40, 40, 50), 3.0f);

                // Back hair
                g.FillEllipse(hairDarkBrush, 14 + dx, 18 + dy, 36, 40);

                // Cat ears
                PointF[] leftEar = {
                    new PointF(15 + dx + earTwitch, 22 + dy),
                    new PointF(19 + dx + earTwitch * 1.5f, 6 + dy),
                    new PointF(29 + dx, 18 + dy)
                };
                PointF[] rightEar = {
                    new PointF(35 + dx, 18 + dy),
                    new PointF(45 + dx, 6 + dy),
                    new PointF(49 + dx, 22 + dy)
                };
                g.FillPolygon(hairBrush, leftEar);
                g.FillPolygon(hairBrush, rightEar);

                PointF[] leftInner = {
                    new PointF(18 + dx + earTwitch, 20 + dy),
                    new PointF(21 + dx + earTwitch * 1.3f, 10 + dy),
                    new PointF(27 + dx, 18 + dy)
                };
                PointF[] rightInner = {
                    new PointF(37 + dx, 18 + dy),
                    new PointF(43 + dx, 10 + dy),
                    new PointF(46 + dx, 20 + dy)
                };
                g.FillPolygon(innerEarBrush, leftInner);
                g.FillPolygon(innerEarBrush, rightInner);

                // Head
                g.FillEllipse(skinBrush, 17 + dx, 16 + dy, 30, 28);

                // Body
                g.FillEllipse(clothBrush, 19 + dx, 40 + dy, 26, 22);
                g.FillEllipse(clothTrimBrush, 28 + dx, 42 + dy, 8, 4);

                // Bangs & sides
                g.FillEllipse(hairBrush, 14 + dx, 14 + dy, 36, 20);
                g.FillEllipse(hairBrush, 13 + dx, 22 + dy, 10, 22);
                g.FillEllipse(hairBrush, 41 + dx, 22 + dy, 10, 22);

                // Hairpin
                g.FillEllipse(neonYellowBrush, 18 + dx, 20 + dy, 6, 6);

                // Headphones
                if (headphones)
                {
                    g.DrawArc(headphonePen, 16 + dx, 10 + dy, 32, 28, 180, 180);
                    g.FillEllipse(clothBrush, 11 + dx, 22 + dy, 8, 16);
                    g.FillEllipse(neonCyanBrush, 12 + dx, 25 + dy, 5, 10);
                    g.FillEllipse(clothBrush, 45 + dx, 22 + dy, 8, 16);
                    g.FillEllipse(neonCyanBrush, 47 + dx, 25 + dy, 5, 10);
                }

                // Eyes
                if (eyeState == "open")
                {
                    g.FillEllipse(eyeDarkBrush, 23 + dx, 25 + dy, 6, 8);
                    g.FillEllipse(eyeBlueBrush, 24 + dx, 28 + dy, 4, 5);
                    g.FillEllipse(whiteBrush, 24 + dx, 26 + dy, 2.5f, 3);
                    g.FillEllipse(eyeSparkleBrush, 26 + dx, 30 + dy, 1.5f, 1.5f);

                    g.FillEllipse(eyeDarkBrush, 35 + dx, 25 + dy, 6, 8);
                    g.FillEllipse(eyeBlueBrush, 36 + dx, 28 + dy, 4, 5);
                    g.FillEllipse(whiteBrush, 36 + dx, 26 + dy, 2.5f, 3);
                    g.FillEllipse(eyeSparkleBrush, 38 + dx, 30 + dy, 1.5f, 1.5f);
                }
                else if (eyeState == "blink")
                {
                    g.DrawArc(eyePen, 23 + dx, 27 + dy, 6, 4, 0, 180);
                    g.DrawArc(eyePen, 35 + dx, 27 + dy, 6, 4, 0, 180);
                }
                else if (eyeState == "happy")
                {
                    PointF[] lp = { new PointF(23 + dx, 26 + dy), new PointF(28 + dx, 28 + dy), new PointF(23 + dx, 30 + dy) };
                    PointF[] rp = { new PointF(41 + dx, 26 + dy), new PointF(36 + dx, 28 + dy), new PointF(41 + dx, 30 + dy) };
                    g.DrawLines(eyePen, lp);
                    g.DrawLines(eyePen, rp);
                }
                else if (eyeState == "sleep")
                {
                    g.DrawLine(eyePen, 23 + dx, 28 + dy, 29 + dx, 28 + dy);
                    g.DrawLine(eyePen, 35 + dx, 28 + dy, 41 + dx, 28 + dy);
                }

                // Blush
                g.FillEllipse(blushBrush, 20 + dx, 31 + dy, 5, 3);
                g.FillEllipse(blushBrush, 39 + dx, 31 + dy, 5, 3);

                // Mouth
                if (mouthState == "w")
                {
                    g.DrawArc(mouthPen, 29 + dx, 31 + dy, 3, 3, 0, 180);
                    g.DrawArc(mouthPen, 32 + dx, 31 + dy, 3, 3, 0, 180);
                }
                else if (mouthState == "open")
                {
                    g.FillEllipse(mouthPen.Brush, 30 + dx, 31 + dy, 4, 4);
                }
                else if (mouthState == "sleep")
                {
                    g.DrawLine(mouthPen, 30 + dx, 33 + dy, 34 + dx, 33 + dy);
                }

                // Keyboard
                if (keyboard)
                {
                    using var kbBrush = new SolidBrush(Color.FromArgb(25, 25, 35));
                    using var kbOutline = new Pen(Color.FromArgb(60, 60, 80), 1.0f);
                    g.FillRectangle(kbBrush, 14, 50, 36, 12);
                    g.DrawRectangle(kbOutline, 14, 50, 36, 12);

                    g.FillRectangle(neonCyanBrush, 16, 52, 6, 3);
                    g.FillRectangle(neonPinkBrush, 24, 52, 6, 3);
                    g.FillRectangle(neonYellowBrush, 32, 52, 6, 3);
                    g.FillRectangle(neonCyanBrush, 40, 52, 8, 3);

                    g.FillRectangle(neonPinkBrush, 16, 57, 10, 3);
                    g.FillRectangle(neonCyanBrush, 28, 57, 12, 3);
                    g.FillRectangle(neonYellowBrush, 42, 57, 6, 3);
                }

                // Paws
                if (pawState == "chest")
                {
                    g.FillEllipse(skinBrush, 25 + dx, 45 + dy, 6, 5);
                    g.FillEllipse(skinBrush, 33 + dx, 45 + dy, 6, 5);
                }
                else if (pawState == "type_left")
                {
                    g.FillEllipse(skinBrush, 20, 49, 7, 6);
                    g.FillEllipse(skinBrush, 36, 44, 7, 6);
                    g.FillEllipse(neonYellowBrush, 18, 47, 4, 4);
                }
                else if (pawState == "type_right")
                {
                    g.FillEllipse(skinBrush, 20, 44, 7, 6);
                    g.FillEllipse(skinBrush, 36, 49, 7, 6);
                    g.FillEllipse(neonYellowBrush, 39, 47, 4, 4);
                }
                else if (pawState == "music")
                {
                    g.FillEllipse(skinBrush, 22 + dx, 42 + dy, 6, 6);
                    g.FillEllipse(skinBrush, 36 + dx, 42 + dy, 6, 6);
                }
                else if (pawState == "petted")
                {
                    g.FillEllipse(skinBrush, 16 + dx, 36 + dy, 8, 8);
                    g.FillEllipse(innerEarBrush, 18 + dx, 38 + dy, 4, 4);
                    g.FillEllipse(skinBrush, 40 + dx, 36 + dy, 8, 8);
                    g.FillEllipse(innerEarBrush, 42 + dx, 38 + dy, 4, 4);
                }
                else if (pawState == "sleep")
                {
                    g.FillEllipse(skinBrush, 28 + dx, 42 + dy, 10, 6);
                }

                // Particles
                if (particle == "heart1")
                {
                    g.FillEllipse(neonPinkBrush, 43, 6, 5, 5);
                    g.FillEllipse(neonPinkBrush, 47, 6, 5, 5);
                    PointF[] tri = { new PointF(42, 8), new PointF(53, 8), new PointF(47.5f, 14) };
                    g.FillPolygon(neonPinkBrush, tri);
                }
                else if (particle == "heart2")
                {
                    g.FillEllipse(neonPinkBrush, 43, 4, 5, 5);
                    g.FillEllipse(neonPinkBrush, 47, 4, 5, 5);
                    PointF[] tri1 = { new PointF(42, 6), new PointF(53, 6), new PointF(47.5f, 12) };
                    g.FillPolygon(neonPinkBrush, tri1);

                    g.FillEllipse(neonPinkBrush, 10, 10, 4, 4);
                    g.FillEllipse(neonPinkBrush, 13, 10, 4, 4);
                    PointF[] tri2 = { new PointF(9, 12), new PointF(18, 12), new PointF(13.5f, 17) };
                    g.FillPolygon(neonPinkBrush, tri2);
                }
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            return new AvaBitmap(ms);
        }
    }
}
