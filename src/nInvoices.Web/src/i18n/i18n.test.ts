// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest'
import { availableLanguages, currentLanguage, DEFAULT_LANGUAGE, i18n, languageName, matchLanguage, setLanguage, t } from './index'
import { formatMoney } from '@/utils/format'

afterEach(async () => {
  await setLanguage(DEFAULT_LANGUAGE)
})

describe('i18n', () => {
  it('starts in English', () => {
    expect(DEFAULT_LANGUAGE).toBe('en')
    expect(availableLanguages[0]).toBe('en')
    expect(currentLanguage()).toBe('en')
  })

  it('lists every locale file as a language', () => {
    expect(availableLanguages).toContain('it')
  })

  it.each([
    ['it', 'it'],
    ['IT', 'it'],
    ['it-CH', 'it'],
    ['it_IT', 'it'],
    ['xx', null],
    ['', null],
    [null, null]
  ])('matches %s to %s', (language, expected) => {
    expect(matchLanguage(language)).toBe(expected)
  })

  it('switches every text, and the html lang, to the chosen language', async () => {
    await setLanguage('it')

    expect(currentLanguage()).toBe('it')
    expect(document.documentElement.lang).toBe('it')
    expect(t('common.cancel')).toBe('Annulla')
  })

  it('remembers the language in this browser', async () => {
    await setLanguage('it')

    expect(localStorage.getItem('ninvoices.language')).toBe('it')
  })

  it('falls back to English for a language the app does not have', async () => {
    expect(await setLanguage('xx')).toBe('en')
    expect(currentLanguage()).toBe('en')
  })

  it('shows the English text for a key a language lacks', () => {
    // A language whose file is missing a key, as a new translation may be
    i18n.global.setLocaleMessage('zz', { common: { cancel: 'Zz-cancel' } } as never)
    i18n.global.locale.value = 'zz'

    expect(t('common.cancel')).toBe('Zz-cancel')
    expect(t('common.save')).toBe('Save')
  })

  it('formats money the way the language writes it', async () => {
    expect(formatMoney(1234.5, 'EUR')).toBe('€1,234.50')

    await setLanguage('it')

    expect(formatMoney(1234.5, 'EUR')).toContain('234,50')
  })

  it('names each language in itself', () => {
    expect(languageName('en')).toBe('English')
    expect(languageName('it')).toBe('Italiano')
  })
})
