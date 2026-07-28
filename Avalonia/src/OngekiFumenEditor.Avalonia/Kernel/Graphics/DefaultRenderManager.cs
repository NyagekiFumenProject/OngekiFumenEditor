using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

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
        return implments.Select(x => x.Name);
    }

    public IRenderManagerImpl GetCurrentRenderManagerImpl()
    {
        if (currentImpl is not null)
            return currentImpl;

        var defaultName = Properties.ProgramSetting.Default.DefaultRenderManagerImplementName;
        if (implments.FirstOrDefault(x => x.Name.Equals(defaultName, StringComparison.InvariantCultureIgnoreCase)) is IRenderManagerImpl impl)
            return currentImpl = impl;

        return currentImpl = implments.FirstOrDefault();
    }

    public void SetRenderManagerImpl(string implName)
    {
        Properties.ProgramSetting.Default.DefaultRenderManagerImplementName = implName;
        Properties.ProgramSetting.Default.Save();
    }
}
