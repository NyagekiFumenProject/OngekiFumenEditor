using OpenTK.Wpf;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IDrawingManager
	{
		/// <summary>
		/// 等待渲染环境初始化完成
		/// </summary>
		/// <param name="cancellation"></param>
		/// <returns></returns>
		Task WaitForGraphicsInitializationDone(CancellationToken cancellation = default);

		/// <summary>
		/// 等待渲染环境初始化完成
		/// </summary>
		/// <param name="cancellation"></param>
		/// <returns></returns>
		Task CreateGraphicsContext(GLWpfControl glView, CancellationToken cancellation = default);

		/// <summary>
		/// 检查并试图初始化渲染环境
		/// </summary>
		/// <returns></returns>
		Task CheckOrInitGraphics();
	}
}
