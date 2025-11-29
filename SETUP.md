# Ax Vault: Full Project Setup Guide

This guide provides a complete step-by-step procedure for setting up and running the entire Ax Vault project, from the backend API to the frontend application, including the necessary networking infrastructure with NGINX and Cloudflare Tunnel.

**Technology Stack:**
-   **IDE:** Visual Studio Code
-   **Frontend:** .NET MAUI
-   **Backend:** ASP.NET Core Web API
-   **Database:** MSSQL
-   **Reverse Proxy:** NGINX
-   **Tunneling Service:** Cloudflare Tunnel

---

### **Part 1: Prerequisites & Initial Setup**

Before you begin, ensure you have the necessary tools installed.

#### **Step 1: Install .NET SDK**
The project is built using the .NET SDK.
1.  Go to the official [.NET download page](https://dotnet.microsoft.com/download/dotnet).
2.  Download and install the latest **.NET SDK** (not just the Runtime) for your operating system (e.g., Windows x64).
3.  Verify the installation by opening a terminal (PowerShell) and running `dotnet --version`.

#### **Step 2: Install .NET MAUI Workload**
This provides the templates and tools for building MAUI applications.
1.  Open your terminal and run the following command:
    ```bash
    dotnet workload install maui
    ```

#### **Step 3: Install VS Code Extensions**
For the best development experience in Visual Studio Code, install the following extensions:
1.  Open the **Extensions** view (`Ctrl+Shift+X`).
2.  Search for and install:
    -   **C# Dev Kit** (by Microsoft)
    -   **.NET MAUI** (by Microsoft)

---

### **Part 2: Backend Setup (ASP.NET Core Web API)**

These steps will get the backend server running.

1.  **Navigate to the Backend Directory:**
    Open a terminal and change to the backend folder:
    ```powershell
    cd Backend
    ```
2.  **Restore Dependencies:**
    Run the following command to download the necessary packages:
    ```powershell
    dotnet restore
    ```
3.  **Run the Backend API:**
    Start the server with this command:
    ```powershell
    dotnet run
    ```
    The API will now be running and listening at `http://localhost:5180`. Keep this terminal window open.

---

### **Part 3: NGINX Setup (Reverse Proxy)**

NGINX will act as a secure gatekeeper for our backend.

1.  **Download NGINX:**
    -   Go to the [NGINX download page](http://nginx.org/en/download.html).
    -   Download the "Mainline version" for Windows (it will be a `.zip` file).

2.  **Extract NGINX:**
    -   Create a folder named `nginx` in your C: drive.
    -   Extract the contents of the downloaded `.zip` file into `C:\nginx`.

3.  **Configure NGINX:**
    -   Navigate to `C:\nginx\conf` and open the `nginx.conf` file in a text editor.
    -   **Delete all the default text** and replace it with the configuration below. This tells NGINX to listen on port `8080` and forward valid requests to our backend on port `5180`.

    ```nginx
    # Simple NGINX configuration for reverse proxying
    worker_processes  1;

events {
    worker_connections  1024;
}

http {
    server {
        listen 8080;
        server_name localhost;

        # This is the secret key your MAUI app must send.
        # You can change this to any long, random string.
        set $expected_secret "b8e3a2f1-c4d7-4b9a-8e1f-6c3d7b5a8e0f";

        # Check for the secret header. If it's wrong or missing, block the request.
        if ($http_x_axvault_key != $expected_secret) {
            return 403; # Forbidden
        }

        # If the key is correct, forward the request to the ASP.NET Core API.
        location / {
            proxy_pass http://localhost:5180;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
    ```

4.  **Start NGINX:**
    -   Open a **new** terminal window.
    -   Navigate to your NGINX directory: `cd C:\nginx`
    -   Start the NGINX server: `start nginx`
    -   If a Windows Firewall prompt appears, you must **"Allow access"**. NGINX will run in the background.

---

### **Part 4: Cloudflare Tunnel Setup (Public Access)**

This will give us a permanent public subdomain that securely connects to our local NGINX server.

1.  **Create a Cloudflare Account:**
    -   Go to the [Cloudflare sign-up page](https://dash.cloudflare.com/sign-up) and create a **free account**.

2.  **Enter the Zero Trust Dashboard:**
    -   From the main Cloudflare page, click **"Zero Trust"** on the left sidebar.
    -   Complete the free onboarding process (you will be asked to pick a "team name").

3.  **Create the Tunnel:**
    -   In the Zero Trust dashboard, navigate to **Access -> Tunnels**.
    -   Click **"Create a tunnel"**.
    -   Choose **"Cloudflared"** as the connector type and click **"Next"**.
    -   Give your tunnel a name (e.g., `ax-vault-backend`) and click **"Save tunnel"**.

4.  **Install the Tunnel Connector:**
    -   On the next screen, select the **Windows** tab.
    -   You will see a command like `cloudflared.exe service install <YOUR_UNIQUE_TOKEN>`.
    -   **Copy this entire command** from your dashboard.
    -   Open a new PowerShell window **as an Administrator**.
    -   Paste and run the command to install the tunnel as a Windows service.

5.  **Route Traffic to NGINX:**
    -   Back in the Cloudflare dashboard, click **"Next"**.
    -   On the "Route traffic" page, create a **Public Hostname**:
        -   **Subdomain:** Enter a name for your app (e.g., `my-ax-vault`).
        -   **Domain:** Select the domain Cloudflare offers (e.g., `*.trycloudflare.com`).
        -   **Service -> Type:** Select **`HTTP`**.
        -   **Service -> URL:** Enter **`localhost:8080`**. (This points Cloudflare to your NGINX server).
    -   Click **"Save tunnel"**.

You now have a permanent public URL for your backend!

---

### **Part 5: Frontend Setup & Final Configuration**

The final step is to configure the MAUI app to use the new public URL.

1.  **Modify the ApiClient:**
    -   In VS Code, open the frontend project.
    -   Navigate to and open the file: `Frontend/Services/ApiClient.cs`.
    -   Modify the constructor to use your Cloudflare URL and add the secret key. **Remember to replace the placeholder URL with your actual Cloudflare subdomain.**

    **Change this:**
    ```csharp
    public ApiClient()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("http://localhost:5180");
    }
    ```

    **To this:**
    ```csharp
    public ApiClient()
    {
        _httpClient = new HttpClient();
        // Replace with your actual Cloudflare Tunnel URL
        _httpClient.BaseAddress = new Uri("https://my-ax-vault.trycloudflare.com"); 
        // Add the secret key to match the one in your nginx.conf
        _httpClient.DefaultRequestHeaders.Add("X-AxVault-Key", "b8e3a2f1-c4d7-4b9a-8e1f-6c3d7b5a8e0f");
    }
    ```

2.  **Run the MAUI Application:**
    -   Open a new terminal and navigate to the frontend directory: `cd Frontend`
    -   Restore dependencies: `dotnet restore`
    -   Build and run the application for Windows:
        ```powershell
        dotnet build -t:Run -f net10.0-windows10.0.19041.0
        ```

Your Ax Vault application should now launch and be fully connected to your local backend through the secure Cloudflare/NGINX infrastructure.