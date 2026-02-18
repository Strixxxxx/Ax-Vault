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

            Console.WriteLine($"[AppVerifier] Incoming request: {context.Request.Method} {context.Request.Path}");

            if (!context.Request.Headers.TryGetValue("X-Frontend-Secret", out var frontendSecret))
            {
                Console.WriteLine("[AppVerifier] REJECTED: X-Frontend-Secret header missing.");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Forbidden: Frontend secret is missing.");
                return;
            }

            if (!PasswordHasher.VerifyPassword(_backendSecretHash, frontendSecret!))
            {
                Console.WriteLine($"[AppVerifier] REJECTED: Secret verification failed. Provided: {frontendSecret.ToString().Substring(0, Math.Min(5, frontendSecret.ToString().Length))}...");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden: Invalid frontend secret.");
                return;
            }

            Console.WriteLine("[AppVerifier] PASSED: Secret verified.");
            await _next(context);
        }
    }
}
