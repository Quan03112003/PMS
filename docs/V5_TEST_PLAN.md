# LOTUS PMS V5 — KẾ HOẢN KIỂM THỬ (TEST PLAN)

**Ngày:** 06/11/2026
**Mục đích:** Đảm bảo V5 sẵn sàng production

---

## 1. KIẾN TRÚC KIỂM THỬ

```
┌─────────────────────────────────────────────────────────────┐
│  GIAI ĐOẠN 1: STATIC CONTRACT (Python)                      │
│  15 test cases → test_v5_static_contracts.py                │
│  Kiểm tra: endpoints, API wiring, Angular references        │
└──────────────────────────┬──────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  GIAI ĐOẠN 2: C# UNIT TEST (Pricing & Tax)                  │
│  10 test cases → qa/v5_unit_tests/Program.cs                │
│  Kiểm tra: pricing, tax, timezone, adjustment               │
└──────────────────────────┬──────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  GIAI ĐOẠN 3: API E2E (Python + requests)                   │
│  10 test cases → qa/v5_api_e2e.py                           │
│  Kiểm tra: login, CRUD, financial, audit                    │
└──────────────────────────┬──────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  GIAI ĐOẠN 4: ANGULAR E2E (Playwright)                      │
│  6 test cases → qa/v5_angular_e2e.py                         │
│  Kiểm tra: UI, user flow, business logic                    │
└──────────────────────────┬──────────────────────────────────┘
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  GIAI ĐOẠN 5: PERFORMANCE & SECURITY                        │
│  Load test 50 users, Pen test OWASP Top 10                  │
│  Backup/restore demo DB                                     │
└──────────────────────────┬──────────────────────────────────┘
                           ▼
                           ▼
              GATE V5 PASSED → PRODUCTION READY
```

---

## 2. CHECKLIST TIẾP THEO (V4 → V5)

| Gate | Hành động | Output |
|------|-----------|--------|
| G0 | Chạy `dotnet build -c Release` | PASS/FAIL |
| G1 | Chạy `npm run build` | PASS/FAIL |
| G2 | Chạy `npm run test:types` | PASS/FAIL |
| G3 | Chạy `test_v5_static_contracts.py` | 15/15 PASS |
| G4 | Chạy `v5_unit_tests/Program.cs` | 10/10 PASS |
| G5 | Chạy `v5_api_e2e.py --confirm-demo` | 10/10 PASS |
| G6 | Chạy `v5_angular_e2e.py --confirm-demo` | 6/6 PASS |
| G7 | Backup DB + restore test | PASS/FAIL |
| G8 | Load test | < 2s @ 50 users |
| G9 | Pen test | 0 OWASP High |
| G10 | E2E acceptance | ALL PASS |

---

## 3. CHI TIẾT HÀNH TRÌNH

### 3.1. Static Contract Tests (20 phút)

```powershell
# 1. Run Python test
py qa/test_v5_static_contracts.py

# 2. Expected output: 15 PASS, exit code 0
# 3. Nếu FAIL, fix code then re-run
```

### 3.2. C# Unit Tests (15 phút)

```powershell
# 1. Go to v5_unit_tests folder
cd qa\v5_unit_tests

# 2. Run unit tests
dotnet run -c Release

# 3. Expected output: 10/10 PASS
# 4. Nếu FAIL, fix then re-run
```

### 3.3. API E2E Tests (60 phút)

```powershell
# 1. Start backend (02_START_BACKEND.bat)
# 2. Start frontend (03_START_ANGULAR.bat) - optional for full test
# 3. Run E2E tests
py qa\v5_api_e2e.py --confirm-demo --out qa/v5_e2e_results.json

# 4. Expected: 10 PASS, exit code 0
# 5. Review results: qa/v5_e2e_results.json
```

### 3.4. Angular E2E Tests (60 phút)

```powershell
# 1. Start backend + frontend
# 2. Install Playwright if needed
pip install playwright
playwright install chromium

# 3. Run E2E
py qa\v5_angular_e2e.py --confirm-demo --out qa/v5_angular_e2e_results.json

# 4. Expected: 6 PASS, exit code 0
```

### 3.5. Performance & Security (60 phút)

```powershell
# Load test with k6 (install: winget install k6)
k6 run --vus 50 --duration 5m qa/load_test.js

# Expected: p(95) < 2s
```

### 3.6. Backup/Restore (10 phút)

```powershell
# Backup
sqlcmd -S localhost -d HotelChainPms -Q "BACKUP DATABASE HotelChainPms TO DISK = 'C:\backup\pms_v5_backup.bak'"

# Restore
sqlcmd -S localhost -d master -Q "RESTORE DATABASE HotelChainPms FROM DISK = 'C:\backup\pms_v5_backup.bak' WITH REPLACE"
```

---

## 4. TỔNG HỢP KẾT QUẢ

| Gate | Kiểm tra | Thời gian | Output |
|------|----------|-----------|--------|
| G0-G3 | Build & Static | 45 phút | `qa/v5_build_results.json` |
| G4-G5 | Unit tests | 15 phút | `qa/v5_unit_results.json` |
| G6 | API E2E | 60 phút | `qa/v5_e2e_results.json` |
| G7 | Angular E2E | 60 phút | `qa/v5_angular_e2e_results.json` |
| G8-G10 | Performance/Security | 90 phút | `qa/v5_perf_results.json` |

**Tổng thời gian:** 3h30 phút
**Môi trường test:** Windows + PowerShell + Python + Node.js + Docker
## 7. NOTE

- V5 chỉ được coi là production-ready khi **10 gates tất cả PASS**
- Nếu bất kỳ gate nào FAIL, phải fix và re-run
- Đăng ký test time trên phần mềm quản lý (Jira/Confluence)
- Ghi nhận results vào `qa/evidence/` sau mỗi gate