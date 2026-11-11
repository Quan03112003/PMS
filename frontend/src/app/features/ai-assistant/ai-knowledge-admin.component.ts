import { CommonModule } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { AiAssistantService } from './ai-assistant.service';

@Component({
  selector: 'app-ai-knowledge-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-knowledge-admin.component.html',
  styleUrl: './ai-knowledge-admin.component.css'
})
export class AiKnowledgeAdminComponent implements OnInit {
  @Input() propertyId = 0;
  docs: any[] = [];
  golden: any[] = [];
  runs: any[] = [];
  notice = '';
  title = '';
  module = 'OTHER';
  question = { datasetCode: 'default', module: 'OTHER', intent: 'HOW_TO', question: '', questionType: 'PROCEDURE', difficulty: 'MEDIUM', answerable: 'ANSWERABLE', expectedAnswer: '', expectedKeywords: '', questionVariations: '', evaluationCriteria: '', sourceDocumentIds: '', sourceVersion: '' };

  constructor(private api: ApiService, private ai: AiAssistantService) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.api.get<any[]>(`/admin/ai/knowledge?propertyId=${this.propertyId || ''}`).subscribe(rows => this.docs = rows);
    this.api.get<any[]>('/admin/ai/golden-dataset').subscribe(rows => this.golden = rows);
    this.api.get<any[]>('/admin/ai/evaluations').subscribe(rows => this.runs = rows);
  }

  upload(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.ai.upload(file, this.title || file.name, this.module, this.propertyId).subscribe({
      next: res => { this.notice = res.message; this.reload(); },
      error: err => this.notice = err?.error?.message || 'Không tải được tệp'
    });
  }

  act(id: string, action: 'approve' | 'reject' | 'reindex'): void {
    this.api.post(`/admin/ai/knowledge/${id}/${action}`, {}).subscribe({ next: () => this.reload(), error: err => this.notice = err?.error?.message || 'Không thực hiện được' });
  }

  remove(id: string): void {
    this.api.delete(`/admin/ai/knowledge/${id}`).subscribe({ next: () => this.reload() });
  }

  saveGolden(): void {
    this.api.post('/admin/ai/golden-dataset', this.question).subscribe({
      next: () => { this.notice = 'Đã lưu mẫu ở trạng thái nháp.'; this.reload(); },
      error: err => this.notice = err?.error?.message || 'Không lưu được mẫu'
    });
  }

  approveGolden(id: string): void {
    this.api.post(`/admin/ai/golden-dataset/${id}/approve`, {}).subscribe({ next: () => this.reload() });
  }

  importCsv(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const body = new FormData();
    body.append('file', file, file.name);
    this.api.postForm('/admin/ai/golden-dataset/import', body).subscribe({
      next: () => { this.notice = 'Đã nhập CSV ở trạng thái nháp.'; this.reload(); },
      error: err => this.notice = err?.error?.message || 'Không nhập được CSV'
    });
  }

  exportCsv(): void {
    this.api.getText('/admin/ai/golden-dataset/export').subscribe(text => {
      const url = URL.createObjectURL(new Blob([text], { type: 'text/csv' }));
      const link = document.createElement('a');
      link.href = url;
      link.download = 'golden-dataset.csv';
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  run(): void {
    this.api.post<any>(`/admin/ai/evaluations/run?propertyId=${this.propertyId || ''}`, {}).subscribe({
      next: res => { this.notice = `Chấm truy xuất: ${res.passed}/${res.total} đạt.`; this.reload(); },
      error: err => this.notice = err?.error?.message || 'Không chạy được đánh giá'
    });
  }
}
