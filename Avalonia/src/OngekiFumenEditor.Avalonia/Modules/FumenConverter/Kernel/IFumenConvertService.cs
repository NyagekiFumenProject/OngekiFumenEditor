using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;

public interface IFumenConvertService
{
    Task<FumenConverterWrapper.GenerateResult> GenerateAsync(
        FumenConvertOption option,
        OngekiFumen inMemoryFumen = null,
        CancellationToken cancellationToken = default);
}
