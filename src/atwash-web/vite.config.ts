import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
 plugins: [
  react(),
  VitePWA({
   registerType: 'autoUpdate',
   includeAssets: ['favicon.svg'],
   manifest: {
    name: 'ATWashControl - Gestão de Lava-Rápido',
    short_name: 'ATWashControl',
    description: 'Seu lava-rápido na palma da mão',
    start_url: '/',
    display: 'standalone',
    background_color: '#ffffff',
    theme_color: '#64107b',
    icons: [
     {src:'/icon-192.svg',sizes:'any',type:'image/svg+xml',purpose:'any'},
     {src:'/icon-512.svg',sizes:'any',type:'image/svg+xml',purpose:'any'}
    ]
   },
   workbox: {
    navigateFallback: '/index.html',
    globPatterns: ['**/*.{js,css,html,svg,png,woff2}'],
    runtimeCaching: [],
    navigateFallbackDenylist: [/^\/api\//]
   }
  })
 ],
 server: { proxy: { '/api': { target: 'http://localhost:5098', changeOrigin: true } } }
});
