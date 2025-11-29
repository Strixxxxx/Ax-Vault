using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;
using Backend.Services;

namespace Backend.Middleware
{
    public class AppVerifierMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string? _backendSecretHash;

        public AppVerifierMiddleware(RequestDelegate next)
        {
            _next = next;
            _backendSecretHash = Environment.GetEnvironmentVariable("BACKEND_SECRET_KEY");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Allow the temporary hash generation endpoint to bypass this check
            if (context.Request.Path.StartsWithSegments("/api/auth/hash-secret"))
            {
                await _next(context);
                return;
            }
            
            if (string.IsNullOrEmpty(_backendSecretHash))
            {
                // If the backend secret isn't configured, block all requests for security.
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("Service is not configured. Backend secret is missing.");
                return;
            }

            if (!context.Request.Headers.TryGetValue("X-Frontend-Secret", out var frontendSecret))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Forbidden: Frontend secret is missing.");
                return;
            }

            if (!PasswordHasher.VerifyPassword(_backendSecretHash, frontendSecret!))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden: Invalid frontend secret.");
                return;
            }

            await _next(context);
        }
    }
}
