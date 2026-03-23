using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CoreTGridCalculator = OngekiFumenEditor.Core.Modules.FumenVisualEditor.TGridCalculator;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public static class TGridCalculator
    {
        #region Frame -> AudioTime

        public const float FRAME_DURATION = CoreTGridCalculator.FRAME_DURATION;

        #endregion

        #region AudioTime -> TGrid

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, FumenVisualEditorViewModel editor)
           => ConvertAudioTimeToTGrid(audioTime, editor.Fumen.BpmList);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, BpmList bpmList)
            => CoreTGridCalculator.ConvertAudioTimeToTGrid(audioTime, bpmList);

        #endregion

        #region TGrid -> AudioTime

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, FumenVisualEditorViewModel editor)
            => ConvertTGridToAudioTime(tGrid, editor.Fumen.BpmList);

        public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, BpmList bpmList)
            => CoreTGridCalculator.ConvertTGridToAudioTime(tGrid, bpmList);

        #endregion

        #region [DesignMode] Y -> TGrid

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TGrid ConvertYToTGrid_DesignMode(double pickY, FumenVisualEditorViewModel editor)
            => ConvertYToTGrid_DesignMode(pickY, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

        public static TGrid ConvertYToTGrid_DesignMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
            => CoreTGridCalculator.ConvertYToTGrid_DesignMode(pickY, soflanList, bpmList, scale);

        #endregion

        #region [DesignMode] Y -> AudioTime

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ConvertYToAudioTime_DesignMode(double pickY, FumenVisualEditorViewModel editor)
            => ConvertYToAudioTime_DesignMode(pickY, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

        public static TimeSpan ConvertYToAudioTime_DesignMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
            => CoreTGridCalculator.ConvertYToAudioTime_DesignMode(pickY, soflanList, bpmList, scale);

        #endregion

        #region [DesignMode] AudioTime -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertAudioTimeToY_DesignMode(TimeSpan audioTime, FumenVisualEditorViewModel editor)
            => ConvertAudioTimeToY_DesignMode(audioTime, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertAudioTimeToY_DesignMode(TimeSpan audioTime, SoflanList soflanList, BpmList bpmList, double scale)
            => CoreTGridCalculator.ConvertAudioTimeToY_DesignMode(audioTime, soflanList, bpmList, scale);

        #endregion

        #region [DesignMode] TGrid -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_DesignMode(TGrid tGrid, FumenVisualEditorViewModel editor)
            => ConvertTGridToY_DesignMode(tGrid, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_DesignMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale)
            => CoreTGridCalculator.ConvertTGridToY_DesignMode(tGrid, soflanList, bpmList, scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridUnitToY_DesignMode(double tGridUnit, FumenVisualEditorViewModel editor)
            => ConvertTGridUnitToY_DesignMode(tGridUnit, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

        public static double ConvertTGridUnitToY_DesignMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale)
            => CoreTGridCalculator.ConvertTGridUnitToY_DesignMode(tGridUnit, soflanList, bpmList, scale);

        #endregion

        #region [PreviewMode] Y -> TGrid[]

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<TGrid> ConvertYToTGrid_PreviewMode(double pickY, FumenVisualEditorViewModel editor)
            => ConvertYToTGrid_PreviewMode(pickY, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

        public static IEnumerable<TGrid> ConvertYToTGrid_PreviewMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
            => CoreTGridCalculator.ConvertYToTGrid_PreviewMode(pickY, soflanList, bpmList, scale);

        #endregion

        #region [PreviewMode] TGrid -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_PreviewMode(TGrid tGrid, FumenVisualEditorViewModel editor)
            => ConvertTGridToY_PreviewMode(tGrid, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_PreviewMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale)
            => CoreTGridCalculator.ConvertTGridToY_PreviewMode(tGrid, soflanList, bpmList, scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, FumenVisualEditorViewModel editor)
            => ConvertTGridUnitToY_PreviewMode(tGridUnit, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

        public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale)
            => CoreTGridCalculator.ConvertTGridUnitToY_PreviewMode(tGridUnit, soflanList, bpmList, scale);

        #endregion

        #region [PreviewMode] AudioTime -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertAudioTimeToY_PreviewMode(TimeSpan audioTime, FumenVisualEditorViewModel editor)
            => ConvertAudioTimeToY_PreviewMode(audioTime, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertAudioTimeToY_PreviewMode(TimeSpan audioTime, SoflanList soflanList, BpmList bpmList, double scale)
            => CoreTGridCalculator.ConvertAudioTimeToY_PreviewMode(audioTime, soflanList, bpmList, scale);

        #endregion

        #region [PreviewMode] VisbleTimelines

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode(FumenVisualEditorViewModel editor)
            => GetVisbleTimelines_PreviewMode(editor.CurrentDrawingTargetContext.CurrentSoflanList, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.CurrentDrawingTargetContext.Rect.MinY, editor.CurrentDrawingTargetContext.Rect.MaxY, editor.Setting.JudgeLineOffsetY, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode(SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double currentY, double viewHeight, double judgeLineOffsetY, int beatSplit, double scale)
            => CoreTGridCalculator.GetVisbleTimelines_PreviewMode(soflans, bpmList, meterList, currentY, viewHeight, judgeLineOffsetY, beatSplit, scale);

        #endregion

        #region [DesignMode] VisbleTimelines

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode(FumenVisualEditorViewModel editor)
            => GetVisbleTimelines_DesignMode(editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.RectInDesignMode.MinY, editor.RectInDesignMode.MaxY, editor.Setting.JudgeLineOffsetY, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode(SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double minVisibleCanvasY, double maxVisibleCanvasY, double judgeLineOffsetY, int beatSplit, double scale)
            => CoreTGridCalculator.GetVisbleTimelines_DesignMode(soflans, bpmList, meterList, minVisibleCanvasY, maxVisibleCanvasY, judgeLineOffsetY, beatSplit, scale);

        #endregion

        public static (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) GetCurrentTimeSignature(TGrid tGrid, FumenVisualEditorViewModel editor)
            => GetCurrentTimeSignature(tGrid, editor.Fumen.BpmList, editor.Fumen.MeterChanges);

        public static (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) GetCurrentTimeSignature(TGrid tGrid, BpmList bpmList, MeterChangeList meterList)
            => CoreTGridCalculator.GetCurrentTimeSignature(tGrid, bpmList, meterList);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList(FumenVisualEditorViewModel editor)
            => GetAllBpmUniformPositionList(editor.Fumen.BpmList);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList(BpmList bpmList)
            => CoreTGridCalculator.GetAllBpmUniformPositionList(bpmList);

        public static double CalculateOffsetYPerBeat(BPMChange bpm, MeterChange meter, int beatSplit, double scale)
            => CoreTGridCalculator.CalculateOffsetYPerBeat(bpm, meter, beatSplit, scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime_DesignMode(float y, float range, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale)
            => CoreTGridCalculator.TryPickMagneticBeatTime_DesignMode(y, range, soflans, bpmList, meterChanges, beatSplit, scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime(float y, float range, FumenVisualEditorViewModel editor)
            => TryPickMagneticBeatTime_DesignMode(y, range, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime(float y, FumenVisualEditorViewModel editor)
            => TryPickClosestBeatTime_DesignMode(y, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

        public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime_DesignMode(float y, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale)
            => CoreTGridCalculator.TryPickClosestBeatTime_DesignMode(y, soflans, bpmList, meterChanges, beatSplit, scale);

        internal static object ConvertTGridUnitToY_DesignMode(double v, SoflanList soflans, BpmList bpmList, object verticalScale)
            => CoreTGridCalculator.ConvertTGridUnitToY_DesignMode(v, soflans, bpmList, verticalScale);
    }
}
