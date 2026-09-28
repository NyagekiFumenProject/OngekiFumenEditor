using Caliburn.Micro;
using Microsoft.CodeAnalysis.Differencing;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.IEditorDocumentManager;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
    [Export(typeof(IEditorDocumentManager))]
    [Export(typeof(ISchedulable))]
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

                IoC.Get<WindowTitleHelper>().UpdateWindowTitleByEditor(value);
            }
        }

        [ImportingConstructor]
        public DefaultEditorDocumentManager(ISchedulerManager schedulerManager)
        {
            this.schedulerManager = schedulerManager;
            UpdateAutoSaveStatus();
            Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
        }

        public void NotifyDeactivate(FumenVisualEditorViewModel editor)
        {
            Log.LogDebug($"editor deactivated: {editor.GetHashCode()} {editor.DisplayName}");
            var otherActive = currentEditor.Where(x => x != editor).FirstOrDefault(x => x.IsActive);
            CurrentActivatedEditor = otherActive;
        }

        public void NotifyActivate(FumenVisualEditorViewModel editor)
        {
            Log.LogDebug($"editor activated: {editor.GetHashCode()} {editor.DisplayName}");
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
            OnNotifyDestoryed?.Invoke(editor);
        }

        public void OnSchedulerTerm()
        {
            Properties.EditorGlobalSetting.Default.PropertyChanged -= Default_PropertyChanged;
        }

        public async Task OnScheduleCall(CancellationToken cancellationToken)
        {
            if (!Properties.EditorGlobalSetting.Default.IsEnableAutoSave)
                return;

            if (Properties.EditorGlobalSetting.Default.AutoSaveAllDirtyDocuments)
            {
                //批量自动保存跑在调度器后台线程上，无法弹出「另存为」对话框，因此只保存已经拥有
                //FilePath 的脏文档；没有保存路径的文档需要用户交互选择位置，留给用户手动保存。
                //先拍快照，避免保存过程中集合被其它线程(创建/关闭文档)修改。
                var editors = GetCurrentEditors().ToArray();
                foreach (var dirtyEditor in editors)
                {
                    if (!dirtyEditor.IsDirty || string.IsNullOrWhiteSpace(dirtyEditor.FilePath))
                        continue;

                    try
                    {
                        Log.LogInfo($"begin auto save dirty document: {dirtyEditor.FileName}");
                        await dirtyEditor.Save(dirtyEditor.FilePath);
                        Log.LogInfo($"auto save dirty document done: {dirtyEditor.FileName}");
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"auto save dirty document failed: {dirtyEditor.FileName}", ex);
                    }
                }

                return;
            }

            if (CurrentActivatedEditor is null || string.IsNullOrWhiteSpace(CurrentActivatedEditor.FilePath) || Dispatcher.CurrentDispatcher is not Dispatcher dispatcher)
                return;

            var editor = CurrentActivatedEditor;
            Log.LogInfo($"begin auto save current document: {editor.FileName}");
            //editor.LockAllUserInteraction();
            await editor.Save(editor.FilePath);
            //editor.UnlockAllUserInteraction();
            Log.LogInfo($"auto save done.");
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
