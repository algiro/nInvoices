import { defineConfig, mergeConfig } from 'vitest/config'
import viteConfig from './vite.config'

export default defineConfig(env =>
  mergeConfig(viteConfig(env), {
    test: {
      // Plain logic runs in Node; a test importing browser-bound modules (stores, the auth service)
      // starts with: // @vitest-environment jsdom
      environment: 'node',
      include: ['src/**/*.test.ts']
    }
  })
)
