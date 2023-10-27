using Caliburn.Micro;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs
{
	public class FumenData : PropertyChangedBase
	{
		private float level;
		public float Level
		{
			get => level;
			set => Set(ref level, value);
		}

		private bool enable = false;
		public bool Enable
		{
			get => enable;
			set => Set(ref enable, value);
		}

		private string fileName = null;
		public string FileName
		{
			get => fileName;
			set => Set(ref fileName, value);
		}
	}
}
