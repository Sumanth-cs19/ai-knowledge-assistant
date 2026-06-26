import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { APP_CONFIG } from '../constants/app.constants';
import {
  AdminChatAnalyticsDto,
  AdminDocumentAnalyticsDto,
  AdminFeedbackAnalyticsDto,
  AdminOverviewDto,
  AdminUserAnalyticsDto,
  AdminUserDto,
  RoleDto
} from '../models/admin.model';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly adminUrl = `${APP_CONFIG.apiBaseUrl}/admin`;
  private readonly analyticsUrl = `${this.adminUrl}/analytics`;

  getOverview(): Observable<AdminOverviewDto> {
    return this.http.get<AdminOverviewDto>(`${this.analyticsUrl}/overview`);
  }

  getUsers(): Observable<AdminUserDto[]> {
    return this.http.get<AdminUserDto[]>(`${this.adminUrl}/users`);
  }

  getUserById(id: string): Observable<AdminUserDto> {
    return this.http.get<AdminUserDto>(`${this.adminUrl}/users/${id}`);
  }

  updateUserRole(id: string, roleId: string): Observable<AdminUserDto> {
    return this.http.put<AdminUserDto>(`${this.adminUrl}/users/${id}/role`, { roleId });
  }

  deleteUser(id: string): Observable<void> {
    return this.http.delete<void>(`${this.adminUrl}/users/${id}`);
  }

  getRoles(): Observable<RoleDto[]> {
    return this.http.get<RoleDto[]>(`${this.adminUrl}/roles`);
  }

  getUserAnalytics(): Observable<AdminUserAnalyticsDto> {
    return this.http.get<AdminUserAnalyticsDto>(`${this.analyticsUrl}/users`);
  }

  getDocumentAnalytics(): Observable<AdminDocumentAnalyticsDto> {
    return this.http.get<AdminDocumentAnalyticsDto>(`${this.analyticsUrl}/documents`);
  }

  getChatAnalytics(): Observable<AdminChatAnalyticsDto> {
    return this.http.get<AdminChatAnalyticsDto>(`${this.analyticsUrl}/chats`);
  }

  getFeedbackAnalytics(): Observable<AdminFeedbackAnalyticsDto> {
    return this.http.get<AdminFeedbackAnalyticsDto>(`${this.analyticsUrl}/feedback`);
  }
}
