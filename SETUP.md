# Ax Vault: Setup & Deployment Guide

This guide provides a professional procedure for setting up Ax Vault, focusing on local development and automated production deployment to MonsterASP.net.

---

## 🛠️ Prerequisites

Before you begin, ensure you have the following installed:

- **.NET SDK (v10.0)**: [Download here](https://dotnet.microsoft.com/download/dotnet)
- **.NET MAUI Workload**: Run `dotnet workload install maui`
- **Visual Studio Code Extensions**: 
  - C# Dev Kit (Microsoft)
  - .NET MAUI (Microsoft)
- **PostgreSQL**: Cloud-hosted database (e.g., Supabase, Aiven, or Render PostgreSQL).

---

## 💻 Part 1: Local Development

### **Step 1: Backend API**
1.  Navigate to `Backend/`
2.  Configure your connection string in `.env` (copy from `.env.example` if available). Ensure it's a valid PostgreSQL connection string.
3.  Run: `dotnet restore && dotnet run`
4.  The API serves at `http://localhost:5180`.

### **Step 2: Frontend App**
1.  Navigate to `Frontend/`
2.  Open `Services/ApiClient.cs` and ensure the `localUrl` points to your backend (e.g., `http://192.168.100.105:5180/`).
3.  Run for Windows:
    ```powershell
    dotnet run -f net10.0-windows10.0.19041.0
    ```
4.  Run for Android:
    ```powershell
    dotnet run -f net10.0-android
    ```

---

## 🚀 Part 2: Production Deployment (Render)

Ax Vault is configured for containerized deployment on **Render**.

### **Step 1: Docker Configuration**
The backend includes a `Dockerfile` that uses .NET 10.0. Render will automatically detect this and build the container.
- **Port**: The application listens on port `10000` (configured in `Dockerfile` via `ASPNETCORE_HTTP_PORTS`).

### **Step 2: Render Environment Variables**
In your Render Dashboard, add the following **Environment Variables**:

| Key | Description |
| :--- | :--- |
| `DB_CONNECTION_STRING` | Your PostgreSQL Connection String |
| `JWT_SECRET` | A secure long string for JWT signing |
| `JWT_ISSUER` | e.g., `AxVaultAPI` |
| `JWT_AUDIENCE` | e.g., `AxVaultUsers` |
| `FRONTEND_SECRET_KEY` | Handshake key for secure API communication |
| `SMTP_HOST` | (Optional) Email service host |
| `SMTP_PORT` | (Optional) Email service port |
| `SMTP_USER` | (Optional) Email service username |
| `SMTP_PASS` | (Optional) Email service password |
| `SMTP_FROM` | (Optional) "From" email address |

### **Step 3: Trigger Deployment**
Any push to the **`main`** branch will automatically trigger a build on Render if you have connected your GitHub repository.

### **Step 4: Update Frontend API URL**
Once deployed, ensure the `renderUrl` in `Frontend/Services/ApiClient.cs` matches your Render service URL (e.g., `https://ax-vault.onrender.com/`).

---

## 🔒 Security Best Practices

- **Zero-Knowledge**: The user's master password is never sent to the server.
- **Secrets Management**: Always use GitHub Secrets for deployment credentials.
- **Dynamic Multi-Tenancy**: Data is isolated into user-prefixed tables, with a `Vaults` table acting as the central directory for navigation.
