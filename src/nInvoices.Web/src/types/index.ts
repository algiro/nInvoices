// The API's types come from its OpenAPI document (openapi.json → api.generated.ts, `npm run types`):
// here they get their usual names. Edit the C# DTOs, not these; only the frontend-only types at the
// end are written by hand.
import type { components } from './api.generated'

type Schemas = components['schemas']

// ---------- enums: travel by name ----------

export type InvoiceType = Schemas['InvoiceType']
export const InvoiceType = {
  Monthly: 'Monthly',
  OneTime: 'OneTime',
  Quarterly: 'Quarterly',
  Annual: 'Annual'
} as const satisfies Record<InvoiceType, InvoiceType>

export type TaxApplicationType = Schemas['TaxApplicationType']
export const TaxApplicationType = {
  OnSubtotal: 'OnSubtotal',
  OnTax: 'OnTax'
} as const satisfies Record<TaxApplicationType, TaxApplicationType>

export type InvoiceStatus = Schemas['InvoiceStatus']
export const InvoiceStatus = {
  Draft: 'Draft',
  Finalized: 'Finalized',
  Sent: 'Sent',
  Paid: 'Paid',
  Cancelled: 'Cancelled'
} as const satisfies Record<InvoiceStatus, InvoiceStatus>

export type DayType = Schemas['DayType']
export const DayType = {
  Worked: 'Worked',
  PublicHoliday: 'PublicHoliday',
  UnpaidLeave: 'UnpaidLeave'
} as const satisfies Record<DayType, DayType>

export type RateType = Schemas['RateType']
export const RateType = {
  Daily: 'Daily',
  Monthly: 'Monthly',
  Hourly: 'Hourly',
  Fixed: 'Fixed'
} as const satisfies Record<RateType, RateType>

export type HolidayRuleKind = Schemas['HolidayRuleKind']
export const HolidayRuleKind = {
  Fixed: 'Fixed',
  EasterOffset: 'EasterOffset',
  NthWeekday: 'NthWeekday'
} as const satisfies Record<HolidayRuleKind, HolidayRuleKind>

export type ExpenseCategory = Schemas['ExpenseCategory']
export const ExpenseCategory = {
  Other: 'Other',
  SocialSecurity: 'SocialSecurity',
  ProfessionalServices: 'ProfessionalServices',
  Software: 'Software',
  Equipment: 'Equipment',
  Office: 'Office',
  Travel: 'Travel',
  Training: 'Training',
  Insurance: 'Insurance',
  Banking: 'Banking'
} as const satisfies Record<ExpenseCategory, ExpenseCategory>

// Labels shown in the UI
export const InvoiceTypeNames: Record<InvoiceType, string> = {
  Monthly: 'Monthly',
  OneTime: 'One-Time',
  Quarterly: 'Quarterly',
  Annual: 'Annual'
}

export const InvoiceStatusNames: Record<InvoiceStatus, string> = {
  Draft: 'Draft',
  Finalized: 'Finalized',
  Sent: 'Sent',
  Paid: 'Paid',
  Cancelled: 'Cancelled'
}

export const DayTypeNames: Record<DayType, string> = {
  Worked: 'Worked',
  PublicHoliday: 'Public Holiday',
  UnpaidLeave: 'Unpaid Leave'
}

export const RateTypeNames: Record<RateType, string> = {
  Daily: 'Daily',
  Monthly: 'Monthly',
  Hourly: 'Hourly',
  Fixed: 'Fixed'
}

export const ExpenseCategoryNames: Record<ExpenseCategory, string> = {
  Other: 'Other',
  SocialSecurity: 'Social security',
  ProfessionalServices: 'Professional services',
  Software: 'Software and subscriptions',
  Equipment: 'Equipment',
  Office: 'Office and utilities',
  Travel: 'Travel',
  Training: 'Training',
  Insurance: 'Insurance',
  Banking: 'Bank and payment fees'
}

// ---------- the API's data ----------

export type MoneyDto = Schemas['MoneyDto']
export type AddressDto = Schemas['AddressDto']
export type CustomerDto = Schemas['CustomerDto']
export type CreateCustomerDto = Schemas['CreateCustomerDto']
export type UpdateCustomerDto = Schemas['UpdateCustomerDto']
export type RateDto = Schemas['RateDto']
export type CreateRateDto = Schemas['CreateRateDto']
export type UpdateRateDto = Schemas['UpdateRateDto']
export type TaxDto = Schemas['TaxDto']
export type CreateTaxDto = Schemas['CreateTaxDto']
export type UpdateTaxDto = Schemas['UpdateTaxDto']
export type InvoiceTemplateDto = Schemas['InvoiceTemplateDto']
export type CreateInvoiceTemplateDto = Schemas['CreateInvoiceTemplateDto']
export type UpdateInvoiceTemplateDto = Schemas['UpdateInvoiceTemplateDto']
export type ProjectDto = Schemas['ProjectDto']
export type CreateProjectDto = Schemas['CreateProjectDto']
export type UpdateProjectDto = Schemas['UpdateProjectDto']
export type DeleteProjectResultDto = Schemas['DeleteProjectResultDto']
export type WorkDayProjectDto = Schemas['WorkDayProjectDto']
export type WorkDayDto = Schemas['WorkDayDto']
export type ExpenseDto = Schemas['ExpenseDto']
export type InvoiceDto = Schemas['InvoiceDto']
export type GenerateInvoiceDto = Schemas['GenerateInvoiceDto']
export type InvoiceNumberingDto = Schemas['InvoiceNumberingDto']
export type UpdateInvoiceNumberingDto = Schemas['UpdateInvoiceNumberingDto']
export type InvoiceDraftPreviewDto = Schemas['InvoiceDraftPreviewDto']
export type UpdateInvoiceDto = Schemas['UpdateInvoiceDto']
export type TemplateValidationResultDto = Schemas['TemplateValidationResultDto']
export type MonthlyReportTemplateDto = Schemas['MonthlyReportTemplateDto']
export type CreateMonthlyReportTemplateDto = Schemas['CreateMonthlyReportTemplateDto']
export type UpdateMonthlyReportTemplateDto = Schemas['UpdateMonthlyReportTemplateDto']
export type EmailTemplateDto = Schemas['EmailTemplateDto']
export type CreateEmailTemplateDto = Schemas['CreateEmailTemplateDto']
export type UpdateEmailTemplateDto = Schemas['UpdateEmailTemplateDto']
export type EmailTemplatePreviewDto = Schemas['EmailTemplatePreviewDto']
export type GmailStatusDto = Schemas['GmailStatusDto']
export type ComplianceFieldDto = Schemas['ComplianceFieldDto']
export type SigningCertificateDto = Schemas['SigningCertificateDto']
export type ComplianceSettingsDto = Schemas['ComplianceSettingsDto']
export type ComplianceCountryDto = Schemas['ComplianceCountryDto']
export type UpdateComplianceSettingsDto = Schemas['UpdateComplianceSettingsDto']
export type InvoiceVerifactuDto = Schemas['InvoiceVerifactuDto']
export type VerifactuStatusDto = Schemas['VerifactuStatusDto']
export type VerifactuSubmissionRunDto = Schemas['VerifactuSubmissionRunDto']
export type ChainReportDto = Schemas['ChainReportDto']
export type ComplianceIssueDto = Schemas['ComplianceIssueDto']
export type InvoiceEInvoiceDto = Schemas['InvoiceEInvoiceDto']
export type EInvoiceSubmissionDto = Schemas['EInvoiceSubmissionDto']
export type EInvoiceChannelDto = Schemas['EInvoiceChannelDto']
export type EInvoiceDeliveryDto = Schemas['EInvoiceDeliveryDto']
export type EInvoiceGenerationDto = Schemas['EInvoiceGenerationDto']
export type EmailTemplateOptionDto = Schemas['EmailTemplateOptionDto']
export type EmailAttachmentOptionDto = Schemas['EmailAttachmentOptionDto']
export type InvoiceEmailComposeDto = Schemas['InvoiceEmailComposeDto']
export type CreateInvoiceEmailDraftDto = Schemas['CreateInvoiceEmailDraftDto']
export type InvoiceEmailDto = Schemas['InvoiceEmailDto']
export type HolidayRuleDto = Schemas['HolidayRuleDto']
export type SaveHolidayRuleDto = Schemas['SaveHolidayRuleDto']
export type PublicHolidayDto = Schemas['PublicHolidayDto']
export type HolidayCalendarDto = Schemas['HolidayCalendarDto']
export type HolidayCountryDto = Schemas['HolidayCountryDto']
export type CustomerHolidaysDto = Schemas['CustomerHolidaysDto']
export type InvoiceSortField = Schemas['InvoiceSortField']
export type InvoicePageDto = Schemas['InvoicePageDto']
export type InvoiceAmountDto = Schemas['InvoiceAmountDto']
export type InvoiceSummaryDto = Schemas['InvoiceSummaryDto']
export type BulkInvoiceResultDto = Schemas['BulkInvoiceResultDto']
export type TaxPredictionDto = Schemas['TaxPredictionDto']
export type TaxPeriodDto = Schemas['TaxPeriodDto']
export type TaxFormDto = Schemas['TaxFormDto']
export type TaxFormLineDto = Schemas['TaxFormLineDto']
export type TaxPaymentDto = Schemas['TaxPaymentDto']
export type RecordTaxPaymentDto = Schemas['RecordTaxPaymentDto']
export type TaxPredictionSettingsDto = Schemas['TaxPredictionSettingsDto']
export type TaxCountryDto = Schemas['TaxCountryDto']
export type TaxRegimeDto = Schemas['TaxRegimeDto']
export type TaxProfileDto = Schemas['TaxProfileDto']
export type UpdateTaxProfileDto = Schemas['UpdateTaxProfileDto']
export type BusinessExpenseDto = Schemas['BusinessExpenseDto']
export type SaveBusinessExpenseDto = Schemas['SaveBusinessExpenseDto']
export type RecurringExpenseDto = Schemas['RecurringExpenseDto']
export type SaveRecurringExpenseDto = Schemas['SaveRecurringExpenseDto']

// ---------- frontend-only ----------

export interface TemplatePreviewDto {
  /** Rendered HTML, or null when the template could not be rendered. */
  html: string | null;
  errors: string[];
}

// API Response types
export interface ApiError {
  error: string;
  details?: string[];
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export type ComplianceFieldType = 'Text' | 'Choice' | 'Boolean';

export type Weekday = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'

/** Filters, sort and page of the invoice list (query-string parameters of /api/invoices/search). */
export interface InvoiceSearchParams {
  status?: string;
  customerId?: number;
  type?: string;
  year?: number;
  search?: string;
  sort?: InvoiceSortField;
  dir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

export type BulkInvoiceChange = 'finalize' | 'mark-as-sent' | 'mark-as-paid'
