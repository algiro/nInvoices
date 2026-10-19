# Italy launch posts (drafts)

Before posting: check each community's self-promotion rules, and reply to comments for the first day.
Do not state deadlines; send readers to the Agenzia delle Entrate for those. Replace `<LINK-DEMO>` and `<GIF>`.

---

## 1. Long post (r/ItaliaPersonalFinance, r/partitaiva-style groups, forum P.IVA)

**Titolo:** Ho fatto un gestionale di fatture gratuito e self-hosted per chi fattura a giornata/ore (con FatturaPA, forfettario e ordinario)

Sono un freelance e fatturare ogni mese a mano (giorni lavorati, festività, IVA, ritenuta, bollo, PDF, mail)
era noioso, quindi ho costruito **nInvoices**, pensato per chi fattura **tempo** (a giornata o a ore).

**Cosa fa**
- Segni i giorni lavorati su un calendario (le festività sono già indicate) e vedi la **fattura vera prima
  di crearla**. PDF di fattura e timesheet da un tuo modello HTML.
- Tariffe a giornata/ora/mese, progetti per cliente, spese, più valute.
- **FatturaPA 1.2 (FPR12)** generata per ogni fattura e validata contro lo schema ufficiale. Non è
  firmata: la carichi tu sul SDI.
- Regime **forfettario (RF19)** e **ordinario (RF01)**, Partita IVA e codice fiscale con cifra di controllo,
  codice destinatario o PEC del cliente, natura dello 0% IVA.
- **Rivalsa INPS**, **ritenuta d'acconto** e **bollo virtuale** (2 € sopra 77,47 €) calcolati, con il netto da pagare.
- Stima trimestrale delle imposte (imposta sostitutiva o IRPEF, INPS Gestione Separata, acconti, IVA
  trimestrale). Stima, non presenta nulla.
- Dati cifrati a riposo, backup in un solo file cifrato. L'app è disponibile anche in italiano.

**Senza giri di parole**
- Gratuita e **self-hosted** (Docker, oppure sul tuo PC con SQLite in pochi minuti). Licenza
  *source-available* (PolyForm Shield): uso gratuito per la tua attività, codice leggibile e modificabile,
  ma non puoi offrirla come servizio concorrente. **Non è open source in senso stretto.**
- I file passano lo schema ufficiale, ma **non sono ancora passati dal SDI reale**. Considerala una
  versione preliminare. Cerco proprio qualcuno che provi a mandare un file e mi racconti la risposta.
- Non ci sono ancora le fatture verso la PA (FPA12).
- Gran parte del codice è stata scritta con l'aiuto dell'IA (Claude Code); io lo progetto, lo rivedo e lo
  collaudo, e dietro ci sono molti test automatici e la CI.

**Provarla senza installare nulla:** posso darti accesso gratuito alla mia istanza (<LINK-DEMO>), con dati di
prova e senza garanzia di disponibilità.

Codice e guida: https://github.com/algiro/nInvoices

Se fatturi a giornata o a ore, dimmi cosa ti manca.

---

## 2. Short post (LinkedIn / X)

Fatturare ogni mese a giornata, a mano? Ho fatto **nInvoices**: segni i giorni su un calendario, vedi la
fattura vera prima di crearla ed esce PDF + timesheet. FatturaPA, forfettario/ordinario, rivalsa INPS,
ritenuta e bollo inclusi (versione preliminare, cerco chi la provi col SDI). Gratis e self-hosted, codice
visibile (source-available). https://github.com/algiro/nInvoices
