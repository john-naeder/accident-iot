namespace AccidentMonitor.Application.common;
public class ServiceResult
{
    public int Code { get; }
    public string Message { get; }
    public bool IsSuccess => Code == 0;

    public ServiceResult(int code, string message)
    {
        Code = code;
        Message = message ?? string.Empty;
    }
}