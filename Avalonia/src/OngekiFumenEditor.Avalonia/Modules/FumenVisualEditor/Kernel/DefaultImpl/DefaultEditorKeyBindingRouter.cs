#nullable enable

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Gekimini.Avalonia.Modules.Window.Views;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Compat;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Behaviors.BatchMode;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.DefaultImpl;

[RegisterSingleton<IEditorKeyBindingRouter>]
internal sealed class DefaultEditorKeyBindingRouter : IEditorKeyBindingRouter
{
    private const int ExpectedEditorActionCount = 35;

    private readonly IKeyBindingManager keyBindingManager;
    private readonly IEditorDocumentManager editorDocumentManager;
    private readonly ILogger<DefaultEditorKeyBindingRouter> logger;
    private readonly IReadOnlyDictionary<KeyBindingDefinition, EditorKeyAction> actions;
    private readonly EventHandler<KeyEventArgs> keyDownHandler;

    private TopLevel? attachedTopLevel;
    private Window? attachedWindow;

    internal int MappedActionCount => actions.Count;

    internal bool HasActionFor(KeyBindingDefinition definition) => actions.ContainsKey(definition);

    internal static bool ShouldYieldToFocusedControlForTest(object? eventSource) =>
        ShouldYieldToFocusedControl(eventSource);

    private delegate Task EditorKeyAction(
        FumenVisualEditorViewModel editor,
        ActionExecutionContext executionContext);

    public DefaultEditorKeyBindingRouter(
        IKeyBindingManager keyBindingManager,
        IEditorDocumentManager editorDocumentManager,
        ILogger<DefaultEditorKeyBindingRouter> logger)
    {
        this.keyBindingManager = keyBindingManager;
        this.editorDocumentManager = editorDocumentManager;
        this.logger = logger;

        actions = CreateActions();
        if (actions.Count != ExpectedEditorActionCount)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedEditorActionCount} editor key binding actions, but mapped {actions.Count}.");
        }

        keyDownHandler = OnKeyDown;
    }

    public void Attach(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        if (ReferenceEquals(attachedTopLevel, topLevel))
            return;

        Detach();

        attachedTopLevel = topLevel;
        attachedTopLevel.AddHandler(
            InputElement.KeyDownEvent,
            keyDownHandler,
            RoutingStrategies.Bubble,
            handledEventsToo: false);

        if (topLevel is Window window)
        {
            attachedWindow = window;
            attachedWindow.Closed += OnAttachedWindowClosed;
        }

        logger.LogInformation("Attached the editor key binding router to {TopLevelType}.", topLevel.GetType().Name);
    }

    public void Detach()
    {
        if (attachedTopLevel is not null)
            attachedTopLevel.RemoveHandler(InputElement.KeyDownEvent, keyDownHandler);

        if (attachedWindow is not null)
            attachedWindow.Closed -= OnAttachedWindowClosed;

        attachedTopLevel = null;
        attachedWindow = null;
    }

    private void OnAttachedWindowClosed(object? sender, EventArgs e)
    {
        Detach();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key == Key.None)
            return;

        var editor = editorDocumentManager.CurrentActivatedEditor;
        if (editor is null || ShouldYieldToFocusedControl(e.Source))
            return;

        var activeLayer = editor.IsBatchMode
            ? KeyBindingLayer.Batch
            : KeyBindingLayer.Normal;

        var matches = keyBindingManager.KeyBindingDefinations
            .Where(definition => definition.Layer == KeyBindingLayer.Global || definition.Layer == activeLayer)
            .Where(definition => keyBindingManager.CheckKeyBinding(definition, e))
            .ToArray();

        if (matches.Length == 0)
            return;

        if (matches.Length > 1)
        {
            logger.LogError(
                "Editor key binding conflict for {Key} ({Modifiers}) in {Layer}: {Definitions}. No action was executed.",
                e.Key,
                e.KeyModifiers,
                activeLayer,
                string.Join(", ", matches.Select(definition => definition.ConfigKey)));
            return;
        }

        var definition = matches[0];
        if (!actions.TryGetValue(definition, out var action))
        {
            logger.LogError("No editor action is mapped for key binding {Definition}.", definition.ConfigKey);
            return;
        }

        var executionContext = new ActionExecutionContext
        {
            Source = e.Source ?? sender ?? attachedTopLevel,
            EventArgs = e,
            View = attachedTopLevel
        };

        try
        {
            var actionTask = action(editor, executionContext);
            e.Handled = true;
            await actionTask;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Editor key binding action {Definition} failed.", definition.ConfigKey);
        }
    }

    private static bool ShouldYieldToFocusedControl(object? eventSource)
    {
        if (eventSource is not Visual sourceVisual)
            return false;

        return sourceVisual
            .GetVisualAncestors()
            .Prepend(sourceVisual)
            .Any(static visual => visual is
                WindowViewBase or
                TextBox or
                NumericUpDown or
                ComboBox or
                DataGrid or
                DataGridCell);
    }

    private static IReadOnlyDictionary<KeyBindingDefinition, EditorKeyAction> CreateActions()
    {
        return new Dictionary<KeyBindingDefinition, EditorKeyAction>
        {
            [KeyBindingDefinitions.KBD_ChangeDockableLaneType] = AsTask(
                static (editor, context) => editor.KeyboardAction_ChangeDockableLaneType(context)),
            [KeyBindingDefinitions.KBD_FastSetObjectIsCritical] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastSetObjectIsCritical(context)),
            [KeyBindingDefinitions.KBD_FastPlaceDockableObjectToWallLeft] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastPlaceDockableObjectToWallLeft(context)),
            [KeyBindingDefinitions.KBD_FastPlaceDockableObjectToWallRight] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastPlaceDockableObjectToWallRight(context)),
            [KeyBindingDefinitions.KBD_FastPlaceDockableObjectToRight] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastPlaceDockableObjectToRight(context)),
            [KeyBindingDefinitions.KBD_FastPlaceNewHold] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastPlaceNewHold(context)),
            [KeyBindingDefinitions.KBD_FastPlaceNewTap] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastPlaceNewTap(context)),
            [KeyBindingDefinitions.KBD_FastPlaceDockableObjectToCenter] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastPlaceDockableObjectToCenter(context)),
            [KeyBindingDefinitions.KBD_FastPlaceDockableObjectToLeft] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastPlaceDockableObjectToLeft(context)),
            [KeyBindingDefinitions.KBD_DeleteSelectingObjects] = AsTask(
                static (editor, _) => editor.KeyboardAction_DeleteSelectingObjects()),
            [KeyBindingDefinitions.KBD_SelectAllObjects] = AsTask(
                static (editor, context) => editor.KeyboardAction_SelectAllObjects(context)),
            [KeyBindingDefinitions.KBD_CancelSelectingObjects] = AsTask(
                static (editor, context) => editor.KeyboardAction_CancelSelectingObjects(context)),
            [KeyBindingDefinitions.KBD_HideOrShow] = AsTask(
                static (editor, context) => editor.KeyboardAction_HideOrShow(context)),
            [KeyBindingDefinitions.KBD_ToggleBatchMode] = AsTask(
                static (editor, context) => editor.KeyboardAction_ToggleBatchMode(context)),
            [KeyBindingDefinitions.KBD_FastAddConnectableChild] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastAddConnectableChild(context)),
            [KeyBindingDefinitions.KBD_FastSwitchFlickDirection] = AsTask(
                static (editor, context) => editor.KeyboardAction_FastSwitchFlickDirection(context)),
            [KeyBindingDefinitions.KBD_CopySelectedObjects] =
                static (editor, _) => editor.MenuItemAction_CopySelectedObjects(),
            [KeyBindingDefinitions.KBD_PasteCopiesObjects] =
                static (editor, context) => editor.KeyboardAction_PasteCopiesObjects(context),
            [KeyBindingDefinitions.KBD_ScrollPageDown] = AsTask(
                static (editor, _) => editor.ScrollPage(-1)),
            [KeyBindingDefinitions.KBD_ScrollPageUp] = AsTask(
                static (editor, _) => editor.ScrollPage(1)),

            [KeyBindingDefinitions.KBD_Batch_ModeWallLeft] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputWallLeft>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneLeft] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputLaneLeft>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneCenter] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputLaneCenter>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneRight] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputLaneRight>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeWallRight] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputWallRight>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneColorful] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputLaneColorful>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeTap] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputTap>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeHold] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputHold>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeFlick] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputFlick>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeLaneBlock] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputLaneBlock>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeNormalBell] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputNormalBell>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeClipboard] =
                static (editor, _) => SelectBatchSubmode<BatchModeInputClipboard>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeFilterLanes] =
                static (editor, _) => SelectBatchSubmode<BatchModeFilterLanes>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeFilterDockableObjects] =
                static (editor, _) => SelectBatchSubmode<BatchModeFilterDockableObjects>(editor),
            [KeyBindingDefinitions.KBD_Batch_ModeFilterFloatingObjects] =
                static (editor, _) => SelectBatchSubmode<BatchModeFilterFloatingObjects>(editor)
        };
    }

    private static EditorKeyAction AsTask(Action<FumenVisualEditorViewModel, ActionExecutionContext> action)
    {
        return (editor, executionContext) =>
        {
            action(editor, executionContext);
            return Task.CompletedTask;
        };
    }

    private static Task SelectBatchSubmode<TSubmode>(FumenVisualEditorViewModel editor)
        where TSubmode : BatchModeSubmode
    {
        editor.BatchModeBehavior.CurrentSubmode = BatchModeBehavior.Submodes.OfType<TSubmode>().Single();
        return Task.CompletedTask;
    }
}
