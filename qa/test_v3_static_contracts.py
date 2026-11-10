#!/usr/bin/env python3
"""V3 source contracts. These are NOT runtime/E2E tests and cannot prove SQL behavior."""
from pathlib import Path
import json
import re
import sys

root=Path(__file__).resolve().parent.parent
backend=(root/'backend/HotelPms.Api/Program.cs').read_text(encoding='utf-8')
advanced=(root/'backend/HotelPms.Api/AdvancedEndpoints.cs').read_text(encoding='utf-8')
models=(root/'backend/HotelPms.Api/Models.cs').read_text(encoding='utf-8')
migration=(root/'sql/05-v3-qa-safety-upgrade.sql').read_text(encoding='utf-8')
angular=(root/'frontend/src/app/app.component.ts').read_text(encoding='utf-8')
advangular=(root/'frontend/src/app/advanced.component.ts').read_text(encoding='utf-8')
testapi=(root/'qa/e2e_api_sqlserver.py').read_text(encoding='utf-8')
checks={
  'SRC-01 Payment has immutable UUID idempotency key': 'IdempotencyKey { get; set; }' in models and 'ValidIdempotency(req.IdempotencyKey)' in backend,
  'SRC-02 Unique filtered SQL index for payment request': 'CREATE UNIQUE INDEX IX_payments_idempotency_key' in migration and 'HasFilter(NotNullFilter("idempotency_key"))' in models and '$"{column} IS NOT NULL"' in models and '$"[{column}] IS NOT NULL"' in models,
  'SRC-03 Payment lock before sum and insert': backend.index('await LockReservation(db, reservationId)') < backend.index('var total = await db.Charges.Where(x => x.ReservationId == reservationId)'),
  'SRC-04 Refund writes negative signed ledger entry': 'Amount = -req.Amount' in backend and 'Kind = "REFUND"' in backend,
  'SRC-05 Cancel checks outstanding deposits': 'if (paid != 0) return Ops.Conflict("Booking còn tiền cọc' in backend,
  'SRC-06 Restore checks overlapping confirmed reservations': 'x.CheckInDate < r.CheckOutDate && x.CheckOutDate > r.CheckInDate' in advanced,
  'SRC-07 Restore checks out-of-order room': 'room == null || room.IsOutOfOrder' in advanced,
  'SRC-08 Reversed debt can be reused for new checkout': 'debt.Status != "REVERSED"' in advanced,
  'SRC-09 One actual occupant list per booking': 'DbSet<ReservationOccupant> Occupants' in models and '/reservations/{id:int}/occupants' in advanced,
  'SRC-10 Occupant migration preserves existing DB': "IF OBJECT_ID('dbo.reservation_occupants', 'U') IS NULL" in migration,
  'SRC-11 Room is locked before booking conflict check': 'LockRoom(db, req.RoomId)' in backend and 'LockRoom(db, roomId)' in advanced,
  'SRC-12 Inventory lock used in sales and stock adjustments': advanced.count('LockService(db, req.ServiceItemId)') >= 2 and 'LockService(db, req.ServiceId)' in backend,
  'SRC-13 Angular creates UUIDs for payment requests': angular.count('idempotencyKey: crypto.randomUUID()') >= 3,
  'SRC-14 Angular group payments carry UUIDs': 'idempotencyKey: crypto.randomUUID()' in advangular,
  'SRC-15 E2E script includes two-request races': 'ThreadPoolExecutor(max_workers=2)' in testapi and 'V3-12' in testapi and 'V3-14' in testapi,
  'SRC-16 No direct claim that offline preview tests API': 'NOT offline preview' in (root/'qa/e2e_angular_browser.py').read_text(encoding='utf-8'),
}
results=[{'id':code.split(' ')[0],'title':code,'status':'PASS' if ok else 'FAIL'} for code,ok in checks.items()]
print(json.dumps(results,ensure_ascii=False,indent=2))
(root/'qa/v3_source_contract_results.json').write_text(json.dumps({'scope':'STATIC_CODE_ONLY_NOT_RUNTIME','cases':results},ensure_ascii=False,indent=2),encoding='utf-8')
sys.exit(0 if all(checks.values()) else 1)
