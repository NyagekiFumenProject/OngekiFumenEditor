using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.FastPickLane;

public abstract class FastPickLaneCommandDefinition<T> : CommandDefinition where T : LaneStartBase
{
    public override string Name => $"Editor.FastPickLane_{typeof(T).Name}";

    public override LocalizedString Text => field ??= LocalizedString.CreateFromTemplateFunc(() => $"{Lang.FastPickLane}({typeof(T).Name})");

    public override LocalizedString ToolTip => Text;
}

[RegisterSingleton<CommandDefinitionBase>]
public class FastPickWallLeftLaneCommandDefinition : FastPickLaneCommandDefinition<WallLeftStart>
{
    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FastPickWallLeftLaneCommandDefinition>(
        new KeyGesture(Key.OemTilde, KeyModifiers.Alt));
}

[RegisterSingleton<CommandDefinitionBase>]
public class FastPickLeftLaneCommandDefinition : FastPickLaneCommandDefinition<LaneLeftStart>
{
    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FastPickLeftLaneCommandDefinition>(
        new KeyGesture(Key.D1, KeyModifiers.Alt));
}

[RegisterSingleton<CommandDefinitionBase>]
public class FastPickCenterLaneCommandDefinition : FastPickLaneCommandDefinition<LaneCenterStart>
{
    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FastPickCenterLaneCommandDefinition>(
        new KeyGesture(Key.D2, KeyModifiers.Alt));
}

[RegisterSingleton<CommandDefinitionBase>]
public class FastPickRightLaneCommandDefinition : FastPickLaneCommandDefinition<LaneRightStart>
{
    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FastPickRightLaneCommandDefinition>(
        new KeyGesture(Key.D3, KeyModifiers.Alt));
}

[RegisterSingleton<CommandDefinitionBase>]
public class FastPickWallRightLaneCommandDefinition : FastPickLaneCommandDefinition<WallRightStart>
{
    [RegisterStaticObject<CommandKeyboardShortcut>]
    public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FastPickWallRightLaneCommandDefinition>(
        new KeyGesture(Key.D4, KeyModifiers.Alt));
}
