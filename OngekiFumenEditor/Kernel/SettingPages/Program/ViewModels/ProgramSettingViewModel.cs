using AssocSupport;
using AssocSupport.Models;
using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Kernel.Graphics.Skia;
using OngekiFumenEditor.Kernel.ProgramUpdater;
using OngekiFumenEditor.Kernel.ProgramUpdater.Dialogs.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OngekiFumenEditor.Kernel.SettingPages.Program.ViewModels
{
    [Export(typeof(ISettingsEditor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ProgramSettingViewModel : PropertyChangedBase, ISettingsEditor
    {
        public ProgramSetting Setting => ProgramSetting.Default;

        public bool EnableAssociate => !AssociationUtility.IsRegistered("OngekiFumenEditor", "NyagekiFumenProject");

        public IEnumerable<string> AvaliableRenderManagerImplNames => IoC.Get<IRenderManager>().GetAvaliableRenderManagerImplNames();

        public IEnumerable<string> AvaliableSkiaBackends => Enum.GetNames<RenderBackendType>();

        public IEnumerable<int> D3DRenderQueueFrameCountOptions { get; } = Enumerable.Range(2, 4);

        /// <summary>编辑器字体家族候选：首项为空字符串表示使用平台默认字体。</summary>
        public IEnumerable<string> EditorFontFamilyNames { get; } = GetEditorFontFamilyNames();

        /// <summary>MSAA 采样数候选。0 表示关闭（保持默认行为）。</summary>
        public IEnumerable<int> MsaaSampleCountOptions { get; } = new[] { 0, 2, 4, 8 };

        /// <summary>进程优先级档位候选，Value 与 <see cref="ProgramSetting.ProcessPriorityTier"/> 取值对应。</summary>
        public IEnumerable<KeyValuePair<int, string>> ProcessPriorityTierOptions { get; } = new[]
        {
            new KeyValuePair<int, string>(0, "Normal"),
            new KeyValuePair<int, string>(1, "BelowNormal"),
            new KeyValuePair<int, string>(2, "AboveNormal"),
            new KeyValuePair<int, string>(3, "High"),
        };

        /// <summary>OpenGL 实现专属选项（MSAA/兼容性/GL 调试日志）是否可见。</summary>
        public bool IsOpenGLBackend => string.Equals(Setting.DefaultRenderManagerImplementName, OpenGLRenderManagerImplName, StringComparison.OrdinalIgnoreCase);

        /// <summary>Skia 实现专属选项（Skia 后端选择/渲染队列深度）是否可见。</summary>
        public bool IsSkiaBackend => string.Equals(Setting.DefaultRenderManagerImplementName, SkiaRenderManagerImplName, StringComparison.OrdinalIgnoreCase);

        /// <summary>D3D 渲染队列深度仅对 Skia + DirectX12 后端生效（该后端由 D3D9On12 呈现实现消费）。</summary>
        public bool IsSkiaDirectX12Backend => IsSkiaBackend && string.Equals(Setting.SkiaRenderBackend, nameof(RenderBackendType.DirectX12), StringComparison.OrdinalIgnoreCase);

        private const string OpenGLRenderManagerImplName = "OpenGL";
        private const string SkiaRenderManagerImplName = "Skia";

        private static IEnumerable<string> GetEditorFontFamilyNames()
        {
            var names = new List<string> { string.Empty };

            try
            {
                names.AddRange(SixLabors.Fonts.SystemFonts.Families
                    .Select(x => x.Name)
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            }
            catch (Exception ex)
            {
                Log.LogWarn($"Failed to enumerate system font families: {ex.Message}");
            }

            var current = ProgramSetting.Default.EditorFontFamilyName;
            if (!string.IsNullOrWhiteSpace(current) && !names.Contains(current, StringComparer.OrdinalIgnoreCase))
                names.Add(current);

            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Length == 0 ? 0 : 1)
                .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public int D3DRenderQueueFrameCount
        {
            get => Math.Clamp(Setting.D3DRenderQueueFrameCount, 2, 5);
            set
            {
                var clamped = Math.Clamp(value, 2, 5);
                if (Setting.D3DRenderQueueFrameCount == clamped)
                    return;

                Setting.D3DRenderQueueFrameCount = clamped;
                NotifyOfPropertyChange();
                ApplyChanges();
            }
        }

        private bool enableAssociateNyagekiProj = true;
        public bool EnableAssociateNyagekiProj
        {
            get => enableAssociateNyagekiProj;
            set => Set(ref enableAssociateNyagekiProj, value);
        }

        private bool enableAssociateNyageki = true;
        public bool EnableAssociateNyageki
        {
            get => enableAssociateNyageki;
            set => Set(ref enableAssociateNyageki, value);
        }

        private bool enableAssociateOgkr = true;
        public bool EnableAssociateOgkr
        {
            get => enableAssociateOgkr;
            set => Set(ref enableAssociateOgkr, value);
        }

        private bool enableAssociateNyagekiScript = true;
        public bool EnableAssociateNyagekiScript
        {
            get => enableAssociateNyagekiScript;
            set => Set(ref enableAssociateNyagekiScript, value);
        }

        public ProgramSettingViewModel()
        {
            ProgramUpdater = IoC.Get<IProgramUpdater>();
            Setting.PropertyChanged += SettingPropertyChanged;
        }

        private void SettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Log.LogDebug($"logs setting property changed : {e.PropertyName}");
            if (e.PropertyName == nameof(ProgramSetting.D3DRenderQueueFrameCount))
                NotifyOfPropertyChange(() => D3DRenderQueueFrameCount);
            else if (e.PropertyName == nameof(ProgramSetting.DefaultRenderManagerImplementName))
            {
                NotifyOfPropertyChange(() => IsOpenGLBackend);
                NotifyOfPropertyChange(() => IsSkiaBackend);
                NotifyOfPropertyChange(() => IsSkiaDirectX12Backend);
            }
            else if (e.PropertyName == nameof(ProgramSetting.SkiaRenderBackend))
                NotifyOfPropertyChange(() => IsSkiaDirectX12Backend);
        }

        public string SettingsPageName => Resources.TabProgram;

        public string SettingsPagePath => Resources.TabEnviorment;

        public IProgramUpdater ProgramUpdater { get; }

        public void ApplyChanges()
        {
            Setting.Save();
        }

        public void OnDumpFolderPathButtonClick()
        {
            using var openFolderDialog = new FolderBrowserDialog();
            openFolderDialog.ShowNewFolderButton = true;
            openFolderDialog.SelectedPath = Path.GetFullPath(AppDirectoryHelper.ResolveRelative(Setting.DumpFileDirPath));
            if (openFolderDialog.ShowDialog() == DialogResult.OK)
            {
                var folderPath = openFolderDialog.SelectedPath;
                if (!Directory.Exists(folderPath))
                {
                    MessageBox.Show(Resources.ErrorFolderIsEmpty);
                    OnDumpFolderPathButtonClick();
                    return;
                }
                Setting.DumpFileDirPath = folderPath;
                ApplyChanges();
            }
        }

        public void ThrowException()
        {
            Task.Run(() => throw new Exception("塔塔开!"));
        }

        public async void RegisterNyagekiAssociations()
        {
            var iconFolder = Path.Combine(AppDirectoryHelper.ExecutableDirectory, "Resources", "FileAssociationIcons");
            var iconFilePath = Path.Combine(iconFolder, "icon.ico");

            if (!File.Exists(iconFilePath))
            {
                Directory.CreateDirectory(iconFolder);
                var streamInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/logo32.ico"));
                using var fs = streamInfo.Stream;
                using var fs2 = File.OpenWrite(iconFilePath);
                await fs.CopyToAsync(fs2);
            }

            var software = new Software
            {
                Name = "OngekiFumenEditor",
                CompanyName = "NyagekiFumenProject",
                Description = "Make Offgeki Great Again!",
                Icon = iconFilePath,
            };

            if (EnableAssociateNyagekiProj)
            {
                software.Identifiers.Add(new ProgrammaticID
                {
                    Type = new FileType
                    {
                        Extension = ".nyagekiProj",
                        ContentType = "application/sample",
                        PerceivedType = PerceivedTypes.Application,
                    },
                    Command = new ShellCommand
                    {
                        Path = Application.ExecutablePath,
                        Argument = "%1"
                    },
                    Description = "Ongeki Fumen Editor Fumen Project File",
                    Icon = iconFilePath,
                });
            }

            if (EnableAssociateNyageki)
            {
                software.Identifiers.Add(new ProgrammaticID
                {
                    Type = new FileType
                    {
                        Extension = ".nyageki",
                        ContentType = "application/sample",
                        PerceivedType = PerceivedTypes.Application,
                    },
                    Command = new ShellCommand
                    {
                        Path = Application.ExecutablePath,
                        Argument = "%1"
                    },
                    Description = "Ongeki Fumen Editor Fumen File",
                    Icon = iconFilePath,
                });
            }

            if (EnableAssociateOgkr)
            {
                software.Identifiers.Add(new ProgrammaticID
                {
                    Type = new FileType
                    {
                        Extension = ".ogkr",
                        ContentType = "application/sample",
                        PerceivedType = PerceivedTypes.Application,
                    },
                    Command = new ShellCommand
                    {
                        Path = Application.ExecutablePath,
                        Argument = "%1"
                    },
                    Description = "Ongeki Fumen File",
                    Icon = iconFilePath,
                });
            }

            if (EnableAssociateNyagekiScript)
            {
                software.Identifiers.Add(new ProgrammaticID
                {
                    Type = new FileType
                    {
                        Extension = ".nyagekiScript",
                        ContentType = "application/sample",
                        PerceivedType = PerceivedTypes.Application,
                    },
                    Command = new ShellCommand
                    {
                        Path = Application.ExecutablePath,
                        Argument = "%1"
                    },
                    Description = "Ongeki Fumen Editor Script File",
                    Icon = iconFilePath,
                });
            }

            if (software.Identifiers.Count == 0)
            {
                MessageBox.Show(Resources.RegisterOneFileTypeAtLeast, Resources.FileAssociation);
                return;
            }

            try
            {
                var content = JsonSerializer.Serialize(software);
                Log.LogDebug($"software = {content}");

                if (AssociationUtility.Register(software))
                    MessageBox.Show(Resources.RegisterSuccess, Resources.FileAssociation);
                else
                    MessageBox.Show(Resources.RegisterFail, Resources.FileAssociation);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(Resources.RequestAdminPermission, Resources.FileAssociation);
            }

            NotifyOfPropertyChange(() => EnableAssociate);
        }

        public void ResetAllSettings()
        {
            if (MessageBox.Show(Resources.ResetAllSettingComfirm, Resources.Warning, MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            var settingList = new ApplicationSettingsBase[] {
                Properties.AudioPlayerToolViewerSetting.Default,
                Properties.AudioSetting.Default,
                Properties.EditorGlobalSetting.Default,
                Properties.LogSetting.Default,
                Properties.OptionGeneratorToolsSetting.Default,
                Properties.ProgramSetting.Default,
            };

            foreach (var setting in settingList)
            {
                setting.Reset();
                setting.Save();
            }

            MessageBox.Show(Resources.ResetCompleted);
        }

        public async Task CheckUpdate(ActionExecutionContext e)
        {
            using var _ = e.DisableSourceByDisposable();
            await ProgramUpdater.CheckUpdatable();
        }

        public async Task OpenShowNewVersionDialog(ActionExecutionContext e)
        {
            await IoC.Get<IWindowManager>().ShowWindowAsync(new ShowNewVersionDialogViewModel());
        }

        public async Task OpenRenderPerfomenceMeasurePanel()
        {
            await IoC.Get<IWindowManager>().ShowWindowAsync(IoC.Get<IRenderPerfomenceMeasurePanel>());
        }

        public void UnRegisterNyagekiAssociations()
        {
            try
            {
                if (AssociationUtility.Unregister("OngekiFumenEditor", "NyagekiFumenProject"))
                    MessageBox.Show(Resources.UnregisterSuccess, Resources.FileAssociation);
                else
                    MessageBox.Show(Resources.UnregisterFail, Resources.FileAssociation);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(Resources.RequestAdminPermission, Resources.FileAssociation);
            }
            catch
            {
                MessageBox.Show(Resources.UnregisterFail, Resources.FileAssociation);
            }

            NotifyOfPropertyChange(() => EnableAssociate);
        }
    }
}
