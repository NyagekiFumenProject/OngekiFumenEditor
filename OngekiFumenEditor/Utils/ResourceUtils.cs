using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Texture = OngekiFumenEditor.Kernel.Graphics.Base.Texture;

namespace OngekiFumenEditor.Utils
{
    public static class ResourceUtils
    {
        public static Stream OpenReadFromLocalAssemblyResourcesFolder(string resourceName)
            => typeof(ResourceUtils).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources." + resourceName);

        public static Stream OpenReadResourceStream(string relativeUrl)
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(relativeUrl, UriKind.Relative));
            return info.Stream;
        }

        public static Texture OpenReadTextureFromResource(string relativeUrl)
        {
            using var stream = OpenReadResourceStream(relativeUrl);
            using var bitmap = Image.FromStream(stream) as Bitmap;
            return new Texture(bitmap);
        }

        public static Texture OpenReadTextureFromFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var bitmap = Image.FromStream(stream) as Bitmap;
            return new Texture(bitmap);
        }

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern int GetPrivateProfileString(
        string section, string key, string defaultValue,
        StringBuilder retVal, int size, string filePath);

        public static string ReadIniConfig(string filePath, string section, string key)
        {
            var result = new StringBuilder(255);

            try
            {
                GetPrivateProfileString(section, key, string.Empty, result, 255, filePath);
                return result.ToString();
            }
            catch (Exception e)
            {
                Log.LogError($"Read .ini file {filePath} failed: {e.Message}");
                return default;
            }
        }

        public static bool OpenReadTextureSizeOriginByConfigFile(string iniFilePath, string textureName, out Vector2 size, out Vector2 origin)
        {
            size = default;
            origin = default;

            try
            {
                var str = ReadIniConfig(iniFilePath, "TextureSizeOrigin", textureName + "Size");
                var split = str.Split(',');
                size = new(float.Parse(split[0]), float.Parse(split[1]));

                str = ReadIniConfig(iniFilePath, "TextureSizeOrigin", textureName + "Origin");
                split = str.Split(',');
                origin = new(float.Parse(split[0]), float.Parse(split[1]));
                return true;
            }
            catch (Exception e)
            {
                //todo log
                return false;
            }
        }
    }
}
