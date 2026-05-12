/**
 * Production environment. In the default Docker topology, the Angular bundle
 * is served by nginx which reverse-proxies /api and /hubs to the .NET container
 * on the same origin — no absolute URL is needed.
 */
export const environment = {
  production: true,
  apiBase: '',
  hubBase: '',
  authLoginUrl: '/api/auth/login',
  authLogoutUrl: '/api/auth/logout',
  authMeUrl: '/api/auth/me',
} as const;
