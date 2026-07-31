using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics;

[RegisterSingleton<IRenderManager>]
internal class DefaultRenderManager : IRenderManager
{
    private readonly IEnumerable<IRenderManagerImpl> implments;
    private IRenderManagerImpl currentImpl;

    public DefaultRenderManager(IEnumerable<IRenderManagerImpl> implments)
    {
        this.implments = implments.ToArray();
        if (this.implments.GroupBy(x => x.Name).FirstOrDefault(x => x.Count() > 1)?.Key is string conflictName)
            throw new Exception($"There are more render manager objects with same name: {conflictName}");
    }

    public IEnumerable<string> GetAvaliableRenderManagerImplNames()
    {
        return implments
            .Where(x => x.Name.Equals("Skia", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name);
    }

    public IRenderManagerImpl GetCurrentRenderManagerImpl()
    {
        if (currentImpl is not null)
            return currentImpl;

        if (implments.FirstOrDefault(x => x.Name.Equals("Skia", StringComparison.OrdinalIgnoreCase)) is IRenderManagerImpl impl)
            return currentImpl = impl;

        throw new InvalidOperationException("The Avalonia.Skia render manager is not registered.");
    }

    public void SetRenderManagerImpl(string implName)
    {
        if (!string.Equals(implName, "Skia", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Only the Skia render manager is supported: {implName}");

        currentImpl = GetCurrentRenderManagerImpl();
    }
}
