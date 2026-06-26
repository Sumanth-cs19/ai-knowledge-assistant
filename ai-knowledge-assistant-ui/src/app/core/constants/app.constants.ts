import { appEnvironment } from '../../../environments/environment';

export const APP_CONFIG = {
  name: 'AI Knowledge Assistant',
  apiBaseUrl: appEnvironment.api.baseUrl
} as const;
