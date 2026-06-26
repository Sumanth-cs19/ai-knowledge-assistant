import { Injectable, computed, signal } from '@angular/core';

import { DEFAULT_USER_PREFERENCES, ThemePreference, UserPreferences } from '../models/preferences.model';

const PREFERENCES_STORAGE_KEY = 'ai-knowledge-assistant.preferences';

@Injectable({
  providedIn: 'root'
})
export class PreferencesService {
  private readonly preferencesState = signal<UserPreferences>(this.loadPreferences());
  readonly preferences = computed(() => this.preferencesState());
  readonly isCompactSidebar = computed(() => this.preferencesState().compactSidebar);

  initializeTheme(): void {
    this.applyTheme(this.preferencesState().theme);
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
      if (this.preferencesState().theme === 'system') {
        this.applyTheme('system');
      }
    });
  }

  updatePreferences(preferences: UserPreferences): void {
    this.preferencesState.set(preferences);
    localStorage.setItem(PREFERENCES_STORAGE_KEY, JSON.stringify(preferences));
    this.applyTheme(preferences.theme);
  }

  updateTheme(theme: ThemePreference): void {
    this.updatePreferences({
      ...this.preferencesState(),
      theme
    });
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
      return {
        ...DEFAULT_USER_PREFERENCES,
        ...JSON.parse(rawValue) as Partial<UserPreferences>
      };
    } catch {
      return DEFAULT_USER_PREFERENCES;
    }
  }

  private applyTheme(theme: ThemePreference): void {
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const resolvedTheme = theme === 'system'
      ? prefersDark ? 'dark' : 'light'
      : theme;

    document.documentElement.dataset['theme'] = resolvedTheme;
    document.body.dataset['theme'] = resolvedTheme;
  }
}
