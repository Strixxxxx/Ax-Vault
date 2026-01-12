using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.OpenApi.Models;
using Backend.Data;
using Backend.Middleware;
using Backend.Services; // Import the services namespace
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

// Load .env file at the very beginning
try
{
    var currentDirectory = Directory.GetCurrentDirectory();
    var envFile = Path.Combine(currentDirectory, ".env");
    Console.WriteLine($"Application working directory: {currentDirectory}");
    Console.WriteLine($"Looking for .env file at: {envFile}");
    
    if (File.Exists(envFile))
    {
        Console.WriteLine("✅ .env file found!");
        
        try
        {
            DotNetEnv.Env.Load(envFile);
            Console.WriteLine("✅ .env file loaded successfully");
            
            // Verify that critical JWT settings were loaded
            bool jwtConfigValid = true;
            
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
            if (string.IsNullOrEmpty(jwtSecret))
            {
                Console.WriteLine("❌ JWT_SECRET not found in .env file or couldn't be loaded");
                jwtConfigValid = false;
            }
            
            var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
            if (string.IsNullOrEmpty(jwtIssuer))
            {
                Console.WriteLine("❌ JWT_ISSUER not found in .env file or couldn't be loaded");
                jwtConfigValid = false;
            }
            
            var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
            if (string.IsNullOrEmpty(jwtAudience))
            {
                Console.WriteLine("❌ JWT_AUDIENCE not found in .env file or couldn't be loaded");
                jwtConfigValid = false;
            }
            
            if (jwtConfigValid)
            {
                Console.WriteLine("✅ JWT configuration loaded successfully");
            }
            else
            {
                Console.WriteLine("⚠️ JWT configuration incomplete. Authentication will fail!");
                Console.WriteLine("⚠️ Make sure your .env file contains JWT_SECRET, JWT_ISSUER, and JWT_AUDIENCE");
            }
        }
        catch (Exception envLoadEx)
        {
            Console.WriteLine($"❌ Error parsing .env file: {envLoadEx.Message}");
            Console.WriteLine("⚠️ Please check the format of your .env file");
        }
    }
    else
    {
        Console.WriteLine("❌ .env file not found!");
        Console.WriteLine("⚠️ The application requires a .env file with JWT configuration.");
        Console.WriteLine("⚠️ Please create a .env file based on env-template.txt.");
        
        // Try to copy the template file if it exists
        var templateFile = Path.Combine(currentDirectory, "env-template.txt");
        if (File.Exists(templateFile))
        {
            Console.WriteLine("ℹ️ Found env-template.txt file. You can copy it to create your .env file:");
            Console.WriteLine($"   Copy {templateFile} to {envFile}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Critical error accessing .env file: {ex.Message}");
    Console.WriteLine("⚠️ Authentication and database connections will likely fail!");
}

var builder = WebApplication.CreateBuilder(args);
string connectionString = string.Empty;

try
{
    // Use ConnectionHelper to get the master connection string
    connectionString = ConnectionHelper.GetMasterConnectionString();
    
    // Configure Entity Framework with SQL Server
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
catch (InvalidOperationException ex)
{
    // Log the error and prevent the app from starting if the connection string is invalid
    // A logger is not available here, so we use Console
    Console.WriteLine($"❌ CRITICAL: {ex.Message}");
    // Exit the application if database configuration is missing
    return;
}


// Register services
builder.Services.AddScoped<Backend.Services.PlatformTableService>();
builder.Services.AddSingleton<EncryptionService>();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMauiApp", policy =>
    {
        policy.AllowAnyOrigin() // For development, we'll allow any origin
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add API controller support
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ax Vault API", Version = "v1" });
});

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),
        ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
        NameClaimType = JwtRegisteredClaimNames.UniqueName, // Look for unique_name claim
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_SECRET")!))
    };
});

builder.Services.AddAuthorization();


var app = builder.Build();

// Get the logger from the app
var logger = app.Logger;

// Skip automatic migrations (the tables already exist)
logger.LogWarning("Skipping database migrations for existing tables.");

// Test database connection directly
try
{
    logger.LogInformation("Attempting to connect to the master database...");
    var maskedConnectionString = ConnectionHelper.MaskConnectionString(connectionString);

    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    logger.LogInformation("✅ Successfully connected to the master database!");
    connection.Close();
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Database connection failed. Please check your database configuration in the .env file.");
    // We can choose to exit here if a database connection is absolutely required to run.
    // For now, we will log the error and continue, as some endpoints might not need a DB.
}


// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ax Vault API v1"));
}

app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowMauiApp");

app.UseAuthentication();
app.UseAuthorization();

// Register the custom app verifier middleware
app.UseMiddleware<AppVerifierMiddleware>();

// Map endpoints
app.MapGet("/", () => "Ax Vault API is running!");      
app.MapControllers();

// Register a callback to display the actual listening URLs
app.Lifetime.ApplicationStarted.Register(() =>
{
    var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
    var addressFeature = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
    
    if (addressFeature != null)
    {
        foreach (var address in addressFeature.Addresses)
        {
            Console.WriteLine($"Now listening on: {address}");
        }
    }
});

app.Run();
