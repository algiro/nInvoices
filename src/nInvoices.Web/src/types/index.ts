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
  address: AddressDto;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateCustomerDto {
  name: string;
  fiscalId: string;
  address: AddressDto;
}

export interface UpdateCustomerDto {
  name: string;
  fiscalId: string;
  address: AddressDto;
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

export interface WorkDayDto {
  date: string;
  dayType?: DayType;
  hoursWorked?: number;
  notes?: string;
}

export interface ExpenseDto {
  description: string;
  amount: number;
  currency: string;
  date: string;
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
