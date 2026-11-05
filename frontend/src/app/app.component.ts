import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { AdvancedComponent } from './advanced.component';

@Component({ selector: 'app-root', standalone: true, imports: [CommonModule, FormsModule, AdvancedComponent], templateUrl: './app.component.html' })
export class AppComponent implements OnInit {
  user: any = null;
  token = '';
  loginEmail = 'reception@pms.local';
  loginPassword = 'Demo@12345';
  loginBusy = false;
  view = 'dashboard';
  properties: any[] = [];
  propertyId = 0;
  rooms: any[] = [];
  roomTypes: any[] = [];
  bookings: any[] = [];
  stays: any[] = [];
  services: any[] = [];
  guests: any[] = [];
  agencies: any[] = [];
  ratePlans: any[] = [];
  users: any[] = [];
  shifts: any[] = [];
  audits: any[] = [];
  dashboard: any = {};
  report: any = null;
  modal = '';
  form: any = {};
  folio: any = null;
  availableRooms: any[] = [];
  filter = '';
  floor = 0;
  toastMessage = '';
  toastIsError = false;
  loading = false;
  today = localIsoDate(0);
  tomorrow = localIsoDate(1);
  reportFrom = localIsoDate(-7);
  reportTo = localIsoDate(0);
  sidebarOpen = false;

  advancedViews = ['waitlist','groups','transfers','rates','buildings','stockledger','suppliers','retail','vouchers','debts','assets','dayclose','chain','agencyReports','shiftReconcile','registration'];

  menuGroups = [
    { title: 'TỔNG QUAN', items: [{ id: 'dashboard', label: 'Bảng điều khiển', icon: '◫' }] },
    { title: 'LỄ TÂN', items: [
      { id: 'roommap', label: 'Sơ đồ phòng', icon: '▦' },
      { id: 'bookings', label: 'Đặt phòng', icon: '▤' },
      { id: 'groups', label: 'Khách đoàn', icon: '▦' },
      { id: 'waitlist', label: 'Phòng hàng đợi', icon: '◷' },
      { id: 'transfers', label: 'Chuyển dịch vụ', icon: '⇆' },
      { id: 'debts', label: 'Công nợ / khôi phục', icon: '₫' },
      { id: 'assets', label: 'Tài sản khách', icon: '◈' },
      { id: 'stays', label: 'Khách đang ở', icon: '♙' },
      { id: 'housekeeping', label: 'Buồng phòng', icon: '✧' },
      { id: 'shifts', label: 'Ca thu ngân', icon: '◷' }
    ] },
    { title: 'QUẢN LÝ', items: [
      { id: 'properties', label: 'Cơ sở khách sạn', icon: '⌂' },
      { id: 'manageRooms', label: 'Phòng & hạng phòng', icon: '▧' },
      { id: 'buildings', label: 'Tòa nhà', icon: '▥' },
      { id: 'rates', label: 'Bảng giá giờ/ngày/đêm', icon: '₫' },
      { id: 'users', label: 'Nhân viên & quyền', icon: '♟' },
      { id: 'services', label: 'Dịch vụ & minibar', icon: '◈' },
      { id: 'stockledger', label: 'Nhập xuất tồn kho', icon: '▤' },
      { id: 'suppliers', label: 'Nhà cung cấp', icon: '◇' },
      { id: 'retail', label: 'Bán hàng ngoài phòng', icon: '♧' },
      { id: 'vouchers', label: 'Phiếu thu / phiếu chi', icon: '▣' },
      { id: 'guests', label: 'Khách hàng', icon: '♧' },
      { id: 'agencies', label: 'Đại lý & kênh', icon: '◇' },
      { id: 'reports', label: 'Báo cáo doanh thu', icon: '▥' },
      { id: 'chain', label: 'Báo cáo chuỗi', icon: '▦' },
      { id: 'agencyReports', label: 'Hiệu quả đại lý', icon: '◇' },
      { id: 'shiftReconcile', label: 'Đối soát ca', icon: '₫' },
      { id: 'registration', label: 'Danh sách lưu trú', icon: '▤' },
      { id: 'dayclose', label: 'Snapshot chốt ngày', icon: '◷' },
      { id: 'audit', label: 'Nhật ký thao tác', icon: '☷' }
    ] }
  ];

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    const token = localStorage.getItem('pms_token');
    const storedUser = localStorage.getItem('pms_user');
    if (token && storedUser) {
      try { this.token = token; this.user = JSON.parse(storedUser); this.loadProperties(); }
      catch { this.logout(); }
    }
  }

  get isManager(): boolean { return ['CHAIN_ADMIN', 'PROPERTY_MANAGER'].includes(this.user?.role); }
  get currentProperty(): any { return this.properties.find(p => p.id === Number(this.propertyId)); }
  get shownRooms(): any[] {
    return this.rooms.filter(r => (this.floor === 0 || r.floor === Number(this.floor)) &&
      (!this.filter || `${r.number} ${r.roomTypeName} ${r.current?.fullName || ''}`.toLowerCase().includes(this.filter.toLowerCase())));
  }
  get floors(): number[] { return Array.from(new Set(this.rooms.map(x => x.floor))).sort((a, b) => a - b); }
  get remaining(): number { return Number(this.folio?.balance ?? 0); }
  get openShift(): any { return this.shifts.find(s => s.status === 'OPEN' && s.userId === this.user?.id); }

  login(): void {
    this.loginBusy = true;
    this.api.post('/auth/login', { email: this.loginEmail, password: this.loginPassword }).subscribe({
      next: (r: any) => {
        this.loginBusy = false; this.token = r.token; this.user = r.user;
        localStorage.setItem('pms_token', r.token); localStorage.setItem('pms_user', JSON.stringify(r.user));
        this.loadProperties(); this.notice('Đăng nhập thành công');
      }, error: () => { this.loginBusy = false; this.notice('Sai tài khoản/mật khẩu hoặc API chưa chạy', true); }
    });
  }
  logout(): void { localStorage.removeItem('pms_token'); localStorage.removeItem('pms_user'); this.user = null; this.token = ''; }
  notice(msg: string, error = false): void { this.toastMessage = msg; this.toastIsError = error; }
  onError(err: any): void {
    const msg = err?.error?.message || (err?.status === 0 ? 'Không kết nối được API. Hãy chạy ASP.NET Core tại localhost:5080.' : err?.status === 401 ? 'Phiên đăng nhập hết hạn, hãy đăng nhập lại' : `Giao dịch thất bại (HTTP ${err?.status || 'unknown'})`);
    this.notice(msg, true);
    if (err?.status === 401) this.logout();
  }
  run(call: Observable<any>, callback?: (x: any) => void, message = 'Đã lưu thành công'): void {
    this.loading = true;
    call.subscribe({ next: (r) => { this.loading = false; this.modal = ''; this.notice(message); callback?.(r); this.refresh(); },
      error: e => { this.loading = false; this.onError(e); } });
  }
  loadProperties(): void {
    this.api.get<any[]>('/properties').subscribe({ next: props => {
      this.properties = props;
      const stored = Number(localStorage.getItem('pms_property'));
      this.propertyId = props.some(p => p.id === stored) ? stored : props[0]?.id || 0;
      this.refresh();
    }, error: e => this.onError(e) });
  }
  changeProperty(): void { this.propertyId = Number(this.propertyId); localStorage.setItem('pms_property', String(this.propertyId)); this.floor = 0; this.refresh(); }
  setView(v: string): void { this.view = v; this.sidebarOpen = false; this.filter = ''; this.refresh(); }
  refresh(): void {
    if (!this.user || !this.propertyId) return;
    const id = this.propertyId;
    this.api.get(`/dashboard?propertyId=${id}`).subscribe({ next: r => this.dashboard = r, error: e => this.onError(e) });
    this.api.get<any[]>(`/rooms?propertyId=${id}`).subscribe({ next: r => this.rooms = r, error: e => this.onError(e) });
    this.api.get<any[]>(`/room-types?propertyId=${id}`).subscribe({ next: r => this.roomTypes = r, error: e => this.onError(e) });
    if (['bookings', 'dashboard'].includes(this.view)) this.api.get<any[]>(`/reservations?propertyId=${id}`).subscribe({ next: r => this.bookings = r, error: e => this.onError(e) });
    if (['stays', 'dashboard', 'roommap'].includes(this.view)) this.api.get<any[]>(`/stays?propertyId=${id}`).subscribe({ next: r => this.stays = r, error: e => this.onError(e) });
    if (['services', 'stays'].includes(this.view)) this.api.get<any[]>(`/services?propertyId=${id}`).subscribe({ next: r => this.services = r, error: e => this.onError(e) });
    if (this.view === 'users') this.api.get<any[]>(`/users?propertyId=${id}`).subscribe({ next: r => this.users = r, error: e => this.onError(e) });
    if (this.view === 'guests') this.api.get<any[]>(`/guests?propertyId=${id}`).subscribe({ next: r => this.guests = r, error: e => this.onError(e) });
    if (this.view === 'agencies') this.api.get<any[]>(`/agencies?propertyId=${id}`).subscribe({ next: r => this.agencies = r, error: e => this.onError(e) });
    if (this.view === 'shifts') this.api.get<any[]>(`/shifts?propertyId=${id}`).subscribe({ next: r => this.shifts = r, error: e => this.onError(e) });
    if (this.view === 'audit') this.api.get<any[]>(`/audit?propertyId=${id}`).subscribe({ next: r => this.audits = r, error: e => this.onError(e) });
    if (this.view === 'reports') this.loadReport();
  }
  title(): string { return this.menuGroups.flatMap(g => g.items).find(i => i.id === this.view)?.label || 'PMS'; }
  statusText(status: string): string {
    const dict: Record<string, string> = { VACANT: 'Phòng trống', OCCUPIED: 'Đang ở', ARRIVAL: 'Khách sắp đến', DIRTY: 'Chưa dọn',
      MAINTENANCE: 'Bảo trì', CLEAN: 'Đã dọn', INSPECTED: 'Sẵn sàng', CONFIRMED: 'Đã xác nhận', CHECKED_IN: 'Đang ở',
      CHECKED_OUT: 'Đã trả', CANCELLED: 'Đã hủy', OPEN: 'Đang mở', CLOSED: 'Đã khóa', DIRECT: 'Trực tiếp', OTA: 'OTA' };
    return dict[status] || status;
  }
  badge(status: string): string { return status.toLowerCase().replaceAll('_', '-'); }

  openModal(type: string, values: any = {}): void {
    this.modal = type;
    this.form = { propertyId: this.propertyId, checkInDate: this.today, checkOutDate: localIsoDate(2), adults: 2, children: 0,
      bookingSource: 'DIRECT', bookingMode: 'OVERNIGHT', agencyId: null, selectedRatePlanId: null, nightlyRate: null, price: 0, unit: 'Lần', quantity: 1, trackInventory: false,
      stockQuantity: 0, openingAmount: 0, method: 'CASH', amount: 0, capacity: 2, floor: 1,
      commissionPercent: 0, ...values };
    if (type === 'booking') { this.searchAvailable(); this.api.get<any[]>(`/rate-plans?propertyId=${this.propertyId}`).subscribe({next: x => this.ratePlans = x, error: e => this.onError(e)}); this.api.get<any[]>(`/agencies?propertyId=${this.propertyId}`).subscribe({next: x => this.agencies = x, error: e => this.onError(e)}); }
  }
  searchAvailable(): void {
    if (!this.form.checkInDate || !this.form.checkOutDate || this.form.checkInDate >= this.form.checkOutDate) { this.availableRooms = []; return; }
    const params = `propertyId=${this.propertyId}&startDate=${this.form.checkInDate}&endDate=${this.form.checkOutDate}`;
    this.api.get<any[]>('/availability?' + params).subscribe({ next: r => this.availableRooms = r, error: e => this.onError(e) });
  }
  onBookingRoomChange(): void { if (this.form.selectedRatePlanId) { this.onRatePlanChange(); return; } this.form.nightlyRate = this.availableRooms.find(r => r.id === this.form.roomId)?.baseRate ?? null; }
  onRatePlanChange(): void { const selected = this.ratePlans.find(x => x.id === +this.form.selectedRatePlanId); if (selected) { this.form.bookingMode = selected.mode; this.form.nightlyRate = selected.basePrice; } }
  chooseRoom(r: any): void { this.form.roomId = r.id; this.form.nightlyRate = r.baseRate; }
  createBooking(): void {
    if (!this.form.roomId || !this.form.guestName) { this.notice('Vui lòng chọn phòng và nhập tên khách', true); return; }
    this.run(this.api.post('/reservations', { ...this.form, roomId: Number(this.form.roomId), nightlyRate: Number(this.form.nightlyRate) }),
      () => this.setView('bookings'), 'Đã tạo và xác nhận đặt phòng');
  }
  createRoomType(): void { this.run(this.api.post('/room-types', { ...this.form, baseRate: Number(this.form.baseRate), capacity: Number(this.form.capacity) })); }
  createRoom(): void { this.run(this.api.post('/rooms', { ...this.form, roomTypeId: Number(this.form.roomTypeId), floor: Number(this.form.floor) })); }
  createService(): void { this.run(this.api.post('/services', { ...this.form, price: Number(this.form.price), stockQuantity: Number(this.form.stockQuantity) })); }
  createProperty(): void { this.run(this.api.post('/properties', this.form), () => this.loadProperties(), 'Đã tạo cơ sở khách sạn'); }
  createUser(): void { this.run(this.api.post('/users', { ...this.form, role: this.form.role || 'FRONT_DESK_AGENT' })); }
  createAgency(): void { this.run(this.api.post('/agencies', { ...this.form, commissionPercent: Number(this.form.commissionPercent) })); }
  restock(): void { this.run(this.api.post(`/services/${this.form.id}/restock`, { quantity: Number(this.form.quantity) })); }
  checkIn(b: any): void { if (confirm(`Nhận phòng ${b.roomNumber} cho ${b.fullName}?`)) this.run(this.api.post(`/reservations/${b.id}/check-in`, {}), () => this.setView('stays'), 'Check-in thành công'); }
  cancelBooking(b: any): void { if (confirm(`Hủy đặt phòng ${b.code}? Nếu đã có cọc, hãy hoàn cọc trước.`)) this.run(this.api.post(`/reservations/${b.id}/cancel`, {}), undefined, 'Đã hủy đặt phòng'); }
  refundDeposit(b: any): void {
    if (this.user?.role !== 'CHAIN_ADMIN' && this.user?.role !== 'PROPERTY_MANAGER') return;
    const entered = prompt(`Số tiền cọc cần hoàn cho ${b.code} (VND):`);
    if (!entered) return;
    const amount = Number(entered);
    if (!Number.isFinite(amount) || amount <= 0) { this.notice('Số tiền hoàn không hợp lệ', true); return; }
    const reference = prompt('Mã chuyển hoàn/biên nhận (bắt buộc):');
    if (!reference) return;
    if (!confirm(`Xác nhận hoàn ${amount.toLocaleString('vi-VN')} VND cho ${b.code}?`)) return;
    this.run(this.api.post(`/reservations/${b.id}/refund-deposit`,
      { amount, method: 'TRANSFER', reference, idempotencyKey: crypto.randomUUID() }), undefined,
      'Đã ghi sổ hoàn tiền cọc; có thể hủy đặt phòng');
  }
  openFolio(stayId: number): void {
    this.api.get(`/stays/${stayId}/folio`).subscribe({ next: r => { this.folio = r; this.modal = 'folio'; }, error: e => this.onError(e) });
  }
  openFolioByReservation(resId: number): void {
    const s = this.stays.find(x => x.reservationId === resId);
    if (s) this.openFolio(s.id);
    else { this.setView('stays'); this.notice('Hãy mở hồ sơ khách đang ở để thao tác'); }
  }
  addService(): void {
    this.run(this.api.post(`/stays/${this.folio.details.id}/services`, { serviceId: Number(this.form.serviceId), quantity: Number(this.form.quantity) }),
      () => this.openFolio(this.folio.details.id), 'Đã ghi nhận dịch vụ vào folio');
  }
  payment(): void {
    this.run(this.api.post(`/stays/${this.folio.details.id}/payments`, { amount: Number(this.form.amount), method: this.form.method, reference: this.form.reference, idempotencyKey: crypto.randomUUID() }),
      () => this.openFolio(this.folio.details.id), 'Thanh toán thành công');
  }
  deposit(): void {
    this.run(this.api.post(`/reservations/${this.form.reservationId}/deposit`, { amount: Number(this.form.amount), method: this.form.method, reference: this.form.reference, idempotencyKey: crypto.randomUUID() }),
      undefined, 'Đã ghi nhận tiền cọc');
  }
  checkout(): void {
    if (!confirm(`Xác nhận trả phòng ${this.folio.details.roomNumber}?`)) return;
    this.run(this.api.post(`/stays/${this.folio.details.id}/checkout`, {}), () => this.setView('roommap'), 'Check-out thành công. Phòng chuyển sang Chưa dọn.');
  }
  extend(): void { this.run(this.api.post(`/stays/${this.folio.details.id}/extend`, { newCheckOutDate: this.form.newCheckOutDate }),
    () => this.openFolio(this.folio.details.id), 'Gia hạn lưu trú thành công'); }
  transfer(): void { this.run(this.api.post(`/stays/${this.folio.details.id}/transfer`, { newRoomId: Number(this.form.newRoomId) }),
    () => this.openFolio(this.folio.details.id), 'Đã đổi phòng. Phòng cũ chuyển Chưa dọn.'); }
  housekeeping(room: any, status: string): void { this.run(this.api.patch(`/rooms/${room.id}/housekeeping`, { status }), undefined, `Phòng ${room.number}: ${this.statusText(status)}`); }
  maintenance(room: any): void {
    if (!this.isManager) return;
    this.run(this.api.patch(`/rooms/${room.id}/maintenance`, { isOutOfOrder: !room.isOutOfOrder, note: !room.isOutOfOrder ? 'Khóa bảo trì từ giao diện PMS' : '' }));
  }
  openShiftAction(): void { this.run(this.api.post('/shifts/open', { propertyId: this.propertyId, openingAmount: Number(this.form.openingAmount) })); }
  closeShiftAction(shift: any): void {
    this.form = { shiftId: shift.id, closingAmount: shift.openingAmount, note: '' }; this.modal = 'closeShift';
  }
  confirmCloseShift(): void { this.run(this.api.post(`/shifts/${this.form.shiftId}/close`, { closingAmount: Number(this.form.closingAmount), note: this.form.note })); }
  loadReport(): void {
    this.api.get(`/reports/summary?propertyId=${this.propertyId}&from=${this.reportFrom}&to=${this.reportTo}`).subscribe({ next: r => this.report = r, error: e => this.onError(e) });
  }
  roomClick(room: any): void {
    if (room.current) this.openFolioByReservation(room.current.id);
    else if (room.isOutOfOrder) this.notice('Phòng đang khóa bảo trì', true);
    else if (room.housekeepingStatus !== 'INSPECTED') this.setView('housekeeping');
    else this.openModal('booking', { roomId: room.id, nightlyRate: room.baseRate });
  }
}
function localIsoDate(offset: number): string {
  const d = new Date(); d.setDate(d.getDate() + offset);
  const yyyy = d.getFullYear(); const mm = String(d.getMonth() + 1).padStart(2, '0'); const dd = String(d.getDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}
