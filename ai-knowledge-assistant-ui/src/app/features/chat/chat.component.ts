import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnDestroy, ViewChild, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

import {
  ChatMessageDto,
  ChatMessageRole,
  ChatResponse,
  ChatUiMessage,
  ConversationDto
} from '../../core/models/chat.model';
import { DocumentDto, DocumentStatus } from '../../core/models/document.model';
import { ChatService, ChatStreamError } from '../../core/services/chat.service';
import { ConversationService } from '../../core/services/conversation.service';
import { DocumentService } from '../../core/services/document.service';
import { PreferencesService } from '../../core/services/preferences.service';
import {
  ConfirmationDialogComponent,
  ConfirmationDialogData
} from '../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { ChatInputComponent } from './components/chat-input/chat-input.component';
import { ChatMessageComponent } from './components/chat-message/chat-message.component';
import { ConversationSidebarComponent } from './components/conversation-sidebar/conversation-sidebar.component';
import { TypingIndicatorComponent } from './components/typing-indicator/typing-indicator.component';

@Component({
  selector: 'app-chat',
  imports: [
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatSidenavModule,
    ChatInputComponent,
    ChatMessageComponent,
    ConversationSidebarComponent,
    TypingIndicatorComponent
  ],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatComponent implements OnDestroy {
  @ViewChild('messageViewport') private readonly messageViewport?: ElementRef<HTMLElement>;
  @ViewChild(ChatInputComponent) private readonly chatInput?: ChatInputComponent;

  protected readonly conversations = signal<ConversationDto[]>([]);
  protected readonly activeConversationId = signal<string | null>(null);
  protected readonly messages = signal<ChatUiMessage[]>([]);
  protected readonly isGenerating = signal(false);
  protected readonly isCreatingConversation = signal(false);
  protected readonly isLoadingConversation = signal(false);
  protected readonly documents = signal<DocumentDto[]>([]);
  protected readonly documentAvailability = signal<DocumentAvailability>('loading');
  protected readonly generationStage = signal<'idle' | 'searching' | 'thinking' | 'generating'>('idle');
  protected readonly chatStatus = signal<{ kind: 'asking' | 'success' | 'error'; message: string } | null>(null);
  protected readonly sidebarOpened = signal(true);
  protected readonly showScrollToBottom = signal(false);
  protected readonly hasMessages = computed(() => this.messages().length > 0);
  protected readonly canAskQuestions = computed(() => this.documentAvailability() === 'ready');
  protected readonly knowledgeDocuments = computed(() => this.documents().slice(0, 5));
  protected readonly activeConversationTitle = computed(() => {
    return this.conversations().find((conversation) => conversation.id === this.activeConversationId())?.title
      ?? 'New chat';
  });

  private readonly chatService = inject(ChatService);
  private readonly conversationService = inject(ConversationService);
  private readonly documentService = inject(DocumentService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly preferencesService = inject(PreferencesService);
  private abortController: AbortController | null = null;
  private lastQuestion: string | null = null;
  private pendingTitleQuestion: string | null = null;
  private documentStatusPollId: number | null = null;
  private initialConversationPreferenceApplied = false;

  constructor() {
    this.loadConversations();
    this.loadDocumentAvailability();
  }

  ngOnDestroy(): void {
    this.abortController?.abort();
    this.clearDocumentStatusPoll();
  }

  protected newChat(): void {
    if (this.isCreatingConversation()) {
      return;
    }

    this.stopGeneration();
    this.activeConversationId.set(null);
    this.messages.set([]);
    this.chatStatus.set({ kind: 'asking', message: 'Creating a new chat...' });
    this.lastQuestion = null;
    this.pendingTitleQuestion = null;
    this.focusChatInput();

    this.isCreatingConversation.set(true);
    this.conversationService.createConversation().pipe(
      finalize(() => this.isCreatingConversation.set(false))
    ).subscribe({
      next: (conversation) => {
        this.activeConversationId.set(conversation.id);
        this.conversations.update((items) => [
          conversation,
          ...items.filter((item) => item.id !== conversation.id)
        ]);
        this.chatStatus.set(null);
        this.focusChatInput();
      },
      error: () => {
        this.chatStatus.set({ kind: 'error', message: 'A new conversation could not be created.' });
        this.toastr.error('A new conversation could not be created.', 'New chat failed');
      }
    });
  }

  protected openConversation(id: string): void {
    this.stopGeneration();
    this.activeConversationId.set(id);
    this.messages.set([]);
    this.isLoadingConversation.set(true);
    this.chatStatus.set({ kind: 'asking', message: 'Loading conversation...' });
    this.conversationService.getConversationMessages(id).pipe(
      finalize(() => this.isLoadingConversation.set(false))
    ).subscribe({
      next: (response) => {
        this.messages.set(response.items.map((message) => this.toUiMessage(message)));
        this.chatStatus.set(null);
        this.scrollToBottom();
      },
      error: () => {
        this.chatStatus.set({ kind: 'error', message: 'Conversation messages could not be loaded.' });
        this.toastr.error('Conversation messages could not be loaded.', 'Chat unavailable');
      }
    });
  }

  protected sendMessage(question: string): void {
    if (this.isGenerating() || !this.canAskQuestions()) {
      if (!this.canAskQuestions()) {
        this.toastr.info('Wait until at least one document is indexed before asking a question.', 'Documents required');
      }
      return;
    }

    this.lastQuestion = question;
    if (!this.hasMessages()) {
      const activeConversation = this.conversations()
        .find((conversation) => conversation.id === this.activeConversationId());
      if (!activeConversation || this.isPlaceholderTitle(activeConversation.title)) {
        this.pendingTitleQuestion = question;
      }
    }
    const userMessage: ChatUiMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: question,
      createdAt: new Date().toISOString()
    };
    const assistantMessage: ChatUiMessage = {
      id: crypto.randomUUID(),
      role: 'assistant',
      content: '',
      createdAt: new Date().toISOString(),
      isStreaming: true,
      citations: []
    };

    this.messages.update((messages) => [...messages, userMessage, assistantMessage]);
    this.scrollToBottom();
    this.isGenerating.set(true);
    this.generationStage.set('searching');
    this.chatStatus.set({ kind: 'asking', message: 'Searching indexed documents for an answer...' });
    this.abortController = new AbortController();

    window.setTimeout(() => {
      if (this.isGenerating()) {
        this.generationStage.set('generating');
      }
    }, 900);

    const request = { question, conversationId: this.activeConversationId() };
    if (!this.preferencesService.preferences().streamingEnabled) {
      this.abortController = null;
      this.chatService.ask(request).subscribe({
        next: (response) => this.completeAssistantMessage(assistantMessage.id, response),
        error: (error: { status?: number; error?: { detail?: string; title?: string } }) => {
          const kind = error.status === 401 ? 'unauthorized' : error.status === 0 ? 'network' : 'api';
          this.failAssistantMessage(assistantMessage.id, {
            kind,
            status: error.status,
            message: error.error?.detail ?? error.error?.title ?? 'The chat request failed. Please try again.'
          });
        }
      });
      return;
    }

    void this.chatService.streamAsk(
      request,
      {
        onToken: (token) => this.appendToken(assistantMessage.id, token),
        onComplete: (event) => this.completeAssistantMessage(assistantMessage.id, event.response ?? null),
        onError: (error) => this.failAssistantMessage(assistantMessage.id, error),
        onDone: () => this.finishStream(assistantMessage.id)
      },
      this.abortController.signal
    ).catch((error: unknown) => {
      if ((error as DOMException).name !== 'AbortError') {
        this.failAssistantMessage(assistantMessage.id, {
          kind: 'network',
          message: 'Network error while generating the response.'
        });
      }
    });
  }

  protected stopGeneration(): void {
    const wasGenerating = this.isGenerating();
    this.abortController?.abort();
    this.abortController = null;
    this.isGenerating.set(false);
    this.generationStage.set('idle');
    if (wasGenerating) {
      this.chatStatus.set({ kind: 'error', message: 'Generation stopped.' });
    }
    this.messages.update((messages) => messages.map((message) => message.isStreaming
      ? { ...message, isStreaming: false, content: message.content || 'Generation stopped.' }
      : message));
  }

  protected regenerate(): void {
    if (this.lastQuestion) {
      this.sendMessage(this.lastQuestion);
    }
  }

  protected copyMessage(content: string): void {
    void navigator.clipboard.writeText(content);
    this.toastr.success('Response copied.', 'Copied');
  }

  protected renameConversation(conversation: ConversationDto): void {
    const title = window.prompt('Rename conversation', conversation.title)?.trim();
    if (!title) {
      return;
    }

    this.conversationService.updateConversation(conversation.id, { title }).subscribe({
      next: (updated) => {
        this.conversations.update((items) => items.map((item) => item.id === updated.id ? updated : item));
        this.toastr.success('Conversation renamed.', 'Updated');
      }
    });
  }

  protected deleteConversation(conversation: ConversationDto): void {
    const dialogRef = this.dialog.open<ConfirmationDialogComponent, ConfirmationDialogData, boolean>(
      ConfirmationDialogComponent,
      {
        data: {
          title: 'Delete conversation',
          message: `Delete "${conversation.title}"?`,
          confirmText: 'Delete',
          cancelText: 'Cancel'
        }
      }
    );

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.conversationService.deleteConversation(conversation.id).subscribe({
        next: () => {
          this.conversations.update((items) => items.filter((item) => item.id !== conversation.id));
          if (this.activeConversationId() === conversation.id) {
            this.newChat();
          }
          this.toastr.success('Conversation deleted.', 'Deleted');
        }
      });
    });
  }

  protected archiveConversation(conversation: ConversationDto): void {
    this.conversationService.archiveConversation(conversation.id).subscribe({
      next: () => {
        this.conversations.update((items) => items.filter((item) => item.id !== conversation.id));
        if (this.activeConversationId() === conversation.id) {
          this.newChat();
        }
        this.toastr.success('Conversation archived.', 'Archived');
      }
    });
  }

  protected openDocument(documentId: string): void {
    void this.router.navigate(['/documents'], { queryParams: { documentId } });
  }

  protected openDocuments(): void {
    void this.router.navigate(['/documents']);
  }

  protected retryDocumentCheck(): void {
    this.loadDocumentAvailability();
  }

  protected getDocumentStatusLabel(status: DocumentDto['status']): string {
    return DocumentStatus[this.normalizeDocumentStatus(status)];
  }

  protected getDocumentStatusClass(status: DocumentDto['status']): string {
    return this.getDocumentStatusLabel(status).toLowerCase();
  }

  protected toggleSidebar(): void {
    this.sidebarOpened.update((value) => !value);
  }

  protected onMessagesScroll(): void {
    const element = this.messageViewport?.nativeElement;
    if (!element) {
      return;
    }

    this.showScrollToBottom.set(element.scrollHeight - element.scrollTop - element.clientHeight > 240);
  }

  protected scrollToBottomButton(): void {
    this.scrollToBottom();
  }

  private loadConversations(): void {
    this.conversationService.getConversations().subscribe({
      next: (response) => {
        const conversations = response.items.filter((conversation) => !conversation.isArchived);
        this.conversations.set(conversations);
        if (!this.initialConversationPreferenceApplied) {
          this.initialConversationPreferenceApplied = true;
          if (this.preferencesService.preferences().defaultChatBehavior === 'continue-last' && conversations[0]) {
            this.openConversation(conversations[0].id);
          }
        }
      }
    });
  }

  private appendToken(messageId: string, token: string): void {
    this.generationStage.set('generating');
    this.chatStatus.set({ kind: 'asking', message: 'Generating the answer...' });
    this.messages.update((messages) => messages.map((message) => {
      return message.id === messageId
        ? { ...message, content: `${message.content}${token}` }
        : message;
    }));
    this.scrollToBottom();
  }

  private completeAssistantMessage(messageId: string, response: ChatResponse | null): void {
    const streamedAnswer = this.messages().find((message) => message.id === messageId)?.content.trim();
    if (!response?.answer?.trim() && !streamedAnswer) {
      this.failAssistantMessage(messageId, {
        kind: 'no-answer',
        message: 'The AI service returned no answer. Please try again.'
      });
      return;
    }

    this.isGenerating.set(false);
    this.generationStage.set('idle');
    this.chatStatus.set({ kind: 'success', message: 'Answer received.' });
    this.toastr.success('Answer received.', 'Chat');
    this.abortController = null;

    if (response?.conversationId) {
      this.activeConversationId.set(response.conversationId);
    }

    const conversationId = response?.conversationId ?? this.activeConversationId();
    if (conversationId && this.pendingTitleQuestion) {
      this.ensureConversationTitle(conversationId, this.pendingTitleQuestion);
      this.pendingTitleQuestion = null;
    }

    this.messages.update((messages) => messages.map((message) => {
      return message.id === messageId
        ? {
          ...message,
          id: response?.assistantMessageId ?? message.id,
          content: response?.answer ?? message.content,
          createdAt: response?.createdAt ?? message.createdAt,
          isStreaming: false,
          citations: response?.citations ?? response?.sources ?? []
        }
        : message;
    }));
    this.loadConversations();
    this.scrollToBottom();
  }

  private finishStream(messageId: string): void {
    if (!this.isGenerating()) {
      return;
    }

    const streamedAnswer = this.messages().find((message) => message.id === messageId)?.content.trim();
    if (!streamedAnswer) {
      this.failAssistantMessage(messageId, {
        kind: 'no-answer',
        message: 'The AI service returned no answer. Please try again.'
      });
      return;
    }

    this.isGenerating.set(false);
    this.generationStage.set('idle');
    this.chatStatus.set({ kind: 'success', message: 'Answer received.' });
    this.toastr.success('Answer received.', 'Chat');
    this.abortController = null;
    this.messages.update((messages) => messages.map((message) => message.id === messageId
      ? { ...message, isStreaming: false }
      : message));
  }

  private failAssistantMessage(messageId: string, error: ChatStreamError): void {
    this.isGenerating.set(false);
    this.generationStage.set('idle');
    this.abortController = null;
    this.messages.update((messages) => messages.map((item) => item.id === messageId
      ? { ...item, content: error.message, isStreaming: false }
      : item));
    this.chatStatus.set({ kind: 'error', message: error.message });

    const title = error.kind === 'not-indexed'
      ? 'Document not indexed yet'
      : error.kind === 'no-context'
        ? 'No matching document content'
      : error.kind === 'unauthorized'
        ? 'Authentication required'
        : error.kind === 'network'
          ? 'Connection failed'
          : 'Chat failed';
    this.toastr.error(error.message, title);

    if (error.kind === 'unauthorized') {
      void this.router.navigate(['/login']);
    }
  }

  private toUiMessage(message: ChatMessageDto): ChatUiMessage {
    return {
      id: message.id,
      role: this.normalizeRole(message.role),
      content: message.content,
      createdAt: message.createdAt,
      citations: message.citations ?? []
    };
  }

  private normalizeRole(role: ChatMessageDto['role']): ChatUiMessage['role'] {
    if (role === ChatMessageRole.Assistant || role === 'Assistant') {
      return 'assistant';
    }

    if (role === ChatMessageRole.System || role === 'System') {
      return 'system';
    }

    return 'user';
  }

  private ensureConversationTitle(conversationId: string, firstQuestion: string): void {
    this.conversationService.getConversation(conversationId).subscribe({
      next: (conversation) => {
        if (!this.isPlaceholderTitle(conversation.title)
          && !this.isQuestionDerivedTitle(conversation.title, firstQuestion)) {
          this.updateConversationInList(conversation);
          return;
        }

        this.conversationService.updateConversation(conversationId, {
          title: this.createFallbackTitle(firstQuestion)
        }).subscribe({
          next: (updated) => this.updateConversationInList(updated)
        });
      }
    });
  }

  private createFallbackTitle(question: string): string {
    const words = question.trim().split(/\s+/).filter(Boolean);
    const title = words.slice(0, 8).join(' ');
    return words.length > 8 ? `${title}...` : title;
  }

  private isPlaceholderTitle(title: string): boolean {
    const normalizedTitle = title.trim().toLowerCase();
    return !normalizedTitle || normalizedTitle === 'new conversation' || normalizedTitle === 'new chat';
  }

  private isQuestionDerivedTitle(title: string, question: string): boolean {
    const normalizedQuestion = question.trim().split(/\s+/).filter(Boolean).join(' ');
    const backendFallback = normalizedQuestion.length <= 60
      ? normalizedQuestion
      : `${normalizedQuestion.slice(0, 60).trim()}...`;
    return title.trim() === backendFallback;
  }

  private updateConversationInList(conversation: ConversationDto): void {
    this.conversations.update((items) => items.map((item) => {
      return item.id === conversation.id ? conversation : item;
    }));
  }

  private loadDocumentAvailability(showLoading = true): void {
    this.clearDocumentStatusPoll();
    if (showLoading) {
      this.documentAvailability.set('loading');
    }

    this.documentService.getMyDocuments(!showLoading).subscribe({
      next: (documents) => {
        this.documents.set(documents);
        const availability = resolveDocumentAvailability(documents);
        this.documentAvailability.set(availability);

        if (availability === 'processing') {
          this.scheduleDocumentStatusPoll();
        }
      },
      error: () => this.documentAvailability.set('error')
    });
  }

  private scheduleDocumentStatusPoll(delay = 5000): void {
    this.clearDocumentStatusPoll();
    this.documentStatusPollId = window.setTimeout(() => this.loadDocumentAvailability(false), delay);
  }

  private clearDocumentStatusPoll(): void {
    if (this.documentStatusPollId !== null) {
      window.clearTimeout(this.documentStatusPollId);
      this.documentStatusPollId = null;
    }
  }

  private normalizeDocumentStatus(status: DocumentDto['status']): DocumentStatus {
    return typeof status === 'number'
      ? status
      : DocumentStatus[status] ?? DocumentStatus.Pending;
  }

  private focusChatInput(): void {
    window.setTimeout(() => this.chatInput?.focus());
  }

  private scrollToBottom(): void {
    window.setTimeout(() => {
      const element = this.messageViewport?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
        this.showScrollToBottom.set(false);
      }
    });
  }
}

export type DocumentAvailability = 'loading' | 'none' | 'processing' | 'ready' | 'error';

export function resolveDocumentAvailability(documents: DocumentDto[]): Exclude<DocumentAvailability, 'loading' | 'error'> {
  const normalizeStatus = (status: DocumentDto['status']): DocumentStatus => {
    return typeof status === 'number' ? status : DocumentStatus[status] ?? DocumentStatus.Pending;
  };

  if (documents.some((document) => normalizeStatus(document.status) === DocumentStatus.Indexed)) {
    return 'ready';
  }

  return documents.some((document) => {
    const status = normalizeStatus(document.status);
    return status === DocumentStatus.Pending || status === DocumentStatus.Processing;
  })
    ? 'processing'
    : 'none';
}
