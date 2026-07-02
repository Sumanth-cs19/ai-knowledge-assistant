import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin } from 'rxjs';

import { ConversationDto } from '../../core/models/chat.model';
import { DocumentDto, DocumentStatus } from '../../core/models/document.model';
import { AuthService } from '../../core/services/auth.service';
import { ConversationService } from '../../core/services/conversation.service';
import { DocumentService } from '../../core/services/document.service';
import { SkeletonComponent } from '../../shared/components/skeleton/skeleton.component';

interface DashboardActivity {
  icon: string;
  action: string;
  occurredAt: string;
  route: string;
}

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, RouterLink, MatButtonModule, MatCardModule, MatIconModule, MatProgressBarModule, SkeletonComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  protected readonly documents = signal<DocumentDto[]>([]);
  protected readonly conversations = signal<ConversationDto[]>([]);
  protected readonly totalConversations = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly currentUser = inject(AuthService).currentUser;
  protected readonly recentDocuments = computed(() => [...this.documents()]
    .sort((left, right) => Date.parse(right.uploadedAt) - Date.parse(left.uploadedAt))
    .slice(0, 5));
  protected readonly indexedDocuments = computed(() => this.documents()
    .filter((document) => this.statusLabel(document.status) === 'Indexed').length);
  protected readonly showGettingStarted = computed(() => !this.isLoading() && (
    this.documents().length === 0
    || this.indexedDocuments() === 0
    || this.totalConversations() === 0
  ));
  protected readonly gettingStartedSteps = computed(() => [
    { label: 'Upload your first document', complete: this.documents().length > 0 },
    { label: 'Wait until the document is Indexed', complete: this.indexedDocuments() > 0 },
    { label: 'Ask your first question', complete: this.totalConversations() > 0 }
  ]);
  protected readonly metricCards = computed(() => [
    { label: 'Total Documents', value: this.documents().length, icon: 'description' },
    { label: 'Indexed Documents', value: this.indexedDocuments(), icon: 'task_alt' },
    { label: 'Total Conversations', value: this.totalConversations(), icon: 'forum' },
    { label: 'Current Role', value: this.currentUser()?.role || 'User', icon: 'badge' }
  ]);
  protected readonly recentActivity = computed<DashboardActivity[]>(() => {
    const documentActivity = this.documents().map((document) => ({
      icon: 'upload_file',
      action: `Uploaded ${document.originalFileName}`,
      occurredAt: document.uploadedAt,
      route: '/documents'
    }));
    const conversationActivity = this.conversations().map((conversation) => ({
      icon: 'chat_bubble_outline',
      action: conversation.isArchived
        ? `Archived conversation: ${conversation.title || 'Untitled conversation'}`
        : `Asked: \"${conversation.title || 'Untitled conversation'}\"`,
      occurredAt: conversation.updatedAt,
      route: '/chat'
    }));

    return [...documentActivity, ...conversationActivity]
      .sort((left, right) => Date.parse(right.occurredAt) - Date.parse(left.occurredAt))
      .slice(0, 6);
  });

  private readonly documentService = inject(DocumentService);
  private readonly conversationService = inject(ConversationService);

  constructor() {
    this.loadDashboard();
  }

  protected refresh(): void {
    this.loadDashboard();
  }

  protected statusLabel(status: DocumentDto['status']): string {
    if (typeof status === 'string') {
      return status;
    }

    return DocumentStatus[status] ?? 'Unknown';
  }

  protected statusClass(status: DocumentDto['status']): string {
    return this.statusLabel(status).toLowerCase();
  }

  protected activityDateLabel(value: string): string {
    const activityDate = new Date(value);
    const today = new Date();
    const dayStart = new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime();
    const activityDayStart = new Date(
      activityDate.getFullYear(),
      activityDate.getMonth(),
      activityDate.getDate()
    ).getTime();
    const daysAgo = Math.max(0, Math.round((dayStart - activityDayStart) / 86_400_000));

    if (daysAgo === 0) {
      return 'Today';
    }
    if (daysAgo === 1) {
      return 'Yesterday';
    }
    return `${daysAgo} days ago`;
  }

  private loadDashboard(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      documents: this.documentService.getMyDocuments(),
      conversations: this.conversationService.getConversations(1, 20)
    }).subscribe({
      next: ({ documents, conversations }) => {
        this.documents.set(documents);
        this.conversations.set(conversations.items);
        this.totalConversations.set(conversations.totalCount);
        this.isLoading.set(false);
      },
      error: () => {
        this.errorMessage.set('Dashboard data could not be loaded. Your workspace is still available from the navigation.');
        this.isLoading.set(false);
      }
    });
  }
}
