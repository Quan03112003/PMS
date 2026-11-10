import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { openCompanySheet } from './company-sheet';
import { AdvancedComponent } from './advanced.component';
import { OtaComponent } from './ota.component';
import { RoadmapComponent } from './roadmap.component';

@Component({ selector: 'app-root', standalone: true, imports: [CommonModule, FormsModule, AdvancedComponent, OtaComponent, RoadmapComponent], templateUrl: './app.component.html' })
export class AppComponent implements OnInit, OnDestroy {
  user: any = null;
  token = '';
  loginEmail = 'reception@pms.local';
  loginPassword = 'Demo@12345';
  loginBusy = false;
  clockNow = new Date();
  private clockTimer: ReturnType<typeof setInterval> | null = null;
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
  bookingLayout: 'list' | 'tape' = 'list';
  tapeStart = localIsoDate(0);
  housekeepingTasks: any[] = [];
  priceQuote: any = null;
  priceTouched = false;
  deskNotes: any[] = [];
  closeout: any = { warnings: [], holds: [], notes: [], audits: [], refunds: [], alerts: [], noteTypes: [], integrations: null };
  depositForm = { reservationId: 0, amount: 0, dueAt: '', policy: 'STANDARD' };
  noteForm = { reservationId: 0, noteTypeId: 0, text: '' };
  holdForm = { roomId: 0, guestName: '', phone: '', checkInDate: '', checkOutDate: '', holdMinutes: 30 };
  auditDate = '';
  refundForm = { paymentId: 0, amount: 0, reasonCode: 'GUEST', reasonText: '' };
  reopenReason = '';
  deskNoteText = '';
  roomMenu: any = null;

  advancedViews = ['waitlist','groups','transfers','rates','buildings','stockledger','suppliers','retail','vouchers','debts','assets','dayclose','chain','collections','receipts','agencyReports','shiftReconcile','registration','pricingQuote',
    'adjustmentVouchers','taxRules','holidayRates','eInvoices'];

  // Branch codes follow the requirement groups (A–I) of docs/LOTUS_PMS_V4_DoiChieu.xlsx.
  menuTree: MenuNode[] = [
    { id: 'dashboard', label: 'Bảng điều khiển', icon: 'dash' },
    { key: 'A', code: 'A', label: 'Đặt phòng & lễ tân', icon: 'desk', children: [
      { key: 'A1', code: 'A1', label: 'Đặt phòng', children: [
        { id: 'bookings', label: 'Danh sách đặt phòng' },
        { id: 'pricingQuote', label: 'Báo giá & phụ thu' },
        { id: 'waitlist', label: 'Phòng hàng đợi' },
        { id: 'groups', label: 'Khách đoàn' },
        { id: 'closeout', label: 'Cọc, giữ phòng, chốt ngày' }
      ] },
      { key: 'A2', code: 'A2', label: 'Lưu trú', children: [
        { id: 'roommap', label: 'Sơ đồ phòng' },
        { id: 'stays', label: 'Khách đang ở' },
        { id: 'transfers', label: 'Chuyển dịch vụ' },
        { id: 'assets', label: 'Tài sản khách gửi' },
        { id: 'registration', label: 'Danh sách lưu trú' }
      ] }
    ] },
    { key: 'B', code: 'B', label: 'Kênh bán OTA & đại lý', icon: 'ota', children: [
      { id: 'ota', label: 'Kết nối OTA' },
      { id: 'agencies', label: 'Đại lý & kênh' },
      { id: 'agencyReports', label: 'Hiệu quả đại lý' }
    ] },
    { key: 'C', code: 'C', label: 'Giá & khuyến mại', icon: 'rate', children: [
      { id: 'rates', label: 'Bảng giá giờ/ngày/đêm' },
      { id: 'holidayRates', label: 'Giá lễ tết & cao điểm' },
      { id: 'taxRules', label: 'Quy tắc thuế' }
    ] },
    { key: 'D', code: 'D', label: 'Thanh toán & công nợ', icon: 'cash', children: [
      { key: 'D1', code: 'D1', label: 'Thu ngân', children: [
        { id: 'shifts', label: 'Ca thu ngân' },
        { id: 'shiftReconcile', label: 'Đối soát ca' },
        { id: 'vouchers', label: 'Phiếu thu / phiếu chi' }
      ] },
      { key: 'D2', code: 'D2', label: 'Công nợ & điều chỉnh', children: [
        { id: 'debts', label: 'Công nợ / khôi phục' },
        { id: 'adjustmentVouchers', label: 'Phiếu điều chỉnh folio' },
        { id: 'eInvoices', label: 'Hóa đơn điện tử (nháp)' }
      ] }
    ] },
    { key: 'E', code: 'E', label: 'Buồng phòng & dịch vụ', icon: 'hk', children: [
      { id: 'housekeeping', label: 'Buồng phòng' },
      { id: 'services', label: 'Dịch vụ & minibar' },
      { id: 'retail', label: 'Bán hàng ngoài phòng' }
    ] },
    { key: 'F', code: 'F', label: 'Kho & nhà cung cấp', icon: 'stock', children: [
      { id: 'stockledger', label: 'Nhập xuất tồn kho' },
      { id: 'suppliers', label: 'Nhà cung cấp' }
    ] },
    { key: 'G', code: 'G', label: 'Khách hàng', icon: 'guest', children: [
      { id: 'guests', label: 'Hồ sơ khách hàng' }
    ] },
    { key: 'H', code: 'H', label: 'Báo cáo', icon: 'report', children: [
      { id: 'reports', label: 'Báo cáo doanh thu' },
      { id: 'receipts', label: 'Phiếu thanh toán' },
      { id: 'collections', label: 'Thu tiền & tạm ứng' },
      { id: 'chain', label: 'Báo cáo chuỗi' },
      { id: 'dayclose', label: 'Snapshot chốt ngày' }
    ] },
    { key: 'I', code: 'I', label: 'Hệ thống', icon: 'sys', children: [
      { key: 'I1', code: 'I1', label: 'Cơ sở & phòng', children: [
        { id: 'properties', label: 'Cơ sở khách sạn' },
        { id: 'manageRooms', label: 'Phòng & hạng phòng' },
        { id: 'buildings', label: 'Tòa nhà' }
      ] },
      { id: 'users', label: 'Nhân viên & quyền' },
      { id: 'audit', label: 'Nhật ký thao tác' },
      { id: 'roadmap', label: 'Lộ trình yêu cầu' }
    ] }
  ];
  openBranches = new Set<string>(JSON.parse(localStorage.getItem('pms_menu_open') || '[]'));
  menuSearch = '';

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.expandTo(this.view);
    this.clockTimer = setInterval(() => { this.clockNow = new Date(); }, 1000);
    const token = localStorage.getItem('pms_token');
    const storedUser = localStorage.getItem('pms_user');
    if (token && storedUser) {
      try { this.token = token; this.user = JSON.parse(storedUser); this.loadProperties(); }
      catch { this.logout(); }
    }
  }

  ngOnDestroy(): void {
    if (this.clockTimer) clearInterval(this.clockTimer);
  }

  get isManager(): boolean { return ['CHAIN_ADMIN', 'PROPERTY_MANAGER'].includes(this.user?.role); }
  get isChainAdmin(): boolean { return this.user?.role === 'CHAIN_ADMIN'; }
  get canMaintain(): boolean { return this.isManager || this.user?.role === 'MAINTENANCE_AGENT'; }
  canView(itemId: string): boolean {
    if (this.isManager) return itemId !== 'properties' || this.isChainAdmin;
    const roleViews: Record<string, string[]> = {
      FRONT_DESK_AGENT: ['dashboard','roommap','bookings','groups','closeout','waitlist','transfers','debts','assets','stays','housekeeping','shifts','guests','pricingQuote','ota'],
      SALES_AGENT: ['dashboard','roommap','bookings','guests','agencies','rates','pricingQuote','ota','agencyReports'],
      HOUSEKEEPING_AGENT: ['dashboard','roommap','housekeeping'],
      MAINTENANCE_AGENT: ['dashboard','roommap','housekeeping','manageRooms'],
      ACCOUNTING_AGENT: ['dashboard','bookings','closeout','stays','shifts','vouchers','debts','reports','dayclose','shiftReconcile','guests']
    };
    return roleViews[this.user?.role || '']?.includes(itemId) ?? false;
  }
  departmentName(role: string): string {
    const names: Record<string, string> = {
      CHAIN_ADMIN: 'Quản trị toàn chuỗi', PROPERTY_MANAGER: 'Quản lý khách sạn', FRONT_DESK_AGENT: 'Lễ tân',
      SALES_AGENT: 'Kinh doanh', HOUSEKEEPING_AGENT: 'Buồng phòng', MAINTENANCE_AGENT: 'Bảo trì', ACCOUNTING_AGENT: 'Kế toán'
    };
    return names[role] || role;
  }
  propertyName(propertyId: number | null): string { return this.properties.find(p => p.id === propertyId)?.name || 'Toàn chuỗi'; }
  get creatableRoles(): { value: string; label: string }[] {
    const departments = [
      { value: 'FRONT_DESK_AGENT', label: 'Lễ tân' }, { value: 'SALES_AGENT', label: 'Kinh doanh' },
      { value: 'HOUSEKEEPING_AGENT', label: 'Buồng phòng' }, { value: 'MAINTENANCE_AGENT', label: 'Bảo trì' },
      { value: 'ACCOUNTING_AGENT', label: 'Kế toán' }
    ];
    return this.isChainAdmin ? [{ value: 'CHAIN_ADMIN', label: 'Quản trị toàn chuỗi' }, { value: 'PROPERTY_MANAGER', label: 'Quản lý khách sạn' }, ...departments] : departments;
  }
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
      }, error: e => {
        this.loginBusy = false;
        if (e?.status === 401) this.notice('Email hoặc mật khẩu không đúng', true);
        else this.onError(e);
      }
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
  setView(v: string): void { this.view = v; this.sidebarOpen = false; this.filter = ''; this.expandTo(v); this.refresh(); }
  nodeVisible(node: MenuNode): boolean {
    if (node.children) return node.children.some(c => this.nodeVisible(c));
    if (!this.canView(node.id!)) return false;
    const q = this.menuSearch.trim().toLowerCase();
    return !q || node.label.toLowerCase().includes(q);
  }
  get anyMenuMatch(): boolean { return this.menuTree.some(n => this.nodeVisible(n)); }
  isOpen(node: MenuNode): boolean { return !!this.menuSearch.trim() || this.openBranches.has(node.key!); }
  toggleBranch(node: MenuNode): void {
    if (this.openBranches.has(node.key!)) this.openBranches.delete(node.key!); else this.openBranches.add(node.key!);
    localStorage.setItem('pms_menu_open', JSON.stringify([...this.openBranches]));
  }
  branchActive(node: MenuNode): boolean { return !!node.children?.some(c => c.id === this.view || this.branchActive(c)); }
  private expandTo(viewId: string, nodes = this.menuTree): boolean {
    for (const n of nodes) {
      if (n.id === viewId) return true;
      if (n.children && this.expandTo(viewId, n.children)) { this.openBranches.add(n.key!); return true; }
    }
    return false;
  }
  private leaves(nodes = this.menuTree): MenuNode[] { return nodes.flatMap(n => n.children ? this.leaves(n.children) : [n]); }
  refresh(): void {
    if (!this.user || !this.propertyId) return;
    const id = this.propertyId;
    this.api.get(`/dashboard?propertyId=${id}`).subscribe({ next: r => this.dashboard = r, error: e => this.onError(e) });
    this.api.get<any[]>(`/rooms?propertyId=${id}`).subscribe({ next: r => this.rooms = r, error: e => this.onError(e) });
    this.api.get<any[]>(`/room-types?propertyId=${id}`).subscribe({ next: r => this.roomTypes = r, error: e => this.onError(e) });
    if (['bookings', 'dashboard', 'roommap'].includes(this.view) && this.canView('bookings')) this.api.get<any[]>(`/reservations?propertyId=${id}`).subscribe({ next: r => this.bookings = r, error: e => this.onError(e) });
    if (['housekeeping', 'dashboard'].includes(this.view)) this.api.get<any[]>(`/housekeeping-tasks?propertyId=${id}`).subscribe({ next: r => this.housekeepingTasks = r, error: e => this.onError(e) });
    if (this.view === 'dashboard') {
      this.api.get<any[]>(`/desk-notes?propertyId=${id}`).subscribe({ next: r => this.deskNotes = r, error: e => this.onError(e) });
      this.api.get<any>(`/operations/alerts?propertyId=${id}`).subscribe({ next: r => this.closeout.alerts = r.alerts || [], error: () => this.closeout.alerts = [] });
    }
    if (this.view === 'closeout') this.loadCloseout();
    if (['stays', 'dashboard', 'roommap'].includes(this.view) && this.canView('stays')) this.api.get<any[]>(`/stays?propertyId=${id}`).subscribe({ next: r => this.stays = r, error: e => this.onError(e) });
    if (['services', 'stays'].includes(this.view)) this.api.get<any[]>(`/services?propertyId=${id}`).subscribe({ next: r => this.services = r, error: e => this.onError(e) });
    if (this.view === 'users') this.api.get<any[]>(`/users?propertyId=${id}`).subscribe({ next: r => this.users = r, error: e => this.onError(e) });
    if (this.view === 'guests') this.api.get<any[]>(`/guests?propertyId=${id}`).subscribe({ next: r => this.guests = r, error: e => this.onError(e) });
    if (this.view === 'agencies') this.api.get<any[]>(`/agencies?propertyId=${id}`).subscribe({ next: r => this.agencies = r, error: e => this.onError(e) });
    if (this.view === 'shifts') this.api.get<any[]>(`/shifts?propertyId=${id}`).subscribe({ next: r => this.shifts = r, error: e => this.onError(e) });
    if (this.view === 'audit') this.api.get<any[]>(`/audit?propertyId=${id}`).subscribe({ next: r => this.audits = r, error: e => this.onError(e) });
    if (this.view === 'reports') this.loadReport();
  }
  title(): string { return this.leaves().find(i => i.id === this.view)?.label || 'PMS'; }
  statusText(status: string): string {
    const dict: Record<string, string> = { VACANT: 'Phòng trống', OCCUPIED: 'Đang ở', ARRIVAL: 'Khách sắp đến', DIRTY: 'Chưa dọn',
      MAINTENANCE: 'Bảo trì', CLEAN: 'Đã dọn', INSPECTED: 'Sẵn sàng', CONFIRMED: 'Đã xác nhận', CHECKED_IN: 'Đang ở',
      CHECKED_OUT: 'Đã trả', CANCELLED: 'Đã hủy', OPEN: 'Đang mở', CLOSED: 'Đã khóa', DIRECT: 'Trực tiếp', OTA: 'OTA',
      NEW: 'Đang dọn', PENDING: 'Chờ nghiệm thu', FINISH: 'Đã dọn xong', STAY: 'Dọn lưu', CHECKOUT: 'Dọn checkout' };
    return dict[status] || status;
  }
  badge(status: string): string { return status.toLowerCase().replaceAll('_', '-'); }

  openModal(type: string, values: any = {}): void {
    this.modal = type;
    this.form = { propertyId: this.propertyId, checkInDate: this.today, checkOutDate: localIsoDate(2), adults: 2, children: 0,
      bookingSource: 'DIRECT', bookingMode: 'OVERNIGHT', agencyId: null, selectedRatePlanId: null, nightlyRate: null, price: 0, unit: 'Lần', quantity: 1, trackInventory: false,
      stockQuantity: 0, openingAmount: 0, method: 'CASH', amount: 0, capacity: 2, floor: 1,
      title: 'Mr', gender: 'Nam', idDocumentType: 'CCCD', stayPurpose: 'Du lịch',
      commissionPercent: 0, ...values };
    if (type === 'booking') {
      this.priceTouched = false; this.priceQuote = null;
      this.searchAvailable();
      this.api.get<any[]>(`/rate-plans?propertyId=${this.propertyId}`).subscribe({next: x => this.ratePlans = x, error: e => this.onError(e)});
      this.api.get<any[]>(`/agencies?propertyId=${this.propertyId}`).subscribe({next: x => this.agencies = x, error: e => this.onError(e)});
    }
  }
  searchAvailable(): void {
    if (!this.form.checkInDate || !this.form.checkOutDate || this.form.checkInDate >= this.form.checkOutDate) { this.availableRooms = []; this.refreshQuote(); return; }
    const params = `propertyId=${this.propertyId}&startDate=${this.form.checkInDate}&endDate=${this.form.checkOutDate}`;
    this.api.get<any[]>('/availability?' + params).subscribe({ next: r => { this.availableRooms = r; this.refreshQuote(); }, error: e => this.onError(e) });
  }
  onBookingRoomChange(): void { this.refreshQuote(); }
  onRatePlanChange(): void {
    const selected = this.ratePlans.find(x => x.id === +this.form.selectedRatePlanId);
    if (selected) this.form.bookingMode = selected.mode;
    this.priceTouched = false;
    this.refreshQuote();
  }
  onPriceEdit(value: number): void {
    if (!this.priceTouched && this.priceQuote && Number(value) === Number(this.priceQuote.roomUnit)) return;
    this.priceTouched = value === null || value === undefined || (value as any) === '' ? false : true;
    this.refreshQuote();
  }
  refreshQuote(): void {
    if (this.modal !== 'booking' || !this.form.roomId || !this.form.checkInDate || !this.form.checkOutDate || this.form.checkInDate >= this.form.checkOutDate) { this.priceQuote = null; return; }
    const payload = {
      propertyId: this.propertyId, roomId: Number(this.form.roomId), checkInDate: this.form.checkInDate, checkOutDate: this.form.checkOutDate,
      bookingMode: this.form.bookingMode, adults: Number(this.form.adults || 1), children: Number(this.form.children || 0),
      plannedHours: this.form.bookingMode === 'HOURLY' && this.form.plannedHours ? Number(this.form.plannedHours) : null,
      roomRateOverride: this.priceTouched ? Number(this.form.nightlyRate) : null,
      ratePlanId: this.form.selectedRatePlanId ? Number(this.form.selectedRatePlanId) : null
    };
    this.api.post<any>('/pricing/room-quote', payload).subscribe({ next: q => { this.priceQuote = q; if (!this.priceTouched) this.form.nightlyRate = q.roomUnit; }, error: e => this.onError(e) });
  }
  chooseRoom(r: any): void { this.form.roomId = r.id; this.refreshQuote(); }
  createBooking(): void {
    if (!this.form.roomId || !this.form.guestName) { this.notice('Vui lòng chọn phòng và nhập tên khách', true); return; }
    const payload = { ...this.form, roomId: Number(this.form.roomId), adults: Number(this.form.adults || 1), children: Number(this.form.children || 0),
      nightlyRate: this.priceTouched ? Number(this.form.nightlyRate) : null,
      ratePlanId: this.form.selectedRatePlanId ? Number(this.form.selectedRatePlanId) : null,
      plannedHours: this.form.bookingMode === 'HOURLY' && this.form.plannedHours ? Number(this.form.plannedHours) : null };
    this.run(this.api.post('/reservations', payload), () => this.setView('bookings'), 'Đã tạo và xác nhận đặt phòng');
  }
  createRoomType(): void {
    const money = (v: any) => v === '' || v == null ? null : Number(v);
    this.run(this.api.post('/room-types', { ...this.form, baseRate: Number(this.form.baseRate), capacity: Number(this.form.capacity),
      weekdayRate: money(this.form.weekdayRate), weekendRate: money(this.form.weekendRate), hourlyRate: money(this.form.hourlyRate),
      extraHourRate: money(this.form.extraHourRate), extraPersonRate: money(this.form.extraPersonRate) }));
  }
  createRoom(): void { this.run(this.api.post('/rooms', { ...this.form, roomTypeId: Number(this.form.roomTypeId), floor: Number(this.form.floor) })); }
  createService(): void { this.run(this.api.post('/services', { ...this.form, price: Number(this.form.price), stockQuantity: Number(this.form.stockQuantity) })); }
  createProperty(): void { this.run(this.api.post('/properties', this.form), () => this.loadProperties(), 'Đã tạo cơ sở khách sạn'); }
  createUser(): void {
    const role = this.form.role || 'FRONT_DESK_AGENT';
    const payload = { ...this.form, role, propertyId: role === 'CHAIN_ADMIN' ? null : Number(this.form.propertyId || this.propertyId) };
    this.run(this.api.post('/users', payload));
  }
  editUser(user: any): void { this.openModal('editUser', { ...user, password: '' }); }
  updateUser(): void {
    const role = this.form.role;
    const payload = { ...this.form, role, propertyId: role === 'CHAIN_ADMIN' ? null : Number(this.form.propertyId), password: this.form.password || null };
    this.run(this.api.patch(`/users/${this.form.id}`, payload));
  }
  toggleUser(user: any): void {
    const action = user.isActive ? 'khóa' : 'mở khóa';
    if (!confirm(`Bạn muốn ${action} tài khoản ${user.email}?`)) return;
    this.run(this.api.patch(`/users/${user.id}`, { ...user, isActive: !user.isActive, password: null }), undefined,
      user.isActive ? 'Đã khóa tài khoản' : 'Đã mở khóa tài khoản');
  }
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
  transfer(): void {
    if (!this.form.newRoomId || !this.form.reasonCode) { this.notice('Chọn phòng mới và lý do', true); return; }
    if (this.form.reasonCode === 'OTHER' && !String(this.form.reasonNote || '').trim()) { this.notice('Lý do khác cần ghi chú', true); return; }
    const keepPrice = this.form.keepPrice !== false;
    this.run(this.api.post(`/stays/${this.folio.details.id}/transfer`, {
      newRoomId: Number(this.form.newRoomId), reasonCode: this.form.reasonCode, reasonNote: this.form.reasonNote || null, keepPrice
    }), () => this.openFolio(this.folio.details.id), keepPrice ? 'Đã đổi phòng. Giá đã khóa được giữ nguyên.' : 'Đã đổi phòng và ghi chênh lệch đêm còn lại.');
  }
  housekeeping(room: any, status: string): void { this.run(this.api.patch(`/rooms/${room.id}/housekeeping`, { status }), undefined, `Phòng ${room.number}: ${this.statusText(status)}`); }
  maintenance(room: any): void {
    if (!this.canMaintain) return;
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
  roomClick(room: any): void { this.roomMenu = this.roomMenu?.id === room.id ? null : room; }
  actRoom(action: string): void {
    const room = this.roomMenu;
    if (!room) return;
    if (action === 'folio' && room.current) this.openFolioByReservation(room.current.id);
    else if (action === 'checkin' && room.next) this.checkIn({ id: room.next.id, roomNumber: room.number, fullName: 'khách đặt trước' });
    else if (action === 'book') this.openModal('booking', { roomId: room.id, nightlyRate: room.baseRate, bookingMode: 'OVERNIGHT' });
    else if (action === 'hourly') this.openModal('booking', { roomId: room.id, nightlyRate: room.hourlyRate || room.baseRate, bookingMode: 'HOURLY', checkOutDate: localIsoDate(1) });
    else if (action === 'dirty') this.housekeeping(room, 'DIRTY');
    else if (action === 'clean') this.housekeeping(room, 'CLEAN');
    else if (action === 'inspected') this.housekeeping(room, 'INSPECTED');
    else if (action === 'repair') this.maintenance(room);
    else if (action === 'assets') this.setView('assets');
    this.roomMenu = null;
  }
  get tapeDays(): string[] { return Array.from({ length: 7 }, (_, i) => shiftIso(this.tapeStart, i)); }
  tapeCell(room: any, day: string): { text: string; cls: string } {
    const hit = this.bookings.find(b => b.roomId === room.id && b.status !== 'CANCELLED' && String(b.checkInDate).slice(0, 10) <= day && String(b.checkOutDate).slice(0, 10) > day);
    if (hit) return { text: hit.fullName, cls: hit.bookingMode === 'HOURLY' ? 'tape-hour' : hit.status === 'CHECKED_IN' ? 'tape-in' : 'tape-book' };
    if (room.housekeepingStatus === 'DIRTY') return { text: 'Đang dọn', cls: 'tape-dirty' };
    if (room.isOutOfOrder) return { text: 'Sửa chữa', cls: 'tape-ooo' };
    return { text: '', cls: '' };
  }
  shiftTape(days: number): void { this.tapeStart = shiftIso(this.tapeStart, days); }
  vnDate(value: string): string { const [y, m, d] = String(value).slice(0, 10).split('-'); return d && m && y ? `${d}/${m}/${y}` : value; }
  printSheet(id: string): void { openCompanySheet(id); }
  printConfirm(b: any): void {
    const page = window.open('', '_blank', 'noopener,width=760,height=900');
    if (!page) { this.notice('Trình duyệt đang chặn cửa sổ in', true); return; }
    const hotel = this.currentProperty?.name || 'Lotus PMS';
    page.document.write(`<!doctype html><html><head><title>Xác nhận ${b.code}</title><style>body{font-family:Segoe UI,sans-serif;padding:28px;color:#243044}h1{font-size:22px;margin:0}table{width:100%;border-collapse:collapse;margin-top:16px}td{padding:8px 0;border-bottom:1px solid #e6ebf2}small{color:#667085}</style></head><body><small>RESERVATION CONFIRMATION / XÁC NHẬN ĐẶT PHÒNG</small><h1>${hotel}</h1><table><tr><td>Mã / Code</td><td><b>${b.code}</b></td></tr><tr><td>Khách / Guest</td><td>${b.fullName}</td></tr><tr><td>Phòng / Room</td><td>${b.roomNumber}</td></tr><tr><td>Nhận – trả / Stay</td><td>${b.checkInDate} → ${b.checkOutDate}</td></tr><tr><td>Giá / Rate</td><td>${Number(b.nightlyRate).toLocaleString('vi-VN')} ₫</td></tr><tr><td>Trạng thái / Status</td><td>${this.statusText(b.status)}</td></tr></table><p><small>In từ Lotus PMS. Đây là xác nhận nội bộ, chưa phải hóa đơn.</small></p></body></html>`);
    page.document.close();
    page.focus();
    page.print();
  }
  loadCloseout(): void {
    const id = this.propertyId;
    if (!this.auditDate) this.auditDate = localIsoDate(0);
    if (!this.holdForm.checkInDate) { this.holdForm.checkInDate = localIsoDate(0); this.holdForm.checkOutDate = localIsoDate(1); }
    this.api.get<any[]>(`/deposits/warnings?propertyId=${id}`).subscribe({ next: r => this.closeout.warnings = r, error: e => this.onError(e) });
    this.api.get<any>(`/reservation-holds?propertyId=${id}`).subscribe({ next: r => this.closeout.holds = r.holds || [], error: e => this.onError(e) });
    this.api.get<any[]>(`/booking-note-types?propertyId=${id}`).subscribe({ next: r => this.closeout.noteTypes = r, error: e => this.onError(e) });
    this.api.get<any[]>(`/night-audit?propertyId=${id}`).subscribe({ next: r => this.closeout.audits = r, error: e => this.onError(e) });
    this.api.get<any[]>(`/payment-refunds?propertyId=${id}`).subscribe({ next: r => this.closeout.refunds = r, error: e => this.onError(e) });
    this.api.get<any>(`/operations/alerts?propertyId=${id}`).subscribe({ next: r => this.closeout.alertSummary = r, error: e => this.onError(e) });
    if (this.isManager) this.api.get<any>('/integrations/readiness').subscribe({ next: r => this.closeout.integrations = r, error: e => this.onError(e) });
    this.api.get<any[]>(`/reservations?propertyId=${id}`).subscribe({ next: r => this.bookings = r, error: e => this.onError(e) });
  }
  saveDepositTerms(): void {
    this.run(this.api.patch(`/reservations/${this.depositForm.reservationId}/deposit-terms`, { amount: +this.depositForm.amount, dueAtUtc: this.depositForm.dueAt ? new Date(this.depositForm.dueAt).toISOString() : null, policyCode: this.depositForm.policy }), () => this.loadCloseout(), 'Đã lưu hạn cọc. Phiếu không bị hủy.');
  }
  scanDeposits(): void { this.run(this.api.post(`/deposits/reminders?propertyId=${this.propertyId}`, {}), () => this.loadCloseout(), 'Đã ghi nhắc cọc. Không hủy phiếu.'); }
  saveNote(): void {
    this.run(this.api.post(`/reservations/${this.noteForm.reservationId}/notes`, { noteTypeId: +this.noteForm.noteTypeId, noteText: this.noteForm.text }), () => { this.noteForm.text = ''; this.loadNotes(); }, 'Đã lưu ghi chú, bản cũ được giữ');
  }
  loadNotes(): void {
    if (!this.noteForm.reservationId) return;
    this.api.get<any[]>(`/reservations/${this.noteForm.reservationId}/notes`).subscribe({ next: r => this.closeout.notes = r, error: e => this.onError(e) });
  }
  createHold(): void {
    this.run(this.api.post('/reservation-holds', { propertyId: +this.propertyId, roomId: +this.holdForm.roomId, guestName: this.holdForm.guestName, guestPhone: this.holdForm.phone, checkInDate: this.holdForm.checkInDate, checkOutDate: this.holdForm.checkOutDate, holdMinutes: +this.holdForm.holdMinutes, idempotencyKey: crypto.randomUUID() }), () => this.loadCloseout(), 'Đã giữ phòng');
  }
  releaseHold(id: number): void { this.run(this.api.post(`/reservation-holds/${id}/release`, { reason: 'Lễ tân nhả phòng' }), () => this.loadCloseout(), 'Đã nhả phòng'); }
  convertHold(id: number): void { this.run(this.api.post(`/reservation-holds/${id}/convert`, {}), () => this.loadCloseout(), 'Đã chuyển giữ chỗ thành phiếu'); }
  runAudit(): void { this.run(this.api.post('/night-audit/run', { propertyId: +this.propertyId, businessDate: this.auditDate }), () => this.loadCloseout(), 'Đã chạy chốt ngày'); }
  lockAudit(id: number): void { this.run(this.api.post(`/night-audit/${id}/lock`, {}), () => this.loadCloseout(), 'Đã khóa ngày'); }
  reopenAudit(id: number): void { this.run(this.api.post(`/night-audit/${id}/reopen`, { reason: this.reopenReason }), () => this.loadCloseout(), 'Đã mở lại ngày'); }
  requestRefund(): void {
    this.run(this.api.post(`/payments/${this.refundForm.paymentId}/refund-requests`, { amount: +this.refundForm.amount, reasonCode: this.refundForm.reasonCode, reasonText: this.refundForm.reasonText, idempotencyKey: crypto.randomUUID() }), () => this.loadCloseout(), 'Đã lập phiếu hoàn, chờ người khác duyệt');
  }
  approveRefund(id: number): void { this.run(this.api.post(`/payment-refunds/${id}/approve`, {}), () => this.loadCloseout(), 'Đã ghi khoản hoàn'); }
  rejectRefund(id: number): void { this.run(this.api.post(`/payment-refunds/${id}/reject`, { reason: 'Không duyệt' }), () => this.loadCloseout(), 'Đã từ chối phiếu hoàn'); }
  saveThresholds(occ: string, revenue: string): void {
    this.run(this.api.patch(`/properties/${this.propertyId}/alert-thresholds`, { lowOccupancyPercent: +occ, lowRevenueAmount: +revenue }), () => this.loadCloseout(), 'Đã lưu ngưỡng cảnh báo');
  }
  toggleItem(item: any): void {
    this.run(this.api.patch(`/housekeeping-task-items/${item.id}`, { isDone: !item.isDone, note: item.note || '' }), undefined, item.isDone ? 'Đã bỏ hạng mục' : 'Đã xong hạng mục');
  }
  approveTask(task: any): void { this.run(this.api.post(`/housekeeping-tasks/${task.id}/approve`, {}), undefined, 'Đã nghiệm thu phòng'); }
  saveTask(task: any): void {
    this.run(this.api.patch(`/housekeeping-tasks/${task.id}`, { cleaner1: task.cleaner1, cleaner2: task.cleaner2, cleaner3: task.cleaner3, note: task.note }), undefined, 'Đã lưu phiếu dọn');
  }
  addDeskNote(): void {
    const text = this.deskNoteText.trim();
    if (!text) return;
    this.run(this.api.post('/desk-notes', { propertyId: this.propertyId, text }), () => { this.deskNoteText = ''; }, 'Đã ghi chú ca');
  }
  doneNote(note: any): void { this.run(this.api.post(`/desk-notes/${note.id}/done`, {}), undefined, 'Đã xong ghi chú'); }
}
export interface MenuNode { id?: string; key?: string; code?: string; label: string; icon?: string; children?: MenuNode[]; }
function localIsoDate(offset: number): string {
  const d = new Date(); d.setDate(d.getDate() + offset);
  return isoFromDate(d);
}
function shiftIso(iso: string, days: number): string {
  const d = new Date(`${iso}T12:00:00`); d.setDate(d.getDate() + days);
  return isoFromDate(d);
}
function isoFromDate(d: Date): string {
  const yyyy = d.getFullYear(); const mm = String(d.getMonth() + 1).padStart(2, '0'); const dd = String(d.getDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}
