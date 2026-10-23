namespace OcrWebApp.Middlewares;

public class UserSessionMiddleware
{
    public const string CookieName = "OCR_USER_SESSION";
    public const string SessionItemKey = "SessionToken";

    private readonly RequestDelegate _next;

    public UserSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? sessionToken = null;

        if (context.Request.Cookies.TryGetValue(CookieName, out var cookieVal) &&
            !string.IsNullOrWhiteSpace(cookieVal) &&
            Guid.TryParse(cookieVal, out _))
        {
            sessionToken = cookieVal;
        }

        if (string.IsNullOrEmpty(sessionToken))
        {
            sessionToken = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append(CookieName, sessionToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = context.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                IsEssential = true
            });
        }

        context.Items[SessionItemKey] = sessionToken;

        await _next(context);
    }
}
