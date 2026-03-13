using System.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects;

public class InterpolatableSoflan : Soflan
{
    private readonly List<IKeyframeSoflan> cachedInterpolatedSoflans = new();

    private bool cachedValid;

    private EasingTypes easing = EasingTypes.None;

    private int interpolateCountPerResT = 16;

    public InterpolatableSoflan()
    {
        EndIndicator = new InterpolatableSoflanIndicator {RefSoflan = this};
        EndIndicator.PropertyChanged += EndIndicator_PropertyChanged;
        displayables = new IDisplayableObject[] {this, EndIndicator};
    }

    public override string IDShortName => "[INTP_SFL]";

    public EasingTypes Easing
    {
        get => easing;
        set => SetProperty(ref easing, value);
    }

    public int InterpolateCountPerResT
    {
        get => interpolateCountPerResT;
        set => SetProperty(ref interpolateCountPerResT, value);
    }

    private void EndIndicator_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Speed):
                OnPropertyChanged(nameof(Speed));
                break;
            case nameof(TGrid):
                OnPropertyChanged(nameof(EndTGrid));
                break;
            default:
                OnPropertyChanged(nameof(EndIndicator));
                break;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Speed):
            case nameof(TGrid):
            case nameof(InterpolateCountPerResT):
            case nameof(EndTGrid):
            case nameof(ApplySpeedInDesignMode):
            case nameof(Easing):
                cachedValid = false;
                break;
        }

        base.OnPropertyChanged(e);
    }

    public override string ToString()
    {
        return $"{base.ToString()} --> EndSpeed[{((InterpolatableSoflanIndicator) EndIndicator)?.Speed}x]";
    }

    public override void Copy(OngekiObjectBase fromObj)
    {
        base.Copy(fromObj);

        if (fromObj is not InterpolatableSoflan soflan)
            return;

        Speed = soflan.Speed;
        ApplySpeedInDesignMode = soflan.ApplySpeedInDesignMode;
        Easing = soflan.Easing;
    }

    public void UpdateCachedInterpolatedSoflans()
    {
        cachedInterpolatedSoflans.Clear();

        var fromTotalGrid = TGrid.TotalGrid;
        var toTotalGrid = EndTGrid.TotalGrid;

        var fromSpeed = Speed;
        var toSpeed = (EndIndicator as InterpolatableSoflanIndicator).Speed;

        if (fromSpeed == toSpeed || fromTotalGrid == toTotalGrid)
        {
            cachedInterpolatedSoflans.Add(new KeyframeSoflan
            {
                TGrid = new TGrid(0, toTotalGrid),
                Speed = toSpeed,
                ApplySpeedInDesignMode = ApplySpeedInDesignMode
            });
        }
        else
        {
            var stepGridLength = (int) (TGrid.DEFAULT_RES_T / InterpolateCountPerResT);

            for (var curGrid = fromTotalGrid; curGrid < toTotalGrid; curGrid += stepGridLength)
            {
                var nextGrid = Math.Min(curGrid + stepGridLength, toTotalGrid);

                var normalized = nextGrid == toTotalGrid
                    ? 1
                    : (curGrid - fromTotalGrid) * 1.0d / (toTotalGrid - fromTotalGrid);
                var transformed = (float) Interpolation.ApplyEasing(Easing, normalized);

                var speed = fromSpeed + transformed * (toSpeed - fromSpeed);

                cachedInterpolatedSoflans.Add(new KeyframeSoflan
                {
                    TGrid = new TGrid(0, curGrid),
                    Speed = speed,
                    ApplySpeedInDesignMode = ApplySpeedInDesignMode
                });
            }
        }

        cachedValid = true;
    }

    public override IEnumerable<IKeyframeSoflan> GenerateKeyframeSoflans()
    {
        if (!cachedValid)
            UpdateCachedInterpolatedSoflans();
        return cachedInterpolatedSoflans;
    }

    public override float CalculateSpeed(TGrid t)
    {
        var list = GenerateKeyframeSoflans();
        var r = ((IList<IKeyframeSoflan>) list).LastOrDefaultByBinarySearch(t, x => x.TGrid);
        return r?.Speed ?? 1;
    }

    public class InterpolatableSoflanIndicator : SoflanEndIndicator
    {
        private float speed = 1;

        public float Speed
        {
            get => speed;
            set => SetProperty(ref speed, value);
        }

        public override string IDShortName => "[INTP_SFL_End]";

        public override void Copy(OngekiObjectBase from)
        {
            base.Copy(from);

            if (from is not InterpolatableSoflanIndicator f)
                return;
            Speed = f.Speed;
        }
    }
}