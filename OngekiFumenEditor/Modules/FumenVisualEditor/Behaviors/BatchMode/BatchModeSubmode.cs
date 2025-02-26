#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Kernel.KeyBinding;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Behaviors.BatchMode;

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

    public string HelperText => $"{DisplayName} ({KeyBindingDefinition.FormatToExpression(KeyBinding)})";
    public string DisplayName => Resources.ResourceManager.GetString(ResourceKey)!;

    public override string Name => $"BatchMode.{GetType().Name}";
    public override Uri IconSource =>
        new Uri($"pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/Batch/{ResourceKey}.png");

    public override string Text => DisplayName;
}

/// <summary>
/// A sub-mode that controls what the user is able to select.
/// </summary>
public abstract class BatchModeFilterSubmode : BatchModeSubmode
{
    public sealed override string ToolTip => Resources.BatchModeFilterTooltipFormat.Format(HelperText);
    public abstract Func<OngekiObjectBase, bool> FilterFunction { get; }
}

public abstract class BatchModeInputSubmode : BatchModeSubmode
{
    public override string ToolTip => HelperText;

    public abstract IEnumerable<OngekiTimelineObjectBase> GenerateObject();
    public virtual bool AutoSelect => false;
    public virtual BatchModeObjectModificationAction? ModifyObjectCtrl { get; } = null;
    public virtual BatchModeObjectModificationAction? ModifyObjectShift =>
        AutoSelect
            ? new BatchModeObjectModificationAction(null, Resources.BatchModeModifierAddToSelection)
            : null;
}

[CommandDefinition]
public class BatchModeInputClipboard : BatchModeInputSubmode
{
    private IFumenEditorClipboard Clipboard;

    public BatchModeInputClipboard()
    {
        Clipboard = IoC.Get<IFumenEditorClipboard>();
    }

    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeClipboard;
    public override string ResourceKey => nameof(Resources.Clipboard);
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
    where T : OngekiTimelineObjectBase
{
    public override Type ObjectType => typeof(T);

    public override IEnumerable<OngekiTimelineObjectBase> GenerateObject()
    {
        yield return Activator.CreateInstance<T>();
    }
}

public abstract class BatchModeInputLane<T> : BatchModeInputSubmode<T>
    where T : LaneStartBase, new()
{
    public override bool AutoSelect => true;
}

[CommandDefinition]
public class BatchModeInputLaneLeft : BatchModeInputLane<LaneLeftStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneLeft;
    public override string ResourceKey => nameof(Resources.LaneLeft);
}

[CommandDefinition]
public class BatchModeInputLaneCenter : BatchModeInputLane<LaneCenterStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneCenter;
    public override string ResourceKey => nameof(Resources.LaneCenter);
}

[CommandDefinition]
public class BatchModeInputLaneRight : BatchModeInputLane<LaneRightStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneRight;
    public override string ResourceKey => nameof(Resources.LaneRight);
}

[CommandDefinition]
public class BatchModeInputWallRight : BatchModeInputLane<WallRightStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeWallRight;
    public override string ResourceKey => nameof(Resources.WallRight);
}

[CommandDefinition]
public class BatchModeInputWallLeft : BatchModeInputLane<WallLeftStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeWallLeft;
    public override string ResourceKey => nameof(Resources.WallLeft);
}

[CommandDefinition]
public class BatchModeInputLaneColorful : BatchModeInputLane<ColorfulLaneStart>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneColorful;
    public override string ResourceKey => nameof(Resources.LaneColorful);
}

public abstract class BatchModeInputHitSubmode<T> : BatchModeInputSubmode<T>
    where T : OngekiTimelineObjectBase, ICriticalableObject
{
    public override BatchModeObjectModificationAction ModifyObjectCtrl { get; } = new(CritObject, Resources.BatchModeModifierSetCritical);

    private static void CritObject(OngekiObjectBase baseObject)
    {
        ((ICriticalableObject)baseObject).IsCritical = true;
    }
}

[CommandDefinition]
public class BatchModeInputTap : BatchModeInputHitSubmode<Tap>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeTap;
    public override string ResourceKey => nameof(Resources.Tap);
}

[CommandDefinition]
public class BatchModeInputHold : BatchModeInputHitSubmode<Hold>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeHold;
    public override string ResourceKey => nameof(Resources.Hold);
    public override bool AutoSelect => true;
}

[CommandDefinition]
public class BatchModeInputFlick : BatchModeInputHitSubmode<Flick>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeFlick;
    public override BatchModeObjectModificationAction ModifyObjectShift { get; } = new(SwitchFlick, Resources.BatchModeModifierSwitchDirection);

    private static void SwitchFlick(OngekiObjectBase baseObject)
    {
        ((Flick)baseObject).Direction = Flick.FlickDirection.Right;
    }

    public override string ResourceKey => nameof(Resources.Flick);
}

[CommandDefinition]
public class BatchModeInputLaneBlock : BatchModeInputSubmode<LaneBlockArea>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeLaneBlock;
    public override BatchModeObjectModificationAction ModifyObjectCtrl { get; } =
        new BatchModeObjectModificationAction(SwitchDirection, Resources.BatchModeModifierSwitchDirection);

    private static void SwitchDirection(OngekiObjectBase baseObject)
    {
        ((LaneBlockArea)baseObject).Direction = LaneBlockArea.BlockDirection.Right;
    }

    public override string ResourceKey => nameof(Resources.LaneBlock);
}

[CommandDefinition]
public class BatchModeInputNormalBell : BatchModeInputSubmode<Bell>
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeNormalBell;
    public override string ResourceKey => nameof(Resources.Bell);
}

[CommandDefinition]
public class BatchModeFilterLanes : BatchModeFilterSubmode
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeFilterLanes;
    public override string ResourceKey => nameof(Resources.ObjectFilterLanes);
    public override Func<OngekiObjectBase, bool> FilterFunction => obj => obj is LaneStartBase or LaneNextBase;
}

[CommandDefinition]
public class BatchModeFilterDockableObjects : BatchModeFilterSubmode
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeFilterDockableObjects;
    public override string ResourceKey => nameof(Resources.ObjectFilterDockables);
    public override Func<OngekiObjectBase, bool> FilterFunction => obj => obj is Tap or Hold or HoldEnd;
}

[CommandDefinition]
public class BatchModeFilterFloatingObjects : BatchModeFilterSubmode
{
    public override KeyBindingDefinition KeyBinding => KeyBindingDefinitions.KBD_Batch_ModeFilterFloatingObjects;
    public override string ResourceKey => nameof(Resources.ObjectFilterFloating);
    public override Func<OngekiObjectBase, bool> FilterFunction => obj => obj is Bell or Bullet or Flick;
}

public class BatchModeObjectModificationAction(Action<OngekiObjectBase>? modifier, string description)
{
    public string Description { get; } = description;
    public Action<OngekiObjectBase>? Function { get; } = modifier;
}

