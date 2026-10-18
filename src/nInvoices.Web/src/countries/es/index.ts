import { defineAsyncComponent } from 'vue'
import type { CountryUi } from '../types'

/** Spain: Verifactu (the record chain in Settings, the record and QR code of each invoice) */
export const spain: CountryUi = {
  code: 'ES',
  complianceSettings: defineAsyncComponent(() => import('./VerifactuChainSettings.vue')),
  invoicePanels: [defineAsyncComponent(() => import('./InvoiceVerifactuPanel.vue'))]
}
