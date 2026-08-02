using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;

public static class FumenConverterWrapper
{
    public record GenerateResult(bool IsSuccess, string Message = "");

    public static Task<GenerateResult> Generate(FumenConvertOption option, OngekiFumen inMemoryFumen = null) =>
        IoC.Get<IFumenConvertService>().GenerateAsync(option, inMemoryFumen);
}
