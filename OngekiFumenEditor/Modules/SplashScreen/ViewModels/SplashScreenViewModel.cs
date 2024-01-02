using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Commands;
using Gemini.Framework.Languages;
using Gemini.Framework.Services;
using Gemini.Modules.Shell.Commands;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.OpenUrlCommon;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Kernel.RecentFiles.Commands;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Gemini.Modules.Shell.Commands.NewFileCommandHandler;

namespace OngekiFumenEditor.Modules.SplashScreen.ViewModels
{
	[Export(typeof(ISplashScreenWindow))]
	public class SplashScreenViewModel : WindowBase, ISplashScreenWindow
	{
		public ObservableCollection<string> Languages { get; } = new ObservableCollection<string>();

		public string initLanguage;
		public string SelectedLanguage
		{
			get { return languageManager.GetCurrentLanguage(); }
			set
			{
				languageManager.SetLanguage(value);

				NotifyOfPropertyChange(() => SelectedLanguage);
				NotifyOfPropertyChange(() => IsRequestRestartProgram);
			}
		}

		public bool IsRequestRestartProgram => initLanguage != SelectedLanguage;

		public bool DisableShowSplashScreenAfterBoot
		{
			get => ProgramSetting.Default.DisableShowSplashScreenAfterBoot;
			set
			{
				ProgramSetting.Default.DisableShowSplashScreenAfterBoot = value;
				ProgramSetting.Default.Save();
				NotifyOfPropertyChange(() => DisableShowSplashScreenAfterBoot);
			}
		}

		public ObservableCollection<GroupedItem> GroupedItems { get; } = new();

		private static readonly (TimeSpan, string)[] checkList = new[]
		{
			(TimeSpan.FromMinutes(15),"刚刚"),
			(TimeSpan.FromMinutes(30),"半小时前"),
			(TimeSpan.FromHours(24),"今天"),
			(TimeSpan.FromDays(7),"一个星期内"),
			(TimeSpan.FromDays(30),"一个月内"),
		};

		private readonly ILanguageManager languageManager;
		private readonly IShell shell;

		[ImportingConstructor]
		public SplashScreenViewModel(ILanguageManager languageManager, IShell shell)
		{
			initLanguage = languageManager.GetCurrentLanguage();
			Languages.Clear();
			foreach (var lang in languageManager.GetAvaliableLanguageNames())
				Languages.Add(lang);
			this.languageManager = languageManager;
			this.shell = shell;
		}

		protected override void OnViewLoaded(object view)
		{
			base.OnViewLoaded(view);

			GroupedItems.Clear();
			var recentManager = IoC.Get<IEditorRecentFilesManager>();
			foreach (var item in recentManager.RecentRecordInfos
				.GroupBy(x => GroupByDateTime(x.LastAccessTime))
				.Select(x => new GroupedItem(x.Key, x.ToArray())))
				GroupedItems.Add(item);
		}

		private string GroupByDateTime(DateTime? d)
		{
			if (d is DateTime date)
			{
				var now = DateTime.Now;
				var diff = now - date;

				foreach ((var timeSpan, var name) in checkList)
				{
					if (diff < timeSpan)
						return name;
				}
			}

			return "更早";
		}

		public async void OpenRecent(RecentRecordInfo info)
		{
			var def = new OpenRecentFileCommandListDefinition();
			await CommandRouterHelper.ExecuteCommand(new Command(def)
			{
				Tag = info
			});
		}

		public async void CreateNewProject()
		{
			var editorProvider = IoC.Get<IFumenVisualEditorProvider>();
			var def = new NewFileCommandListDefinition();

			await CommandRouterHelper.ExecuteCommand(new Command(def)
			{
				Tag = new NewFileTag()
				{
					FileType = editorProvider.FileTypes.First(),
					EditorProvider = editorProvider,
				}
			});
		}

		public async void OpenProject()
		{
			var def = new OpenFileCommandDefinition();
			await CommandRouterHelper.ExecuteCommand(new Command(def)
			{ });
		}

		public async void FastOpen()
		{
			var def = new FastOpenFumenCommandDefinition();
			await CommandRouterHelper.ExecuteCommand(new Command(def)
			{ });
		}

		public async void OpenTutorial()
		{
			var def = new UsageWikiCommandDefinition();
			await CommandRouterHelper.ExecuteCommand(new Command(def)
			{ });
		}
	}
}
