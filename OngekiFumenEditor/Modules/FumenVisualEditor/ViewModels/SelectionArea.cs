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
        set => Set(ref isActive, value);
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
        var minTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(Rect.Top, editor);
        var maxTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(Rect.Bottom, editor);
        var minXGrid = XGridCalculator.ConvertXToXGrid(Rect.Left, editor);
        var maxXGrid = XGridCalculator.ConvertXToXGrid(Rect.Right, editor);

        return editor.Fumen.GetAllDisplayableObjects()
            .OfType<OngekiObjectBase>()
            .Distinct()
            .Where(Check);

        bool Check(OngekiObjectBase obj)
        {
            if (obj is ITimelineObject timelineObject)
            {
                if (timelineObject.TGrid > maxTGrid || timelineObject.TGrid < minTGrid)
                    return false;
            }

            if (obj is IHorizonPositionObject horizonPositionObject)
            {
                if (horizonPositionObject.XGrid > maxXGrid || horizonPositionObject.XGrid < minXGrid)
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