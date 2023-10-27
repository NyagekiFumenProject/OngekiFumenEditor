namespace OngekiFumenEditor.Base
{
	public interface ISerializable
	{
		/// <summary>
		/// 生成符合谱面格式定义的内容
		/// </summary>
		/// <param name="fumenData"></param>
		/// <returns></returns>
		public string Serialize();
	}
}
