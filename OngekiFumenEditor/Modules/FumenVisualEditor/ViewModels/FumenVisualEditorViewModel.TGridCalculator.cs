using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.Collections;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime)
            => TGridCalculator.ConvertAudioTimeToTGrid(audioTime, Fumen.BpmList);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan ConvertTGridToAudioTime(TGrid tGrid)
            => TGridCalculator.ConvertTGridToAudioTime(tGrid, Fumen.BpmList);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TGrid ConvertYToTGrid_DesignMode(double pickY)
            => TGridCalculator.ConvertYToTGrid_DesignMode(pickY, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan ConvertYToAudioTime_DesignMode(double pickY)
            => TGridCalculator.ConvertYToAudioTime_DesignMode(pickY, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ConvertAudioTimeToY_DesignMode(TimeSpan audioTime)
            => TGridCalculator.ConvertAudioTimeToY_DesignMode(audioTime, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ConvertTGridToY_DesignMode(TGrid tGrid)
            => TGridCalculator.ConvertTGridToY_DesignMode(tGrid, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ConvertTGridUnitToY_DesignMode(double tGridUnit)
            => TGridCalculator.ConvertTGridUnitToY_DesignMode(tGridUnit, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<TGrid> ConvertYToTGrid_PreviewMode(double pickY)
            => TGridCalculator.ConvertYToTGrid_PreviewMode(pickY, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ConvertTGridToY_PreviewMode(TGrid tGrid)
            => TGridCalculator.ConvertTGridToY_PreviewMode(tGrid, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ConvertTGridUnitToY_PreviewMode(double tGridUnit)
            => TGridCalculator.ConvertTGridUnitToY_PreviewMode(tGridUnit, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ConvertAudioTimeToY_PreviewMode(TimeSpan audioTime)
            => TGridCalculator.ConvertAudioTimeToY_PreviewMode(audioTime, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode()
            => TGridCalculator.GetVisbleTimelines_PreviewMode(CurrentDrawingTargetContext.CurrentSoflanList, Fumen.BpmList, Fumen.MeterChanges, CurrentDrawingTargetContext.Rect.MinY, CurrentDrawingTargetContext.Rect.MaxY, Setting.JudgeLineOffsetY, Setting.BeatSplit, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode()
            => TGridCalculator.GetVisbleTimelines_DesignMode(Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Fumen.MeterChanges, RectInDesignMode.MinY, RectInDesignMode.MaxY, Setting.JudgeLineOffsetY, Setting.BeatSplit, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) GetCurrentTimeSignature(TGrid tGrid)
            => TGridCalculator.GetCurrentTimeSignature(tGrid, Fumen.BpmList, Fumen.MeterChanges);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList()
            => TGridCalculator.GetAllBpmUniformPositionList(Fumen.BpmList);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime(float y, float range)
            => TGridCalculator.TryPickMagneticBeatTime_DesignMode(y, range, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Fumen.MeterChanges, Setting.BeatSplit, Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime(float y)
            => TGridCalculator.TryPickClosestBeatTime_DesignMode(y, Fumen.SoflansMap.DefaultSoflanList, Fumen.BpmList, Fumen.MeterChanges, Setting.BeatSplit, Setting.VerticalDisplayScale);
    }
}
