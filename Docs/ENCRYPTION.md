# Encryption of stored data

nInvoices encrypts the sensitive columns of its database: customer names, tax IDs, addresses and
emails, every amount, invoice contents, notes, templates, e-invoice files and Verifactu records.
Someone who gets a copy of the database (a dump, a backup, a stolen disk, read access to
PostgreSQL) sees `enc1:…` instead of the data.

It does **not** protect against whoever runs the server: the API decrypts data to show it, render
PDFs and send e-invoices, so the server operator can always reach it. People who need to keep their
data from the operator should run their own instance.

## How it works

```
Master key (one per server)    a file, kept OUTSIDE the database and its backups
   │ wraps
   ▼
User key (one per user)        table "UserKeys", stored wrapped by the master key
   │ encrypts (AES-256-GCM)
   ▼
Sensitive columns              "enc1:" + base64(key id | nonce | tag | ciphertext)
```

- Each user gets a random 256-bit key on their first save. It is stored encrypted with the
  master key, and the API keeps it in memory only while running.
- Every value gets a fresh random nonce, and is tied to its column: a value copied into another
  column, or altered, fails to decrypt instead of being shown.
- What stays in clear: ids, dates, statuses, currencies, invoice numbers, tax rates and codes,
  project and rate names, and settings. They are needed for filtering and sorting in SQL.

### For developers

- A column is encrypted by `.IsEncrypted("Entity.Column")` in its EF configuration
  (`Infrastructure/Data/Configurations`). The name is authenticated with every value: **never
  rename it** once data is stored.
- An encrypted column is stored as text whatever its .NET type (decimals too), so **SQL can't
  filter, sort, group or sum it**. Load the rows and do it in memory, as `InvoiceRepository` does
  for the customer-name search and the sort by customer and total. A query comparing an encrypted
  column with a value fails with an `InvalidOperationException` rather than silently matching
  nothing.
- Values are encrypted in `ApplicationDbContext.SaveChangesAsync`, with the current user's key.
  `ExecuteUpdate` and raw SQL bypass it: don't use them on encrypted columns.
- Values stored before a column was encrypted have no `enc1:` prefix. They are read as they are and
  encrypted in place at the next startup (`LegacyDataEncryption`). The same happens after restoring
  an older backup.

## The master key

| Setting | Default | |
|---|---|---|
| `Encryption:KeyFile` | `encryption/master.key` under the API's content root | 32 random bytes, base64, one line |
| `Encryption:CreateKeyFileIfMissing` | `true` in Development only | never in production: a new key can't read the existing data |
| `Encryption:PreviousKeyFiles` | empty | comma-separated, only while rotating (below) |

In production the API **refuses to start** without the key file, and with a key that doesn't match
the stored user keys.

### Creating it (once per server)

On the Docker host, next to `docker-compose.yml`:

```bash
mkdir -p secrets && chmod 700 secrets
(umask 077 && head -c 32 /dev/urandom | base64 > secrets/ninvoices-master.key)
```

The compose files mount it read-only at `/run/secrets/ninvoices-master.key` and set
`Encryption__KeyFile` to it.

### Backing it up: do this before the first start

**Losing the master key means losing all the data, for every user, permanently.** The database
backups can't be read without it. Keep at least two copies, away from the database backups:

1. In your password manager (the file is one short line of text).
2. Encrypted in the infra repo: `infra ittudes fetch` copies it to
   `secrets/ittudes/ninvoices-master.key`, then encrypt it with sops like the other secrets.

Never store it next to the database dumps (`~/backups/ninvoices`) or inside `volumes/`: a backup
that holds both protects nothing.

### Restoring a server

Restore the database dump, put the key file back in `secrets/`, start the stack. The same key, or
the API won't start.

### Rotating it

1. Create a new key file. Set `Encryption:KeyFile` to the new one, and
   `Encryption:PreviousKeyFiles` to the old one.
2. Restart: the API re-wraps every user key with the new master key (the data itself isn't touched,
   so it takes seconds).
3. Remove `PreviousKeyFiles`, restart, back up the new key, and destroy the old one.

## Backups for users

Users don't hold any key: while their account exists, the server can always read their data. A
user's backup is *Settings → Backup and transfer → Download a backup*: one file with

- customers, with their rates, taxes, templates, projects, worked days (and how they were split
  across projects) and expenses not on an invoice yet;
- invoices, with their tax lines and expenses;
- shared templates, the images they use, the holiday calendars the user changed, invoice numbering
  and the e-invoicing settings per country.

Restoring it re-encrypts everything with the keys of whichever server it lands on, so the same file
moves data to another nInvoices server. What already exists is kept as it is (same VAT number, same
invoice number, same image name, same country), and the invoice sequence only ever moves forward.

Not in the backup: the e-invoicing signing certificate (it is encrypted with the server's own keys:
upload it again), sent-email history, generated e-invoice files and Verifactu records (a
tamper-evident log that belongs to the server that produced it), and the server-wide settings.

### Passphrase-protected backups

By default the backup is encrypted **in the browser** with a passphrase the user types (at least
12 characters); the passphrase never reaches the server, and nobody can recover it. Downloading
without a passphrase needs an explicit tick.

The file is a JSON envelope (`format: "ninvoices-encrypted-backup"`): the key is
PBKDF2-SHA-256 of the passphrase (600,000 iterations, random 16-byte salt), the cipher AES-256-GCM
(random 12-byte IV), with `ninvoices-encrypted-backup/1` as additional data. The details are in
`src/nInvoices.Web/src/utils/backupCrypto.ts`.

It can be opened without nInvoices, with Node 18 or later and no packages:

```bash
node tools/decrypt-backup.mjs ninvoices-backup-2026-10-05.encrypted.json > backup.json
```

## Deleting an account (crypto-shredding)

Users delete their own account in *Settings → Delete your account*. They tick that they have a
backup, type `DELETE`, and confirm. Then:

1. **nInvoices deletes all their data** (`DELETE /api/account`, `AccountDataDeletion`): every row of
   every user-data table, in one transaction. The order is worked out from the foreign keys of the
   model, so a new table can't be forgotten. Verifactu records, otherwise append-only, go too. The
   Gmail access is revoked first.
2. **Their data key is destroyed**, so copies of their rows left in database backups can no longer
   be read. The backups still hold a copy of the key itself until they expire (14 days with the
   default rotation): only then is the deletion complete everywhere.
3. **Keycloak deletes the sign-in account.** The web app sends the user to Keycloak's own
   *Delete account* page, which asks them to confirm. If they cancel, the data is gone but the
   account stays, and signing in again gives an empty workspace.

Step 3 needs Keycloak's *Delete Account* action enabled once per server:

```bash
./docker/keycloak/enable-account-deletion.sh
```

Without it, the data is still deleted and the user is told the sign-in account was kept; the
administrator can then remove it in the Keycloak admin console (*Users → (user) → Delete*).

Deleting a user in the Keycloak admin console does **not** delete their nInvoices data: their rows
stay, readable by nobody, since nobody can sign in as them. To remove the data too, ask them to
delete their account from Settings first.

Invoices, and Verifactu records with them, usually have to be kept for years (four in Spain): the
page tells users to download a backup first.
