namespace Auth.API.Services.Dokploy;

public class DokployApiException : Exception
{
    public DokployApiException(string message, int? statusCode = null, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public int? StatusCode { get; }
    public string? ResponseBody { get; }
}
