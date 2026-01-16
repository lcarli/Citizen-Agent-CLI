# Build and Package Script for CitizenAgent Setup CLI
# Usage: .\build-package.ps1

param(
    [switch]$Install,      # Install globally after build
    [switch]$Uninstall,    # Uninstall before reinstalling
    [switch]$Clean         # Clean before build
)

$ErrorActionPreference = "Stop"

# Colors
function Write-Header { param($msg) Write-Host "`n$msg" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Info { param($msg) Write-Host "[INFO] $msg" -ForegroundColor Yellow }
function Write-Err { param($msg) Write-Host "[ERROR] $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         CitizenAgent Setup CLI - Build & Package              ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

# Get script directory
$ScriptDir = $PSScriptRoot
Set-Location $ScriptDir

# Read version from csproj
$csproj = [xml](Get-Content "CitizenAgent.Setup.Cli.csproj")
$version = $csproj.Project.PropertyGroup.Version
Write-Info "Version: $version"

# Clean
if ($Clean) {
    Write-Header "Cleaning..."
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
    if (Test-Path "nupkg") { Remove-Item -Recurse -Force "nupkg" }
    Write-Success "Cleaned"
}

# Build
Write-Header "Building..."
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Err "Build failed"
    exit 1
}
Write-Success "Build completed"

# Package
Write-Header "Creating NuGet package..."
dotnet pack -c Release -o ./nupkg --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Err "Package failed"
    exit 1
}

$packagePath = Join-Path $ScriptDir "nupkg\CitizenAgent.Setup.Cli.$version.nupkg"
if (Test-Path $packagePath) {
    $size = [math]::Round((Get-Item $packagePath).Length / 1MB, 2)
    Write-Success "Package created: CitizenAgent.Setup.Cli.$version.nupkg ($size MB)"
} else {
    Write-Err "Package file not found"
    exit 1
}

# Install globally (optional)
if ($Install) {
    Write-Header "Installing globally..."
    
    if ($Uninstall) {
        Write-Info "Uninstalling existing version..."
        dotnet tool uninstall -g CitizenAgent.Setup.Cli 2>$null
    }
    
    dotnet tool install --global CitizenAgent.Setup.Cli --add-source ./nupkg --version $version
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Installed globally as 'ca-setup'"
        Write-Info "Run 'ca-setup --help' to get started"
    } else {
        Write-Err "Installation failed. Try with -Uninstall flag to remove existing version first."
    }
}

# Summary
Write-Header "Summary"
Write-Host ""
Write-Host "  Package: " -NoNewline; Write-Host "nupkg\CitizenAgent.Setup.Cli.$version.nupkg" -ForegroundColor White
Write-Host "  Binary:  " -NoNewline; Write-Host "bin\Release\net8.0\ca-setup.exe" -ForegroundColor White
Write-Host ""
Write-Host "  To install globally:" -ForegroundColor DarkGray
Write-Host "    .\build-package.ps1 -Install -Uninstall" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  To run directly:" -ForegroundColor DarkGray
Write-Host "    .\bin\Release\net8.0\ca-setup.exe all" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  To publish to NuGet.org:" -ForegroundColor DarkGray
Write-Host "    dotnet nuget push ./nupkg/CitizenAgent.Setup.Cli.$version.nupkg --api-key <KEY> --source https://api.nuget.org/v3/index.json" -ForegroundColor DarkGray
Write-Host ""
