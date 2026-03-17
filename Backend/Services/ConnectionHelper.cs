using Npgsql;
using Microsoft.Extensions.Configuration;

namespace Backend.Services
{
    public class ConnectionHelper
    {
        private readonly IConfiguration _configuration;

        public ConnectionHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string? GetSetting(string name)
        {
            // Try IConfiguration first (mapped to appsettings.json or actual environment variables)
            var value = _configuration[name];
            
            // Fallback to Environment if not found or empty (though IConfiguration usually picks this up)
            if (string.IsNullOrEmpty(value))
            {
                value = Environment.GetEnvironmentVariable(name);
            }

            return value;
        }

        public string GetMasterConnectionString()
        {
            string? server = GetSetting("DB_SERVER");
            string? user = GetSetting("DB_USER");
            string? password = GetSetting("DB_PASSWORD");
            string? portStr = GetSetting("DB_PORT");
            string? database = GetSetting("DB_DATABASE")?.Trim('"');

            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(database))
            {
                throw new InvalidOperationException("One or more master database connection settings are not set. Ensure DB_SERVER, DB_DATABASE, DB_USER, and DB_PASSWORD are provided via Environment or appsettings.json.");
            }

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = server,
                Database = database,
                Username = user,
                Password = password,
                SslMode = SslMode.Require
            };

            if (!string.IsNullOrEmpty(portStr) && int.TryParse(portStr, out int port))
            {
                builder.Port = port;
            }

            return builder.ConnectionString;
        }

        public string GetUserDbConnectionString(string dbName)
        {
            string? server = GetSetting("DB_SERVER");
            string? user = GetSetting("DB_USER");
            string? password = GetSetting("DB_PASSWORD");
            string? portStr = GetSetting("DB_PORT");

            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("One or more user database connection settings are not set. Ensure DB_SERVER, DB_USER, and DB_PASSWORD are provided.");
            }
            
            if (string.IsNullOrEmpty(dbName))
            {
                throw new ArgumentException("Database name cannot be null or empty.", nameof(dbName));
            }

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = server,
                Database = dbName,
                Username = user,
                Password = password,
                SslMode = SslMode.Require
            };

            if (!string.IsNullOrEmpty(portStr) && int.TryParse(portStr, out int port))
            {
                builder.Port = port;
            }

            return builder.ConnectionString;
        }
        
        public string GetMasterConnectionStringWithoutInitialCatalog()
        {
            string? server = GetSetting("DB_SERVER");
            string? user = GetSetting("DB_USER");
            string? password = GetSetting("DB_PASSWORD");
            string? portStr = GetSetting("DB_PORT");

            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("One or more master database connection settings are not set.");
            }

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = server,
                Database = "postgres", 
                Username = user,
                Password = password,
                SslMode = SslMode.Require
            };

            if (!string.IsNullOrEmpty(portStr) && int.TryParse(portStr, out int port))
            {
                builder.Port = port;
            }

            return builder.ConnectionString;
        }

        public static string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return "[empty]";
            
            try 
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString) { Password = "******" };
                return builder.ConnectionString;
            }
            catch 
            {
                return "[malformed connection string - cannot mask password]";
            }
        }
    }
}
