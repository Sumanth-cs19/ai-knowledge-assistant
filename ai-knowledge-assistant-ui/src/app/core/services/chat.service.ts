import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { APP_CONFIG } from '../constants/app.constants';
import { ChatAskRequest, ChatStreamEvent } from '../models/chat.model';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private readonly authService = inject(AuthService);
  private readonly streamUrl = `${APP_CONFIG.apiBaseUrl}/chat/ask/stream`;

  async streamAsk(
    request: ChatAskRequest,
    callbacks: {
      onToken: (token: string) => void;
      onComplete: (event: ChatStreamEvent) => void;
      onError: (message: string) => void;
    },
    abortSignal: AbortSignal
  ): Promise<void> {
    const response = await this.fetchStream(request, abortSignal);

    if (response.status === 401 && this.authService.getRefreshToken()) {
      await firstValueFrom(this.authService.refreshToken());
      return this.streamAsk(request, callbacks, abortSignal);
    }

    if (!response.ok || !response.body) {
      callbacks.onError(await this.getErrorMessage(response));
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });
      const frames = buffer.split('\n\n');
      buffer = frames.pop() ?? '';

      for (const frame of frames) {
        this.handleSseFrame(frame, callbacks);
      }
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
    callbacks: {
      onToken: (token: string) => void;
      onComplete: (event: ChatStreamEvent) => void;
      onError: (message: string) => void;
    }
  ): void {
    if (frame.startsWith('event: done')) {
      return;
    }

    const dataLine = frame.split('\n').find((line) => line.startsWith('data:'));
    const data = dataLine?.replace(/^data:\s*/, '');
    if (!data) {
      return;
    }

    const event = JSON.parse(data) as ChatStreamEvent;
    if (event.type === 'token' && event.token) {
      callbacks.onToken(event.token);
      return;
    }

    if (event.type === 'complete') {
      callbacks.onComplete(event);
    }
  }

  private async getErrorMessage(response: Response): Promise<string> {
    try {
      const body = await response.json() as { detail?: string; title?: string; errors?: Record<string, string[]> };
      if (body.errors) {
        return Object.values(body.errors).flat().join(' ');
      }

      return body.detail ?? body.title ?? 'The AI request failed.';
    } catch {
      return response.status === 503
        ? 'The AI provider is unavailable.'
        : 'Network error while generating the response.';
    }
  }
}
