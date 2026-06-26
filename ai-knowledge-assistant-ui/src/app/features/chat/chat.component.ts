import { Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { ToastrService } from 'ngx-toastr';

import {
  ChatMessageDto,
  ChatMessageRole,
  ChatResponse,
  ChatUiMessage,
  ConversationDto
} from '../../core/models/chat.model';
import { ChatService } from '../../core/services/chat.service';
import { ConversationService } from '../../core/services/conversation.service';
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
export class ChatComponent {
  @ViewChild('messageViewport') private readonly messageViewport?: ElementRef<HTMLElement>;

  protected readonly conversations = signal<ConversationDto[]>([]);
  protected readonly activeConversationId = signal<string | null>(null);
  protected readonly messages = signal<ChatUiMessage[]>([]);
  protected readonly isGenerating = signal(false);
  protected readonly generationStage = signal<'idle' | 'searching' | 'thinking' | 'generating'>('idle');
  protected readonly sidebarOpened = signal(true);
  protected readonly hasMessages = computed(() => this.messages().length > 0);

  private readonly chatService = inject(ChatService);
  private readonly conversationService = inject(ConversationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private abortController: AbortController | null = null;
  private lastQuestion: string | null = null;

  constructor() {
    this.loadConversations();
  }

  protected newChat(): void {
    this.stopGeneration();
    this.activeConversationId.set(null);
    this.messages.set([]);
  }

  protected openConversation(id: string): void {
    this.stopGeneration();
    this.activeConversationId.set(id);
    this.conversationService.getConversationMessages(id).subscribe({
      next: (response) => {
        this.messages.set(response.items.map((message) => this.toUiMessage(message)));
        this.scrollToBottom();
      }
    });
  }

  protected sendMessage(question: string): void {
    if (this.isGenerating()) {
      return;
    }

    this.lastQuestion = question;
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
    this.abortController = new AbortController();

    window.setTimeout(() => {
      if (this.isGenerating()) {
        this.generationStage.set('generating');
      }
    }, 900);

    void this.chatService.streamAsk(
      {
        question,
        conversationId: this.activeConversationId()
      },
      {
        onToken: (token) => this.appendToken(assistantMessage.id, token),
        onComplete: (event) => this.completeAssistantMessage(assistantMessage.id, event.response ?? null),
        onError: (message) => this.failAssistantMessage(assistantMessage.id, message)
      },
      this.abortController.signal
    ).catch((error: unknown) => {
      if ((error as DOMException).name !== 'AbortError') {
        this.failAssistantMessage(assistantMessage.id, 'Network error while generating the response.');
      }
    });
  }

  protected stopGeneration(): void {
    this.abortController?.abort();
    this.abortController = null;
    this.isGenerating.set(false);
    this.generationStage.set('idle');
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

  protected toggleSidebar(): void {
    this.sidebarOpened.update((value) => !value);
  }

  private loadConversations(): void {
    this.conversationService.getConversations().subscribe({
      next: (response) => this.conversations.set(response.items.filter((conversation) => !conversation.isArchived))
    });
  }

  private appendToken(messageId: string, token: string): void {
    this.generationStage.set('generating');
    this.messages.update((messages) => messages.map((message) => {
      return message.id === messageId
        ? { ...message, content: `${message.content}${token}` }
        : message;
    }));
    this.scrollToBottom();
  }

  private completeAssistantMessage(messageId: string, response: ChatResponse | null): void {
    this.isGenerating.set(false);
    this.generationStage.set('idle');
    this.abortController = null;

    if (response?.conversationId) {
      this.activeConversationId.set(response.conversationId);
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

  private failAssistantMessage(messageId: string, message: string): void {
    this.isGenerating.set(false);
    this.generationStage.set('idle');
    this.abortController = null;
    this.messages.update((messages) => messages.map((item) => item.id === messageId
      ? { ...item, content: message, isStreaming: false }
      : item));
    this.toastr.error(message, 'Chat failed');
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

  private scrollToBottom(): void {
    window.setTimeout(() => {
      const element = this.messageViewport?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }
}
