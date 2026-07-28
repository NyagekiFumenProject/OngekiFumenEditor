#if false
// Legacy migration snapshot (commented by design).
// Source: OngekiFumenEditor/Modules/PreviewSvgGenerator/Kernel/DefaultPreviewSvgGenerator.cs
// Keep this file as a restore guide while SVG runtime dependencies are being replaced.

namespace OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator.Kernel;

public partial class DefaultPreviewSvgGenerator
{
    /*
     * Restorable blocks:
     * - GenerateCriticalEffect()
     * - SerializeFumenToSvg()
     * - SerializeBeams()
     * - SerializeTap()
     * - SerializeBell()
     * - SerializeEvents()
     * - SerializeLanes()
     * - SerializePlayField()
     *
     * Migration strategy:
     * 1) Restore geometry/math-only code first.
     * 2) Replace WPF/System.Windows-dependent color/text parts.
     * 3) Re-enable Svg rendering output per section.
     */
}
#endif


