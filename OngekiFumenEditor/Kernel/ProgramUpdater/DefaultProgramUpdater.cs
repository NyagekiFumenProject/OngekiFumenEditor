using Caliburn.Micro;
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
        private const string ApiEndPoint = "https://fumen.nageki-net.com";
        private const int ParentProcessExitTimeoutMilliseconds = 30_000;

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

        private string preparedUpdaterFilePath;
        private string preparedSourceFolder;

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
                var versionInfo = await http.GetFromJsonAsync<VersionInfo>(url);
                if (versionInfo is not null)
                {
                    // 清单里的 time 表示 UTC（带 Z，或反序列化成 Unspecified），展示与比较前统一换算成本地时间。
                    versionInfo.Time = versionInfo.Time.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(versionInfo.Time, DateTimeKind.Utc).ToLocalTime()
                        : versionInfo.Time.ToLocalTime();
                }
                RemoteVersionInfo = versionInfo;
            }
            catch (Exception e)
            {
                Log.LogError($"Can't check update because exception:{e.Message}", e);
                RemoteVersionInfo = null;
            }
        }

        public async Task PrepareUpdateAsync(IProgress<UpdatePrepareProgress> progress, CancellationToken cancellationToken)
        {
            if (RemoteVersionInfo is null)
                throw new Exception("Can't start update because RemoteVersionInfo is empty.");

            var isMaster = "master".Equals(RemoteVersionInfo.Branch, StringComparison.InvariantCultureIgnoreCase);
            var url = $"{ApiEndPoint}/editor/get?requireMasterBranch={isMaster}";

            var zipFilePath = Path.Combine(TempFileHelper.GetTempFolderPath("updater", "zip"), "editor.zip");
            try
            {
                Log.LogInfo($"begin download editor zip file: {url}");
                using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    var contentLength = response.Content.Headers.ContentLength ?? -1;

                    using var ns = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var fs = File.Create(zipFilePath);

                    var buffer = new byte[81920];
                    long received = 0;
                    var stopwatch = Stopwatch.StartNew();
                    var lastReportAt = TimeSpan.Zero;
                    long lastReportBytes = 0;
                    double speed = 0;

                    void ReportDownload()
                    {
                        var now = stopwatch.Elapsed;
                        var seconds = (now - lastReportAt).TotalSeconds;
                        if (seconds > 0)
                        {
                            var instant = (received - lastReportBytes) / seconds;
                            // 指数平滑，避免速度数字剧烈跳动。
                            speed = speed <= 0 ? instant : speed * 0.6 + instant * 0.4;
                        }
                        lastReportAt = now;
                        lastReportBytes = received;
                        progress?.Report(new UpdatePrepareProgress(UpdatePrepareStep.Downloading, contentLength > 0 ? (double)received / contentLength : -1, speed));
                    }

                    while (true)
                    {
                        var read = await ns.ReadAsync(buffer, cancellationToken);
                        if (read <= 0)
                            break;

                        await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        received += read;

                        // 每 250ms 上报一次：既能平滑显示速度，也不会把 UI 线程刷爆。
                        if ((stopwatch.Elapsed - lastReportAt).TotalMilliseconds >= 250)
                            ReportDownload();
                    }

                    ReportDownload();
                }

                var tempZipFolder = TempFileHelper.GetTempFolderPath("updater", $"{RemoteVersionInfo.Branch}_{RemoteVersionInfo.Version}");
                var sourceFolder = TempFileHelper.GetTempFolderPath("updater", $"{RemoteVersionInfo.Branch}_{RemoteVersionInfo.Version}");
                Log.LogInfo($"tempZipFolder = {tempZipFolder}");

                using (var zipFile = new ZipArchive(File.OpenRead(zipFilePath), ZipArchiveMode.Read))
                {
                    var entries = zipFile.Entries;
                    var runFolder = Path.GetFullPath(tempZipFolder);
                    var payloadFolder = Path.GetFullPath(sourceFolder);
                    var stepTotal = entries.Count * 2;
                    var stepDone = 0;

                    void ExtractAll(string targetFolder)
                    {
                        Directory.CreateDirectory(targetFolder);
                        foreach (var entry in entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var destinationPath = Path.GetFullPath(Path.Combine(targetFolder, entry.FullName));
                            if (!destinationPath.StartsWith(targetFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                throw new IOException($"Zip entry escapes the target directory: {entry.FullName}");

                            if (string.IsNullOrEmpty(entry.Name))
                                Directory.CreateDirectory(destinationPath);
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                                entry.ExtractToFile(destinationPath, overwrite: true);
                            }

                            progress?.Report(new UpdatePrepareProgress(UpdatePrepareStep.Extracting, (double)++stepDone / stepTotal));
                        }
                    }

                    // 解压两份：一份供更新器进程自身运行，一份作为待复制到安装目录的负载，避免更新器覆盖自己正在运行的文件。
                    await Task.Run(() =>
                    {
                        ExtractAll(runFolder);
                        ExtractAll(payloadFolder);
                    }, cancellationToken);
                }

                var updaterFilePath = Path.Combine(tempZipFolder, "OngekiFumenEditor.CommandLine.exe");
                if (!File.Exists(updaterFilePath))
                    throw new Exception($"Downloaded wrong file, updater file is not found: {updaterFilePath}");

                preparedUpdaterFilePath = updaterFilePath;
                preparedSourceFolder = sourceFolder;
                Log.LogInfo($"update package is ready: updater={updaterFilePath}, source={sourceFolder}");
            }
            finally
            {
                try
                {
                    File.Delete(zipFilePath);
                }
                catch (Exception e)
                {
                    Log.LogWarn($"Failed to delete downloaded zip file: {zipFilePath}, {e.Message}");
                }
            }
        }

        public void LaunchPreparedUpdate()
        {
            if (preparedUpdaterFilePath is null || preparedSourceFolder is null)
                throw new Exception("Can't launch update because the update package has not been prepared.");

            var targetFolder = AppDirectoryHelper.ExecutableDirectory;
            var args = new string[]
            {
                "updater",
                "-v",
                "--targetFolder", targetFolder,
                "--sourceFolder", preparedSourceFolder,
                "--sourceVersion", ThisAssembly.AssemblyFileVersion,
                "--parentProcessId", Process.GetCurrentProcess().Id.ToString()
            };

            Log.LogInfo($"updaterFilePath: {preparedUpdaterFilePath}");
            Log.LogInfo($"targetFolder: {targetFolder}");
            Log.LogInfo($"args: {string.Join(" ", args)}");
            Log.LogInfo($"user comfirmed.");

            Process.Start(preparedUpdaterFilePath, args);
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
            if (option.ParentProcessId > 0 && option.ParentProcessId != Process.GetCurrentProcess().Id)
            {
                try
                {
                    using var parentProcess = Process.GetProcessById(option.ParentProcessId);
                    Log.LogInfo($"waiting for parent editor process to exit, pid: {option.ParentProcessId}");
                    if (!parentProcess.WaitForExit(ParentProcessExitTimeoutMilliseconds))
                    {
                        Log.LogWarn($"parent editor process did not exit in time, force killing it, pid: {option.ParentProcessId}");
                        parentProcess.Kill();
                        parentProcess.WaitForExit();
                    }
                }
                catch (ArgumentException)
                {
                    Log.LogInfo($"parent editor process already exited, pid: {option.ParentProcessId}");
                }
            }

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
                    Log.LogError($"can't kill other editor, pid: {process.Id}", e);
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
                    Log.LogError($"backup file failed: {targetFilePath} -> {targetBackupFilePath}", e);
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
                    Log.LogError($"move file failed: {sourceFilePath} -> {targetFilePath}", e);
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
            var startInfo = new ProcessStartInfo(targetProgram)
            {
                WorkingDirectory = targetFolder,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--wait");
            startInfo.ArgumentList.Add("--notifySucess");
            startInfo.ArgumentList.Add("--sourceVersion");
            startInfo.ArgumentList.Add(sourceVersion);
            Process.Start(startInfo);

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
