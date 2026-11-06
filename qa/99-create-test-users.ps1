# =============================================================================
# LOTUS PMS V3 — SCRIPT TẠO NGƯỜI DỤNG TEST QUA API
# Chạy sau khi Backend + SQL Server đã khởi động.
# Mục đích: tạo thêm nhiều tài khoản với các vai trò khác nhau để kiểm thử phân quyền.
# =============================================================================
$ErrorActionPreference = "Stop"
$base = "http://localhost:5080/api"

Write-Host "=== LOTUS PMS V3 — TẠO NGƯỜI DỤNG TEST ===" -ForegroundColor Cyan

# 1. Đăng nhập làm CHAIN_ADMIN (admin@pms.local / Demo@12345)
Write-Host "`n[1] Đăng nhập admin..." -ForegroundColor Cyan
$loginBody = @{ email = "admin@pms.local"; password = "Demo@12345" } | ConvertTo-Json
$auth = Invoke-RestMethod "$base/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($auth.token)" }
Write-Host "   Token đã lấy: $($auth.token.Substring(0,20))..." -ForegroundColor Green

# 2. Lấy danh sách properties
$properties = @(Invoke-RestMethod "$base/properties" -Headers $headers)
Write-Host "   Có $($properties.Count) cơ sở khách sạn" -ForegroundColor Green
foreach ($p in $properties) { Write-Host "   - $($p.code): $($p.name) (Id=$($p.id))" }

# 3. Danh sách người dùng cần tạo
$usersToCreate = @(
    @{ FullName = "Lễ Tân HCMC"; Email = "frontdesk.hcmc@pms.local"; Password = "Demo@12345"; Role = "FRONT_DESK_AGENT"; PropertyCode = "HCMC-SIGNATURE" },
    @{ FullName = "Quản Lý HCMC"; Email = "manager.hcmc@pms.local"; Password = "Demo@12345"; Role = "PROPERTY_MANAGER"; PropertyCode = "HCMC-SIGNATURE" },
    @{ FullName = "Kế Toán HCMC"; Email = "accounting.hcmc@pms.local"; Password = "Demo@12345"; Role = "ACCOUNTING_AGENT"; PropertyCode = "HCMC-SIGNATURE" },
    @{ FullName = "Buồng Phòng HCMC"; Email = "housekeeping.hcmc@pms.local"; Password = "Demo@12345"; Role = "HOUSEKEEPING_AGENT"; PropertyCode = "HCMC-SIGNATURE" },
    @{ FullName = "Bảo Trì HCMC"; Email = "maintenance.hcmc@pms.local"; Password = "Demo@12345"; Role = "MAINTENANCE_AGENT"; PropertyCode = "HCMC-SIGNATURE" },
    @{ FullName = "Lễ Tân Đà Nẵng"; Email = "frontdesk.dn@pms.local"; Password = "Demo@12345"; Role = "FRONT_DESK_AGENT"; PropertyCode = "DN-RIVERSIDE" },
    @{ FullName = "Quản Lý Đà Nẵng"; Email = "manager.dn@pms.local"; Password = "Demo@12345"; Role = "PROPERTY_MANAGER"; PropertyCode = "DN-RIVERSIDE" },
    @{ FullName = "Kế Toán Đà Nẵng"; Email = "accounting.dn@pms.local"; Password = "Demo@12345"; Role = "ACCOUNTING_AGENT"; PropertyCode = "DN-RIVERSIDE" },
    @{ FullName = "Lễ Tân Nha Trang"; Email = "frontdesk.nt@pms.local"; Password = "Demo@12345"; Role = "FRONT_DESK_AGENT"; PropertyCode = "NHA-TRAM" },
    @{ FullName = "Quản Lý Nha Trang"; Email = "manager.nt@pms.local"; Password = "Demo@12345"; Role = "PROPERTY_MANAGER"; PropertyCode = "NHA-TRAM" },
    @{ FullName = "Nhân Viên Toàn Chuỗi"; Email = "staff.chain@pms.local"; Password = "Demo@12345"; Role = "CHAIN_ADMIN"; PropertyCode = $null }
)

# 4. Tạo từng người dùng
$successCount = 0
$failCount = 0

foreach ($u in $usersToCreate) {
    # Tìm propertyId từ code
    $propId = $null
    if ($u.PropertyCode) {
        $p = $properties | Where-Object { $_.code -eq $u.PropertyCode }
        if ($p) { $propId = $p[0].id }
    }

    $body = @{
        PropertyId = $propId
        FullName = $u.FullName
        Email = $u.Email
        Password = $u.Password
        Role = $u.Role
    } | ConvertTo-Json

    try {
        $result = Invoke-RestMethod "$base/users" -Method Post -Body $body -ContentType "application/json" -Headers $headers
        Write-Host "   [OK] $($u.Email) / Role=$($u.Role) / Property=$($u.PropertyCode)" -ForegroundColor Green
        $successCount++
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq 409) {
            Write-Host "   [SKIP] $($u.Email) đã tồn tại" -ForegroundColor Yellow
        }
        else {
            Write-Host "   [FAIL] $($u.Email): $($_.Exception.Message)" -ForegroundColor Red
            $failCount++
        }
    }
}

Write-Host "`n=== KẾT QUẢ: $successCount thành công, $failCount thất bại ===" -ForegroundColor Cyan
Write-Host "Danh sách tài khoản test:" -ForegroundColor Cyan
Write-Host "  admin@pms.local           / Demo@12345 / CHAIN_ADMIN (toàn chuỗi)"
Write-Host "  manager@pms.local         / Demo@12345 / PROPERTY_MANAGER (Hà Nội)"
Write-Host "  reception@pms.local       / Demo@12345 / FRONT_DESK_AGENT (Hà Nội)"
Write-Host "  frontdesk.hcmc@pms.local  / Demo@12345 / FRONT_DESK_AGENT (HCMC)"
Write-Host "  manager.hcmc@pms.local    / Demo@12345 / PROPERTY_MANAGER (HCMC)"
Write-Host "  accounting.hcmc@pms.local / Demo@12345 / ACCOUNTING_AGENT (HCMC)"
Write-Host "  frontdesk.dn@pms.local    / Demo@12345 / FRONT_DESK_AGENT (Đà Nẵng)"
Write-Host "  manager.dn@pms.local      / Demo@12345 / PROPERTY_MANAGER (Đà Nẵng)"
Write-Host "  frontdesk.nt@pms.local    / Demo@12345 / FRONT_DESK_AGENT (Nha Trang)"
Write-Host "  manager.nt@pms.local      / Demo@12345 / PROPERTY_MANAGER (Nha Trang)"