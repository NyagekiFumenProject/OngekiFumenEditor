using System;
using Avalonia;
using Avalonia.Controls;
using System.ComponentModel;
namespace OngekiFumenEditor.Avalonia.UI.Controls;

public partial class RangeValue : UserControl, INotifyPropertyChanged
{
    public static readonly StyledProperty<string> DisplayNameProperty =
        AvaloniaProperty.Register<RangeValue, string>(nameof(DisplayName), string.Empty);

    public static readonly StyledProperty<double> MinValueProperty =
        AvaloniaProperty.Register<RangeValue, double>(nameof(MinValue), 0d);

    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<RangeValue, double>(nameof(MaxValue), 100d);

    public static readonly StyledProperty<double> CurrentValueProperty =
        AvaloniaProperty.Register<RangeValue, double>(nameof(CurrentValue), 50d);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<RangeValue, double>(nameof(Step), 1d);

    public string DisplayName
    {
        get => GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public double MinValue
    {
        get => GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public double MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public double CurrentValue
    {
        get => GetValue(CurrentValueProperty);
        set => SetValue(CurrentValueProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int CurrentIntValue
    {
        get => (int)CurrentValue;
        set => CurrentValue = value;
    }

    public new event PropertyChangedEventHandler PropertyChanged;

    public RangeValue()
    {
        InitializeComponent();
        this.GetObservable(CurrentValueProperty).Subscribe(new AnonymousObserver<double>(_ =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIntValue)))));
    }
    private sealed class AnonymousObserver<T>(Action<T> onNext) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(T value) => onNext(value);
    }
}
