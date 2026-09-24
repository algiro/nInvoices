/** Starter templates offered by "Load sample" in the template editor. */

export const invoiceSample = `<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
</head>
<body>
    <h1>INVOICE</h1>
    
    <h3>Invoice #[[ invoiceNumber ]]</h3>
    <p>Date: [[ date ]]</p>
    
    <h3>Bill To:</h3>
    <p>
        <strong>[[ customer.name ]]</strong><br>
        [[ customer.address.street ]]<br>
        [[ customer.address.city ]], [[ customer.address.postalCode ]]<br>
        VAT: [[ customer.fiscalId ]]
    </p>
    
    <h3>Services &amp; Charges</h3>
    <table>
        <tr>
            <th width="60%">Description</th>
            <th width="20%">Quantity</th>
            <th width="20%">Amount</th>
        </tr>
        
        [[ for item in lineItems ]]
        <tr>
            <td>[[ item.description ]]</td>
            <td>[[ item.quantity ]]</td>
            <td>[[ FormatCurrency item.amount currency ]]</td>
        </tr>
        [[ end ]]
        
        <tr>
            <td><strong>Subtotal</strong></td>
            <td></td>
            <td><strong>[[ FormatCurrency subtotal currency ]]</strong></td>
        </tr>
        
        [[ for tax in taxes ]]
        <tr>
            <td>[[ tax.description ]] ([[ FormatDecimal tax.rate 2 ]]%)</td>
            <td></td>
            <td>[[ FormatCurrency tax.amount currency ]]</td>
        </tr>
        [[ end ]]
        
        <tr>
            <td><h3>TOTAL</h3></td>
            <td></td>
            <td><h3>[[ FormatCurrency total currency ]]</h3></td>
        </tr>
    </table>
    
    [[ if workedDays ]]
    <p>
        <strong>Period:</strong> [[ monthDescription ]]<br>
        <strong>Days Worked:</strong> [[ workedDays ]]<br>
        <strong>Daily Rate:</strong> [[ FormatCurrency monthlyRate currency ]]
    </p>
    [[ end ]]
    
    <p>Thank you for your business!</p>
</body>
</html>`

export const monthlyReportSample = `<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Monthly Work Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 2rem; }
        h1 { color: #1e40af; border-bottom: 3px solid #1e40af; padding-bottom: 0.5rem; }
        .summary { margin: 2rem 0; padding: 1rem; background: #f3f4f6; border-radius: 0.5rem; }
        .summary-item { display: inline-block; margin-right: 2rem; }
        .summary-label { font-weight: bold; color: #4b5563; }
        table { width: 100%; border-collapse: collapse; margin-top: 2rem; }
        th { background: #1e40af; color: white; padding: 0.75rem; text-align: left; }
        td { padding: 0.75rem; border: 1px solid #e5e7eb; }
        .worked { background: #e8f5e9; }
        .public-holiday { background: #fff3e0; }
        .unpaid-leave { background: #ffebee; }
        .weekend { background: #f9fafb; color: #9ca3af; }
    </style>
</head>
<body>
    <h1>Monthly Work Report - [[ LocalizeMonth monthNumber locale ]] [[ year ]]</h1>
    
    <div class="summary">
        <div class="summary-item">
            <span class="summary-label">Cliente:</span> [[ customer.name ]]
        </div>
        <div class="summary-item">
            <span class="summary-label">Giorni Lavorati:</span> [[ workedDaysCount ]]
        </div>
        <div class="summary-item">
            <span class="summary-label">Festività:</span> [[ publicHolidayCount ]]
        </div>
        <div class="summary-item">
            <span class="summary-label">Permessi:</span> [[ unpaidLeaveCount ]]
        </div>
    </div>

    <table>
        <thead>
            <tr>
                <th>Date</th>
                <th>Giorno</th>
                <th>Status</th>
                <th>Ore</th>
                <th>Progetti</th>
            </tr>
        </thead>
        <tbody>
            [[ for day in monthDays ]]
            <tr class="[[ if day.isWeekend ]]weekend[[ else if day.isWorked ]]worked[[ else if day.isPublicHoliday ]]public-holiday[[ else if day.isUnpaidLeave ]]unpaid-leave[[ end ]]">
                <td>[[ day.dateValue | date.to_string "%Y-%m-%d" ]]</td>
                <td>[[ LocalizeDayOfWeek day.dateValue locale ]]</td>
                <td>[[ day.type ]]</td>
                <td>[[ if day.hours ]][[ day.hours ]][[ end ]]</td>
                <td>[[ for p in day.projects ]][[ p.name ]] ([[ p.hours ]]h)[[ if !for.last ]], [[ end ]][[ end ]]</td>
            </tr>
            [[ end ]]
        </tbody>
    </table>

    [[ if projectSummary.size > 0 ]]
    <h2>Riepilogo per Progetto</h2>
    <table>
        <thead>
            <tr>
                <th>Progetto</th>
                <th>Giorni</th>
                <th>Ore</th>
                <th>Importo</th>
            </tr>
        </thead>
        <tbody>
            [[ for p in projectSummary ]]
            <tr>
                <td>[[ p.name ]]</td>
                <td>[[ p.workedDays ]]</td>
                <td>[[ p.totalHours ]]</td>
                <td>[[ if p.amount ]][[ FormatCurrency p.amount (currency ?? "EUR") ]][[ end ]]</td>
            </tr>
            [[ end ]]
        </tbody>
    </table>
    [[ end ]]
</body>
</html>`

export const monthlyReportSampleName = 'Standard Monthly Report (Italian)'
