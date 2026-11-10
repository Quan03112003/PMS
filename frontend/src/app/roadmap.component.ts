import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { REQUIREMENTS, Requirement } from './requirements.data';

// Delivered work that the source workbook (assessed against V4) does not know about yet.
const LOTUS_PROGRESS: Record<string, string> = {
  'YC-GIA-07': 'V5.0: có danh mục giá lễ tết (API + màn hình); chưa áp vào công thức tính giá',
  'YC-GIA-09': 'V5.0: lưu snapshot bảng giá khi gán vào booking; điều chỉnh qua phiếu điều chỉnh có duyệt',
  'YC-TC-08': 'V5.0: phiếu điều chỉnh folio maker-checker, ghi bút toán mới, không sửa giao dịch cũ',
  'YC-OTA-01': 'V5.0: nhận booking qua webhook ký số + xác nhận 2 lớp; chạy sandbox, chưa nối đối tác thật',
  'YC-OTA-02': 'V5.0: chống trùng theo mã kênh + dấu vân tay, đưa vào hàng đợi rà soát, không tự xóa',
  'YC-BS-01': 'V5.0: lớp tích hợp kênh (mapping, nhật ký, idempotency); cần API/sandbox Hotelink để kết nối thật'
};

@Component({
  selector: 'app-roadmap', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <div class="rm">
    <div class="rm-head">
      <div><div class="rm-eyebrow">LOTUS PMS · ĐỐI CHIẾU YÊU CẦU</div><h1>Lộ trình yêu cầu</h1>
        <p>{{ all.length }} mã yêu cầu từ <b>LOTUS_PMS_V4_DoiChieu.xlsx</b>. Giai đoạn là đề xuất, không phải cam kết đã hoàn thành.</p></div>
      <button class="rm-btn" (click)="exportCsv()">Xuất CSV ({{ rows.length }})</button>
    </div>
    <div class="rm-kpis">
      @for (p of phases; track p.name) {
        <button [class.on]="phase === p.name" (click)="phase = phase === p.name ? '' : p.name"><span>{{ p.name }}</span><strong>{{ p.count }}</strong></button>
      }
    </div>
    <div class="rm-filters">
      <input type="search" [(ngModel)]="q" placeholder="Tìm mã, tên, API…">
      <select [(ngModel)]="group"><option value="">Tất cả phân hệ</option>@for (g of groups; track g) {<option [value]="g">{{ g }}</option>}</select>
      <select [(ngModel)]="phase"><option value="">Tất cả giai đoạn</option>@for (p of phases; track p.name) {<option [value]="p.name">{{ p.name }}</option>}</select>
      <select [(ngModel)]="priority"><option value="">Mọi ưu tiên</option>@for (p of priorities; track p) {<option [value]="p">{{ p }}</option>}</select>
      <label><input type="checkbox" [(ngModel)]="onlyProgress"> Chỉ mục đã có tiến triển</label>
    </div>
    <div class="rm-table"><table>
      <thead><tr><th>Mã</th><th>Yêu cầu</th><th>Ưu tiên</th><th>Giai đoạn</th><th>Khả thi</th><th>Hiện trạng</th></tr></thead>
      <tbody>
        @for (r of rows; track r.code) {
          <tr class="rm-row" [class.open]="openCode === r.code" (click)="openCode = openCode === r.code ? '' : r.code">
            <td><b>{{ r.code }}</b><small>{{ r.group }}</small></td>
            <td class="rm-name">{{ r.name }}</td>
            <td><span class="rm-tag" [class]="'rm-tag p-' + r.priority">{{ r.priority }}</span></td>
            <td>{{ r.phase }}</td>
            <td>{{ r.feasibility }}</td>
            <td>@if (progress[r.code]) {<span class="rm-progress">{{ progress[r.code] }}</span>} @else { <span>{{ r.v4Status }}</span> }</td>
          </tr>
          @if (openCode === r.code) {
            <tr class="rm-detail"><td colspan="6"><div class="rm-grid">
              <div><h4>Hướng triển khai</h4><p>{{ r.approach || '—' }}</p><h4>Phụ thuộc / điều kiện</h4><p>{{ r.dependency || '—' }}</p><h4>API đề xuất</h4><p><code>{{ r.api || '—' }}</code></p></div>
              <div><h4>Quy tắc nghiệp vụ</h4><p>{{ r.rules || '—' }}</p><h4>Tiêu chí nghiệm thu</h4><p>{{ r.acceptance || '—' }}</p></div>
            </div></td></tr>
          }
        } @empty { <tr><td colspan="6" class="rm-empty">Không có yêu cầu phù hợp bộ lọc</td></tr> }
      </tbody></table></div>
  </div>`,
  styles: [`
    .rm-head{display:flex;justify-content:space-between;gap:16px;align-items:flex-start;margin-bottom:18px}.rm-head h1{font-family:var(--display,'Sora',sans-serif);letter-spacing:-.03em}.rm-head p{font-size:.84rem;color:#6c757d;margin:0}
    .rm-eyebrow{color:#3f6ad8;font-size:.68rem;font-weight:800;letter-spacing:.12em;margin-bottom:8px;text-transform:uppercase}
    .rm-btn{border:1px solid #d7dde5;background:#fff;border-radius:6px;padding:10px 16px;font-weight:800;font-size:.78rem;color:#3f4d67}
    .rm-btn:hover{border-color:#3f6ad8;color:#3f6ad8;background:#eaf0fb}
    .rm-kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:10px;margin-bottom:14px}
    .rm-kpis button{background:#fff;border:1px solid #eef1f5;border-radius:6px;padding:12px 14px;text-align:left;display:flex;flex-direction:column;gap:6px;box-shadow:0 .47rem 2.19rem rgba(63,77,103,.06);transition:transform .15s,border-color .15s}
    .rm-kpis button:hover{transform:translateY(-2px)}.rm-kpis button.on{border-color:#3f6ad8;background:#eaf0fb}.rm-kpis span{font-size:.72rem;color:#7a8499;font-weight:700;text-transform:uppercase;letter-spacing:.04em}.rm-kpis strong{font-family:var(--display,'Sora',sans-serif);font-size:1.4rem;color:#2b3348;letter-spacing:-.04em}
    .rm-filters{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:12px}
    .rm-filters input[type=search],.rm-filters select{border:1px solid #d7dde5;border-radius:6px;padding:9px 10px;font-size:.8rem;background:#fff;color:#3f4d67}
    .rm-filters input[type=search]{min-width:240px}.rm-filters label{font-size:.78rem;color:#5c6778;display:flex;gap:6px;align-items:center}
    .rm-table{background:#fff;border:1px solid #eef1f5;border-radius:6px;overflow:auto;box-shadow:0 .47rem 2.19rem rgba(63,77,103,.06)}
    .rm-table td{white-space:normal;vertical-align:top;padding:12px 14px}.rm-table td small{display:block;color:#98a2b3;font-size:.68rem;margin-top:3px;max-width:150px}
    .rm-row{cursor:pointer}.rm-row.open td{background:#f7faff}.rm-name{min-width:260px;color:#344054;font-weight:600}
    .rm-tag{padding:4px 8px;border-radius:4px;font-size:.68rem;font-weight:800;background:#eef1f5;color:#5c6778}.rm-tag.p-P1{background:#fde8ee;color:#d92550}.rm-tag.p-P2{background:#fff6db;color:#a07a08}.rm-tag.p-P3{background:#eaf0fb;color:#3f6ad8}
    .rm-progress{display:inline-block;background:#e6f8ef;color:#238a55;border-radius:4px;padding:4px 8px;font-weight:700;font-size:.72rem}
    .rm-detail td{background:#fafbfd}.rm-grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}.rm-grid h4{font-size:.68rem;letter-spacing:.06em;color:#3f6ad8;margin:8px 0 4px;text-transform:uppercase}.rm-grid p{font-size:.8rem;color:#5c6778;line-height:1.6;margin:0 0 6px}
    .rm-empty{text-align:center;color:#98a2b3;padding:30px}
    @media (max-width:900px){.rm-grid{grid-template-columns:1fr}}`]
})
export class RoadmapComponent {
  all = REQUIREMENTS;
  progress = LOTUS_PROGRESS;
  q = ''; group = ''; phase = ''; priority = ''; onlyProgress = false; openCode = '';
  groups = [...new Set(this.all.map(r => r.group))];
  priorities = [...new Set(this.all.map(r => r.priority))].sort();
  phases = [...new Set(this.all.map(r => r.phase))].sort().map(name => ({ name, count: this.all.filter(r => r.phase === name).length }));

  get rows(): Requirement[] {
    const q = this.q.trim().toLowerCase();
    return this.all.filter(r => (!this.group || r.group === this.group) && (!this.phase || r.phase === this.phase) &&
      (!this.priority || r.priority === this.priority) && (!this.onlyProgress || !!this.progress[r.code]) &&
      (!q || `${r.code} ${r.name} ${r.api} ${r.approach}`.toLowerCase().includes(q)));
  }

  exportCsv(): void {
    const head = ['Mã', 'Phân hệ', 'Yêu cầu', 'Ưu tiên', 'Giai đoạn', 'Khả thi', 'Hiện trạng', 'Hướng triển khai', 'API đề xuất'];
    const lines = [head, ...this.rows.map(r => [r.code, r.group, r.name, r.priority, r.phase, r.feasibility, this.progress[r.code] || r.v4Status, r.approach, r.api])];
    const csv = '\uFEFF' + lines.map(l => l.map(x => `"${String(x ?? '').replace(/^([=+@-])/, "'$1").replaceAll('"', '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a'); a.href = url; a.download = 'lo-trinh-yeu-cau.csv'; a.click(); URL.revokeObjectURL(url);
  }
}
