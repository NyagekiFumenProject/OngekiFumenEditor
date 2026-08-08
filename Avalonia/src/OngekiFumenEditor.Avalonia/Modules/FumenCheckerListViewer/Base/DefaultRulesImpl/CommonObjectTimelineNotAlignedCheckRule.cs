using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[RegisterSingleton]
	internal class CommonObjectTimelineNotAlignedCheckRule : IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			IEnumerable<ICheckResult> CheckList(IEnumerable<OngekiTimelineObjectBase> objs)
			{
				const string RuleName = "ObjectTimelineNotAligned";
				var beats = fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(fumen.BpmList);

				var currentIndex = 0;

				var lengthPerBeat = 0d;
				var currentStartTGrid = default(TGrid);
				var currentMeter = default(MeterChange);
				var nextStartTGrid = default(TGrid);
				void UpdateStatus()
				{
					(_, currentStartTGrid, currentMeter, _) = beats[currentIndex];
					nextStartTGrid = beats.ElementAtOrDefault(currentIndex + 1).startTGrid;
					//璁＄畻姣忎竴鎷嶇殑(grid)闀垮害
					var resT = currentStartTGrid.ResT;
					var beatCount = currentMeter.BunShi;
					lengthPerBeat = resT * 1.0d / beatCount;
				}

				UpdateStatus();

				foreach (var obj in objs.OrderBy(x => x.TGrid))
				{
					//纭繚obj灞炰簬currentStartTGrid閲岄潰鐨?
					while (nextStartTGrid != null && obj.TGrid >= nextStartTGrid)
					{
						currentIndex++;
						UpdateStatus();
					}

					var diff = obj.TGrid - currentStartTGrid;
					var totalGrid = diff.TotalGrid(obj.TGrid.ResT);
					var div = totalGrid / lengthPerBeat;
					var trck = div - Math.Truncate(div);
					var beat = trck != 0 ? (1 / trck) : 0;
					var revBeat = trck != 0 ? (1 / (1 - trck)) : 0;

					if ((!(revBeat == (int)revBeat)) && (!(beat == (int)beat)))
					{
						yield return new CommonCheckResult()
						{
							Severity = RuleSeverity.Problem,
							Description = Lang.ObjectTimelineNotAligned.Format(obj.IDShortName),
							LocationDescription = $"{obj}",
							NavigateBehavior = new NavigateToObjectBehavior(obj),
							RuleName = RuleName,
						};
					}
				}
			}

			var objs = Enumerable.Empty<OngekiTimelineObjectBase>()
				.Concat(fumen.Taps);

			foreach (var result in CheckList(objs))
				yield return result;
		}
	}
}



