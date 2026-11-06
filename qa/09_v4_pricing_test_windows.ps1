# Windows PowerShell 5.1 / PowerShell 7
# V4 Pricing & Surcharge Formula Tests (C#)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Missing .NET SDK 10'
}

Write-Host '=== KIEM TRA 13 CA CONG THUC GIA VA PHU THU V4 (C#) ===' -ForegroundColor Cyan
Push-Location (Join-Path $root 'qa/v4_pricing_tests')
try {
    & dotnet run -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Pricing unit tests FAIL"
    }
} finally {
    Pop-Location
}

Write-Host 'PASS: 13/13 ca cong thuc gia V4 dat chuan.' -ForegroundColor Green
