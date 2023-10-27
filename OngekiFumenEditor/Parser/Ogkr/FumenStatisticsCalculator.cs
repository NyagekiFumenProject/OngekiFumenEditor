using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr
{
	//special thanks geeki___ for saving my hair. ^_^
	public class FumenStatisticsCalculator
	{
		public static FumenStatisticsResult CalculateObjectStatisticsAsync(OngekiFumen fumen)
		{
			var result = new FumenStatisticsResult();

			result.BellObjects = CalculateBellObjectsAsync(fumen);
			result.FlickObjects = CalculateFlickObjectsAsync(fumen);
			result.SideObjects = CalculateSideObjectsAsync(fumen);
			result.TapObjects = CalculateTapObjectsAsync(fumen);
			result.SideHoldObjects = CalculateSideHoldObjectsAsync(fumen);
			result.HoldObjects = CalculateHoldObjectsAsync(fumen);

			result.TotalObjects =
				result.FlickObjects +
				result.SideObjects +
				result.HoldObjects +
				result.TapObjects +
				result.SideHoldObjects;
			/*
            var msg = $"T_TOTAL {result.TotalObjects}{Environment.NewLine}";
            msg += $"T_TAP {result.TapObjects}{Environment.NewLine}";
            msg += $"T_HOLD {result.HoldObjects}{Environment.NewLine}";
            msg += $"T_SIDE {result.SideObjects}{Environment.NewLine}";
            msg += $"T_SHOLD {result.SideHoldObjects}{Environment.NewLine}";
            msg += $"T_FLICK {result.FlickObjects}{Environment.NewLine}";
            msg += $"T_BELL {result.BellObjects}{Environment.NewLine}";

            MessageBox.Show(msg);
            */
			return result;
		}

		private static int CalculateHoldObjectsAsync(OngekiFumen fumen)
		{
			return fumen.Holds
				.Where(x => x.ReferenceLaneStart?.IsWallLane != true)
				.Select(x => CalculateHold(x, fumen))
				.Sum();
		}

		private static int CalculateSideHoldObjectsAsync(OngekiFumen fumen)
		{
			return fumen.Holds
				.Where(x => x.ReferenceLaneStart?.IsWallLane == true)
				.Select(x => CalculateHold(x, fumen))
				.Sum();
		}

		private static int CalculateHold(Hold x, OngekiFumen fumen)
		{
			var timeResolution_ = fumen.MetaInfo.TRESOLUTION;

			int CalcHoldTickStepSize(TGrid time)
			{
				var bpm = fumen.BpmList.GetBpm(time).BPM;
				var progressJudgeBPM = fumen.MetaInfo.ProgJudgeBpm;
				var standardBeatLen = timeResolution_ >> 2; //取1/4切片长度

				if (bpm < progressJudgeBPM)
				{
					while (bpm < progressJudgeBPM)
					{
						standardBeatLen >>= 1;
						bpm *= 2f;
					}
				}
				else
				{
					for (progressJudgeBPM *= 2f; progressJudgeBPM <= bpm; progressJudgeBPM *= 2f)
					{
						standardBeatLen <<= 1;
					}
				}
				return standardBeatLen;
			}

			var holdStartTGrid = x.TGrid;
			var holdEndTGrid = x.HoldEnd?.TGrid;

			var count = 0;
			var tickGrid = CalcHoldTickStepSize(holdStartTGrid);
			var curTGrid = holdStartTGrid + new GridOffset(0, tickGrid);

			if (holdEndTGrid is not null)
			{
				while (curTGrid < holdEndTGrid)
				{
					count++;
					tickGrid = CalcHoldTickStepSize(curTGrid);
					curTGrid = curTGrid + new GridOffset(0, tickGrid);
				}
			}

			return count + 1;
		}

		private static int CalculateTapObjectsAsync(OngekiFumen fumen)
		{
			return fumen.Taps.Count() + fumen.Holds.Count() - CalculateSideObjectsAsync(fumen);
		}

		private static int CalculateSideObjectsAsync(OngekiFumen fumen)
		{
			var wallTapsCount = fumen.Taps.Where(x => x.IsWallTap).Count();
			var wallHoldCount = fumen.Holds.Where(x => x.IsWallHold).Count();
			return wallTapsCount + wallHoldCount;
		}

		private static int CalculateFlickObjectsAsync(OngekiFumen fumen)
		{
			return fumen.Flicks.Count;
		}

		private static int CalculateBellObjectsAsync(OngekiFumen fumen)
		{
			return fumen.Bells.Count;
		}
	}
}
