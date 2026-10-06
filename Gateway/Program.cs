using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("per-user", httpContext =>
    {
        var key = httpContext.User.Identity?.Name
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anon";

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        // EN: Only for routes that require authentication.
        // PT: Só para rotas que exigem autenticação.
        var policy = context.Route.AuthorizationPolicy;
        if (string.IsNullOrEmpty(policy))
            return;

        context.AddRequestTransform(transform =>
        {
            var userId = transform.HttpContext.User
                .FindFirst("sub")?.Value;
            var headers = transform.ProxyRequest.Headers;

            if (userId is not null)
                headers.TryAddWithoutValidation(
                    "X-User-Id", userId);

            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapReverseProxy();

app.Run();
