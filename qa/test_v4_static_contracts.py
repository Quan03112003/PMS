#!/usr/bin/env python3
"""V4 static contracts for Pricing & Night Audit Precheck."""
from pathlib import Path
import json
import sys

root = Path(__file__).resolve().parent.parent
backend = (root / 'backend/HotelPms.Api/Program.cs').read_text(encoding='utf-8')
pricing = (root / 'backend/HotelPms.Api/PricingV4.cs').read_text(encoding='utf-8')
night_audit = (root / 'backend/HotelPms.Api/NightAuditV4.cs').read_text(encoding='utf-8')
angular_app = (root / 'frontend/src/app/app.component.ts').read_text(encoding='utf-8')
angular_adv = (root / 'frontend/src/app/advanced.component.ts').read_text(encoding='utf-8')
angular_html = (root / 'frontend/src/app/advanced.component.html').read_text(encoding='utf-8')

checks = {
    'V4-01 Pricing module wired in Program.cs': 'app.MapPricingV4()' in backend,
    'V4-02 Night audit precheck wired in Program.cs': 'app.MapNightAuditV4()' in backend,
    'V4-03 Pricing quote endpoint defined': 'api.MapPost("/pricing/quote"' in pricing,
    'V4-04 Pricing adjustment endpoint defined': 'api.MapPost("/stays/{stayId:int}/pricing-adjustments"' in pricing,
    'V4-05 Night audit precheck endpoint defined': 'api.MapGet("/night-audit/precheck"' in night_audit,
    'V4-06 Pricing adjustment uses row lock on reservation': 'await Ops.LockReservation(db, res.Id)' in pricing,
    'V4-07 Pricing adjustment prevents duplicate category for reservation': 'c.ReservationId == res.Id && c.Category == kind' in pricing,
    'V4-08 Pricing adjustment restricted to managers': 'Ops.CanManage(actor)' in pricing,
    'V4-09 Early check-in validates <= 15m from actual stay check-in': 'Math.Abs((req.ActualAtLocal - stayCheckInLocal).TotalMinutes) > 15' in pricing,
    'V4-10 Future time limited to 15m in advance': 'req.ActualAtLocal > nowLocal.AddMinutes(15)' in pricing,
    'V4-11 Angular menu includes pricingQuote': "'pricingQuote'" in angular_app and 'Báo giá & phụ thu' in angular_app,
    'V4-12 Angular calls pricing quote API': "this.api.post('/pricing/quote'" in angular_adv,
    'V4-13 Angular calls pricing adjustment API': '/pricing-adjustments' in angular_adv,
    'V4-14 Angular calls night audit precheck API': '/night-audit/precheck' in angular_adv,
    'V4-15 Angular template renders pricingQuote screen': "view === 'pricingQuote'" in angular_html,
    'V4-16 Angular template renders dayclose precheck panel': "precheckResult" in angular_html,
}

results = [{'id': code.split(' ')[0], 'title': code, 'status': 'PASS' if ok else 'FAIL'} for code, ok in checks.items()]
print(json.dumps(results, ensure_ascii=False, indent=2))
sys.exit(0 if all(checks.values()) else 1)
