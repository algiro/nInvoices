import type { Component } from 'vue'

/**
 * What one country adds to the screens, beside what the API describes (fields, regimes, formats), which the
 * shared screens already show. Each country lives in its own folder (`countries/<code>/`) and is listed in
 * `countries/index.ts`; nothing else names a country. Components are loaded when first shown.
 */
export interface CountryUi {
  /** ISO 3166-1 alpha-2 code, as the API gives it */
  code: string
  /**
   * Shown in the country's block of "Invoicing rules by country", after its fields.
   * Props: `country: ComplianceCountryDto`.
   */
  complianceSettings?: Component
  /**
   * Panels on the invoice page. Props: `invoiceId: number`, `status: string` (to look again when it changes).
   * Each one hides itself when it has nothing to show for the invoice.
   */
  invoicePanels?: Component[]
}
