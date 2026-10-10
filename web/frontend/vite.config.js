import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// `npm run build` writes the static site to ../static (committed): upload that folder to any static host
// (S3 + CloudFront, Nginx). Edit static/config.json there to point at the deployed API.
export default defineConfig({
  plugins: [react()],
  base: './',
  build: { outDir: '../static', emptyOutDir: true },
  server: { host: '127.0.0.1', port: 5173, strictPort: true },
  preview: { host: '127.0.0.1', port: 4173, strictPort: true },
});
