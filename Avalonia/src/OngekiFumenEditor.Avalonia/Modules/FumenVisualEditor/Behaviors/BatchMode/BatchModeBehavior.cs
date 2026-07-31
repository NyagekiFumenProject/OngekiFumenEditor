using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia;
using Avalonia.Data.Converters;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Behaviors.BatchMode;

public class BatchModeBehavior : ObservableObject
{
    public static readonly ImmutableList<BatchModeSubmode> Submodes =
        new List<BatchModeSubmode>
        {
            new BatchModeInputClipboard(),
            new BatchModeInputWallLeft(),
            new BatchModeInputLaneLeft(),
            new BatchModeInputLaneCenter(),
            new BatchModeInputLaneRight(),
            new BatchModeInputWallRight(),
            new BatchModeInputLaneColorful(),
            new BatchModeInputTap(),
            new BatchModeInputHold(),
            new BatchModeInputFlick(),
            new BatchModeInputLaneBlock(),
            new BatchModeInputNormalBell(),
            new BatchModeFilterLanes(),
            new BatchModeFilterDockableObjects(),
            new BatchModeFilterFloatingObjects(),
        }.ToImmutableList();

    private BatchModeSubmode _currentSubmode = Submodes[0];
    public BatchModeSubmode CurrentSubmode
    {
        get => _currentSubmode;
        set => SetProperty(ref _currentSubmode, value);
    }

}

public class BatchModeSubmodeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ((BatchModeSubmode)value)?.DisplayName?.Text ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsInstanceOfToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Log.LogInfo($"{parameter}");
        Log.LogInfo($"{parameter}");
        return value?.GetType().IsSubclassOf((Type)parameter!) ?? false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
