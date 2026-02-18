using Frontend.Services; // Add this using statement
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Frontend.Components.RouteGuard
{
    public class RouteGuardService
    {
        public RouteGuardService()
        {
            // Constructor is now empty
        }

        public async Task<bool> ValidateModuleAccess(string username, string vaultPassword, string targetModule, string? token = null)
        {
            try
            {
                Console.WriteLine("--- ROUTE GUARD SERVICE ---");
                Console.WriteLine($"Validating access for user '{username}' to module '{targetModule}'");

                // Get the auth token: Use passed token if available, otherwise check secure storage
                if (string.IsNullOrEmpty(token))
                {
                    token = SessionService.Instance.AuthToken;
                }
                
                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }
                Console.WriteLine($"Found auth token of length: {token.Length}. First 15 chars: {token.Substring(0, Math.Min(15, token.Length))}...");


                var requestData = new RouteGuardRequest
                {
                    VaultPassword = vaultPassword,
                    TargetModule = targetModule,
                    UserIdentifier = SessionService.Instance.UserIdentifier // Pass identifier explicitly
                };

                // Create HTTP request with authorization header
                var request = new HttpRequestMessage(HttpMethod.Post, "api/RouteGuard/validate");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(requestData);

                // Log request details (don't log the full unique key for security)
                var requestJson = await request.Content.ReadAsStringAsync();
                Console.WriteLine($"Sending request with Payload: {requestJson}");


                var response = await ApiClient.Instance.SendAsync(request);

                Console.WriteLine($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RouteGuardResponse>();
                    Console.WriteLine($"[RouteGuard] Success! IsAuthorized={result?.IsAuthorized}, Message={result?.Message}");
                    return result?.IsAuthorized ?? false;
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[RouteGuard] FAILED with status {response.StatusCode}");
                Console.WriteLine($"[RouteGuard] Error Body: {errorContent}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating module access: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }
    }

    public class RouteGuardRequest
    {
        public required string VaultPassword { get; set; }
        public required string TargetModule { get; set; }
        public string? UserIdentifier { get; set; }
    }

    public class RouteGuardResponse
    {
        public bool IsAuthorized { get; set; }
        public required string Message { get; set; }
    }
} 