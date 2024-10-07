using Caliburn.Micro;
using OngekiFumenEditor.Kernel.ArgProcesser.Attributes;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models
{
	public class JacketGenerateOption : PropertyChangedBase
	{
		private int musicId = -1;
		
		[LocalizableOptionBinding<int>("musicId", "ProgramOptionMusicId", -1, true)]
		public int MusicId
		{
			get => musicId; set => Set(ref musicId, value);
		}

		private string outputAssetbundleFilePath;
		
		[LocalizableOptionBinding<string>("outputFolder", "ProgramOptionOutputFolderAsset", default, true)]
		public string OutputAssetbundleFolderPath
		{
			get => outputAssetbundleFilePath; set => Set(ref outputAssetbundleFilePath, value);
		}

		private string inputImageFilePath;
		
		[LocalizableOptionBinding<string>("inputFile", "ProgramOptionInputFileJacket", default, true)]
		public string InputImageFilePath
		{
			get => inputImageFilePath; set => Set(ref inputImageFilePath, value);
		}

		private int width = 520;
		
		[LocalizableOptionBinding<int>("outputWidth", "ProgramOptionJacketOutputWidth", 520)]
		public int Width
		{
			get => width; set => Set(ref width, value);
		}

		private int height = 520;
		
		[LocalizableOptionBinding<int>("outputHeight", "ProgramOptionJacketOutputHeight", 520)]
		public int Height
		{
			get => height; set => Set(ref height, value);
		}

		private int widthSmall = 220;
		
		[LocalizableOptionBinding<int>("outputHeightSmall", "ProgramOptionJacketOutputHeightSmall", 220)]
		public int WidthSmall
		{
			get => widthSmall; set => Set(ref widthSmall, value);
		}

		private int heightSmall = 220;
		
		[LocalizableOptionBinding<int>("outputWidthSmall", "ProgramOptionJacketOutputHeightSmall", 220)]
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
