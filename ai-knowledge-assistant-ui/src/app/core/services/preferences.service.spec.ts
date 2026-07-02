import { TestBed } from '@angular/core/testing';

import { DEFAULT_USER_PREFERENCES } from '../models/preferences.model';
import { PreferencesService } from './preferences.service';

describe('PreferencesService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  it('uses light-only defaults without a theme preference', () => {
    const service = TestBed.inject(PreferencesService);

    expect(service.preferences()).toEqual(DEFAULT_USER_PREFERENCES);
    expect('theme' in service.preferences()).toBe(false);
  });

  it('migrates legacy stored preferences and ignores the old theme value', () => {
    localStorage.setItem('ai-knowledge-assistant.preferences', JSON.stringify({
      theme: 'dark',
      defaultChatBehavior: 'continue-last',
      streamingEnabled: false,
      compactSidebar: true
    }));

    const service = TestBed.inject(PreferencesService);

    expect(service.preferences()).toEqual({
      defaultChatBehavior: 'continue-last',
      streamingEnabled: false,
      compactSidebar: true
    });
  });

  it('persists saved sidebar and streaming preferences', () => {
    const service = TestBed.inject(PreferencesService);
    service.updatePreferences({
      defaultChatBehavior: 'new-chat',
      streamingEnabled: false,
      compactSidebar: true
    });

    expect(JSON.parse(localStorage.getItem('ai-knowledge-assistant.preferences') ?? '{}')).toEqual({
      defaultChatBehavior: 'new-chat',
      streamingEnabled: false,
      compactSidebar: true
    });
    expect(service.isCompactSidebar()).toBe(true);
  });

  it('previews compact sidebar without persisting and reverts to the saved value', () => {
    const service = TestBed.inject(PreferencesService);

    service.previewCompactSidebar(true);

    expect(service.isCompactSidebar()).toBe(true);
    expect(service.preferences().compactSidebar).toBe(false);
    expect(localStorage.getItem('ai-knowledge-assistant.preferences')).toBeNull();

    service.clearCompactSidebarPreview();

    expect(service.isCompactSidebar()).toBe(false);
  });
});
