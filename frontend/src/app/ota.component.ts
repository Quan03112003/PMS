import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from './api.service';

function day(delta = 0): string {
  const d = new Date(); d.setDate(d.getDate() + delta);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

@Component({
  selector: 'app-ota', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <div class="ota">
    <div class="ota-head">
      <div><div class="ota-eyebrow">B · KÊNH BÁN OTA · YC-OTA-01 / YC-OTA-02 / YC-BS-01</div><h1>Kết nối OTA</h1>
        <p>Lớp tích hợp phía PMS: mapping, nhận booking ký số, chống trùng, xác nhận 2 lớp. Kết nối thật cần hợp đồng và API của đối tác (Hotelink…).</p></div>
      <button class="ota-btn" (click)="load()">↻ Tải lại</button>
    </div>
    <div class="ota-providers">
      @for (p of providers; track p.code) {
        <div class="ota-provider"><b>{{ p.code }}</b><span class="ota-pill" [class.ok]="p.code === 'SANDBOX' || p.webhookConfigured">{{ p.code === 'SANDBOX' ? 'Sẵn sàng thử nghiệm' : p.webhookConfigured ? 'Đã cấu hình khóa webhook' : 'Chưa kết nối' }}</span><small>{{ p.note }}</small></div>
      }
    </div>
    @if (error) { <div class="ota-msg err">{{ error }}</div> }
    @if (success) { <div class="ota-msg ok">{{ success }}</div> }
    <div class="ota-tabs">
      @for (t of tabs; track t.id) {<button [class.on]="tab === t.id" (click)="tab = t.id">{{ t.label }}@if (t.id === 'bookings' && pendingCount) {<em>{{ pendingCount }}</em>}</button>}
    </div>

    @if (tab === 'bookings') {
      <section class="ota-panel"><div class="ota-row"><h2>Booking nhận từ kênh</h2>
        <select [(ngModel)]="statusFilter" (ngModelChange)="loadBookings()"><option value="">Tất cả trạng thái</option>@for (s of statusNames | keyvalue; track s.key) {<option [value]="s.key">{{ s.value }}</option>}</select></div>
        <div class="ota-scroll"><table><thead><tr><th>Kênh / mã</th><th>Khách</th><th>Lưu trú</th><th>Phòng PMS</th><th>Xác nhận</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>
          @for (b of bookings; track b.id) {
            <tr><td><b>{{ b.provider }}</b>@if (b.isSandbox) {<span class="ota-pill">SANDBOX</span>}<small>#{{ b.channelBookingId }} · {{ b.eventType }}</small></td>
              <td><b>{{ b.guestName }}</b><small>{{ b.adults }} NL · {{ currency(b.totalAmount) }}</small></td>
              <td>{{ b.checkInDate }} → {{ b.checkOutDate }}<small>Mã phòng kênh: {{ b.channelRoomCode }}</small></td>
              <td>@if (b.reservationCode) {<b>{{ b.reservationCode }}</b><small>Phòng {{ b.roomNumber }}</small>} @else {—}</td>
              <td><small>Lớp 1: {{ b.firstConfirmedAtUtc ? (b.firstConfirmedAtUtc | date:'dd/MM HH:mm') : '—' }}</small><small>Lớp 2: {{ b.secondConfirmedAtUtc ? (b.secondConfirmedAtUtc | date:'dd/MM HH:mm') : '—' }}</small></td>
              <td><span class="ota-status" [class]="'ota-status s-' + b.status">{{ statusNames[b.status] || b.status }}</span><small>{{ b.message }}</small>@if (b.reviewNote) {<small>Rà soát: {{ b.reviewNote }}</small>}</td>
              <td>@if (b.status === 'PENDING_CONFIRMATION') {<button class="ota-btn tiny primary" (click)="confirm(b)">Xác nhận lớp 2</button>}
                @if (b.status === 'NEEDS_REVIEW' || b.status === 'FAILED') {<button class="ota-btn tiny" (click)="review(b, 'ACCEPT')">Chấp nhận</button> <button class="ota-btn tiny" (click)="review(b, 'REJECT')">Từ chối</button>}</td></tr>
          } @empty {<tr><td colspan="7" class="ota-empty">Chưa có booking từ kênh. Dùng tab "Gửi thử Sandbox" để kiểm tra luồng.</td></tr>}
        </tbody></table></div>
        <p class="ota-note">Lớp 1: PMS tự giữ phòng và tạo booking khi nhận bản tin hợp lệ. Lớp 2: lễ tân đối chiếu với extranet rồi xác nhận. Booking OTA chưa đủ 2 lớp bị chặn hủy và đổi giá. Bản tin trùng không bị xóa mà đưa vào hàng chờ rà soát.</p>
      </section>
    }

    @if (tab === 'setup') {
      <div class="ota-grid">
        <section class="ota-panel"><h2>Kết nối kênh</h2><div class="ota-form">
          <label>Nhà cung cấp <select [(ngModel)]="connForm.provider">@for (p of providers; track p.code) {<option [value]="p.code">{{ p.code }}</option>}</select></label>
          <label>Tên hiển thị <input [(ngModel)]="connForm.displayName" placeholder="Hotelink - Lotus Hà Nội"></label>
          <label>Mã khách sạn trên kênh <input [(ngModel)]="connForm.hotelCode" placeholder="HN-CENTRAL"></label>
          <button class="ota-btn primary" (click)="createConnection()">Thêm kết nối</button></div>
          <table><thead><tr><th>#</th><th>Kênh</th><th>Mã KS</th><th>Trạng thái</th><th></th></tr></thead><tbody>
            @for (c of connections; track c.id) {<tr><td>{{ c.id }}</td><td><b>{{ c.displayName }}</b><small>{{ c.provider }}</small></td><td>{{ c.hotelCode }}</td><td><span class="ota-pill" [class.ok]="c.status === 'ACTIVE'">{{ c.status }}</span></td>
              <td>@if (c.status !== 'ACTIVE') {<button class="ota-btn tiny primary" (click)="setStatus(c, 'ACTIVE')">Bật</button>} @else {<button class="ota-btn tiny" (click)="setStatus(c, 'DISABLED')">Tắt</button>}</td></tr>}
            @empty {<tr><td colspan="5" class="ota-empty">Chưa có kết nối</td></tr>}
          </tbody></table></section>
        <section class="ota-panel"><h2>Mapping hạng phòng / giá</h2><div class="ota-form">
          <label>Kết nối <select [(ngModel)]="mapForm.connectionId"><option [ngValue]="0">-- Chọn --</option>@for (c of connections; track c.id) {<option [ngValue]="c.id">#{{ c.id }} {{ c.displayName }}</option>}</select></label>
          <label>Hạng phòng PMS <select [(ngModel)]="mapForm.roomTypeId"><option [ngValue]="0">-- Chọn --</option>@for (t of roomTypes; track t.id) {<option [ngValue]="t.id">{{ t.name }} · {{ currency(t.baseRate) }}</option>}</select></label>
          <label>Mã phòng trên kênh <input [(ngModel)]="mapForm.channelRoomCode" placeholder="DLX-DBL"></label>
          <label>Mã giá trên kênh (tùy chọn) <input [(ngModel)]="mapForm.channelRateCode" placeholder="BAR"></label>
          <label>Bảng giá PMS (tùy chọn) <select [(ngModel)]="mapForm.ratePlanId"><option [ngValue]="null">-- Không gắn --</option>@for (p of ratePlans; track p.id) {<option [ngValue]="p.id">{{ p.code }} · {{ p.name }}</option>}</select></label>
          <button class="ota-btn primary" (click)="createMapping()">Lưu mapping</button></div>
          <table><thead><tr><th>Kết nối</th><th>Mã kênh</th><th>Hạng phòng PMS</th></tr></thead><tbody>
            @for (m of mappings; track m.id) {<tr><td>#{{ m.connectionId }}</td><td><b>{{ m.channelRoomCode }}</b><small>{{ m.channelRateCode || 'mọi mã giá' }}</small></td><td>{{ m.roomTypeName }}</td></tr>}
            @empty {<tr><td colspan="3" class="ota-empty">Chưa có mapping</td></tr>}
          </tbody></table></section>
      </div>
    }

    @if (tab === 'sandbox') {
      <section class="ota-panel"><h2>Gửi booking thử (Sandbox)</h2>
        @if (!sandboxConnections.length) {<p class="ota-note">Cần một kết nối nhà cung cấp SANDBOX đang bật và có mapping (tab Thiết lập kênh).</p>}
        <div class="ota-form cols">
          <label>Kết nối SANDBOX <select [(ngModel)]="sbx.connectionId">@for (c of sandboxConnections; track c.id) {<option [ngValue]="c.id">#{{ c.id }} {{ c.displayName }}</option>}</select></label>
          <label>Sự kiện <select [(ngModel)]="sbx.eventType"><option value="NEW">NEW - đặt mới</option><option value="MODIFY">MODIFY - thay đổi</option><option value="CANCEL">CANCEL - hủy</option></select></label>
          <label>Mã booking kênh (để trống = tự sinh) <input [(ngModel)]="sbx.channelBookingId" placeholder="SBX-..."></label>
          <label>Mã phòng kênh <select [(ngModel)]="sbx.roomCode">@for (m of sandboxMappings; track m.id) {<option [value]="m.channelRoomCode">{{ m.channelRoomCode }} → {{ m.roomTypeName }}</option>}</select></label>
          <label>Tên khách <input [(ngModel)]="sbx.guestName"></label><label>Điện thoại <input [(ngModel)]="sbx.phone"></label>
          <label>Nhận phòng <input type="date" [(ngModel)]="sbx.checkInDate"></label><label>Trả phòng <input type="date" [(ngModel)]="sbx.checkOutDate"></label>
          <label>Người lớn <input type="number" min="1" [(ngModel)]="sbx.adults"></label><label>Tổng tiền kênh (VND) <input type="number" min="0" [(ngModel)]="sbx.totalAmount"></label>
        </div>
        <div class="ota-row"><button class="ota-btn primary" [disabled]="!sbx.connectionId || busy" (click)="pushSandbox()">Gửi bản tin</button>
          <button class="ota-btn" [disabled]="!lastSandbox || busy" (click)="pushSandbox(true)">Gửi lại đúng bản tin trước (kiểm tra chống trùng)</button></div>
        @if (lastResult) {<p class="ota-note">Kết quả: <b>{{ statusNames[lastResult.booking.status] || lastResult.booking.status }}</b>{{ lastResult.replay ? ' · bản tin lặp lại, không tạo booking mới' : '' }} — {{ lastResult.booking.message }}</p>}
      </section>
    }

    @if (tab === 'logs') {
      <div class="ota-grid">
        <section class="ota-panel"><h2>Nhật ký đồng bộ</h2><div class="ota-scroll"><table><thead><tr><th>Thời gian</th><th>Kênh</th><th>Sự kiện</th><th>Kết quả</th><th>Nội dung</th></tr></thead><tbody>
          @for (l of logs; track l.id) {<tr><td>{{ l.createdAtUtc | date:'dd/MM HH:mm:ss' }}</td><td>{{ l.provider }}</td><td>{{ l.eventType }}</td><td><b>{{ l.outcome }}</b></td><td>{{ l.message }}</td></tr>}
          @empty {<tr><td colspan="5" class="ota-empty">Chưa có nhật ký</td></tr>}
        </tbody></table></div></section>
        <section class="ota-panel"><h2>Hợp đồng webhook cho đối tác</h2>
          <p class="ota-note"><b>POST</b> <code>{{ webhookBase }}/api/integrations/ota/HOTELINK/webhook</code></p>
          <p class="ota-note">Header <code>X-Lotus-Timestamp</code>: Unix giây (lệch tối đa 5 phút).<br>Header <code>X-Lotus-Signature</code>: <code>sha256=</code> + hex(HMAC-SHA256(khóa, timestamp + "." + body)).<br>Khóa đặt ở biến môi trường máy chủ <code>Ota__Providers__HOTELINK__WebhookSecret</code> (≥ 32 ký tự), không lưu trong database.</p>
          <pre>{{ sampleBody }}</pre>
          <p class="ota-note">Gửi lại cùng nội dung được trả về bản đã nhận (idempotent). Chưa cấu hình khóa thì webhook trả 503.</p></section>
      </div>
    }
  </div>`,
  styles: [`
    .ota-head{display:flex;justify-content:space-between;gap:16px;align-items:flex-start;margin-bottom:16px}.ota-head h1{font-family:var(--display,'Sora',sans-serif);letter-spacing:-.03em}.ota-head p{font-size:.84rem;color:#6c757d;margin:0;max-width:760px}
    .ota-eyebrow{color:#3f6ad8;font-size:.68rem;font-weight:800;letter-spacing:.12em;margin-bottom:8px;text-transform:uppercase}
    .ota-providers{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:10px;margin-bottom:14px}
    .ota-provider{background:#fff;border:1px solid #eef1f5;border-radius:6px;padding:12px 14px;display:flex;flex-wrap:wrap;gap:6px 10px;align-items:center;box-shadow:0 .47rem 2.19rem rgba(63,77,103,.06)}.ota-provider small{width:100%;color:#8b95a8;font-size:.72rem}
    .ota-pill{display:inline-block;margin-left:6px;padding:3px 8px;border-radius:4px;font-size:.68rem;font-weight:800;background:#fff6db;color:#a07a08}.ota-pill.ok{background:#e6f8ef;color:#238a55}
    .ota-tabs{display:flex;gap:6px;margin-bottom:12px;flex-wrap:wrap}.ota-tabs button{border:1px solid #d7dde5;background:#fff;border-radius:6px;padding:9px 14px;font-weight:800;font-size:.78rem;color:#5c6778}
    .ota-tabs button.on{background:#3f6ad8;color:#fff;border-color:#3f6ad8;box-shadow:0 4px 12px rgba(63,106,216,.22)}.ota-tabs em{font-style:normal;background:#d92550;color:#fff;border-radius:9px;padding:1px 6px;margin-left:6px;font-size:.68rem}
    .ota-panel{background:#fff;border:1px solid #eef1f5;border-radius:6px;padding:18px;margin-bottom:14px;box-shadow:0 .47rem 2.19rem rgba(63,77,103,.06)}.ota-grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}
    .ota-row{display:flex;gap:10px;align-items:center;justify-content:space-between;flex-wrap:wrap;margin-bottom:10px}
    .ota-form{display:grid;gap:10px;margin-bottom:14px}.ota-form.cols{grid-template-columns:repeat(2,minmax(0,1fr))}
    .ota-form label{display:flex;flex-direction:column;gap:5px;font-size:.72rem;font-weight:800;color:#4a5568}
    .ota-form input,.ota-form select,.ota-row select{border:1px solid #d7dde5;border-radius:6px;padding:9px 10px;font-size:.8rem;color:#3f4d67;background:#fff}
    .ota-btn{border:1px solid #d7dde5;background:#fff;border-radius:6px;padding:9px 14px;font-weight:800;font-size:.78rem;color:#3f4d67}.ota-btn.primary{background:#3f6ad8;border-color:#3f6ad8;color:#fff}
    .ota-btn.tiny{padding:6px 9px;font-size:.68rem}.ota-btn:disabled{opacity:.45;cursor:not-allowed}
    .ota-scroll{overflow:auto}.ota-panel table td{white-space:normal;vertical-align:top;padding:10px 12px}.ota-panel td small{display:block;color:#98a2b3;font-size:.68rem;margin-top:3px}
    .ota-status{padding:4px 8px;border-radius:4px;font-size:.68rem;font-weight:800;background:#eef1f5;color:#5c6778}.ota-status.s-CONFIRMED{background:#e6f8ef;color:#238a55}
    .ota-status.s-PENDING_CONFIRMATION{background:#fff6db;color:#a07a08}.ota-status.s-NEEDS_REVIEW,.ota-status.s-FAILED{background:#fde8ee;color:#d92550}
    .ota-msg{padding:10px 12px;border-radius:6px;margin-bottom:12px;font-size:.8rem}.ota-msg.err{background:#fde8ee;color:#d92550}.ota-msg.ok{background:#e6f8ef;color:#238a55}
    .ota-note{font-size:.8rem;color:#5c6778;line-height:1.7}.ota-empty{text-align:center;color:#98a2b3;padding:24px}
    pre{background:#3f4d67;color:#dfe6f2;border-radius:6px;padding:12px;font-size:.72rem;overflow:auto}
    @media (max-width:1100px){.ota-grid,.ota-form.cols{grid-template-columns:1fr}}`]
})
export class OtaComponent implements OnChanges {
  @Input() propertyId = 0;
  tabs = [{ id: 'bookings', label: 'Booking từ kênh' }, { id: 'setup', label: 'Thiết lập kênh' }, { id: 'sandbox', label: 'Gửi thử Sandbox' }, { id: 'logs', label: 'Nhật ký & webhook' }];
  tab = 'bookings';
  statusNames: Record<string, string> = { PENDING_CONFIRMATION: 'Chờ xác nhận lớp 2', CONFIRMED: 'Đã xác nhận 2 lớp', NEEDS_REVIEW: 'Chờ rà soát',
    FAILED: 'Lỗi - cần xử lý', CANCELLED: 'Đã hủy theo kênh', REJECTED: 'Đã từ chối', RESOLVED: 'Đã xử lý' };
  providers: any[] = []; connections: any[] = []; mappings: any[] = []; bookings: any[] = []; logs: any[] = []; roomTypes: any[] = []; ratePlans: any[] = [];
  statusFilter = ''; error = ''; success = ''; busy = false;
  connForm: any = { provider: 'SANDBOX', displayName: '', hotelCode: '' };
  mapForm: any = { connectionId: 0, roomTypeId: 0, channelRoomCode: '', channelRateCode: '', ratePlanId: null };
  sbx: any = this.newSandbox();
  lastSandbox: any = null; lastResult: any = null;
  webhookBase = location.origin;
  sampleBody = JSON.stringify({ hotelCode: 'HN-CENTRAL', channelBookingId: 'HL-100245', eventType: 'NEW', guestName: 'Nguyen Van A', phone: '0900000000',
    email: null, checkInDate: day(7), checkOutDate: day(9), roomCode: 'DLX-DBL', rateCode: 'BAR', adults: 2, children: 0, totalAmount: 1800000, currency: 'VND' }, null, 2);

  constructor(private api: ApiService) {}
  get pendingCount(): number { return this.bookings.filter(b => ['PENDING_CONFIRMATION', 'NEEDS_REVIEW', 'FAILED'].includes(b.status)).length; }
  get sandboxConnections(): any[] { return this.connections.filter(c => c.provider === 'SANDBOX' && c.status === 'ACTIVE'); }
  get sandboxMappings(): any[] { return this.mappings.filter(m => m.connectionId === +this.sbx.connectionId); }

  ngOnChanges(): void { if (this.propertyId) this.load(); }
  newSandbox(): any { return { connectionId: 0, eventType: 'NEW', channelBookingId: '', roomCode: '', guestName: 'Khach OTA Thu Nghiem', phone: '0900000000',
    checkInDate: day(1), checkOutDate: day(2), adults: 2, totalAmount: 0 }; }
  private get<T>(path: string, done: (x: T) => void): void { this.api.get<T>(path).subscribe({ next: done, error: e => this.error = e?.error?.message || `Không tải được ${path}` }); }
  load(): void {
    const id = this.propertyId; this.error = '';
    this.get<any[]>('/ota/providers', x => this.providers = x);
    this.get<any[]>(`/ota/connections?propertyId=${id}`, x => { this.connections = x; this.pickDefaults(); });
    this.get<any[]>(`/ota/mappings?propertyId=${id}`, x => { this.mappings = x; this.pickDefaults(); });
    this.get<any[]>(`/room-types?propertyId=${id}`, x => this.roomTypes = x);
    this.get<any[]>(`/rate-plans?propertyId=${id}`, x => this.ratePlans = x);
    this.get<any[]>(`/ota/logs?propertyId=${id}`, x => this.logs = x);
    this.loadBookings();
  }
  loadBookings(): void { this.get<any[]>(`/ota/bookings?propertyId=${this.propertyId}${this.statusFilter ? '&status=' + this.statusFilter : ''}`, x => this.bookings = x); }
  private pickDefaults(): void {
    if (!this.sandboxConnections.some(c => c.id === +this.sbx.connectionId)) this.sbx.connectionId = this.sandboxConnections[0]?.id ?? 0;
    if (!this.sandboxMappings.some(m => m.channelRoomCode === this.sbx.roomCode)) this.sbx.roomCode = this.sandboxMappings[0]?.channelRoomCode ?? '';
  }
  private send(call: any, message: string, after?: (r: any) => void): void {
    this.busy = true; this.error = ''; this.success = '';
    call.subscribe({ next: (r: any) => { this.busy = false; this.success = message; after?.(r); this.load(); },
      error: (e: any) => { this.busy = false; this.error = e?.error?.message || `Lỗi API (${e?.status || 'mất kết nối'})`; } });
  }
  createConnection(): void { this.send(this.api.post('/ota/connections', { propertyId: +this.propertyId, ...this.connForm }), 'Đã thêm kết nối kênh',
    () => this.connForm = { provider: 'SANDBOX', displayName: '', hotelCode: '' }); }
  setStatus(c: any, status: string): void { this.send(this.api.patch(`/ota/connections/${c.id}/status`, { status }), `Kết nối #${c.id}: ${status}`); }
  createMapping(): void {
    this.send(this.api.post('/ota/mappings', { connectionId: +this.mapForm.connectionId, roomTypeId: +this.mapForm.roomTypeId, channelRoomCode: this.mapForm.channelRoomCode,
      channelRateCode: this.mapForm.channelRateCode || null, ratePlanId: this.mapForm.ratePlanId }), 'Đã lưu mapping',
      () => this.mapForm = { ...this.mapForm, channelRoomCode: '', channelRateCode: '' });
  }
  pushSandbox(replay = false): void {
    const body = replay ? this.lastSandbox : { ...this.sbx, connectionId: +this.sbx.connectionId, adults: +this.sbx.adults, children: 0, totalAmount: +this.sbx.totalAmount,
      channelBookingId: this.sbx.channelBookingId || 'SBX-' + crypto.randomUUID().slice(0, 8).toUpperCase() };
    this.send(this.api.post('/ota/sandbox/bookings', body), replay ? 'Đã gửi lại bản tin' : 'Đã gửi bản tin sandbox', r => { this.lastSandbox = body; this.lastResult = r; });
  }
  confirm(b: any): void { this.send(this.api.post(`/ota/bookings/${b.id}/confirm`, {}), `Đã xác nhận lớp 2 booking ${b.channelBookingId}`); }
  review(b: any, decision: 'ACCEPT' | 'REJECT'): void {
    const note = prompt(decision === 'ACCEPT' ? 'Ghi chú lý do chấp nhận (tối thiểu 5 ký tự):' : 'Lý do từ chối (tối thiểu 5 ký tự):');
    if (!note?.trim()) return;
    this.send(this.api.post(`/ota/bookings/${b.id}/review`, { decision, note }), 'Đã ghi nhận rà soát');
  }
  currency(n: number): string { return (n || 0).toLocaleString('vi-VN') + ' ₫'; }
}
