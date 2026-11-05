# Chạy trên Windows PowerShell 5.1+ / PowerShell 7, chỉ thao tác BUILD (không ghi SQL).
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Thiếu .NET SDK 10: https://dotnet.microsoft.com/download' }
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { throw 'Thiếu Node.js 22 / npm' }
Write-Host '=== BACKEND ==='
Push-Location (Join-Path $root 'backend/HotelPms.Api')
try {
  & dotnet restore
  if ($LASTEXITCODE -ne 0) { throw "dotnet restore FAIL ($LASTEXITCODE)" }
  & dotnet build -c Release --no-restore
  if ($LASTEXITCODE -ne 0) { throw "dotnet build FAIL ($LASTEXITCODE)" }
} finally { Pop-Location }
Write-Host '=== FRONTEND ==='
Push-Location (Join-Path $root 'frontend')
try {
  & npm install --no-audit --no-fund
  if ($LASTEXITCODE -ne 0) { throw "npm install FAIL ($LASTEXITCODE)" }
  & npm run build
  if ($LASTEXITCODE -ne 0) { throw "Angular build FAIL ($LASTEXITCODE)" }
  & npm run test:types
  if ($LASTEXITCODE -ne 0) { throw "Typecheck FAIL ($LASTEXITCODE)" }
} finally { Pop-Location }
Write-Host 'PASS BUILD GATE: backend + Angular production + typecheck' -ForegroundColor Green
