namespace Tye.Hosting
{
    internal class ProcessResult
    {
        public ProcessResult(string standardOutput, string standardError, int exitCode)
        {
            StandardOutput = standardOutput;
            StandardError = standardError;
            ExitCode = exitCode;
        }

        public string StandardOutput { get; }
        public string StandardError { get; }
        public int ExitCode { get; }
    }
}
