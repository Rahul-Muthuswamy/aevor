# Aevor Build & Package Script
Write-Host "Building Aevor..." -ForegroundColor Cyan

# Clean previous output
if (Test-Path "publish") { Remove-Item "publish" -Recurse -Force }
if (Test-Path "dist") { Remove-Item "dist" -Recurse -Force }
New-Item -ItemType Directory -Path "dist" | Out-Null

# Restore dependencies
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore

# Run tests
Write-Host "Running tests..." -ForegroundColor Yellow
dotnet test --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
  Write-Host "Tests failed. Aborting." -ForegroundColor Red
  exit 1
}

# Publish single file executable
Write-Host "Publishing..." -ForegroundColor Yellow
dotnet publish src/aevor_ui/aevor_ui.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:PublishReadyToRun=true `
  --output publish/win-x64

if ($LASTEXITCODE -ne 0) {
  Write-Host "Publish failed." -ForegroundColor Red
  exit 1
}

Write-Host "Build complete." -ForegroundColor Green
Write-Host "Executable: publish/win-x64/Aevor.exe" -ForegroundColor Green
Write-Host ""
Write-Host "To create installer:" -ForegroundColor Cyan
Write-Host "Install Inno Setup then run:" -ForegroundColor White
Write-Host "iscc installer/aevor_setup.iss" -ForegroundColor White
