import { Injectable, computed, signal } from '@angular/core';

import { DEFAULT_USER_PREFERENCES, UserPreferences } from '../models/preferences.model';

const PREFERENCES_STORAGE_KEY = 'ai-knowledge-assistant.preferences';

@Injectable({
  providedIn: 'root'
})
export class PreferencesService {
  private readonly preferencesState = signal<UserPreferences>(this.loadPreferences());
  private readonly compactSidebarPreview = signal<boolean | null>(null);
  readonly preferences = computed(() => this.preferencesState());
  readonly isCompactSidebar = computed(() => {
    return this.compactSidebarPreview() ?? this.preferencesState().compactSidebar;
  });

  updatePreferences(preferences: UserPreferences): void {
    const savedPreferences = { ...preferences };
    this.preferencesState.set(savedPreferences);
    this.compactSidebarPreview.set(null);
    localStorage.setItem(PREFERENCES_STORAGE_KEY, JSON.stringify(savedPreferences));
  }

  previewCompactSidebar(compact: boolean): void {
    this.compactSidebarPreview.set(compact);
  }

  clearCompactSidebarPreview(): void {
    this.compactSidebarPreview.set(null);
  }

  resetPreferences(): void {
    this.updatePreferences(DEFAULT_USER_PREFERENCES);
  }

  private loadPreferences(): UserPreferences {
    const rawValue = localStorage.getItem(PREFERENCES_STORAGE_KEY);
    if (!rawValue) {
      return DEFAULT_USER_PREFERENCES;
    }

    try {
      const stored = JSON.parse(rawValue) as Partial<UserPreferences>;
      return {
        defaultChatBehavior: stored.defaultChatBehavior === 'continue-last' ? 'continue-last' : 'new-chat',
        streamingEnabled: stored.streamingEnabled ?? DEFAULT_USER_PREFERENCES.streamingEnabled,
        compactSidebar: stored.compactSidebar ?? DEFAULT_USER_PREFERENCES.compactSidebar
      };
    } catch {
      return DEFAULT_USER_PREFERENCES;
    }
  }
}
