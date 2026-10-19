/** Polished starter templates for the sample data (look of Docs/default-invoice-template.html, in English, without its hard-coded taxes). */

export const sampleInvoiceTemplate = `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Invoice [[ invoiceNumber ]]</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            color: #333;
            padding: 40px;
            line-height: 1.4;
        }
        
        .header-wave {
            position: relative;
            background: linear-gradient(135deg, #89CFF0 0%, #4A90A4 100%);
            height: 100px;
            margin: -40px -40px 40px -40px;
            overflow: hidden;
        }
        
        .header-wave::before {
            content: '';
            position: absolute;
            bottom: -5px;
            left: 0;
            width: 100%;
            height: 60px;
            background: white;
            border-radius: 50% 50% 0 0 / 100% 100% 0 0;
        }
        
        .header-container {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 40px;
        }
        
        .invoice-title {
            font-size: 32px;
            font-weight: 700;
            color: #2E6B7F;
            margin-bottom: 5px;
        }
        
        .invoice-number {
            font-size: 18px;
            color: #5A9FB8;
        }
        
        .company-logo {
            text-align: right;
        }
        
        .company-name {
            display: flex;
            align-items: center;
            justify-content: flex-end;
            gap: 10px;
            font-size: 20px;
            font-weight: 600;
            color: #2E6B7F;
        }
        
        .logo-circle {
            width: 40px;
            height: 40px;
            border-radius: 50%;
            background: #5A9FB8;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-weight: bold;
        }
        
        .info-section {
            display: grid;
            grid-template-columns: 1fr 1fr 1fr;
            gap: 30px;
            margin-bottom: 40px;
        }
        
        .info-block {
            padding: 0;
        }
        
        .info-label {
            font-size: 11px;
            text-transform: uppercase;
            color: #5A9FB8;
            font-weight: 600;
            margin-bottom: 8px;
            letter-spacing: 0.5px;
        }
        
        .info-content {
            font-size: 14px;
            color: #333;
            line-height: 1.8;
        }
        
        .info-content strong {
            font-weight: 600;
            display: block;
            margin-bottom: 4px;
        }
        
        .info-value {
            color: #2E6B7F;
            font-weight: 600;
        }
        
        .summary-section {
            background: #F8FCFD;
            border-left: 4px solid #5A9FB8;
            padding: 10px 14px;
            margin-bottom: 20px;
        }
        
        .summary-title {
            font-size: 10px;
            text-transform: uppercase;
            color: #5A9FB8;
            font-weight: 600;
            margin-bottom: 6px;
            letter-spacing: 0.5px;
        }
        
        .summary-text {
            font-size: 11px;
            line-height: 1.4;
            color: #555;
            margin-bottom: 5px;
        }
        
        .summary-text:last-child {
            margin-bottom: 0;
        }
        
        .items-table {
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 30px;
        }
        
        .items-table thead {
            background: #E8F4F7;
        }
        
        .items-table th {
            padding: 12px;
            text-align: left;
            font-size: 11px;
            text-transform: uppercase;
            color: #5A9FB8;
            font-weight: 600;
            letter-spacing: 0.5px;
            border-bottom: 2px solid #5A9FB8;
        }
        
        .items-table th:last-child,
        .items-table td:last-child {
            text-align: right;
            white-space: nowrap;
            min-width: 120px;
        }
        
        .items-table th:nth-child(2),
        .items-table td:nth-child(2) {
            text-align: center;
        }
        
        .items-table tbody tr {
            border-bottom: 1px solid #E5E7EB;
        }
        
        .items-table td {
            padding: 14px 12px;
            font-size: 14px;
            color: #333;
        }
        
        .section-header-row {
            background: #F8FCFD !important;
            font-weight: 600;
            color: #2E6B7F;
        }
        
        .section-header-row td {
            padding: 10px 12px;
            font-size: 13px;
        }
        
        .totals-row {
            font-weight: 600;
            background: #F9FAFB;
        }
        
        .total-final-row {
            font-weight: 700;
            background: #E8F4F7;
            font-size: 13px;
        }
        
        .total-final-row td {
            padding: 6px 12px !important;
        }
        
        .footer-note {
            text-align: center;
            margin-top: 40px;
            padding-top: 30px;
            border-top: 2px solid #E8F4F7;
            color: #666;
            font-size: 13px;
        }
        
        [[ if workedDays ]]
        .period-info {
            background: #FFF9E6;
            border-left: 4px solid #FFD700;
            padding: 10px 14px;
            margin: 15px 0;
        }
        
        .period-info-title {
            font-size: 11px;
            text-transform: uppercase;
            color: #B8860B;
            font-weight: 600;
            margin-bottom: 6px;
            letter-spacing: 0.5px;
        }
        
        .period-info-grid {
            display: flex;
            align-items: center;
            gap: 20px;
        }
        
        .period-info-item {
            font-size: 12px;
            display: flex;
            align-items: center;
            gap: 8px;
        }
        
        .period-info-label {
            color: #666;
            font-size: 11px;
            white-space: nowrap;
        }
        
        .period-info-value {
            font-weight: 600;
            color: #333;
            font-size: 12px;
            white-space: nowrap;
        }
        [[ end ]]

        /* Verifactu QR code: 34 mm symbol with 3 mm of white around it, headed and followed by the required texts */
        .verifactu-qr {
            width: 46mm;
            margin: 0 0 6mm 0;
            text-align: center;
            font-size: 11px;
            font-weight: 600;
            color: #000;
        }
        .verifactu-qr-code {
            width: 40mm;
            height: 40mm;
            padding: 3mm;
            margin: 1mm auto;
            background: #fff;
        }
        .verifactu-qr-code svg {
            width: 100%;
            height: 100%;
        }
    </style>
</head>
<body>
    <div class="header-wave"></div>

    <div class="header-container">
        <div>
            <div class="invoice-title">Invoice</div>
            <div class="invoice-number">#[[ invoiceNumber ]]</div>
        </div>
        <div class="company-logo">
            <div class="company-name">
                <div class="logo-circle">AD</div>
            </div>
        </div>
    </div>

    <div class="info-section">
        <div class="info-block">
            <div class="info-label">From</div>
            <div class="info-content">
                <strong>Alex Demo, freelance developer</strong>
                1 Example Street<br>
                00000 Sampletown<br>
                Exampleland
            </div>
        </div>

        <div class="info-block">
            <div class="info-label">Bill to</div>
            <div class="info-content">
                <strong>[[ customer.name ]]</strong>
                [[ customer.address.street ]]<br>
                [[ customer.address.postalCode ]] [[ customer.address.city ]]<br>
                VAT: [[ customer.fiscalId ]]
            </div>
        </div>

        <div class="info-block">
            <div class="info-label">Invoice no.</div>
            <div class="info-content">
                <span class="info-value">[[ invoiceNumber ]]</span>
            </div>

            <div class="info-label" style="margin-top: 15px;">Invoice date</div>
            <div class="info-content">
                <span class="info-value">[[ FormatDate date "dd/MM/yyyy" ]]</span>
            </div>
        </div>
    </div>

    <table class="items-table">
        <thead>
            <tr>
                <th width="65%">Description</th>
                <th width="15%">Qty</th>
                <th width="20%">Amount</th>
            </tr>
        </thead>
        <tbody>
            [[ for item in lineItems ]]
            <tr>
                <td>[[ item.description ]]</td>
                <td>[[ item.quantity ]]</td>
                <td>[[ FormatCurrency item.amount currency ]]</td>
            </tr>
            [[ end ]]

            [[ for tax in taxes ]]
            <tr>
                <td colspan="2" style="text-align: right; padding-right: 20px;">
                    [[ tax.description ]] ([[ FormatDecimal tax.rate 2 ]]%)
                </td>
                <td>[[ FormatCurrency tax.amount currency ]]</td>
            </tr>
            [[ end ]]

            <tr class="total-final-row">
                <td colspan="2" style="text-align: right; padding-right: 20px;">TOTAL</td>
                <td>[[ FormatCurrency total currency ]]</td>
            </tr>
        </tbody>
    </table>

    <div class="period-info">
        <div class="period-info-title">Payment terms:</div>
        <div class="period-info-grid">
            <div class="period-info-item">
                <span class="period-info-label">Bank transfer</span>
            </div>
            <div class="period-info-item">
                <span class="period-info-label">IBAN:</span>
                <span class="period-info-value">Sample Bank &middot; XX00 0000 0000 0000 0000 0000</span>
            </div>
        </div>
    </div>

    <div class="summary-section">
        <div class="summary-text">Sample invoice with fictional data, made by nInvoices.</div>
    </div>
</body>
</html>`

export const sampleTimesheetTemplate = `<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Timesheet</title>
    <style>
        body { 
            font-family: Arial, sans-serif; 
            margin: 1rem 1.5rem; 
            font-size: 13px;
        }
        h1 { 
            color: #1e40af; 
            border-bottom: 2px solid #1e40af; 
            padding-bottom: 0.3rem; 
            margin-bottom: 0.8rem;
            font-size: 20px;
        }
        .summary { 
            margin: 0.8rem 0; 
            padding: 0.6rem 0.8rem; 
            background: #f3f4f6; 
            border-radius: 0.3rem; 
            font-size: 12px;
        }
        .summary-item { 
            display: inline-block; 
            margin-right: 1.5rem; 
        }
        .summary-label { 
            font-weight: bold; 
            color: #4b5563; 
        }
        table { 
            width: 100%; 
            border-collapse: collapse; 
            margin-top: 1rem; 
            font-size: 12px;
        }
        th { 
            background: #1e40af; 
            color: white; 
            padding: 0.4rem 0.6rem; 
            text-align: left; 
            font-size: 11px;
            font-weight: 600;
        }
        td { 
            padding: 0.35rem 0.6rem; 
            border: 1px solid #e5e7eb; 
        }
        .worked { background: #ccffcc; }
        .public-holiday { background: #fff3e0; }
        .unpaid-leave { background: #99ccff; }
        .weekend { background: #f9fafb; color: #9ca3af; }
    </style>
</head>
<body>
    <h1>Timesheet - [[ LocalizeMonth monthNumber "en-US" ]] [[ year ]]</h1>
    
    <div class="summary">
        <div class="summary-item">
            <span class="summary-label">Customer:</span> [[ customer.name ]]
        </div>
        <div class="summary-item">
            <span class="summary-label">Worked days:</span> [[ workedDaysCount ]]
        </div>
        <div class="summary-item">
            <span class="summary-label">Public holidays:</span> [[ publicHolidayCount ]]
        </div>
        <div class="summary-item">
            <span class="summary-label">Unpaid leave:</span> [[ unpaidLeaveCount ]]
        </div>
    </div>

    <table>
        <thead>
            <tr>
                <th>Date</th>
                <th>Day</th>
                <th>Hours</th>
            </tr>
        </thead>
        <tbody>
            [[ for day in monthDays ]]
            <tr class="[[ if day.isWeekend ]]weekend[[ else if day.isWorked ]]worked[[ else if day.isPublicHoliday ]]public-holiday[[ else if day.isUnpaidLeave ]]unpaid-leave[[ end ]]">
                <td>[[ day.dateValue | date.to_string "%d-%m-%Y" ]]</td>
                <td>[[ LocalizeDayOfWeek day.dateValue "en-US" ]]</td>
                <td> [[ if day.type == "Worked" ]] 8 [[ else if day.type == "PublicHoliday" ]] Holiday [[ else ]] - [[ end ]] </td>
            </tr>
            [[ end ]]
        </tbody>
    </table>
</body>
</html>`

export const sampleTimesheetName = 'Standard monthly timesheet'
