import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AiTurn } from './ai-assistant.models';
import { AiAssistantService } from './ai-assistant.service';

@Component({
  selector: 'app-ai-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-assistant-widget.component.html',
  styleUrl: './ai-assistant-widget.component.css'
})
export class AiAssistantWidgetComponent {
  @Input() open = false;
  @Input() view = 'dashboard';
  @Input() propertyId = 0;
  @Output() opened = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();
  draft = '';
  busy = false;
  error = '';
  conversationId: string | null = null;
  turns: AiTurn[] = [];
  suggestions = ['Chức năng này dùng để làm gì?', 'Lỗi thường gặp là gì?'];
  history: { id: string; module: string }[] = [];

  constructor(private ai: AiAssistantService) {}

  get module(): string {
    const map: Record<string, string> = {
      dashboard: 'DASHBOARD', bookings: 'BOOKING', roommap: 'ROOM_MANAGEMENT', stays: 'CHECK_IN',
      housekeeping: 'HOUSEKEEPING', shifts: 'CASHIER', debts: 'DEBT', reports: 'REPORT', guests: 'GUEST', ota: 'OTA'
    };
    return map[this.view] || 'OTHER';
  }

  toggleHistory(): void {
    this.ai.conversations().subscribe({
      next: rows => this.history = rows,
      error: () => this.error = 'Không tải được lịch sử'
    });
  }

  openHistory(id: string): void {
    this.ai.conversation(id).subscribe({
      next: data => {
        this.conversationId = id;
        this.turns = (data.messages || []).map((m: any) => ({ id: m.id, role: m.role, content: m.content, citations: this.readCitations(m.citationsJson) }));
        this.error = '';
      },
      error: () => this.error = 'Không mở được hội thoại'
    });
  }

  onKey(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  send(text = this.draft): void {
    const message = text.trim();
    if (!message || this.busy) return;
    this.draft = '';
    this.error = '';
    this.busy = true;
    this.turns = [...this.turns, { role: 'user', content: message }];
    this.ai.chat(message, this.conversationId, this.module, this.propertyId).subscribe({
      next: res => {
        this.conversationId = res.conversationId;
        this.suggestions = res.suggestedQuestions?.length ? res.suggestedQuestions : this.suggestions;
        this.turns = [...this.turns, { id: res.message.id, role: 'assistant', content: res.message.content, citations: res.citations }];
        this.busy = false;
      },
      error: (err) => {
        this.error = err?.error?.message || 'Trợ lý chưa trả lời được';
        this.busy = false;
      }
    });
  }

  retry(): void {
    const last = [...this.turns].reverse().find(x => x.role === 'user');
    if (last) this.send(last.content);
  }

  rate(turn: AiTurn, helpful: boolean): void {
    if (!turn.id) return;
    this.ai.feedback(turn.id, helpful).subscribe({ error: () => this.error = 'Chưa lưu được đánh giá' });
  }

  copy(content: string): void {
    navigator.clipboard?.writeText(content).catch(() => { this.error = 'Không sao chép được'; });
  }

  private readCitations(raw: string): AiTurn['citations'] {
    try { return JSON.parse(raw || '[]'); } catch { return []; }
  }
}
