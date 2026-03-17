# Ax Vault: Zero-Knowledge Password Manager

Ax Vault is a secure, multi-platform, and multi-tenant password management system designed with **Zero-Knowledge** security principles. Your data is encrypted on your device before it ever touches the server, ensuring that only you have access to your sensitive information.

## 🚀 Key Features

- **Zero-Knowledge Security**: End-to-end encryption using Argon2id for key derivation and AES-256-GCM for data encryption.
- **Multi-Tenant Architecture**: A shared database using a `Vaults` directory table to navigate and manage isolated, user-specific dynamic tables (format: `[AccountID]_[Platform]`).
- **Cross-Platform**: Built with .NET MAUI, supporting both **Windows** and **Android**.
- **Built-In Customized Backups**: Secure, encrypted backup and restore functionality to keep your data safe.
- **CI/CD Integration**: Automated backend deployment to Render via GitHub Actions for continuous delivery.

## 📂 Project Structure

This repository is a monorepo containing both the frontend and backend components:

- **[Backend](./Backend)**: ASP.NET Core Web API with PostgreSQL database integration.
- **[Frontend](./Frontend)**: .NET MAUI application for desktop (Windows) and mobile (Android).
- **[SETUP.md](./SETUP.md)**: Comprehensive guide for local development and automated production deployment.

## 🛠️ Technology Stack

| Layer | Technology |
| :--- | :--- |
| **Frontend** | .NET MAUI, C#, XAML |
| **Backend** | ASP.NET Core Web API, C# |
| **Database** | PostgreSQL (Raw ADO.NET / Npgsql) |
| **Security** | Argon2id, AES-256-GCM, JWT |
| **DevOps** | GitHub Actions, Render |

---

For detailed setup and deployment instructions, please refer to the **[SETUP.md](./SETUP.md)** file.
