using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections.Base.RangeTree;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.DefaultCommonImpl.Sound;

[RegisterSingleton<IFumenSoundPlayer>]
public class DefaultFumenSoundPlayer : IFumenSoundPlayer, IDisposable
{
    private record MeterAction(TimeSpan Time, TimeSpan BeatInterval, int BeatCount, bool IsSkip);

    private readonly IntervalTree<TimeSpan, DurationSoundEvent> durationEvents = new();
    private readonly HashSet<DurationSoundEvent> currentPlayingDurationEvents = [];
    private readonly object locker = new();

    private LinkedList<SoundEvent> events = [];
    private LinkedListNode<SoundEvent> itor;

    private LinkedList<MeterAction> meterActions = [];
    private LinkedListNode<MeterAction> meterActionsItor;
    private int currentMeterHitCount;

    private CancellationTokenSource updateCts;
    private Task updateTask;

    private IAudioPlayer player;
    private FumenVisualEditorViewModel editor;
    private bool isPlaying;
    public bool IsPlaying => isPlaying && (player?.IsPlaying ?? false);
    private static int loopIdGen;

    public string SoundFolderPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Sound");
    public bool EnableLoopPlayTiming { get; set; } = true;

    public SoundControl SoundControl { get; set; } = SoundControl.All;

    private readonly Dictionary<SoundControl, ISoundPlayer> cacheSounds = [];
    private Task<bool> loadTask;

    public DefaultFumenSoundPlayer()
    {
        InitSounds();
    }

    private async void InitSounds()
    {
        var source = new TaskCompletionSource<bool>();
        loadTask = source.Task;
        var audioManager = IoC.Get<IAudioManager>();

        var soundFolderPath = SoundFolderPath;
        if (!Directory.Exists(soundFolderPath))
        {
            Log.LogError($"Sound folder not found: {soundFolderPath}");
            source.SetResult(false);
            return;
        }

        bool noError = true;

        async Task Load(SoundControl sound, string fileName)
        {
            var fixFilePath = Path.Combine(soundFolderPath, fileName);
            try
            {
                using var file = new LocalSimpleFile(fixFilePath);
                cacheSounds[sound] = await audioManager.LoadSoundAsync(file);
            }
            catch (Exception e)
            {
                Log.LogError($"Can't load {sound} sound file : {fixFilePath} , reason : {e.Message}");
                noError = false;
            }
        }

        cacheSounds.Clear();
        await Load(SoundControl.Tap, "tap.wav");
        await Load(SoundControl.Bell, "bell.wav");
        await Load(SoundControl.CriticalTap, "extap.wav");
        await Load(SoundControl.WallTap, "wall.wav");
        await Load(SoundControl.CriticalWallTap, "exwall.wav");
        await Load(SoundControl.Flick, "flick.wav");
        await Load(SoundControl.Bullet, "bullet.wav");
        await Load(SoundControl.CriticalFlick, "exflick.wav");
        await Load(SoundControl.HoldEnd, "holdend.wav");
        await Load(SoundControl.ClickSE, "clickse.wav");
        await Load(SoundControl.HoldTick, "holdtick.wav");
        await Load(SoundControl.BeamPrepare, "beamprepare.wav");
        await Load(SoundControl.BeamLoop, "beamlooping.wav");
        await Load(SoundControl.BeamEnd, "beamend.wav");
        await Load(SoundControl.MetronomeStrongBeat, "metronomeStrongBeat.wav");
        await Load(SoundControl.MetronomeWeakBeat, "metronomeWeakBeat.wav");
        await Load(SoundControl.BossWave, "bossWave.wav");

        if (!noError)
            Log.LogWarning("Some sounds failed to load.");

        source.SetResult(noError);
    }

    public async Task Prepare(FumenVisualEditorViewModel editor, IAudioPlayer player)
    {
        await loadTask;

        await StopUpdateLoopAsync();

        this.player = player;
        this.editor = editor;

        RebuildEvents();

        updateCts = new CancellationTokenSource();
        updateTask = Task.Run(() => OnUpdate(updateCts.Token), updateCts.Token);
    }

    private static IEnumerable<TGrid> CalculateHoldTicks(Hold x, OngekiFumen fumen)
    {
        int? CalcHoldTickStepSize()
        {
            var met = fumen.MeterChanges.GetMeter(x.TGrid);
            var bpm = fumen.BpmList.GetBpm(x.TGrid);
            var resT = bpm.TGrid.ResT;
            var beatCount = met.Bunbo;
            if (beatCount == 0)
                return null;
            return (int)(resT / beatCount);
        }

        if (CalcHoldTickStepSize() is not int lengthPerBeat)
            yield break;

        var stepGrid = new GridOffset(0, lengthPerBeat);
        var curTGrid = x.TGrid + stepGrid;
        if (x.HoldEnd is null)
            yield break;
        while (curTGrid < x.HoldEnd.TGrid)
        {
            yield return curTGrid;
            curTGrid += stepGrid;
        }
    }

    private static IEnumerable<TGrid> CalculateDefaultClickSEs(OngekiFumen fumen)
    {
        var tGrid = TGrid.Zero;
        var endTGrid = new TGrid(1, 0);
        var met = fumen.MeterChanges.GetMeter(tGrid);
        var bpm = fumen.BpmList.GetBpm(tGrid);
        var resT = bpm.TGrid.ResT;
        var beatCount = met.Bunbo;
        if (beatCount == 0)
            yield break;

        var lengthPerBeat = (int)(resT / beatCount);
        var stepGrid = new GridOffset(0, lengthPerBeat);
        var curTGrid = tGrid + stepGrid;
        while (curTGrid < endTGrid)
        {
            yield return curTGrid;
            curTGrid += stepGrid;
        }
    }

    private void RebuildEvents()
    {
        StopAllLoop();
        events.Clear();
        durationEvents.Clear();
        currentPlayingDurationEvents.Clear();

        var list = new HashSet<SoundEvent>();
        var durationList = new HashSet<DurationSoundEvent>();

        void AddSound(SoundControl sound, TGrid tGrid)
        {
            list.Add(new SoundEvent
            {
                Sounds = sound,
                Time = TGridCalculator.ConvertTGridToAudioTime(tGrid, editor),
            });
        }

        void AddDurationSound(SoundControl sound, TGrid tGrid, TGrid endTGrid, int loopId = 0)
        {
            durationList.Add(new DurationSoundEvent
            {
                Sounds = sound,
                LoopId = loopId,
                Time = TGridCalculator.ConvertTGridToAudioTime(tGrid, editor),
                EndTime = TGridCalculator.ConvertTGridToAudioTime(endTGrid, editor),
            });
        }

        var fumen = editor.Fumen;
        var soundObjects = fumen.GetAllDisplayableObjects().OfType<OngekiTimelineObjectBase>();

        if (!fumen.ClickSEs.Any(x => x.TGrid.TotalUnit <= 1))
        {
            foreach (var tGrid in CalculateDefaultClickSEs(fumen))
                AddSound(SoundControl.ClickSE, tGrid);
        }

        var typeSet = new HashSet<Type>();
        foreach (var group in soundObjects.GroupBy(x => x.TGrid))
        {
            var sounds = (SoundControl)0;
            typeSet.Clear();

            foreach (var obj in group.Where(x => x is Tap || typeSet.Add(x.GetType())))
            {
                sounds |= obj switch
                {
                    Tap { ReferenceLaneStart: { IsWallLane: true }, IsCritical: false } or Hold
                        { ReferenceLaneStart: { IsWallLane: true }, IsCritical: false } => SoundControl.WallTap,
                    Tap { ReferenceLaneStart: { IsWallLane: true }, IsCritical: true } or Hold
                        { ReferenceLaneStart: { IsWallLane: true }, IsCritical: true } => SoundControl.CriticalWallTap,
                    Tap { ReferenceLaneStart: { IsWallLane: false }, IsCritical: false } or Hold
                        { ReferenceLaneStart: { IsWallLane: false }, IsCritical: false } => SoundControl.Tap,
                    Tap { ReferenceLaneStart: { IsWallLane: false }, IsCritical: true } or Hold
                        { ReferenceLaneStart: { IsWallLane: false }, IsCritical: true } => SoundControl.CriticalTap,
                    Tap { ReferenceLaneStart: null, IsCritical: false } or Hold { ReferenceLaneStart: null, IsCritical: false } => SoundControl.Tap,
                    Tap { ReferenceLaneStart: null, IsCritical: true } or Hold { ReferenceLaneStart: null, IsCritical: true } => SoundControl.CriticalTap,
                    Bell => SoundControl.Bell,
                    Bullet => SoundControl.Bullet,
                    Flick { IsCritical: false } => SoundControl.Flick,
                    Flick { IsCritical: true } => SoundControl.CriticalFlick,
                    HoldEnd => SoundControl.HoldEnd,
                    ClickSE => SoundControl.ClickSE,
                    EnemySet { TagTblValue: EnemySet.WaveChangeConst.Boss } => SoundControl.BossWave,
                    _ => default
                };

                if (obj is Hold hold)
                {
                    foreach (var tickTGrid in CalculateHoldTicks(hold, fumen))
                        AddSound(SoundControl.HoldTick, tickTGrid);
                }

                if (obj is BeamStart beam)
                {
                    var loopId = ++loopIdGen;
                    AddSound(SoundControl.BeamEnd, beam.MaxTGrid);
                    AddDurationSound(SoundControl.BeamLoop, beam.TGrid, beam.MaxTGrid, loopId);

                    var leadInTGrid = TGridCalculator.ConvertAudioTimeToTGrid(
                        TGridCalculator.ConvertTGridToAudioTime(beam.TGrid, editor) -
                        TGridCalculator.ConvertFrameToAudioTime(BeamStart.LEAD_IN_DURATION_FRAME), editor);
                    if (leadInTGrid is null)
                        leadInTGrid = TGrid.Zero;
                    AddSound(SoundControl.BeamPrepare, leadInTGrid);
                }
            }

            if (sounds != 0)
                AddSound(sounds, group.Key);
        }

        events = new LinkedList<SoundEvent>(list.OrderBy(x => x.Time));
        foreach (var durationEvent in durationList)
            durationEvents.Add(durationEvent.Time, durationEvent.EndTime, durationEvent);
        itor = default;

        meterActions.Clear();
        if (EnableLoopPlayTiming)
        {
            var oneTGrid = new TGrid(1, 0);
            var timeSignatureList = fumen.MeterChanges.GetCachedAllTimeSignatureUniformPositionList(fumen.BpmList);
            foreach (var timeSignature in timeSignatureList)
            {
                var beatCount = timeSignature.meter.Bunbo;
                var isSkip = beatCount == 0;
                var beatInterval = isSkip
                    ? default
                    : TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(TGrid.Zero, oneTGrid, timeSignature.bpm.BPM)) / beatCount;

                meterActions.AddLast(new MeterAction(timeSignature.audioTime, beatInterval, beatCount, isSkip));
            }
        }

        meterActionsItor = default;
        currentMeterHitCount = 0;
    }

    private void UpdateInternal(CancellationToken token)
    {
        if ((itor is null && meterActionsItor is null) || player is null || token.IsCancellationRequested)
            return;

        if (!IsPlaying)
        {
            StopAllLoop();
            return;
        }

        var currentTime = player.CurrentTime;

        while (itor is not null)
        {
            var ct = currentTime.TotalMilliseconds - itor.Value.Time.TotalMilliseconds;
            if (ct >= 0)
            {
                PlaySoundsOnce(itor.Value.Sounds);
                itor = itor.Next;
            }
            else
                break;
        }

        while (meterActionsItor is not null)
        {
            var nextActionItor = meterActionsItor.Next;
            if (meterActionsItor.Value.IsSkip)
            {
                meterActionsItor = nextActionItor;
                currentMeterHitCount = 0;
                continue;
            }

            var nextBeatTime = meterActionsItor.Value.Time + meterActionsItor.Value.BeatInterval * currentMeterHitCount;
            if (nextActionItor != null && nextBeatTime > nextActionItor.Value.Time)
            {
                meterActionsItor = nextActionItor;
                currentMeterHitCount = 0;
                continue;
            }

            var ct = currentTime.TotalMilliseconds - nextBeatTime.TotalMilliseconds;
            if (ct >= 0)
            {
                var beatIdx = currentMeterHitCount % meterActionsItor.Value.BeatCount;
                var sound = beatIdx == 0 ? SoundControl.MetronomeStrongBeat : SoundControl.MetronomeWeakBeat;
                PlaySoundsOnce(sound);
                currentMeterHitCount++;
            }
            else
                break;
        }

        lock (locker)
        {
            var queryDurationEvents = durationEvents.Query(currentTime);
            foreach (var durationEvent in queryDurationEvents)
            {
                if (!currentPlayingDurationEvents.Contains(durationEvent) &&
                    SoundControl.HasFlag(durationEvent.Sounds) &&
                    cacheSounds.TryGetValue(durationEvent.Sounds, out var soundPlayer))
                {
                    var initPlayTime = currentTime - durationEvent.Time;
                    soundPlayer.PlayLoop(durationEvent.LoopId, initPlayTime);
                    currentPlayingDurationEvents.Add(durationEvent);
                }
            }

            foreach (var durationEvent in currentPlayingDurationEvents.Where(x => currentTime < x.Time || currentTime > x.EndTime).ToArray())
            {
                if (cacheSounds.TryGetValue(durationEvent.Sounds, out var soundPlayer))
                {
                    soundPlayer.StopLoop(durationEvent.LoopId);
                    currentPlayingDurationEvents.Remove(durationEvent);
                }
            }
        }
    }

    private async Task OnUpdate(CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            UpdateInternal(cancel);
            try
            {
                await Task.Delay(1, cancel);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private void PlaySoundsOnce(SoundControl sounds)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CheckPlay(SoundControl subFlag)
        {
            if (sounds.HasFlag(subFlag) && SoundControl.HasFlag(subFlag) && cacheSounds.TryGetValue(subFlag, out var sound))
                sound.PlayOnce();
        }

        CheckPlay(SoundControl.Tap);
        CheckPlay(SoundControl.CriticalTap);
        CheckPlay(SoundControl.Bell);
        CheckPlay(SoundControl.WallTap);
        CheckPlay(SoundControl.CriticalWallTap);
        CheckPlay(SoundControl.Bullet);
        CheckPlay(SoundControl.Flick);
        CheckPlay(SoundControl.CriticalFlick);
        CheckPlay(SoundControl.HoldEnd);
        CheckPlay(SoundControl.HoldTick);
        CheckPlay(SoundControl.ClickSE);
        CheckPlay(SoundControl.BeamPrepare);
        CheckPlay(SoundControl.BeamEnd);
        CheckPlay(SoundControl.MetronomeStrongBeat);
        CheckPlay(SoundControl.MetronomeWeakBeat);
        CheckPlay(SoundControl.BossWave);
    }

    public void Seek(TimeSpan msec, bool pause)
    {
        Pause();
        itor = events.Find(events.FirstOrDefault(x => msec < x.Time));
        meterActionsItor = meterActions.Find(meterActions.LastOrDefault(x => msec >= x.Time));
        if (meterActionsItor is null)
            currentMeterHitCount = 0;
        else if (meterActionsItor.Value.IsSkip)
            currentMeterHitCount = 0;
        else
            currentMeterHitCount = (int)((msec - meterActionsItor.Value.Time) / meterActionsItor.Value.BeatInterval);

        if (!pause)
            PlayInternal();
    }

    private void StopAllLoop()
    {
        lock (locker)
        {
            foreach (var durationEvent in currentPlayingDurationEvents.ToArray())
            {
                if (cacheSounds.TryGetValue(durationEvent.Sounds, out var soundPlayer))
                {
                    soundPlayer.StopLoop(durationEvent.LoopId);
                    currentPlayingDurationEvents.Remove(durationEvent);
                }
            }
        }
    }

    public void Stop()
    {
        StopAllLoop();
        isPlaying = false;
    }

    private void PlayInternal()
    {
        if (player is null)
            return;
        isPlaying = true;
    }

    public void Play()
    {
        if (player is null)
            return;

        itor ??= events.First;
        meterActionsItor ??= meterActions.First;
        currentMeterHitCount = 0;
        PlayInternal();
    }

    public void Pause()
    {
        isPlaying = false;
        StopAllLoop();
    }

    public void Dispose()
    {
        _ = StopUpdateLoopAsync();
        foreach (var sound in cacheSounds.Values)
            sound.Dispose();
    }

    public async Task Clean()
    {
        Stop();
        await StopUpdateLoopAsync();
        player = default;
        editor = default;
        events.Clear();
    }

    private async Task StopUpdateLoopAsync()
    {
        if (updateCts is null)
            return;

        updateCts.Cancel();
        if (updateTask is not null)
        {
            try
            {
                await updateTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        updateTask = default;
        updateCts.Dispose();
        updateCts = default;
    }

    public float? GetVolume(SoundControl sound)
    {
        return cacheSounds.TryGetValue(sound, out var player) ? player.Volume : null;
    }

    public void SetVolume(SoundControl sound, float volume)
    {
        if (cacheSounds.TryGetValue(sound, out var player))
            player.Volume = volume;
    }

    public async Task<bool> ReloadSoundFiles()
    {
        InitSounds();
        return await loadTask;
    }
}

