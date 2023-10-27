using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ArgProcesser
{
	public interface IProgramArgProcessManager
	{
		Task ProcessArgs(string[] args);
	}
}
