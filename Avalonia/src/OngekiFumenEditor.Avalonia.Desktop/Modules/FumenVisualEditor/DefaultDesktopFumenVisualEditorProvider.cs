#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Gekimini.Avalonia.Framework;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

namespace OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor;

// 注册自身与全部实现接口（IEditorProvider、IFumenVisualEditorProvider），接口由生成器
// 经代理工厂转发到同一单例；Skip 对展开产生的重复描述符按服务类型去重。
[RegisterSingleton<IEditorProvider>(Registration = RegistrationStrategy.SelfWithProxyFactory, Duplicate = DuplicateStrategy.Skip)]
public sealed class DefaultDesktopFumenVisualEditorProvider : FumenVisualEditorProviderBase
{
    public override bool CanCreateNew => true;

    protected override IEditorProjectSetupFilePicker CreateSetupFilePicker() =>
        new AvaloniaEditorProjectSetupFilePicker();

    protected override Task<EditorFileAccessContext> RestoreContextAsync(
        EditorFileAccessContextSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storageProvider = (Application.Current as global::OngekiFumenEditor.Avalonia.App)
            ?.TopLevel?.StorageProvider
            ?? throw new InvalidOperationException("No active Desktop storage provider is available.");
        return snapshot.ToContextAsync(storageProvider);
    }
}
