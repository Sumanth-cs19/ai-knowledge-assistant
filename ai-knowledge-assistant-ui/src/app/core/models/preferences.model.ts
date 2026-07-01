export interface UserPreferences {
  defaultChatBehavior: 'new-chat' | 'continue-last';
  streamingEnabled: boolean;
  compactSidebar: boolean;
}

export const DEFAULT_USER_PREFERENCES: UserPreferences = {
  defaultChatBehavior: 'new-chat',
  streamingEnabled: true,
  compactSidebar: false
};
