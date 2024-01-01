using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs;
using OngekiFumenEditor.Modules.OptionGeneratorTools.ViewModels.Dialogs;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Linq;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.ViewModels
{
	[Export(typeof(IMusicXmlGenerator))]
	public class MusicXmlWindowViewModel : WindowBase, IMusicXmlGenerator
	{
		public Difficult[] Difficults => Enum.GetValues<Difficult>().OrderBy(x => x).ToArray();
		public VersionID[] VersionIDs => enumManager.Versions.Values.OrderBy(x => x.Id).ToArray();
		public Genre[] Genres => enumManager.Genres.Values.OrderBy(x => x.Id).ToArray();

		private EnumFetchManager enumManager = new EnumFetchManager();
		public EnumFetchManager EnumManager => enumManager;

		private bool isBusy;
		public bool IsBusy
		{
			get => isBusy;
			set
			{
				Set(ref isBusy, value);
				NotifyOfPropertyChange(nameof(IsEditable));
			}
		}

		private string gamePath;
		public string GamePath
		{
			get => gamePath;
			set
			{
				Set(ref gamePath, value);
				UpdateGamePath();
				NotifyOfPropertyChange(nameof(IsEditable));
			}
		}

		private Difficult currentSelectedDifficult = Difficult.Master;
		public Difficult CurrentSelectedDifficult
		{
			get => currentSelectedDifficult;
			set
			{
				Set(ref currentSelectedDifficult, value);
				NotifyOfPropertyChange(nameof(CurrentSelectedFumenData));
			}
		}

		public bool IsEditable => Directory.Exists(GamePath) && !IsBusy;

		private MusicXmlGenerateOption musicXmlOption = new();
		public MusicXmlGenerateOption MusicXmlOption
		{
			get => musicXmlOption;
			set
			{
				Set(ref musicXmlOption, value);
				CurrentSelectedDifficult = CurrentSelectedDifficult;
			}
		}

		public FumenData CurrentSelectedFumenData
			=> MusicXmlOption.FumenDatas.TryGetValue(CurrentSelectedDifficult, out var r) ?
			r :
			(MusicXmlOption.FumenDatas[CurrentSelectedDifficult] = new());

		public MusicXmlWindowViewModel()
		{
			var lastFolderPath = Properties.OptionGeneratorToolsSetting.Default.LastLoadedGameFolder;
			if (Directory.Exists(lastFolderPath))
			{
				GamePath = lastFolderPath;
			}
			else
			{
				Properties.OptionGeneratorToolsSetting.Default.LastLoadedGameFolder = default;
				Properties.OptionGeneratorToolsSetting.Default.Save();
			}
		}

		public async Task<bool> Generate(string saveFilePath, MusicXmlGenerateOption option)
		{
			try
			{
				var musicXml = MusicXmlSerialization.Serialize(option, enumManager);

				using var fs = File.Open(saveFilePath, FileMode.Create);
				using var writer = XmlWriter.Create(fs, new XmlWriterSettings()
				{
					Async = true,
					Encoding = System.Text.Encoding.UTF8,
					Indent = true
				});
				await musicXml.SaveAsync(writer, default);

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public async void Save()
		{
			var saveFilePath = FileDialogHelper.SaveFile(Resources.SaveMusicXmlFile, new[] { ("Music.xml", Resources.MusicXmlFileFormat) });
			MessageBox.Show(Resources.GenerateMusicXmlFile + (await Generate(saveFilePath, MusicXmlOption) ? Resources.Success : Resources.Fail));
		}

		public void OnOpenSelectGamePathDialog()
		{
			if (!FileDialogHelper.OpenDirectory(Resources.SelectPackageFolder, out var folderPath))
				return;
			if (!Directory.Exists(folderPath))
				return;
			GamePath = folderPath;
			Properties.OptionGeneratorToolsSetting.Default.LastLoadedGameFolder = GamePath;
			Properties.OptionGeneratorToolsSetting.Default.Save();
		}

		public async void ParseMusicXml()
		{
			var selectedXmlFile = FileDialogHelper.OpenFile(Resources.SelectMusicXmlFile, new[] { ("Music.xml", Resources.MusicXmlFileFormat) });
			if (!File.Exists(selectedXmlFile))
				return;
			IsBusy = true;
			using var fs = File.OpenRead(selectedXmlFile);
			var musicXml = await XDocument.LoadAsync(fs, LoadOptions.None, default);

			var opt = MusicXmlSerialization.Serialize(musicXml, enumManager);
			MusicXmlOption = opt;

			IsBusy = false;
		}

		private async void UpdateGamePath()
		{
			IsBusy = true;

			await enumManager.Init(GamePath);
			Refresh();

			IsBusy = false;
		}

		public async void SelectCard()
		{
			var cards = enumManager.BossCards.Values;
			var dialogViewModel = new BossCardSelectorWindowViewModel(cards, MusicXmlOption.BossCard);

			await IoC.Get<IWindowManager>().ShowDialogAsync(dialogViewModel);

			MusicXmlOption.BossCard = dialogViewModel.Selected;
		}

		public async void SelectStage()
		{
			var stages = enumManager.Stages.Values;
			var dialogViewModel = new EnumStructsSelectorWindowViewModel(stages, MusicXmlOption.Stage);

			await IoC.Get<IWindowManager>().ShowDialogAsync(dialogViewModel);

			MusicXmlOption.Stage = dialogViewModel.Selected as Stage;
		}

		public async void SelectRight()
		{
			var rights = enumManager.MusicRights.Values;
			var dialogViewModel = new EnumStructsSelectorWindowViewModel(rights, MusicXmlOption.MusicRightName);

			await IoC.Get<IWindowManager>().ShowDialogAsync(dialogViewModel);

			MusicXmlOption.MusicRightName = dialogViewModel.Selected as MusicRight;
		}
	}
}
