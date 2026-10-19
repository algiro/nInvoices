import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'
import { execSync } from 'node:child_process'

// The commit this bundle is built from, shown in Settings > General next to the API's: the Docker build passes
// VITE_APP_COMMIT (there is no .git in the image); a local build asks git.
function appCommit(env: Record<string, string>): string {
  if (env.VITE_APP_COMMIT) return env.VITE_APP_COMMIT
  try {
    const sha = execSync('git rev-parse --short=7 HEAD', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim()
    const dirty = execSync('git status --porcelain', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim() !== ''
    return dirty ? `${sha}-dirty` : sha
  } catch {
    return ''
  }
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
    plugins: [vue()],
    define: {
      // vue-i18n: Composition API only (the legacy API is left out of the bundle)
      __VUE_I18N_FULL_INSTALL__: true,
      __VUE_I18N_LEGACY_API__: false,
      __INTLIFY_PROD_DEVTOOLS__: false,
      __APP_COMMIT__: JSON.stringify(appCommit(env))
    },
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
