import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { APP_CONFIG } from '../constants/app.constants';
import { ChatAskRequest, ChatStreamEvent } from '../models/chat.model';
import { AuthService } from './auth.service';
import { ClientLoggerService } from './client-logger.service';

export type ChatStreamErrorKind = 'unauthorized' | 'not-indexed' | 'no-context' | 'network' | 'no-answer' | 'api';

export interface ChatStreamError {
  kind: ChatStreamErrorKind;
  message: string;
  status?: number;
}

interface ChatStreamCallbacks {
  onToken: (token: string) => void;
  onComplete: (event: ChatStreamEvent) => void;
  onError: (error: ChatStreamError) => void;
}

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private readonly authService = inject(AuthService);
  private readonly logger = inject(ClientLoggerService);
  private readonly streamUrl = `${APP_CONFIG.apiBaseUrl}/chat/ask/stream`;

  async streamAsk(
    request: ChatAskRequest,
    callbacks: ChatStreamCallbacks,
    abortSignal: AbortSignal
  ): Promise<void> {
    return this.executeStream(request, callbacks, abortSignal, false);
  }

  private async executeStream(
    request: ChatAskRequest,
    callbacks: ChatStreamCallbacks,
    abortSignal: AbortSignal,
    hasRetriedAuthentication: boolean
  ): Promise<void> {
    let response: Response;
    try {
      response = await this.fetchStream(request, abortSignal);
    } catch (error: unknown) {
      if ((error as DOMException).name === 'AbortError') {
        throw error;
      }

      this.logger.error('Chat streaming request could not reach the API.', error);
      callbacks.onError({
        kind: 'network',
        message: 'The chat service could not be reached. Check your connection and try again.'
      });
      return;
    }

    if (response.status === 401 && !hasRetriedAuthentication && this.authService.getRefreshToken()) {
      try {
        await firstValueFrom(this.authService.refreshToken());
        return this.executeStream(request, callbacks, abortSignal, true);
      } catch (error: unknown) {
        this.logger.warn('Chat authentication refresh failed.', error);
      }
    }

    if (!response.ok || !response.body) {
      const streamError = await this.getErrorMessage(response);
      this.logger.warn('Chat API returned an unsuccessful response.', {
        status: response.status,
        kind: streamError.kind
      });
      callbacks.onError(streamError);
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let completed = false;

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }

        buffer += decoder.decode(value, { stream: true });
        const frames = buffer.split('\n\n');
        buffer = frames.pop() ?? '';

        for (const frame of frames) {
          completed = this.handleSseFrame(frame, callbacks) || completed;
        }
      }
    } catch (error: unknown) {
      if ((error as DOMException).name === 'AbortError') {
        throw error;
      }

      this.logger.error('Chat response stream failed.', error);
      callbacks.onError({ kind: 'network', message: 'The response stream was interrupted. Please try again.' });
      return;
    }

    if (!completed && !abortSignal.aborted) {
      callbacks.onError({
        kind: 'no-answer',
        message: 'The AI service finished without returning an answer. Please try again.'
      });
    }
  }

  private fetchStream(request: ChatAskRequest, abortSignal: AbortSignal): Promise<Response> {
    return fetch(this.streamUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'text/event-stream',
        ...(this.authService.getAccessToken()
          ? { Authorization: `Bearer ${this.authService.getAccessToken()}` }
          : {})
      },
      body: JSON.stringify(request),
      signal: abortSignal
    });
  }

  private handleSseFrame(
    frame: string,
    callbacks: ChatStreamCallbacks
  ): boolean {
    if (frame.startsWith('event: done')) {
      return false;
    }

    const dataLine = frame.split('\n').find((line) => line.startsWith('data:'));
    const data = dataLine?.replace(/^data:\s*/, '');
    if (!data) {
      return false;
    }

    const event = JSON.parse(data) as ChatStreamEvent;
    if (event.type === 'token' && event.token) {
      callbacks.onToken(event.token);
      return false;
    }

    if (event.type === 'complete') {
      callbacks.onComplete(event);
      return true;
    }

    return false;
  }

  private async getErrorMessage(response: Response): Promise<ChatStreamError> {
    if (response.status === 401) {
      this.authService.clearSession();
      return {
        kind: 'unauthorized',
        message: 'Your session has expired. Sign in again to continue.',
        status: response.status
      };
    }

    try {
      const body = await response.json() as { detail?: string; title?: string; errors?: Record<string, string[]> };
      const message = body.errors
        ? Object.values(body.errors).flat().join(' ')
        : body.detail ?? body.title ?? 'The AI request failed.';
      const hasNoRelevantContext = response.status === 400
        && /no relevant document chunks/i.test(message);
      const isDocumentNotReady = response.status === 400
        && /not indexed|indexing/i.test(message);

      return {
        kind: hasNoRelevantContext ? 'no-context' : isDocumentNotReady ? 'not-indexed' : 'api',
        message: hasNoRelevantContext
          ? 'I could not find this in your uploaded documents.'
          : isDocumentNotReady
            ? 'No indexed document content is available yet. Wait for the document status to show Indexed, then try again.'
            : message,
        status: response.status
      };
    } catch {
      return {
        kind: 'api',
        message: response.status === 502 || response.status === 503
          ? 'The AI provider is temporarily unavailable.'
          : 'The AI request failed. Please try again.',
        status: response.status
      };
    }
  }
}
