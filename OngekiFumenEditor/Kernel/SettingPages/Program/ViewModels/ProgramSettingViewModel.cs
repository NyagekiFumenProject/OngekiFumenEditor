using AssocSupport;
using AssocSupport.Models;
using Caliburn.Micro;
using Gemini.Modules.Settings;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.IO;
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

        public ProgramSettingViewModel()
        {
            Setting.PropertyChanged += SettingPropertyChanged;
        }

        private void SettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Log.LogDebug($"logs setting property changed : {e.PropertyName}");
        }

        public string SettingsPageName => Resources.TabProgram;

        public string SettingsPagePath => Resources.TabEnviorment;

        public void ApplyChanges()
        {
            Setting.Save();
        }

        public void OnDumpFolderPathButtonClick()
        {
            using var openFolderDialog = new FolderBrowserDialog();
            openFolderDialog.ShowNewFolderButton = true;
            openFolderDialog.SelectedPath = Path.GetFullPath(Setting.DumpFileDirPath);
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
            var iconFolder = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Resources", "FileAssociationIcons");
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
