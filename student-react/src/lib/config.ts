export const appConfig = {
  apiUrl: import.meta.env.VITE_API_URL ?? window.location.origin,
  oidcAuthority: import.meta.env.VITE_OIDC_AUTHORITY ?? `${window.location.origin}/`,
  oidcClientId: import.meta.env.VITE_OIDC_CLIENT_ID ?? 'KnowledgeHub_App',
  appBaseUrl: import.meta.env.VITE_APP_BASE_URL ?? window.location.origin,
  adminUrl: import.meta.env.VITE_ADMIN_URL ?? `${window.location.origin}/admin/`,
};
