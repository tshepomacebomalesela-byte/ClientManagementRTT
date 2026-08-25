using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace ClientManagement.Service;

public sealed class BasicAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public BasicAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration) { _next = next; _configuration = configuration; }

    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var header) || !AuthenticationHeaderValue.TryParse(header, out var value) || !value.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase)) { Challenge(context); return; }
        try
        {
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(value.Parameter ?? string.Empty)).Split(':', 2);
            if (credentials.Length != 2 || credentials[0] != (_configuration["Auth:Username"] ?? "admin") || credentials[1] != (_configuration["Auth:Password"] ?? "admin123")) { Challenge(context); return; }
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.Name, credentials[0]) }, "Basic"));
            await _next(context);
        }
        catch (FormatException) { Challenge(context); }
    }

    private static void Challenge(HttpContext context) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; context.Response.Headers.WWWAuthenticate = "Basic realm=\"Client Management\""; }
}