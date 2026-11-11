import re
from datetime import date

ABSTAIN = "Hiện tại tôi chưa tìm thấy tài liệu nghiệp vụ đã được xác nhận để trả lời chính xác câu hỏi này. Bạn vui lòng liên hệ quản trị viên hoặc bộ phận phụ trách nghiệp vụ."

SECTIONS = [
    (["BOOKING"], ["dat phong", "booking", "giu phong", "tao dat"], "Đặt phòng",
     "Mở Đặt phòng & lễ tân → Đặt phòng → Danh sách đặt phòng, rồi tạo đặt phòng. Chọn phòng, ngày nhận, ngày trả và xác nhận. Nhận từ hôm nay trở về trước chỉ giữ phòng khi buồng phòng đang INSPECTED. Gói tuần cần số đêm chia hết cho 7. Gói tháng cần số đêm chia hết cho 30."),
    (["CHECK_IN", "ROOM_MANAGEMENT"], ["nhan phong", "check-in", "checkin", "check in"], "Nhận phòng",
     "Phiếu phải đang CONFIRMED. Trên Danh sách đặt phòng bấm Check-in, hoặc trên Sơ đồ phòng bấm Nhận phòng khi phòng đang đến. Phần mềm từ chối nếu chưa đến ngày nhận, phòng không INSPECTED, hoặc phòng đã có khách đang ở."),
    (["CHECK_OUT", "FOLIO", "PAYMENT"], ["tra phong", "check-out", "checkout", "check out", "folio"], "Trả phòng",
     "Mở folio của phiếu đang CHECKED_IN. Còn phải thanh toán khác 0 thì nút Check-out không bấm được. Thu hết rồi mới trả phòng."),
    (["ROOM_MANAGEMENT", "HOUSEKEEPING"], ["so do", "trang thai phong", "buong", "ve sinh", "inspected", "dirty"], "Trạng thái phòng",
     "Sơ đồ phòng nằm ở Đặt phòng & lễ tân → Lưu trú → Sơ đồ phòng. Buồng phòng INSPECTED mới bán cho ngày nhận hôm nay. DIRTY là phòng bẩn, CLEAN là đã dọn, phòng hỏng hiện MAINTENANCE. Khách đang ở hiện OCCUPIED."),
    (["CASHIER", "PAYMENT", "FOLIO"], ["thu tien", "ca thu", "tien mat", "city ledger", "cong no"], "Thu tiền",
     "Thu tiền trong folio. Tiền mặt cần ca thu ngân đang mở. City Ledger ghi nợ và đóng phiếu khách. Công nợ xem ở Thanh toán & công nợ → Công nợ / khôi phục."),
    (["HOUSEKEEPING"], ["don phong", "buong phong"], "Buồng phòng",
     "Mở Buồng phòng & dịch vụ → Buồng phòng. Trạng thái INSPECTED là đã kiểm, dùng để bán. Đổi trạng thái vệ sinh không tự nhận phòng và không tự thu tiền."),
    (["REPORT"], ["bao cao", "doanh thu"], "Báo cáo",
     "Mở Báo cáo → Báo cáo doanh thu. Số liệu lấy từ phiếu và folio đang có, không phải số model tự tính."),
    (["OTHER", "DASHBOARD", "SYSTEM_ERROR"], ["chuc nang", "o dau", "menu"], "Vị trí chức năng",
     "Đặt phòng và sơ đồ nằm trong Đặt phòng & lễ tân. Buồng phòng nằm trong Buồng phòng & dịch vụ. Ca thu ngân và công nợ nằm trong Thanh toán & công nợ. Kho tri thức AI là chỗ duyệt tài liệu, không phải khung hỏi đáp. Khung hỏi đáp là nút Hỏi góc phải."),
]


def normalize(value: str | None) -> str:
    text = (value or "").lower()
    table = str.maketrans({
        "à": "a", "á": "a", "ạ": "a", "ả": "a", "ã": "a", "â": "a", "ầ": "a", "ấ": "a", "ậ": "a", "ẩ": "a", "ẫ": "a",
        "ă": "a", "ằ": "a", "ắ": "a", "ặ": "a", "ẳ": "a", "ẵ": "a", "è": "e", "é": "e", "ẹ": "e", "ẻ": "e", "ẽ": "e",
        "ê": "e", "ề": "e", "ế": "e", "ệ": "e", "ể": "e", "ễ": "e", "ì": "i", "í": "i", "ị": "i", "ỉ": "i", "ĩ": "i",
        "ò": "o", "ó": "o", "ọ": "o", "ỏ": "o", "õ": "o", "ô": "o", "ồ": "o", "ố": "o", "ộ": "o", "ổ": "o", "ỗ": "o",
        "ơ": "o", "ờ": "o", "ớ": "o", "ợ": "o", "ở": "o", "ỡ": "o", "ù": "u", "ú": "u", "ụ": "u", "ủ": "u", "ũ": "u",
        "ư": "u", "ừ": "u", "ứ": "u", "ự": "u", "ử": "u", "ữ": "u", "ỳ": "y", "ý": "y", "ỵ": "y", "ỷ": "y", "ỹ": "y", "đ": "d",
    })
    return text.translate(table)


def parts(question: str) -> list[str]:
    chunks = [part.strip() for part in re.split(r"[.?!\n;]", question or "") if part.strip()]
    chunks = [part for part in chunks if len(part) >= 2]
    return chunks or [(question or "").strip()]


def is_how_to(text: str) -> bool:
    return any(key in text for key in ("lam ", "cach ", "the nao", "buoc", "o dau", "chuc nang", "loi thuong", "bi loi"))


def wants_money(question: str) -> bool:
    text = normalize(question)
    if is_how_to(text):
        return False
    return any(key in text for key in ("bao cao", "doanh thu", "tien phong", "loi nhuan", "phieu chi", "thang nay", "thang truoc", "nam nay", "nam ngoai", "nam truoc"))


def wants_counts(question: str) -> bool:
    text = normalize(question)
    if is_how_to(text) or wants_money(question):
        return False
    return any(key in text for key in ("bao nhieu", "so luong", "cong suat", "dang o", "hom nay", "phong", "trang thai"))


def wants_rooms(question: str) -> bool:
    text = normalize(question)
    if is_how_to(text):
        return False
    return "phong nao" in text or "nhung phong" in text or "danh sach phong" in text or ("phong" in text and bool(re.search(r"\d{2,4}", question or "")))


def wants_live(question: str) -> bool:
    text = normalize(question)
    if is_how_to(text):
        return False
    return wants_money(question) or any(key in text for key in ("phong", "trang thai", "bao nhieu", "so luong", "cong suat", "dang o", "hom nay", "so do")) or bool(re.search(r"\d{2,4}", question or ""))


def guide_for(question: str, module: str) -> str | None:
    text = normalize(question)
    picked = [section for section in SECTIONS if module in section[0] or any(key in text for key in section[1])][:2]
    if not picked and is_how_to(text):
        picked = [section for section in SECTIONS if module in section[0]][:1]
    if not picked:
        return None
    return "\n\n".join(f"{section[2]}\n{section[3]}" for section in picked)


def money(amount: float) -> str:
    return f"{int(round(amount)):,}".replace(",", ".") + " ₫"


def show_date(iso: str) -> str:
    year, month, day = iso.split("-")
    return f"{day}/{month}/{year}"


def report_lines(report: dict) -> str:
    return "\n".join([
        f"{report['label']} ({show_date(report['start'])} – {show_date(report['end'])}), cùng Báo cáo doanh thu:",
        f"• Tiền phòng: {money(report['room'])}",
        f"• Dịch vụ: {money(report['service'])}",
        f"• No-show: {money(report['noShow'])}",
        f"• Trả muộn: {money(report['late'])}",
        f"• Bán lẻ: {money(report['retail'])}",
        f"• Ngoài phòng: {money(report['outside'])}",
        f"• Phiếu chi: {money(report['expenses'])}",
        f"• Tổng phát sinh folio: {money(report['folio'])}",
        f"• Đã thu trên folio: {money(report['collected'])}",
        f"• Lãi lỗ vận hành: {money(report['profit'])}",
        "Tiền phòng chỉ gồm hạng ROOM. No-show và trả muộn không cộng vào tiền phòng.",
    ])


def pick_reports(question: str, reports: list[dict]) -> list[dict]:
    text = normalize(question)
    wanted = []
    if "hom nay" in text:
        wanted.append("Hôm nay")
    if "thang nay" in text:
        wanted.append("Tháng này")
    if "thang truoc" in text:
        wanted.append("Tháng trước")
    if "nam nay" in text:
        wanted.append("Năm nay")
    if "nam ngoai" in text or "nam truoc" in text:
        wanted.append("Năm trước")
    if not wanted:
        wanted = ["Tháng này"]
    chosen = [row for row in reports if row["label"] in wanted]
    return chosen or reports[:1]


def count_lines(counts: dict) -> str:
    occupancy = counts["occupancy"]
    occupancy_text = str(occupancy).replace(".", ",") if isinstance(occupancy, float) else str(occupancy)
    return "\n".join([
        "Cùng số trên Bảng điều khiển:",
        f"• Đang ở: {counts['inHouse']}",
        f"• Sẵn sàng bán hôm nay: {counts['available']}",
        f"• Đến hôm nay: {counts['arrivals']}",
        f"• Trả hôm nay: {counts['departures']}",
        f"• Phòng kinh doanh: {counts['selling']}. Phòng hỏng: {counts['broken']}. Phòng bẩn: {counts['dirty']}. Công suất: {occupancy_text}%.",
    ])


def room_lines(question: str, rooms: list[dict]) -> str:
    numbers = set(re.findall(r"\d{2,4}", question or ""))
    chosen = [room for room in rooms if room["number"] in numbers] if numbers else list(rooms)
    if not chosen:
        return "Không thấy số phòng này ở cơ sở đang chọn." if numbers else "Hôm nay không có phiếu CHECKED_IN hoặc CONFIRMED còn hiệu lực."
    lines = []
    for room in chosen[:30]:
        housekeeping = "hỏng, không bán" if room.get("outOfOrder") else room.get("housekeeping")
        if room.get("code"):
            check_in = show_date(room["checkIn"])[0:5]
            check_out = show_date(room["checkOut"])[0:5]
            stay = f"phiếu {room['code']} {room['status']}, nhận {check_in}, trả {check_out}"
        else:
            stay = "không có phiếu đang hiệu lực"
        lines.append(f"Phòng {room['number']}, tầng {room['floor']}: buồng {housekeeping}; {stay}.")
    return "\n".join(lines)


def golden_for(part: str, golden: list[dict]) -> str | None:
    text = normalize(part)
    for row in golden:
        kind = (row.get("questionType") or "").upper()
        if kind not in ("PROCEDURE", "HOW_TO"):
            continue
        expected = (row.get("expectedAnswer") or "").strip()
        if not expected or "₫" in expected:
            continue
        if normalize(row.get("question")).strip(" ?.") == text.strip(" ?."):
            return expected
    return None


def answer_part(part: str, packet: dict) -> str | None:
    if not wants_live(part):
        learned = golden_for(part, packet.get("golden") or [])
        if learned:
            return learned
        return guide_for(part, packet.get("module") or "OTHER")
    if not packet.get("propertySelected"):
        return "Hãy chọn một cơ sở trên thanh trên cùng rồi hỏi lại."
    lines = []
    if wants_money(part) and not packet.get("canSeeFinance"):
        lines.append("Báo cáo doanh thu chỉ hiện với tài khoản có quyền báo cáo.")
    if wants_money(part) and packet.get("canSeeFinance"):
        for report in pick_reports(part, packet.get("reports") or []):
            lines.append(report_lines(report))
    if (wants_counts(part) or wants_rooms(part)) and packet.get("counts"):
        lines.append(count_lines(packet["counts"]))
    if wants_rooms(part):
        lines.append(room_lines(part, packet.get("rooms") or []))
    text = "\n".join(line for line in lines if line)
    return text or None


def compose(packet: dict) -> str:
    question = packet.get("message") or ""
    chunks = parts(question)
    blocks = []
    for index, part in enumerate(chunks):
        block = answer_part(part, packet) or ABSTAIN
        blocks.append(block if len(chunks) == 1 else f"Câu {index + 1}. {part}\n{block}")
    if all(ABSTAIN in block and "\n" not in block for block in blocks):
        excerpts = [doc.get("excerpt", "").strip() for doc in packet.get("documents") or [] if doc.get("excerpt")]
        return "\n\n".join(excerpts) if excerpts else ABSTAIN
    return "\n\n".join(blocks)


def trainable_rows(rows: list[dict]) -> list[dict]:
    kept = []
    for row in rows:
        if (row.get("approvalStatus") or "").upper() != "APPROVED":
            continue
        kind = (row.get("questionType") or "").upper()
        if kind not in ("PROCEDURE", "HOW_TO"):
            continue
        answer = (row.get("expectedAnswer") or "").strip()
        question = (row.get("question") or "").strip()
        if not question or not answer or "₫" in answer:
            continue
        kept.append({"question": question, "answer": answer})
    return kept
