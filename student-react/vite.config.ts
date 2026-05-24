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
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
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
    },
  },
});
