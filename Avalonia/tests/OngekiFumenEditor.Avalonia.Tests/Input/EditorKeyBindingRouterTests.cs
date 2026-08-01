using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.DefaultImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Input;

public sealed class EditorKeyBindingRouterTests
{
    [Fact]
    public void DefinitionMap_AllThirtyFiveEditorDefinitionsHaveOneTypedAction()
    {
        var definitions = typeof(KeyBindingDefinitions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(KeyBindingDefinition))
            .Select(static field =>
            {
                var value = field.GetValue(null);
                Assert.IsType<KeyBindingDefinition>(value);
                return (KeyBindingDefinition)value!;
            })
            .ToArray();
        var router = CreateRouter(new StubKeyBindingManager(definitions));

        Assert.Equal(35, definitions.Length);
        Assert.Equal(35, definitions.Distinct().Count());
        Assert.Equal(35, definitions.Select(static definition => definition.ConfigKey).Distinct().Count());
        Assert.Equal(35, router.MappedActionCount);
        Assert.All(definitions, definition =>
            Assert.True(router.HasActionFor(definition), $"No action is mapped for {definition.ConfigKey}."));
    }

    [AvaloniaFact]
    public void FocusSensitiveControls_AreRecognizedWithoutDependingOnPlatformFocusImplementation()
    {
        object[] focusSensitiveControls =
        [
            new TextBox(),
            new NumericUpDown(),
            new ComboBox(),
            new DataGrid(),
            new DataGridCell(),
            new global::OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Dialogs.ConfigKeyBindingDialog()
        ];

        Assert.All(focusSensitiveControls, control =>
            Assert.True(
                DefaultEditorKeyBindingRouter.ShouldYieldToFocusedControlForTest(control),
                $"The router should yield to {control.GetType().FullName}."));
        Assert.False(DefaultEditorKeyBindingRouter.ShouldYieldToFocusedControlForTest(new Border()));
        Assert.False(DefaultEditorKeyBindingRouter.ShouldYieldToFocusedControlForTest(new object()));
    }

    [AvaloniaFact]
    public void KeyDown_FromTextEntry_YieldsBeforeCheckingDefinitions()
    {
        var definition = new KeyBindingDefinition("test-focus-yield", Key.S);
        var keyBindingManager = new StubKeyBindingManager([definition]) { MatchAll = true };
        var router = CreateRouter(keyBindingManager);
        var textBox = new TextBox();
        var window = new Window
        {
            Width = 320,
            Height = 160,
            Content = textBox
        };

        try
        {
            window.Show();
            window.UpdateLayout();
            router.Attach(window);

            var eventArgs = RaiseKeyDown(textBox, Key.S);

            Assert.Equal(0, keyBindingManager.CheckCallCount);
            Assert.False(eventArgs.Handled);
        }
        finally
        {
            router.Detach();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyDown_ConflictingDefinitions_LeavesEventUnhandledAndExecutesNothing()
    {
        KeyBindingDefinition[] definitions =
        [
            new KeyBindingDefinition("test-conflict-a", Key.S),
            new KeyBindingDefinition("test-conflict-b", Key.S)
        ];
        var keyBindingManager = new StubKeyBindingManager(definitions) { MatchAll = true };
        var logger = new RecordingLogger<DefaultEditorKeyBindingRouter>();
        var router = CreateRouter(keyBindingManager, logger);
        var window = new Window();

        try
        {
            router.Attach(window);

            var eventArgs = RaiseKeyDown(window, Key.S);

            Assert.Equal(2, keyBindingManager.CheckCallCount);
            Assert.False(eventArgs.Handled);
            var logEntry = Assert.Single(logger.Entries, static entry => entry.Level == LogLevel.Error);
            Assert.Equal(LogLevel.Error, logEntry.Level);
            Assert.Contains("conflict", logEntry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("No editor action", logEntry.Message, StringComparison.Ordinal);
        }
        finally
        {
            router.Detach();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AttachCalledTwiceAndDetach_DoesNotDuplicateOrRetainHandler()
    {
        var definition = new KeyBindingDefinition("test-attach", Key.S);
        var keyBindingManager = new StubKeyBindingManager([definition]);
        var router = CreateRouter(keyBindingManager);
        var window = new Window();

        try
        {
            router.Attach(window);
            router.Attach(window);

            RaiseKeyDown(window, Key.S);
            Assert.Equal(1, keyBindingManager.CheckCallCount);

            router.Detach();
            RaiseKeyDown(window, Key.S);
            Assert.Equal(1, keyBindingManager.CheckCallCount);
        }
        finally
        {
            router.Detach();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ClosingAttachedWindow_DetachesHandler()
    {
        var definition = new KeyBindingDefinition("test-close", Key.S);
        var keyBindingManager = new StubKeyBindingManager([definition]);
        var router = CreateRouter(keyBindingManager);
        var window = new Window { Width = 160, Height = 90 };

        window.Show();
        router.Attach(window);
        window.Close();
        RaiseKeyDown(window, Key.S);

        Assert.Equal(0, keyBindingManager.CheckCallCount);
    }

    private static DefaultEditorKeyBindingRouter CreateRouter(
        StubKeyBindingManager keyBindingManager,
        ILogger<DefaultEditorKeyBindingRouter>? logger = null)
    {
        return new DefaultEditorKeyBindingRouter(
            keyBindingManager,
            new StubEditorDocumentManager(),
            logger ?? NullLogger<DefaultEditorKeyBindingRouter>.Instance);
    }

    private static KeyEventArgs RaiseKeyDown(InputElement source, Key key)
    {
        var eventArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = source,
            Key = key,
            KeyModifiers = KeyModifiers.None
        };
        source.RaiseEvent(eventArgs);
        return eventArgs;
    }

    private sealed class StubKeyBindingManager : IKeyBindingManager
    {
        public StubKeyBindingManager(IEnumerable<KeyBindingDefinition> definitions)
        {
            KeyBindingDefinations = definitions.ToArray();
        }

        public bool MatchAll { get; init; }

        public int CheckCallCount { get; private set; }

        public IEnumerable<KeyBindingDefinition> KeyBindingDefinations { get; }

        public bool CheckKeyBinding(KeyBindingDefinition defination, KeyEventArgs e)
        {
            CheckCallCount++;
            return MatchAll;
        }

        public void ChangeKeyBinding(KeyBindingDefinition definition, Key newKey, KeyModifiers newModifier)
        {
            throw new NotSupportedException();
        }

        public KeyBindingDefinition QueryKeyBinding(Key key, KeyModifiers modifier, KeyBindingLayer layer)
        {
            throw new NotSupportedException();
        }

        public void SaveConfig()
        {
            throw new NotSupportedException();
        }

        public void LoadConfig()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubEditorDocumentManager : IEditorDocumentManager
    {
        public event IEditorDocumentManager.NotifyCreateFunc OnNotifyCreated
        {
            add { }
            remove { }
        }

        public event IEditorDocumentManager.ActivateEditorChangedFunc OnActivateEditorChanged
        {
            add { }
            remove { }
        }

        public event IEditorDocumentManager.NotifyDestoryFunc OnNotifyDestoryed
        {
            add { }
            remove { }
        }

        public FumenVisualEditorViewModel CurrentActivatedEditor { get; } =
            (FumenVisualEditorViewModel)RuntimeHelpers.GetUninitializedObject(typeof(FumenVisualEditorViewModel));

        public void NotifyDeactivate(FumenVisualEditorViewModel editor)
        {
        }

        public void NotifyActivate(FumenVisualEditorViewModel editor)
        {
        }

        public void NotifyCreate(FumenVisualEditorViewModel editor)
        {
        }

        public void NotifyDestory(FumenVisualEditorViewModel editor)
        {
        }

        public IEnumerable<FumenVisualEditorViewModel> GetCurrentEditors()
        {
            return [CurrentActivatedEditor];
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NoopScope : IDisposable
        {
            public static NoopScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
