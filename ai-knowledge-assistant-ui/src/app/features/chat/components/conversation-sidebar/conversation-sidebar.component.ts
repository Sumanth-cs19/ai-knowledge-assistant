import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';

import { ConversationDto } from '../../../../core/models/chat.model';

@Component({
  selector: 'app-conversation-sidebar',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatMenuModule
  ],
  templateUrl: './conversation-sidebar.component.html',
  styleUrl: './conversation-sidebar.component.scss'
})
export class ConversationSidebarComponent {
  @Input() activeConversationId: string | null = null;
  @Input() isCreatingConversation = false;
  @Input() set conversations(value: ConversationDto[]) {
    this.conversationState.set(value);
  }

  @Output() newChat = new EventEmitter<void>();
  @Output() openConversation = new EventEmitter<string>();
  @Output() renameConversation = new EventEmitter<ConversationDto>();
  @Output() deleteConversation = new EventEmitter<ConversationDto>();
  @Output() archiveConversation = new EventEmitter<ConversationDto>();

  private readonly conversationState = signal<ConversationDto[]>([]);

  protected readonly conversationGroups = computed(() => {
    const groups = [
      { label: 'Today', conversations: [] as ConversationDto[] },
      { label: 'Yesterday', conversations: [] as ConversationDto[] },
      { label: 'Last 7 Days', conversations: [] as ConversationDto[] },
      { label: 'Older', conversations: [] as ConversationDto[] }
    ];
    const startOfToday = this.startOfDay(new Date());

    for (const conversation of this.conversationState()) {
      const updatedAt = new Date(conversation.updatedAt);
      const ageInDays = Number.isNaN(updatedAt.getTime())
        ? Number.POSITIVE_INFINITY
        : Math.floor((startOfToday.getTime() - this.startOfDay(updatedAt).getTime()) / 86_400_000);

      if (ageInDays <= 0) {
        groups[0].conversations.push(conversation);
      } else if (ageInDays === 1) {
        groups[1].conversations.push(conversation);
      } else if (ageInDays <= 6) {
        groups[2].conversations.push(conversation);
      } else {
        groups[3].conversations.push(conversation);
      }
    }

    return groups.filter((group) => group.conversations.length > 0);
  });

  private startOfDay(value: Date): Date {
    return new Date(value.getFullYear(), value.getMonth(), value.getDate());
  }
}
