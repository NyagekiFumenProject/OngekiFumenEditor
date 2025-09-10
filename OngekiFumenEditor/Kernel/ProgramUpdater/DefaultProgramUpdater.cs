﻿using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Menus;
using Gemini.Modules.MainMenu;
using Gemini.Modules.MainMenu.Controls;
using Gemini.Modules.MainMenu.Models;
using Gemini.Modules.MainMenu.Views;
using Gemini.Modules.MainWindow.Views;
using OngekiFumenEditor.Kernel.MiscMenu.Commands.About;
using OngekiFumenEditor.Kernel.ProgramUpdater.Dialogs.ViewModels;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Markup;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OngekiFumenEditor.Kernel.ProgramUpdater
{
    [Export(typeof(IProgramUpdater))]
    [Export(typeof(ISchedulable))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class DefaultProgramUpdater : PropertyChangedBase, IProgramUpdater, ISchedulable
    {
        private const string ApiEndPoint = "https://fumen.naominet.live";

        public bool HasNewVersion
        {
            get
            {
                if (RemoteVersionInfo?.Version is not Version remoteVersion)
                    return false;
                var localVersion = Version.Parse(ThisAssembly.AssemblyFileVersion);

                return remoteVersion > localVersion;
            }
        }

        private VersionInfo remoteVersionInfo;
        public VersionInfo RemoteVersionInfo
        {
            get => remoteVersionInfo;
            set
            {
                Set(ref remoteVersionInfo, value);
                NotifyOfPropertyChange(nameof(HasNewVersion));

                App.Current.Dispatcher.Invoke(() =>
                {
                    if (updatableButton is not null)
                        updatableButton.Visibility = HasNewVersion ? Visibility.Visible : Visibility.Collapsed;
                });
            }
        }

        public string SchedulerName => "Program Update Check Scheduler";

        public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromMinutes(5);

        private HttpClient http;

        private bool isModified = false;
        private Button updatableButton;

        private void ModifyFrameworkMenuView()
        {
            if (isModified)
                return;

            IEnumerable<T> GetAllMenuItems2<T>(DependencyObject parent)
            {
                if (parent is T menuItem)
                    yield return menuItem;

                foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
                    foreach (var f in GetAllMenuItems2<T>(child))
                        yield return f;
            }

            var mainMenuView2 = GetAllMenuItems2<MainMenuView>(App.Current.MainWindow).FirstOrDefault();
            var contentPresent = mainMenuView2.Parent as ContentControl;
            contentPresent.Content = null;

            var grid = new Grid();

            grid.SetResourceReference(Grid.BackgroundProperty, "MenuDefaultBackground");

            ColumnDefinition column1 = new ColumnDefinition()
            {
                Width = new GridLength(1, GridUnitType.Star)
            };
            ColumnDefinition column2 = new ColumnDefinition()
            {
                Width = GridLength.Auto
            };
            grid.ColumnDefinitions.Add(column1);
            grid.ColumnDefinitions.Add(column2);

            Grid.SetColumn(mainMenuView2, 0);
            grid.Children.Add(mainMenuView2);

            var icon = new BitmapImage(new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/notication.png"));
            icon.Freeze();
            var textblock = new TextBlock()
            {
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            textblock.Text = Resources.HasNewVersion;
            updatableButton = new Button()
            {
                BorderThickness = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                Visibility = Visibility.Collapsed,
                Content = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new Image()
                        {
                            Height = 20,
                            Source = icon
                        },
                        textblock
                    }
                },
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            updatableButton.SetResourceReference(Button.BackgroundProperty, "MenuDefaultBackground");
            updatableButton.Click += (e, ee) =>
            {
                IoC.Get<IWindowManager>().ShowWindowAsync(new ShowNewVersionDialogViewModel()).Wait();
            };

            Grid.SetColumn(updatableButton, 1);
            grid.Children.Add(updatableButton);

            contentPresent.Content = grid;

            isModified = true;
        }

        public DefaultProgramUpdater()
        {
            http = new HttpClient();
        }

        public async Task CheckUpdatable()
        {
            if (!ProgramSetting.Default.EnableUpdateCheck)
            {
                RemoteVersionInfo = null;
                return;
            }

            if ((App.Current as App)?.IsGUIMode ?? false)
                App.Current.Dispatcher.Invoke(ModifyFrameworkMenuView);

            try
            {
                var url = $"{ApiEndPoint}/editor/getVersionInfo?requireMasterBranch={ProgramSetting.Default.UpdaterCheckMasterBranchOnly}";
                RemoteVersionInfo = await http.GetFromJsonAsync<VersionInfo>(url);
            }
            catch (Exception e)
            {
                Log.LogError($"Can't check update because exception:{e.Message}", e);
                RemoteVersionInfo = null;
            }
        }

        public async Task StartUpdate()
        {
            if (RemoteVersionInfo is null)
                throw new Exception("Can't start update because RemoteVersionInfo is empty.");

            var isMaster = "master".Equals(RemoteVersionInfo.Branch, StringComparison.InvariantCultureIgnoreCase);
            var url = $"{ApiEndPoint}/editor/get?requireMasterBranch={isMaster}";

            var zipStream = new MemoryStream();
            {
                Log.LogInfo($"begin download editor zip file: {url}");
                using var ns = await http.GetStreamAsync(url);
                await ns.CopyToAsync(zipStream);
                zipStream.Seek(0, SeekOrigin.Begin);
            }

            var tempZipFolder = TempFileHelper.GetTempFolderPath("updater", $"{RemoteVersionInfo.Branch}_{RemoteVersionInfo.Version}");
            var sourceFolder = TempFileHelper.GetTempFolderPath("updater", $"{RemoteVersionInfo.Branch}_{RemoteVersionInfo.Version}");
            Log.LogInfo($"tempZipFolder = {tempZipFolder}");
            using var zipFile = new ZipArchive(zipStream, ZipArchiveMode.Read);
            zipFile.ExtractToDirectory(tempZipFolder);
            zipFile.ExtractToDirectory(sourceFolder);

            var updaterFilePath = Path.Combine(tempZipFolder, "OngekiFumenEditor.CommandLine.exe");
            if (!File.Exists(updaterFilePath))
                throw new Exception($"Downloaded wrong file, updater file is not found: {updaterFilePath}");

            var targetFolder = Path.GetDirectoryName(typeof(DefaultProgramUpdater).Assembly.Location);
            var args = new string[] { "updater", "-v", "--targetFolder", targetFolder, "--sourceFolder", sourceFolder, "--sourceVersion", ThisAssembly.AssemblyFileVersion };

            Log.LogInfo($"updaterFilePath: {updaterFilePath}");
            Log.LogInfo($"targetFolder: {updaterFilePath}");
            Log.LogInfo($"args: {string.Join(" ", args)}");

            if (MessageBox.Show(Resources.ProgramReadyToUpdate, Resources.Warning, MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                return;

            Log.LogInfo($"user comfirmed.");
            Process.Start(updaterFilePath, args);
            App.Current.Shutdown();
        }

        public (int exitCode, string message) CommandExecuteUpdate(UpdaterOption option)
        {
            var targetFolder = option.TargetFolder;
            var sourceVersion = option.SourceVersion;
            var sourceFolder = option.SourceFolder /*Path.GetDirectoryName(typeof(DefaultProgramUpdater).Assembly.Location)*/;

            var bakSuffix = $".bak_{RandomHepler.RandomString(10)}";
            Log.LogInfo($"sourceFolder: {sourceFolder}");

            //Dic<full,relative>
            var moveFiles = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories)
                .Where(x =>
                {
                    //filter unused files by extension
                    return Path.GetExtension(x).ToLower() switch
                    {
                        ".log" or
                        ".xml" or
                        ".dmp" => false,
                        _ => true
                    };
                })
                .Select(x => Path.GetRelativePath(sourceFolder, x))
                .ToList();

            foreach (var dir in moveFiles.GroupBy(x => Path.GetDirectoryName(x)).Select(x => x.Key))
                Directory.CreateDirectory(Path.Combine(targetFolder, dir));

            void DoRollback()
            {
                Log.LogInfo($"rollback begin");
                foreach (var relativePath in moveFiles)
                {
                    var targetFilePath = Path.Combine(targetFolder, relativePath);
                    var targetBackupFilePath = Path.Combine(targetFolder, relativePath + bakSuffix);

                    try
                    {
                        if (File.Exists(targetBackupFilePath))
                        {
                            File.Move(targetBackupFilePath, targetFilePath);
                            Log.LogInfo($"* rollback file: {targetBackupFilePath} -> {targetFilePath}");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.LogError($"rollback file failed: {targetBackupFilePath} -> {targetFilePath}", e);
                    }
                }
                Log.LogInfo($"rollback end");
            }

            //setup enviorment
            //kill others editor processes
            var curPid = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcessesByName("OngekiFumenEditor").Where(x => curPid != x.Id))
            {
                try
                {
                    process.Kill();
                    Log.LogInfo($"other editor killed, pid: {process.Id}");
                }
                catch (Exception e)
                {
                    return (-1, $"can't kill other editor, pid: {process.Id}");
                }
            }

            //backup files which will be replaced.
            foreach (var relativePath in moveFiles)
            {
                var targetFilePath = Path.Combine(targetFolder, relativePath);
                var targetBackupFilePath = Path.Combine(targetFolder, relativePath + bakSuffix);

                try
                {
                    if (File.Exists(targetFilePath))
                    {
                        File.Move(targetFilePath, targetBackupFilePath);
                        Log.LogInfo($"* backup file: {targetFilePath} -> {targetBackupFilePath}");
                    }
                }
                catch (Exception e)
                {
                    DoRollback();
                    return (-2, $"backup file failed: {targetFilePath} -> {targetBackupFilePath}");
                }
            }

            //move files!
            foreach (var relativePath in moveFiles)
            {
                var sourceFilePath = Path.Combine(sourceFolder, relativePath);
                var targetFilePath = Path.Combine(targetFolder, relativePath);

                try
                {
                    File.Copy(sourceFilePath, targetFilePath);
                    Log.LogInfo($"* move file: {sourceFilePath} -> {targetFilePath}");
                }
                catch (Exception e)
                {
                    DoRollback();
                    return (-3, $"move file failed: {sourceFilePath} -> {targetFilePath}");
                }
            }

            //delete backup files
            foreach (var relativePath in moveFiles)
            {
                var targetBackupFilePath = Path.Combine(targetFolder, relativePath + bakSuffix);

                try
                {
                    File.Delete(targetBackupFilePath);
                    Log.LogInfo($"* delete backup file: {targetBackupFilePath}");
                }
                catch (Exception e)
                {
                    Log.LogError($"delete backup file failed: {targetBackupFilePath}", e);
                }
            }

            //start program and notify user result
            var targetProgram = Path.Combine(targetFolder, "OngekiFumenEditor.exe");
            Process.Start(targetProgram, ["--wait", "--notifySucess", "--sourceVersion", sourceVersion]);

            return (0, string.Empty);
        }

        public void OnSchedulerTerm()
        {

        }

        public async Task OnScheduleCall(CancellationToken cancellationToken)
        {
            await CheckUpdatable();
        }
    }
}
