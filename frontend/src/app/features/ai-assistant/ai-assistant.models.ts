export interface AiCitation {
  documentId: string;
  title: string;
  page: number | null;
  section: string;
  excerpt: string;
}

export interface AiChatResponse {
  conversationId: string;
  message: { id: string; content: string; createdAt: string };
  citations: AiCitation[];
  requiresEscalation: boolean;
  suggestedQuestions: string[];
}

export interface AiTurn {
  id?: string;
  role: 'user' | 'assistant';
  content: string;
  citations?: AiCitation[];
}
