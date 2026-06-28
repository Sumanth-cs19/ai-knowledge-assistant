import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { AuthService } from './auth.service';
import { ChatService } from './chat.service';
import { ClientLoggerService } from './client-logger.service';

describe('ChatService streaming', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders token events, completion metadata, and done without reloading', async () => {
    TestBed.configureTestingModule({
      providers: [
        ChatService,
        {
          provide: AuthService,
          useValue: {
            getAccessToken: () => 'test-access-token',
            getRefreshToken: () => null,
            refreshToken: () => of(null),
            clearSession: vi.fn()
          }
        },
        {
          provide: ClientLoggerService,
          useValue: { error: vi.fn(), warn: vi.fn() }
        }
      ]
    });
    const service = TestBed.inject(ChatService);
    const responsePayload = {
      conversationId: crypto.randomUUID(),
      userMessageId: crypto.randomUUID(),
      assistantMessageId: crypto.randomUUID(),
      question: 'What is recursion?',
      answer: 'Recursion solves a problem using smaller instances.',
      createdAt: new Date().toISOString(),
      citations: []
    };
    const stream = [
      `data: ${JSON.stringify({ Type: 'token', Token: 'Recursion ' })}\n\n`,
      `data: ${JSON.stringify({ type: 'token', token: 'works.' })}\n\n`,
      `data: ${JSON.stringify({ Type: 'complete', Response: responsePayload })}\n\n`,
      'event: done\ndata: {}\n\n'
    ].join('');
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(stream, {
      status: 200,
      headers: { 'Content-Type': 'text/event-stream' }
    }));
    let rendered = '';
    let completedConversationId: string | undefined;
    let done = false;

    await service.streamAsk(
      { question: 'What is recursion?', conversationId: null },
      {
        onToken: (token) => rendered += token,
        onComplete: (event) => completedConversationId = event.response?.conversationId,
        onError: (error) => expect.fail(error.message),
        onDone: () => done = true
      },
      new AbortController().signal);

    const requestOptions = fetchMock.mock.calls[0][1] as RequestInit;
    expect(rendered).toBe('Recursion works.');
    expect(completedConversationId).toBe(responsePayload.conversationId);
    expect(done).toBe(true);
    expect(requestOptions.method).toBe('POST');
    expect(requestOptions.headers).toMatchObject({ Authorization: 'Bearer test-access-token' });
  });
});
