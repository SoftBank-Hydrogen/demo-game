import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const proxy = {
  '/api': 'http://127.0.0.1:8000',
  '/uploads': 'http://127.0.0.1:8000',
};

// FastAPI serves ../static in the single-server build.
export default defineConfig({
  plugins: [react()],
  base: './',
  build: { outDir: '../static', emptyOutDir: true },
  server: { host: '127.0.0.1', port: 5173, strictPort: true, proxy },
  preview: { host: '127.0.0.1', port: 4173, strictPort: true, proxy },
});
