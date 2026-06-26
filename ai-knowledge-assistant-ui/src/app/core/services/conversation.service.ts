import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../constants/app.constants';
import {
  ChatMessagePage,
  ConversationCreateRequest,
  ConversationDto,
  ConversationPage,
  ConversationUpdateRequest
} from '../models/chat.model';

@Injectable({
  providedIn: 'root'
})
export class ConversationService {
  private readonly http = inject(HttpClient);
  private readonly conversationsUrl = `${APP_CONFIG.apiBaseUrl}/conversations`;

  getConversations(page = 1, pageSize = 50): Observable<ConversationPage> {
    return this.http.get<ConversationPage>(this.conversationsUrl, {
      params: { page, pageSize }
    });
  }

  createConversation(request: ConversationCreateRequest = {}): Observable<ConversationDto> {
    return this.http.post<ConversationDto>(`${this.conversationsUrl}/`, request);
  }

  getConversation(id: string): Observable<ConversationDto> {
    return this.http.get<ConversationDto>(`${this.conversationsUrl}/${id}`);
  }

  getConversationMessages(id: string, page = 1, pageSize = 100): Observable<ChatMessagePage> {
    return this.http.get<ChatMessagePage>(`${this.conversationsUrl}/${id}/messages`, {
      params: { page, pageSize }
    });
  }

  updateConversation(id: string, request: ConversationUpdateRequest): Observable<ConversationDto> {
    return this.http.put<ConversationDto>(`${this.conversationsUrl}/${id}`, request);
  }

  deleteConversation(id: string): Observable<void> {
    return this.http.delete<void>(`${this.conversationsUrl}/${id}`);
  }

  archiveConversation(id: string): Observable<void> {
    return this.http.post<void>(`${this.conversationsUrl}/${id}/archive`, {});
  }
}
