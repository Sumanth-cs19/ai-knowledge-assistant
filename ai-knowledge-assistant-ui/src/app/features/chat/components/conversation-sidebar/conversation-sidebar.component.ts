import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';

import { ConversationDto } from '../../../../core/models/chat.model';

@Component({
  selector: 'app-conversation-sidebar',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule
  ],
  templateUrl: './conversation-sidebar.component.html',
  styleUrl: './conversation-sidebar.component.scss'
})
export class ConversationSidebarComponent {
  @Input() activeConversationId: string | null = null;
  @Input() set conversations(value: ConversationDto[]) {
    this.conversationState.set(value);
  }

  @Output() newChat = new EventEmitter<void>();
  @Output() openConversation = new EventEmitter<string>();
  @Output() renameConversation = new EventEmitter<ConversationDto>();
  @Output() deleteConversation = new EventEmitter<ConversationDto>();
  @Output() archiveConversation = new EventEmitter<ConversationDto>();

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  private readonly conversationState = signal<ConversationDto[]>([]);

  protected readonly filteredConversations = computed(() => {
    const query = this.searchControl.value.trim().toLowerCase();
    return this.conversationState().filter((conversation) => {
      return !query || conversation.title.toLowerCase().includes(query);
    });
  });
}
