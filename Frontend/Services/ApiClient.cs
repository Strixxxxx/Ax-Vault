using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json; // Added for JsonContent

namespace Frontend.Services
{
    public class ErrorLogModel
    {
        public string Message { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ApiClient
    {
        private static readonly Lazy<HttpClient> _lazyInstance = new Lazy<HttpClient>(() =>
        {
            var instance = new HttpClient();

            // Dual-Mode Connectivity: Try local first, then fall back to Render
            string localUrl = "http://192.168.100.105:5180/";
            string renderUrl = "https://ax-vault.onrender.com/";
            string apiBaseUrl = renderUrl; // Default to Render

            try
            {
                // Quick health check to see if local backend is active
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var checkClient = new HttpClient { BaseAddress = new Uri(localUrl), Timeout = TimeSpan.FromSeconds(2) };
                
                // We just need a response, even a 404 is fine as long as the server is "there"
                var responseTask = checkClient.GetAsync("/", cts.Token);
                responseTask.Wait(cts.Token);
                
                if (responseTask.IsCompletedSuccessfully)
                {
                    apiBaseUrl = localUrl;
                    Console.WriteLine("🚀 Connected to LOCAL backend (192.168.100.105)");
                }
            }
            catch
            {
                Console.WriteLine("☁️ Local backend not found. Falling back to RENDER.");
            }

            instance.BaseAddress = new Uri(apiBaseUrl);

            // Get the frontend secret key from the manually loaded settings
            var secretKey = AppSettings.FrontendSecret;
            if (string.IsNullOrEmpty(secretKey))
            {
                throw new Exception("CRITICAL: FRONTEND_SECRET_KEY is not set. API calls will fail.");
            }
            else
            {
                instance.DefaultRequestHeaders.Add("X-Frontend-Secret", secretKey);
            }
            return instance;
        });

        public static HttpClient Instance => _lazyInstance.Value;

        public static void SetAuthToken(string? token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Instance.DefaultRequestHeaders.Authorization = null;
            }
            else
            {
                Instance.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public static async Task SendErrorLog(Exception ex, string source = "Frontend")
        {
            try
            {
                var errorLog = new ErrorLogModel
                {
                    Message = ex.Message,
                    StackTrace = ex.StackTrace ?? "No stack trace available",
                    Source = source,
                    Type = ex.GetType().Name,
                    Timestamp = DateTime.UtcNow
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "api/errorlog");
                request.Content = JsonContent.Create(errorLog);

                // Do not await the response, send and forget to avoid blocking
                _ = Instance.SendAsync(request);
            }
            catch (Exception logEx)
            {
                // Log to console if sending error log fails
                Console.WriteLine($"Failed to send error log: {logEx.Message}");
            }
        }
    }
}
