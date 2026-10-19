# Spain launch posts (drafts, not for the repo's main branch)

Before posting: check each community's self-promotion rules, answer comments for the first day, and
verify the Verifactu dates and obligations on the AEAT site (they have been postponed before). The
posts below deliberately state no deadline.

Replace `<LINK-DEMO>` with the hosted-instance link and `<GIF>` with the recording.

---

## 1. Long post (r/autonomos, r/SpainFIRE-style forums, foros de autónomos)

**Título:** Hice una herramienta gratuita y auto-alojable para facturar por días/horas (con Facturae, FACe y Verifactu) y busco gente que la pruebe

Soy autónomo desarrollador y me cansé de hacer la factura mensual a mano en Excel/Word: contar los días
trabajados, restar festivos, calcular IVA/IRPF, exportar el PDF, mandar el correo... Así que construí
**nInvoices**, pensada para quien factura **tiempo** (días u horas) a uno o varios clientes.

**Qué hace**
- Marcas los días trabajados en un calendario (los festivos ya vienen marcados) y ves la **factura real
  antes de crearla**. Genera la factura y el parte de horas en PDF con tu propia plantilla HTML.
- Tarifas por día/hora/mes, proyectos por cliente, gastos, IVA, **IGIC** y retenciones tipo **IRPF**.
- Numeración configurable, ciclo de vida de la factura (borrador, emitida, enviada, pagada, anulada).
- **Facturae 3.2.2 firmada (XAdES)** y envío a **FACe** para facturar a administraciones públicas.
- **Verifactu**: registro encadenado por hash, QR y leyenda en la factura, y envío a la AEAT en segundo plano.
- Estimación trimestral de impuestos (modelo 130, 303 o 420) con sus plazos. Estima, no presenta.
- Tus datos cifrados en reposo, con copia de seguridad en un solo fichero cifrado.
- La aplicación está en español (y en inglés, italiano y alemán).

**Lo que tienes que saber, sin letra pequeña**
- Es **gratuita y auto-alojable** (Docker, o en tu portátil con SQLite en unos minutos). Su licencia es
  *source-available* (PolyForm Shield): puedes usarla gratis para tu negocio, leer y modificar el código,
  pero no ofrecerla como servicio competidor. **No es "open source" en sentido estricto.**
- La parte española está hecha contra los esquemas y especificaciones oficiales, pero **todavía no la he
  probado contra los entornos de pruebas reales de la AEAT ni de FACe**. Considérala una versión preliminar.
  Por eso busco a alguien que pueda probarla ahí y me cuente qué responde. Lo que está y no está verificado
  está en `Docs/SPAIN-EINVOICING.md`.
- No hay todavía facturas rectificativas ni simplificadas.

**Probarla sin instalar nada:** puedo darte acceso gratuito a mi instancia (<LINK-DEMO>) para que la veas
funcionando. Es una instancia de pruebas: usa datos de ejemplo, sin garantía de disponibilidad.

Código y guía de instalación: https://github.com/algiro/nInvoices

Si facturas por días u horas, me interesa mucho saber qué te falta. Críticas bienvenidas.

---

## 2. Short post (LinkedIn / X / Mastodon)

Facturar cada mes por días trabajados a mano es un rollo. Hice **nInvoices**: marcas los días en un
calendario, ves la factura real antes de crearla y sale el PDF + el parte de horas.
Incluye Facturae firmada, FACe y Verifactu (versión preliminar, busco quien la pruebe contra la AEAT).
Gratis y auto-alojable, código disponible (source-available). Puedo darte acceso a mi instancia de
pruebas. https://github.com/algiro/nInvoices

---

## 3. Post for developers (r/programacion, Telegram dev groups)

**Título:** Verifactu + Facturae + FACe en .NET 10: lo que aprendí implementándolos

Ángulos para el cuerpo (elige uno, no los tres):
- Cadena de hash de Verifactu y tabla *append-only* que el DbContext se niega a modificar.
- Firma XAdES-EPES de Facturae y validación contra el esquema oficial.
- Arquitectura: cada país es un plugin (`ICountryPlugin`), el núcleo nunca nombra un país.

Cierra con la petición concreta: "Si tienes acceso a los entornos de pruebas de AEAT/FACe, ¿puedes
probarlo y contarme qué responde?" y enlaza `Docs/SPAIN-EINVOICING.md`.

---

## Notes
- Lead with the pain and the demo; put the license sentence in the body, not the title.
- Attach the GIF or 3 screenshots (`Docs/screenshots/`).
- Do not paste the same text in several subreddits on the same day.
- Do not promise filing: the tax part estimates, it does not submit to the AEAT.
