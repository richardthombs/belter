import { defineConfig } from 'vite'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [tailwindcss()],
  server: {
    proxy: {
      // Proxy REST API calls to the Gateway in local development
      '/api': 'http://localhost:5080',
      // Proxy SignalR WebSocket connections to the Gateway
      '/hubs': {
        target: 'http://localhost:5080',
        ws: true,
        changeOrigin: true,
      },
    },
  },
})
