import { apiClient } from './client';
import type {
  CustomerHolidaysDto,
  HolidayCalendarDto,
  HolidayCountryDto,
  HolidayRuleDto,
  SaveHolidayRuleDto
} from '../types';

/**
 * Public holiday calendars per country, and the holidays that apply to a customer.
 */
export const holidaysApi = {
  /** Countries with built-in rules or a stored calendar. */
  async getCountries(): Promise<HolidayCountryDto[]> {
    return apiClient.get<HolidayCountryDto[]>('/api/holidays/countries');
  },

  /** A country's rules and the holidays they give in `year`. */
  async getCalendar(countryCode: string, year: number): Promise<HolidayCalendarDto> {
    return apiClient.get<HolidayCalendarDto>(`/api/holidays/${countryCode}`, { year });
  },

  /** The customer's public holidays in a month. */
  async getForCustomer(customerId: number, year: number, month: number): Promise<CustomerHolidaysDto> {
    return apiClient.get<CustomerHolidaysDto>(`/api/holidays/customer/${customerId}`, { year, month });
  },

  async createRule(countryCode: string, rule: SaveHolidayRuleDto): Promise<HolidayRuleDto> {
    return apiClient.post<HolidayRuleDto>(`/api/holidays/${countryCode}/rules`, rule);
  },

  async updateRule(id: number, rule: SaveHolidayRuleDto): Promise<HolidayRuleDto> {
    return apiClient.put<HolidayRuleDto>(`/api/holidays/rules/${id}`, rule);
  },

  async deleteRule(id: number): Promise<void> {
    return apiClient.delete<void>(`/api/holidays/rules/${id}`);
  },

  /** Replaces the country's rules with the built-in ones. */
  async reset(countryCode: string, year: number): Promise<HolidayCalendarDto> {
    return apiClient.post<HolidayCalendarDto>(`/api/holidays/${countryCode}/reset?year=${year}`);
  }
};
