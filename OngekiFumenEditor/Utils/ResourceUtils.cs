using System;
using System.Drawing;
using System.IO;
using Texture = OngekiFumenEditor.Kernel.Graphics.Base.Texture;

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
