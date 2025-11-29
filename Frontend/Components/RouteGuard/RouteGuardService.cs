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

        public async Task<bool> ValidateModuleAccess(string username, string uniqueKey, string targetModule)
        {
            try
            {
                Console.WriteLine($"RouteGuardService - Validating access for user '{username}' to module '{targetModule}'");

                var request = new RouteGuardRequest
                {
                    Username = username,
                    UniqueKey = uniqueKey,
                    TargetModule = targetModule
                };

                // Log request details (don't log the full unique key for security)
                Console.WriteLine($"Sending request with Username: {username}, TargetModule: {targetModule}, Key Length: {uniqueKey?.Length ?? 0}");

                var response = await ApiClient.Instance.PostAsJsonAsync("api/RouteGuard/validate", request);

                Console.WriteLine($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RouteGuardResponse>();
                    Console.WriteLine($"Response content: IsAuthorized={result?.IsAuthorized}, Message={result?.Message}");
                    return result?.IsAuthorized ?? false;
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error response: {errorContent}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating module access: {ex.Message}");
                return false;
            }
        }
    }

    public class RouteGuardRequest
    {
        public required string Username { get; set; }
        public required string UniqueKey { get; set; }
        public required string TargetModule { get; set; }
    }

    public class RouteGuardResponse
    {
        public bool IsAuthorized { get; set; }
        public required string Message { get; set; }
    }
} 