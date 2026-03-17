using Microsoft.EntityFrameworkCore;
using System;
using Npgsql;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.OpenApi.Models;
using Backend.Data;
using Backend.Middleware;
using Backend.Services; // Import the services namespace
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Smart .env loading: Only attempt to load if the file exists (Local machine)
// On Render, environment variables are injected directly into the system.
try
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (File.Exists(envPath))
    {
        DotNetEnv.Env.Load(envPath);
        Console.WriteLine("✅ Local .env file loaded successfully.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Note: Could not load .env file: {ex.Message}");
}

var builder = WebApplication.CreateBuilder(args);

// Register Security Services
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<ConnectionHelper>();

string connectionString = string.Empty;

try
{
    // Use a temporary instance to get the connection string during startup
    var tempHelper = new ConnectionHelper(builder.Configuration);
    connectionString = tempHelper.GetMasterConnectionString();
    
    // Configure Entity Framework with PostgreSQL
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}
catch (Exception ex)
{
    Console.WriteLine($"❌ CRITICAL: Database configuration error: {ex.Message}");
    return;
}


// Register services
builder.Services.AddScoped<Backend.Services.PlatformTableService>();
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddScoped<EmailService>();

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
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT_ISSUER"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER"),
        ValidAudience = builder.Configuration["JWT_AUDIENCE"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
        NameClaimType = JwtRegisteredClaimNames.UniqueName,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["JWT_SECRET"] ?? Environment.GetEnvironmentVariable("JWT_SECRET") ?? "fallback_secret_key_for_development_only"))
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

    using var connection = new NpgsqlConnection(connectionString);
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
