namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel
{
	public struct ExecuteResult
	{
		public ExecuteResult(bool isSuccess, string errorMessage = default, object result = default)
		{
			this.Success = isSuccess;
			this.ErrorMessage = errorMessage;
			Result = result;
		}

		public bool Success { get; set; }
		public string ErrorMessage { get; set; }
		public object Result { get; }
	}
}
