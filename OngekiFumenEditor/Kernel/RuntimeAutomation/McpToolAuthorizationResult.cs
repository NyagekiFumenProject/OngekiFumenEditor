namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class McpToolAuthorizationResult
    {
        public bool IsAuthorized { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public bool BackupFumenBeforeExecution { get; set; }

        public static McpToolAuthorizationResult Authorized()
        {
            return new McpToolAuthorizationResult
            {
                IsAuthorized = true,
            };
        }

        public static McpToolAuthorizationResult Denied(string errorCode, string errorMessage, bool backupFumenBeforeExecution = false)
        {
            return new McpToolAuthorizationResult
            {
                IsAuthorized = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                BackupFumenBeforeExecution = backupFumenBeforeExecution,
            };
        }
    }
}
