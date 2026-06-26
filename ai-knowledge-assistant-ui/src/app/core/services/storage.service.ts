import { Injectable } from '@angular/core';

import { StoredAuthState } from '../models/auth.model';

const AUTH_STORAGE_KEY = 'ai-knowledge-assistant.auth';

@Injectable({
  providedIn: 'root'
})
export class StorageService {
  getAuthState(): StoredAuthState | null {
    const value = localStorage.getItem(AUTH_STORAGE_KEY);
    if (!value) {
      return null;
    }

    try {
      return JSON.parse(value) as StoredAuthState;
    } catch {
      this.clearAuthState();
      return null;
    }
  }

  setAuthState(state: StoredAuthState): void {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(state));
  }

  clearAuthState(): void {
    localStorage.removeItem(AUTH_STORAGE_KEY);
  }
}
