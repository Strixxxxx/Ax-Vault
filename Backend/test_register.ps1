$ErrorActionPreference = "Stop"

function Test-Registration {
    param (
        [string]$Username,
        [string]$Email,
        [string]$Password = "Password123!",
        [string]$UniqueKey = "SecretKey123!"
    )

    $url = "http://localhost:5180/api/auth/register"
    
    $body = @{
        Username = $Username
        Email = $Email
        Password = $Password
        UniqueKey = $UniqueKey
        Timezone = "UTC"
    } | ConvertTo-Json

    # Add the required secret header to bypass AppVerifierMiddleware
    $headers = @{
        "X-Frontend-Secret" = "Ax_Vault_Secret_Key"
    }

    Write-Host "Registering user: $Username"
    
    try {
        $response = Invoke-RestMethod -Uri $url -Method Post -Body $body -Headers $headers -ContentType "application/json" -ErrorAction Stop
        Write-Host "✅ Registration Successful!" -ForegroundColor Green
        Write-Host $response
    }
    catch {
        Write-Host "❌ Registration Failed!" -ForegroundColor Red
        $errorResponse = $_.Exception.Response
        if ($errorResponse) {
             $reader = New-Object System.IO.StreamReader($errorResponse.GetResponseStream())
             $responseText = $reader.ReadToEnd()
             Write-Host "Status Code: $($errorResponse.StatusCode)"
             Write-Host "Error Details: $responseText" -ForegroundColor Yellow
        } else {
             Write-Host "Error: $($_.Exception.Message)"
        }
    }
}

# 1. Test with a FIXED debug user (so test_routeguard.ps1 works consistently)
Test-Registration -Username "TestUser_Debug" -Email "test_debug@example.com"

# 2. Instructions for user
Write-Host "`n*** NOW, PLEASE EDIT THIS SCRIPT TO USE YOUR PROBLEMATIC USERNAME/EMAIL OR RUN IT MANUALLY ***" -ForegroundColor Cyan
