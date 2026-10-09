using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms.Design;
using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

public class SelectionArea : PropertyChangedBase
{
    public SelectionAreaKind SelectionAreaKind;

    private FumenVisualEditorViewModel editor;
    private readonly Dictionary<OngekiObjectBase, Rect> horizonalBounds = new();

    private Func<OngekiObjectBase, bool>? filterFunc;
    public Func<OngekiObjectBase, bool>? FilterFunc
    {
        get => filterFunc;
        set => Set(ref filterFunc, value);
    }

    private Point startPoint;
    public Point StartPoint
    {
        get => startPoint;
        set
        {
            Set(ref startPoint, value);
            Rect = new Rect(startPoint, endPoint);
            Log.LogInfo(Rect.ToString());
        }
    }

    private Point endPoint;

    public Point EndPoint
    {
        get => endPoint;
        set
        {
            Set(ref endPoint, value);
            Rect = new Rect(startPoint, endPoint);
        }
    }

    private Rect rect;
    public Rect Rect
    {
        get => rect;
        set => Set(ref rect, value);
    }

    private bool isActive = true;
    public bool IsActive
    {
        get => isActive;
        set
        {
            if (!Set(ref isActive, value))
                return;

            horizonalBounds.Clear();
            if (value)
            {
                foreach (var hit in editor.GetHits())
                    CacheSelectableObjectBounds(hit.Key, hit.Value);
            }
        }
    }

    internal void CacheSelectableObjectBounds(OngekiObjectBase obj, Rect bounds)
    {
        // 自动滚动会清空每帧的命中区域；保留本次框选见过的 Horizonal 标签，供移出视口后继续判断。
        if (IsActive && obj is ITimelineObject && obj is not IHorizonPositionObject)
            horizonalBounds[obj] = bounds;
    }

    public SelectionArea(FumenVisualEditorViewModel editor)
    {
        this.editor = editor;
        SelectionAreaKind = SelectionAreaKind.Select;
        IsActive = false;
    }

    public bool IsClick()
    {
        return Rect.Size.Width * Rect.Size.Height < 5;
    }

    public IEnumerable<OngekiObjectBase> GetRangeObjects(bool applyFilter = true)
    {
        var minTGrid = editor.ConvertYToTGrid_DesignMode(Rect.Top);
        if (minTGrid is null)
            minTGrid = TGrid.Zero;
        var maxTGrid = editor.ConvertYToTGrid_DesignMode(Rect.Bottom);
        var minXGrid = XGridCalculator.ConvertXToXGrid(Rect.Left, editor);
        var maxXGrid = XGridCalculator.ConvertXToXGrid(Rect.Right, editor);

        return editor.Fumen.GetAllDisplayableObjects()
            .OfType<OngekiObjectBase>()
            .Distinct()
            .Where(Check);

        bool Check(OngekiObjectBase obj)
        {
            if (obj is ITimelineObject && obj is not IHorizonPositionObject)
            {
                // Horizonal 物件没有 XGrid，按实际标签中心做二维框选，避免只凭时间范围选中。
                if (!editor.TryGetSelectableObjectBounds(obj, out var bounds) &&
                    !horizonalBounds.TryGetValue(obj, out bounds))
                    return false;

                if (!Rect.Contains(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)))
                    return false;
            }
            else
            {
                if (obj is ITimelineObject timelineObject &&
                    (timelineObject.TGrid > maxTGrid || timelineObject.TGrid < minTGrid))
                    return false;

                if (obj is IHorizonPositionObject horizonPositionObject &&
                    (horizonPositionObject.XGrid > maxXGrid || horizonPositionObject.XGrid < minXGrid))
                    return false;
            }

            if (applyFilter && (!FilterFunc?.Invoke(obj) ?? false)) {
                return false;
            }

            return true;
        }
    }

    public void ApplyRangeAction()
    {
        if (!editor.IsDesignMode)
        {
            editor.ToastNotify(Resources.EditorMustBeDesignMode);
            return;
        }

        SelectionAreaKind.SelectAction(editor, GetRangeObjects());
    }
}

public class SelectionAreaKind
{
    public static readonly SelectionAreaKind Select = new SelectionAreaKind((editor, objs) =>
    {
        objs = objs.ToArray();

        if (objs.Count() == 1)
            editor.NotifyObjectClicked(objs.Single());
        else {
            foreach (var o in objs.OfType<ISelectableObject>())
                o.IsSelected = true;
            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(editor);
        }
    });

    public static readonly SelectionAreaKind Delete = new SelectionAreaKind((editor, objs) =>
    {
        objs = objs.ToArray();
        if (!objs.Any())
            return;

        editor.DeleteSelection();
    });

    public readonly Action<FumenVisualEditorViewModel, IEnumerable<OngekiObjectBase>> SelectAction;
    private SelectionAreaKind(Action<FumenVisualEditorViewModel, IEnumerable<OngekiObjectBase>> selectAction)
    {
        SelectAction = selectAction;
    }
}
