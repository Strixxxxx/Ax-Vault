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

            // Set the base address to your development machine's IP where Nginx is running
            string apiBaseUrl = "http://192.168.100.105:5180"; // Replace with your actual IP address
            instance.BaseAddress = new Uri(apiBaseUrl);

            // Get the frontend secret key from the manually loaded settings
            var secretKey = AppSettings.FrontendSecret;
            if (string.IsNullOrEmpty(secretKey))
            {
                // This is a critical failure. The app cannot communicate with the backend.
                throw new Exception("CRITICAL: FRONTEND_SECRET_KEY is not set. API calls will fail.");
            }
            else
            {
                // Add the secret key as a default header for all requests
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
