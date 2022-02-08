using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser.Commands
{
    [CommandHandler]
    public class ViewSvgToLaneBrowserCommandHandler : CommandHandlerBase<ViewSvgToLaneBrowserCommandDefinition>
    {
        private readonly IWindowManager _windowManager;

        [ImportingConstructor]
        public ViewSvgToLaneBrowserCommandHandler(IWindowManager windowManager)
        {
            _windowManager = windowManager;
        }

        public override async Task Run(Command command)
        {
            await _windowManager.ShowWindowAsync(IoC.Get<ISvgToLaneBrowser>());
        }
    }
}