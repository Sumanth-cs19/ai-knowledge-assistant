export type ThemePreference = 'light' | 'dark' | 'system';

export interface UserPreferences {
  theme: ThemePreference;
  defaultChatBehavior: 'new-chat' | 'continue-last';
  streamingEnabled: boolean;
  compactSidebar: boolean;
}

export const DEFAULT_USER_PREFERENCES: UserPreferences = {
  theme: 'system',
  defaultChatBehavior: 'new-chat',
  streamingEnabled: true,
  compactSidebar: false
};
