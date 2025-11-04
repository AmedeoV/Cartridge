# Set Android SDK Environment Variable
$androidSdkPath = "$env:LOCALAPPDATA\Android\Sdk"

Write-Host "Setting ANDROID_HOME to: $androidSdkPath" -ForegroundColor Green

# Set for current session
$env:ANDROID_HOME = $androidSdkPath
$env:ANDROID_SDK_ROOT = $androidSdkPath

# Add to PATH for current session
$env:PATH = "$androidSdkPath\platform-tools;$androidSdkPath\tools;$androidSdkPath\emulator;$env:PATH"

Write-Host "✓ ANDROID_HOME set successfully!" -ForegroundColor Green
Write-Host "✓ Android SDK tools added to PATH!" -ForegroundColor Green
Write-Host ""
Write-Host "To make this permanent, run the following command in PowerShell as Administrator:" -ForegroundColor Yellow
Write-Host '[System.Environment]::SetEnvironmentVariable("ANDROID_HOME", "' + $androidSdkPath + '", "User")' -ForegroundColor Cyan
Write-Host ""
Write-Host "Verifying SDK location..." -ForegroundColor Yellow

if (Test-Path $androidSdkPath) {
    Write-Host "✓ Android SDK found at: $androidSdkPath" -ForegroundColor Green

    # Check for essential tools
    $platformTools = Join-Path $androidSdkPath "platform-tools\adb.exe"
    if (Test-Path $platformTools) {
        Write-Host "✓ ADB found!" -ForegroundColor Green
    } else {
        Write-Host "⚠ ADB not found. You may need to install Android SDK Platform-Tools." -ForegroundColor Yellow
    }
} else {
    Write-Host "✗ Android SDK not found!" -ForegroundColor Red
}

Write-Host ""
Write-Host "Environment configured for current session. You can now build the app!" -ForegroundColor Green
Write-Host "Run: dotnet build -f net9.0-android" -ForegroundColor Cyan

