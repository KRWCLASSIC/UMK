namespace UMK.Diagnostics;

public enum UmkExitCode
{
    Success = 0,
    ExecutionError = 1,
    ConfigurationError = 2,
    UsageError = 3
}

public sealed class UmkException : Exception
{
    public UmkExitCode ExitCode { get; }

    public UmkException(string message, UmkExitCode exitCode = UmkExitCode.ConfigurationError)
        : base(message)
    {
        ExitCode = exitCode;
    }

    public UmkException(string message, Exception innerException, UmkExitCode exitCode = UmkExitCode.ConfigurationError)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }
}

