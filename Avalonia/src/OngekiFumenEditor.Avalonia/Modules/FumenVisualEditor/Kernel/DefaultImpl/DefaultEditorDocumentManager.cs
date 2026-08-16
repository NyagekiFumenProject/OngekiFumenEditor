using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Gekimini.Avalonia.Platforms.Services.MainWindow;
using Injectio.Attributes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.IEditorDocumentManager;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Utils.DeadHandler;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
    [RegisterSingleton<IEditorDocumentManager>]
    [RegisterSingleton<ISchedulable>]
    public class DefaultEditorDocumentManager : IEditorDocumentManager, ISchedulable
    {
        private HashSet<FumenVisualEditorViewModel> currentEditor = new();
        public event ActivateEditorChangedFunc OnActivateEditorChanged;
        public event NotifyCreateFunc OnNotifyCreated;
        public event NotifyDestoryFunc OnNotifyDestoryed;

        public string SchedulerName => "DefaultEditorDocumentManager.AutoSaveScheduler";

        private TimeSpan scheduleCallLoopInterval;
        public TimeSpan ScheduleCallLoopInterval => scheduleCallLoopInterval;

        private FumenVisualEditorViewModel currentActivatedEditor;
        private readonly ISchedulerManager schedulerManager;

        public FumenVisualEditorViewModel CurrentActivatedEditor
        {
            get => currentActivatedEditor;
            private set
            {
                var old = currentActivatedEditor;
                currentActivatedEditor = value;
                OnActivateEditorChanged?.Invoke(value, old);

                try
                {
                    // 对齐 WPF WindowTitleHelper：活动编辑器变化时刷新主窗口标题。
                    IoC.Get<IPlatformMainWindow>().Title = "Ongeki Fumen Editor" +
                        (value is not null ? $" - {value.DisplayName} " : string.Empty);
                }
                catch (InvalidOperationException)
                {
                    // 无 GUI 主窗口的环境（测试/命令行）下忽略窗口标题刷新。
                }
            }
        }

        
        public DefaultEditorDocumentManager(ISchedulerManager schedulerManager)
        {
            this.schedulerManager = schedulerManager;
            UpdateAutoSaveStatus();
            Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
        }

        public void NotifyDeactivate(FumenVisualEditorViewModel editor)
        {
            Log.LogDebug($"editor deactivated: {editor.GetHashCode()} {editor.DisplayName}");
            // Gekimini 下活动文档由 IShell 驱动，不再按 IsActive 自选替补；
            // shell 的 ActiveDocumentChanged 会随后告知新的活动编辑器。
            if (ReferenceEquals(CurrentActivatedEditor, editor))
                CurrentActivatedEditor = null;
        }

        public void NotifyActivate(FumenVisualEditorViewModel editor)
        {
            Log.LogDebug($"editor activated: {editor.GetHashCode()} {editor.DisplayName}");
            // shell 事件顺序不保证先 Opened 后 Activated，未登记时先按创建处理。
            if (!currentEditor.Contains(editor))
                NotifyCreate(editor);
            CurrentActivatedEditor = editor;
        }

        public void NotifyCreate(FumenVisualEditorViewModel editor)
        {
            Log.LogDebug($"editor created: {editor.GetHashCode()} {editor.DisplayName}");
            currentEditor.Add(editor);
            OnNotifyCreated?.Invoke(editor);
        }

        public void NotifyDestory(FumenVisualEditorViewModel editor)
        {
            Log.LogDebug($"editor destoryed: {editor.GetHashCode()} {editor.DisplayName}");
            currentEditor.Remove(editor);
            if (CurrentActivatedEditor == editor)
                NotifyDeactivate(editor);
            try
            {
                OnNotifyDestoryed?.Invoke(editor);
            }
			finally
			{
				editor.Dispose();
			}
        }

        public void OnSchedulerTerm()
        {
            Properties.EditorGlobalSetting.Default.PropertyChanged -= Default_PropertyChanged;
        }

        public async Task OnScheduleCall(CancellationToken cancellationToken)
        {
            if (!Properties.EditorGlobalSetting.Default.IsEnableAutoSave)
                return;

            if (CurrentActivatedEditor is null)
                return;

            var editor = CurrentActivatedEditor;
            if (editor.EditorContext?.ProjectFile is null)
                return;

            if (!EditorProjectIoGate.TryEnter(out var lease))
            {
                Log.LogDebug($"Skip recovery snapshot for '{editor.FileName}' because project I/O is busy.");
                return;
            }

            using (lease)
            {
                try
                {
                    var snapshot = await FumenRescue.SaveRecoverySnapshotAsync(editor, cancellationToken);
                    if (snapshot is not null)
                        Log.LogInfo($"Recovery snapshot updated for '{editor.FileName}'.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Log.LogWarn($"Unable to update recovery snapshot for '{editor.FileName}': {exception.Message}");
                }
            }
        }

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.EditorGlobalSetting.AutoSaveTimeInterval):
                case nameof(Properties.EditorGlobalSetting.IsEnableAutoSave):
                    UpdateAutoSaveStatus();
                    break;
                default:
                    break;
            }
        }

        private void UpdateAutoSaveStatus()
        {
            scheduleCallLoopInterval = TimeSpan.FromMinutes(Properties.EditorGlobalSetting.Default.AutoSaveTimeInterval);

            if (Properties.EditorGlobalSetting.Default.IsEnableAutoSave)
                schedulerManager.AddScheduler(this);
            else
                schedulerManager.RemoveScheduler(this);
        }

        public IEnumerable<FumenVisualEditorViewModel> GetCurrentEditors()
        {
            return currentEditor;
        }
    }
}



