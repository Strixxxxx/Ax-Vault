namespace Frontend.Services
{
    public static class AppSettings
    {
        public static string? FrontendSecret => DotNetEnv.Env.GetString("FRONTEND_SECRET_KEY");
    }
}
