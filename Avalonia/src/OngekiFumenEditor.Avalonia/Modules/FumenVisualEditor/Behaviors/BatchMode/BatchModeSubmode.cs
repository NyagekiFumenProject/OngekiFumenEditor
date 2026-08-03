#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Behaviors.BatchMode;

/// <summary>
/// Represents a sub-mode of the Batch Mode.
/// The sub-mode determines the behavior when clicking and holding modifiers.
/// Sub-modes are selected via Batch Mode shortcuts.
/// Only one sub-mode can be active at a time.
/// </summary>
public abstract class BatchModeSubmode : CommandDefinition
{
    public abstract KeyBindingDefinition KeyBinding { get; }
    public abstract string ResourceKey { get; }

    public LocalizedString HelperText => field ??= LocalizedString.CreateFromTemplateFunc(() => $"{DisplayName} ({KeyBindingDefinition.FormatToExpression(KeyBinding)})");
    public LocalizedString DisplayName => field ??= Lang.LocalizerManager.GetLocalizedTextSource(ResourceKey).ToLocalizedString();

    public override string Name => $"BatchMode.{GetType().Name}";
    public override Uri IconSource =>
        new Uri($"avares://OngekiFumenEditor.Avalonia/Resources/Icons/Batch/{ResourceKey}.png");

    public override LocalizedString Text => DisplayName;
}

/// <summary>
/// A sub-mode that controls what the user is able to select.
/// </summary>
public abstract class BatchModeFilterSubmode : BatchModeSubmode
{
    public sealed override LocalizedString ToolTip => field ??= Lang.B.BatchModeFilterTooltipFormat.ToFormatLocalizedString(HelperText.Text);
    public abstract Func<OngekiObjectBase, bool> FilterFunction { get; }
}

public abstract class BatchModeInputSubmode : BatchModeSubmode
{
    public override LocalizedString ToolTip => HelperText;

    public abstract IEnumerable<OngekiTimelineObjectBase> GenerateObject();
    public virtual bool AutoSelect => false;
    public virtual BatchModeObjectModificationAction? ModifyObjectCtrl { get; } = null;
    public virtual BatchModeObjectModificationAction? ModifyObjectShift =>
        AutoSelect
            ? new BatchModeObjectModificationAction(null, Lang.BatchModeModifierAddToSelection)
            : null;
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputClipboard : BatchModeInputSubmode
{
    private IFumenEditorClipboard Clipboard;

    public BatchModeInputClipboard()
    {
        Clipboard = IoC.Get<IFumenEditorClipboard>();
    }

    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeClipboard;
    public override string ResourceKey => nameof(Lang.Clipboard);
    public override IEnumerable<OngekiTimelineObjectBase> GenerateObject()
    {
        return Clipboard.CurrentCopiedObjects.Select(obj => (OngekiTimelineObjectBase)obj.CopyNew());
    }
}

public abstract class BatchModeSingleInputSubmode : BatchModeInputSubmode
{
    public abstract Type ObjectType { get; }
}

public abstract class BatchModeInputSubmode<T> : BatchModeSingleInputSubmode
    where T : OngekiTimelineObjectBase, new()
{
    public override Type ObjectType => typeof(T);

    public override IEnumerable<OngekiTimelineObjectBase> GenerateObject()
    {
        yield return new T();
    }
}

public abstract class BatchModeInputLane<T> : BatchModeInputSubmode<T>
    where T : LaneStartBase, new()
{
    public override bool AutoSelect => true;
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputLaneLeft : BatchModeInputLane<LaneLeftStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneLeft;
    public override string ResourceKey => nameof(Lang.LaneLeft);
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputLaneCenter : BatchModeInputLane<LaneCenterStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneCenter;
    public override string ResourceKey => nameof(Lang.LaneCenter);
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputLaneRight : BatchModeInputLane<LaneRightStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneRight;
    public override string ResourceKey => nameof(Lang.LaneRight);
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputWallRight : BatchModeInputLane<WallRightStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeWallRight;
    public override string ResourceKey => nameof(Lang.WallRight);
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputWallLeft : BatchModeInputLane<WallLeftStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeWallLeft;
    public override string ResourceKey => nameof(Lang.WallLeft);
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputLaneColorful : BatchModeInputLane<ColorfulLaneStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneColorful;
    public override string ResourceKey => nameof(Lang.LaneColorful);
}

public abstract class BatchModeInputHitSubmode<T> : BatchModeInputSubmode<T>
    where T : OngekiTimelineObjectBase, ICriticalableObject, new()
{
    public override BatchModeObjectModificationAction ModifyObjectCtrl { get; } = new(CritObject, Lang.BatchModeModifierSetCritical);

    private static void CritObject(OngekiObjectBase baseObject)
    {
        ((ICriticalableObject)baseObject).IsCritical = true;
    }
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputTap : BatchModeInputHitSubmode<Tap>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeTap;
    public override string ResourceKey => nameof(Lang.Tap);
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputHold : BatchModeInputHitSubmode<Hold>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeHold;
    public override string ResourceKey => nameof(Lang.Hold);
    public override bool AutoSelect => true;
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputFlick : BatchModeInputHitSubmode<Flick>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeFlick;
    public override BatchModeObjectModificationAction ModifyObjectShift { get; } = new(SwitchFlick, Lang.BatchModeModifierSwitchDirection);

    private static void SwitchFlick(OngekiObjectBase baseObject)
    {
        ((Flick)baseObject).Direction = Flick.FlickDirection.Right;
    }

    public override string ResourceKey => nameof(Lang.Flick);
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputLaneBlock : BatchModeInputSubmode<LaneBlockArea>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneBlock;
    public override BatchModeObjectModificationAction ModifyObjectCtrl { get; } =
        new BatchModeObjectModificationAction(SwitchDirection, Lang.BatchModeModifierSwitchDirection);

    private static void SwitchDirection(OngekiObjectBase baseObject)
    {
        ((LaneBlockArea)baseObject).Direction = LaneBlockArea.BlockDirection.Right;
    }

    public override string ResourceKey => nameof(Lang.LaneBlock);
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeInputNormalBell : BatchModeInputSubmode<Bell>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeNormalBell;
    public override string ResourceKey => nameof(Lang.Bell);
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeFilterLanes : BatchModeFilterSubmode
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeFilterLanes;
    public override string ResourceKey => nameof(Lang.ObjectFilterLanes);
    public override Func<OngekiObjectBase, bool> FilterFunction => obj => obj is LaneStartBase or LaneNextBase;
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeFilterDockableObjects : BatchModeFilterSubmode
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeFilterDockableObjects;
    public override string ResourceKey => nameof(Lang.ObjectFilterDockables);
    public override Func<OngekiObjectBase, bool> FilterFunction => obj => obj is Tap or Hold or HoldEnd;
}

[RegisterSingleton<CommandDefinitionBase>]
public class BatchModeFilterFloatingObjects : BatchModeFilterSubmode
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeFilterFloatingObjects;
    public override string ResourceKey => nameof(Lang.ObjectFilterFloating);
    public override Func<OngekiObjectBase, bool> FilterFunction => obj => obj is Bell or Bullet or Flick;
}

public class BatchModeObjectModificationAction(Action<OngekiObjectBase>? modifier, string description)
{
    public string Description { get; } = description;
    public Action<OngekiObjectBase>? Function { get; } = modifier;
}



