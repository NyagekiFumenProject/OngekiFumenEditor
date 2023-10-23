using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;

namespace OngekiFumenEditor.Modules.AudioAdjustWindow.Commands
{
    [CommandHandler]
    public class ViewAudioAdjustWindowCommandHandler : CommandHandlerBase<ViewAudioAdjustWindowCommandDefinition>
    {
        private readonly IWindowManager _windowManager;

        [ImportingConstructor]
        public ViewAudioAdjustWindowCommandHandler(IWindowManager windowManager)
        {
            _windowManager = windowManager;
        }

        public override async Task Run(Command command)
        {
            await _windowManager.ShowWindowAsync(IoC.Get<IAudioAdjustWindow>());
        }
    }
}