using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace TouchBarSettings
{
    public static class PetImporter
    {
        public const int TargetHeight = 50;

        public static string GetPetsDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string petsDir = Path.Combine(baseDir, "pets");
            if (!Directory.Exists(petsDir))
            {
                // Check HostApp_v2 or root
                string hostAppPets = Path.Combine(baseDir, "HostApp_v2", "pets");
                if (Directory.Exists(Path.Combine(baseDir, "HostApp_v2")))
                {
                    petsDir = hostAppPets;
                }
            }

            Directory.CreateDirectory(petsDir);
            return petsDir;
        }

        public static List<string> GetAvailableSkins()
        {
            var skins = new List<string> { "Default", "Pikachu", "Eevee", "Cat", "Dino", "Gengar", "Charmander", "Squirtle", "Psyduck", "Snorlax", "Mew" };

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] searchDirs = new[]
                {
                    GetPetsDirectory(),
                    Path.Combine(baseDir, "pets"),
                    Path.Combine(baseDir, "HostApp_v2", "pets"),
                    Path.Combine(baseDir, "..", "pets")
                };

                foreach (var dir in searchDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        var dirs = Directory.GetDirectories(dir);
                        foreach (var d in dirs)
                        {
                            string name = Path.GetFileName(d);
                            if (!skins.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)))
                            {
                                skins.Add(name);
                            }
                        }
                    }
                }
            }
            catch { }

            return skins;
        }

        public static bool ImportGif(string gifPath, string petName, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                if (string.IsNullOrWhiteSpace(petName))
                {
                    petName = Path.GetFileNameWithoutExtension(gifPath);
                }

                // Sanitize name
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    petName = petName.Replace(c, '_');
                }

                string petsDir = GetPetsDirectory();
                string targetDir = Path.Combine(petsDir, petName);
                Directory.CreateDirectory(targetDir);

                using (var img = Image.FromFile(gifPath))
                {
                    var dimension = new FrameDimension(img.FrameDimensionsList[0]);
                    int frameCount = img.GetFrameCount(dimension);

                    if (frameCount <= 0)
                    {
                        errorMessage = "GIF не содержит кадров.";
                        return false;
                    }

                    for (int i = 0; i < frameCount; i++)
                    {
                        img.SelectActiveFrame(dimension, i);
                        using (var resized = ResizeFrame(img, TargetHeight))
                        {
                            string outPath = Path.Combine(targetDir, $"frame_{i}.png");
                            resized.Save(outPath, ImageFormat.Png);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool ImportPngSprites(IEnumerable<string> pngPaths, string petName, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                if (string.IsNullOrWhiteSpace(petName))
                {
                    petName = "CustomPet_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                }

                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    petName = petName.Replace(c, '_');
                }

                string petsDir = GetPetsDirectory();
                string targetDir = Path.Combine(petsDir, petName);
                Directory.CreateDirectory(targetDir);

                int index = 0;
                foreach (var path in pngPaths)
                {
                    if (File.Exists(path))
                    {
                        using (var img = Image.FromFile(path))
                        using (var resized = ResizeFrame(img, TargetHeight))
                        {
                            string outPath = Path.Combine(targetDir, $"frame_{index++}.png");
                            resized.Save(outPath, ImageFormat.Png);
                        }
                    }
                }

                if (index == 0)
                {
                    errorMessage = "Не найдено подходящих PNG изображений.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static Bitmap ResizeFrame(Image src, int targetHeight)
        {
            float ratio = (float)src.Width / Math.Max(1, src.Height);
            int targetWidth = Math.Max(1, (int)Math.Round(targetHeight * ratio));

            var bmp = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, targetWidth, targetHeight));
            }
            return bmp;
        }
    }
}
