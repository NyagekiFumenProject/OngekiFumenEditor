using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using static OngekiFumenEditor.Avalonia.Base.Collections.SoflanList;

namespace OngekiFumenEditor.Avalonia.Base
{
    public interface ISoflanQueryable
    {
        IList<SoflanPoint> GetSoflanPositionList(BpmList bpmList, bool isDesignMode);
        IEnumerable<VisibleTGridRange> GetVisibleRanges(double currentY, double viewHeight, double preOffset, BpmList bpmList, double scale,bool isDesignMode);
        IEnumerable<KeyframeSoflan> GenerateDurationSoflans(BpmList bpmList);
        IEnumerable<KeyframeSoflan> GenerateKeyframeSoflans(BpmList bpmList);
    }
}
