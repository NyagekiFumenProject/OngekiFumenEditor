using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.AudioAdjustWindow.ViewModels
{
    [Export(typeof(IAudioAdjustWindow))]
    public class AudioAdjustWindowViewModel : WindowBase, IAudioAdjustWindow
    {
        private string inputFumenFilePath = "";
        public string InputFumenFilePath { get => inputFumenFilePath; set => Set(ref inputFumenFilePath, value); }

        private string outputFumenFilePath = "";
        public string OutputFumenFilePath { get => outputFumenFilePath; set => Set(ref outputFumenFilePath, value); }

        private bool isUseInputFile = true;
        public bool IsUseInputFile
        {
            get => isUseInputFile;
            set
            {
                if (CurrentEditorName is null)
                    Set(ref isUseInputFile, true);
                else
                    Set(ref isUseInputFile, value);
                NotifyOfPropertyChange(() => IsCurrentEditorAsInputFumen);
                NotifyOfPropertyChange(() => CurrentEditorName);
                NotifyOfPropertyChange(() => Bpm);
            }
        }

        public bool IsCurrentEditorAsInputFumen
        {
            get => !IsUseInputFile;
            set => IsUseInputFile = !value;
        }

        public string CurrentEditorName => IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor?.DisplayName;

        public float Unit { get; set; } = 0;
        public int Grid { get; set; } = 0;
        public float Seconds { get; set; } = 0;

        private double? bpm = null;
        public double Bpm
        {
            get
            {
                if (IsCurrentEditorAsInputFumen)
                    return IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor?.Fumen.BpmList.FirstBpm.BPM ?? 0;
                if (bpm is double b)
                    return b;
                return default;
            }
            set => Set(ref bpm, value);
        }

        private bool isUseGridOffset = false;
        public bool IsUseGridOffset
        {
            get => isUseGridOffset;
            set
            {
                Set(ref isUseGridOffset, value);
            }
        }

        private bool isRecalculateObjects = false;
        public bool IsRecalculateObjects
        {
            get => isRecalculateObjects;
            set
            {
                Set(ref isRecalculateObjects, value);
            }
        }

        public AudioAdjustWindowViewModel()
        {
            NotifyOfPropertyChange(() => CurrentEditorName);
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += (n, _) => NotifyOfPropertyChange(() => CurrentEditorName);
        }

        public void OnOpenSelectInputFileDialog()
        {
            var result = FileDialogHelper.OpenFile("选择音频文件", FileDialogHelper.GetSupportAudioFileExtensionFilterList());
            if (!string.IsNullOrWhiteSpace(result))
            {
                InputFumenFilePath = result;
                IsUseInputFile = true;
            }
        }

        public void OnOpenSelectOutputFileDialog()
        {
            var result = FileDialogHelper.SaveFile("保存新音频文件路径", new[] { (".wav", ".wav音频文件") });
            if (!string.IsNullOrWhiteSpace(result))
                OutputFumenFilePath = result;
        }

        public async void OnExecuteConverter()
        {
            var audioFilePath = "";
            var currentEditor = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor;

            if (IsUseInputFile)
            {
                if (!File.Exists(InputFumenFilePath))
                {
                    MessageBox.Show("请先选择要处理的音频文件");
                    return;
                }

                audioFilePath = InputFumenFilePath;
            }
            else
            {
                audioFilePath = currentEditor.EditorProjectData.AudioFilePath;
            }

            if (!File.Exists(audioFilePath))
            {
                MessageBox.Show("无音频文件输入");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputFumenFilePath))
            {
                MessageBox.Show("没钦定新的音频文件保存位置");
                return;
            }

            var timeOffset = TimeSpan.Zero;
            if (IsUseGridOffset)
            {
                var bpmChange = new BPMChange()
                {
                    BPM = Bpm,
                    TGrid = TGrid.Zero
                };
                var offset = new GridOffset(Unit, Grid);
                var msec = MathUtils.CalculateBPMLength(bpmChange, TGrid.Zero + offset, 240);
                timeOffset = TimeSpan.FromMilliseconds(msec);
            }
            else
            {
                timeOffset = TimeSpan.FromSeconds(Seconds);
            }

            try
            {
                var audio = AcbGeneratorFuck.Generator.LoadAsWavFile(audioFilePath);
                var offseted = AcbGeneratorFuck.Generator.AdjustDuration(audio, timeOffset.TotalSeconds);

                using var fs = File.OpenWrite(OutputFumenFilePath);

                if (IsCurrentEditorAsInputFumen)
                {
                    if (IsRecalculateObjects)
                    {
                        var offset = currentEditor.Fumen.BpmList.FirstBpm.LengthConvertToOffset(timeOffset.TotalMilliseconds, 240);
                        var map = new Dictionary<ITimelineObject, (TGrid before, TGrid after)>();

                        foreach (var timelineObject in currentEditor.Fumen.GetAllDisplayableObjects().OfType<ITimelineObject>())
                        {
                            var newTGrid = timelineObject.TGrid + offset;
                            if (newTGrid is null)
                            {
                                MessageBox.Show($"存在某个物件无法应用新的延迟：{timelineObject}");
                                return;
                            }

                            map[timelineObject] = (timelineObject.TGrid, newTGrid);
                        }

                        currentEditor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("应用音频延迟", () =>
                        {
                            foreach (var item in map)
                                item.Key.TGrid = item.Value.after.CopyNew();
                        }, () =>
                        {
                            foreach (var item in map)
                                item.Key.TGrid = item.Value.before.CopyNew();
                        }));
                    }
                }

                offseted.SaveTo(fs);
                if (IsCurrentEditorAsInputFumen)
                    MessageBox.Show($"处理完成，但推荐您保存并重新打开当前项目，以查看最新的音频变更，或者物件位置变动。");
                else
                    MessageBox.Show($"处理完成");
            }
            catch (Exception e)
            {
                MessageBox.Show($"处理失败：{e.Message}");
            }
        }
    }
}
