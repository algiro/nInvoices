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
4. Send it to FACe: either press *Send to FACe* in the **E-invoice** panel (see [FACe](#face-sending-to-public-administrations)),
   or **download** the `.xsig` file and upload it at <https://face.gob.es> yourself.

If the invoice breaks a rule (for example a missing DIR3 code, an invalid NIF, or a customer with no tax
line), the E-invoice panel lists what to fix; fix it and press *Regenerate*.

## FACe: sending to public administrations

**FACe** is the portal through which invoices reach Spanish public administrations. nInvoices can send the signed
Facturae there for you and show where the invoice stands (registered, accounted, paid, rejected...).

### Setting it up on the server (the operator)

Set `FACE_ENVIRONMENT` (`Compliance:Spain:Face:Environment`):

- `Test`: FACe's staging service (`se-ws-face.redsara.es`). Nothing reaches a real administration. Start here.
- `Production`: the real service (`ws.face.gob.es`).

Left empty, users do not see the *Send* button. `FACE_SIGNATURE_ALGORITHM` is `Sha256` by default; FACe's own
documentation shows SHA-1, so if FACe refuses the signature, set `Sha1`.

### For each user

1. In FACe's staging portal (see the FACe site for its address) register the **same certificate** you uploaded to
   nInvoices as the one of your provider account. FACe only accepts requests signed with a registered certificate.
   (Without that FACe answers with a fault saying the certificate is unknown, and nInvoices shows it.)
2. In the Spanish settings fill in *Email for FACe notifications*.
3. Finalize the invoice, generate the Facturae and press *Send to FACe*. On success you get FACe's **registry
   code**; *Refresh status* asks FACe where the invoice stands now.

Rules to know:

- Only invoices to **public administrations** go through FACe.
- Sending is always your own action, never automatic, and it **cannot be taken back**. Once sent, the Facturae
  file can no longer be regenerated and the invoice can no longer be deleted.
- A refusal by FACe (unknown certificate, invalid file...) shows FACe's message and stores nothing, so you can fix
  the problem and send again.
- Asking for a *cancellation* of a sent invoice (solicitud de anulación) is done in FACe itself; nInvoices shows
  its status after a refresh.

### What has and has not been verified

Checked: the requests against FACe's published documentation (operation names, namespace, SOAPAction,
WS-Security layout); the signature of the body, which verifies with an independent implementation; the client
against a local stub, including SOAP faults and network errors.

**Not checked: a real exchange with FACe's staging service.** The first send is the real test. Things that may
need adjusting, in this order of likelihood: the signature algorithm (`Sha1` option), the shape of the
production namespace, and the fields of FACe's answer (it is read tolerantly by element name). Response
signatures from FACe are not verified.

## Canary Islands: IGIC

In the Canary Islands the indirect tax is **IGIC** (Impuesto General Indirecto Canario), not IVA. Both Facturae and
Verifactu name the tax, so nInvoices needs to know which one a tax is:

1. In *Taxes*, open (or create) the tax, for example *IGIC 7%* with the rate 7. Under the Spanish fields, set
   **Which tax is this?** to *IGIC (Canary Islands)*. Left empty, a tax is IVA.
2. Keep the **withholding** as a separate tax with a **negative rate** (for example *IRPF -15%*). It needs no
   special setting: it is written as an IRPF withholding in Facturae and, as for IVA, is not part of what
   Verifactu reports.

What changes for an IGIC tax:

- **Facturae**: the tax is written with tax type `03` (IGIC) instead of `01` (IVA), in the invoice totals and on
  every line.
- **Verifactu**: the record carries `Impuesto = 03` and the general regime key (`ClaveRegimen 01`).
- **Zero rates**: IGIC has a real zero rate (*tipo cero*). For a tax at 0% choose *Taxed at 0%* under *Verifactu
  treatment of a 0% tax*. For an exempt or not-subject operation choose the matching option instead; **E7** and
  **E8** exist for IGIC only, and AEAT's own list gives a different article for each of E1 to E8 depending on the
  tax (the option labels name both).

Only the general regime is supported. IGIC's special regimes (simplified regime, small retailers, travel agencies and
so on, which need other regime keys) are not, and an invoice has one IGIC line, like one VAT line. Not checked: a real
submission of an IGIC invoice to AEAT's test environment. The rules used here come from AEAT's published validation
rules (version 1.2.2) and schemas; whether the general regime key `01` is in AEAT's list for IGIC (L8B) is taken from
secondary sources.

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
2. Every invoice needs **one VAT (or IGIC) line**. If an invoice charges 0% VAT, open that tax (Taxes → edit) and say why:
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
  An invoice needs at least one tax line (use a 0% tax for an exempt invoice); Verifactu needs exactly one VAT (or IGIC) line.
- Withholdings (such as IRPF) are the taxes with a **negative rate**.
- Corrective invoices (facturas rectificativas) are not implemented, in Facturae or Verifactu. To correct an
  invoice you cancel it (which records a cancellation) and issue a new one. Verifactu's *subsanación* (correcting
  a record AEAT accepted with errors) is not implemented either.
- Verifactu only covers complete invoices (type F1) to customers with a tax id. Simplified invoices (F2) are not supported.
- The Facturae signature is XAdES-EPES (the format FACe asks for). Validate your first real invoice in FACe's test
  environment before relying on it.
- FACe: sending and reading the status are built; requesting a cancellation (anulación) and the detailed list of
  status changes are not.
- B2B e-invoicing under the Crea y Crece law (UBL/EN 16931) is not implemented.
