# Ax Vault: Backend API

The backend for Ax Vault is a high-performance ASP.NET Core Web API that manages user authentication, account synchronization, and multi-tenant database operations.

## 🛠️ Technical Overview

- **Framework**: ASP.NET Core Web API
- **Language**: C#
- **Database Access**: Raw ADO.NET (SqlConnection) for maximum control and performance.
- **Database Engine**: PostgreSQL.
- **Security**: 
  - **Argon2id**: Industry-standard key derivation for secure password hashing.
  - **JWT**: Bearer token authentication for secure communication.
  - **Multi-Tenancy**: Uses a `Vaults` directory table to manage and route requests to isolated, dynamic tables for each user and platform (e.g., `"5_Gmail"`).

## 🚀 Getting Started

1.  **Prerequisites**:
    - .NET SDK (v9.0 or later recommended).
    - PostgreSQL instance.

2.  **Configuration**:
    - Update `appsettings.json` or `.env` with your SQL Connection String.

3.  **Run the API**:
    ```powershell
    cd Backend
    dotnet restore
    dotnet run
    ```

The API will be available at `http://localhost:5180`.

## 📂 Key Components

- **Controllers**: Handle HTTP requests (Account, Platform, Auth).
- **Services**: Business logic for encryption, email (OTP), and table management.
- **Data**: Connection helpers and raw SQL execution logic.
