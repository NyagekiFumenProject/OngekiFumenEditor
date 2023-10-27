using OngekiFumenEditor.Base.Attributes;
using SharpVectors.Renderers.Wpf;
using SvgConverter;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects.Svg
{
	public class SvgImageFilePrefab : SvgPrefabBase
	{
		public const string CommandName = "[SVG_IMG]";
		public override string IDShortName => CommandName;

		private FileInfo svgFile = null;
		[ObjectPropertyBrowserSingleSelectedOnly]
		public FileInfo SvgFile
		{
			get => svgFile;
			set => Set(ref svgFile, value);
		}

		public SvgImageFilePrefab() : base()
		{

		}

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not SvgImageFilePrefab from)
				return;

			if (from.SvgFile?.FullName is string path)
				SvgFile = new FileInfo(path);
		}

		public override void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
		{
			switch (propertyName)
			{
				case nameof(SvgFile):
					ReloadSvgFile();
					break;
				default:
					base.NotifyOfPropertyChange(propertyName);
					break;
			}
		}

		public void ReloadSvgFile()
		{
			if (SvgFile is null)
			{
				CleanGeometry();
				return;
			}

			var svgContent = ConverterLogic.ConvertSvgToObject(SvgFile.FullName, ResultMode.DrawingGroup, new WpfDrawingSettings()
			{
				IncludeRuntime = false,
				TextAsGeometry = true,
				OptimizePath = true,
				EnsureViewboxSize = true
			}, out _, new()) as DrawingGroup;
			svgContent.Freeze();

			RebuildGeometry();

			ApplySvgContent(svgContent);
		}

		public override string ToString() => $"{base.ToString()} File[{Path.GetFileName(SvgFile?.Name)}]";
	}
}
