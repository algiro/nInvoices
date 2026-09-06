import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
    plugins: [vue()],
    base: env.VITE_BASE || '/',
    resolve: {
      alias: {
        '@': path.resolve(__dirname, './src')
      }
    },
    server: {
      // PORT + VITE_PROXY_TARGET let the Aspire AppHost drive these; the defaults
      // are the standalone `npm run dev` values.
      host: true,          // bind 0.0.0.0 so the Aspire reverse proxy can reach it
      port: Number(process.env.PORT) || 3000,
      strictPort: true,
      proxy: {
        '/api': {
          target: process.env.VITE_PROXY_TARGET || 'http://localhost:5297',
          changeOrigin: true
        }
      }
    }
  }
})
