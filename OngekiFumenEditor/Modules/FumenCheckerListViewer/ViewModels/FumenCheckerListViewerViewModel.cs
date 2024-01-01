using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.ViewModels
{
	[Export(typeof(IFumenCheckerListViewer))]
	public class FumenCheckerListViewerViewModel : Tool, IFumenCheckerListViewer
	{
		public override PaneLocation PreferredLocation => PaneLocation.Bottom;

		public ObservableCollection<ICheckResult> CheckResults { get; } = new ObservableCollection<ICheckResult>();

		public int ErrorCount => CheckResults.Count(x => x.Severity == RuleSeverity.Error);
		public int ProblemCount => CheckResults.Count(x => x.Severity == RuleSeverity.Problem);
		public int SuggestCount => CheckResults.Count(x => x.Severity == RuleSeverity.Suggest);

		private bool enableShowError = true;
		public bool EnableShowError
		{
			get => enableShowError;
			set
			{
				Set(ref enableShowError, value);
				RefreshFilter();
			}
		}

		private bool enableShowProblem = true;
		public bool EnableShowProblem
		{
			get => enableShowProblem;
			set
			{
				Set(ref enableShowProblem, value);
				RefreshFilter();
			}
		}

		private bool enableShowSuggest = true;
		public bool EnableShowSuggest
		{
			get => enableShowSuggest;
			set
			{
				Set(ref enableShowSuggest, value);
				RefreshFilter();
			}
		}

		private FumenVisualEditorViewModel editor = default;
		public FumenVisualEditorViewModel Editor
		{
			get => editor;
			set
			{
				this.RegisterOrUnregisterPropertyChangeEvent(editor, value, OnEditorPropChanged);
				Set(ref editor, value);
				RefreshCurrentFumen();
			}
		}

		private void OnEditorPropChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
				RefreshCurrentFumen();
		}

		private List<IFumenCheckRule> checkRules;
		private ListView listView;

		public FumenCheckerListViewerViewModel()
		{
			DisplayName = Resources.FumenCheckerListViewer;
			checkRules = IoC.GetAll<IFumenCheckRule>().ToList();
			IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (n, o) => Editor = n;
			Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
		}

		public void OnItemDoubleClick(ICheckResult checkResult)
		{
			checkResult?.NavigateBehavior?.Navigate(Editor);
		}

		public void RefreshCurrentFumen()
		{
			CheckResults.Clear();

			try
			{
				if (Editor?.Fumen is not null)
				{
					var fumen = Editor.Fumen;

					foreach (var checkRule in checkRules.SelectMany(x => x.CheckRule(fumen, Editor)))
					{
						CheckResults.Add(checkRule);
					}
				}
			}
			catch (Exception e)
			{
				CheckResults.Clear();
				Log.LogError($"FumenCheckerListViewer can't refresh checkers", e);
			}

			NotifyOfPropertyChange(() => ErrorCount);
			NotifyOfPropertyChange(() => ProblemCount);
			NotifyOfPropertyChange(() => SuggestCount);
		}

		public void OnListViewLoaded(ActionExecutionContext e)
		{
			listView = e.Source as ListView;
			CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(listView.ItemsSource);
			view.Filter = x => OnCheckResultsFilter(x as ICheckResult);
		}

		public void RefreshFilter() => CollectionViewSource.GetDefaultView(listView.ItemsSource)?.Refresh();

		private bool OnCheckResultsFilter(ICheckResult checkResult)
		{
			switch (checkResult.Severity)
			{
				case RuleSeverity.Suggest:
					return EnableShowSuggest;
				case RuleSeverity.Problem:
					return EnableShowProblem;
				case RuleSeverity.Error:
					return EnableShowError;
				default:
					return false;
			}
		}
	}
}
