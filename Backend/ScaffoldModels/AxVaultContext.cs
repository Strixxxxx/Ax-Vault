using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Backend.ScaffoldModels;

public partial class AxVaultContext : DbContext
{
    public AxVaultContext()
    {
    }

    public AxVaultContext(DbContextOptions<AxVaultContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Get database connection parameters from environment variables
            var dbServer = Environment.GetEnvironmentVariable("DB_SERVER");
            var dbDatabase = Environment.GetEnvironmentVariable("DB_DATABASE");
            var dbUser = Environment.GetEnvironmentVariable("DB_USER");
            var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
            var dbPort = Environment.GetEnvironmentVariable("DB_PORT");

            // Build connection string from environment variables
            string connectionString = $"Server={dbServer}{(string.IsNullOrEmpty(dbPort) ? "" : $",{dbPort}")};Database={dbDatabase};User ID={dbUser};Password={dbPassword};TrustServerCertificate=True";
            
            optionsBuilder.UseSqlServer(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Username).HasName("PK__Accounts__F3DBC5737C865120");

            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");
            entity.Property(e => e.DatabaseName)
                .HasMaxLength(50)
                .HasColumnName("Database Name");
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .HasColumnName("email");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.UniqueKey)
                .HasMaxLength(255)
                .HasColumnName("unique key");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
