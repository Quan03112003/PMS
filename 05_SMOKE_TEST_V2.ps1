# Run AFTER database + backend are up. Demo accounts/credentials only.
$ErrorActionPreference = "Stop"
$base = "http://localhost:5080/api"
try {
  $health = Invoke-RestMethod "$base/health"
  Write-Host "Health: $($health.status)" -ForegroundColor Green
  $login = Invoke-RestMethod "$base/auth/login" -Method POST -ContentType "application/json" -Body (@{email="admin@pms.local";password="Demo@12345"} | ConvertTo-Json)
  $h = @{ Authorization = "Bearer $($login.token)" }
  $props = Invoke-RestMethod "$base/properties" -Headers $h
  $id = $props[0].id
  $rates = @(Invoke-RestMethod "$base/rate-plans?propertyId=$id" -Headers $h)
  $buildings = @(Invoke-RestMethod "$base/buildings?propertyId=$id" -Headers $h)
  $suppliers = @(Invoke-RestMethod "$base/suppliers?propertyId=$id" -Headers $h)
  $stock = @(Invoke-RestMethod "$base/stock/movements?propertyId=$id" -Headers $h)
  $debts = @(Invoke-RestMethod "$base/debts?propertyId=$id" -Headers $h)
  $today = (Get-Date).ToString('yyyy-MM-dd')
  $report = Invoke-RestMethod "$base/reports/chain?from=$today&to=$today" -Headers $h
  Write-Host "PASS: hotel=$id rates=$($rates.Count) buildings=$($buildings.Count) suppliers=$($suppliers.Count) ledger=$($stock.Count) debts=$($debts.Count) report-properties=$(@($report.properties).Count)" -ForegroundColor Green
  Write-Host "For write actions, follow docs/V2_QC_SCENARIOS.md and verify in DBeaver."
} catch {
  Write-Host "SMOKE FAILED: $($_.Exception.Message)" -ForegroundColor Red
  exit 1
}
