using System;
using System.Net.Http;

namespace Frontend.Services
{
    public class ApiClient
    {
        private static readonly Lazy<HttpClient> _lazyInstance = new Lazy<HttpClient>(() =>
        {
            var instance = new HttpClient();

            // Set the base address to your development machine's IP where Nginx is running
            string apiBaseUrl = "http://localhost:5180/"; // Replace with your actual IP address
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
    }
}
