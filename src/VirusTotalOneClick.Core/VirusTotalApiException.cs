namespace VirusTotalOneClick.Core;

public sealed class VirusTotalApiException : Exception
{
    public VirusTotalApiException(string message, int statusCode, TimeSpan? retryAfter = null)
        : base(message)
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    public int StatusCode { get; }

    public TimeSpan? RetryAfter { get; }
}
