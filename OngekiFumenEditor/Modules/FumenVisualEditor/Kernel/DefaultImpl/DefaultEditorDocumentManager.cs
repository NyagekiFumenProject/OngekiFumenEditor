using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.IEditorDocumentManager;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
    [Export(typeof(IEditorDocumentManager))]
    [Export(typeof(ISchedulable))]
    public class DefaultEditorDocumentManager : IEditorDocumentManager, ISchedulable
    {
        private HashSet<FumenVisualEditorViewModel> currentEditor = new();
        public event ActivateEditorChangedFunc OnActivateEditorChanged;

        public string SchedulerName => "DefaultEditorDocumentManager.AutoSaveScheduler";

        private TimeSpan scheduleCallLoopInterval;
        public TimeSpan ScheduleCallLoopInterval => scheduleCallLoopInterval;

        private FumenVisualEditorViewModel currentActivatedEditor;
        public FumenVisualEditorViewModel CurrentActivatedEditor
        {
            get => currentActivatedEditor;
            private set
            {
                var old = currentActivatedEditor;
                currentActivatedEditor = value;
                OnActivateEditorChanged?.Invoke(value, old);
            }
        }

        public DefaultEditorDocumentManager()
        {
            UpdateAutoSaveStatus();
            Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
        }

        public void NotifyDeactivate(FumenVisualEditorViewModel editor)
        {
            Log.LogInfo($"editor deactivated: {editor.GetHashCode()} {editor.DisplayName}");
            var otherActive = currentEditor.Where(x => x != editor).FirstOrDefault(x => x.IsActive);
            CurrentActivatedEditor = otherActive;
        }

        public void NotifyActivate(FumenVisualEditorViewModel editor)
        {
            Log.LogInfo($"editor activated: {editor.GetHashCode()} {editor.DisplayName}");
            CurrentActivatedEditor = editor;
        }

        public void NotifyCreate(FumenVisualEditorViewModel editor)
        {
            Log.LogInfo($"editor created: {editor.GetHashCode()} {editor.DisplayName}");
            currentEditor.Add(editor);
        }

        public void NotifyDestory(FumenVisualEditorViewModel editor)
        {
            Log.LogInfo($"editor destoryed: {editor.GetHashCode()} {editor.DisplayName}");
            currentEditor.Remove(editor);
            if (CurrentActivatedEditor == editor)
                NotifyDeactivate(editor);
        }

        public void OnSchedulerTerm()
        {
            Properties.EditorGlobalSetting.Default.PropertyChanged -= Default_PropertyChanged;
        }

        public async Task OnScheduleCall(CancellationToken cancellationToken)
        {
            if (!Properties.EditorGlobalSetting.Default.IsEnableAutoSave)
                return;

            if (CurrentActivatedEditor is null || string.IsNullOrWhiteSpace(CurrentActivatedEditor.FilePath))
                return;

            Log.LogInfo($"begin auto save current document: {CurrentActivatedEditor.FileName}");
            CurrentActivatedEditor.LockAllUserInteraction();
            await CurrentActivatedEditor.Save(CurrentActivatedEditor.FilePath);
            CurrentActivatedEditor.UnlockAllUserInteraction();
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

            var schedulerManager = IoC.Get<ISchedulerManager>();
            if (Properties.EditorGlobalSetting.Default.IsEnableAutoSave)
                schedulerManager.AddScheduler(this);
            else
                schedulerManager.RemoveScheduler(this);
        }
    }
}
