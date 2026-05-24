export const appConfig = {
  apiUrl: import.meta.env.VITE_API_URL ?? 'https://localhost:44305',
  oidcAuthority: import.meta.env.VITE_OIDC_AUTHORITY ?? 'https://localhost:44305',
  oidcClientId: import.meta.env.VITE_OIDC_CLIENT_ID ?? 'KnowledgeHub_StudentReact',
  appBaseUrl: import.meta.env.VITE_APP_BASE_URL ?? window.location.origin,
};
