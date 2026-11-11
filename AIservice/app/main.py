import json
import os
from pathlib import Path

from fastapi import Depends, FastAPI, Header, HTTPException
from pydantic import BaseModel, Field

from app.answer import compose, trainable_rows

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "data"
KEY = os.environ.get("AI_SERVICE_KEY", "")

app = FastAPI(title="Lotus AI Service", version="1")


class Counts(BaseModel):
    inHouse: int
    available: int
    arrivals: int
    departures: int
    selling: int
    broken: int
    dirty: int
    occupancy: float


class RoomFact(BaseModel):
    number: str
    floor: int
    housekeeping: str
    outOfOrder: bool = False
    code: str | None = None
    status: str | None = None
    checkIn: str | None = None
    checkOut: str | None = None


class ReportFact(BaseModel):
    label: str
    start: str
    end: str
    room: float
    service: float
    noShow: float
    late: float
    retail: float
    outside: float
    expenses: float
    folio: float
    collected: float
    profit: float


class DocFact(BaseModel):
    title: str = ""
    section: str = ""
    excerpt: str = ""


class GoldenFact(BaseModel):
    question: str = ""
    expectedAnswer: str = ""
    module: str = ""
    questionType: str = ""
    approvalStatus: str = ""


class ChatIn(BaseModel):
    message: str
    module: str = "OTHER"
    propertySelected: bool = False
    canSeeFinance: bool = False
    today: str | None = None
    counts: Counts | None = None
    rooms: list[RoomFact] = Field(default_factory=list)
    reports: list[ReportFact] = Field(default_factory=list)
    documents: list[DocFact] = Field(default_factory=list)
    golden: list[GoldenFact] = Field(default_factory=list)


class FinetuneIn(BaseModel):
    rows: list[GoldenFact] = Field(default_factory=list)


def require_key(x_lotus_ai_key: str | None = Header(default=None)) -> None:
    if not KEY or x_lotus_ai_key != KEY:
        raise HTTPException(status_code=401, detail="Sai khóa dịch vụ AI")


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "runtime": "python", "model": "grounded"}


@app.post("/v1/chat", dependencies=[Depends(require_key)])
def chat(body: ChatIn) -> dict:
    content = compose(body.model_dump())
    return {"content": content, "model": "grounded"}


@app.post("/v1/finetune/prepare", dependencies=[Depends(require_key)])
def prepare(body: FinetuneIn) -> dict:
    rows = trainable_rows([row.model_dump() for row in body.rows])
    DATA.mkdir(parents=True, exist_ok=True)
    target = DATA / "finetune.jsonl"
    with target.open("w", encoding="utf-8") as handle:
        for row in rows:
            handle.write(json.dumps({
                "messages": [
                    {"role": "system", "content": "Chỉ trả lời quy trình đã duyệt. Không bịa số tiền, tên khách, số giấy tờ."},
                    {"role": "user", "content": row["question"]},
                    {"role": "assistant", "content": row["answer"]},
                ]
            }, ensure_ascii=False) + "\n")
    return {
        "exported": len(rows),
        "file": "data/finetune.jsonl",
        "trainable": False,
        "reason": "Máy này không có GPU để fine-tune. File jsonl chỉ gồm quy trình đã duyệt, không gồm số doanh thu vì số đổi theo ngày.",
    }
