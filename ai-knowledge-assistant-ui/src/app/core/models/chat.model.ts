import { PagedResponse } from './document.model';

export enum ChatMessageRole {
  User = 1,
  Assistant = 2,
  System = 3
}

export interface ChatAskRequest {
  question: string;
  conversationId?: string | null;
  documentId?: string | null;
  selectedDocumentIds?: string[] | null;
}

export interface ChatCitationDto {
  documentId: string;
  chunkId: string;
  chunkIndex: number;
  documentName: string;
  originalFileName: string;
  similarity: number;
  snippet: string;
  scoreType?: 'hybrid' | 'local-fallback' | 'document-coverage' | string;
}

export interface ChatResponse {
  conversationId: string;
  userMessageId: string;
  assistantMessageId: string;
  question: string;
  answer: string;
  createdAt: string;
  citations: ChatCitationDto[];
  sources?: ChatCitationDto[];
}

export interface ChatStreamEvent {
  type: 'token' | 'complete';
  token?: string | null;
  response?: ChatResponse | null;
}

export interface ConversationDto {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  isArchived: boolean;
}

export interface ConversationCreateRequest {
  title?: string | null;
}

export interface ConversationUpdateRequest {
  title: string;
}

export interface ChatMessageDto {
  id: string;
  conversationId: string;
  role: ChatMessageRole | keyof typeof ChatMessageRole;
  content: string;
  tokenCount: number;
  createdAt: string;
  citations?: ChatCitationDto[];
}

export type ConversationPage = PagedResponse<ConversationDto>;
export type ChatMessagePage = PagedResponse<ChatMessageDto>;

export interface ChatUiMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  createdAt: string;
  isStreaming?: boolean;
  citations?: ChatCitationDto[];
}
