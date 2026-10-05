/**
 * Passphrase protection for backup files, done in the browser: the passphrase never leaves it.
 *
 * An encrypted backup is a small JSON envelope:
 *   { format: "ninvoices-encrypted-backup", version: 1,
 *     kdf:    { name: "PBKDF2", hash: "SHA-256", iterations, salt },   // salt: base64, 16 bytes
 *     cipher: { name: "AES-GCM", iv },                                  // iv: base64, 12 bytes
 *     data:   base64(ciphertext | 16-byte tag) }
 * The key is PBKDF2-SHA-256(passphrase, salt, iterations), 256 bits; the plaintext is the backup's
 * JSON in UTF-8; the format and version are authenticated as additional data. Any tool with PBKDF2
 * and AES-GCM can open it (Docs/ENCRYPTION.md shows how, without nInvoices).
 */

export const ENCRYPTED_BACKUP_FORMAT = 'ninvoices-encrypted-backup'
const VERSION = 1
// OWASP's recommendation for PBKDF2-SHA-256 (2023): about a second in a browser, slow to guess
const ITERATIONS = 600_000
export const MIN_PASSPHRASE_LENGTH = 12

export interface EncryptedBackup {
  format: typeof ENCRYPTED_BACKUP_FORMAT
  version: number
  kdf: { name: 'PBKDF2'; hash: 'SHA-256'; iterations: number; salt: string }
  cipher: { name: 'AES-GCM'; iv: string }
  data: string
}

/** Thrown when the passphrase is wrong or the file was altered: AES-GCM can't tell the two apart. */
export class WrongPassphraseError extends Error {
  constructor() {
    super('Wrong passphrase, or the file is damaged.')
    this.name = 'WrongPassphraseError'
  }
}

export function isEncryptedBackup(value: unknown): value is EncryptedBackup {
  return typeof value === 'object' && value !== null && (value as { format?: unknown }).format === ENCRYPTED_BACKUP_FORMAT
}

export async function encryptBackup(json: string, passphrase: string): Promise<EncryptedBackup> {
  const salt = crypto.getRandomValues(new Uint8Array(16))
  const iv = crypto.getRandomValues(new Uint8Array(12))
  const key = await deriveKey(passphrase, salt, ITERATIONS)
  const ciphertext = await crypto.subtle.encrypt(
    { name: 'AES-GCM', iv, additionalData: associatedData(VERSION) },
    key,
    new TextEncoder().encode(json),
  )
  return {
    format: ENCRYPTED_BACKUP_FORMAT,
    version: VERSION,
    kdf: { name: 'PBKDF2', hash: 'SHA-256', iterations: ITERATIONS, salt: toBase64(salt) },
    cipher: { name: 'AES-GCM', iv: toBase64(iv) },
    data: toBase64(new Uint8Array(ciphertext)),
  }
}

export async function decryptBackup(backup: EncryptedBackup, passphrase: string): Promise<string> {
  if (backup.version !== VERSION || backup.kdf?.name !== 'PBKDF2' || backup.kdf.hash !== 'SHA-256' || backup.cipher?.name !== 'AES-GCM')
    throw new Error('This encrypted backup was made by a newer version of nInvoices.')

  const key = await deriveKey(passphrase, fromBase64(backup.kdf.salt), backup.kdf.iterations)
  try {
    const plaintext = await crypto.subtle.decrypt(
      { name: 'AES-GCM', iv: fromBase64(backup.cipher.iv), additionalData: associatedData(backup.version) },
      key,
      fromBase64(backup.data),
    )
    return new TextDecoder().decode(plaintext)
  } catch {
    throw new WrongPassphraseError()
  }
}

async function deriveKey(passphrase: string, salt: Uint8Array<ArrayBuffer>, iterations: number): Promise<CryptoKey> {
  const material = await crypto.subtle.importKey('raw', new TextEncoder().encode(passphrase.normalize('NFC')), 'PBKDF2', false, ['deriveKey'])
  return crypto.subtle.deriveKey(
    { name: 'PBKDF2', hash: 'SHA-256', salt, iterations },
    material,
    { name: 'AES-GCM', length: 256 },
    false,
    ['encrypt', 'decrypt'],
  )
}

function associatedData(version: number): Uint8Array<ArrayBuffer> {
  return new TextEncoder().encode(`${ENCRYPTED_BACKUP_FORMAT}/${version}`)
}

function toBase64(bytes: Uint8Array): string {
  let binary = ''
  for (let i = 0; i < bytes.length; i += 0x8000)
    binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000))
  return btoa(binary)
}

function fromBase64(text: string): Uint8Array<ArrayBuffer> {
  const binary = atob(text)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i)
  return bytes
}
