using Caliburn.Micro;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models
{
	public class JacketGenerateOption : PropertyChangedBase
	{
		private int musicId = -1;
		public int MusicId
		{
			get => musicId; set => Set(ref musicId, value);
		}

		private string outputAssetbundleFilePath;
		public string OutputAssetbundleFolderPath
		{
			get => outputAssetbundleFilePath; set => Set(ref outputAssetbundleFilePath, value);
		}

		private string inputImageFilePath;
		public string InputImageFilePath
		{
			get => inputImageFilePath; set => Set(ref inputImageFilePath, value);
		}

		private int width = 520;
		public int Width
		{
			get => width; set => Set(ref width, value);
		}

		private int height = 520;
		public int Height
		{
			get => height; set => Set(ref height, value);
		}

		private int widthSmall = 220;
		public int WidthSmall
		{
			get => widthSmall; set => Set(ref widthSmall, value);
		}

		private int heightSmall = 220;
		public int HeightSmall
		{
			get => heightSmall; set => Set(ref heightSmall, value);
		}

		private bool updateAssetBytesFile = true;
		public bool UpdateAssetBytesFile
		{
			get => updateAssetBytesFile; set => Set(ref updateAssetBytesFile, value);
		}
	}
}
