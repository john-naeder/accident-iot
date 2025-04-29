namespace AccidentMonitor.Application.Common.Results;

public class ServiceResult(int code, string message)
{
    public int Code { get; } = code;
    public string Message { get; } = message ?? string.Empty;
    public bool IsSuccess => Code == 0;
}
