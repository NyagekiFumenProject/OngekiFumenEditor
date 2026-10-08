using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
    public partial interface IAudioManager : IDisposable
    {
        float SoundVolume { get; set; }
        float MusicVolume { get; set; }
        float MusicSpeed { get; set; }

        Task<ISoundPlayer> LoadSoundAsync(string filePath);
        Task<IAudioPlayer> LoadAudioAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 只取音频时长：优先读文件头/帧索引（wav 的 RIFF 头、mp3 的帧索引、acb 转换后的缓存 wav 头），
        /// 不再为拿一个时长把整曲解码一遍；取不到时回退到 <see cref="LoadAudioAsync"/>。
        /// </summary>
        Task<TimeSpan> GetAudioDurationAsync(string filePath, CancellationToken cancellationToken = default);

        IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; }
    }
}
