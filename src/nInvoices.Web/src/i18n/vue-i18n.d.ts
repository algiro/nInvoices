import type { MessageSchema } from './index'

// English (src/locales/en.json) is the schema of every language: editors complete the keys of t('…').
// src/i18n/locales.test.ts fails when the code uses a key English doesn't have.
declare module 'vue-i18n' {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  export interface DefineLocaleMessage extends MessageSchema {}
}
