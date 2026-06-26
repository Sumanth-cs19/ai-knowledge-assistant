import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { ToastrService } from 'ngx-toastr';
import { forkJoin } from 'rxjs';

import {
  AdminChatAnalyticsDto,
  AdminDocumentAnalyticsDto,
  AdminFeedbackAnalyticsDto,
  AdminOverviewDto,
  AdminUserAnalyticsDto,
  AdminUserDto,
  RoleDto
} from '../../core/models/admin.model';
import { AdminService } from '../../core/services/admin.service';
import {
  ConfirmationDialogComponent,
  ConfirmationDialogData
} from '../../shared/components/confirmation-dialog/confirmation-dialog.component';

@Component({
  selector: 'app-admin',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTabsModule
  ],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent {
  protected readonly overview = signal<AdminOverviewDto | null>(null);
  protected readonly userAnalytics = signal<AdminUserAnalyticsDto | null>(null);
  protected readonly documentAnalytics = signal<AdminDocumentAnalyticsDto | null>(null);
  protected readonly chatAnalytics = signal<AdminChatAnalyticsDto | null>(null);
  protected readonly feedbackAnalytics = signal<AdminFeedbackAnalyticsDto | null>(null);
  protected readonly users = signal<AdminUserDto[]>([]);
  protected readonly selectedUser = signal<AdminUserDto | null>(null);
  protected readonly roles = signal<RoleDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly roleFilterControl = new FormControl<string>('all', { nonNullable: true });
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(5);

  protected readonly filteredUsers = computed(() => {
    const query = this.searchControl.value.trim().toLowerCase();
    const roleId = this.roleFilterControl.value;

    return this.users().filter((user) => {
      const matchesSearch = !query || user.email.toLowerCase().includes(query);
      const matchesRole = roleId === 'all' || user.role.id === roleId;
      return matchesSearch && matchesRole;
    });
  });

  protected readonly pagedUsers = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filteredUsers().slice(start, start + this.pageSize());
  });

  protected readonly metricCards = computed(() => {
    const overview = this.overview();
    if (!overview) {
      return [];
    }

    return [
      { label: 'Total users', value: overview.totalUsers, icon: 'group' },
      { label: 'Active users', value: overview.activeUsers, icon: 'verified_user' },
      { label: 'Total documents', value: overview.totalDocuments, icon: 'description' },
      { label: 'Indexed documents', value: overview.indexedDocuments, icon: 'fact_check' },
      { label: 'Failed documents', value: overview.failedDocuments, icon: 'error' },
      { label: 'Total conversations', value: overview.totalConversations, icon: 'forum' },
      { label: 'Total messages', value: overview.totalChatMessages, icon: 'chat' },
      {
        label: 'Avg feedback',
        value: overview.averageFeedbackRating === null ? 'N/A' : overview.averageFeedbackRating.toFixed(1),
        icon: 'thumbs_up_down'
      }
    ];
  });

  private readonly adminService = inject(AdminService);
  private readonly dialog = inject(MatDialog);
  private readonly toastr = inject(ToastrService);

  constructor() {
    this.loadAdminData();
    this.searchControl.valueChanges.subscribe(() => this.pageIndex.set(0));
    this.roleFilterControl.valueChanges.subscribe(() => this.pageIndex.set(0));
  }

  protected refresh(): void {
    this.loadAdminData();
  }

  protected viewUser(user: AdminUserDto): void {
    this.adminService.getUserById(user.id).subscribe({
      next: (response) => this.selectedUser.set(response),
      error: () => this.toastr.error('Could not load user details.', 'Admin action failed')
    });
  }

  protected updateRole(user: AdminUserDto, roleId: string): void {
    if (!roleId || roleId === user.role.id) {
      return;
    }

    this.adminService.updateUserRole(user.id, roleId).subscribe({
      next: (updatedUser) => {
        this.users.update((items) => items.map((item) => item.id === updatedUser.id ? updatedUser : item));
        if (this.selectedUser()?.id === updatedUser.id) {
          this.selectedUser.set(updatedUser);
        }
        this.toastr.success('User role updated.', 'Role updated');
      },
      error: () => this.toastr.error('Could not update user role.', 'Admin action failed')
    });
  }

  protected deleteUser(user: AdminUserDto): void {
    const dialogRef = this.dialog.open<ConfirmationDialogComponent, ConfirmationDialogData, boolean>(
      ConfirmationDialogComponent,
      {
        data: {
          title: 'Delete user',
          message: `Delete ${user.email}? This action cannot be undone.`,
          confirmText: 'Delete',
          cancelText: 'Cancel'
        }
      }
    );

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.adminService.deleteUser(user.id).subscribe({
        next: () => {
          this.users.update((items) => items.filter((item) => item.id !== user.id));
          if (this.selectedUser()?.id === user.id) {
            this.selectedUser.set(null);
          }
          this.toastr.success('User deleted.', 'Delete complete');
        },
        error: () => this.toastr.error('Could not delete user.', 'Admin action failed')
      });
    });
  }

  protected pageChanged(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  protected hasNoUserResults(): boolean {
    return this.users().length > 0 && this.filteredUsers().length === 0;
  }

  private loadAdminData(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      overview: this.adminService.getOverview(),
      userAnalytics: this.adminService.getUserAnalytics(),
      documentAnalytics: this.adminService.getDocumentAnalytics(),
      chatAnalytics: this.adminService.getChatAnalytics(),
      feedbackAnalytics: this.adminService.getFeedbackAnalytics(),
      users: this.adminService.getUsers(),
      roles: this.adminService.getRoles()
    }).subscribe({
      next: (response) => {
        this.overview.set(response.overview);
        this.userAnalytics.set(response.userAnalytics);
        this.documentAnalytics.set(response.documentAnalytics);
        this.chatAnalytics.set(response.chatAnalytics);
        this.feedbackAnalytics.set(response.feedbackAnalytics);
        this.users.set(response.users);
        this.roles.set(response.roles);
        this.isLoading.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(this.getErrorMessage(error));
        this.isLoading.set(false);
      }
    });
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 403) {
      return 'You do not have permission to access the admin dashboard.';
    }

    return 'Admin data could not be loaded. Please try again.';
  }
}
