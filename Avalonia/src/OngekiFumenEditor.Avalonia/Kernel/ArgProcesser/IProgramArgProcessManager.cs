using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Kernel.ArgProcesser
{
	public interface IProgramArgProcessManager
	{
		Task ProcessArgs(string[] args);
    }
}

