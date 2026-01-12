namespace Frontend.Services
{
    public static class AppSettings
    {
        public static string? FrontendSecret => System.Environment.GetEnvironmentVariable("FRONTEND_SECRET_KEY");
    }
}
