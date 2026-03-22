using OngekiFumenEditor.Base.Attributes;
using System.IO;
using System.Runtime.CompilerServices;

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
			CleanGeometry();
		}

		public override string ToString() => $"{base.ToString()} File[{Path.GetFileName(SvgFile?.Name)}]";
	}
}
