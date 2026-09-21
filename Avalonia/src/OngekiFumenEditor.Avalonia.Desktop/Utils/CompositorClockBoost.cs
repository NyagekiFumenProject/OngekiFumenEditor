using System;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Avalonia.Desktop.Utils;

/// <summary>
/// Windows 合成器时钟提升(dcomp.h <c>DCompositionBoostCompositorClock</c>)。
/// 该调用请求系统在存在延迟敏感的合成内容时动态切到更高的刷新率,用于验证
/// 高刷显示器上编辑器合成节拍是否被合成器时钟限制。请求必须成对释放。
/// </summary>
/// <remarks>
/// 仅在 Windows 11(Build 22000)及以上存在该导出;更低版本会返回 <see cref="UnsupportedHResult"/>。
/// </remarks>
internal static class CompositorClockBoost
{
    private const string LibraryName = "dcomp.dll";
    private const string FunctionName = "DCompositionBoostCompositorClock";

    /// <summary>系统未导出该 API 时返回的 HRESULT(E_NOTIMPL)。</summary>
    private const int UnsupportedHResult = unchecked((int)0x80004001);

    /// <summary>
    /// 请求(<paramref name="enabled"/> 为 <see langword="true"/>)或释放(false)合成器时钟提升。
    /// </summary>
    /// <param name="enabled">是否请求提升刷新率。</param>
    /// <param name="hresult">原始 HRESULT;当前系统不支持该 API 时为 E_NOTIMPL。</param>
    /// <returns>调用成功时为 <see langword="true"/>。</returns>
    public static bool TrySetEnabled(bool enabled, out int hresult)
    {
        try
        {
            hresult = DCompositionBoostCompositorClock(enabled);
            return hresult >= 0;
        }
        catch (DllNotFoundException)
        {
            hresult = UnsupportedHResult;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            hresult = UnsupportedHResult;
            return false;
        }
    }

    [DllImport(LibraryName, EntryPoint = FunctionName, ExactSpelling = true)]
    private static extern int DCompositionBoostCompositorClock([MarshalAs(UnmanagedType.Bool)] bool enable);
}
