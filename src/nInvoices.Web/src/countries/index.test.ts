import { describe, it, expect } from 'vitest'
import { countryUi, countryInvoicePanels } from './index'

describe('countryUi', () => {
  it('finds a country by its code, whatever the case', () => {
    expect(countryUi('ES')?.code).toBe('ES')
    expect(countryUi('es')?.code).toBe('ES')
  })

  it('has nothing for a country without screens of its own', () => {
    expect(countryUi('FR')).toBeUndefined()
  })

  it('collects the invoice panels of every country', () => {
    expect(countryInvoicePanels.length).toBeGreaterThan(0)
  })
})
