# Ax Vault: .NET MAUI Frontend

The frontend for Ax Vault is a modern, responsive mobile and desktop application built with .NET MAUI. It provides a "Zero-Knowledge" interface for managing your digital vault.

## 📱 Platforms

- **Windows**: Desktop application.
- **Android**: Mobile application (v21.0+).

## 🛠️ Key Features

- **Zero-Knowledge Architecture**: All sensitive data is encrypted/decrypted locally. The server never sees your plaintext vault password.
- **Account Management**: Add, update, and organize your credentials with ease.
- **Secure Backups**: Create encrypted ZIP backups with Argon2id and AES-GCM.
- **OTP Verification**: Secure login and password recovery via Email OTP.
- **Vault Toggle**: Interactive password visibility toggles for secure entry.

## 🚀 Getting Started

1.  **Prerequisites**:
    - .NET SDK.
    - .NET MAUI workload (`dotnet workload install maui`).

2.  **Run the App**:
    - **Windows**:
      ```powershell
      dotnet run -f net10.0-windows10.0.19041.0
      ```
    - **Android**:
      ```powershell
      dotnet run -f net10.0-android
      ```

## 📂 Structure

- **Components**: Reusable UI components (Toasts, Navigation, Popups).
- **Services**: Logic for API communication, Backups, and State management.
- **Views**: Main application pages for the vault, settings, and authentication.
