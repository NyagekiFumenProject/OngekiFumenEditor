using AkiraMindController.Communication;
using AkiraMindController.Communication.Services.Assets;
using AkiraMindController.Communication.Services.GameStatus;
using AkiraMindController.Communication.Services.MusicSelect;
using AkiraMindController.Communication.Services.Playing;
using AkiraMindController.Communication.Services.Result;
using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using kRpc.Client;
using kRpc.Coroutines;
using kRpc.Utils;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Base;
using OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Views;
using OngekiFumenEditor.Core.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Attributes;
using OngekiFumenEditor.Utils.Ogkr;
using OpenTK.Compute.OpenCL;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Log = OngekiFumenEditor.Utils.Log;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.ViewModels
{
    [Export(typeof(IOngekiGamePlayControllerViewer))]
    [MapToView(ViewType = typeof(OngekiGamePlayControllerViewerView))]
    public class OngekiGamePlayControllerViewerViewModel : Tool, IOngekiGamePlayControllerViewer
    {
        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        public bool IsConnected => ConnectStatus == ConnectStatus.Connected;

        private ConnectStatus connectStatus = ConnectStatus.NotConnect;
        public ConnectStatus ConnectStatus
        {
            get => connectStatus;
            set
            {
                Set(ref connectStatus, value);
                NotifyOfPropertyChange(() => IsConnected);
            }
        }

        private string ogkrSavePath;
        public string OgkrSavePath
        {
            get => ogkrSavePath;
            set => Set(ref ogkrSavePath, value);
        }

        private bool isPlayGuideSEAfterPlay;
        public bool IsPlayGuideSEAfterPlay
        {
            get => isPlayGuideSEAfterPlay;
            set => Set(ref isPlayGuideSEAfterPlay, value);
        }

        private bool isAutoPlay;
        public bool IsAutoPlay
        {
            get => isAutoPlay;
            set
            {
                Set(ref isAutoPlay, value);
                MakeSureOptionsApplied();
            }
        }

        private bool isPauseIfMissBellOrDamaged;
        public bool IsPauseIfMissBellOrDamaged
        {
            get => isPauseIfMissBellOrDamaged;
            set
            {
                Set(ref isPauseIfMissBellOrDamaged, value);
                MakeSureOptionsApplied();
            }
        }


        private string curAutoFaderTargetDataStr;
        public string CurAutoFaderTargetDataStr
        {
            get => curAutoFaderTargetDataStr;
            set
            {
                Set(ref curAutoFaderTargetDataStr, value);
            }
        }

        private string preAutoFaderTargetDataStr;
        public string PreAutoFaderTargetDataStr
        {
            get => preAutoFaderTargetDataStr;
            set
            {
                Set(ref preAutoFaderTargetDataStr, value);
            }
        }

        private float seekTimeMsec;
        public float SeekTimeMsec
        {
            get => seekTimeMsec;
            set => Set(ref seekTimeMsec, value);
        }

        private float calcCurFrame;
        public float CalcCurFrame
        {
            get => calcCurFrame;
            set => Set(ref calcCurFrame, value);
        }

        private bool isPlayAfterSeek;
        public bool IsPlayAfterSeek
        {
            get => isPlayAfterSeek;
            set => Set(ref isPlayAfterSeek, value);
        }

        private string cueId;
        public string CueId
        {
            get => cueId;
            set => Set(ref cueId, value);
        }

        private bool isReloadAfterSeek;
        public bool IsReloadAfterSeek
        {
            get => isReloadAfterSeek;
            set => Set(ref isReloadAfterSeek, value);
        }

        private string connectString = "ws://127.0.0.1:30001/nyageki";
        private WebsocketRpcClient client;
        private Dictionary<Type, IRpcService> cacheRpcServices = new();

        public string ConnectString
        {
            get => connectString;
            set => Set(ref connectString, value);
        }

        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            DisplayName = "AkariMindController";
        }

        static OngekiGamePlayControllerViewerViewModel()
        {
            CoroutineMgr.Instance.StartThread();
            kRpc.Utils.Log.Implement = OnKRpcLog;
        }

        private static void OnKRpcLog(string obj)
        {
            Log.LogDebug(obj);
        }

        public void Disconnect()
        {
            client?.Dispose();
            Clear();
            ConnectStatus = ConnectStatus.Disconnected;
        }

        public async void Connect()
        {
            client = new WebsocketRpcClient("ws://127.0.0.1:30001/nyageki", rpcClient =>
            {
                //rpcClient.RegisterServiceType<MyGameStatusCallback, IGameStatusCallback>();
                //rpcClient.RegisterServiceType<MyMusicSelectCallback, IMusicSelectCallback>();
                //rpcClient.RegisterServiceType<MyPlayingCallback, IPlayingCallback>();
                //rpcClient.RegisterServiceType<MyResultCallback, IResultCallback>();
            });
            client.OnDisconnected += OnClientDisconnected;

            client.Start(requireHeartbeat: true);
        }

        private void OnClientDisconnected()
        {
            Disconnect();
        }

        private void Clear()
        {
            cacheRpcServices.Clear();
            client = null;
        }

        private async Task<T> RequestRemoteService<T>() where T : class, IRpcService
        {
            if (cacheRpcServices.TryGetValue(typeof(T), out IRpcService service))
            {
                Log.LogDebug($"return cached rpc service for {typeof(T).Name}");
                return (T)service;
            }

            Log.LogDebug($"starting request rpc service for {typeof(T).Name}");
            service = await client.GetRemoteServiceType<T>().StartValueTask();
            cacheRpcServices[typeof(T)] = service;
            Log.LogDebug($"saved rpc service for {typeof(T).Name}");
            return (T)service;
        }

        public void OpenOgkrSavePathDialog()
        {
            OgkrSavePath = FileDialogHelper.SaveFile(
                Resources.GamePlayControllerSelectOgkrSavePathPrompt,
                new[] { (".ogkr", Resources.GamePlayControllerOgkrFileType) }) ?? OgkrSavePath;
        }

        public async void GetOgkrSavePathFromGamePlay()
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return;

            var musicSelect = await RequestRemoteService<IMusicSelectRpcService>();
            var assets = await RequestRemoteService<IAssetsRpcService>();

            var fumenData = await musicSelect.GetCurrentFumenDataSelected().StartValueTask();
            var musicData = await musicSelect.GetCurrentMusicDataSelected().StartValueTask();


            OgkrSavePath = await assets.GetFumenFilePath(musicData.MusicId, fumenData.Difficulty).StartValueTask();
            MessageBox.Show(
                Resources.GamePlayControllerGetFumenPathSuccess,
                Resources.GamePlayControllerWindowTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public async Task Play()
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return;

            var play = await RequestRemoteService<IPlayingRpcService>();
            await play.SetPlayStatus(PlayStatus.Playing).StartValueTask();
        }

        public async Task Pause()
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return;

            var play = await RequestRemoteService<IPlayingRpcService>();
            await play.SetPlayStatus(PlayStatus.Paused).StartValueTask();
        }

        public async Task Restart()
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return;

            var play = await RequestRemoteService<IPlayingRpcService>();
            await play.ForceRestart().StartValueTask();
        }

        public async Task SeekTo(TimeSpan time)
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return;

            Log.LogError($"akari seek to {time} , playAfterSeek : {IsPlayAfterSeek}");
            if (IsReloadAfterSeek)
                await Reload();

            var play = await RequestRemoteService<IPlayingRpcService>();
            await play.SeekTo((int)time.TotalMilliseconds, IsPlayAfterSeek).StartValueTask();
        }

        public Task SeekTo() => SeekTo(TimeSpan.FromMilliseconds(SeekTimeMsec));

        public async void RefreshUI()
        {
            //var data = await GetNotesManagerData();

            //isAutoPlay = (data)?.IsAutoPlay ?? false;
            //isPauseIfMissBellOrDamaged = (data)?.IsPauseIfMissBellOrDamaged ?? false;

            //NotifyOfPropertyChange(() => IsAutoPlay);
            //NotifyOfPropertyChange(() => IsPauseIfMissBellOrDamaged);
        }

        public async Task Reload()
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return;
            if (!File.Exists(OgkrSavePath))
                return;
            //if ((await GetNotesManagerData()) is not NotesManagerData data)
            //    return;

            var play = await RequestRemoteService<IPlayingRpcService>();
            await play.SetPlayStatus(PlayStatus.Paused).StartValueTask();

            var currentPlaytime = await play.GetCurrentPlayingTime().StartValueTask();
            await GenerateOgkr(OgkrSavePath);

            await play.SeekTo(currentPlaytime, IsPlayAfterSeek).StartValueTask();
        }

        private async Task MakeSureOptionsApplied()
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return;

            //await SendMessageAsync(new AutoPlay() { isEnable = IsAutoPlay });
            //await SendMessageAsync(new SetNoteManagerValue() { name = "isPauseIfMissBellOrDamaged", value = IsPauseIfMissBellOrDamaged.ToString() });
        }

        private async Task GenerateOgkr(string ogkrSavePath)
        {
            if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
                return;

            var result = await StandardizeFormat.Process(editor.Fumen);

            if (result?.SerializedFumen is OngekiFumen serializedFumen)
            {
                var data = await IoC.Get<IFumenParserManager>().GetSerializer(ogkrSavePath).SerializeAsync(serializedFumen);
                await File.WriteAllBytesAsync(ogkrSavePath, data);

                var play = await RequestRemoteService<IPlayingRpcService>();
                await play.HotReloadFumenFile(ogkrSavePath).StartValueTask();

                Log.LogInfo($"AkariMindController generate fumen to {ogkrSavePath}");
            }
            else
            {
                var errorMsg = result.Message;
                Log.LogError($"AkariMindController can't generate fumen : {errorMsg}");
                MessageBox.Show(
                    string.Format(Resources.GamePlayControllerGenerateOgkrFailedFormat, errorMsg),
                    Resources.GamePlayControllerWindowTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public async Task<bool> UpdateCheckConnecting()
        {
            try
            {
                var gameStatusRpcService = await RequestRemoteService<IGameStatusRpcService>();
                await gameStatusRpcService.GetCurrentGameStatus().StartValueTask();

            }
            catch (Exception e)
            {
                ConnectStatus = ConnectStatus.Disconnected;
                return false;
            }
            ConnectStatus = ConnectStatus.Connected;
            return false;
        }

        public Task<bool> CheckVailed()
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return Task.FromResult(false);
            //todo
            return Task.FromResult(true);
        }

        public async void ForceEnd()
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return;

            var play = await RequestRemoteService<IPlayingRpcService>();
            await play.ForceEndPlay().StartValueTask();
        }

        public async void DumpUnfinishInfo()
        {
            if (ConnectStatus != ConnectStatus.Connected)
                return;

            //await SendMessageAsync<DumpUnfinishInfo>();
        }

        public async Task<bool> IsPlaying()
        {
            var play = await RequestRemoteService<IPlayingRpcService>();
            return PlayStatus.Playing == await play.GetPlayStatus().StartValueTask();
        }

        //public async Task<NotesManagerData?> GetNotesManagerData()
        //{
        //    if (ConnectStatus != ConnectStatus.Connected)
        //        return null;

        //    if ((await SendMessageAsync<GetNoteManagerValue, GetNoteManagerValue.ReturnValue>()) is not GetNoteManagerValue.ReturnValue retVal)
        //        return null;

        //    var msecPerFrame = 1000 / 60.0;

        //    return new NotesManagerData()
        //    {
        //        CurrentTime = TimeSpan.FromMilliseconds(retVal.currentFrame * msecPerFrame),
        //        InvisibleTime = TimeSpan.FromMilliseconds(retVal.invisibleFrame * msecPerFrame),
        //        VisibleTime = TimeSpan.FromMilliseconds(retVal.visibleFrame * msecPerFrame),
        //        NoteEndTime = TimeSpan.FromMilliseconds(retVal.noteEndFrame * msecPerFrame),
        //        NoteStartTime = TimeSpan.FromMilliseconds(retVal.noteStartFrame * msecPerFrame),
        //        PlayEndTime = TimeSpan.FromMilliseconds(retVal.playEndFrame * msecPerFrame),
        //        PlayStartTime = TimeSpan.FromMilliseconds(retVal.playStartFrame * msecPerFrame),
        //        PlayProgress = retVal.playProgress,
        //        OgkrFilePath = retVal.ogkrFilePath,
        //        IsPlayEnd = retVal.isPlayEnd,
        //        IsPlaying = retVal.isPlaying,
        //        IsAutoPlay = retVal.autoPlay,
        //        IsPauseIfMissBellOrDamaged = retVal.isPauseIfMissBellOrDamaged
        //    };
        //}

        //public async void GetAutoFaderData()
        //{
        //    if (ConnectStatus != ConnectStatus.Connected)
        //        return;

        //    if ((await SendMessageAsync<GetNoteManagerAutoPlayData, GetNoteManagerAutoPlayData.ReturnValue>()) is GetNoteManagerAutoPlayData.ReturnValue data)
        //    {
        //        CurAutoFaderTargetDataStr = data.curFaderTargetStr;
        //        PreAutoFaderTargetDataStr = data.prevFaderTargetStr;
        //    }
        //}

        //public async void ApplyAutoFaderData()
        //{
        //    if (ConnectStatus != ConnectStatus.Connected)
        //        return;

        //    await SendMessageAsync(new SetNoteManagerValue() { name = "curFaderTarget", value = CurAutoFaderTargetDataStr?.Replace("\n", "") });
        //    await SendMessageAsync(new SetNoteManagerValue() { name = "prevFaderTarget", value = PreAutoFaderTargetDataStr?.Replace("\n", "") });
        //}

        //public async void PlayCustomSound()
        //{
        //    if (ConnectStatus != ConnectStatus.Connected)
        //        return;

        //    await SendMessageAsync(new PlayCustomCommonSound { cueIdList = CueId.Split("+").Select(int.Parse).ToArray() });
        //}

        //public async void DumpAutoFaderTarget()
        //{
        //    if (ConnectStatus != ConnectStatus.Connected)
        //        return;

        //    if ((await SendMessageAsync<DumpNoteManagerAutoPlayData, DumpNoteManagerAutoPlayData.ReturnValue>()) is DumpNoteManagerAutoPlayData.ReturnValue data)
        //    {
        //        var filePath = data.dumpFilePath;
        //        if (File.Exists(filePath))
        //        {
        //            if (MessageBox.Show("转储成功,是否打开文件夹?", "DumpAutoFaderTarget", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        //            {
        //                var dir = Path.GetDirectoryName(filePath);
        //                ProcessUtils.OpenPath(dir);
        //            }
        //        }
        //        else
        //        {
        //            MessageBox.Show("转储失败,请自行查看游戏日志", "DumpAutoFaderTarget");
        //        }
        //    }
        //}

        //public async void ManualCallCalcAutoPlayFader()
        //{
        //    if (ConnectStatus != ConnectStatus.Connected)
        //        return;

        //    if (await SendMessageAsync<CalculateNextAutoPlayData, CalculateNextAutoPlayData.ReturnValue>(new CalculateNextAutoPlayData() { frame = CalcCurFrame }) is CalculateNextAutoPlayData.ReturnValue data)
        //    {
        //        CurAutoFaderTargetDataStr = data.curFaderTargetStr;
        //    }
        //}
    }
}
