import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  resolve: {
    dedupe: ['chart.js'],
  },
  optimizeDeps: {
    // Force Vite to pre-bundle chart.js so pptxviewjs can find it
    include: ['chart.js/auto'],
  },
  server: {
    port: 3000,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/connect': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/.well-known': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/signin-oidc': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/signout-callback-oidc': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/AbpApplicationConfiguration': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
    },
  },
  preview: {
    proxy: {
      '/api': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/connect': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/.well-known': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/signin-oidc': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/signout-callback-oidc': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
      '/AbpApplicationConfiguration': {
        target: 'https://localhost:44305',
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
