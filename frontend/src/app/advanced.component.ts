import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from './api.service';

function day(delta = 0): string {
  const d = new Date(); d.setDate(d.getDate() + delta);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

@Component({ selector: 'app-pms-advanced', standalone: true, imports: [CommonModule, FormsModule],
  templateUrl: './advanced.component.html', styleUrl: './advanced.component.css' })
export class AdvancedComponent implements OnChanges {
  @Input() propertyId = 0;
  @Input() view = 'waitlist';
  @Input() user: any = null;
  @Input() properties: any[] = [];
  items: any[] = [];
  rooms: any[] = [];
  stays: any[] = [];
  services: any[] = [];
  suppliers: any[] = [];
  buildings: any[] = [];
  bookings: any[] = [];
  folio: any = null;
  form: any = {};
  queueRooms: any[] = [];
  groupRooms: any[] = [];
  selectedRooms: number[] = [];
  submitting = false;
  error = '';
  success = '';
  from = day(-7);
  to = day();
  report: any = null;
  agencyReport: any = null;
  selectedShift = 0;
  reconcile: any = null;
  selectedGroup = '';
  groupRoomOccupants: any[] = [];
  selectedGroupReservation = 0;
  occupantForm: any = { fullName: '', phone: '', nationality: '', identityLast4: '', isPrimary: true };
  groupMethod = 'TRANSFER';
  groupAmount = 0;
  selectedStay = 0;
  selectedCharge = 0;
  targetStay = 0;
  transferQty = 1;
  selectedWaitlistId = 0;
  selectedRoomId = 0;
  ratePlans: any[] = [];
  quoteForm: any = { ratePlanId: null, checkInDate: day(), checkOutDate: day(1), actualCheckInLocal: '', actualCheckOutLocal: '' };
  quoteResult: any = null;
  adjustForm: any = { stayId: null, ratePlanId: null, kind: 'LATE_CHECKOUT', actualAtLocal: '' };
  precheckResult: any = null;
  prechecking = false;
  titles: Record<string, string> = {
    waitlist: 'Đặt phòng hàng đợi', groups: 'Khách đoàn & thanh toán hộ', transfers: 'Chuyển dịch vụ giữa các phòng',
    rates: 'Bảng giá giờ / ngày / đêm', buildings: 'Tòa nhà & phân phòng', stockledger: 'Nhập xuất tồn kho',
    suppliers: 'Nhà cung cấp', retail: 'Bán hàng ngoài phòng', vouchers: 'Phiếu thu – phiếu chi',
    debts: 'Công nợ khách hàng', assets: 'Tài sản khách gửi', dayclose: 'Snapshot chốt ngày',
    chain: 'Tổng hợp toàn chuỗi', agencyReports: 'Báo cáo hiệu quả đại lý', shiftReconcile: 'Đối soát tiền mặt theo ca', registration: 'Danh sách khai báo lưu trú',
    pricingQuote: 'Báo giá & phụ thu',
    adjustmentVouchers: 'Phiếu điều chỉnh',
    nightAudit: 'Chốt đêm',
    taxRules: 'Quy tắc thuế',
    eInvoices: 'Hóa đơn điện tử'
  };
  constructor(private api: ApiService) { this.resetForm(); }
  get canManage(): boolean { return ['CHAIN_ADMIN', 'PROPERTY_MANAGER'].includes(this.user?.role); }
  get canManageFinance(): boolean { return this.canManage || this.user?.role === 'ACCOUNTING_AGENT'; }
  get isChainAdmin(): boolean { return this.user?.role === 'CHAIN_ADMIN'; }
  get reportTotals(): any {
    const p = this.report?.properties ?? [];
    return { roomRevenue: p.reduce((a: number, x: any) => a + x.roomRevenue, 0),
      serviceRevenue: p.reduce((a: number, x: any) => a + x.serviceRevenue, 0),
      retailRevenue: p.reduce((a: number, x: any) => a + x.retailRevenue, 0),
      folioPayments: p.reduce((a: number, x: any) => a + x.folioPayments, 0),
      outstandingDebt: p.reduce((a: number, x: any) => a + x.outstandingDebt, 0) };
  }
  ngOnChanges(): void { this.resetForm(); this.selectedRooms = []; if (this.view === 'vouchers') this.form.type = 'EXPENSE'; if (this.propertyId) this.load(); }
  resetForm(): void {
    this.form = { propertyId: this.propertyId, checkInDate: day(), checkOutDate: day(1), businessDate: day(),
      type: 'IN', method: 'TRANSFER', paymentMethod: 'TRANSFER', quantity: 1, quantityChange: 1,
      unitCost: 0, amount: 0, includedHours: 2, basePrice: 0, extraHourPrice: 0,
      mode: 'OVERNIGHT', adults: 1, roomTypeId: null, supplierId: null };
  }
  fetch(path: string, field = 'items'): void {
    this.api.get<any>(path).subscribe({ next: x => (this as any)[field] = x,
      error: e => this.error = e?.error?.message || `Không tải được ${path}` });
  }
  load(): void {
    this.error = ''; const id = this.propertyId;
    const paths: Record<string, string> = {
      waitlist: '/waitlist', groups: '/groups', rates: '/rate-plans', buildings: '/buildings',
      stockledger: '/stock/movements', suppliers: '/suppliers', retail: '/retail-sales',
      vouchers: '/vouchers', debts: '/debts', assets: '/guest-assets', dayclose: '/daily-closes'
    };
    if (paths[this.view]) this.fetch(`${paths[this.view]}?propertyId=${id}`);
    if (['waitlist', 'groups', 'buildings'].includes(this.view)) this.fetch(`/rooms?propertyId=${id}`, 'rooms');
    if (['waitlist', 'groups'].includes(this.view)) this.fetch(`/room-types?propertyId=${id}`, 'buildings');
    if (['stockledger', 'retail'].includes(this.view)) this.fetch(`/services?propertyId=${id}`, 'services');
    if (this.view === 'stockledger') this.fetch(`/suppliers?propertyId=${id}`, 'suppliers');
    if (['debts', 'assets', 'transfers'].includes(this.view)) this.fetch(`/stays?propertyId=${id}`, 'stays');
    if (this.view === 'transfers') this.fetch(`/rooms?propertyId=${id}`, 'rooms');
    if (this.view === 'debts') this.fetch(`/checkouts?propertyId=${id}`, 'bookings');
    if (this.view === 'registration') this.fetch(`/reservations?propertyId=${id}`, 'items');
    if (this.view === 'chain') this.loadReport();
    if (this.view === 'agencyReports') this.loadAgencyReport();
    if (this.view === 'shiftReconcile') this.fetch(`/shifts?propertyId=${id}`, 'items');
    if (this.view === 'groups') this.searchRooms();
    if (this.view === 'pricingQuote') {
      this.fetch(`/rate-plans?propertyId=${id}`, 'ratePlans');
      this.fetch(`/stays?propertyId=${id}`, 'stays');
    }
  }
  calculateQuote(): void {
    this.error = ''; this.success = '';
    if (!this.quoteForm.ratePlanId) { this.error = 'Vui lòng chọn bảng giá'; return; }
    const body: any = {
      propertyId: +this.propertyId,
      ratePlanId: +this.quoteForm.ratePlanId,
      checkInDate: this.quoteForm.checkInDate,
      checkOutDate: this.quoteForm.checkOutDate
    };
    if (this.quoteForm.actualCheckInLocal) body.actualCheckInLocal = this.quoteForm.actualCheckInLocal;
    if (this.quoteForm.actualCheckOutLocal) body.actualCheckOutLocal = this.quoteForm.actualCheckOutLocal;
    this.api.post('/pricing/quote', body).subscribe({
      next: r => { this.quoteResult = r; },
      error: e => { this.error = e?.error?.message || 'Lỗi tính báo giá'; }
    });
  }
  postAdjustment(): void {
    if (!this.canManage) { this.error = 'Chỉ Quản lý hoặc Admin mới có quyền ghi phụ thu vào folio.'; return; }
    if (!this.adjustForm.stayId) { this.error = 'Vui lòng chọn lượt lưu trú'; return; }
    if (!this.adjustForm.ratePlanId) { this.error = 'Vui lòng chọn bảng giá phụ thu'; return; }
    if (!this.adjustForm.actualAtLocal) { this.error = 'Vui lòng nhập thời gian thực tế'; return; }
    const stayId = +this.adjustForm.stayId;
    const body = {
      kind: this.adjustForm.kind,
      ratePlanId: +this.adjustForm.ratePlanId,
      actualAtLocal: this.adjustForm.actualAtLocal
    };
    this.submit(`/stays/${stayId}/pricing-adjustments`, body, () => {
      this.adjustForm = { stayId: null, ratePlanId: null, kind: 'LATE_CHECKOUT', actualAtLocal: '' };
    });
  }
  runPrecheck(): void {
    this.prechecking = true; this.error = '';
    const date = this.form.businessDate || day();
    this.api.get(`/night-audit/precheck?propertyId=${this.propertyId}&businessDate=${date}`).subscribe({
      next: res => { this.precheckResult = res; this.prechecking = false; },
      error: e => { this.error = e?.error?.message || 'Lỗi kiểm tra trước chốt'; this.prechecking = false; }
    });
  }
  submit(path: string, body: any, after?: () => void): void {
    if (this.submitting) return;
    this.submitting = true; this.error = ''; this.success = '';
    this.api.post(path, body).subscribe({ next: () => { this.submitting = false; this.success = 'Đã ghi nhận';
      this.resetForm(); after?.(); this.load(); }, error: e => { this.submitting = false; this.error = e?.error?.message || `Lỗi API (${e?.status || 'mất kết nối'})`; } });
  }
  createWaitlist(): void { this.submit('/waitlist', { ...this.form, roomTypeId: this.form.roomTypeId ? +this.form.roomTypeId : null }); }
  assignWaitlist(): void { this.submit(`/waitlist/${this.selectedWaitlistId}/assign`, { roomId: +this.selectedRoomId },
    () => { this.selectedWaitlistId = 0; this.selectedRoomId = 0; }); }
  selectQueue(item: any): void {
    this.selectedWaitlistId = item.id;
    const params = `propertyId=${this.propertyId}&startDate=${item.checkInDate}&endDate=${item.checkOutDate}`;
    this.api.get<any[]>(`/availability?${params}`).subscribe({next: r => this.queueRooms = r.filter(x => !item.roomTypeId || x.roomTypeId === item.roomTypeId), error: e => this.error = e?.error?.message || 'Không tải được phòng khả dụng' });
  }
  searchRooms(): void {
    if (this.form.checkInDate >= this.form.checkOutDate) { this.groupRooms = []; return; }
    this.api.get<any[]>(`/availability?propertyId=${this.propertyId}&startDate=${this.form.checkInDate}&endDate=${this.form.checkOutDate}`)
      .subscribe({ next: r => this.groupRooms = r, error: e => this.error = e?.error?.message || 'Không tải được phòng' });
  }
  toggleRoom(id: number): void { this.selectedRooms = this.selectedRooms.includes(id) ? this.selectedRooms.filter(x => x !== id) : [...this.selectedRooms, id]; }
  createGroup(): void { this.submit('/group-bookings', { ...this.form, roomIds: this.selectedRooms, bookingSource: 'DIRECT' }, () => this.selectedRooms = []); }
  selectGroup(code: string): void { this.selectedGroup = code; this.loadGroupOccupants(); }
  loadGroupOccupants(): void {
    if (!this.selectedGroup) return;
    this.api.get<any[]>(`/groups/${encodeURIComponent(this.selectedGroup)}/occupants?propertyId=${this.propertyId}`)
      .subscribe({ next: rows => this.groupRoomOccupants = rows, error: e => this.error = e?.error?.message || 'Không tải được khách đoàn theo phòng' });
  }
  addOccupant(): void {
    if (!this.selectedGroupReservation || !this.occupantForm.fullName?.trim()) { this.error = 'Chọn phòng và nhập tên khách ở thực tế'; return; }
    this.submit(`/reservations/${this.selectedGroupReservation}/occupants`, this.occupantForm,
      () => { this.loadGroupOccupants(); this.occupantForm = { fullName: '', phone: '', nationality: '', identityLast4: '', isPrimary: false }; });
  }
  groupAction(action: string): void {
    if (!this.selectedGroup) return;
    if (action === 'pay') this.submit(`/groups/${encodeURIComponent(this.selectedGroup)}/pay`, { propertyId: this.propertyId, amount: +this.groupAmount, method: this.groupMethod, idempotencyKey: crypto.randomUUID() });
    else this.submit(`/groups/${encodeURIComponent(this.selectedGroup)}/${action}?propertyId=${this.propertyId}`, {});
  }
  selectStay(id: number): void { this.selectedStay = +id; this.selectedCharge = 0; this.folio = null;
    if (id) this.api.get(`/stays/${id}/folio`).subscribe({ next: x => this.folio = x, error: e => this.error = e?.error?.message || 'Không tải được folio' }); }
  transferCharge(): void { this.submit(`/charges/${this.selectedCharge}/transfer`, { targetStayId: +this.targetStay, quantity: +this.transferQty }, () => this.selectStay(this.selectedStay)); }
  createRate(): void { this.submit('/rate-plans', { ...this.form, basePrice: +this.form.basePrice, includedHours: +this.form.includedHours, extraHourPrice: +this.form.extraHourPrice }); }
  createBuilding(): void { this.submit('/buildings', this.form); }
  assignBuilding(roomId: number, buildingId: any): void {
    this.api.patch(`/rooms/${roomId}/building`, { buildingId: buildingId ? +buildingId : null }).subscribe({ next: () => { this.success = 'Đã gán tòa nhà'; this.load(); }, error: e => this.error = e?.error?.message || 'Không thể gán tòa nhà' });
  }
  createSupplier(): void { this.submit('/suppliers', this.form); }
  createStockMove(): void {
    const q = this.form.type === 'ADJUST' ? +this.form.quantityChange : Math.abs(+this.form.quantityChange) * (this.form.type === 'OUT' ? -1 : 1);
    this.submit('/stock/movements', { ...this.form, serviceItemId: +this.form.serviceItemId, quantityChange: q,
      unitCost: +this.form.unitCost, supplierId: this.form.supplierId ? +this.form.supplierId : null });
  }
  createRetailSale(): void { this.submit('/retail-sales', { ...this.form, serviceItemId: +this.form.serviceItemId, quantity: +this.form.quantity }); }
  createVoucher(): void { this.submit('/vouchers', { ...this.form, amount: +this.form.amount }); }
  collectDebt(id: number): void { this.submit(`/debts/${id}/collect`, { amount: +this.form.amount, method: this.form.method, reference: 'COLLECT-DEBT', idempotencyKey: crypto.randomUUID() }); }
  debtCheckout(id: number): void { this.submit(`/stays/${id}/checkout-debt`, { note: this.form.note || 'Công nợ chờ thu' }); }
  restoreStay(id: number): void { this.submit(`/stays/${id}/restore`, {}); }
  createAsset(): void { this.submit('/guest-assets', { ...this.form, stayId: +this.form.stayId, quantity: +this.form.quantity }); }
  createClose(): void { this.submit('/daily-closes', { ...this.form, businessDate: this.form.businessDate }); }
  loadAgencyReport(): void { this.api.get(`/reports/agencies?propertyId=${this.propertyId}&from=${this.from}&to=${this.to}`)
    .subscribe({next: x => this.agencyReport = x, error: e => this.error = e?.error?.message || 'Không tải được hiệu quả đại lý'}); }
  loadReconcile(): void { if (!this.selectedShift) return; this.api.get(`/shifts/${this.selectedShift}/reconcile`)
    .subscribe({next: x => this.reconcile = x, error: e => this.error = e?.error?.message || 'Không đối soát được ca'}); }
  printPdf(): void { window.print(); }
  loadReport(): void { this.api.get(`/reports/chain?from=${this.from}&to=${this.to}`).subscribe({ next: x => this.report = x, error: e => this.error = e?.error?.message || 'Không tải được báo cáo' }); }
  exportRegistration(): void {
    this.api.getText(`/reports/registration-csv?propertyId=${this.propertyId}&from=${this.from}&to=${this.to}`).subscribe({ next: txt => {
      const url = URL.createObjectURL(new Blob([txt], { type: 'text/csv;charset=utf-8' }));
      const a = document.createElement('a'); a.href = url; a.download = `khai-bao-luu-tru-${this.propertyId}.csv`; a.click(); URL.revokeObjectURL(url);
      this.success = 'Đã xuất CSV (không phải biểu mẫu khai báo chính thức)';
    }, error: e => this.error = e?.error?.message || 'Không tải được CSV' });
  }
  exportChain(): void {
    const lines: unknown[][] = [['Cơ sở','Số phòng','Đang ở','Tiền phòng','Dịch vụ','Bán lẻ','Đã thu','Công nợ'], ...((this.report?.properties ?? []).map((p: any) =>
      [p.propertyName,p.rooms,p.inHouse,p.roomRevenue,p.serviceRevenue,p.retailRevenue,p.folioPayments,p.outstandingDebt]))];
    const csv = '\uFEFF' + lines.map(line => line.map(x => { const raw = String(x ?? ''); const safe = /^[=+@-]/.test(raw) && isNaN(Number(raw)) ? '\'' + raw : raw; return `"${safe.replaceAll('"','""')}"`; }).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a'); a.href = url; a.download = 'bao-cao-chuoi.csv'; a.click(); URL.revokeObjectURL(url);
  }
  currency(n: number): string { return (n || 0).toLocaleString('vi-VN') + ' ₫'; }
}
