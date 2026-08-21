namespace ChurchProjection.Api;

/// <summary>
/// The one shape every non-2xx response takes (API-CONTRACT "Errors"). Messages
/// are written for the volunteer running the service, and never carry a stack
/// trace.
/// </summary>
public sealed record ApiError(ApiError.Body Error)
{
    public sealed record Body(string Code, string Message);

    public static IResult Result(int status, string code, string message) =>
        Results.Json(new ApiError(new Body(code, message)), statusCode: status);

    public static IResult BadRequest(string code, string message) => Result(400, code, message);

    public static IResult NotFound(string code, string message) => Result(404, code, message);
}
