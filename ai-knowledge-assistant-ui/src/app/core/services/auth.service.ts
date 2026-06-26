import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { APP_CONFIG } from '../constants/app.constants';
import {
  AuthResponse,
  LoginRequest,
  RefreshTokenRequest,
  RegisterRequest,
  StoredAuthState,
  UserDto
} from '../models/auth.model';
import { StorageService } from './storage.service';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storageService = inject(StorageService);
  private readonly authState = signal<StoredAuthState | null>(this.storageService.getAuthState());

  readonly currentUser = computed(() => this.authState()?.user ?? null);

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${APP_CONFIG.apiBaseUrl}/auth/login`, request).pipe(
      tap((response) => this.setSession(response))
    );
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${APP_CONFIG.apiBaseUrl}/auth/register`, request).pipe(
      tap((response) => this.setSession(response))
    );
  }

  refreshToken(): Observable<AuthResponse> {
    const refreshToken = this.getRefreshToken();
    const request: RefreshTokenRequest = { refreshToken: refreshToken ?? '' };

    return this.http.post<AuthResponse>(`${APP_CONFIG.apiBaseUrl}/auth/refresh`, request).pipe(
      tap((response) => this.setSession(response))
    );
  }

  logout(): Observable<void> {
    const refreshToken = this.getRefreshToken();
    this.clearSession();

    return this.http.post<void>(`${APP_CONFIG.apiBaseUrl}/auth/logout`, {
      refreshToken: refreshToken ?? ''
    });
  }

  getCurrentUser(): UserDto | null {
    return this.currentUser();
  }

  isAuthenticated(): boolean {
    const state = this.authState();
    if (!state?.accessToken) {
      return false;
    }

    if (this.isExpired(state.refreshTokenExpiresAt)) {
      this.clearSession();
      return false;
    }

    return true;
  }

  isAdmin(): boolean {
    return this.currentUser()?.role?.toLowerCase() === 'admin';
  }

  getAccessToken(): string | null {
    return this.authState()?.accessToken ?? null;
  }

  getRefreshToken(): string | null {
    return this.authState()?.refreshToken ?? null;
  }

  clearSession(): void {
    this.storageService.clearAuthState();
    this.authState.set(null);
  }

  private setSession(response: AuthResponse): void {
    const user = this.createUser(response);
    const state: StoredAuthState = {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      accessTokenExpiresAt: response.accessTokenExpiresAt,
      refreshTokenExpiresAt: response.refreshTokenExpiresAt,
      user
    };

    this.storageService.setAuthState(state);
    this.authState.set(state);
  }

  private createUser(response: AuthResponse): UserDto {
    const claims = this.decodeJwtPayload(response.accessToken);

    return {
      id: this.getClaim(claims, 'sub') ?? this.getClaim(claims, 'nameid') ?? '',
      email: response.email,
      role: this.getClaim(claims, 'role') ?? this.getClaim(claims, 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role') ?? 'User'
    };
  }

  private decodeJwtPayload(token: string): Record<string, unknown> {
    const [, payload] = token.split('.');
    if (!payload) {
      return {};
    }

    try {
      const normalizedPayload = payload.replace(/-/g, '+').replace(/_/g, '/');
      return JSON.parse(atob(normalizedPayload)) as Record<string, unknown>;
    } catch {
      return {};
    }
  }

  private getClaim(claims: Record<string, unknown>, key: string): string | null {
    const value = claims[key];
    return typeof value === 'string' ? value : null;
  }

  private isExpired(expiresAt: string): boolean {
    return new Date(expiresAt).getTime() <= Date.now();
  }
}
