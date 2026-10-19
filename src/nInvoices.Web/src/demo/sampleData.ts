import type { DataExport } from '@/api/importExport'
import { InvoiceType, RateType, TaxApplicationType, DayType } from '@/types'
import { emailSampleName, emailSampleSubject, emailSampleBody } from '@/components/templates/editor/templateSamples'
import { sampleInvoiceTemplate, sampleTimesheetTemplate, sampleTimesheetName } from './sampleTemplates'

/** Fiscal ids of the sample customers: a customer with one of these is sample data. */
export const SAMPLE_FISCAL_IDS = ['SAMPLE-0001', 'SAMPLE-0002'] as const

const iso = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`

/** The weekdays of the `monthsBack`-th month before `today`'s (1 = last month). */
function weekdaysOfMonthBefore(today: Date, monthsBack: number): Date[] {
  const first = new Date(today.getFullYear(), today.getMonth() - monthsBack, 1)
  const days: Date[] = []
  for (let d = new Date(first); d.getMonth() === first.getMonth(); d.setDate(d.getDate() + 1)) {
    if (d.getDay() !== 0 && d.getDay() !== 6) days.push(new Date(d))
  }
  return days
}

/**
 * Fictional customers with rates, taxes, projects and two months of worked days, plus the starter
 * templates, in the shape the import accepts. Nothing is invoiced: generating the invoice is the demo.
 * All names, companies and amounts are made up.
 */
export function buildSampleData(today: Date): DataExport {
  const now = today.toISOString()
  const months = [2, 1]

  const studioDays = months.flatMap(back => weekdaysOfMonthBefore(today, back)).map((date, i) => ({
    date: iso(date),
    dayType: DayType.Worked,
    hoursWorked: null,
    notes: null,
    rateIndex: null,
    // Every third day is split between the two projects
    projects: i % 3 === 2
      ? [{ projectName: 'Platform', hours: 5 }, { projectName: 'Mobile app', hours: 3 }]
      : [{ projectName: 'Platform', hours: 8 }]
  }))

  const labsDays = months.flatMap(back => weekdaysOfMonthBefore(today, back))
    .filter(date => date.getDay() >= 2 && date.getDay() <= 4)
    .map(date => ({
      date: iso(date),
      dayType: DayType.Worked,
      hoursWorked: 6,
      notes: null,
      rateIndex: null,
      projects: [{ projectName: 'Data pipeline', hours: 6 }]
    }))

  const vat = {
    taxId: 'VAT',
    description: 'VAT 20%',
    handlerId: 'PERCENTAGE',
    rate: 20,
    applicationType: TaxApplicationType.OnSubtotal,
    appliedToTaxId: null,
    order: 1,
    isActive: true,
    createdAt: now
  }

  return {
    exportVersion: '1.0',
    exportedAt: now,
    customers: [
      {
        name: 'Northwind Studio (sample)',
        fiscalId: SAMPLE_FISCAL_IDS[0],
        address: { street: 'Example Street', houseNumber: '12', city: 'Berlin', zipCode: '10115', country: 'Germany' },
        createdAt: now,
        rates: [{ type: RateType.Daily, price: { amount: 480, currency: 'EUR' }, createdAt: now, isActive: true }],
        taxes: [vat],
        invoiceTemplates: [],
        monthlyReportTemplates: [],
        email: 'accounts@northwind.example',
        locale: 'en-US',
        projects: [{ name: 'Platform', isActive: true }, { name: 'Mobile app', isActive: true }],
        workDays: studioDays
      },
      {
        name: 'Acme Labs (sample)',
        fiscalId: SAMPLE_FISCAL_IDS[1],
        address: { street: 'Sample Road', houseNumber: '7', city: 'London', zipCode: 'EC1A 1BB', country: 'United Kingdom' },
        createdAt: now,
        rates: [{ type: RateType.Hourly, price: { amount: 75, currency: 'EUR' }, createdAt: now, isActive: true }],
        taxes: [],
        invoiceTemplates: [],
        monthlyReportTemplates: [],
        email: 'billing@acme-labs.example',
        locale: 'en-US',
        projects: [{ name: 'Data pipeline', isActive: true }],
        workDays: labsDays
      }
    ],
    sharedTemplates: {
      invoiceTemplates: [{
        invoiceType: InvoiceType.Monthly,
        name: 'Standard invoice',
        content: sampleInvoiceTemplate,
        isActive: true,
        createdAt: now
      }],
      monthlyReportTemplates: [{
        invoiceType: InvoiceType.Monthly,
        name: sampleTimesheetName,
        content: sampleTimesheetTemplate,
        isActive: true,
        createdAt: now
      }],
      emailTemplates: [{
        name: emailSampleName,
        subject: emailSampleSubject,
        body: emailSampleBody,
        isActive: true,
        createdAt: now
      }]
    }
  }
}
