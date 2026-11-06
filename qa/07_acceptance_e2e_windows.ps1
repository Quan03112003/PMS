# Chạy CHỈ SAU KHI backend + Angular + SQL Server đã khởi động trên localhost DEMO.
# Nguy hiểm: test tạo booking, payment, checkout và thay đổi tồn kho.
param([switch]$ConfirmDemo)
$ErrorActionPreference = 'Stop'
if (-not $ConfirmDemo) { throw 'Chỉ chạy trên database DEMO. Chạy lại với -ConfirmDemo' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Get-Command py -ErrorAction SilentlyContinue)) { throw 'Thiếu Python py launcher (3.10+)' }
Write-Host '=== HEALTH ==='
$health = Invoke-RestMethod 'http://localhost:5080/api/health' -TimeoutSec 15
if ($health.status -ne 'ok') { throw "API health không OK" }
Write-Host '=== STATIC CONTRACT ==='
& py (Join-Path $root 'qa/test_v3_static_contracts.py')
if ($LASTEXITCODE -ne 0) { throw 'Static contracts V3 FAIL' }
& py (Join-Path $root 'qa/test_v4_static_contracts.py')
if ($LASTEXITCODE -ne 0) { throw 'Static contracts V4 FAIL' }
Write-Host '=== API + SQL SERVER E2E ==='
& py (Join-Path $root 'qa/e2e_api_sqlserver.py') --confirm-demo --out (Join-Path $root 'qa/pms_v3_api_e2e_results.json')
if ($LASTEXITCODE -ne 0) { throw 'API + SQL Server E2E FAIL/BLOCKED' }
Write-Host '=== ANGULAR + API + SQL REAL BROWSER E2E ==='
& py (Join-Path $root 'qa/e2e_angular_browser.py') --confirm-demo
if ($LASTEXITCODE -ne 0) { throw 'Angular E2E FAIL/BLOCKED' }
Write-Host 'PASS TEST GATE: API/DB + real browser' -ForegroundColor Green
