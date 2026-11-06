# Windows PowerShell 5.1 / PowerShell 7
# V4 Pricing & Precheck Read-only Smoke Test
param(
    [string]$BaseUrl = "http://localhost:5080/api"
)

$ErrorActionPreference = 'Stop'
Write-Host "=== LOTUS PMS V4 - READ-ONLY API SMOKE TEST ===" -ForegroundColor Cyan

# 1. Health check
Write-Host "1. Kiem tra /api/health..."
$health = Invoke-RestMethod "$BaseUrl/health" -Method Get
if ($health.status -ne "ok") {
    throw "API health is not OK"
}
Write-Host "   OK: version=$($health.version)" -ForegroundColor Green

# 2. Login reception
Write-Host "2. Dang nhap le tan reception@pms.local..."
$loginBody = @{ Email = "reception@pms.local"; Password = "Demo@12345" } | ConvertTo-Json
$auth = Invoke-RestMethod "$BaseUrl/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $auth.token
if (-not $token) { throw "Dang nhap that bai, khong co token" }
$headers = @{ Authorization = "Bearer $token" }
Write-Host "   OK: token nhan thanh cong" -ForegroundColor Green

# 3. Get Property and Rate Plans
Write-Host "3. Lay danh muc co so va bang gia..."
$props = Invoke-RestMethod "$BaseUrl/properties" -Method Get -Headers $headers
if ($props.Count -eq 0) { throw "Khong co co so nao" }
$propId = $props[0].id
$plans = Invoke-RestMethod "$BaseUrl/rate-plans?propertyId=$propId" -Method Get -Headers $headers
if ($plans.Count -eq 0) { throw "Khong co bang gia nao" }
$overnightPlan = $plans | Where-Object { $_.mode -eq "OVERNIGHT" } | Select-Object -First 1
if (-not $overnightPlan) { $overnightPlan = $plans[0] }
Write-Host "   OK: Chon co so ID=$propId, Bang gia $($overnightPlan.code) ($($overnightPlan.mode))" -ForegroundColor Green

# 4. POST /api/pricing/quote (Overnight test)
Write-Host "4. Goi POST /api/pricing/quote (Bao gia qua dem)..."
$quoteBody = @{
    propertyId = $propId
    ratePlanId = $overnightPlan.id
    checkInDate = "2026-11-10"
    checkOutDate = "2026-11-12"
    actualCheckInLocal = "2026-11-10T12:00:00"
    actualCheckOutLocal = "2026-11-12T13:30:00"
} | ConvertTo-Json

$quote = Invoke-RestMethod "$BaseUrl/pricing/quote" -Method Post -Headers $headers -Body $quoteBody -ContentType "application/json"
if ($quote.nights -ne 2) { throw "So dem tinh sai: $($quote.nights)" }
if ($quote.earlyHours -ne 2) { throw "Gio nhan som tinh sai: $($quote.earlyHours)" }
if ($quote.lateHours -ne 2) { throw "Gio tra muon tinh sai: $($quote.lateHours)" }
Write-Host "   OK: Tong tien bao gia = $($quote.totalAmount) (Nights=$($quote.nights), EarlyHours=$($quote.earlyHours), LateHours=$($quote.lateHours))" -ForegroundColor Green

# 5. GET /api/night-audit/precheck
Write-Host "5. Goi GET /api/night-audit/precheck..."
$precheck = Invoke-RestMethod "$BaseUrl/night-audit/precheck?propertyId=$propId&businessDate=2026-11-10" -Method Get -Headers $headers
Write-Host "   OK: canClose=$($precheck.canClose), openShifts=$($precheck.openShiftsCount), pendingArrivals=$($precheck.pendingArrivalsCount), pendingDepartures=$($precheck.pendingDeparturesCount)" -ForegroundColor Green
if ($precheck.warnings.Count -gt 0) {
    Write-Host "   Canh bao truoc chot: $($precheck.warnings -join ' | ')" -ForegroundColor Yellow
}

Write-Host "PASS: V4 Read-only API smoke test hoan tat thanh cong." -ForegroundColor Green
