using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Window.Views;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Dialogs;

public partial class ConfigKeyBindingDialog : WindowViewBase, INotifyPropertyChanged
{
    private readonly IKeyBindingManager keyBindingManager;
    private readonly IDialogManager dialogManager;
    private IReadOnlyList<KeyBindingDefinition> conflictDefinitions = [];

    public KeyBindingDefinition Definition { get; }

    private KeyModifiers modifier;
    private Key key;

    public new event PropertyChangedEventHandler PropertyChanged;

    public string CurrentExpression => KeyBindingDefinition.FormatToExpression(key, modifier);

    public IReadOnlyList<KeyBindingDefinition> ConflictDefinitions => conflictDefinitions;
    public KeyBindingDefinition ConflictDefinition => conflictDefinitions.FirstOrDefault();
    public string ConflictDisplayName => string.Join(", ", conflictDefinitions.Select(x => x.DisplayName));
    public bool HasConflict => conflictDefinitions.Count > 0;

    public ConfigKeyBindingDialog(KeyBindingDefinition definition)
        : this(definition, null, null)
    {
    }

    internal ConfigKeyBindingDialog(
        KeyBindingDefinition definition,
        IKeyBindingManager keyBindingManager,
        IDialogManager dialogManager)
    {
        Definition = definition;
        this.keyBindingManager = keyBindingManager;
        this.dialogManager = dialogManager;
        key = definition.Key;
        modifier = definition.Modifiers;

        DataContext = this;
        InitializeComponent();

        ConfirmButton.Click += OnConfirmButtonClick;
        ClearButton.Click += OnClearButtonClick;
        ResetButton.Click += OnResetButtonClick;

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
    }

    public ConfigKeyBindingDialog()
        : this(new KeyBindingDefinition("kbd_none", Key.None))
    {
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (!IsActive)
            return;

        var inputKey = e.Key;
        if (Definition.Key == Key.None)
            TryClearModifier(inputKey);

        UpdateExpression();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsActive)
            return;

        var inputKey = e.Key;
        if (TryGetModifier(inputKey, out var inputModifier))
        {
            modifier = inputModifier;
            key = Key.None;
        }
        else
        {
            key = inputKey;
        }

        UpdateExpression();
    }

    private static bool TryGetModifier(Key key, out KeyModifiers modifier)
    {
        switch (key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
                modifier = KeyModifiers.Control;
                return true;
            case Key.LeftShift:
            case Key.RightShift:
                modifier = KeyModifiers.Shift;
                return true;
            case Key.LeftAlt:
            case Key.RightAlt:
                modifier = KeyModifiers.Alt;
                return true;
            case Key.LWin:
            case Key.RWin:
                modifier = KeyModifiers.Meta;
                return true;
            default:
                modifier = KeyModifiers.None;
                return false;
        }
    }

    private bool TryClearModifier(Key key)
    {
        switch (key)
        {
            case Key.LeftCtrl:
            case Key.RightCtrl:
            case Key.LeftShift:
            case Key.RightShift:
            case Key.LeftAlt:
            case Key.RightAlt:
            case Key.LWin:
            case Key.RWin:
                modifier = KeyModifiers.None;
                return true;
            default:
                return false;
        }
    }

    public void Clear()
    {
        key = Key.None;
        modifier = KeyModifiers.None;
        UpdateExpression();
    }

    private async void OnConfirmButtonClick(object sender, RoutedEventArgs e)
    {
        ConfirmButton.IsEnabled = false;
        try
        {
            if (await TryApplyAsync())
                Close(true);
        }
        finally
        {
            ConfirmButton.IsEnabled = true;
        }
    }

    private void OnClearButtonClick(object sender, RoutedEventArgs e)
    {
        Clear();
    }

    private void OnResetButtonClick(object sender, RoutedEventArgs e)
    {
        ApplyDefault();
    }

    public void ApplyDefault()
    {
        key = Definition.DefaultKey;
        modifier = Definition.DefaultModifiers;
        UpdateExpression();
    }

    public async Task<bool> TryApplyAsync()
    {
        UpdateConflicts();
        if (HasConflict && !await DialogManager.ShowComfirmDialog(
                Lang.ConflictNotifyComfirm.Format(ConflictDisplayName),
                Lang.Warning))
        {
            Log.LogInfo($"Key binding conflict replacement canceled: {ConflictDisplayName}");
            return false;
        }

        KeyBindingManager.ChangeKeyBindingResolvingConflicts(Definition, key, modifier);
        SetConflictDefinitions([]);
        return true;
    }

    private void UpdateExpression()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentExpression)));

        UpdateConflicts();
    }

    private void UpdateConflicts()
    {
        SetConflictDefinitions(KeyBindingManager.QueryKeyBindingConflicts(Definition, key, modifier));
    }

    private void SetConflictDefinitions(IReadOnlyList<KeyBindingDefinition> value)
    {
        conflictDefinitions = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConflictDefinitions)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConflictDefinition)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConflictDisplayName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasConflict)));
    }

    private IKeyBindingManager KeyBindingManager => keyBindingManager ?? IoC.Get<IKeyBindingManager>();
    private IDialogManager DialogManager => dialogManager ?? IoC.Get<IDialogManager>();
}

