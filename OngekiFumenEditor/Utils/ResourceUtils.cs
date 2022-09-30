using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Texture = OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Texture;

namespace OngekiFumenEditor.Utils
{
    public static class ResourceUtils
    {
        public static Stream OpenReadFromLocalAssemblyResource(string resourceName) => typeof(ResourceUtils).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources." + resourceName);
    
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
    }
}
