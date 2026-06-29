# Lumos Desktop — Publish & Package Script
# Builds a self-contained release and creates a distributable package.
#
# Usage:
#   .\scripts\publish.ps1              # Default: publish to ./artifacts/
#   .\scripts\publish.ps1 -Version 1.0.0 -OutputDir D:\releases

param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = "",
    [switch]$CreateZip,
    [switch]$CreateInstaller
)

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$SolutionPath = Join-Path $ProjectRoot "src\Lumos.sln"
$DesktopProject = Join-Path $ProjectRoot "src\Lumos.Desktop\Lumos.Desktop.csproj"

if (-not $OutputDir) {
    $OutputDir = Join-Path $ProjectRoot "artifacts"
}

$PublishDir = Join-Path $OutputDir "lumos-$Version-win-x64"
$DotNet = "dotnet"

Write-Host "=== Lumos Desktop v$Version Publish ===" -ForegroundColor Cyan
Write-Host "Project root: $ProjectRoot"
Write-Host "Output: $PublishDir"

# ── Step 1: Restore ─────────────────────────────────────────────────────
Write-Host "`n[1/5] Restoring packages..." -ForegroundColor Yellow
& $DotNet restore $SolutionPath
if ($LASTEXITCODE -ne 0) { Write-Host "Restore failed!" -ForegroundColor Red; exit 1 }

# ── Step 2: Build ───────────────────────────────────────────────────────
Write-Host "`n[2/5] Building solution..." -ForegroundColor Yellow
& $DotNet build $SolutionPath -c Release
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }

# ── Step 3: Run tests ──────────────────────────────────────────────────
Write-Host "`n[3/5] Running tests..." -ForegroundColor Yellow
& $DotNet test $SolutionPath -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Host "Tests failed!" -ForegroundColor Red; exit 1 }

# ── Step 4: Publish self-contained ─────────────────────────────────────
Write-Host "`n[4/5] Publishing self-contained release..." -ForegroundColor Yellow
Remove-Item -Path $PublishDir -Recurse -ErrorAction SilentlyContinue

& $DotNet publish $DesktopProject `
    -c Release `
    -o $PublishDir `
    --self-contained true `
    -r win-x64 `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded

if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed!" -ForegroundColor Red; exit 1 }

# ── Step 5: Copy appcast.xml ─────────────────────────────────────────
if ($CreateInstaller) {
    Write-Host "`n[5/7] Copying appcast.xml..." -ForegroundColor Yellow
    Copy-Item (Join-Path $ProjectRoot "appcast.xml") -Destination $PublishDir -Force
}

# ── Step 6: Create installer (Inno Setup) ──────────────────────────────
if ($CreateInstaller) {
    Write-Host "`n[6/7] Creating Inno Setup installer..." -ForegroundColor Yellow
    $issPath = Join-Path $ProjectRoot "scripts\installer.iss"
    $isccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    
    if (Test-Path $isccPath) {
        & $isccPath $issPath
        Write-Host "Installer created in artifacts/" -ForegroundColor Green
    } else {
        Write-Host "Inno Setup not found at $isccPath" -ForegroundColor Yellow
        Write-Host "Download from https://jrsoftware.org/isdl.php and install with default options." -ForegroundColor Yellow
        Write-Host "Then re-run with -CreateInstaller" -ForegroundColor Yellow
    }
}

# ── Step 7: Create ZIP (optional) ──────────────────────────────────────
if ($CreateZip) {
    Write-Host "`n[7/7] Creating ZIP archive..." -ForegroundColor Yellow
    $zipPath = Join-Path $OutputDir "lumos-$Version-win-x64.zip"
    Compress-Archive -Path "$PublishDir\*" -DestinationPath $zipPath -Force
    Write-Host "ZIP archive: $zipPath" -ForegroundColor Green
}

Write-Host "`n=== Publish complete ===" -ForegroundColor Green
Write-Host "Published to: $PublishDir"
Write-Host ""
Write-Host "To create a redistributable package, run:" -ForegroundColor Gray
Write-Host "  .\scripts\publish.ps1 -CreateZip" -ForegroundColor Gray
Write-Host "  .\scripts\publish.ps1 -CreateInstaller    # requires Inno Setup" -ForegroundColor Gray
Write-Host ""
Write-Host "To run the published version:" -ForegroundColor Gray
Write-Host "  $PublishDir\Lumos.Desktop.exe" -ForegroundColor Gray
