$ErrorActionPreference = "Stop"

# Configuration
$BaseUrl = "http://localhost:5180"
$BackendSecretKey = "Ax_Vault_Secret_Key" # From your earlier .env check

# Test Credentials (from the previous successful registration)
$Username = "TestUser_Debug"
$Password = "Password123!"
$UniqueKey = "SecretKey123!" 

# Headers with the Frontend Secret
$CommonHeaders = @{
    "X-Frontend-Secret" = $BackendSecretKey
    "Content-Type"      = "application/json"
}

function Test-RouteGuard {
    Write-Host "`n=== 1. Login to get JWT Token ===" -ForegroundColor Cyan
    
    $loginBody = @{
        Username = $Username
        Password = $Password
    } | ConvertTo-Json

    try {
        $loginResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -Body $loginBody -Headers $CommonHeaders
        $token = $loginResponse.Token
        Write-Host "✅ Login Successful! Token retrieved." -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Login Failed: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
             $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
             Write-Host $reader.ReadToEnd() -ForegroundColor Yellow
        }
        return
    }

    Write-Host "`n=== 2. Validate Unique Key ===" -ForegroundColor Cyan
    
    # Add Authorization header
    $authHeaders = $CommonHeaders.Clone()
    $authHeaders["Authorization"] = "Bearer $token"

    $validateBody = @{
        TargetModule = "TestModule"
        UniqueKey    = $UniqueKey
    } | ConvertTo-Json

    Write-Host "Sending UniqueKey: '$UniqueKey'"

    try {
        $validateResponse = Invoke-RestMethod -Uri "$BaseUrl/api/RouteGuard/validate" -Method Post -Body $validateBody -Headers $authHeaders
        
        if ($validateResponse.isAuthorized -eq $true) {
            Write-Host "✅ VALIDATION SUCCESSFUL!" -ForegroundColor Green
            Write-Host "Message: $($validateResponse.message)"
        }
        else {
            Write-Host "❌ VALIDATION FAILED (200 OK but Authorized=False)" -ForegroundColor Red
            Write-Host "Message: $($validateResponse.message)"
        }
    }
    catch {
        Write-Host "❌ VALIDATION FAILED (HTTP Error)" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)"
        if ($_.Exception.Response) {
             $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
             Write-Host $reader.ReadToEnd() -ForegroundColor Yellow
        }
    }
}

Test-RouteGuard
