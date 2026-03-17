using Npgsql;

namespace Backend.Services
{
    public static class ConnectionHelper
    {
        private static readonly string? DbServer = Environment.GetEnvironmentVariable("DB_SERVER");
        private static readonly string? DbUser = Environment.GetEnvironmentVariable("DB_USER");
        private static readonly string? DbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
        private static readonly string? DbPort = Environment.GetEnvironmentVariable("DB_PORT");
        private static readonly string? MasterDbName = Environment.GetEnvironmentVariable("DB_DATABASE")?.Trim('"');

        public static string GetMasterConnectionString()
        {
            if (string.IsNullOrEmpty(DbServer) || string.IsNullOrEmpty(DbUser) || string.IsNullOrEmpty(DbPassword) || string.IsNullOrEmpty(MasterDbName))
            {
                throw new InvalidOperationException("One or more master database connection environment variables are not set. Please check your .env file and ensure DB_SERVER, DB_DATABASE, DB_USER, and DB_PASSWORD are all set.");
            }
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = DbServer,
                Database = MasterDbName,
                Username = DbUser,
                Password = DbPassword,
                SslMode = SslMode.Require
            };

            if (!string.IsNullOrEmpty(DbPort) && int.TryParse(DbPort, out int port))
            {
                builder.Port = port;
            }

            return builder.ConnectionString;
        }

        public static string GetUserDbConnectionString(string dbName)
        {
            if (string.IsNullOrEmpty(DbServer) || string.IsNullOrEmpty(DbUser) || string.IsNullOrEmpty(DbPassword))
            {
                throw new InvalidOperationException("One or more user database connection environment variables are not set. Please check your .env file and ensure DB_SERVER, DB_USER, and DB_PASSWORD are all set.");
            }
            
            if (string.IsNullOrEmpty(dbName))
            {
                throw new ArgumentException("Database name cannot be null or empty.", nameof(dbName));
            }
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = DbServer,
                Database = dbName,
                Username = DbUser,
                Password = DbPassword,
                SslMode = SslMode.Require
            };

            if (!string.IsNullOrEmpty(DbPort) && int.TryParse(DbPort, out int port))
            {
                builder.Port = port;
            }

            return builder.ConnectionString;
        }
        
        public static string GetMasterConnectionStringWithoutInitialCatalog()
        {
            if (string.IsNullOrEmpty(DbServer) || string.IsNullOrEmpty(DbUser) || string.IsNullOrEmpty(DbPassword))
            {
                throw new InvalidOperationException("One or more master database connection environment variables are not set. Please check your .env file and ensure DB_SERVER, DB_USER, and DB_PASSWORD are all set.");
            }
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = DbServer,
                Database = "postgres", // Default database for PG if none specified
                Username = DbUser,
                Password = DbPassword,
                SslMode = SslMode.Require
            };

            if (!string.IsNullOrEmpty(DbPort) && int.TryParse(DbPort, out int port))
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
                // If the connection string is malformed, we can't parse it, so we return a generic masked value.
                return "[malformed connection string - cannot mask password]";
            }
        }
    }
}
