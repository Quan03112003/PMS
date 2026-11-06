#!/usr/bin/env python3
"""
V5 static contracts for Pricing, Night Audit Precheck, Adjustments, Tax, Holiday, E-Invoice, Timezone.

Cooked for LOTUS PMS V5 — production readiness.
"""
from pathlib import Path
import sys

root = Path(__file__).resolve().parent.parent
backend = (root / 'backend/HotelPms.Api/Program.cs').read_text(encoding='utf-8')
pricing = (root / 'backend/HotelPms.Api/PricingV4.cs').read_text(encoding='utf-8')
night_audit = (root / 'backend/HotelPms.Api/NightAuditV4.cs').read_text(encoding='utf-8')
angular_app = (root / 'frontend/src/app/app.component.ts').read_text(encoding='utf-8')
angular_adv = (root / 'frontend/src/app/advanced.component.ts').read_text(encoding='utf-8')
angular_html = (root / 'frontend/src/app/advanced.component.html').read_text(encoding='utf-8')

checks = {
    # V4 core wiring (must pass for V5 continuity)
    'V5-01 Pricing module wired in Program.cs': 'app.MapPricingV4()' in backend,
    'V5-02 Night audit precheck wired in Program.cs': 'app.MapNightAuditV4()' in backend,
    'V5-03 Pricing quote endpoint defined': 'api.MapPost("/pricing/quote"' in pricing,
    'V5-04 Pricing adjustment endpoint defined': 'api.MapPost("/stays/{stayId:int}/pricing-adjustments"' in pricing,
    'V5-05 Night audit precheck endpoint defined': 'api.MapGet("/night-audit/precheck"' in night_audit,

    # V5 new endpoints
    'V5-06 Adjustment voucher endpoint defined': 'api.MapPost("/adjustment-vouchers"' in pricing,
    'V5-07 Adjustment voucher approval endpoint': 'api.MapPost("/adjustment-vouchers/{id:int}/approve"' in pricing,
    'V5-08 Adjustment voucher post endpoint': 'api.MapPost("/adjustment-vouchers/{id:int}/post"' in pricing,
    'V5-09 Tax rule endpoint defined': 'api.MapPost("/tax-rules"' in pricing.replace('/stays/{stayId:int}/pricing-adjustments', '')),
    'V5-10 Holiday rate endpoint defined': 'api.MapGet("/holiday-rates"' in nightly_fallback := pricing,
    'V5-11 E-invoice endpoint defined': 'api.MapPost("/e-invoices"' in pricing,
    'V5-12 E-invoice send endpoint': 'api.MapPost("/e-invoices/{id:int}/send"' in pricing,
    'V5-13 Timezone edit endpoint defined': 'api.MapPatch("/properties/{id:int}/timezone"' in pricing,
    'V5-14 Rate plan change endpoint': 'api.MapPatch("/reservations/{id:int}/rate-plan"' in pricing,
    'V5-15 Night audit run endpoint': 'api.MapPost("/night-audit/run"' in night_audit,

    # Frontend wiring
    'V5-16 Angular menu includes pricingQuote': "'pricingQuote'" in angular_app,
    'V5-17 Angular menu includes adjustmentVouchers': "'adjustmentVouchers'" in angular_adv or "adjustment-vouchers" in angular_html,
    'V5-18 Angular menu includes nightAudit': "'nightAudit'" in angular_adv or "night-audit" in angular_html,
    'V5-19 Angular menu includes taxRules': "'taxRules'" in angular_adv or "tax-rules" in angular_html,
    'V5-20 Angular menu includes eInvoices': "'eInvoices'" in angular_adv or "/e-invoices" in angular_html,
    'V5-21 Angular calls pricing quote API': "this.api.post('/pricing/quote'" in angular_adv,
    'V5-22 Angular calls pricing adjustment API': "/pricing-adjustments" in angular_adv,
    'V5-23 Angular calls night audit precheck API': "/night-audit/precheck" in angular_adv,
    'V5-24 Angular template renders pricingQuote screen': "view === 'pricingQuote'" in angular_html,
    'V5-25 Angular template renders dayclose precheck panel': "precheckResult" in angular_html,

    # V5 RBAC (partial — static check)
    'V5-26 Adjustment voucher restricted to managers': "Ops.CanManage(actor)" in pricing and "adjustment-vouchers" in pricing,
}

results = [{'id': code.split(' ')[0], 'title': code, 'status': 'PASS' if ok else 'FAIL'} for code, ok in checks.items()]
print(json.dumps(results, ensure_ascii=False, indent=2))
sys.exit(0 if all(checks.values()) else 1)