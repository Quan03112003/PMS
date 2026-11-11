import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app.answer import compose, trainable_rows

FACTS = {
    "module": "OTHER",
    "propertySelected": True,
    "canSeeFinance": True,
    "counts": {"inHouse": 5, "available": 4, "arrivals": 2, "departures": 0, "selling": 18, "broken": 0, "dirty": 4, "occupancy": 27.8},
    "rooms": [{"number": "201", "floor": 2, "housekeeping": "INSPECTED", "outOfOrder": False, "code": "BK-DEMO-007", "status": "CONFIRMED", "checkIn": "2026-11-11", "checkOut": "2026-11-14"}],
    "reports": [
        {"label": "Tháng này", "start": "2026-11-01", "end": "2026-11-11", "room": 14200000, "service": 15000, "noShow": 1600000, "late": 50000, "retail": 0, "outside": 680000, "expenses": 0, "folio": 15865000, "collected": 5615000, "profit": 16545000},
        {"label": "Năm nay", "start": "2026-01-01", "end": "2026-11-11", "room": 14200000, "service": 15000, "noShow": 1600000, "late": 50000, "retail": 0, "outside": 680000, "expenses": 0, "folio": 15865000, "collected": 5615000, "profit": 16545000},
    ],
    "documents": [],
    "golden": [{"question": "Làm thế nào để nhận phòng?", "expectedAnswer": "Quy trình nhận phòng đã duyệt.", "questionType": "PROCEDURE", "approvalStatus": "APPROVED"}],
}


class AnswerTests(unittest.TestCase):
    def test_splits_how_to_and_money(self):
        text = compose({**FACTS, "message": "Làm thế nào để nhận phòng? Báo cáo tháng này bao nhiêu?"})
        self.assertIn("Câu 1", text)
        self.assertIn("Câu 2", text)
        self.assertIn("Quy trình nhận phòng đã duyệt.", text)
        self.assertIn("14.200.000 ₫", text)
        self.assertIn("16.545.000 ₫", text)

    def test_front_desk_hides_money(self):
        text = compose({**FACTS, "canSeeFinance": False, "reports": [], "message": "Báo cáo tháng này bao nhiêu?"})
        self.assertIn("quyền báo cáo", text)
        self.assertNotIn("14.200.000", text)

    def test_finetune_skips_money_rows(self):
        rows = trainable_rows([
            {"approvalStatus": "APPROVED", "questionType": "PROCEDURE", "question": "Cách nhận phòng?", "expectedAnswer": "Bấm Check-in."},
            {"approvalStatus": "APPROVED", "questionType": "PROCEDURE", "question": "Doanh thu?", "expectedAnswer": "14.200.000 ₫"},
            {"approvalStatus": "DRAFT", "questionType": "PROCEDURE", "question": "Nháp", "expectedAnswer": "Chưa duyệt"},
        ])
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["answer"], "Bấm Check-in.")
