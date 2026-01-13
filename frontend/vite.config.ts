import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Check if certificate files exist (for development mode)
const certKeyPath = path.resolve(__dirname, '../certificates/dev-https-key.pem');
const certPath = path.resolve(__dirname, '../certificates/dev-https-cert.pem');
const hasCerts = fs.existsSync(certKeyPath) && fs.existsSync(certPath);

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 3000,
    ...(hasCerts && {
      https: {
        key: fs.readFileSync(certKeyPath),
        cert: fs.readFileSync(certPath)
      }
    })
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          'react-vendor': ['react', 'react-dom'],
          'antd-vendor': ['antd', '@ant-design/icons'],
          'apollo-vendor': ['@apollo/client', 'graphql'],
          'auth0-vendor': ['@auth0/auth0-react']
        }
      }
    },
    chunkSizeWarningLimit: 600
  }
});
