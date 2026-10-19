# Germany launch posts (drafts)

Before posting: check each community's self-promotion rules, and reply to comments for the first day.
Verify the current E-Rechnung obligations (receiving vs issuing, transition periods) on the
Bundesfinanzministerium site before quoting any date; the drafts name none. Replace `<LINK-DEMO>`, `<GIF>`.

---

## 1. Long post (r/de_EDV, r/selbststaendig, Freelancer-Foren)

**Titel:** Kostenlose, selbst gehostete Rechnungs-Software für Freiberufler mit Tages-/Stundenabrechnung (XRechnung, Kleinunternehmer, USt-IdNr)

Ich bin selbstständig und wollte die monatliche Rechnung nicht mehr in Excel/Word zusammenbauen: Arbeitstage
zählen, Feiertage abziehen, Steuer berechnen, PDF exportieren, Mail schreiben. Deshalb habe ich
**nInvoices** gebaut, für alle, die **Zeit** (Tage oder Stunden) abrechnen.

**Was es kann**
- Arbeitstage im Kalender markieren (Feiertage sind vorbelegt) und die **echte Rechnung ansehen, bevor sie
  erstellt wird**. PDF für Rechnung und Stundennachweis aus einer eigenen HTML-Vorlage.
- Tages-, Stunden- und Monatssätze, Projekte pro Kunde, Auslagen, mehrere Währungen.
- **XRechnung 3.0** (UBL 2.1) auf Wunsch für eine festgeschriebene Rechnung. In den Tests gegen das
  UBL-2.1-Schema und mit dem KoSIT-Validator (EN 16931 + XRechnung-Schematron) geprüft.
- Steuernummer bzw. USt-IdNr (§ 14 UStG) mit Prüfung, **Kleinunternehmer** (§ 19 UStG) mit Hinweis,
  Leitweg-ID und E-Rechnungs-E-Mail des Kunden, Grund für jede 0-%-Rechnung (Reverse Charge, innergemeinschaftliche
  Lieferung, Export, nicht steuerbar ...).
- Steuerschätzung (Einkommensteuer, Soli, Kirchensteuer, Gewerbesteuer, Vorauszahlungen, USt-Voranmeldungen).
  Es schätzt, es reicht nichts ein.
- Daten verschlüsselt gespeichert, Backup in einer verschlüsselten Datei. Die App gibt es auch auf Deutsch.

**Ehrlich gesagt**
- Kostenlos und **selbst gehostet** (Docker oder lokal mit SQLite in wenigen Minuten). Die Lizenz ist
  *source-available* (PolyForm Shield): kostenlos für das eigene Geschäft, Code les- und änderbar, aber nicht
  als Konkurrenzdienst anbietbar. **Kein Open Source im strengen Sinn.**
- Die XRechnung besteht die offiziellen Prüfungen, **wurde aber noch nicht in einem echten Empfängerportal
  eingereicht**. Bitte als Vorabversion verstehen. Wer eine Datei an ein Portal schicken kann und mir die
  Antwort berichtet, hilft mir am meisten.
- ZUGFeRD und Gutschriften fehlen noch.
- Ein großer Teil des Codes entstand mit KI-Unterstützung (Claude Code); ich entwerfe, prüfe und teste ihn,
  dahinter stehen viele automatisierte Tests und CI.

**Ohne Installation testen:** Ich gebe gern kostenlosen Zugang zu meiner Instanz (<LINK-DEMO>): Testdaten,
keine Verfügbarkeitsgarantie.

Code und Anleitung: https://github.com/algiro/nInvoices

Wenn du nach Tagen oder Stunden abrechnest: Was fehlt dir?

---

## 2. Short post (LinkedIn / XING / Mastodon)

Rechnung nach Arbeitstagen jeden Monat von Hand? **nInvoices**: Tage im Kalender markieren, die echte Rechnung
vorab sehen, PDF + Stundennachweis erhalten. Mit XRechnung 3.0 (KoSIT-geprüft), Kleinunternehmer und
USt-IdNr-Prüfung (Vorabversion, ich suche Tester für Empfängerportale). Kostenlos, selbst gehostet,
Code einsehbar (source-available). https://github.com/algiro/nInvoices
