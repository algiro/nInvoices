import { describe, it, expect } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'
import { join, relative } from 'node:path'
import { baseCompile } from '@intlify/message-compiler'

// The locale files checked against each other and against the code: English is the reference.

const SRC = join(__dirname, '..')
const LOCALES = join(SRC, 'locales')

type Messages = { [key: string]: string | Messages }

function flatten(messages: Messages, prefix = ''): Record<string, string> {
  const result: Record<string, string> = {}
  for (const [key, value] of Object.entries(messages)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (typeof value === 'string') result[path] = value
    else Object.assign(result, flatten(value, path))
  }
  return result
}

const files = readdirSync(LOCALES).filter(f => f.endsWith('.json'))
const locales = Object.fromEntries(
  files.map(f => [f.replace(/\.json$/, ''), flatten(JSON.parse(readFileSync(join(LOCALES, f), 'utf8')) as Messages)])
)
const english = locales.en

/** {name} placeholders, sorted; the plural forms of a text share them */
function placeholders(text: string): string[] {
  return [...new Set([...text.matchAll(/\{(\w+)\}/g)].map(m => m[1]))].sort()
}

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return entry.name === 'locales' ? [] : sourceFiles(path)
    return /\.(vue|ts)$/.test(entry.name) && !entry.name.endsWith('.test.ts') && !entry.name.endsWith('.generated.ts') ? [path] : []
  })
}

describe('locale files', () => {
  it('include English', () => {
    expect(Object.keys(locales)).toContain('en')
  })

  it.each(Object.keys(locales))('%s: every text is valid vue-i18n syntax', language => {
    const broken: string[] = []
    for (const [key, text] of Object.entries(locales[language])) {
      // '@', '|', '{' and '}' are syntax: a literal one is written {'@'}
      baseCompile(text, { onError: error => broken.push(`${key}: ${error.message}`) })
    }
    expect(broken).toEqual([])
  })

  it.each(Object.keys(locales).filter(l => l !== 'en'))('%s: has no key English lacks', language => {
    // A key only a translation has is a typo or a leftover
    expect(Object.keys(locales[language]).filter(key => !(key in english))).toEqual([])
  })

  it.each(Object.keys(locales).filter(l => l !== 'en'))('%s: keeps the placeholders of English', language => {
    const differing = Object.entries(locales[language])
      .filter(([key, text]) => placeholders(text).join() !== placeholders(english[key] ?? '').join())
      .map(([key]) => key)
    expect(differing).toEqual([])
  })

  it.each(Object.keys(locales).filter(l => l !== 'en'))('%s: keeps the number of plural forms of English', language => {
    const forms = (text: string) => text.split(/(?<!\{'?)\|(?!'?\})/).length
    const differing = Object.entries(locales[language])
      .filter(([key, text]) => key in english && forms(text) !== forms(english[key]))
      .map(([key]) => key)
    expect(differing).toEqual([])
  })
})

describe('the code', () => {
  it('uses only keys English has', () => {
    const missing: string[] = []
    for (const file of sourceFiles(SRC)) {
      const source = readFileSync(file, 'utf8')
      // t('key'), $t('key'), keypath="key"; keys built at run time (t(`a.${b}`)) are not checked here
      const keys = [...source.matchAll(/\bt\(\s*'([\w.]+)'/g), ...source.matchAll(/keypath="([\w.]+)"/g)].map(m => m[1])
      for (const key of keys) {
        if (!(key in english)) missing.push(`${relative(SRC, file)}: ${key}`)
      }
    }
    expect(missing).toEqual([])
  })
})
