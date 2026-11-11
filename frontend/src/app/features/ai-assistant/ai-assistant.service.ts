import { Injectable } from '@angular/core';
import { ApiService } from '../../core/services/api.service';
import { AiChatResponse } from './ai-assistant.models';

@Injectable({ providedIn: 'root' })
export class AiAssistantService {
  constructor(private api: ApiService) {}

  chat(message: string, conversationId: string | null, module: string, propertyId: number) {
    return this.api.post<AiChatResponse>('/ai/chat', {
      conversationId,
      message,
      context: { module, pageTitle: module, propertyId, locale: 'vi' }
    });
  }

  conversations() { return this.api.get<any[]>('/ai/conversations'); }
  conversation(id: string) { return this.api.get<any>(`/ai/conversations/${id}`); }
  removeConversation(id: string) { return this.api.delete<any>(`/ai/conversations/${id}`); }
  feedback(messageId: string, helpful: boolean) { return this.api.post('/ai/feedback', { messageId, helpful, note: '' }); }

  upload(file: File, title: string, module: string, propertyId: number) {
    const body = new FormData();
    body.append('file', file, file.name);
    body.append('title', title);
    body.append('module', module);
    body.append('version', '1');
    if (propertyId) body.append('propertyId', String(propertyId));
    return this.api.postForm<any>('/admin/ai/knowledge/upload', body);
  }
}
