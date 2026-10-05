#!/usr/bin/env node
// Decrypts a passphrase-protected nInvoices backup without nInvoices (Node 18+, no packages).
//
//   node tools/decrypt-backup.mjs ninvoices-backup-2026-10-05.json > backup.json
//
// The passphrase is asked for on the terminal (or read from NINVOICES_BACKUP_PASSPHRASE).
// Format: see src/nInvoices.Web/src/utils/backupCrypto.ts and Docs/ENCRYPTION.md.
import { readFileSync } from 'node:fs'
import { pbkdf2Sync, createDecipheriv } from 'node:crypto'
import { createInterface } from 'node:readline'

const FORMAT = 'ninvoices-encrypted-backup'

const file = process.argv[2]
if (!file) {
  console.error('Usage: node tools/decrypt-backup.mjs <encrypted backup file> > backup.json')
  process.exit(2)
}

const backup = JSON.parse(readFileSync(file, 'utf8'))
if (backup.format !== FORMAT) {
  console.error(`${file} is not an encrypted nInvoices backup (it may be a plain one already).`)
  process.exit(2)
}
if (backup.version !== 1 || backup.kdf?.name !== 'PBKDF2' || backup.kdf?.hash !== 'SHA-256' || backup.cipher?.name !== 'AES-GCM') {
  console.error('Unsupported backup version.')
  process.exit(2)
}

const passphrase = process.env.NINVOICES_BACKUP_PASSPHRASE ?? (await ask('Passphrase: '))
const key = pbkdf2Sync(passphrase.normalize('NFC'), Buffer.from(backup.kdf.salt, 'base64'), backup.kdf.iterations, 32, 'sha256')
const data = Buffer.from(backup.data, 'base64')
const decipher = createDecipheriv('aes-256-gcm', key, Buffer.from(backup.cipher.iv, 'base64'))
decipher.setAAD(Buffer.from(`${FORMAT}/${backup.version}`, 'utf8'))
decipher.setAuthTag(data.subarray(data.length - 16))
try {
  process.stdout.write(Buffer.concat([decipher.update(data.subarray(0, data.length - 16)), decipher.final()]))
} catch {
  console.error('Wrong passphrase, or the file is damaged.')
  process.exit(1)
}

function ask(question) {
  // Hides what is typed: the passphrase isn't echoed to the terminal
  const rl = createInterface({ input: process.stdin, output: process.stderr, terminal: true })
  rl._writeToOutput = (text) => { if (text.startsWith(question)) process.stderr.write(question) }
  return new Promise((resolve) => rl.question(question, (answer) => { rl.close(); process.stderr.write('\n'); resolve(answer) }))
}
