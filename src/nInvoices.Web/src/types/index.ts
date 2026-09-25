// TypeScript interfaces matching backend DTOs
// Ensures type safety across frontend-backend communication

export enum InvoiceType {
  Monthly = 0,
  OneTime = 1,
  Quarterly = 2,
  Annual = 3
}

export enum TaxApplicationType {
  OnSubtotal = 0,
  OnTax = 1
}

export enum InvoiceStatus {
  Draft = 0,
  Finalized = 1,
  Sent = 2,
  Paid = 3,
  Cancelled = 4
}

export enum DayType {
  Worked = 0,
  PublicHoliday = 1,
  UnpaidLeave = 2
}

// Helper functions to convert enum numbers to display strings
export const InvoiceTypeNames: Record<InvoiceType, string> = {
  [InvoiceType.Monthly]: 'Monthly',
  [InvoiceType.OneTime]: 'One-Time',
  [InvoiceType.Quarterly]: 'Quarterly',
  [InvoiceType.Annual]: 'Annual'
};

export const InvoiceStatusNames: Record<InvoiceStatus, string> = {
  [InvoiceStatus.Draft]: 'Draft',
  [InvoiceStatus.Finalized]: 'Finalized',
  [InvoiceStatus.Sent]: 'Sent',
  [InvoiceStatus.Paid]: 'Paid',
  [InvoiceStatus.Cancelled]: 'Cancelled'
};

export const DayTypeNames: Record<DayType, string> = {
  [DayType.Worked]: 'Worked',
  [DayType.PublicHoliday]: 'Public Holiday',
  [DayType.UnpaidLeave]: 'Unpaid Leave'
};

export enum RateType {
  Daily = 'Daily',
  Monthly = 'Monthly',
  Hourly = 'Hourly'
}

export const RateTypeNames: Record<RateType, string> = {
  [RateType.Daily]: 'Daily',
  [RateType.Monthly]: 'Monthly',
  [RateType.Hourly]: 'Hourly'
};

export interface MoneyDto {
  amount: number;
  currency: string;
}

export interface AddressDto {
  street: string;
  houseNumber: string;
  city: string;
  zipCode: string;
  country: string;
  state?: string;
}

export interface CustomerDto {
  id: number;
  name: string;
  fiscalId: string;
  locale: string;
  address: AddressDto;
  createdAt: string;
  updatedAt?: string;
  /** Default recipient of invoice emails */
  email?: string | null;
  /** Comma-separated addresses copied on invoice emails */
  ccEmails?: string | null;
  /** Country (ISO code) whose public holidays apply; null to follow the address */
  holidayCountry?: string | null;
  /** The holiday country that applies: the chosen one, or the address country's code (null when not recognized) */
  effectiveHolidayCountry?: string | null;
}

export interface CreateCustomerDto {
  name: string;
  fiscalId: string;
  address: AddressDto;
  locale: string;
  email?: string | null;
  ccEmails?: string | null;
  holidayCountry?: string | null;
}

export interface UpdateCustomerDto {
  name: string;
  fiscalId: string;
  address: AddressDto;
  locale: string;
  email?: string | null;
  ccEmails?: string | null;
  holidayCountry?: string | null;
}

export interface RateDto {
  id: number;
  customerId: number;
  type: RateType;
  price: MoneyDto;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateRateDto {
  customerId: number;
  type: RateType;
  price: MoneyDto;
}

export interface UpdateRateDto {
  type: RateType;
  price: MoneyDto;
}

export interface TaxDto {
  id: number;
  customerId: number;
  taxId: string;
  description: string;
  handlerId: string;
  rate: number;
  appliedToTaxId?: number;
  order: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateTaxDto {
  customerId: number;
  taxId: string;
  description: string;
  handlerId: string;
  rate: number;
  applicationType: TaxApplicationType;
  appliedToTaxId?: number | null;
  order: number;
}

export interface UpdateTaxDto {
  taxId: string;
  description: string;
  handlerId: string;
  rate: number;
  appliedToTaxId?: number;
  order: number;
  isActive: boolean;
}

export interface InvoiceTemplateDto {
  id: number;
  customerId: number;
  invoiceType: InvoiceType;
  name: string;
  content: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateInvoiceTemplateDto {
  customerId: number;
  invoiceType: InvoiceType;
  name: string;
  content: string;
}

export interface UpdateInvoiceTemplateDto {
  invoiceType: InvoiceType;
  name: string;
  content: string;
  isActive: boolean;
}

export interface ProjectDto {
  id: number;
  customerId: number;
  name: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateProjectDto {
  customerId: number;
  name: string;
}

export interface UpdateProjectDto {
  name: string;
  isActive: boolean;
}

export interface DeleteProjectResultDto {
  found: boolean;
  deleted: boolean;
  deactivated: boolean;
}

export interface WorkDayProjectDto {
  projectName: string;
  hours: number;
  projectId?: number | null;
}

export interface WorkDayDto {
  date: string;
  dayType?: DayType;
  hoursWorked?: number;
  notes?: string;
  projects?: WorkDayProjectDto[];
}

export interface ExpenseDto {
  description: string;
  amount: number;
  currency: string;
  date?: string; // the API defaults to today
}

export interface InvoiceDto {
  id: number;
  customerId: number;
  type: InvoiceType | string;  // Backend serializes as string
  invoiceNumber: string;
  issueDate: string;
  dueDate?: string;
  workedDays?: number;
  year?: number;
  month?: number;
  monthlyReportTemplateId?: number;
  subtotal: MoneyDto;
  totalExpenses: MoneyDto;
  totalTaxes: MoneyDto;
  total: MoneyDto;
  status: InvoiceStatus | string;  // Backend serializes as string
  renderedContent?: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface GenerateInvoiceDto {
  customerId: number;
  invoiceType: InvoiceType;
  issueDate: string;
  year?: number;
  month?: number;
  workDays?: WorkDayDto[];
  expenses?: ExpenseDto[];
  invoiceNumberFormat?: string;
  monthlyReportTemplateId?: number;
}

/** The invoice and timesheet a GenerateInvoiceDto would produce, rendered but not saved. */
export interface InvoiceDraftPreviewDto {
  invoiceNumber: string | null;
  invoiceHtml: string | null;
  /** Why the invoice can't be generated (no rate, no active template, a template error). */
  errors: string[];
  timesheetHtml: string | null;
  /** Why the timesheet can't be rendered; it doesn't block generating the invoice. */
  timesheetError: string | null;
  subtotal: MoneyDto | null;
  totalExpenses: MoneyDto | null;
  totalTaxes: MoneyDto | null;
  total: MoneyDto | null;
  taxes: { description: string; rate: number; amount: MoneyDto }[];
}

export interface UpdateInvoiceDto {
  dueDate?: string;
  renderedContent?: string;
  notes?: string;
}

export interface TemplateValidationResultDto {
  isValid: boolean;
  errors: string[];
  placeholders: string[];
}

export interface TemplatePreviewDto {
  /** Rendered HTML, or null when the template could not be rendered. */
  html: string | null;
  errors: string[];
}

export interface MonthlyReportTemplateDto {
  id: number;
  customerId: number;
  invoiceType: InvoiceType;
  name: string;
  content: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateMonthlyReportTemplateDto {
  customerId: number;
  name: string;
  content: string;
  invoiceType?: InvoiceType;
}

export interface UpdateMonthlyReportTemplateDto {
  name: string;
  content: string;
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

// ---------- Invoice emails (Gmail drafts) ----------

export interface EmailTemplateDto {
  id: number;
  customerId: number;
  name: string;
  subject: string;
  body: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateEmailTemplateDto {
  customerId: number;
  name: string;
  subject: string;
  body: string;
}

export interface UpdateEmailTemplateDto {
  name: string;
  subject: string;
  body: string;
}

export interface EmailTemplatePreviewDto {
  subject: string | null;
  html: string | null;
  errors: string[];
}

export interface GmailStatusDto {
  /** False when the server has no Google OAuth client configured */
  configured: boolean;
  connected: boolean;
  emailAddress?: string | null;
  connectedAt?: string | null;
  lastUsedAt?: string | null;
}

export interface EmailTemplateOptionDto {
  /** null for the built-in default */
  id: number | null;
  name: string;
  isActive: boolean;
}

export interface EmailAttachmentOptionDto {
  key: 'invoice' | 'monthlyReport' | string;
  fileName: string;
  includedByDefault: boolean;
}

export interface InvoiceEmailComposeDto {
  invoiceId: number;
  templateId: number | null;
  templates: EmailTemplateOptionDto[];
  /** Connected Gmail address, or null when Gmail is not connected */
  from: string | null;
  to: string;
  cc: string | null;
  subject: string;
  body: string;
  attachments: EmailAttachmentOptionDto[];
  errors: string[];
}

export interface CreateInvoiceEmailDraftDto {
  to: string;
  cc?: string | null;
  subject: string;
  body: string;
  includeMonthlyReport: boolean;
}

export interface InvoiceEmailDto {
  id: number;
  invoiceId: number;
  from: string;
  to: string;
  cc?: string | null;
  subject: string;
  attachments: string[];
  gmailDraftId: string;
  /** Opens the draft in Gmail */
  gmailUrl: string;
  createdAt: string;
}

// ---------- public holidays ----------

export enum HolidayRuleKind {
  Fixed = 'Fixed',
  EasterOffset = 'EasterOffset',
  NthWeekday = 'NthWeekday'
}

export type Weekday = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'

/** A rule of a country's holiday calendar; only the fields used by `kind` are set. */
export interface HolidayRuleDto {
  id: number;
  name: string;
  kind: HolidayRuleKind;
  month: number | null;
  day: number | null;
  easterOffset: number | null;
  weekday: Weekday | null;
  /** 1-5, or -1 for the last */
  occurrence: number | null;
  fromYear: number | null;
  toYear: number | null;
  isActive: boolean;
}

export type SaveHolidayRuleDto = Omit<HolidayRuleDto, 'id'>

export interface PublicHolidayDto {
  date: string; // yyyy-MM-dd
  name: string;
}

export interface HolidayCalendarDto {
  countryCode: string;
  countryName: string;
  hasBuiltIn: boolean;
  rules: HolidayRuleDto[];
  year: number;
  holidays: PublicHolidayDto[];
}

export interface HolidayCountryDto {
  countryCode: string;
  countryName: string;
  hasBuiltIn: boolean;
  hasCalendar: boolean;
}

export interface CustomerHolidaysDto {
  countryCode: string | null;
  countryName: string | null;
  holidays: PublicHolidayDto[];
}

// ---------- invoice list ----------

export type InvoiceSortField = 'IssueDate' | 'Number' | 'Customer' | 'Period' | 'Status' | 'Total'

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

export interface InvoicePageDto {
  items: InvoiceDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  /** Matching invoices per status name, under every filter except the status one */
  statusCounts: Record<string, number>;
}

export interface InvoiceAmountDto {
  count: number;
  totals: MoneyDto[];
}

export interface InvoiceSummaryDto {
  totalCount: number;
  outstanding: InvoiceAmountDto;
  paidThisYear: InvoiceAmountDto;
  drafts: number;
  years: number[];
}

export type BulkInvoiceChange = 'finalize' | 'mark-as-sent' | 'mark-as-paid'

export interface BulkInvoiceResultDto {
  succeeded: number[];
  skipped: { id: number; invoiceNumber: string | null; reason: string }[];
}
