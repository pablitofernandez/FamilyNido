/**
 * Development environment. The dev server proxies /api, /hubs and the OIDC
 * callback paths to the .NET backend at localhost:5080 (see proxy.conf.json).
 */
export const environment = {
  production: false,
  apiBase: '',
  hubBase: '',
  authLoginUrl: '/api/auth/login',
  authLogoutUrl: '/api/auth/logout',
  authMeUrl: '/api/auth/me',
} as const;
