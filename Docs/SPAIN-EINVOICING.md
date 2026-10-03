# Spanish e-invoicing (Facturae)

nInvoices can issue **Facturae 3.2.2**, the structured invoice Spanish public administrations require
(sent through FACe), signed with your electronic certificate. It is **off by default**: nothing changes
for you, or for users in other countries, until you turn Spain on in *Settings → Invoicing rules by country*.

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

## Limits

- Taxes calculated on top of another tax cannot be expressed in Facturae and are rejected with a message.
  An invoice needs at least one tax line (use a 0% tax for an exempt invoice).
- Withholdings (such as IRPF) are the taxes with a **negative rate**.
- Credit notes (facturas rectificativas) and Verifactu are not implemented yet.
- The signature is XAdES-EPES (the format FACe asks for). Validate your first real invoice in FACe's test
  environment before relying on it.
