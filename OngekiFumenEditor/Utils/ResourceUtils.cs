using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace OngekiFumenEditor.Utils
{
    public static class ResourceUtils
    {
        private static Dictionary<string, string> textureSizeOriginMap = new();

        public static Stream OpenReadFromLocalAssemblyEmbbedResources(string resourceName)
            => typeof(ResourceUtils).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources." + resourceName);

        public static Stream OpenReadResourceStream(string relativeUrl)
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(relativeUrl, UriKind.Relative));
            return info.Stream;
        }

        public static IImage OpenReadTextureFromResource(IRenderManagerImpl impl, string relativeUrl)
        {
            using var stream = OpenReadResourceStream(relativeUrl);
            return impl.LoadImageFromStream(stream);
        }

        public static IImage OpenReadTextureFromFile(IRenderManagerImpl impl, string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return impl.LoadImageFromStream(stream);
        }

        static ResourceUtils()
        {
            var iniFilePath = Path.GetFullPath(@".\Resources\editor\textureSizeAnchor.ini");
            foreach (var line in File.ReadAllLines(iniFilePath))
            {
                var split = line.Split("=");
                if (split.Length != 2)
                    continue;

                textureSizeOriginMap[split[0]] = split[1];
            }
        }

        public static string ReadTextureSizeAnchor(string key)
        {
            if (textureSizeOriginMap.TryGetValue(key, out var val))
                return val;
            return string.Empty;
        }

        public static bool OpenReadTextureSizeAnchorByConfigFile(string textureName, out Vector2 size, out Vector2 anchor)
        {
            size = default;
            anchor = default;
            var good = false;

            try
            {
                var key = textureName + "Size";
                var str = ReadTextureSizeAnchor(key);
                if (!string.IsNullOrWhiteSpace(str))
                {
                    var split = str.Split(',');
                    size = new(float.Parse(split[0].Trim()), float.Parse(split[1].Trim()));
                    good = true;
                }
                else
                {
                    Log.LogWarn($"size key {key} is not found.");
                }

                key = textureName + "Anchor";
                str = ReadTextureSizeAnchor(key);
                if (!string.IsNullOrWhiteSpace(str))
                {
                    var split = str.Split(',');
                    anchor = new(float.Parse(split[0].Trim()), float.Parse(split[1].Trim()));
                }
                else
                {
                    //Log.LogWarn($"anchor key {key} is not found.");
                }

                return good;
            }
            catch (Exception e)
            {
                //todo log
                return false;
            }
        }
    }
}
