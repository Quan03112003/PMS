# Run only after SQL Server + ASP.NET Core API are running.
$ErrorActionPreference = "Stop"
$base = "http://localhost:5080/api"
Write-Host "1) Health check" -ForegroundColor Cyan
$health = Invoke-RestMethod "$base/health"
if ($health.status -ne "ok") { throw "API health failed" }
Write-Host "2) Login as receptionist" -ForegroundColor Cyan
$body = @{ email = 'reception@pms.local'; password = 'Demo@12345' } | ConvertTo-Json
$auth = Invoke-RestMethod "$base/auth/login" -Method Post -Body $body -ContentType 'application/json'
$headers = @{ Authorization = "Bearer $($auth.token)" }
Write-Host "3) Read authorized properties" -ForegroundColor Cyan
$properties = @(Invoke-RestMethod "$base/properties" -Headers $headers)
if ($properties.Count -ne 1) { throw "Expected receptionist to have exactly one property, got $($properties.Count)" }
$id = $properties[0].id
Write-Host "4) Read rooms and reservations" -ForegroundColor Cyan
$rooms = @(Invoke-RestMethod "$base/rooms?propertyId=$id" -Headers $headers)
$booking = @(Invoke-RestMethod "$base/reservations?propertyId=$id" -Headers $headers)
Write-Host "OK: API=$($health.status), propertyId=$id, rooms=$($rooms.Count), bookings=$($booking.Count)" -ForegroundColor Green
