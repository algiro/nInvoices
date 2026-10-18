import type { Component } from 'vue'
import type { CountryUi } from './types'
import { spain } from './es'

export type { CountryUi } from './types'

/** The countries with screens of their own. A new one: its folder, and one entry here. */
const countries: readonly CountryUi[] = [spain]

/** What the country adds to the screens, if anything */
export function countryUi(code: string): CountryUi | undefined {
  return countries.find(c => c.code === code.toUpperCase())
}

/** Every country's panels for the invoice page; each hides itself when it doesn't apply */
export const countryInvoicePanels: readonly Component[] = countries.flatMap(c => c.invoicePanels ?? [])
