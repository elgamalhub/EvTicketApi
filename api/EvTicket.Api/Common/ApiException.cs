namespace EvTicket.Api.Common;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public string? Code { get; }

    public ApiException(int statusCode, string message, string? code = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public static ApiException NotFound(string message = "Resource not found") => new(404, message, "not_found");
    public static ApiException BadRequest(string message = "Invalid request") => new(400, message, "bad_request");
    public static ApiException Forbidden(string message = "You do not have access to this resource") => new(403, message, "forbidden");
    public static ApiException Conflict(string message, string? code = null) => new(409, message, code ?? "conflict");
    public static ApiException Unauthorized(string message = "Not authenticated") => new(401, message, "unauthorized");
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasNext => Page < TotalPages;
}

public static class AppConstants
{
    public const string AdminRole = "Admin";
    public const string OrganizerRole = "Organizer";
    public const string UserRole = "User";
    public static readonly string[] Roles = [AdminRole, OrganizerRole, UserRole];
}
