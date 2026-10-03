# Spanish e-invoicing (Facturae and Verifactu)

nInvoices can issue **Facturae 3.2.2**, the structured invoice Spanish public administrations require
(sent through FACe), signed with your electronic certificate, and keep the **Verifactu** record of every
invoice (see [Verifactu](#verifactu)). It is **off by default**: nothing changes for you, or for users in
other countries, until you turn Spain on in *Settings → Invoicing rules by country*.

## What you need

1. **Your Spanish tax data**: legal name, NIF/NIE/CIF, address (with province) and whether you are an
   individual (autónomo) or a company.
2. **An electronic certificate** to sign the invoices (below).
3. For each public-administration customer, its three **DIR3 codes** (accounting office, managing body,
   processing unit). The customer gives them to you; they are also in the DIR3 directory
   (<https://face.gob.es>).

## The signing certificate

The invoice must be signed with a certificate issued by a certification authority that FACe recognises.
A self-signed certificate does **not** work for real invoices.

### Getting one (free for most people)

| You are | Certificate | Where |
|---|---|---|
| An autónomo or any person | **Certificado de persona física** (FNMT) | [sede.fnmt.gob.es](https://www.sede.fnmt.gob.es) → *Certificados* → *Persona física*. Request online, then confirm your identity at an office (or with the DNIe/Cl@ve where offered). |
| A company (SL, SA...) | **Certificado de representante de persona jurídica** (FNMT) or one for the company issued by another authority | Same site; the administrator requests it. |
| Either | Other recognised authorities (Camerfirma, ACCV, Izenpe, Firmaprofesional...) | Their own sites; some charge a fee. |

Notes:

- The **electronic DNI (DNIe)** cannot be used: its private key lives on the card and cannot be exported.
- The certificate must have an **RSA** key. This is the case for the certificates above.
- Certificates expire (FNMT's after 4 years). nInvoices warns on the Settings page and refuses to sign
  with an expired one, so renew in time.

### Exporting it as a `.p12` / `.pfx` file

nInvoices needs the certificate **with its private key** in one PKCS#12 file, protected by a password
you choose. Export it from the browser or computer where you installed it:

- **Windows (Chrome, Edge):** press `Win+R`, run `certmgr.msc` → *Personal* → *Certificates*, right-click
  your certificate → *All tasks* → *Export...* → **Yes, export the private key** → *Personal Information
  Exchange (.PFX)* → set a password. If *Yes, export the private key* is greyed out, the certificate was
  installed as non-exportable: request or install it again, ticking "mark this key as exportable".
- **Firefox:** *Settings → Privacy & Security → View Certificates → Your Certificates* → select it →
  *Backup...* (this gives a `.p12`).
- **macOS:** *Keychain Access* → *My Certificates* → right-click → *Export...* → `.p12`.

Keep the file and its password safe: anyone holding them can sign invoices in your name.

### Uploading it

*Settings → Invoicing rules by country → Spain → Signing certificate*: choose the file, type its
password, **Upload**. nInvoices checks it (password, RSA private key, not expired) and stores the file and
the password **encrypted** (ASP.NET Data Protection); it is never shown again and only used to sign.
You can replace or remove it at any time.

> The Data Protection keys must survive restarts and redeploys, otherwise the stored certificate becomes
> unreadable and has to be uploaded again (see `DataProtection:KeysPath`, and the Gmail tokens, which work
> the same way).

### A certificate for testing

To try the feature without a real certificate you can create a self-signed one. The generated invoices
are well-formed and correctly signed, but **FACe will not accept them**.

```bash
openssl req -x509 -newkey rsa:2048 -nodes -keyout key.pem -out cert.pem -days 365 \
  -subj "/C=ES/O=Test/CN=Ana Perez Garcia"
openssl pkcs12 -export -out test.p12 -inkey key.pem -in cert.pem -password pass:changeit
```

Upload `test.p12` with the password `changeit`.

## Day to day

1. Turn Spain on in Settings and fill in your data and certificate.
2. On each public-administration customer, tick *Public administration* and enter its DIR3 codes
   (*Customers → edit → Spain invoicing*).
3. Create and **finalize** the invoice as usual. The signed Facturae is generated automatically for
   public administrations. For other customers (Facturae is optional between businesses) use
   *Generate* in the **E-invoice** panel of the invoice.
4. **Download** the `.xsig` file from the invoice page and upload it to FACe
   (<https://face.gob.es>). Sending to FACe from nInvoices directly is not built yet.

If the invoice breaks a rule (for example a missing DIR3 code, an invalid NIF, or a customer with no tax
line), the E-invoice panel lists what to fix; fix it and press *Regenerate*.

## Verifactu

**Verifactu** (Real Decreto 1007/2023, Orden HAC/1177/2024) makes billing software keep a tamper-evident record
of every invoice issued and report it to the Tax Agency (AEAT). It is mandatory for autónomos from
**1 July 2027** (companies from 1 January 2027), whoever the customer is, and it is independent of Facturae.
nInvoices implements the **VERI\*FACTU** mode, in which the records are sent to AEAT as invoices are issued
(no event log or record signing is needed in that mode).

What it does once a user turns it on:

- Finalizing an invoice adds a **record** to the user's chain, in the same save as the invoice. Each record holds
  the hash (SHA-256) of the one before, so changing or removing one afterwards breaks the chain. Records are
  append-only: the application refuses to change or delete them, and so does the PostgreSQL database (a trigger).
- **Cancelling** a recorded invoice adds a cancellation record. A recorded invoice cannot be deleted.
- The invoice shows the **QR code** and the legend *Factura verificable en la sede electrónica de la AEAT*.
- The records are **sent to AEAT** in the background, in chain order, authenticated with the user's certificate,
  keeping to the pause AEAT asks for between submissions. A record AEAT rejects stops the ones after it; the
  invoice page and Settings say why.
- Settings can **check the chain** at any time; a record altered or removed shows up there.

### Setting it up on the server (the operator)

Verifactu stays unavailable to users until the server knows who supplies the software. The *producer* is whoever
distributes nInvoices to its users: the records name it as the billing system, and it is the one who signs the
software's *declaración responsable* (a legal duty of the producer; nInvoices cannot sign it for you).

| Setting (env var in `.env`) | Meaning |
|---|---|
| `Compliance:Spain:Verifactu:Environment` (`VERIFACTU_ENVIRONMENT`) | `Test` (AEAT's external test portal) or `Production`. Empty: unavailable. |
| `...:ProducerName`, `...:ProducerTaxId` (`VERIFACTU_PRODUCER_NAME`, `VERIFACTU_PRODUCER_TAX_ID`) | Legal name and NIF (9 characters) of the producer. |
| `...:SystemId` (`VERIFACTU_SYSTEM_ID`) | Two characters that identify the system, `NI` by default. |
| `...:InstallationNumber` (`VERIFACTU_INSTALLATION_NUMBER`) | Identifies this installation, `1` by default. |
| `...:MultipleTaxpayers` (`VERIFACTU_MULTIPLE_TAXPAYERS`) | `true` if this installation serves several taxpayers. |

Start with `Test`: the QR codes then point to AEAT's test portal and records go to the test service, so nothing
reaches the real one. Move to `Production` only after a real invoice has gone through the test environment.

### For each user

1. In *Settings → Invoicing rules by country → Spain*, fill in your data, upload your certificate (AEAT
   authenticates with it) and tick **Verifactu**. If your certificate is a company *seal* certificate (certificado
   de sello), tick that too; a personal certificate needs nothing.
2. Every invoice needs **one VAT line**. If an invoice charges 0% VAT, open that tax (Taxes → edit) and say why:
   exempt (which article), not subject (place-of-supply rules, or other), or reverse charge. Without it the
   invoice cannot be finalized, and says so.
3. Issue invoices as usual. The invoice page shows the QR code, the record, and where it stands with AEAT.
4. Custom invoice templates need the QR block added by hand; see `Docs/TEMPLATE-GUIDE.md`. The default template
   already has it.

Once Verifactu has recorded invoices, do not change your NIF in the Spanish settings, and keep Verifactu on:
turning it off leaves a gap in the chain.

### What has and has not been verified

Checked against AEAT's own published material: the hash against the three official test vectors; the QR URL
against the official examples (and decoded back from a rendered QR); the record XML and the SOAP answers against
the official XSDs; the service addresses against the official WSDL; and the real HTTP client against a local
server that requires a client certificate.

**Not checked: a real submission to AEAT.** That needs a real certificate and AEAT's test portal. Before
using it for real invoices, send a few from the test environment and check the answers. In particular, confirm:

- that `ImporteTotal` (and the QR amount) leaving out IRPF withholdings is what AEAT expects (it is how the
  secondary sources describe it; the schemas do not say);
- the exempt and not-subject operation codes you pick for 0% VAT;
- the first submission's answers (a record accepted *with errors* is shown with AEAT's remark).

## Limits

- Taxes calculated on top of another tax cannot be expressed in Facturae or Verifactu and are rejected with a message.
  An invoice needs at least one tax line (use a 0% tax for an exempt invoice); Verifactu needs exactly one VAT line.
- Withholdings (such as IRPF) are the taxes with a **negative rate**.
- Corrective invoices (facturas rectificativas) are not implemented, in Facturae or Verifactu. To correct an
  invoice you cancel it (which records a cancellation) and issue a new one. Verifactu's *subsanación* (correcting
  a record AEAT accepted with errors) is not implemented either.
- Verifactu only covers complete invoices (type F1) to customers with a tax id. Simplified invoices (F2) are not supported.
- The Facturae signature is XAdES-EPES (the format FACe asks for). Validate your first real invoice in FACe's test
  environment before relying on it. Sending to FACe from nInvoices directly is not built yet.
- B2B e-invoicing under the Crea y Crece law (UBL/EN 16931) is not implemented.
