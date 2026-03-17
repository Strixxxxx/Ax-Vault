# Ax Vault: Setup & Deployment Guide

This guide provides a professional procedure for setting up Ax Vault, focusing on local development and automated production deployment to MonsterASP.net.

---

## 🛠️ Prerequisites

Before you begin, ensure you have the following installed:

- **.NET SDK (v9.0+)**: [Download here](https://dotnet.microsoft.com/download/dotnet)
- **.NET MAUI Workload**: Run `dotnet workload install maui`
- **Visual Studio Code Extensions**: 
  - C# Dev Kit (Microsoft)
  - .NET MAUI (Microsoft)
- **PostgreSQL**: Local instance or cloud database.

---

## 💻 Part 1: Local Development

### **Step 1: Backend API**
1.  Navigate to `Backend/`
2.  Configure your connection string in `appsettings.json` (ensure it's a PostgreSQL connection string).
3.  Run: `dotnet restore && dotnet run`
4.  The API serves at `http://localhost:5180`.

### **Step 2: Frontend App**
1.  Navigate to `Frontend/`
2.  Open `Services/ApiClient.cs` and ensure the `BaseAddress` points to your backend (e.g., `http://localhost:5180`).
3.  Run for Windows:
    ```powershell
    dotnet run -f net10.0-windows10.0.19041.0
    ```
4.  Run for Android:
    ```powershell
    dotnet run -f net10.0-android
    ```

---

## 🚀 Part 2: Production Deployment (MonsterASP.net)

Ax Vault uses **GitHub Actions** to automate the deployment of the backend API directly to MonsterASP.net.

### **Step 1: Configure GitHub Secrets**
In your GitHub Repository, go to **Settings** > **Secrets and variables** > **Actions** and add these **Repository Secrets**:

| Secret Name | Description |
| :--- | :--- |
| `WEBDEPLOY_SERVER` | `https://[YourServer].siteasp.net:8172/msdeploy.axd` |
| `WEBDEPLOY_SITE` | Your **Site Name** (e.g., `site59397`) |
| `WEBDEPLOY_USERNAME` | Your **WebDeploy Login** (e.g., `site59397`) |
| `WEBDEPLOY_PASSWORD` | Your **WebDeploy Password** |

### **Step 2: Trigger Deployment**
Any push to the **`main`** branch will automatically trigger the deployment workflow defined in `.github/workflows/deploy.yml`. You can monitor the progress in the **Actions** tab.

### **Step 3: Update Frontend API URL**
Once deployed, remember to update the `BaseAddress` in your `Frontend/Services/ApiClient.cs` to your new production URL from MonsterASP.net.

---

## 🔒 Security Best Practices

- **Zero-Knowledge**: The user's master password is never sent to the server.
- **Secrets Management**: Always use GitHub Secrets for deployment credentials.
- **Dynamic Multi-Tenancy**: Data is isolated into user-prefixed tables, with a `Vaults` table acting as the central directory for navigation.