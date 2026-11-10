#!/usr/bin/env python3
"""V5 foundation static contracts.

Source-level checks only: they prove routes and safety rules exist in code, not that they work at runtime.
Runtime evidence comes from the CI postgres-smoke job and API/E2E tests on staging.
"""
from pathlib import Path
import json
import re
import sys

root = Path(__file__).resolve().parent.parent
read = lambda rel: (root / rel).read_text(encoding='utf-8')
program = read('backend/HotelPms.Api/Program.cs')
models = read('backend/HotelPms.Api/Models.cs')
pricing = read('backend/HotelPms.Api/PricingV4.cs')
v5 = read('backend/HotelPms.Api/V5Foundation.cs')
render = read('render.yaml')
upgrade = read('sql/06-v5-foundation-postgres.sql')
angular_adv = read('frontend/src/app/advanced.component.ts')

text_suffixes = {'.cs', '.ts', '.html', '.sql', '.py', '.md', '.json', '.yaml', '.yml', '.ps1', '.bat', '.http'}
scanned = [p for p in root.glob('*') if p.is_file()] + [
    p for folder in ('backend', 'frontend/src', 'sql', 'qa', 'docs', '.github') for p in (root / folder).rglob('*')]
tracked_text = '\n'.join(
    p.read_text(encoding='utf-8', errors='ignore') for p in scanned
    if p.is_file() and p.suffix in text_suffixes and 'bin' not in p.parts and 'obj' not in p.parts
)
secret_pattern = re.compile(r'ep-[a-z0-9-]+\.[a-z0-9.-]*neon\.tech|postgres(?:ql)?://[^:\s"<>]+:[^@\s"<>]+@|npg_[A-Za-z0-9]{8,}')


def section(route: str) -> str:
    start = v5.find(route)
    if start < 0:
        return ''
    end = v5.find('api.Map', start + len(route))
    return v5[start:end if end > 0 else len(v5)]


checks = {
    # V4 continuity
    'V5-01 V4 pricing and night audit modules still wired': 'app.MapPricingV4()' in program and 'app.MapNightAuditV4()' in program,
    'V5-02 V4 pricing quote endpoint unchanged': 'api.MapPost("/pricing/quote"' in pricing,
    'V5-03 V5 foundation module wired': 'app.MapV5Foundation()' in program,

    # Platform safety (Phase 0)
    'V5-04 Production does not seed demo data on restart': re.search(r'Database__InitializeForDemo\s*\n\s*value:\s*"false"', render) is not None,
    'V5-05 Render health check uses readiness endpoint': 'healthCheckPath: /api/health/ready' in render,
    'V5-06 Readiness endpoint queries the database': all(s in program for s in ('/api/health/ready', 'ExecuteSqlRawAsync', 'Status503ServiceUnavailable')),
    'V5-07 Payment idempotency filter is provider aware': 'HasFilter(NotNullFilter("idempotency_key"))' in models,
    'V5-08 Upgrade script is idempotent (IF NOT EXISTS)': upgrade.count('CREATE TABLE IF NOT EXISTS') == 6 and 'ADD COLUMN IF NOT EXISTS' in upgrade,
    'V5-09 Upgrade script has no destructive statements': re.search(r'\b(DROP\s+TABLE|DROP\s+COLUMN|TRUNCATE|DELETE\s+FROM)\b', upgrade, re.I) is None,
    'V5-10 No Neon host or credential committed': secret_pattern.search(tracked_text) is None,

    # Adjustment vouchers: maker-checker, append-only ledger
    'V5-11 Voucher create/approve/reject/post routes exist': all(r in v5 for r in (
        'api.MapPost("/adjustment-vouchers"', '"/adjustment-vouchers/{id:int}/approve"',
        '"/adjustment-vouchers/{id:int}/reject"', '"/adjustment-vouchers/{id:int}/post"')),
    'V5-12 Voucher create requires UUID idempotency key': 'Ops.ValidIdempotency(req.IdempotencyKey)' in section('api.MapPost("/adjustment-vouchers"'),
    'V5-13 Voucher creator cannot approve own voucher': 'voucher.CreatedByUserId == Ops.UserId(actor)' in section('"/adjustment-vouchers/{id:int}/approve"'),
    'V5-14 Voucher post locks folio and adds a new charge': all(s in section('"/adjustment-vouchers/{id:int}/post"') for s in (
        'Ops.LockReservation', 'db.Charges.Add', 'tx.CommitAsync')),
    'V5-15 Voucher post never edits existing charges': re.search(r'db\.Charges\.(Remove|Update)', v5) is None,

    # Configuration modules
    'V5-16 Tax rule create validates rate and overlap': all(s in section('api.MapPost("/tax-rules"') for s in ('RatePercent > 100', 'chồng lấn')),
    'V5-17 Holiday rates readable and creatable': 'api.MapGet("/holiday-rates"' in v5 and 'api.MapPost("/holiday-rates"' in v5,
    'V5-18 E-invoice send does not fake issuance': '"SENT"' not in v5 and 'Ops.Conflict' in section('"/e-invoices/{id:int}/send"'),
    'V5-19 Property time zone validated as IANA id': 'TimeZoneInfo.TryFindSystemTimeZoneById' in section('"/properties/{id:int}/time-zone"'),
    'V5-20 Rate plan change only before check-in, with snapshot': all(s in section('"/reservations/{id:int}/rate-plan"') for s in (
        'reservation.Status != "CONFIRMED"', 'RatePlanSnapshot', 'Ops.LockReservation')),

    # Every V5 write checks property scope
    'V5-21 All V5 write endpoints check property scope': all(
        'Ops.HasScope' in section(r) for r in re.findall(r'(api\.Map(?:Post|Patch)\("[^"]+")', v5)),

    # Frontend
    'V5-22 Angular has no duplicate broken pricing handlers': 'adjustPricing()' not in angular_adv and 'calculatePricingQuote()' not in angular_adv,
    'V5-23 Angular success message no longer mentions SQL Server': 'cập nhật SQL Server' not in angular_adv,
}

results = [{'id': title.split(' ')[0], 'title': title, 'status': 'PASS' if ok else 'FAIL'} for title, ok in checks.items()]
print(json.dumps(results, ensure_ascii=False, indent=2))
failed = [r['title'] for r in results if r['status'] == 'FAIL']
print(f'{len(results) - len(failed)}/{len(results)} PASS', file=sys.stderr)
sys.exit(1 if failed else 0)
